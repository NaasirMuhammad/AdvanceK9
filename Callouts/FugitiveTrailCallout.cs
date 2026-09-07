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
                if(!Prepare("Traffic stop — driver fled on foot",StreetOffset(520f,Random.Next(-160,161)),110f))return false;
            }
            catch(System.Exception ex)
            {
                Game.LogTrivial("AdvancedK9 Callouts: "+GetType().Name+" primary scene calculation failed; using safe fallback: "+ex);
                var playerPosition=Game.LocalPlayer.Character.Position;
                if(!Prepare("Fugitive trail from abandoned vehicle",new Vector3(playerPosition.X+260f,playerPosition.Y,playerPosition.Z),110f))return false;
            }
            float trafficHeading=Game.LocalPlayer.Character.Heading;
            SceneVehicle=SpawnVehicle("primo",Scene,trafficHeading);
            if(SceneVehicle!=null&&SceneVehicle.Exists())
            {
                Vector3 curbPosition=SceneVehicle.GetOffsetPosition(new Vector3(8f,0f,0f));
                SceneVehicle.Position=curbPosition;Scene=curbPosition;CalloutPosition=Scene;
                Game.LogTrivial("AdvancedK9 Callouts: failed traffic stop shifted 8 metres to the roadside shoulder.");
            }
            StagePoliceScene();
            ConfigureTrafficStopScene();
            ControlSceneTraffic();
            return SceneVehicle!=null&&SceneVehicle.Exists();
        }

        public override bool OnCalloutAccepted()
        {
            StartedAt=Game.GameTime;_outcome=Random.Next(4);
            StagePoliceScene();
            ConfigureTrafficStopScene();
            float angle=Random.Next(360);float distance=Random.Next(85,116);Vector3 trailEnd=Scene;
            for(int attempt=0;attempt<10;attempt++)
            {
                float radians=(float)(angle*System.Math.PI/180.0);
                Vector3 candidate=World.GetNextPositionOnStreet(Scene+new Vector3((float)System.Math.Sin(radians)*distance,(float)System.Math.Cos(radians)*distance,0f));
                if(System.Math.Abs(candidate.Z-Scene.Z)<=4f){trailEnd=candidate;break;}
                angle=(angle+37f)%360f;
            }
            if(trailEnd.DistanceTo(Scene)<50f)
            {
                float radians=(float)(angle*System.Math.PI/180.0);
                trailEnd=Scene+new Vector3((float)System.Math.Sin(radians)*90f,(float)System.Math.Cos(radians)*90f,0f);
            }
            float dx=trailEnd.X-Scene.X,dy=trailEnd.Y-Scene.Y;float length=(float)System.Math.Sqrt(dx*dx+dy*dy);
            if(length<.1f){dx=1f;dy=0f;length=1f;}
            Vector3 hidingPosition;
            if(!TryFindExistingCover(trailEnd,out hidingPosition))
            {
                float side=Random.Next(2)==0?-14f:14f;
                hidingPosition=new Vector3(trailEnd.X-dy/length*side,trailEnd.Y+dx/length*side,trailEnd.Z);
                Game.LogTrivial("AdvancedK9 Callouts: fugitive destination placed well off the roadway when streamed cover was unavailable.");
            }
            CoverProp=SpawnProp("prop_dumpster_01a",hidingPosition);
            if(CoverProp!=null&&CoverProp.Exists())hidingPosition=new Vector3(hidingPosition.X+dx/length*2.2f,hidingPosition.Y+dy/length*2.2f,hidingPosition.Z);
            Vector3 escapeStart=new Vector3(Scene.X+dx/length*48f-dy/length*8f,Scene.Y+dy/length*48f+dx/length*8f,Scene.Z);
            Subject=SpawnPed("a_m_m_hillbilly_01",escapeStart,Random.Next(360));if(Subject==null)return false;
            Subject.MaxHealth=500;Subject.Health=500;Subject.BlockPermanentEvents=true;Subject.IsPersistent=true;
            NativeFunction.Natives.TASK_FOLLOW_NAV_MESH_TO_COORD(Subject,hidingPosition.X,hidingPosition.Y,hidingPosition.Z,5.2f,22000,2f,0,0f);
            var escapingSubject=Subject;var finalCover=hidingPosition;
            GameFiber.StartNew(delegate
            {
                try
                {
                    GameFiber.Wait(18000);
                    if(!Finished&&escapingSubject!=null&&escapingSubject.Exists())
                    {
                        if(escapingSubject.DistanceTo(finalCover)>12f&&Game.LocalPlayer.Character.DistanceTo(escapingSubject)>80f)escapingSubject.Position=finalCover;
                        NativeFunction.Natives.TASK_STAND_STILL(escapingSubject,-1);
                    }
                }
                catch(System.Exception ex){Game.LogTrivial("AdvancedK9 Callouts: fugitive escape staging contained: "+ex.Message);}
            },"AdvancedK9 fugitive escape to cover");
            _coverConfirmed=true;_nextCoverSearch=Game.GameTime+9000;
            Game.LogTrivial("AdvancedK9 Callouts: fugitive actively escaping from the stop toward cover 85-115 metres away; dumpster fallback guarantees concealment and no sleeping scenario is used.");
            Functions.PlayScannerAudioUsingPosition("WE_HAVE CRIME_RESIST_ARREST IN_OR_ON_POSITION",Scene);
            RouteToScene("Respond to the failed traffic stop. The driver abandoned the stopped vehicle and fled on foot; officers preserved the driver-seat scent.");
            return base.OnCalloutAccepted();
        }

        private void ConfigureTrafficStopScene()
        {
            if(SceneVehicle!=null&&SceneVehicle.Exists())
            {
                NativeFunction.Natives.SET_VEHICLE_ENGINE_ON(SceneVehicle,false,true,true);
                SceneVehicle.IsPersistent=true;
            }
            if(PoliceVehicle!=null&&PoliceVehicle.Exists())
            {
                NativeFunction.Natives.SET_VEHICLE_SIREN(PoliceVehicle,true);
                NativeFunction.Natives.SET_VEHICLE_LIGHTS(PoliceVehicle,2);
            }
            Game.LogTrivial("AdvancedK9 Callouts: failed-traffic-stop scene staged with suspect vehicle stopped and marked cruiser behind it.");
        }

        public override void Process()
        {
            if(Finished||Subject==null||!Subject.Exists()){if(!Finished)Resolve("~r~Fugitive Trail ended: suspect unavailable.");return;}
            var player=Game.LocalPlayer.Character;
            if(player.DistanceTo(Scene)<350f&&(PoliceVehicle==null||!PoliceVehicle.Exists()||OfficerOne==null||!OfficerOne.Exists()||OfficerTwo==null||!OfficerTwo.Exists()))StagePoliceScene();

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

            if(ApiRequested&&!_suspectLocated&&!_supportCommitted&&(K9TrackingActive()||player.DistanceTo(Scene)>15f||K9DistanceTo(Scene)>12f))
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
                SubjectBlip=Subject.AttachBlip();SubjectBlip.IsRouteEnabled=true;Subject.IsInvincible=true;
                if(_outcome==0)NativeFunction.Natives.TASK_HANDS_UP(Subject,120000,player,-1,true);
                else NativeFunction.Natives.TASK_SMART_FLEE_PED(Subject,player,700f,-1,false,false);
                Game.DisplayNotification(_outcome==0?"~g~Rex alerted on the hidden fugitive. The suspect is surrendering; secure the arrest.":"~o~Rex alerted and flushed the fugitive from cover. Tracking is complete; command APPREHEND if deployment is justified.");
                Game.LogTrivial("AdvancedK9 Callouts: alert-first FugitiveTrail transition completed after Rex reached the stationary hidden subject.");
            }

            if(_suspectLocated)
            {
                if(ProcessPostApprehensionMedical("~g~Fugitive Trail complete: EMS treated the suspect and patrol completed custody.")){}
                else if(Subject.IsDead)Resolve("~o~Fugitive Trail concluded: suspect is deceased.");
                else if((NativeFunction.Natives.IS_PED_CUFFED<bool>(Subject)||Functions.IsPedArrested(Subject))&&!_transportStarted){_transportStarted=true;BeginAutomaticTransport("~g~Fugitive Trail complete: on-scene units transported the prisoner.");}
                else if(Game.GameTime-_locatedAt>600000)Resolve("~o~Fugitive Trail concluded after suspect location.");
            }
            else if(!ApiRequested&&Game.GameTime-StartedAt>900000)Resolve("~r~Fugitive Trail: response expired before scent collection.");
            base.Process();
        }
    }
}
