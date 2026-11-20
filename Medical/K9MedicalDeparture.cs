using System;
using System.Collections.Generic;
using System.Linq;
using Rage;
using Rage.Native;
using AdvancedK9.Medical;
using AdvancedK9.API;

namespace AdvancedK9
{
    internal sealed partial class K9Controller
    {
        private sealed class MedicalDeparture
        {
            internal Vehicle Ambulance;
            internal Ped Driver,Partner,Patient;
            internal bool PursuitHandoffConfirmed, HandoffInProgress;
            internal Rage.Object Stretcher;
            internal bool PatientOnStretcher, PatientOriginallyRagdoll;
            internal float PatientDeckOffset, PatientHeadingOffset;
            internal Vector3 StretcherMinimum, StretcherMaximum;
            internal bool Hospital,Departed,Cancelled,RetryRequested;
            internal string K9;
            internal int Attempts;
        }
        private readonly List<MedicalDeparture> _medicalDepartures=new List<MedicalDeparture>();
        private int _medicalAssessmentGeneration;
        private readonly HashSet<PoolHandle> _hospitalCareRequired=new HashSet<PoolHandle>();
        private uint _lastMedicalCode4;

        private void MaintainMedicalDepartures()
        {
            foreach(var crew in _medicalDepartures.Where(c=>c.Hospital&&!c.Departed&&!c.Cancelled))
                if(ValidSuspect(crew.Patient)&&Living(crew.Patient)&&!crew.PursuitHandoffConfirmed&&!crew.HandoffInProgress)_pr.RefreshK9Custody(crew.Patient,"HospitalTransport");
        }

        private void RequestMedicalSceneClear()
        {
            if(_lastMedicalCode4!=0&&Game.GameTime-_lastMedicalCode4<2000)return;
            _lastMedicalCode4=Game.GameTime;
            foreach(var crew in _medicalDepartures.Where(c=>!c.Cancelled&&!c.Departed))crew.RetryRequested=true;
            Game.LogTrivial("AdvancedK9 EMS: Nexus Code 4 observed; departure recovery signalled for "+_medicalDepartures.Count(c=>!c.Cancelled&&!c.Departed)+" crew(s). Active patient care/hospital transport is retained.");
            // Treatment completion already queues departure. Code 4 must never
            // cancel care, remove the patient, or claim medical clearance.
            if(!_biteMedicalResponseActive)ReturnMedicsToService();
        }

        private bool TransportBitePatient(Ped patient)
        {
            if(patient==null||!patient.Exists()||_biteMedicalVehicle==null||!_biteMedicalVehicle.Exists())
                throw new InvalidOperationException("Hospital transport is required but the patient/ambulance is unavailable.");
            SetSuspectPhase(patient,SuspectControlPhase.HospitalLoading,"EMS accepted hospital transport requirement");
            _hospitalCareRequired.Add(patient.Handle);
            _biteMedicalLoadingPatient=true;
            _pr.RecordBiteReportEvent(patient,"HospitalRequired",_profile.Name,"EMS assigned hospital care. Equipment or boarding failures must not reclassify the patient as medically cleared for arrest.");
            QueueMedicalDeparture(patient,true);
            return true; // Ownership accepted, NOT a completed transport milestone.
        }

        private void ReturnMedicsToService()=>QueueMedicalDeparture(null,false);

        private void QueueMedicalDeparture(Ped patient,bool hospital)
        {
            if(_biteMedicalVehicle==null)return;
            var crew=new MedicalDeparture{Ambulance=_biteMedicalVehicle,Driver=_biteMedicalDriver,Partner=_biteMedicalPartner,Patient=patient,Hospital=hospital,K9=_profile.Name};
            _biteMedicalVehicle=null;_biteMedicalDriver=null;_biteMedicalPartner=null;
            _medicalDepartures.Add(crew);
            Game.LogTrivial("AdvancedK9 EMS: departure queued; hospital="+hospital+". Responder ownership retained until departure/recovery completes.");
            GameFiber.StartNew(()=>RunMedicalDeparture(crew),"AdvancedK9.EMSDeparture");
        }

