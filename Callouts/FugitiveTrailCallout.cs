using System.Collections.Generic;
using AdvancedK9.API;
using LSPD_First_Response.Mod.API;
using LSPD_First_Response.Mod.Callouts;
using Rage;
using Rage.Native;

namespace AdvancedK9.Callouts
{
    [CalloutInterfaceAPI.CalloutInterface("AdvancedK9: Fugitive Trail",CalloutProbability.Medium,"Failed traffic stop. Driver abandoned the vehicle and fled on foot. Responding K9 unit will collect preserved driver-seat scent and track the fugitive.","Code 2","LSPD")]
    public sealed class FugitiveTrailCallout : AdvancedK9Callout
    {
        private enum FugitivePhase{EnRoute,AwaitingScent,Tracking,Located,Medical,Custody,Transport,Complete}
        private FugitivePhase _phase=FugitivePhase.EnRoute;
        private uint _phaseStarted;
        private bool _custodyConfirmed;
        private int _outcome;
        private bool _sceneBriefed;
        private bool _suspectLocated;
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
        private static readonly Queue<int> RecentRoadsideScenes=new Queue<int>();
        private static readonly HashSet<int> FailedRoadsideScenes=new HashSet<int>();
        private int _sceneIndex=-1;
        private int _selectionAttempts;
        private uint _verbalGraceUntil;
        private bool _outcomeTaskIssued;
        private bool _fleeActive;
        private uint _nextPursuitTask;
        private Vector3 _lastSafeSubjectPosition;
        private bool _waterRecoveryUsed;
        private uint _nextCoverSeekTask;
        private bool _naturalCoverReserved;
        private bool _approachDispatchSent;
        private bool _trackingDispatchSent;
        private bool _locatedDispatchSent;
        private uint _deathCandidateSince;
        private uint _coverSearchStarted;
        private bool _activeEscapeFallback;
        private bool _controlDispatchSent;
        private bool _manualTransportNoticeSent;
        private Vector3 _curbAlignedVehiclePosition;
        private bool _isHidingAnimationPlaying;
        private bool _backupArrestAttempted;

        private static readonly Vector3[] RoadsideScenes={
            new Vector3(-565f,-675f,33f),new Vector3(-1035f,-2735f,20f),new Vector3(-1430f,-590f,30f),
            new Vector3(215f,-920f,30f),new Vector3(830f,-1830f,29f),
            new Vector3(-2250f,4290f,46f),new Vector3(-296f,-2732f,6f),
            new Vector3(1850f,3700f,34f),new Vector3(1080f,-690f,57f),new Vector3(-1500f,-790f,10f),
            new Vector3(116f,-1942f,20f),new Vector3(-153f,6346f,31f),new Vector3(-3150f,1100f,20f)
        };

        private static readonly float[] RoadsideHeadings={270f,150f,90f,160f,180f,145f,145f,30f,90f,140f,50f,45f,350f};

