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
        private bool _sceneBriefed;
        private bool _suspectLocated;
        private bool _transportStarted;
        private uint _locatedAt;
        private uint _nextCoverSearch;
        private bool _coverConfirmed;

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
            ControlSceneTraffic();
            return SceneVehicle!=null&&SceneVehicle.Exists();
        }

        public override bool OnCalloutAccepted()
        {
            StartedAt=Game.GameTime;_outcome=Random.Next(4);
            StagePoliceScene();
            float angle=Random.Next(360);float distance=Random.Next(55,76);
            Vector3 startPosition=World.GetNextPositionOnStreet(Scene+new Vector3((float)System.Math.Sin(angle*System.Math.PI/180.0)*distance,(float)System.Math.Cos(angle*System.Math.PI/180.0)*distance,0f));
            Subject=SpawnPed("a_m_m_hillbilly_01",startPosition,Random.Next(360));if(Subject==null)return false;
            Subject.MaxHealth=250;Subject.Health=250;
            NativeFunction.Natives.TASK_SEEK_COVER_FROM_POS(Subject,Scene.X,Scene.Y,Scene.Z,15000,true);
            _nextCoverSearch=Game.GameTime+9000;
            Game.LogTrivial("AdvancedK9 Callouts: fugitive tasked through GTA cover navigation; he will keep moving until engine-valid cover is reached.");
            Functions.PlayScannerAudioUsingPosition("WE_HAVE CRIME_RESIST_ARREST IN_OR_ON_POSITION",Scene);
            RouteToScene("Respond to the abandoned vehicle. The on-scene officer has preserved a scent article from the driver seat.");
            return base.OnCalloutAccepted();
        }

        public override void Process()
        {
            if(Finished||Subject==null||!Subject.Exists()){if(!Finished)Resolve("~r~Fugitive Trail ended: suspect unavailable.");return;}
            var player=Game.LocalPlayer.Character;
            if(player.DistanceTo(Scene)<180f&&(PoliceVehicle==null||!PoliceVehicle.Exists()||OfficerOne==null||!OfficerOne.Exists()||OfficerTwo==null||!OfficerTwo.Exists()))StagePoliceScene();

            if(!_sceneBriefed&&player.DistanceTo(Scene)<28f)
            {
                _sceneBriefed=true;
                Game.DisplayNotification("~b~On-scene officer:~s~ The suspect fled on foot. I preserved their scent from the driver seat.~n~~y~Deploy Rex beside the abandoned vehicle and command TRACK.");
            }
            if(!ApiRequested&&_sceneBriefed)
            {
                AssignCalloutScent(Subject,"preserved scent article from abandoned vehicle driver seat");
                if(ApiRequested){ClearSceneRoute();Game.LogTrivial("AdvancedK9 Callouts: fugitive vehicle scent source registered; awaiting handler command.");}
            }

            if(!_suspectLocated&&!_coverConfirmed&&Game.GameTime>=_nextCoverSearch)
            {
                _nextCoverSearch=Game.GameTime+9000;
                _coverConfirmed=NativeFunction.Natives.IS_PED_IN_COVER<bool>(Subject,false);
                if(!_coverConfirmed)
                {
                    NativeFunction.Natives.TASK_SEEK_COVER_FROM_POS(Subject,Scene.X,Scene.Y,Scene.Z,15000,true);
                    Game.LogTrivial("AdvancedK9 Callouts: fugitive has not reached valid cover; cover-seeking run reissued.");
                }
                else Game.LogTrivial("AdvancedK9 Callouts: fugitive reached GTA engine-valid environmental cover.");
            }
            if(ApiRequested&&!_suspectLocated&&K9TrackingActive())SupportOfficersFollowK9();
            if(ApiRequested&&!_suspectLocated&&System.Math.Min(K9DistanceTo(Subject.Position),player.DistanceTo(Subject))<18f)
            {
                _suspectLocated=true;_locatedAt=Game.GameTime;
                SubjectBlip=Subject.AttachBlip();SubjectBlip.IsRouteEnabled=true;
                if(_outcome==0)NativeFunction.Natives.TASK_HANDS_UP(Subject,120000,player,-1,true);
                else NativeFunction.Natives.TASK_SMART_FLEE_PED(Subject,player,700f,-1,false,false);
                Game.DisplayNotification(_outcome==0?"~g~Rex located the hidden fugitive. The suspect is surrendering; secure the arrest.":"~o~Rex flushed the fugitive from cover. The suspect is fleeing; officers are moving with the K9 team.");
                Game.LogTrivial("AdvancedK9 Callouts: Rex located FugitiveTrail subject behind cover; persistent suspect marker created.");
            }

            if(_suspectLocated)
            {
                if(ProcessPostApprehensionMedical("~g~Fugitive Trail complete: EMS treated the suspect and patrol completed custody.")){}
                else if(Subject.IsDead)Resolve("~o~Fugitive Trail concluded: suspect is deceased.");
                else if(NativeFunction.Natives.IS_PED_CUFFED<bool>(Subject)&&!_transportStarted){_transportStarted=true;BeginAutomaticTransport("~g~Fugitive Trail complete: on-scene units transported the prisoner.");}
                else if(Game.GameTime-_locatedAt>600000)Resolve("~o~Fugitive Trail concluded after suspect location.");
            }
            else if(!ApiRequested&&Game.GameTime-StartedAt>900000)Resolve("~r~Fugitive Trail: response expired before scent collection.");
            base.Process();
        }
    }
}