        private static bool Living(Ped ped)=>ped!=null&&ped.Exists()&&!ped.IsDead;
        private static bool InMedicalSeat(Ped medic,Vehicle vehicle,int seat)
            => Living(medic)&&vehicle!=null&&vehicle.Exists()&&NativeFunction.Natives.GET_PED_IN_VEHICLE_SEAT<Ped>(vehicle,seat,false)==medic;

        private void RunMedicalDeparture(MedicalDeparture crew)
        {
            try
            {
                while(_running&&!crew.Cancelled&&!crew.Departed)
                {
                    try
                    {
                        if(crew.Ambulance==null||!crew.Ambulance.Exists())
                            throw new InvalidOperationException("Ambulance no longer exists; hospital ownership remains pending a replacement medical request.");
                        crew.Attempts++;crew.RetryRequested=false;
                        bool patientLoaded=!crew.Hospital||(Living(crew.Patient)&&crew.Patient.IsInVehicle(crew.Ambulance,false));
                        if(crew.Hospital&&!patientLoaded)patientLoaded=LoadPatientWithStretcher(crew);
                        if((!crew.Hospital||patientLoaded)&&BoardMedicalCrew(crew)&&(!crew.Hospital||ConfirmMedicalPursuitHandoff(crew))&&
                            EmsDeparturePolicy.CanDepart(InMedicalSeat(crew.Driver,crew.Ambulance,-1),Living(crew.Partner),InMedicalSeat(crew.Partner,crew.Ambulance,0),crew.Hospital,patientLoaded))
                        {
                            StartMedicalDrive(crew);
                            Vector3 departure=crew.Ambulance.Position;
                            uint deadline=Game.GameTime+14000;
                            while(_running&&!crew.Cancelled&&crew.Ambulance.Exists()&&crew.Ambulance.DistanceTo(departure)<4f&&Game.GameTime<deadline)GameFiber.Wait(200);
                            if(!crew.Cancelled&&crew.Ambulance.Exists()&&crew.Ambulance.DistanceTo(departure)>=4f)
                            {
                                crew.Departed=true;
                                if(crew.Hospital)
                                {
                                    SetSuspectPhase(crew.Patient,SuspectControlPhase.HospitalTransport,"loaded ambulance departure observed");
                                    _pr.RecordBiteReportEvent(crew.Patient,"Transported",crew.K9,"Patient loaded into the ambulance; the crew boarded and the ambulance departed toward hospital. Hospital admission has not been verified.");
                                    if(_releasedBiteTarget==crew.Patient)ClearReleasedBiteTarget();
                                    else _pr.ReleaseK9Custody(crew.Patient);
                                    _biteMedicalLoadingPatient=false;
                                    _hospitalCareRequired.Remove(crew.Patient.Handle);
                                    if(Living(crew.Patient)){crew.Patient.BlockPermanentEvents=true;NativeFunction.Natives.SET_PED_KEEP_TASK(crew.Patient,true);}
                                    Game.DisplayNotification("~b~EMS:~s~ The ambulance has departed with the patient for hospital care.");
                                }
                                Game.LogTrivial("AdvancedK9 EMS: departure verified by ambulance movement; hospital="+crew.Hospital+", attempts="+crew.Attempts+".");
                                MonitorMedicalDrive(crew);
                                break;
                            }
                        }
                        Game.LogTrivial("AdvancedK9 EMS: departure attempt "+crew.Attempts+" incomplete; equipment/boarding/drive recovery remains active; hospital="+crew.Hospital+".");
                        if(crew.Attempts==1)Game.DisplayNotification("~y~EMS:~s~ Departure delayed. Paramedics are retrying; hospital patients remain assigned to transport.");
                        // Off-screen seat recovery is allowed only after a visible
                        // walk/entry attempt. No responder warps in front of the player.
                        if(CrewOutOfView(crew)&&Living(crew.Driver))
                        {
                            NativeFunction.Natives.SET_PED_INTO_VEHICLE(crew.Driver,crew.Ambulance,-1);
                            if(Living(crew.Partner))NativeFunction.Natives.SET_PED_INTO_VEHICLE(crew.Partner,crew.Ambulance,0);
                            Game.LogTrivial("AdvancedK9 EMS: off-screen responder seat recovery applied; patient transport still requires confirmed loading.");
                        }
                    }
                    catch(Exception ex)
                    {
                        Game.LogTrivial("AdvancedK9 EMS departure recovery: "+ex.GetType().Name+": "+ex.Message);
                        if(crew.Ambulance==null||!crew.Ambulance.Exists())break;
                    }
                    uint retryAt=Game.GameTime+6000;
                    while(_running&&!crew.Cancelled&&!crew.RetryRequested&&Game.GameTime<retryAt)GameFiber.Wait(200);
                }
            }
            finally
            {
                if(!crew.Departed&&crew.Hospital&&!crew.Cancelled)
                {
                    _biteMedicalLoadingPatient=false;
                    _pr.RecordBiteReportEvent(crew.Patient,"EMSFailed",crew.K9,"Hospital transport could not depart because the response vehicle became unavailable. No medical clearance or completed transport is asserted; request replacement EMS.");
                    Game.DisplayNotification("~r~EMS:~s~ Hospital transport needs a replacement ambulance. Request EMS again; the patient has not been medically cleared.");
                }
                _medicalDepartures.Remove(crew);
                ReleaseMedicalCrew(crew);
            }
        }