        private bool PrepareRoadsideScene()
        {
            if(++_selectionAttempts>RoadsideScenes.Length)return false;
            var player=Game.LocalPlayer.Character;
            var eligible=new List<int>();
            for(int i=0;i<RoadsideScenes.Length;i++)
            {
                if(RecentRoadsideScenes.Contains(i)||FailedRoadsideScenes.Contains(i))continue;
                float distance=player.DistanceTo(RoadsideScenes[i]);
                if(distance<180f||distance>2200f)continue;
                int interior=NativeFunction.Natives.GET_INTERIOR_AT_COORDS<int>(RoadsideScenes[i].X,RoadsideScenes[i].Y,RoadsideScenes[i].Z);
                if(interior!=0)continue;
                Vector3 street=World.GetNextPositionOnStreet(RoadsideScenes[i]);
                if(street.DistanceTo(RoadsideScenes[i])>18f||System.Math.Abs(street.Z-RoadsideScenes[i].Z)>3f)continue;
                eligible.Add(i);
            }
            if(eligible.Count==0)
            {
                FailedRoadsideScenes.Clear();
                for(int i=0;i<RoadsideScenes.Length;i++)if(!RecentRoadsideScenes.Contains(i))eligible.Add(i);
            }
            if(eligible.Count==0)return false;
            int best=eligible[Random.Next(eligible.Count)];
            RecentRoadsideScenes.Enqueue(best);
            while(RecentRoadsideScenes.Count>3)RecentRoadsideScenes.Dequeue();
            _sceneIndex=best;
            _sceneHeading=RoadsideHeadings[best];
            Vector3 stagedScene=RoadsideScenes[best];
            try
            {
                Vector3 curb;
                if(NativeFunction.Natives.GET_ROAD_BOUNDARY_USING_HEADING<bool>(stagedScene.X,stagedScene.Y,stagedScene.Z,_sceneHeading,out curb)&&curb.DistanceTo(stagedScene)<24f)
                {
                    Vector3 verifiedRoad;float ignoredNodeHeading;
                    bool roadNodeFound=NativeFunction.Natives.GET_CLOSEST_VEHICLE_NODE_WITH_HEADING<bool>(curb.X,curb.Y,curb.Z,out verifiedRoad,out ignoredNodeHeading,1,3f,0);
                    if(!roadNodeFound)verifiedRoad=World.GetNextPositionOnStreet(curb);
                    if(verifiedRoad.DistanceTo(curb)>8f||!NativeFunction.Natives.IS_POINT_ON_ROAD<bool>(verifiedRoad.X,verifiedRoad.Y,verifiedRoad.Z,0))
                    {
                        FailedRoadsideScenes.Add(best);
                        Game.LogTrivial("AdvancedK9 Callouts: scene "+best+" curb resolved into a driveway, parking apron, or private lot and was rejected.");
                        return PrepareRoadsideScene();
                    }
                    float inwardX=verifiedRoad.X-curb.X,inwardY=verifiedRoad.Y-curb.Y;
                    float inwardLength=(float)System.Math.Sqrt(inwardX*inwardX+inwardY*inwardY);
                    if(inwardLength<0.25f)
                    {
                        FailedRoadsideScenes.Add(best);
                        Game.LogTrivial("AdvancedK9 Callouts: scene "+best+" rejected because a stable curb-to-road inward vector could not be calculated.");
                        return PrepareRoadsideScene();
                    }
                    inwardX/=inwardLength;inwardY/=inwardLength;
                    float curbInset=best==11?0.90f:1.35f;
                    _curbAlignedVehiclePosition=new Vector3(curb.X+inwardX*curbInset,curb.Y+inwardY*curbInset,curb.Z);
                    stagedScene=_curbAlignedVehiclePosition;
                    Game.LogTrivial("AdvancedK9 Callouts: curb formation resolved for scene "+best+": boundary="+curb+", roadReference="+verifiedRoad+", vehicleCenter="+stagedScene+", fixedHeading="+_sceneHeading+".");
                }
                else
                {
                    FailedRoadsideScenes.Add(best);
                    Game.LogTrivial("AdvancedK9 Callouts: scene "+best+" rejected because GTA could not resolve a curb boundary; selecting another scene internally.");
                    return PrepareRoadsideScene();
                }
            }
            catch(System.Exception ex)
            {
                FailedRoadsideScenes.Add(best);
                Game.LogTrivial("AdvancedK9 Callouts: scene "+best+" curb calculation failed and was blacklisted: "+ex.Message);
                return PrepareRoadsideScene();
            }
            return Prepare("Traffic stop — driver fled on foot",stagedScene,75f,false);
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
            _isHidingAnimationPlaying=false;
            SetCustodyObservationEnabled(false);
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
                coverPosition=trailEnd;
                Game.LogTrivial("AdvancedK9 Callouts: distant off-road cover could not be verified; retaining the valid street endpoint until live cover streams.");
            }
            Vector3 hidingPosition;
            if(!TryFindExistingCover(coverPosition,out hidingPosition))
            {
                // Distant world props are not streamed before the player reaches the callout.
                hidingPosition=coverPosition;
                _coverConfirmed=false;
                Game.LogTrivial("AdvancedK9 Callouts: distant cover is not streamed yet; reserving an off-road endpoint and requiring live cover resolution on approach.");
            }
            else
            {
                _naturalCoverReserved=true;
                _coverConfirmed=false;
                Game.LogTrivial("AdvancedK9 Callouts: existing environmental cover reserved near the pedestrian trail end.");
            }
            _hidingPosition=hidingPosition;

