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
        private float _sceneHeading;
        private bool _deathReported;
        private Vector3 _hidingPosition;
        private uint _nextHideTask;
        private static int _lastRoadsideScene=-1;
        private uint _verbalGraceUntil;
        private bool _outcomeTaskIssued;

        private static readonly Vector3[] RoadsideScenes={
            new Vector3(-565f,-675f,33f),new Vector3(-1310f,-1261f,4f),new Vector3(-1430f,-590f,30f),
            new Vector3(1208f,-1396f,35f),new Vector3(1110f,-1730f,35f),new Vector3(830f,-1830f,29f),
            new Vector3(1000f,-2535f,28f),new Vector3(-296f,-2732f,6f),
            new Vector3(1690f,3591f,35f),new Vector3(1865f,3684f,34f),new Vector3(1180f,2690f,38f),
            new Vector3(-445f,6037f,31f),new Vector3(-153f,6346f,31f),new Vector3(-2535f,2341f,33f)
        };

        private static readonly float[] RoadsideHeadings={270f,110f,90f,180f,180f,180f,85f,145f,210f,30f,180f,135f,45f,95f};

        private bool PrepareRoadsideScene()
        {
            var player=Game.LocalPlayer.Character;
            int best=-1;float bestDistance=float.MaxValue;
            for(int i=0;i<RoadsideScenes.Length;i++)
            {
                float distance=player.DistanceTo(RoadsideScenes[i]);
                if(i!=_lastRoadsideScene&&distance<bestDistance&&distance>180f){best=i;bestDistance=distance;}
            }
            if(best<0)
            {
                for(int i=0;i<RoadsideScenes.Length;i++)
                {
                    float distance=player.DistanceTo(RoadsideScenes[i]);
                    if(i!=_lastRoadsideScene&&distance<bestDistance){best=i;bestDistance=distance;}
                }
            }
            if(best<0)return false;
            _lastRoadsideScene=best;
            _sceneHeading=RoadsideHeadings[best];
            int interior=NativeFunction.Natives.GET_INTERIOR_AT_COORDS<int>(RoadsideScenes[best].X,RoadsideScenes[best].Y,RoadsideScenes[best].Z);
            if(interior!=0)return false;
            return Prepare("Traffic stop — driver fled on foot",RoadsideScenes[best],75f,false);
        }

        public override bool OnBeforeCalloutDisplayed()
        {
            try
            {
                if(!PrepareRoadsideScene())return false;
            }
            catch(System.Exception ex)
            {
                Game.LogTrivial("AdvancedK9 Callouts: "+GetType().Name+" primary scene calculation failed; using safe fallback: "+ex);
                return false;
            }
            Game.LogTrivial("AdvancedK9 Callouts: roadside callout location reserved; scene entities will spawn only after acceptance and trail planning.");
            return true;
        }

        public override bool OnCalloutAccepted()
        {
            StartedAt=Game.GameTime;_phaseStarted=StartedAt;_phase=FugitivePhase.EnRoute;_outcome=Random.Next(4);
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
            Vector3 coverPosition=trailEnd;bool offRoadCover=false;
            float[] offsets={10f,-10f,14f,-14f,18f,-18f};
            for(int i=0;i<offsets.Length;i++)
            {
                Vector3 candidate=new Vector3(trailEnd.X-dy/length*offsets[i],trailEnd.Y+dx/length*offsets[i],trailEnd.Z);
                int interior=NativeFunction.Natives.GET_INTERIOR_AT_COORDS<int>(candidate.X,candidate.Y,candidate.Z);
                bool onRoad=NativeFunction.Natives.IS_POINT_ON_ROAD<bool>(candidate.X,candidate.Y,candidate.Z,0);
                if(interior==0&&!onRoad){coverPosition=candidate;offRoadCover=true;break;}
            }
            if(!offRoadCover)
            {
                Game.LogTrivial("AdvancedK9 Callouts: rejected FugitiveTrail scene because no safe off-road hiding position was available.");
                return false;
            }
            Vector3 hidingPosition;
            if(!TryFindExistingCover(coverPosition,out hidingPosition))
            {
                // Distant world props are not streamed before the player reaches the callout.
                // Keep the validated off-road endpoint instead of aborting a valid callout.
                hidingPosition=coverPosition;
                Game.LogTrivial("AdvancedK9 Callouts: distant cover is not streamed yet; using the validated off-road hiding endpoint and resolving live cover on approach.");
            }
            else Game.LogTrivial("AdvancedK9 Callouts: existing environmental cover reserved near the pedestrian trail end.");
            _hidingPosition=hidingPosition;

            SceneVehicle=SpawnVehicle("primo",Scene,_sceneHeading);
            if(SceneVehicle==null||!SceneVehicle.Exists())return false;
            SceneVehicle.IsPersistent=true;
            if(!StagePoliceScene())return false;
            ConfigureTrafficStopScene();
            ControlSceneTraffic();
            Game.LogTrivial("AdvancedK9 Callouts: accepted roadside scene spawned after successful trail validation.");

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
                    {
                        escapingSubject.Position=finalCover;
                        NativeFunction.Natives.TASK_SEEK_COVER_FROM_POS(escapingSubject,Scene.X,Scene.Y,Scene.Z,-1,false);
                        GameFiber.Wait(1200);
                        if(escapingSubject.Exists())NativeFunction.Natives.TASK_STAY_IN_COVER(escapingSubject);
                    }
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
                PoliceVehicle.IsPersistent=true;
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
            ControlLiveTraffic(Scene,42f);
            if(_suspectLocated)ControlLiveTraffic(Subject.Position,48f);
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
            if(_phase==FugitivePhase.Tracking&&!_suspectLocated&&!_supportCommitted&&K9TrackingActive())
            {
                _supportCommitted=true;
                Game.DisplayNotification("~b~On-scene officers:~s~ We are moving behind the K9 team.");
                Game.LogTrivial("AdvancedK9 Callouts: support movement armed by handler departure from scent scene.");
            }
            if(_phase==FugitivePhase.Tracking&&_supportCommitted&&!_suspectLocated)SupportOfficersFollowK9();

            if(!_suspectLocated&&Subject.DistanceTo(_hidingPosition)<7f&&Game.GameTime>=_nextHideTask)
            {
                _nextHideTask=Game.GameTime+3500;
                NativeFunction.Natives.TASK_COWER(Subject,5000);
            }

            float rexDistance=K9DistanceTo(Subject.Position);
            if(ApiRequested&&!_suspectLocated&&!_rexReachedSubject&&rexDistance<3f)
            {
                _rexReachedSubject=true;_rexReachedAt=Game.GameTime;
                Game.LogTrivial("AdvancedK9 Callouts: Rex physically reached FugitiveTrail subject; preserving the suspect's cover/flee task until the alert transition.");
            }
            if(_rexReachedSubject&&!_suspectLocated&&Game.GameTime-_rexReachedAt>=500)
            {
                _suspectLocated=true;_locatedAt=Game.GameTime;_phase=FugitivePhase.Located;_phaseStarted=Game.GameTime;
                EndSupportTracking();
                SubjectBlip=Subject.AttachBlip();SubjectBlip.IsRouteEnabled=true;Subject.IsInvincible=false;
                _verbalGraceUntil=Game.GameTime+12000;
                ControlApprehensionTraffic(Subject.Position);
                SupportOfficersContainSubject();
                Game.DisplayNotification("~o~Rex alerted on the hidden fugitive.~s~ Tracking is complete. Give verbal commands through NPCI; the suspect may surrender, flee, or resist.");
                Game.LogTrivial("AdvancedK9 Callouts: alert-first FugitiveTrail transition completed after Rex reached the stationary hidden subject.");
            }

            if(_suspectLocated)
            {
                ObserveCooperativeControl("verbal challenge");
                if(!_outcomeTaskIssued&&SubjectIsComplying())
                {
                    _outcomeTaskIssued=true;
                    Game.DisplayNotification("~g~Suspect is complying with verbal commands.~s~ Move in and complete the LSPDFR arrest.");
                }
                else if(!_outcomeTaskIssued&&Game.GameTime>=_verbalGraceUntil)
                {
                    _outcomeTaskIssued=true;
                    if(_outcome==0)NativeFunction.Natives.TASK_HANDS_UP(Subject,120000,player,-1,true);
                    else NativeFunction.Natives.TASK_SMART_FLEE_PED(Subject,player,700f,-1,false,false);
                    Game.LogTrivial("AdvancedK9 Callouts: verbal challenge window expired; callout outcome resumed because NPCI/LSPDFR reported no compliance.");
                }
                bool injured=!Subject.IsDead&&(Subject.Health<Subject.MaxHealth-5||Subject.IsRagdoll);
                bool custody=SubjectInCustody();
                bool confirmedDead=Subject.Health<=0&&NativeFunction.Natives.IS_PED_DEAD_OR_DYING<bool>(Subject,true)&&!custody;
                if(confirmedDead)
                {
                    if(!_deathReported)
                    {
                        _deathReported=true;
                        Game.DisplayNotification("~r~Fugitive became unresponsive in custody. The callout scene will remain active for investigation; end it manually when finished.");
                        Game.LogTrivial("AdvancedK9 Callouts: suspect death detected; automatic scene/evidence cleanup suppressed pending manual callout end.");
                    }
                    return;
                }
                else if((injured||MedicalResponseStarted)&&!MedicalResponseComplete)
                {
                    if(ProcessPostApprehensionMedical("")){_phase=FugitivePhase.Medical;_phaseStarted=Game.GameTime;}
                }
                else if(SeriousMedicalTransport&&MedicalResponseComplete)
                {
                    _phase=FugitivePhase.Complete;
                    Resolve("~g~Fugitive Trail complete: EMS transported the seriously injured suspect under LSPDFR custody.");
                }
                else if(custody)
                {
                    if(!_custodyConfirmed)
                    {
                        _custodyConfirmed=true;_phase=FugitivePhase.Custody;_phaseStarted=Game.GameTime;
                        Subject.Health=System.Math.Max(Subject.Health,System.Math.Max(100,Subject.MaxHealth*3/4));
                        Subject.IsInvincible=false;
                        Game.DisplayNotification("~b~Custody confirmed:~s~ LSPDFR owns the arrest state. The on-scene patrol unit is assigned as the single transport owner.");
                        Game.LogTrivial("AdvancedK9 Callouts: FugitiveTrail phase -> Custody; on-scene patrol transport authorized after medical clearance.");
                    }
                    if(NativeFunction.Natives.IS_PED_IN_ANY_VEHICLE<bool>(Subject,false))
                    {
                        Subject.IsInvincible=false;
                        if(_phase!=FugitivePhase.Transport){_phase=FugitivePhase.Transport;_phaseStarted=Game.GameTime;Game.LogTrivial("AdvancedK9 Callouts: FugitiveTrail phase -> Transport (LSPDFR vehicle handoff confirmed).");}
                        else if(Game.GameTime-_phaseStarted>3000){_phase=FugitivePhase.Complete;Resolve("~g~Fugitive Trail complete: LSPDFR custody and prisoner transport confirmed.");}
                    }
                    else if((!MedicalResponseStarted||MedicalResponseComplete)&&!_transportStarted)
                    {
                        _transportStarted=true;_phase=FugitivePhase.Transport;_phaseStarted=Game.GameTime;
                        BeginAutomaticTransport("~g~Fugitive Trail complete: EMS clearance and on-scene patrol transport confirmed.");
                    }
                }
                else if(Game.GameTime-_locatedAt>600000)Resolve("~o~Fugitive Trail concluded after suspect location.");
            }
            else if(!ApiRequested&&Game.GameTime-StartedAt>900000)Resolve("~r~Fugitive Trail: response expired before scent collection.");
            base.Process();
        }
    }
}
