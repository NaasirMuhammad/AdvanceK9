using System;
using AdvancedK9.API;
using LSPD_First_Response.Mod.Callouts;
using Rage;

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

        protected bool Prepare(string message,Vector3 scene,float radius)
        {
            var player=Game.LocalPlayer.Character;
            if(player==null||!player.Exists())
            {
                Game.LogTrivial("AdvancedK9 Callouts: "+GetType().Name+" rejected because the player ped is unavailable.");
                return false;
            }
            Scene=World.GetNextPositionOnStreet(scene);
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
            var player=Game.LocalPlayer.Character;
            return player.GetOffsetPosition(new Vector3(side,forward,0f));
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

        protected void StagePoliceScene()
        {
            Vector3 cruiserPosition=World.GetNextPositionOnStreet(new Vector3(Scene.X-14f,Scene.Y-8f,Scene.Z));
            PoliceVehicle=SpawnVehicle("police3",cruiserPosition,Game.LocalPlayer.Character.Heading);
            Vector3 officerOnePosition=World.GetNextPositionOnStreet(new Vector3(Scene.X-7f,Scene.Y-3f,Scene.Z));
            Vector3 officerTwoPosition=World.GetNextPositionOnStreet(new Vector3(Scene.X+6f,Scene.Y-4f,Scene.Z));
            OfficerOne=SpawnPed("s_m_y_cop_01",officerOnePosition,0f);
            OfficerTwo=SpawnPed("s_m_y_cop_01",officerTwoPosition,180f);
            if(OfficerOne!=null&&OfficerOne.Exists())Rage.Native.NativeFunction.Natives.TASK_STAND_STILL(OfficerOne,-1);
            if(OfficerTwo!=null&&OfficerTwo.Exists())Rage.Native.NativeFunction.Natives.TASK_STAND_STILL(OfficerTwo,-1);
            Game.LogTrivial("AdvancedK9 Callouts: staged police scene for "+GetType().Name+".");
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
            if(!string.IsNullOrWhiteSpace(ContextId))AdvancedK9Api.SendCommand("ClearEvidenceMarkers",ContextId,0,"Scene",Scene.X,Scene.Y,Scene.Z,"callout scene cleared");
            Game.LogTrivial("AdvancedK9 Callouts: cleared scene and evidence markers for "+GetType().Name+".");
            base.End();
        }

        public override void OnCalloutNotAccepted(){End();}
    }
}