            SceneVehicle=SpawnVehicle("primo",_curbAlignedVehiclePosition,_sceneHeading,true);
            if(SceneVehicle==null||!SceneVehicle.Exists())return RejectCurrentScene("suspect vehicle spawn failed");
            SceneVehicle.IsPersistent=true;
            SceneVehicle.Position=_curbAlignedVehiclePosition;
            SceneVehicle.Heading=_sceneHeading;
            NativeFunction.Natives.SET_VEHICLE_ON_GROUND_PROPERLY(SceneVehicle);
            GameFiber.Yield();
            float vehicleRadians=(float)(_sceneHeading*System.Math.PI/180.0);
            Vector3 roadForward=new Vector3(-(float)System.Math.Sin(vehicleRadians),(float)System.Math.Cos(vehicleRadians),0f);
            Vector3 cruiserPosition=SceneVehicle.Position-roadForward*9f;
            if(!StagePoliceScene(cruiserPosition,_sceneHeading,true))return RejectCurrentScene("police scene staging failed");
            if(PoliceVehicle==null||!PoliceVehicle.Exists())return RejectCurrentScene("police cruiser unavailable after staging");
            PoliceVehicle.Position=cruiserPosition;
            PoliceVehicle.Heading=_sceneHeading;
            PoliceVehicle.IsPersistent=true;
            NativeFunction.Natives.SET_VEHICLE_ON_GROUND_PROPERLY(PoliceVehicle);
            GameFiber.Yield();
            Game.LogTrivial("AdvancedK9 Callouts: final curb formation locked: suspect="+SceneVehicle.Position+", cruiser="+PoliceVehicle.Position+", heading="+_sceneHeading+", gap="+SceneVehicle.DistanceTo(PoliceVehicle)+".");
            ConfigureTrafficStopScene();
            ControlSceneTraffic();
            StartBackupOfficerInvestigationLoops();
            Game.LogTrivial("AdvancedK9 Callouts: accepted roadside scene spawned after successful trail validation.");

            Vector3 escapeStart=hidingPosition;
            Vector3 safeEscapeStart;
            if(TryResolveSafePedPosition(escapeStart,out safeEscapeStart))escapeStart=safeEscapeStart;
            Subject=SpawnPed("a_m_m_hillbilly_01",escapeStart,Random.Next(360));if(Subject==null||!Subject.Exists())return RejectCurrentScene("subject spawn failed");
            Subject.Tasks.Clear();Subject.MaxHealth=500;Subject.Health=500;Subject.BlockPermanentEvents=true;Subject.IsPersistent=true;
            var escapingSubject=Subject;var finalCover=hidingPosition;
            GameFiber.StartNew(delegate
            {
                try
                {
                    if(escapingSubject==null||!escapingSubject.Exists())return;
                    Game.LogTrivial("AdvancedK9 Callouts: assigning strict fugitive escape sequence.");
                    using(var sequence=new TaskSequence(escapingSubject))
                    {
                        sequence.Tasks.FollowNavigationMeshToPosition(finalCover,escapingSubject.Heading,5.2f);
                        sequence.Tasks.StandStill(500);
                    }
                    NativeFunction.Natives.SET_PED_KEEP_TASK(escapingSubject,true);
                    uint escapeStarted=Game.GameTime;
                    while(!Finished&&escapingSubject!=null&&escapingSubject.Exists()&&escapingSubject.DistanceTo(finalCover)>5f&&Game.GameTime-escapeStarted<90000)
                    {
                        if(Game.GameTime-escapeStarted>14000&&Game.LocalPlayer.Character.DistanceTo(escapingSubject)>85f)
                        {
                            Vector3 safeFinal;
                            if(TryResolveSafePedPosition(finalCover,out safeFinal))escapingSubject.Position=safeFinal;
                            break;
                        }
                        GameFiber.Wait(250);
                    }
                    if(!Finished&&escapingSubject!=null&&escapingSubject.Exists()&&escapingSubject.DistanceTo(finalCover)<=7f)
                    {
                        Vector3 safeFinal;
                        if(TryResolveSafePedPosition(finalCover,out safeFinal))escapingSubject.Position=safeFinal;
                        escapingSubject.Tasks.Clear();
                        Game.LogTrivial("AdvancedK9 Callouts: escape sequence completed; assigning cover behavior.");
                        NativeFunction.Natives.TASK_SEEK_COVER_FROM_POS(escapingSubject,Scene.X,Scene.Y,Scene.Z,-1,false);
                        GameFiber.Wait(1200);
                        if(escapingSubject.Exists())NativeFunction.Natives.TASK_STAY_IN_COVER(escapingSubject);
                    }
                }
                catch(System.Exception ex){Game.LogTrivial("AdvancedK9 Callouts: fugitive escape staging contained: "+ex.Message);}
            },"AdvancedK9 fugitive escape to cover");
            _lastSafeSubjectPosition=escapeStart;
            _nextCoverSearch=Game.GameTime+3000;
            Game.LogTrivial("AdvancedK9 Callouts: fugitive trail constrained to a local outdoor 65-95 metre pedestrian search; live concealment will be confirmed after streaming.");
            DispatchUpdate("CalloutAccepted","Failed traffic stop","Local patrol jurisdiction","Gray Primo stopped at curb with marked cruiser behind","Adult male in work clothes","Away from the driver side of the stopped vehicle","Unknown weapon status","Respond to a failed traffic stop. The driver abandoned the vehicle and fled on foot. Two patrol officers are holding the scene.","WE_HAVE CRIME_RESIST_ARREST IN_OR_ON_POSITION",Scene);
            RouteToScene("Respond to the failed traffic stop. The driver abandoned the stopped vehicle and fled on foot; officers preserved the driver-seat scent.");
            return base.OnCalloutAccepted();
        }