        private bool BoardMedicalCrew(MedicalDeparture crew)
        {
            if(crew.Cancelled||!Living(crew.Driver)||crew.Ambulance==null||!crew.Ambulance.Exists())return false;
            var ambulance=crew.Ambulance;
            NativeFunction.Natives.SET_VEHICLE_DOORS_LOCKED(ambulance,1);
            NativeFunction.Natives.SET_VEHICLE_HANDBRAKE(ambulance,true);
            // Exit treatment scenarios fully before approaching the front doors.
            foreach(var medic in new[]{crew.Driver,crew.Partner})if(Living(medic)&&!medic.IsInVehicle(ambulance,false))medic.Tasks.Clear();
            uint leaveScenarioUntil=Game.GameTime+1600;
            while(_running&&!crew.Cancelled&&Game.GameTime<leaveScenarioUntil)GameFiber.Wait(100);
            if(crew.Cancelled||!ambulance.Exists())return false;
            Vector3 driverDoor=ambulance.GetOffsetPosition(new Vector3(-1.65f,1.25f,0f));
            Vector3 partnerDoor=ambulance.GetOffsetPosition(new Vector3(1.65f,1.25f,0f));
            ApproachMedicalDoor(crew.Driver,ambulance,-1,driverDoor);
            ApproachMedicalDoor(crew.Partner,ambulance,0,partnerDoor);
            uint approachUntil=Game.GameTime+14000;
            while(_running&&!crew.Cancelled&&ambulance.Exists()&&Game.GameTime<approachUntil&&
                ((!InMedicalSeat(crew.Driver,ambulance,-1)&&Living(crew.Driver)&&crew.Driver.DistanceTo(driverDoor)>1.5f)||
                 (!InMedicalSeat(crew.Partner,ambulance,0)&&Living(crew.Partner)&&crew.Partner.DistanceTo(partnerDoor)>1.5f)))GameFiber.Wait(200);
            if(crew.Cancelled||!ambulance.Exists())return false;
            EnterMedicalSeat(crew.Driver,ambulance,-1);
            EnterMedicalSeat(crew.Partner,ambulance,0);
            uint enterUntil=Game.GameTime+16000;
            while(_running&&!crew.Cancelled&&ambulance.Exists()&&Game.GameTime<enterUntil&&
                (!InMedicalSeat(crew.Driver,ambulance,-1)||(Living(crew.Partner)&&!InMedicalSeat(crew.Partner,ambulance,0))))GameFiber.Wait(200);
            bool boarded=!crew.Cancelled&&InMedicalSeat(crew.Driver,ambulance,-1)&&(!Living(crew.Partner)||InMedicalSeat(crew.Partner,ambulance,0));
            Game.LogTrivial("AdvancedK9 EMS boarding: driverSeat="+InMedicalSeat(crew.Driver,ambulance,-1)+", partnerSeat="+InMedicalSeat(crew.Partner,ambulance,0)+", completed="+boarded+".");
            return boarded;
        }

