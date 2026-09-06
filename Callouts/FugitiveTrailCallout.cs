using LSPD_First_Response.Mod.API;
using LSPD_First_Response.Mod.Callouts;
using Rage;
using Rage.Native;

namespace AdvancedK9.Callouts
{
    [CalloutInfo("AdvancedK9: Fugitive Trail",CalloutProbability.Medium)]
    public sealed class FugitiveTrailCallout : AdvancedK9Callout
    {
        private int _outcome;
        private bool _scentPresented;
        private bool _suspectLocated;
        private uint _locatedAt;

        public override bool OnBeforeCalloutDisplayed()
        {
            try
            {
                if(!Prepare("Fugitive trail from abandoned vehicle",StreetOffset(520f,Random.Next(-160,161)),110f))return false;
            }
            catch(System.Exception ex)
            {
                Game.LogTrivial("AdvancedK9 Callouts: "+GetType().Name+" primary scene calculation failed; using safe fallback: "+ex);
                var playerPosition=Game.LocalPlayer.Character.Position;
                if(!Prepare("Fugitive trail from abandoned vehicle",new Vector3(playerPosition.X+260f,playerPosition.Y,playerPosition.Z),110f))return false;
            }
            SceneVehicle=SpawnVehicle("primo",Scene,Random.Next(360));
            StagePoliceScene();
            return SceneVehicle!=null&&SceneVehicle.Exists();
        }

        public override bool OnCalloutAccepted()
        {
            StartedAt=Game.GameTime;_outcome=Random.Next(4);
            StagePoliceScene();
            Vector3 suspectPosition=World.GetNextPositionOnStreet(new Vector3(Scene.X+Random.Next(260,430),Scene.Y+Random.Next(-260,261),Scene.Z));
            Subject=SpawnPed("a_m_m_hillbilly_01",suspectPosition,Random.Next(360));if(Subject==null)return false;
            NativeFunction.Natives.TASK_COWER(Subject,-1);
            Functions.PlayScannerAudioUsingPosition("WE_HAVE CRIME_RESIST_ARREST IN_OR_ON_POSITION",Scene);
            RouteToScene("Respond to the abandoned vehicle. The on-scene officer has preserved a scent article from the driver seat.");
            return base.OnCalloutAccepted();
        }

        public override void Process()
        {
            if(Finished||Subject==null||!Subject.Exists()){if(!Finished)Resolve("~r~Fugitive Trail ended: suspect unavailable.");return;}
            var player=Game.LocalPlayer.Character;

            if(player.DistanceTo(Scene)<180f&&
                (PoliceVehicle==null||!PoliceVehicle.Exists()||OfficerOne==null||!OfficerOne.Exists()||OfficerTwo==null||!OfficerTwo.Exists()))
                StagePoliceScene();

            if(!_scentPresented&&player.DistanceTo(Scene)<28f)
            {
                _scentPresented=true;
                Game.DisplayNotification("~b~On-scene officer:~s~ The suspect fled on foot. I preserved their scent from the driver seat. Bring Rex beside the vehicle to begin the trail.");
            }

            if(!ApiRequested&&_scentPresented)
            {
                RequestK9("Track",Subject,"officer-presented scent article collected from abandoned vehicle driver seat");
                if(ApiRequested)
                {
                    ClearSceneRoute();
                    Game.DisplayNotification("~b~AdvancedK9:~s~ The officer handed over the scent article and Rex has the assigned scent. Follow Rex and watch his body language; repeating TRACK will retain this callout target.");
                    Game.LogTrivial("AdvancedK9 Callouts: officer scent handoff submitted for FugitiveTrail subject.");
                }
            }

            if(ApiRequested&&!_suspectLocated&&K9DistanceTo(Subject.Position)<18f)
            {
                _suspectLocated=true;_locatedAt=Game.GameTime;
                if(_outcome==0)NativeFunction.Natives.TASK_HANDS_UP(Subject,120000,player,-1,true);
                else if(_outcome==1)NativeFunction.Natives.TASK_SMART_FLEE_PED(Subject,player,600f,-1,false,false);
                else if(_outcome==2)NativeFunction.Natives.TASK_COWER(Subject,-1);
                else NativeFunction.Natives.TASK_WANDER_STANDARD(Subject,10f,10);
                SubjectBlip=Subject.AttachBlip();SubjectBlip.IsRouteEnabled=true;
                Game.DisplayNotification(_outcome==0
                    ?"~g~Rex located the fugitive. The suspect is surrendering; secure the arrest."
                    :"~o~Rex located the fugitive. Suspect marked on the map; take appropriate police action.");
                Game.LogTrivial("AdvancedK9 Callouts: Rex located FugitiveTrail subject; persistent suspect marker created.");
            }

            if(_suspectLocated)
            {
                if(Subject.IsDead)Resolve("~o~Fugitive Trail concluded: suspect is deceased.");
                else if(NativeFunction.Natives.IS_PED_CUFFED<bool>(Subject))Resolve("~g~Fugitive Trail complete: suspect arrested.");
                else if(Game.GameTime-_locatedAt>300000)Resolve("~o~Fugitive Trail concluded after suspect location.");
            }
            else if(Game.GameTime-StartedAt>480000)Resolve("~r~Fugitive Trail: scent trail expired.");

            base.Process();
        }
    }
}
