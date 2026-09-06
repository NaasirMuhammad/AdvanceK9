using System;
using AdvancedK9.API;
using LSPD_First_Response.Mod.Callouts;
using Rage;
using Rage.Native;

namespace AdvancedK9.Callouts
{
    public abstract class AdvancedK9Callout : Callout
    {
        protected readonly Random Random=new Random();
        protected string ContextId;
        protected Vector3 Scene;
        protected Ped Subject;
        protected Ped Reporter;
        protected Vehicle SceneVehicle;
        protected Vehicle PoliceVehicle;
        protected Ped OfficerOne;
        protected Ped OfficerTwo;
        protected Blip SubjectBlip;
        protected Blip SceneBlip;
        protected bool ApiRequested;
        protected uint StartedAt;
        protected bool Finished;
        private bool _cleanupCompleted;
        private bool _trafficControlled;

        protected bool Prepare(string message,Vector3 scene,float radius,bool snapToStreet=true)
        {
            var player=Game.LocalPlayer.Character;
            if(player==null||!player.Exists())
            {
                Game.LogTrivial("AdvancedK9 Callouts: "+GetType().Name+" rejected because the player ped is unavailable.");
                return false;
            }
            Scene=snapToStreet?World.GetNextPositionOnStreet(scene):scene;
            ContextId=GetType().Name+"-"+Guid.NewGuid().ToString("N");
            CalloutMessage=message;
            CalloutPosition=Scene;
            ShowCalloutAreaBlipBeforeAccepting(Scene,radius);
            AddMinimumDistanceCheck(50f,Scene);
            Game.LogTrivial("AdvancedK9 Callouts: "+GetType().Name+" prepared at "+Scene+".");
            return true;
        }

        protected Vector3 StreetOffset(float forward,float side)
        {
            return Game.LocalPlayer.Character.GetOffsetPosition(new Vector3(side,forward,0f));
        }

        protected Ped SpawnPed(string modelName,Vector3 position,float heading)
        {
            var model=new Model(modelName);if(!model.IsValid)return null;model.LoadAndWait();var ped=new Ped(model,position,heading);model.Dismiss();
            if(ped!=null&&ped.Exists()){ped.IsPersistent=true;ped.BlockPermanentEvents=true;}return ped;
        }

        protected Vehicle SpawnVehicle(string modelName,Vector3 position,float heading)
        {
            var model=new Model(modelName);if(!model.IsValid)return null;model.LoadAndWait();var vehicle=new Vehicle(model,position,heading);model.Dismiss();
            if(vehicle!=null&&vehicle.Exists())vehicle.IsPersistent=true;return vehicle;
        }

        private Vehicle SpawnPoliceVehicle(Vector3 position,float heading)
        {
            string[] models={"police3","police","sheriff"};
            foreach(string model in models)
            {
                var vehicle=SpawnVehicle(model,position,heading);
                if(vehicle!=null&&vehicle.Exists())return vehicle;
            }
            return null;
        }

        private Ped SpawnPoliceOfficer(Vector3 position,float heading)
        {
            string[] models={"s_m_y_cop_01","s_m_y_sheriff_01","s_f_y_cop_01"};
            foreach(string model in models)
            {
                var officer=SpawnPed(model,position,heading);
                if(officer!=null&&officer.Exists())return officer;
            }
            return null;
        }

        protected bool StagePoliceScene()
        {
            Vector3 cruiserPosition=SceneVehicle!=null&&SceneVehicle.Exists()
                ?SceneVehicle.GetOffsetPosition(new Vector3(-4f,-9f,0f))
                :new Vector3(Scene.X-10f,Scene.Y-7f,Scene.Z);
            Vector3 officerOnePosition=SceneVehicle!=null&&SceneVehicle.Exists()
                ?SceneVehicle.GetOffsetPosition(new Vector3(-2.5f,-2.5f,0f))
                :new Vector3(Scene.X-4f,Scene.Y-3f,Scene.Z);
            Vector3 officerTwoPosition=SceneVehicle!=null&&SceneVehicle.Exists()
                ?SceneVehicle.GetOffsetPosition(new Vector3(2.5f,-3.5f,0f))
                :new Vector3(Scene.X+4f,Scene.Y-3f,Scene.Z);
            if(PoliceVehicle==null||!PoliceVehicle.Exists())PoliceVehicle=SpawnPoliceVehicle(cruiserPosition,Game.LocalPlayer.Character.Heading);
            if(OfficerOne==null||!OfficerOne.Exists())OfficerOne=SpawnPoliceOfficer(officerOnePosition,0f);
            if(OfficerTwo==null||!OfficerTwo.Exists())OfficerTwo=SpawnPoliceOfficer(officerTwoPosition,180f);
            if(OfficerOne!=null&&OfficerOne.Exists())NativeFunction.Natives.TASK_STAND_STILL(OfficerOne,-1);
            if(OfficerTwo!=null&&OfficerTwo.Exists())NativeFunction.Natives.TASK_STAND_STILL(OfficerTwo,-1);
            bool vehicleReady=PoliceVehicle!=null&&PoliceVehicle.Exists();
            bool officersReady=OfficerOne!=null&&OfficerOne.Exists()&&OfficerTwo!=null&&OfficerTwo.Exists();
            Game.LogTrivial("AdvancedK9 Callouts: police scene verification for "+GetType().Name+
                ": cruiser="+vehicleReady+", officer1="+(OfficerOne!=null&&OfficerOne.Exists())+
                ", officer2="+(OfficerTwo!=null&&OfficerTwo.Exists())+".");
            return vehicleReady&&officersReady;
        }

