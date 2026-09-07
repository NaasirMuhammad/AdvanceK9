using LSPD_First_Response.Mod.API;
using LSPD_First_Response.Mod.Callouts;
using Rage;
using Rage.Native;

namespace AdvancedK9.Callouts
{
    [CalloutInfo("AdvancedK9: Fugitive Trail",CalloutProbability.Medium)]
    public sealed class FugitiveTrailCallout : AdvancedK9Callout
    {
        private enum FugitivePhase{EnRoute,AwaitingScent,Tracking,Located,Medical,Custody,Transport,Complete}
        private FugitivePhase _phase=FugitivePhase.EnRoute;
        private uint _phaseStarted;
        private bool _custodyConfirmed;
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
                Vector3 curbPosition=SceneVehicle.GetOffsetPosition(new Vector3(4.5f,0f,0f));
                SceneVehicle.Position=curbPosition;Scene=curbPosition;CalloutPosition=Scene;
                Game.LogTrivial("AdvancedK9 Callouts: failed traffic stop shifted 4.5 metres toward the curb instead of across multiple lanes.");
            }
            StagePoliceScene();
            ConfigureTrafficStopScene();
            ControlSceneTraffic();
            return SceneVehicle!=null&&SceneVehicle.Exists();
        }

        public override bool OnCalloutAccepted()
        {
            StartedAt=Game.GameTime;_phaseStarted=StartedAt;_phase=FugitivePhase.EnRoute;_outcome=Random.Next(4);
            StagePoliceScene();
            ConfigureTrafficStopScene();
            float angle=Random.Next(360);float distance=Random.Next(65,96);Vector3 trailEnd=Scene;
            for(int attempt=0;attempt<10;attempt++)
            {
                float radians=(float)(angle*System.Math.PI/180.0);
                Vector3 candidate=World.GetNextPositionOnStreet(Scene+new Vector3((float)System.Math.Sin(radians)*distance,(float)System.Math.Cos(radians)*distance,0f));
                int interior=NativeFunction.Natives.GET_INTERIOR_AT_COORDS<int>(candidate.X,candidate.Y,candidate.Z);
                if(System.Math.Abs(candidate.Z-Scene.Z)<=3f&&interior==0){trailEnd=candidate;break;}
                angle=(angle+37f)%360f;
            }
            if(trailEnd.DistanceTo(Scene)<50f)
            {
                float radians=(float)(angle*System.Math.PI/180.0);
                trailEnd=Scene+new Vector3((float)System.Math.Sin(radians)*75f,(float)System.Math.Cos(radians)*75f,0f);
            }
            float dx=trailEnd.X-Scene.X,dy=trailEnd.Y-Scene.Y;float length=(float)System.Math.Sqrt(dx*dx+dy*dy);
            if(length<.1f){dx=1f;dy=0f;length=1f;}
            float coverSide=Random.Next(2)==0?-8f:8f;
            Vector3 coverPosition=new Vector3(trailEnd.X-dy/length*coverSide,trailEnd.Y+dx/length*coverSide,trailEnd.Z);
            CoverProp=SpawnProp("prop_dumpster_01a",coverPosition);
            Vector3 hidingPosition=new Vector3(coverPosition.X+dx/length*1.35f,coverPosition.Y+dy/length*1.35f,coverPosition.Z);
            Game.LogTrivial("AdvancedK9 Callouts: guaranteed physical cover staged off the roadway; subject position is shielded from the scene side.");
            Vector3 escapeStart=new Vector3(Scene.X+dx/length*48f-dy/length*8f,Scene.Y+dy/length*48f+dx/length*8f,Scene.Z);
            Subject=SpawnPed("a_m_m_hillbilly_01",escapeStart,Random.Next(360));if(Subject==null)return false;
            Subject.MaxHealth=500;Subject.Health=500;Subject.BlockPermanentEvents=true;Subject.IsPersistent=true;
            NativeFunction.Natives.TASK_FOLLOW_NAV_MESH_TO_COORD(Subject,hidingPosition.X,hidingPosition.Y,hidingPosition.Z,5.2f,22000,2f,0,0f);
            var escapingSubject=Subject;var finalCover=hidingPosition;
            GameFiber.StartNew(delegate
            {
                try
                {
                    uint escapeStarted=Game.GameTime,nextRetry=0;
                    while(!Finished&&escapingSubject!=null&&escapingSubject.Exists()&&escapingSubject.DistanceTo(finalCover)>5f&&Game.GameTime-escapeStarted<90000)
                    {
                        if(Game.GameTime>=nextRetry)
                        {
                            nextRetry=Game.GameTime+4500;
                            NativeFunction.Natives.TASK_FOLLOW_NAV_MESH_TO_COORD(escapingSubject,finalCover.X,finalCover.Y,finalCover.Z,5.4f,12000,2f,0,0f);
                        }
                        if(Game.GameTime-escapeStarted>14000&&Game.LocalPlayer.Character.DistanceTo(escapingSubject)>85f)
                        {
                            escapingSubject.Position=finalCover;
                            break;
                        }
                        GameFiber.Wait(250);
                    }
                    if(!Finished&&escapingSubject!=null&&escapingSubject.Exists()&&escapingSubject.DistanceTo(finalCover)<=7f)
                        NativeFunction.Natives.TASK_COWER(escapingSubject,-1);
                }
                catch(System.Exception ex){Game.LogTrivial("AdvancedK9 Callouts: fugitive escape staging contained: "+ex.Message);}
            },"AdvancedK9 fugitive escape to cover");
            _coverConfirmed=true;_nextCoverSearch=Game.GameTime+9000;
            Game.LogTrivial("AdvancedK9 Callouts: fugitive trail constrained to an outdoor 65-95 metre pedestrian-scale search with guaranteed cover.");
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

        private bool SubjectInCustody()
        {
            if(Subject==null||!Subject.Exists())return false;
            if(NativeFunction.Natives.IS_PED_CUFFED<bool>(Subject))return true;
            try
            {
                var method=typeof(Functions).GetMethod("IsPedArrested",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static);
                if(method!=null)return (bool)method.Invoke(null,new object[]{Subject});
            }
            catch(System.Exception ex){Game.LogTrivial("AdvancedK9 Callouts: LSPDFR arrest-state reflection fallback: "+ex.Message);}
            return false;
        }

        public override void Process()
        {
            if(Finished||Subject==null||!Subject.Exists()){if(!Finished)Resolve("~r~Fugitive Trail ended: suspect unavailable.");return;}
            var player=Game.LocalPlayer.Character;
            if(player.DistanceTo(Scene)<350f&&(PoliceVehicle==null||!PoliceVehicle.Exists()||OfficerOne==null||!OfficerOne.Exists()||OfficerTwo==null||!OfficerTwo.Exists()))StagePoliceScene();

            if(!_sceneBriefed&&player.DistanceTo(Scene)<28f)
            {
                _sceneBriefed=true;_phase=FugitivePhase.AwaitingScent;_phaseStarted=Game.GameTime;
                Game.DisplayNotification("~b~On-scene officer:~s~ The suspect fled on foot. I preserved their scent from the driver seat.~n~~y~Deploy Rex beside the abandoned vehicle and command TRACK.");
            }
            if(!ApiRequested&&_sceneBriefed)
            {
                AssignCalloutScent(Subject,"preserved scent article from abandoned vehicle driver seat");
                if(ApiRequested){ClearSceneRoute();Game.LogTrivial("AdvancedK9 Callouts: fugitive vehicle scent source registered; awaiting handler command.");}
            }

            if(ApiRequested&&K9TrackingActive()&&_phase==FugitivePhase.AwaitingScent){_phase=FugitivePhase.Tracking;_phaseStarted=Game.GameTime;Game.LogTrivial("AdvancedK9 Callouts: FugitiveTrail phase -> Tracking.");}
            if(_phase==FugitivePhase.Tracking&&!_suspectLocated&&!_supportCommitted&&(K9TrackingActive()||player.DistanceTo(Scene)>15f||K9DistanceTo(Scene)>12f))
            {
                _supportCommitted=true;
                Game.DisplayNotification("~b~On-scene officers:~s~ We are moving behind the K9 team.");
                Game.LogTrivial("AdvancedK9 Callouts: support movement armed by handler departure from scent scene.");
            }
            if(_phase==FugitivePhase.Tracking&&_supportCommitted&&!_suspectLocated)SupportOfficersFollowK9();

            float rexDistance=K9DistanceTo(Subject.Position);
            if(ApiRequested&&!_suspectLocated&&!_rexReachedSubject&&rexDistance<3f)
            {
                _rexReachedSubject=true;_rexReachedAt=Game.GameTime;
                Game.LogTrivial("AdvancedK9 Callouts: Rex physically reached FugitiveTrail subject; preserving the suspect's cover/flee task until the alert transition.");
            }
            if(_rexReachedSubject&&!_suspectLocated&&Game.GameTime-_rexReachedAt>=2800)
            {
                _suspectLocated=true;_locatedAt=Game.GameTime;_phase=FugitivePhase.Located;_phaseStarted=Game.GameTime;
                EndSupportTracking();
                SubjectBlip=Subject.AttachBlip();SubjectBlip.IsRouteEnabled=true;Subject.IsInvincible=true;
                if(_outcome==0)NativeFunction.Natives.TASK_HANDS_UP(Subject,120000,player,-1,true);
                else NativeFunction.Natives.TASK_SMART_FLEE_PED(Subject,player,700f,-1,false,false);
                ControlApprehensionTraffic(Subject.Position);
                SupportOfficersContainSubject();
                Game.DisplayNotification(_outcome==0?"~g~Rex alerted on the hidden fugitive. The suspect is surrendering; secure the arrest.":"~o~Rex alerted and flushed the fugitive from cover. Tracking is complete; command APPREHEND if deployment is justified.");
                Game.LogTrivial("AdvancedK9 Callouts: alert-first FugitiveTrail transition completed after Rex reached the stationary hidden subject.");
            }

            if(_suspectLocated)
            {
                bool injured=!Subject.IsDead&&(Subject.Health<Subject.MaxHealth-5||Subject.IsRagdoll);
                if(Subject.IsDead){_phase=FugitivePhase.Complete;Resolve("~r~Fugitive Trail failed: suspect died before safe custody and transport.");}
                else if((injured||MedicalResponseStarted)&&!MedicalResponseComplete)
                {
                    if(ProcessPostApprehensionMedical("")){_phase=FugitivePhase.Medical;_phaseStarted=Game.GameTime;}
                }
                else if(SeriousMedicalTransport&&MedicalResponseComplete)
                {
                    _phase=FugitivePhase.Complete;
                    Resolve("~g~Fugitive Trail complete: EMS transported the seriously injured suspect under LSPDFR custody.");
                }
                else if(SubjectInCustody())
                {
                    if(!_custodyConfirmed)
                    {
                        _custodyConfirmed=true;_phase=FugitivePhase.Custody;_phaseStarted=Game.GameTime;
                        Subject.Health=System.Math.Max(Subject.Health,System.Math.Max(100,Subject.MaxHealth*3/4));
                        Subject.IsInvincible=false;
                        Game.DisplayNotification("~b~Custody confirmed:~s~ LSPDFR now owns the arrest and transport. AdvancedK9 will remain active until handoff.");
                        Game.LogTrivial("AdvancedK9 Callouts: FugitiveTrail phase -> Custody; no AdvancedK9 ped arrest or transport tasks will be issued.");
                    }
                    if(NativeFunction.Natives.IS_PED_IN_ANY_VEHICLE<bool>(Subject,false))
                    {
                        if(_phase!=FugitivePhase.Transport){_phase=FugitivePhase.Transport;_phaseStarted=Game.GameTime;Game.LogTrivial("AdvancedK9 Callouts: FugitiveTrail phase -> Transport (LSPDFR vehicle handoff confirmed).");}
                        else if(Game.GameTime-_phaseStarted>3000){_phase=FugitivePhase.Complete;Resolve("~g~Fugitive Trail complete: LSPDFR custody and prisoner transport confirmed.");}
                    }
                }
                else if(Game.GameTime-_locatedAt>600000)Resolve("~o~Fugitive Trail concluded after suspect location.");
            }
            else if(!ApiRequested&&Game.GameTime-StartedAt>900000)Resolve("~r~Fugitive Trail: response expired before scent collection.");
            base.Process();
        }
    }
}
