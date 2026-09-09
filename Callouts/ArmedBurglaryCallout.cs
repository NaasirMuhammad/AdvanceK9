using LSPD_First_Response.Mod.API;
using LSPD_First_Response.Mod.Callouts;
using Rage;
using Rage.Native;

namespace AdvancedK9.Callouts
{
    [CalloutInfo("AdvancedK9: Armed Burglary Suspect Hiding",CalloutProbability.Medium)]
    public sealed class ArmedBurglaryCallout : AdvancedK9Callout
    {
        private int _outcome;
        private bool _sceneBriefed;
        private bool _suspectLocated;
        private bool _transportStarted;
        private uint _custodyReadyAt;
        private uint _locatedAt;
        private uint _nextContainmentUpdate;
        private Vector3 _hidePosition;
        private int _sceneIndex;
        private uint _verbalGraceUntil;
        private bool _outcomeTaskIssued;

        private static readonly Vector3[] BusinessScenes={
            new Vector3(28f,-1345f,29f),new Vector3(-48f,-1758f,29f),new Vector3(-1220f,-915f,11f),
            new Vector3(1160f,-326f,69f),new Vector3(149f,-1040f,29f),new Vector3(-1212f,-336f,37f),
            new Vector3(313f,-278f,54f),new Vector3(1175f,2706f,38f),new Vector3(1961f,3740f,32f),
            new Vector3(1730f,6414f,35f),new Vector3(-2962f,482f,15f)
        };
        private static readonly Vector3[] CruiserScenes={
            new Vector3(40f,-1352f,29f),new Vector3(-57f,-1767f,29f),new Vector3(-1208f,-923f,11f),
            new Vector3(1150f,-337f,69f),new Vector3(160f,-1035f,29f),new Vector3(-1200f,-343f,37f),
            new Vector3(324f,-283f,54f),new Vector3(1164f,2698f,38f),new Vector3(1950f,3736f,32f),
            new Vector3(1720f,6405f,35f),new Vector3(-2951f,489f,15f)
        };
        private static readonly float[] CruiserHeadings={270f,50f,35f,100f,340f,25f,250f,180f,300f,155f,85f};

        private bool PrepareBusinessScene()
        {
            var player=Game.LocalPlayer.Character;_sceneIndex=-1;float best=float.MaxValue;
            for(int i=0;i<BusinessScenes.Length;i++)
            {
                float distance=player.DistanceTo(BusinessScenes[i]);
                if(distance>180f&&distance<best){best=distance;_sceneIndex=i;}
            }
            if(_sceneIndex<0)return false;
            int interior=NativeFunction.Natives.GET_INTERIOR_AT_COORDS<int>(BusinessScenes[_sceneIndex].X,BusinessScenes[_sceneIndex].Y,BusinessScenes[_sceneIndex].Z);
            if(interior!=0)return false;
            return Prepare("Armed burglary suspect hiding — K9 requested",BusinessScenes[_sceneIndex],70f,false);
        }

        public override bool OnBeforeCalloutDisplayed()
        {
            try{if(!PrepareBusinessScene())return false;}
            catch(System.Exception ex)
            {
                Game.LogTrivial("AdvancedK9 Callouts: "+GetType().Name+" primary scene calculation failed; using safe fallback: "+ex);
                return false;
            }
            if(!StagePoliceScene(CruiserScenes[_sceneIndex],CruiserHeadings[_sceneIndex]))return false;
            ControlSceneTraffic();
            Game.LogTrivial("AdvancedK9 Callouts: validated exterior business staging selected; roadway and overpass starts are excluded.");
            return true;
        }