        protected bool K9ReadyOnFoot()
        {
            K9ApiSnapshot snapshot;
            return AdvancedK9Api.TryGetSnapshot(out snapshot)&&snapshot.OnDuty&&snapshot.Deployed&&
                snapshot.DogHandle>0&&!string.Equals(snapshot.State,"InVehicle",StringComparison.OrdinalIgnoreCase)&&
                !string.Equals(snapshot.State,"Dismissed",StringComparison.OrdinalIgnoreCase);
        }

        protected void ControlSceneTraffic()
        {
            if(_trafficControlled)return;
            NativeFunction.Natives.SET_ROADS_IN_AREA(Scene.X-32f,Scene.Y-32f,Scene.Z-8f,Scene.X+32f,Scene.Y+32f,Scene.Z+8f,false,true);
            _trafficControlled=true;
            Game.LogTrivial("AdvancedK9 Callouts: traffic control established around "+GetType().Name+" scene.");
        }

        protected float K9DistanceTo(Vector3 position)
        {
            K9ApiSnapshot snapshot;
            if(!AdvancedK9Api.TryGetSnapshot(out snapshot)||snapshot.DogHandle<=0)return float.MaxValue;
            if(!NativeFunction.Natives.DOES_ENTITY_EXIST<bool>(snapshot.DogHandle))return float.MaxValue;
            Vector3 dogPosition=NativeFunction.Natives.GET_ENTITY_COORDS<Vector3>(snapshot.DogHandle,true);
            return dogPosition.DistanceTo(position);
        }

        protected void RouteToScene(string instruction)
        {
            if(SceneBlip!=null&&SceneBlip.Exists())SceneBlip.Delete();
            SceneBlip=new Blip(Scene);SceneBlip.IsRouteEnabled=true;
            Game.DisplayNotification("~b~AdvancedK9 callout:~s~ "+instruction+"~n~Follow the GPS route to the investigation start point.");
            Game.LogTrivial("AdvancedK9 Callouts: routed player to "+GetType().Name+" start at "+Scene+".");
        }

        protected void ClearSceneRoute()
        {
            if(SceneBlip!=null&&SceneBlip.Exists()){SceneBlip.IsRouteEnabled=false;SceneBlip.Delete();}
            SceneBlip=null;
        }

        protected int HandleOf(Entity entity){int value;return entity!=null&&entity.Exists()&&int.TryParse(entity.Handle.ToString(),out value)?value:0;}

        protected bool K9Available()
        {
            K9ApiSnapshot snapshot;return AdvancedK9Api.TryGetSnapshot(out snapshot)&&snapshot.OnDuty&&snapshot.Deployed;
        }

        protected void AssignCalloutScent(Ped target,string details)
        {
            if(ApiRequested||!K9ReadyOnFoot()||target==null||!target.Exists())return;
            ApiRequested=true;
            AdvancedK9Api.SendCommand("AssignScent",ContextId,HandleOf(target),"Ped",Scene.X,Scene.Y,Scene.Z,details);
            Game.DisplayNotification("~b~AdvancedK9 callout:~s~ preserved vehicle scent is ready. Command Rex to COLLECT SCENT or TRACK beside the abandoned vehicle.");
        }

        protected void RequestK9(string command,Ped target,string details)
        {
            if(ApiRequested||!K9Available())return;ApiRequested=true;
            AdvancedK9Api.SendCommand(command,ContextId,HandleOf(target),"Ped",target==null?0:target.Position.X,target==null?0:target.Position.Y,target==null?0:target.Position.Z,details);
            Game.DisplayNotification("~b~AdvancedK9 callout:~s~ "+command+" request sent to the active K9 team.");
        }

        protected void Resolve(string message)
        {
            if(Finished)return;Finished=true;Game.DisplayNotification(message);End();
        }

        public override void End()
        {
            if(_cleanupCompleted)return;
            _cleanupCompleted=true;
            ClearSceneRoute();
            if(SubjectBlip!=null&&SubjectBlip.Exists())SubjectBlip.Delete();
            if(Reporter!=null&&Reporter.Exists())Reporter.Dismiss();
            if(Subject!=null&&Subject.Exists())Subject.Dismiss();
            if(SceneVehicle!=null&&SceneVehicle.Exists())SceneVehicle.Dismiss();
            if(OfficerOne!=null&&OfficerOne.Exists())OfficerOne.Dismiss();
            if(OfficerTwo!=null&&OfficerTwo.Exists())OfficerTwo.Dismiss();
            if(PoliceVehicle!=null&&PoliceVehicle.Exists())PoliceVehicle.Dismiss();
            if(_trafficControlled)
            {
                NativeFunction.Natives.SET_ROADS_IN_AREA(Scene.X-32f,Scene.Y-32f,Scene.Z-8f,Scene.X+32f,Scene.Y+32f,Scene.Z+8f,true,true);
                _trafficControlled=false;
            }
            if(!string.IsNullOrWhiteSpace(ContextId))AdvancedK9Api.SendCommand("ClearEvidenceMarkers",ContextId,0,"Scene",Scene.X,Scene.Y,Scene.Z,"callout scene cleared");
            Game.LogTrivial("AdvancedK9 Callouts: cleared scene and evidence markers for "+GetType().Name+".");
            base.End();
        }

        public override void OnCalloutNotAccepted(){End();}
    }
}