        private static void ApproachMedicalDoor(Ped medic,Vehicle ambulance,int seat,Vector3 door)
        {
            if(!Living(medic)||InMedicalSeat(medic,ambulance,seat))return;
            NativeFunction.Natives.FREEZE_ENTITY_POSITION(medic,false);
            NativeFunction.Natives.TASK_FOLLOW_NAV_MESH_TO_COORD(medic,door.X,door.Y,door.Z,1.4f,14000,.8f,false,ambulance.Heading);
        }
        private static void EnterMedicalSeat(Ped medic,Vehicle ambulance,int seat)
        {
            if(!Living(medic)||InMedicalSeat(medic,ambulance,seat))return;
            // Explicit native timeout/seat/normal-entry flags: no implicit warp.
            NativeFunction.Natives.TASK_ENTER_VEHICLE(medic,ambulance,16000,seat,1.4f,1,0);
        }

        private bool LoadPatientWithStretcher(MedicalDeparture crew)
        {
            var patient=crew.Patient;var ambulance=crew.Ambulance;
            var carrier=Living(crew.Partner)?crew.Partner:crew.Driver;
            if(!ValidSuspect(patient)||!Living(patient)||!Living(carrier)||ambulance==null||!ambulance.Exists()||!MedicalLoadingAllowed(patient))return false;
            Rage.Object bed=crew.Stretcher;Model model=default(Model);bool loaded=false,attached=crew.PatientOnStretcher,patientChanged=attached;
            Vector3 minimum=crew.StretcherMinimum,maximum=crew.StretcherMaximum;
            Vector3 rearPoint=ambulance.GetOffsetPosition(new Vector3(0f,-4f,0f));
            string stage="prepare";
            bool originallyRagdoll=attached?crew.PatientOriginallyRagdoll:NativeFunction.Natives.CAN_PED_RAGDOLL<bool>(patient);
            try
            {
                if(!attached||bed==null||!bed.Exists())
                {
                stage="equipment";
                // The first two historic prop names are not stock GTA assets.
                // Use the ambulance stretcher asset, not the hospital-room bed.
                foreach(string name in new[]{_config.BitePatientStretcherModel,"m25_2_prop_m52_gurney_01a"}.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if(string.IsNullOrWhiteSpace(name)||name.Trim().Equals("v_med_emptybed",StringComparison.OrdinalIgnoreCase))continue;
                    Model candidate=new Model(name);
                    if(!candidate.IsValid)continue;
                    NativeFunction.Natives.REQUEST_MODEL(candidate.Hash);
                    uint loadUntil=Game.GameTime+5000;
                    while(_running&&!crew.Cancelled&&!NativeFunction.Natives.HAS_MODEL_LOADED<bool>(candidate.Hash)&&Game.GameTime<loadUntil)GameFiber.Wait(100);
                    if(crew.Cancelled){candidate.Dismiss();return false;}
                    if(!NativeFunction.Natives.HAS_MODEL_LOADED<bool>(candidate.Hash)){candidate.Dismiss();continue;}
                    model=candidate;Vector3 rear=ambulance.GetOffsetPosition(new Vector3(0f,-4f,0f));
                    bed=new Rage.Object(model,rear);model.Dismiss();
                    if(bed!=null&&bed.Exists()){Game.LogTrivial("AdvancedK9 EMS stretcher model loaded: "+name+".");break;}
                }
                if(bed==null||!bed.Exists()){Game.LogTrivial("AdvancedK9 EMS: ambulance stretcher unavailable. Install a compatible stretcher model; hospital assignment retained.");return false;}
                bed.IsPersistent=true;NativeFunction.Natives.FREEZE_ENTITY_POSITION(bed,true);
                NativeFunction.Natives.SET_ENTITY_COLLISION(bed,false,false);
                NativeFunction.Natives.GET_MODEL_DIMENSIONS(bed.Model.Hash,out minimum,out maximum);
                crew.Stretcher=bed;crew.StretcherMinimum=minimum;crew.StretcherMaximum=maximum;
                stage="approach";
                carrier.Tasks.Clear();
                if(!WalkMedicalCarrier(crew,carrier,rearPoint,1.7f))return false;
                Vector3 nearPatient=patient.GetOffsetPosition(new Vector3(0f,-1.5f,0f));
                if(!WalkMedicalCarrier(crew,carrier,nearPatient,1.8f,bed,minimum,maximum)||!MedicalLoadingAllowed(patient))return false;
                const string poseDict="dead",poseClip="dead_a";
                NativeFunction.Natives.REQUEST_ANIM_DICT(poseDict);
                uint animUntil=Game.GameTime+2200;
                while(_running&&!crew.Cancelled&&!NativeFunction.Natives.HAS_ANIM_DICT_LOADED<bool>(poseDict)&&Game.GameTime<animUntil)GameFiber.Wait(50);
                if(!MedicalLoadingAllowed(patient)||crew.Cancelled||!NativeFunction.Natives.HAS_ANIM_DICT_LOADED<bool>(poseDict)||
                   NativeFunction.Natives.GET_ANIM_DURATION<float>(poseDict,poseClip)<=0f)return false;
                stage="secure patient";
                patientChanged=true;crew.PatientOriginallyRagdoll=originallyRagdoll;patient.Tasks.ClearImmediately();NativeFunction.Natives.FREEZE_ENTITY_POSITION(patient,false);_releasedBiteFrozen=false;
                NativeFunction.Natives.STOP_ANIM_TASK(patient,"combat@damage@writhe","writhe_loop",-4f);
                NativeFunction.Natives.SET_PED_CAN_RAGDOLL(patient,false);
                NativeFunction.Natives.SET_ENTITY_COLLISION(patient,false,false);
                // Configuration is mattress height above the wheels/ground.
                // Position the animated torso on that surface, rather than the ped root.
                float deck=minimum.Z+_config.BitePatientStretcherPatientZ;
                var deckPoint=bed.GetOffsetPosition(new Vector3(0f,0f,deck));
                patient.Position=deckPoint;patient.Heading=carrier.Heading;
                NativeFunction.Natives.ATTACH_ENTITY_TO_ENTITY(patient,bed,0,0f,0f,deck,0f,0f,carrier.Heading-bed.Heading,false,false,false,true,2,true);attached=true;
                NativeFunction.Natives.TASK_PLAY_ANIM(patient,poseDict,poseClip,8f,-4f,-1,1,0f,true,true,true);
                uint poseUntil=Game.GameTime+1000;
                while(MedicalLoadingAllowed(patient)&&!crew.Cancelled&&Game.GameTime<poseUntil&&
                      !NativeFunction.Natives.IS_ENTITY_PLAYING_ANIM<bool>(patient,poseDict,poseClip,3))GameFiber.Wait(50);
                if(!MedicalLoadingAllowed(patient)||crew.Cancelled||!NativeFunction.Natives.IS_ENTITY_ATTACHED_TO_ENTITY<bool>(patient,bed)||
                   !NativeFunction.Natives.IS_ENTITY_PLAYING_ANIM<bool>(patient,poseDict,poseClip,3))return false;
                // The lying clip offsets the skeleton from the entity root. Correct
                // using the live torso position once the pose has evaluated.
                GameFiber.Wait(150);
                int spine=NativeFunction.Natives.GET_PED_BONE_INDEX<int>(patient,24817);
                Vector3 torso=NativeFunction.Natives.GET_WORLD_POSITION_OF_ENTITY_BONE<Vector3>(patient,spine);
                float surface=bed.Position.Z+minimum.Z+_config.BitePatientStretcherPatientZ+.08f;
                float correction=surface-torso.Z;
                if(spine<0||float.IsNaN(correction)||Math.Abs(correction)>2.5f)
                    throw new InvalidOperationException("Stretcher torso alignment could not be verified.");
                deck+=correction;
                NativeFunction.Natives.ATTACH_ENTITY_TO_ENTITY(patient,bed,0,0f,0f,deck,0f,0f,carrier.Heading-bed.Heading,false,false,false,true,2,true);
                crew.PatientDeckOffset=deck;crew.PatientHeadingOffset=carrier.Heading-bed.Heading;
                crew.PatientOnStretcher=true;
                Game.LogTrivial("AdvancedK9 EMS: stretcher torso alignment correction="+correction+", mattress world Z="+surface+".");
                SetSuspectPhase(patient,SuspectControlPhase.HospitalTransport,"patient secured on EMS stretcher");
                }
                stage="verify retained patient";
                if(!EnsureStretcherPatient(crew))return false;
                stage="pursuit handoff";
                Game.LogTrivial("AdvancedK9 EMS: patient attachment and lying animation verified; beginning pursuit handoff.");
                if(!ConfirmMedicalPursuitHandoff(crew))return false;
                stage="move patient to ambulance";
                if(!WalkMedicalCarrier(crew,carrier,rearPoint,1.8f,bed,minimum,maximum,patient))return false;
                NativeFunction.Natives.SET_VEHICLE_DOOR_OPEN(ambulance,2,false,false);NativeFunction.Natives.SET_VEHICLE_DOOR_OPEN(ambulance,3,false,false);
                GameFiber.Wait(900);
                if(crew.Cancelled||!ValidSuspect(patient)||!Living(patient)||!ambulance.Exists()||!MedicalLoadingAllowed(patient))return false;
                stage="ambulance seat transfer";
                int seat=NativeFunction.Natives.IS_VEHICLE_SEAT_FREE<bool>(ambulance,2,false)?2:NativeFunction.Natives.IS_VEHICLE_SEAT_FREE<bool>(ambulance,1,false)?1:-2;
                if(seat==-2)return false;
                NativeFunction.Natives.DETACH_ENTITY(patient,true,true);attached=false;
                patient.Tasks.ClearImmediately();NativeFunction.Natives.SET_PED_INTO_VEHICLE(patient,ambulance,seat);
                loaded=patient.IsInVehicle(ambulance,false);
                NativeFunction.Natives.SET_VEHICLE_DOOR_SHUT(ambulance,2,false);NativeFunction.Natives.SET_VEHICLE_DOOR_SHUT(ambulance,3,false);
                if(loaded)Game.LogTrivial("AdvancedK9 EMS: patient moved on gurney to ambulance rear and loaded; hospital departure still pending crew boarding.");
                return loaded;
            }
            catch(Exception ex){Game.LogTrivial("AdvancedK9 EMS stretcher recovery at "+stage+": "+ex.Message);return false;}
            finally
            {
                bool retain=EmsDeparturePolicy.RetainStretcher(loaded,crew.PatientOnStretcher&&attached,_running,crew.Cancelled,
                    Living(patient),bed!=null&&bed.Exists()&&ambulance.Exists());
                if(retain)
                {
                    Game.LogTrivial("AdvancedK9 EMS: loading delayed at "+stage+"; patient and stretcher retained for retry.");
                }
                else
                {
                    if(patientChanged&&ValidSuspect(patient))
                    {
                        if(attached)NativeFunction.Natives.DETACH_ENTITY(patient,true,true);
                        NativeFunction.Natives.SET_ENTITY_COLLISION(patient,true,true);
                        NativeFunction.Natives.SET_PED_CAN_RAGDOLL(patient,originallyRagdoll);
                    }
                    if(bed!=null&&bed.Exists())bed.Delete();
                    crew.Stretcher=null;crew.PatientOnStretcher=false;
                }
                if(Living(carrier))NativeFunction.Natives.STOP_ANIM_TASK(carrier,"anim@heists@box_carry@","idle",-4f);
            }
        }