        public override bool OnCalloutAccepted()
        {
            StartedAt=Game.GameTime;_outcome=Random.Next(5);
            Reporter=SpawnPed("a_m_y_business_03",new Vector3(Scene.X+2f,Scene.Y,Scene.Z),0f);
            if(!StagePoliceScene(CruiserScenes[_sceneIndex],CruiserHeadings[_sceneIndex]))return false;
            EvidenceProp=SpawnProp("prop_ld_shirt_01",new Vector3(Scene.X-1.5f,Scene.Y+1f,Scene.Z));
            float angle=Random.Next(360);float dx=1f,dy=0f,length=1f;
            bool found=false;Vector3 coverPosition=Scene;float[] offsets={10f,-10f,14f,-14f,18f,-18f};
            for(int routeAttempt=0;routeAttempt<10&&!found;routeAttempt++)
            {
                float distance=Random.Next(65,111);float radians=(float)(angle*System.Math.PI/180.0);
                Vector3 streetTarget=World.GetNextPositionOnStreet(Scene+new Vector3((float)System.Math.Sin(radians)*distance,(float)System.Math.Cos(radians)*distance,0f));
                dx=streetTarget.X-Scene.X;dy=streetTarget.Y-Scene.Y;length=(float)System.Math.Sqrt(dx*dx+dy*dy);if(length<.1f){angle=(angle+41f)%360f;continue;}
                if(System.Math.Abs(streetTarget.Z-Scene.Z)>4f){angle=(angle+41f)%360f;continue;}
                for(int i=0;i<offsets.Length;i++)
                {
                    Vector3 candidate=new Vector3(streetTarget.X-dy/length*offsets[i],streetTarget.Y+dx/length*offsets[i],streetTarget.Z);
                    if(NativeFunction.Natives.GET_INTERIOR_AT_COORDS<int>(candidate.X,candidate.Y,candidate.Z)==0&&!NativeFunction.Natives.IS_POINT_ON_ROAD<bool>(candidate.X,candidate.Y,candidate.Z,0)){coverPosition=candidate;found=true;break;}
                }
                angle=(angle+41f)%360f;
            }
            if(!found){Game.LogTrivial("AdvancedK9 Callouts: Armed Burglary rejected because no safe off-road suspect cover was available.");return false;}
            if(!TryFindExistingCover(coverPosition,out _hidePosition))
            {
                Game.LogTrivial("AdvancedK9 Callouts: Armed Burglary rejected because the trail end had no existing environmental concealment.");
                return false;
            }
            Subject=SpawnPed("g_m_y_mexgoon_02",_hidePosition,Random.Next(360));if(Subject==null)return false;
            Subject.MaxHealth=250;Subject.Health=250;
            NativeFunction.Natives.GIVE_WEAPON_TO_PED(Subject,NativeFunction.Natives.GET_HASH_KEY<uint>("WEAPON_PISTOL"),36,false,false);
            NativeFunction.Natives.TASK_COWER(Subject,-1);
            Functions.PlayScannerAudioUsingPosition("WE_HAVE CRIME_BURGLARY IN_OR_ON_POSITION UNITS_RESPOND_CODE_3",Scene);
            RouteToScene("Respond to the burglary scene. Officers recovered clothing torn from the fleeing suspect.");
            Game.DisplayNotification("~b~Dispatch:~s~ Armed burglary reported at a business. Suspect fled on foot and may still be armed. On-scene officers have preserved a clothing scent article for Rex.");
            return base.OnCalloutAccepted();
        }

