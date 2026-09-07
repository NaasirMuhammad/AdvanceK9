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
        private bool _rexReachedSubject;
        private uint _rexReachedAt;
        private bool _supportCommitted;

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
            Vector3 trailEnd=World.GetNextPositionOnStreet(Scene+new Vector3((float)System.Math.Sin(angle*System.Math.PI/180.0)*distance,(float)System.Math.Cos(angle*System.Math.PI/180.0)*distance,0f));
            Vector3 hidingPosition;
            if(!TryFindExistingCover(trailEnd,out hidingPosition))
            {
                float dx=trailEnd.X-Scene.X,dy=trailEnd.Y-Scene.Y;float length=(float)System.Math.Sqrt(dx*dx+dy*dy);
                if(length<.1f){dx=1f;dy=0f;length=1f;}
                float side=Random.Next(2)==0?-8f:8f;
                Vector3 coverPosition=new Vector3(trailEnd.X-dy/length*side,trailEnd.Y+dx/length*side,trailEnd.Z);
                hidingPosition=new Vector3(coverPosition.X+dx/length*1.7f,coverPosition.Y+dy/length*1.7f,coverPosition.Z);
                Game.LogTrivial("AdvancedK9 Callouts: remote map cover was not streamed; off-road fallback selected without spawning artificial cover.");
            }
            Subject=SpawnPed("a_m_m_hillbilly_01",hidingPosition,Random.Next(360));if(Subject==null)return false;
            Subject.MaxHealth=250;Subject.Health=250;
            NativeFunction.Natives.TASK_START_SCENARIO_IN_PLACE(Subject,"WORLD_HUMAN_BUM_SLUMPED",0,true);
            _coverConfirmed=true;_nextCoverSearch=Game.GameTime+9000;
            Game.LogTrivial("AdvancedK9 Callouts: fugitive staged low and stationary at an off-road hiding point before scent collection.");
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
                Vector3 streamedCover;
                if(TryFindExistingCover(Subject.Position,out streamedCover))
                {
                    Subject.Position=streamedCover;
                    NativeFunction.Natives.TASK_START_SCENARIO_IN_PLACE(Subject,"WORLD_HUMAN_BUM_SLUMPED",0,true);
                    Game.LogTrivial("AdvancedK9 Callouts: fugitive moved to streamed world cover before scent handoff; no artificial cover prop used.");
                }
                Game.DisplayNotification("~b~On-scene officer:~s~ The suspect fled on foot. I preserved their scent from the driver seat.~n~~y~Deploy Rex beside the abandoned vehicle and command TRACK.");
            }
            if(!ApiRequested&&_sceneBriefed)
            {
                AssignCalloutScent(Subject,"preserved scent article from abandoned vehicle driver seat");
                if(ApiRequested){ClearSceneRoute();Game.LogTrivial("AdvancedK9 Callouts: fugitive vehicle scent source registered; awaiting handler command.");}
            }

            if(ApiRequested&&!_suspectLocated&&!_supportCommitted&&K9ReadyOnFoot()&&(K9DistanceTo(Scene)>7f||K9TrackingActive()))
            {
                _supportCommitted=true;
                Game.DisplayNotification("~b~On-scene officers:~s~ We are moving behind the K9 team.");
                Game.LogTrivial("AdvancedK9 Callouts: support movement armed by handler departure from scent scene.");
            }
            if(_supportCommitted&&!_suspectLocated)SupportOfficersFollowK9();

            float rexDistance=K9DistanceTo(Subject.Position);
            if(ApiRequested&&!_suspectLocated&&!_rexReachedSubject&&rexDistance<3f)
            {
                _rexReachedSubject=true;_rexReachedAt=Game.GameTime;
                NativeFunction.Natives.TASK_STAND_STILL(Subject,5000);
                Game.LogTrivial("AdvancedK9 Callouts: Rex physically reached FugitiveTrail subject; waiting for core alert bark and tracking release.");
            }
            if(_rexReachedSubject&&!_suspectLocated&&Game.GameTime-_rexReachedAt>=2800)
            {
                _suspectLocated=true;_locatedAt=Game.GameTime;
                SubjectBlip=Subject.AttachBlip();SubjectBlip.IsRouteEnabled=true;
                if(_outcome==0)NativeFunction.Natives.TASK_HANDS_UP(Subject,120000,player,-1,true);
                else NativeFunction.Natives.TASK_SMART_FLEE_PED(Subject,player,700f,-1,false,false);
                Game.DisplayNotification(_outcome==0?"~g~Rex alerted on the hidden fugitive. The suspect is surrendering; secure the arrest.":"~o~Rex alerted and flushed the fugitive from cover. Tracking is complete; command APPREHEND if deployment is justified.");
                Game.LogTrivial("AdvancedK9 Callouts: alert-first FugitiveTrail transition completed after Rex reached the stationary hidden subject.");
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