        private bool EnsureStretcherPatient(MedicalDeparture crew)
        {
            if(!crew.PatientOnStretcher||!MedicalLoadingAllowed(crew.Patient)||!Living(crew.Patient)||
                crew.Cancelled||crew.Stretcher==null||!crew.Stretcher.Exists())return false;
            var patient=crew.Patient;
            if(!NativeFunction.Natives.IS_ENTITY_ATTACHED_TO_ENTITY<bool>(patient,crew.Stretcher))
                NativeFunction.Natives.ATTACH_ENTITY_TO_ENTITY(patient,crew.Stretcher,0,0f,0f,crew.PatientDeckOffset,0f,0f,crew.PatientHeadingOffset,false,false,false,true,2,true);
            if(!NativeFunction.Natives.IS_ENTITY_PLAYING_ANIM<bool>(patient,"dead","dead_a",3))
            {
                NativeFunction.Natives.REQUEST_ANIM_DICT("dead");
                if(!NativeFunction.Natives.HAS_ANIM_DICT_LOADED<bool>("dead"))return false;
                NativeFunction.Natives.TASK_PLAY_ANIM(patient,"dead","dead_a",8f,-4f,-1,1,0f,true,true,true);
                Game.LogTrivial("AdvancedK9 EMS: restored interrupted stretcher lying pose during hospital loading.");
                GameFiber.Wait(100);
            }
            return NativeFunction.Natives.IS_ENTITY_ATTACHED_TO_ENTITY<bool>(patient,crew.Stretcher)&&
                NativeFunction.Natives.IS_ENTITY_PLAYING_ANIM<bool>(patient,"dead","dead_a",3);
        }