        public override void Process()
        {
            if(Finished||Subject==null||!Subject.Exists()){if(!Finished)Resolve("~r~Armed Burglary ended: suspect unavailable.");return;}
            var player=Game.LocalPlayer.Character;
            if(player.DistanceTo(Scene)<80f)ControlLiveTraffic(Scene,24f);
            if(_suspectLocated&&player.DistanceTo(Subject)<65f)ControlLiveTraffic(Subject.Position,22f);
            if(!_sceneBriefed&&player.DistanceTo(Scene)<35f)
            {
                _sceneBriefed=true;
                Vector3 article=EvidenceProp!=null&&EvidenceProp.Exists()?EvidenceProp.Position:Scene;
                AssignCalloutScent(Subject,article,"torn shirt recovered during armed burglary","A visible piece of the suspect's torn shirt is on the ground. Bring Rex to it and command COLLECT SCENT or TRACK.");
                if(ApiRequested){ClearSceneRoute();Game.LogTrivial("AdvancedK9 Callouts: Armed Burglary clothing scent source registered; patrol search bypassed.");}
                Game.DisplayNotification("~b~Dispatch:~s~ K9 team is beginning the armed-suspect track. Two patrol officers are assigned as tactical cover.");
            }

            if(ApiRequested&&!_suspectLocated&&K9TrackingActive())SupportOfficersFollowK9();
            if(ApiRequested&&!_suspectLocated&&System.Math.Min(K9DistanceTo(Subject.Position),player.DistanceTo(Subject))<18f)
            {
                _suspectLocated=true;_locatedAt=Game.GameTime;
                EndSupportTracking();
                SubjectBlip=Subject.AttachBlip();SubjectBlip.IsRouteEnabled=true;
                _verbalGraceUntil=Game.GameTime+12000;
                ControlApprehensionTraffic(Subject.Position);
                SupportOfficersContainSubject();
                Game.DisplayNotification("~b~Dispatch:~s~ K9 has located the armed burglary suspect. Patrol units are establishing lethal and less-lethal containment.");
                Game.DisplayNotification("~o~Rex located the armed suspect behind cover.~s~ Give verbal commands through NPCI. Officers will contain while the suspect decides whether to comply.");
            }

            if(_suspectLocated)
            {
                ObserveCooperativeControl("armed verbal challenge");
                bool controlLocked=SuspectControlLocked;
                bool custodyLease=UpdateCustodyLease();
                MaintainSupportContainment(custodyLease||SubjectIsInCustody());
                if(controlLocked)_outcomeTaskIssued=true;
                if(!_outcomeTaskIssued&&SubjectIsComplying())
                {
                    _outcomeTaskIssued=true;
                    Game.DisplayNotification("~g~Suspect is complying with verbal commands.~s~ Complete the LSPDFR arrest; Rex remains available.");
                }
                else if(!_outcomeTaskIssued&&!controlLocked&&Game.GameTime>=_verbalGraceUntil)
                {
                    _outcomeTaskIssued=true;
                    if(_outcome<=1)NativeFunction.Natives.TASK_HANDS_UP(Subject,120000,player,-1,true);
                    else if(_outcome<=3)NativeFunction.Natives.TASK_SMART_FLEE_PED(Subject,player,700f,-1,false,false);
                    else NativeFunction.Natives.TASK_COMBAT_PED(Subject,player,0,16);
                    Game.LogTrivial("AdvancedK9 Callouts: armed verbal challenge expired; scripted outcome resumed because no NPCI/LSPDFR compliance was detected.");
                }
                if(Game.GameTime>=_nextContainmentUpdate&&!NativeFunction.Natives.IS_PED_CUFFED<bool>(Subject)){_nextContainmentUpdate=Game.GameTime+1800;SupportOfficersContainSubject();}
                bool injured=Subject.Health<Subject.MaxHealth-5;
                if((injured||MedicalResponseStarted)&&!MedicalResponseComplete){ProcessPostApprehensionMedical("");}
                else if(ConfirmedSubjectDeath())Resolve("~o~Armed Burglary concluded: suspect is deceased.");
                else if(SeriousMedicalTransport&&MedicalResponseComplete)Resolve("~g~Armed Burglary complete: EMS assumed hospital transport under police custody.");
                else if(custodyLease&&CustodyOwnerStable&&(!MedicalResponseStarted||MedicalResponseComplete)&&!_transportStarted)
                {
                    if(_custodyReadyAt==0)_custodyReadyAt=Game.GameTime;
                    if(Game.GameTime-_custodyReadyAt>=10000)
                    {
                        _transportStarted=true;
                        Game.DisplayNotification("~b~Dispatch:~s~ "+CustodyOwner+" custody is stable and medically cleared. On-scene patrol is beginning prisoner transport.");
                        BeginAutomaticTransport("~g~Armed Burglary complete: EMS treatment and prisoner transport completed.");
                    }
                }
                else if(Game.GameTime-_locatedAt>300000)Resolve("~o~Armed Burglary concluded after suspect location.");
            }
            else if(Game.GameTime-StartedAt>420000)Resolve("~r~Armed Burglary: suspect escaped the containment area.");
            base.Process();
        }
    }
}