        private bool RejectCurrentScene(string reason)
        {
            if(_sceneIndex>=0)FailedRoadsideScenes.Add(_sceneIndex);
            Game.LogTrivial("AdvancedK9 Callouts: blacklisted FugitiveTrail scene "+_sceneIndex+" for this session: "+reason+".");
            return false;
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
            if(player.DistanceTo(Scene)<80f)ControlLiveTraffic(Scene,24f);
            if(_suspectLocated&&player.DistanceTo(Subject)<65f)ControlLiveTraffic(Subject.Position,22f);
            MaintainPoliceEmergencyLights();

            if(!_approachDispatchSent&&player.DistanceTo(Scene)<120f)
            {
                _approachDispatchSent=true;
                DispatchUpdate("ApproachingScene","Failed traffic stop","Local patrol jurisdiction","Gray Primo stopped at curb with marked cruiser behind","Adult male in work clothes","Away from the driver side","Unknown weapon status","On-scene units report an adult male in work clothes fled from the driver seat. Use the preserved vehicle scent and search away from the traffic stop.","UNITS_RESPOND_CODE_2",Scene);
            }

            if(!_sceneBriefed&&player.DistanceTo(Scene)<28f)
            {
                _sceneBriefed=true;_phase=FugitivePhase.AwaitingScent;_phaseStarted=Game.GameTime;
                StopBackupOfficerInvestigation();
                Game.DisplayNotification("~b~On-scene officer:~s~ The suspect fled on foot. I preserved their scent from the driver seat.~n~~y~Deploy Rex beside the abandoned vehicle and command TRACK.");
                if(OfficerOne!=null&&OfficerOne.Exists())NativeFunction.Natives.TASK_TURN_PED_TO_FACE_ENTITY(OfficerOne,player,5000);
                AssignCalloutScent(Subject,_hidingPosition,"preserved scent article from abandoned vehicle driver seat; retained for full accepted-callout lifecycle");
                DispatchUpdate("OfficerBriefing","Failed traffic stop","Local patrol jurisdiction","Gray Primo; driver-seat scent preserved","Adult male in work clothes","Away from the stopped vehicle","Unknown weapon status","Contact officer briefing: adult male in work clothes, last seen fleeing away from the stopped vehicle. The driver-seat scent article remains preserved.","OFFICERS_REPORT SUSPECT_LAST_SEEN",Scene);
            }
            if(!ApiRequested&&_sceneBriefed)
            {
                AssignCalloutScent(Subject,_hidingPosition,"preserved scent article from abandoned vehicle driver seat; retained for full accepted-callout lifecycle");
                if(ApiRequested){ClearSceneRoute();Game.LogTrivial("AdvancedK9 Callouts: fugitive vehicle scent source registered; awaiting handler command.");}
            }

            if(ApiRequested&&K9TrackingActive()&&_phase==FugitivePhase.AwaitingScent){_phase=FugitivePhase.Tracking;_phaseStarted=Game.GameTime;Game.LogTrivial("AdvancedK9 Callouts: FugitiveTrail phase -> Tracking.");}
            if(_phase==FugitivePhase.Tracking&&!_trackingDispatchSent)
            {
                _trackingDispatchSent=true;
                DispatchUpdate("ScentAcquired","Fugitive foot trail","Local patrol jurisdiction","Abandoned gray Primo","Adult male in work clothes","Following the K9-selected route","Unknown weapon status","K9 has the preserved driver-seat scent. Patrol officers are joining the handler in search formation.","OFFICERS_REPORT SUSPECT_LAST_SEEN",player.Position);
            }
            if(_phase==FugitivePhase.Tracking&&!_suspectLocated&&!_supportCommitted&&K9TrackingActive())
            {
                _supportCommitted=true;
                Game.LogTrivial("AdvancedK9 Callouts: support movement armed by handler departure from scent scene.");
            }
            if(_phase==FugitivePhase.Tracking&&_supportCommitted&&!_suspectLocated)SupportOfficersFollowK9();

            if(!_suspectLocated&&!_coverConfirmed&&!_activeEscapeFallback&&player.DistanceTo(_hidingPosition)<180f&&Game.GameTime>=_nextCoverSearch)
            {
                if(_coverSearchStarted==0)_coverSearchStarted=Game.GameTime;
                _nextCoverSearch=Game.GameTime+4000;
                Vector3 liveCover;
                Vector3 safeLiveCover;
                if(TryFindExistingCover(_hidingPosition,out liveCover)&&TryResolveSafePedPosition(liveCover,out safeLiveCover))
                {
                    _hidingPosition=safeLiveCover;_naturalCoverReserved=true;_coverConfirmed=false;
                    NativeFunction.Natives.TASK_FOLLOW_NAV_MESH_TO_COORD(Subject,safeLiveCover.X,safeLiveCover.Y,safeLiveCover.Z,4.8f,18000,1.5f,0,0f);
                    Game.LogTrivial("AdvancedK9 Callouts: streamed environmental cover confirmed and fugitive hiding task refreshed.");
                }
                else
                {
                    Vector3 safeFallback;
                    if(TryResolveSafePedPosition(_hidingPosition,out safeFallback)&&!NativeFunction.Natives.IS_POINT_ON_ROAD<bool>(safeFallback.X,safeFallback.Y,safeFallback.Z,0))
                    {
                        _hidingPosition=safeFallback;
                        NativeFunction.Natives.TASK_FOLLOW_NAV_MESH_TO_COORD(Subject,safeFallback.X,safeFallback.Y,safeFallback.Z,5.4f,14000,1.5f,0,0f);
                        Game.LogTrivial("AdvancedK9 Callouts: natural cover unavailable; fugitive is continuing toward an off-road search area without treating it as concealment.");
                    }
                    else if(Game.GameTime>=_nextCoverSeekTask)
                    {
                        _nextCoverSeekTask=Game.GameTime+5000;
                        NativeFunction.Natives.TASK_SEEK_COVER_FROM_POS(Subject,Scene.X,Scene.Y,Scene.Z,12000,false);
                        Game.LogTrivial("AdvancedK9 Callouts: fugitive has no valid concealment yet and will keep moving instead of standing in the roadway.");
                    }
                }
                if(!_coverConfirmed&&_coverSearchStarted!=0&&Game.GameTime-_coverSearchStarted>=25000)
                {
                    _activeEscapeFallback=true;
                    _fleeActive=true;_lastSafeSubjectPosition=Subject.Position;
                    NativeFunction.Natives.TASK_SMART_FLEE_PED(Subject,player,105f,45000,false,false);
                    Game.LogTrivial("AdvancedK9 Callouts: bounded cover search exhausted; suspect assigned a genuine active-flee task instead of a stationary exposed endpoint.");
                    DispatchUpdate("Updated last known: the fugitive did not remain hidden and may still be moving. Continue the K9 track from the preserved vehicle scent.","SUSPECT_LAST_SEEN",Subject.Position);
                }
            }

            if(!_suspectLocated&&!_coverConfirmed&&Subject.DistanceTo(_hidingPosition)<7f&&Game.GameTime>=_nextHideTask)
            {
                _nextHideTask=Game.GameTime+3500;
                bool offRoad=!NativeFunction.Natives.IS_POINT_ON_ROAD<bool>(Subject.Position.X,Subject.Position.Y,Subject.Position.Z,0);
                if(offRoad&&_naturalCoverReserved)
                {
                    _coverConfirmed=true;
                    if(!_isHidingAnimationPlaying)
                    {
                        _isHidingAnimationPlaying=true;
                        Subject.BlockPermanentEvents=true;
                        Subject.Tasks.ClearImmediately();
                        Subject.Tasks.PlayAnimation("amb@code_human_cower@male@base","base",1.0f,AnimationFlags.Loop);
                        NativeFunction.Natives.SET_PED_KEEP_TASK(Subject,true);
                        Game.LogTrivial("AdvancedK9 Callouts: fugitive reached verified physical cover; persistent cower stance locked until the K9 alert transition.");
                    }
                }
                else
                {
                    NativeFunction.Natives.TASK_SEEK_COVER_FROM_POS(Subject,Scene.X,Scene.Y,Scene.Z,12000,false);
                    Game.LogTrivial("AdvancedK9 Callouts: fugitive has not reached physical cover and continues seeking concealment.");
                }
            }

            float rexDistance=K9DistanceTo(Subject.Position);
            if(ApiRequested&&!_suspectLocated&&!_rexReachedSubject&&rexDistance<3f)
            {
                _rexReachedSubject=true;_rexReachedAt=Game.GameTime;
                Game.LogTrivial("AdvancedK9 Callouts: Rex physically reached FugitiveTrail subject; preserving the suspect's cover/flee task until the alert transition.");
            }
            if(_rexReachedSubject&&!_suspectLocated)
            {
                TransitionSuspectAwayFromHiding();
                _suspectLocated=true;_locatedAt=Game.GameTime;_phase=FugitivePhase.Located;_phaseStarted=Game.GameTime;
                SetCustodyObservationEnabled(true);
                EndSupportTracking();
                SubjectBlip=Subject.AttachBlip();SubjectBlip.IsRouteEnabled=true;Subject.IsInvincible=false;
                _verbalGraceUntil=Game.GameTime+(_activeEscapeFallback?8000u:45000u);
                ControlApprehensionTraffic(Subject.Position);
                SupportOfficersContainSubject();
                Game.DisplayNotification("~o~Rex alerted on the hidden fugitive.~s~ Tracking is complete. Give verbal commands through NPCI; the suspect may surrender, flee, or resist.");
                if(!_locatedDispatchSent){_locatedDispatchSent=true;DispatchUpdate("SuspectLocated","Fugitive foot trail","Local patrol jurisdiction","Abandoned gray Primo","Adult male in work clothes","Live K9 alert location","Unknown weapon status","K9 has located the fugitive. Units are establishing armed containment at the live location.","SUSPECT_LOCATED UNITS_RESPOND_CODE_3",Subject.Position);}
                Game.LogTrivial("AdvancedK9 Callouts: alert-first FugitiveTrail transition completed after Rex reached the stationary hidden subject.");
            }

            if(_suspectLocated)
            {
                ObserveCooperativeControl("verbal challenge");
                bool controlLocked=SuspectControlLocked;
                MaintainSupportContainment(CustodyLocked||SubjectIsInCustody());
                if(controlLocked)
                {
                    TransitionSuspectAwayFromHiding();
                    _fleeActive=false;_outcomeTaskIssued=true;
                    K9ApiSnapshot liveK9;
                    if(!_k9DisengageIssued&&AdvancedK9Api.TryGetSnapshot(out liveK9)&&(liveK9.State=="Apprehending"||liveK9.State=="HoldingSuspect"))
                    {
                        _k9DisengageIssued=true;
                        AdvancedK9Api.SendCommand("Release",ContextId,HandleOf(Subject),"Ped",Subject.Position.X,Subject.Position.Y,Subject.Position.Z,"callout compliance/custody safety release");
                        Game.LogTrivial("AdvancedK9 Callouts: compliance/custody detected; K9 release requested exactly once (release transitions the dog to medical standby).");
                    }
                    if(!_controlDispatchSent&&!ArrestProviderOwnsSubject)
                    {
                        _controlDispatchSent=true;
                        string action=Subject.Health<Subject.MaxHealth-5?"K9Apprehension":"SuspectSurrender";
                        string narrative=action=="K9Apprehension"?"The K9 has taken the fugitive to the ground. The suspect is injured; dispatching EMS to the live location.":"The fugitive is complying. Units are moving in to complete the arrest.";
                        DispatchUpdate(action,"Fugitive foot trail","Local patrol jurisdiction","Abandoned gray Primo","Adult male in work clothes","Live apprehension location","Controlled suspect",narrative,"",Subject.Position);
                    }
                }
                if(!_outcomeTaskIssued&&SubjectIsComplying())
                {
                    _outcomeTaskIssued=true;_fleeActive=false;
                    TransitionSuspectAwayFromHiding();
                    BeginBackupOfficerArrestAttempt();
                    Game.DisplayNotification("~g~Suspect is complying with verbal commands.~s~ Move in and complete the LSPDFR arrest.");
                }
                else if(!_outcomeTaskIssued&&!controlLocked&&Game.GameTime>=_verbalGraceUntil)
                {
                    _outcomeTaskIssued=true;
                    TransitionSuspectAwayFromHiding();
                    if(_outcome==0&&!ArrestProviderOwnsSubject)NativeFunction.Natives.TASK_HANDS_UP(Subject,120000,player,-1,true);
                    else
                    {
                        _fleeActive=true;_lastSafeSubjectPosition=Subject.Position;
                        NativeFunction.Natives.TASK_SMART_FLEE_PED(Subject,player,105f,45000,false,false);
                    }
                    Game.LogTrivial("AdvancedK9 Callouts: extended verbal challenge window expired; bounded local outcome resumed because NPCI/LSPDFR reported no compliance.");
                }
                if(_fleeActive&&!controlLocked&&!SubjectIsComplying()&&!Subject.IsDead)
                {
                    bool inWater=NativeFunction.Natives.IS_ENTITY_IN_WATER<bool>(Subject);
                    float escapeDistance=Subject.Position.DistanceTo(_hidingPosition);
                    if(!inWater&&escapeDistance<125f)_lastSafeSubjectPosition=Subject.Position;
                    if(inWater)
                    {
                        TransitionSuspectAwayFromHiding();
                        Subject.Position=_lastSafeSubjectPosition;
                        Subject.Tasks.ClearImmediately();
                        if(!ArrestProviderOwnsSubject)NativeFunction.Natives.TASK_HANDS_UP(Subject,120000,player,-1,true);
                        _fleeActive=false;_waterRecoveryUsed=true;
                        Game.DisplayNotification("~b~Containment:~s~ The fugitive was stopped at the water boundary and is surrendering.");
                        Game.LogTrivial("AdvancedK9 Callouts: unsafe water escape prevented; suspect restored to the last safe land position.");
                    }
                    else if(escapeDistance>=125f)
                    {
                        TransitionSuspectAwayFromHiding();
                        Subject.Tasks.Clear();
                        if(!ArrestProviderOwnsSubject)NativeFunction.Natives.TASK_HANDS_UP(Subject,120000,player,-1,true);
                        _fleeActive=false;
                        Game.DisplayNotification("~b~Containment:~s~ The fugitive reached the perimeter and is surrendering.");
                        Game.LogTrivial("AdvancedK9 Callouts: bounded fugitive perimeter reached; map-wide flight prevented.");
                    }
                    else if(Game.GameTime>=_nextPursuitTask)
                    {
                        _nextPursuitTask=Game.GameTime+2200;
                        uint taser=NativeFunction.Natives.GET_HASH_KEY<uint>("WEAPON_STUNGUN");
                        uint pistol=NativeFunction.Natives.GET_HASH_KEY<uint>("WEAPON_COMBATPISTOL");
                        if(OfficerOne!=null&&OfficerOne.Exists()&&OfficerOne.DistanceTo(Subject)>7f)
                        {
                            NativeFunction.Natives.GIVE_WEAPON_TO_PED(OfficerOne,taser,2,false,true);
                            NativeFunction.Natives.SET_CURRENT_PED_WEAPON(OfficerOne,taser,true);
                            NativeFunction.Natives.TASK_GO_TO_ENTITY(OfficerOne,Subject,-1,9f,6.2f,0f,0);
                            NativeFunction.Natives.SET_PED_KEEP_TASK(OfficerOne,true);
                        }
                        if(OfficerTwo!=null&&OfficerTwo.Exists()&&OfficerTwo.DistanceTo(Subject)>10f)
                        {
                            NativeFunction.Natives.GIVE_WEAPON_TO_PED(OfficerTwo,pistol,60,false,true);
                            NativeFunction.Natives.SET_CURRENT_PED_WEAPON(OfficerTwo,pistol,true);
                            NativeFunction.Natives.TASK_GO_TO_ENTITY(OfficerTwo,Subject,-1,12f,6.0f,0f,0);
                            NativeFunction.Natives.SET_PED_KEEP_TASK(OfficerTwo,true);
                        }
                    }
                }

                bool rawDead=Subject.Health<=0&&NativeFunction.Natives.IS_PED_DEAD_OR_DYING<bool>(Subject,true);
                if(rawDead&&_deathCandidateSince==0)
                {
                    _deathCandidateSince=Game.GameTime;
                    Game.LogTrivial("AdvancedK9 Callouts: transient death candidate observed; waiting for LSPDFR/PR custody ownership to stabilize.");
                }
                else if(!rawDead&&_deathCandidateSince!=0)
                {
                    Game.LogTrivial("AdvancedK9 Callouts: transient death candidate cleared during custody handoff; suspect remains in the arrest workflow.");
                    _deathCandidateSince=0;
                }
                bool confirmedDead=ConfirmedSubjectDeath();
                bool custodyLease=!confirmedDead&&UpdateCustodyLease();
                bool injured=!confirmedDead&&Subject.Health<Subject.MaxHealth-5;
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
                else if(custodyLease)
                {
                    _fleeActive=false;_outcomeTaskIssued=true;
                    if(!_custodyConfirmed&&CustodyOwnerStable)
                    {
                        _custodyConfirmed=true;_phase=FugitivePhase.Custody;_phaseStarted=Game.GameTime;
                        Game.DisplayNotification("~b~Custody confirmed:~s~ "+CustodyOwner+" owns the suspect. AdvancedK9 has suspended escape and suspect tasking.");
                        DispatchUpdate("CustodyConfirmed","Fugitive foot trail","Local patrol jurisdiction","Prisoner transport required","Adult male in custody","Live arrest location","Medically stable or treatment pending",CustodyOwner+" has custody. Maintain the scene until medical clearance and prisoner transport are confirmed.","",Subject.Position);
                        Game.LogTrivial("AdvancedK9 Callouts: FugitiveTrail phase -> Custody; owner="+CustodyOwner+" and all AdvancedK9 suspect task/health writes are blocked.");
                        if(!_manualTransportNoticeSent)
                        {
                            _manualTransportNoticeSent=true;
                            Game.DisplayNotification("~b~Custody confirmed:~s~ Request prisoner transport manually when you are ready to clear the scene.");
                            Game.LogTrivial("AdvancedK9 Callouts: automatic prisoner transport is disabled; awaiting player/provider transport request.");
                        }
                    }
                    if(_custodyConfirmed&&ProviderTransportLoaded()&&Game.GameTime-_phaseStarted>3000)
                    {
                        _phase=FugitivePhase.Complete;Resolve("~g~Fugitive Trail complete: external-provider custody and prisoner transport confirmed.");
                    }
                }
                else if(Game.GameTime-_locatedAt>600000)Resolve("~o~Fugitive Trail concluded after suspect location.");
            }
            else if(!ApiRequested&&Game.GameTime-StartedAt>900000)Resolve("~r~Fugitive Trail: response expired before scent collection.");
            base.Process();
        }