        private bool MedicalLoadingAllowed(Ped patient)
        {
            SuspectSession session;
            return ValidSuspect(patient)&&_suspectSessions.TryGetValue(patient,out session)&&
                (!session.State.ProviderOwns||session.State.Phase==SuspectControlPhase.Cuffed);
        }

        private bool WalkMedicalCarrier(MedicalDeparture crew,Ped carrier,Vector3 destination,float stop,Rage.Object stretcher=null,Vector3 minimum=default(Vector3),Vector3 maximum=default(Vector3),Ped patient=null)
        {
            NativeFunction.Natives.TASK_FOLLOW_NAV_MESH_TO_COORD(carrier,destination.X,destination.Y,destination.Z,1.1f,20000,stop,false,carrier.Heading);
            uint deadline=Game.GameTime+22000;
            if(stretcher!=null)NativeFunction.Natives.REQUEST_ANIM_DICT("anim@heists@box_carry@");
            uint nextPush=0;
            do
            {
                if(!_running||crew.Cancelled||!Living(carrier)||!MedicalLoadingAllowed(crew.Patient))return false;
                if(patient!=null&&!EnsureStretcherPatient(crew))return false;
                if(stretcher!=null&&stretcher.Exists())
                {
                    float front=.42f-minimum.Y;
                    bool transverse=maximum.X-minimum.X>maximum.Y-minimum.Y;
                    if(transverse)front=.42f+maximum.X;
                    Vector3 point=carrier.GetOffsetPosition(new Vector3(0f,front,0f));
                    float ground;
                    if(NativeFunction.Natives.GET_GROUND_Z_FOR_3D_COORD<bool>(point.X,point.Y,point.Z+1.1f,out ground,false)&&Math.Abs(ground-point.Z)<2f)
                    {
                        stretcher.Heading=carrier.Heading+(transverse?90f:0f);
                        stretcher.Position=new Vector3(point.X,point.Y,ground-minimum.Z+.015f);
                    }
                    if(Game.GameTime>=nextPush&&NativeFunction.Natives.HAS_ANIM_DICT_LOADED<bool>("anim@heists@box_carry@")&&
                       !NativeFunction.Natives.IS_ENTITY_PLAYING_ANIM<bool>(carrier,"anim@heists@box_carry@","idle",3))
                    {
                        nextPush=Game.GameTime+800;
                        NativeFunction.Natives.TASK_PLAY_ANIM(carrier,"anim@heists@box_carry@","idle",4f,-4f,-1,49,0f,false,false,false);
                    }
                }
                if(carrier.DistanceTo(destination)<=stop+.4f)return true;
                GameFiber.Yield();
            }while(Game.GameTime<deadline);
            return false;
        }

