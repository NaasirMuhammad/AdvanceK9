using System;
using System.Linq;
using Rage;
using Rage.Native;
using AdvancedK9.Medical;

namespace AdvancedK9
{
    internal sealed partial class K9Controller
    {
        private bool _medicalArrivalInProgress, _medicalArrivalRecoveryRequested;
        private readonly MedicalRecoveryHold _medicalRecoveryHold=new MedicalRecoveryHold();

        private void HandleMedicalRecoveryShortcut()
        {
            bool eligible=_medicalArrivalInProgress&&_biteMedicalResponseActive&&
                Living(_biteMedicalDriver)&&Living(_biteMedicalPartner)&&
                _biteMedicalVehicle!=null&&_biteMedicalVehicle.Exists();
            bool pressed=_config.EmsRecoveryKey!=System.Windows.Forms.Keys.None&&
                Game.IsKeyDownRightNow(_config.EmsRecoveryKey)&&!NativeFunction.Natives.IS_PAUSE_MENU_ACTIVE<bool>();
            if(_medicalRecoveryHold.Update(pressed,eligible,Game.GameTime,_config.EmsRecoveryHoldMilliseconds))
                _medicalArrivalRecoveryRequested=true;
        }

        private bool TryRecoverMedicalArrival(Ped patient,ref Vector3 medicalStage)
        {
            if(!_medicalArrivalRecoveryRequested)return false;
            _medicalArrivalRecoveryRequested=false;
            if(!_medicalArrivalInProgress||!ValidSuspect(patient)||!Living(patient)||
                _biteMedicalVehicle==null||!_biteMedicalVehicle.Exists()||
                !Living(_biteMedicalDriver)||!Living(_biteMedicalPartner))return false;
            Vector3 destination;
            if(!TryFindMedicalRecoveryPosition(patient,out destination))
            {
                Game.DisplayNotification("~y~EMS:~s~ No clear roadside arrival position found. Clear some space and hold "+_config.EmsRecoveryKey+" to retry.");
                Game.LogTrivial("AdvancedK9 EMS: manual arrival recovery rejected; no clear nearby road position.");
                return false;
            }
            var ambulance=_biteMedicalVehicle;
            _biteMedicalDriver.Tasks.ClearImmediately();_biteMedicalPartner.Tasks.ClearImmediately();
            NativeFunction.Natives.SET_VEHICLE_FORWARD_SPEED(ambulance,0f);
            ambulance.Position=destination;ambulance.Heading=HeadingToward(destination,patient.Position);
            NativeFunction.Natives.SET_VEHICLE_ON_GROUND_PROPERLY(ambulance);
            NativeFunction.Natives.SET_PED_INTO_VEHICLE(_biteMedicalDriver,ambulance,-1);
            NativeFunction.Natives.SET_PED_INTO_VEHICLE(_biteMedicalPartner,ambulance,0);
            medicalStage=ambulance.Position;
            Game.LogTrivial("AdvancedK9 EMS: player requested arrival recovery; existing ambulance and crew moved to "+medicalStage+". Treatment has not been skipped.");
            Game.DisplayNotification("~b~EMS:~s~ Ambulance brought to the scene. Paramedics are approaching the patient.");
            return true;
        }

        private bool TryFindMedicalRecoveryPosition(Ped patient,out Vector3 result)
        {
            result=default(Vector3);
            var vehicles=World.GetAllVehicles();var peds=World.GetAllPeds();
            foreach(float radius in new[]{12f,18f,25f,32f})
                for(int i=0;i<16;i++)
                {
                    var candidate=World.GetNextPositionOnStreet(patient.Position+HeadingOffset(i*22.5f,radius));
                    float distance=candidate.DistanceTo(patient.Position);
                    if(distance<9f||distance>38f||Math.Abs(candidate.Z-patient.Position.Z)>3f)continue;
                    // Leave room for the ambulance body, medics and rear doors.
                    if(vehicles.Any(v=>v!=null&&v.Exists()&&v!=_biteMedicalVehicle&&v.DistanceTo(candidate)<8f))continue;
                    if(peds.Any(p=>p!=null&&p.Exists()&&p!=_biteMedicalDriver&&p!=_biteMedicalPartner&&p.DistanceTo(candidate)<6f))continue;
                    float ground;
                    if(!NativeFunction.Natives.GET_GROUND_Z_FOR_3D_COORD<bool>(candidate.X,candidate.Y,candidate.Z+2f,out ground,false)||
                        Math.Abs(ground-candidate.Z)>2f)continue;
                    result=new Vector3(candidate.X,candidate.Y,ground+.15f);return true;
                }
            return false;
        }
    }
}
