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
                if(Living(crew.Patient))_pr.RefreshK9Custody(crew.Patient,"HospitalTransport");
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
                        if((!crew.Hospital||patientLoaded)&&BoardMedicalCrew(crew)&&
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
            Rage.Object bed=null;Model model=default(Model);bool loaded=false,attached=false;
            Vector3 original=patient.Position;bool originallyRagdoll=NativeFunction.Natives.CAN_PED_RAGDOLL<bool>(patient);
            try
            {
                // The first two historic prop names are not stock GTA assets.
                // v_med_emptybed is a base-game wheeled medical gurney.
                foreach(string name in new[]{_config.BitePatientStretcherModel,"v_med_emptybed"}.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if(string.IsNullOrWhiteSpace(name))continue;
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
                if(bed==null||!bed.Exists())return false;
                bed.IsPersistent=true;NativeFunction.Natives.SET_ENTITY_COLLISION(bed,false,false);
                NativeFunction.Natives.PLACE_OBJECT_ON_GROUND_PROPERLY(bed);
                Vector3 rearPoint=ambulance.GetOffsetPosition(new Vector3(0f,-4f,0f));
                carrier.Tasks.Clear();
                if(!WalkMedicalCarrier(crew,carrier,rearPoint,1.7f))return false;
                // Keep wheels at the ground height while the medic pushes the
                // gurney ahead of their body, rather than hanging it from a pelvis bone.
                Vector3 local=NativeFunction.Natives.GET_OFFSET_FROM_ENTITY_GIVEN_WORLD_COORDS<Vector3>(carrier,bed.Position.X,bed.Position.Y,bed.Position.Z);
                NativeFunction.Natives.ATTACH_ENTITY_TO_ENTITY(bed,carrier,0,0f,1.5f,local.Z,0f,0f,0f,false,false,false,true,2,true);
                Vector3 nearPatient=patient.GetOffsetPosition(new Vector3(0f,-1.5f,0f));
                if(!WalkMedicalCarrier(crew,carrier,nearPatient,1.8f)||!MedicalLoadingAllowed(patient))return false;
                NativeFunction.Natives.DETACH_ENTITY(bed,true,true);
                bed.Heading=carrier.Heading;NativeFunction.Natives.PLACE_OBJECT_ON_GROUND_PROPERLY(bed);
                patient.Tasks.ClearImmediately();NativeFunction.Natives.FREEZE_ENTITY_POSITION(patient,false);_releasedBiteFrozen=false;
                NativeFunction.Natives.STOP_ANIM_TASK(patient,"combat@damage@writhe","writhe_loop",-4f);
                NativeFunction.Natives.SET_PED_CAN_RAGDOLL(patient,false);
                NativeFunction.Natives.SET_ENTITY_COLLISION(patient,false,false);
                NativeFunction.Natives.REQUEST_ANIM_DICT("anim@gangops@morgue@table@");
                uint animUntil=Game.GameTime+2000;
                while(_running&&!crew.Cancelled&&!NativeFunction.Natives.HAS_ANIM_DICT_LOADED<bool>("anim@gangops@morgue@table@")&&Game.GameTime<animUntil)GameFiber.Wait(100);
                if(crew.Cancelled)return false;
                if(NativeFunction.Natives.HAS_ANIM_DICT_LOADED<bool>("anim@gangops@morgue@table@"))
                    NativeFunction.Natives.TASK_PLAY_ANIM(patient,"anim@gangops@morgue@table@","body_search",4f,-4f,-1,1,0f,false,false,false);
                // Stock gurney mattress height; configurable for a supplied addon.
                NativeFunction.Natives.ATTACH_ENTITY_TO_ENTITY(patient,bed,0,0f,0f,_config.BitePatientStretcherPatientZ,0f,0f,0f,false,false,false,true,2,true);attached=true;
                local=NativeFunction.Natives.GET_OFFSET_FROM_ENTITY_GIVEN_WORLD_COORDS<Vector3>(carrier,bed.Position.X,bed.Position.Y,bed.Position.Z);
                NativeFunction.Natives.ATTACH_ENTITY_TO_ENTITY(bed,carrier,0,0f,1.5f,local.Z,0f,0f,0f,false,false,false,true,2,true);
                if(!WalkMedicalCarrier(crew,carrier,rearPoint,1.8f))return false;
                NativeFunction.Natives.SET_VEHICLE_DOOR_OPEN(ambulance,2,false,false);NativeFunction.Natives.SET_VEHICLE_DOOR_OPEN(ambulance,3,false,false);
                GameFiber.Wait(900);
                if(crew.Cancelled||!ValidSuspect(patient)||!Living(patient)||!ambulance.Exists()||!MedicalLoadingAllowed(patient))return false;
                int seat=NativeFunction.Natives.IS_VEHICLE_SEAT_FREE<bool>(ambulance,2,false)?2:NativeFunction.Natives.IS_VEHICLE_SEAT_FREE<bool>(ambulance,1,false)?1:-2;
                if(seat==-2)return false;
                NativeFunction.Natives.DETACH_ENTITY(patient,true,true);attached=false;
                patient.Tasks.ClearImmediately();NativeFunction.Natives.SET_PED_INTO_VEHICLE(patient,ambulance,seat);
                loaded=patient.IsInVehicle(ambulance,false);
                NativeFunction.Natives.SET_VEHICLE_DOOR_SHUT(ambulance,2,false);NativeFunction.Natives.SET_VEHICLE_DOOR_SHUT(ambulance,3,false);
                if(loaded)Game.LogTrivial("AdvancedK9 EMS: patient moved on gurney to ambulance rear and loaded; hospital departure still pending crew boarding.");
                return loaded;
            }
            catch(Exception ex){Game.LogTrivial("AdvancedK9 EMS stretcher recovery: "+ex.Message);return false;}
            finally
            {
                if(ValidSuspect(patient))
                {
                    if(attached)NativeFunction.Natives.DETACH_ENTITY(patient,true,true);
                    NativeFunction.Natives.SET_ENTITY_COLLISION(patient,true,true);
                    NativeFunction.Natives.SET_PED_CAN_RAGDOLL(patient,originallyRagdoll);
                    if(!loaded&&attached){patient.Position=original;patient.Tasks.ClearImmediately();}
                }
                if(bed!=null&&bed.Exists())bed.Delete();
            }
        }

        private bool MedicalLoadingAllowed(Ped patient)
        {
            SuspectSession session;
            return ValidSuspect(patient)&&_suspectSessions.TryGetValue(patient,out session)&&
                (!session.State.ProviderOwns||session.State.Phase==SuspectControlPhase.Cuffed);
        }

        private bool WalkMedicalCarrier(MedicalDeparture crew,Ped carrier,Vector3 destination,float stop)
        {
            NativeFunction.Natives.TASK_FOLLOW_NAV_MESH_TO_COORD(carrier,destination.X,destination.Y,destination.Z,1.1f,20000,stop,false,carrier.Heading);
            uint deadline=Game.GameTime+22000;
            while(_running&&!crew.Cancelled&&(!crew.Hospital||MedicalLoadingAllowed(crew.Patient))&&Living(carrier)&&carrier.DistanceTo(destination)>stop+.4f&&Game.GameTime<deadline)GameFiber.Wait(150);
            return !crew.Cancelled&&(!crew.Hospital||MedicalLoadingAllowed(crew.Patient))&&Living(carrier)&&carrier.DistanceTo(destination)<=stop+.4f;
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