        private bool ConfirmMedicalPursuitHandoff(MedicalDeparture crew)
        {
            if(!ValidSuspect(crew.Patient)||crew.Cancelled)return false;
            if(crew.PursuitHandoffConfirmed)return true;
            crew.HandoffInProgress=true;
            try{crew.PursuitHandoffConfirmed=_pr.TryEndMedicalPursuit(crew.Patient);}
            finally{crew.HandoffInProgress=false;}
            return crew.PursuitHandoffConfirmed&&ValidSuspect(crew.Patient)&&Living(crew.Patient)&&!crew.Cancelled;
        }

        private void StartMedicalDrive(MedicalDeparture crew)
        {
            NativeFunction.Natives.SET_VEHICLE_HANDBRAKE(crew.Ambulance,false);
            NativeFunction.Natives.SET_ENTITY_MAX_SPEED(crew.Ambulance,25f);
            NativeFunction.Natives.SET_VEHICLE_SIREN(crew.Ambulance,crew.Hospital);
            if(crew.Hospital)
            {
                var hospital=NearestMedicalHospital(crew.Ambulance.Position);
                NativeFunction.Natives.TASK_VEHICLE_DRIVE_TO_COORD_LONGRANGE(crew.Driver,crew.Ambulance,hospital.X,hospital.Y,hospital.Z,18f,786468,12f);
            }
            else NativeFunction.Natives.TASK_VEHICLE_DRIVE_WANDER(crew.Driver,crew.Ambulance,16f,786468);
        }
        private static Vector3 NearestMedicalHospital(Vector3 position)
        {
            var hospitals=new[]{new Vector3(307f,-595f,43f),new Vector3(342f,-1398f,32f),new Vector3(1839f,3672f,34f),new Vector3(-247f,6331f,32f)};
            return World.GetNextPositionOnStreet(hospitals.OrderBy(p=>p.DistanceTo(position)).First());
        }
        private void MonitorMedicalDrive(MedicalDeparture crew)
        {
            uint nextDriveRepair=Game.GameTime+20000;
            while(_running&&!crew.Cancelled&&crew.Ambulance.Exists())
            {
                if(CrewOutOfView(crew))return;
                if(Game.GameTime>=nextDriveRepair)
                {
                    nextDriveRepair=Game.GameTime+20000;
                    if(InMedicalSeat(crew.Driver,crew.Ambulance,-1)&&crew.Ambulance.Speed<.3f)StartMedicalDrive(crew);
                }
                GameFiber.Wait(1000);
            }
        }
        private static bool CrewOutOfView(MedicalDeparture crew)
        {
            var player=Game.LocalPlayer.Character;
            return player!=null&&player.Exists()&&new Entity[]{crew.Ambulance,crew.Driver,crew.Partner,crew.Patient}
                .Where(e=>e!=null&&e.Exists()).All(e=>player.DistanceTo(e)>120f&&!NativeFunction.Natives.IS_ENTITY_ON_SCREEN<bool>(e));
        }
        private static void ReleaseMedicalCrew(MedicalDeparture crew)
        {
            if(crew.PatientOnStretcher&&crew.Patient!=null&&crew.Patient.Exists())
            {
                NativeFunction.Natives.DETACH_ENTITY(crew.Patient,true,true);
                NativeFunction.Natives.SET_ENTITY_COLLISION(crew.Patient,true,true);
                NativeFunction.Natives.SET_PED_CAN_RAGDOLL(crew.Patient,crew.PatientOriginallyRagdoll);
            }
            if(crew.Stretcher!=null&&crew.Stretcher.Exists())crew.Stretcher.Delete();
            crew.Stretcher=null;crew.PatientOnStretcher=false;
            if(crew.Hospital&&crew.Departed&&crew.Patient!=null&&crew.Patient.Exists())crew.Patient.Dismiss();
            if(crew.Partner!=null&&crew.Partner.Exists())crew.Partner.Dismiss();
            if(crew.Driver!=null&&crew.Driver.Exists())crew.Driver.Dismiss();
            if(crew.Ambulance!=null&&crew.Ambulance.Exists())crew.Ambulance.Dismiss();
        }
        private void ShutdownMedicalResponders()
        {
            _biteMedicalResponseActive=false;_medicalAssessmentGeneration++;
            ReturnMedicsToService();
            foreach(var crew in _medicalDepartures.ToArray()){crew.Cancelled=true;ReleaseMedicalCrew(crew);}
            _biteMedicalLoadingPatient=false;
        }
    }
}