        private void TransitionSuspectAwayFromHiding()
        {
            if(!_isHidingAnimationPlaying)return;
            _isHidingAnimationPlaying=false;
            if(Subject==null||!Subject.Exists())return;
            NativeFunction.Natives.SET_PED_KEEP_TASK(Subject,false);
            Subject.Tasks.ClearImmediately();
            Game.LogTrivial("AdvancedK9 Callouts: suspect task persistence released and hiding animation cleared safely.");
        }

        private void BeginBackupOfficerArrestAttempt()
        {
            if(_backupArrestAttempted||ArrestProviderOwnsSubject||SubjectIsInCustody()||OfficerOne==null||!OfficerOne.Exists())return;
            _backupArrestAttempted=true;
            var arrestOfficer=OfficerOne;var suspect=Subject;
            GameFiber.StartNew(delegate
            {
                try
                {
                    if(arrestOfficer==null||!arrestOfficer.Exists()||suspect==null||!suspect.Exists())return;
                    arrestOfficer.BlockPermanentEvents=true;
                    uint taser=NativeFunction.Natives.GET_HASH_KEY<uint>("WEAPON_STUNGUN");
                    NativeFunction.Natives.SET_CURRENT_PED_WEAPON(arrestOfficer,taser,true);
                    NativeFunction.Natives.TASK_GO_TO_ENTITY(arrestOfficer,suspect,-1,2.2f,2.8f,0f,0);
                    uint approachDeadline=Game.GameTime+10000;
                    while(!Finished&&arrestOfficer.Exists()&&suspect.Exists()&&arrestOfficer.DistanceTo(suspect)>2.8f&&Game.GameTime<approachDeadline)GameFiber.Wait(200);
                    if(!Finished&&arrestOfficer.Exists()&&suspect.Exists()&&!ArrestProviderOwnsSubject&&!SubjectIsInCustody())
                    {
                        NativeFunction.Natives.TASK_ARREST_PED(arrestOfficer,suspect);
                        NativeFunction.Natives.SET_PED_KEEP_TASK(arrestOfficer,true);
                        HandSuspectToLspdfr(suspect);
                        Game.LogTrivial("AdvancedK9 Callouts: less-lethal backup officer began the arrest attempt after confirmed suspect compliance.");
                    }
                }
                catch(System.Exception ex){Game.LogTrivial("AdvancedK9 Callouts: backup arrest attempt contained: "+ex.Message);}
            },"AdvancedK9 backup arrest attempt");
        }

        private void HandSuspectToLspdfr(Ped target)
        {
            if(target==null||!target.Exists())return;
            TransitionSuspectAwayFromHiding();
            _fleeActive=false;_outcomeTaskIssued=true;
            try
            {
                var method=typeof(Functions).GetMethod("SetPedAsArrested",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static,null,new[]{typeof(Ped),typeof(bool)},null);
                if(method==null)
                {
                    Game.LogTrivial("AdvancedK9 Callouts: LSPDFR SetPedAsArrested(Ped, bool) was unavailable; native arrest attempt remains active without forcing an unsupported ownership call.");
                    return;
                }
                method.Invoke(null,new object[]{target,true});
                Game.LogTrivial("AdvancedK9 Callouts: suspect handed to the verified LSPDFR arrest surface; AdvancedK9 suspect task ownership suspended.");
            }
            catch(System.Exception ex){Game.LogTrivial("AdvancedK9 Callouts: LSPDFR custody handoff contained: "+ex.Message);}
        }

        public override void End()
        {
            TransitionSuspectAwayFromHiding();
            base.End();
        }
    }
}
