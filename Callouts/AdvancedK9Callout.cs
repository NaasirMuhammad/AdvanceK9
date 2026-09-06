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
        protected Blip SubjectBlip;
        protected Blip SceneBlip;
        protected bool ApiRequested;
        protected uint StartedAt;
        protected bool Finished;

        protected bool Prepare(string message,Vector3 scene,float radius)
        {
            // LSPDFR only creates registered callouts while the player is on duty. Do not
            // reject the offer because the separately loaded API snapshot can briefly lag
            // during assembly resolution or a duty transition.
            var player=Game.LocalPlayer.Character;
            if(player==null||!player.Exists())
            {
                Game.LogTrivial("AdvancedK9 Callouts: "+GetType().Name+" rejected because the player ped is unavailable.");
                return false;
            }
            Scene=scene;
            ContextId=GetType().Name+"-"+Guid.NewGuid().ToString("N");
            CalloutMessage=message;
            CalloutPosition=scene;
            ShowCalloutAreaBlipBeforeAccepting(scene,radius);
            AddMinimumDistanceCheck(50f,scene);
            Game.LogTrivial("AdvancedK9 Callouts: "+GetType().Name+" prepared at "+scene+".");
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
            ClearSceneRoute();
            if(SubjectBlip!=null&&SubjectBlip.Exists())SubjectBlip.Delete();
            if(Reporter!=null&&Reporter.Exists())Reporter.Dismiss();
            if(Subject!=null&&Subject.Exists())Subject.Dismiss();
            if(SceneVehicle!=null&&SceneVehicle.Exists())SceneVehicle.Dismiss();
            base.End();
        }

        public override void OnCalloutNotAccepted(){End();base.OnCalloutNotAccepted();}
    }
}
