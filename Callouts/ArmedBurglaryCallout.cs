using LSPD_First_Response.Mod.API;
using LSPD_First_Response.Mod.Callouts;
using Rage;
using Rage.Native;
using System.Collections.Generic;

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
        private int _bankScenario;
        private bool _bankScenarioStarted;
        private Vehicle _getawayVehicle;
        private Ped _hostage;
        private bool _bankCustodyNotice;
        private bool _hostageReleased;
        private bool _establishScenePublished;
        private uint _bankWeaponHash;
        private readonly List<Ped> _bankAccomplices=new List<Ped>();
        private LHandle _bankPursuit;
        private bool _bankPursuitStarted;
        private bool _bailoutTrackReady;
        private bool _bankOutcomeReported;
        private uint _scenarioStartedAt;

        private static readonly Vector3[] BusinessScenes={
            new Vector3(235.11f,216.83f,106.29f),       // Pacific Standard
            new Vector3(149.74f,-1040.42f,29.37f),      // Legion Square Fleeca
            new Vector3(313.41f,-279.15f,54.17f),       // Hawick Fleeca
            new Vector3(-1212.98f,-330.84f,37.79f),     // Rockford Hills Fleeca
            new Vector3(-2962.58f,482.63f,15.70f),      // Great Ocean Highway Fleeca
            new Vector3(1175.05f,2706.40f,38.09f),      // Route 68 Fleeca
            new Vector3(-111.24f,6469.31f,31.63f)       // Blaine County Savings
        };
        private static readonly string[] BankNames={
            "Pacific Standard Bank",
            "Legion Square Fleeca Bank",
            "Hawick Fleeca Bank",
            "Rockford Hills Fleeca Bank",
            "Great Ocean Highway Fleeca Bank",
            "Route 68 Fleeca Bank",
            "Blaine County Savings Bank"
        };
        private static readonly Vector3[] CruiserScenes={
            new Vector3(225.45f,210.10f,105.55f),new Vector3(160f,-1035f,29f),
            new Vector3(324f,-283f,54f),new Vector3(-1200f,-343f,37f),
            new Vector3(-2951f,489f,15f),new Vector3(1164f,2698f,38f),
            new Vector3(-103.5f,6458.5f,31.5f)
        };
        private static readonly float[] CruiserHeadings={250f,340f,250f,25f,85f,180f,45f};

        private bool PrepareBusinessScene()
        {
            var player=Game.LocalPlayer.Character;_sceneIndex=-1;float best=float.MaxValue;
            for(int i=0;i<BusinessScenes.Length;i++)
            {
                float distance=player.DistanceTo(BusinessScenes[i]);
                if(distance>180f&&distance<best){best=distance;_sceneIndex=i;}
            }
            if(_sceneIndex<0)return false;
            if(_sceneIndex>=BankNames.Length||_sceneIndex>=CruiserScenes.Length||_sceneIndex>=CruiserHeadings.Length)return false;
            Game.LogTrivial("AdvancedK9 Callouts: selected verified bank scene "+BankNames[_sceneIndex]+" at "+BusinessScenes[_sceneIndex]+"; bank interiors are valid and no longer rejected as exterior businesses.");
            return Prepare("Bank robbery at "+BankNames[_sceneIndex]+" — K9 requested",BusinessScenes[_sceneIndex],70f,false);
        }

        private Vector3 SecondaryCruiserPosition()
        {
            float heading=CruiserHeadings[_sceneIndex];
            float radians=(float)(heading*System.Math.PI/180.0);
            Vector3 forward=new Vector3((float)System.Math.Sin(radians),(float)System.Math.Cos(radians),0f);
            return CruiserScenes[_sceneIndex]-(forward*9.0f);
        }

        private bool StageBankPerimeter()
        {
            if(!StagePoliceScene(CruiserScenes[_sceneIndex],CruiserHeadings[_sceneIndex],true))return false;
            if(!StageDedicatedSceneSecurity(SecondaryCruiserPosition(),CruiserHeadings[_sceneIndex]))return false;
            MaintainSpawnedPoliceAssets();
            Game.LogTrivial("AdvancedK9 Callouts: realistic bank perimeter staged at "+BankNames[_sceneIndex]+" with two marked cruisers, two contact/cover officers, and one dedicated scene-security officer.");
            return true;
        }

        private bool AcceptAndRouteToSelectedBank(string instruction)
        {
            if(!base.OnCalloutAccepted())return false;
            RouteToScene("Respond to "+BankNames[_sceneIndex]+". "+instruction);
            if(SceneBlip!=null&&SceneBlip.Exists())SceneBlip.Name=BankNames[_sceneIndex]+" — Bank Robbery";
            Game.DisplayHelp("GPS route set to ~y~"+BankNames[_sceneIndex]+"~s~.");
            Game.LogTrivial("AdvancedK9 Callouts: accepted bank response routed to "+BankNames[_sceneIndex]+" at "+Scene+" after base acceptance completed.");
            return true;
        }

        private void ConfigureArmedBankRobber(bool equipNow)
        {
            if(Subject==null||!Subject.Exists())return;
            string[] weapons={"WEAPON_PISTOL","WEAPON_COMBATPISTOL","WEAPON_MICROSMG"};
            _bankWeaponHash=NativeFunction.Natives.GET_HASH_KEY<uint>(weapons[Random.Next(weapons.Length)]);
            NativeFunction.Natives.GIVE_WEAPON_TO_PED(Subject,_bankWeaponHash,Random.Next(42,91),false,equipNow);
            NativeFunction.Natives.SET_PED_DROPS_WEAPONS_WHEN_DEAD(Subject,true);
            NativeFunction.Natives.SET_PED_COMBAT_ABILITY(Subject,1);
            NativeFunction.Natives.SET_PED_COMBAT_RANGE(Subject,1);
            NativeFunction.Natives.SET_PED_COMBAT_MOVEMENT(Subject,2);
            NativeFunction.Natives.SET_PED_COMBAT_ATTRIBUTES(Subject,5,true);
            NativeFunction.Natives.SET_PED_COMBAT_ATTRIBUTES(Subject,46,true);
            if(equipNow)NativeFunction.Natives.SET_CURRENT_PED_WEAPON(Subject,_bankWeaponHash,true);
            Game.LogTrivial("AdvancedK9 Callouts: bank robber armed with hash "+_bankWeaponHash+", visibleNow="+equipNow+".");
        }

        private void ConfigureArmedAccomplice(Ped robber,bool equipNow)
        {
            if(robber==null||!robber.Exists())return;
            uint weapon=NativeFunction.Natives.GET_HASH_KEY<uint>(Random.Next(3)==0?"WEAPON_MICROSMG":"WEAPON_COMBATPISTOL");
            NativeFunction.Natives.GIVE_WEAPON_TO_PED(robber,weapon,Random.Next(36,76),false,equipNow);
            NativeFunction.Natives.SET_PED_DROPS_WEAPONS_WHEN_DEAD(robber,true);
            NativeFunction.Natives.SET_PED_COMBAT_ABILITY(robber,1);
            NativeFunction.Natives.SET_PED_COMBAT_RANGE(robber,1);
            NativeFunction.Natives.SET_PED_COMBAT_MOVEMENT(robber,2);
            robber.BlockPermanentEvents=true;robber.IsPersistent=true;
            if(equipNow)NativeFunction.Natives.SET_CURRENT_PED_WEAPON(robber,weapon,true);
            _bankAccomplices.Add(robber);
        }

        private void StageBankManager()
        {
            if(Reporter==null||!Reporter.Exists())return;
            Reporter.IsPersistent=true;Reporter.BlockPermanentEvents=true;
            Vector3 managerPosition=OfficerOne!=null&&OfficerOne.Exists()?OfficerOne.GetOffsetPosition(new Vector3(1.4f,-0.8f,0f)):new Vector3(Scene.X+2f,Scene.Y,Scene.Z);
            Reporter.Position=managerPosition;
            Reporter.Heading=OfficerOne!=null&&OfficerOne.Exists()?OfficerOne.Heading:0f;
            NativeFunction.Natives.TASK_STAND_STILL(Reporter,-1);
            Game.LogTrivial("AdvancedK9 Callouts: bank manager secured beside the contact officer and retained for the full scene.");
        }

        private void PublishBankOutcome(string stage,string summary)
        {
            if(_bankOutcomeReported)return;
            _bankOutcomeReported=true;
            DispatchUpdate(stage,_bankScenario==1?"Bank robbery vehicle escape":_bankScenario==2?"Bank hostage incident":_bankScenario==3?"Pacific Standard interior search":"Bank robbery foot escape","Local patrol jurisdiction",_bankScenario==1?"Dark Buffalo getaway vehicle":"No active getaway vehicle","Armed bank-robbery suspects","Incident scene",ConfirmedSubjectDeath()?"Armed suspect deceased":"Scene stable",BankNames[_sceneIndex]+": "+summary,"",Subject!=null&&Subject.Exists()?Subject.Position:Scene);
            Game.DisplayNotification("~b~Dispatch:~s~ "+summary);
            Game.DisplayHelp("The bank scene remains active. Clear the callout manually after evidence, medical, and prisoner arrangements are complete.");
            Game.LogTrivial("AdvancedK9 Callouts: bank outcome published without dismissing scene personnel: "+summary);
        }

        private bool AllBankRobbersResolved()
        {
            if(Subject!=null&&Subject.Exists()&&!Subject.IsDead&&!SubjectIsInCustody())return false;
            for(int i=0;i<_bankAccomplices.Count;i++)
            {
                Ped robber=_bankAccomplices[i];
                if(robber!=null&&robber.Exists()&&!robber.IsDead)
                {
                    bool arrested=false;
                    try{arrested=NativeFunction.Natives.IS_PED_CUFFED<bool>(robber)||NativeFunction.Natives.IS_PED_HANDCUFFED<bool>(robber);}catch{}
                    if(!arrested)return false;
                }
            }
            return true;
        }

        public override bool OnBeforeCalloutDisplayed()
        {
            try{if(!PrepareBusinessScene())return false;}
            catch(System.Exception ex)
            {
                Game.LogTrivial("AdvancedK9 Callouts: "+GetType().Name+" primary scene calculation failed; using safe fallback: "+ex);
                return false;
            }
            if(!StageBankPerimeter())return false;
            ControlSceneTraffic();
            Game.LogTrivial("AdvancedK9 Callouts: validated exterior business staging selected; roadway and overpass starts are excluded.");
            return true;
        }

        public override bool OnCalloutAccepted()
        {
            StartedAt=Game.GameTime;_outcome=Random.Next(5);
            _bankScenario=Random.Next(3); // foot trail, vehicle escape, or hostage containment
            if(_sceneIndex==0&&Random.Next(4)==0)_bankScenario=3; // Pacific Standard interior search only
            Reporter=SpawnPed("a_m_y_business_03",new Vector3(Scene.X+2f,Scene.Y,Scene.Z),0f);
            if(!StageBankPerimeter())return false;
            StageBankManager();
            if(_bankScenario==1)return InitializeVehicleEscapeScenario();
            if(_bankScenario==2||_bankScenario==3)return InitializeBankContainmentScenario(_bankScenario==3);
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
            ConfigureArmedBankRobber(false);
            if(Random.Next(100)<45)
            {
                Ped accomplice=SpawnPed("g_m_y_mexgoon_01",_hidePosition+new Vector3(1.5f,0.8f,0f),Random.Next(360));
                ConfigureArmedAccomplice(accomplice,false);
                if(accomplice!=null&&accomplice.Exists())NativeFunction.Natives.TASK_COWER(accomplice,-1);
            }
            NativeFunction.Natives.TASK_COWER(Subject,-1);
            if(!AcceptAndRouteToSelectedBank("Officers recovered clothing torn from the fleeing suspect."))return false;
            DispatchUpdate("CalloutAccepted","Bank robbery foot escape","Local patrol jurisdiction","No confirmed getaway vehicle","Armed bank-robbery suspect","Fled on foot from the bank","Possibly armed","Respond to a reported bank robbery. The manager remains at the bank and officers preserved a discarded clothing scent article from a suspect who fled on foot.","WE_HAVE CRIME_BURGLARY IN_OR_ON_POSITION UNITS_RESPOND_CODE_3",Scene);
            return true;
        }

        private bool InitializeVehicleEscapeScenario()
        {
            float radians=(float)(CruiserHeadings[_sceneIndex]*System.Math.PI/180.0);
            Vector3 roadSeed=Scene+new Vector3((float)System.Math.Sin(radians)*24f,(float)System.Math.Cos(radians)*24f,0f);
            Vector3 road=World.GetNextPositionOnStreet(roadSeed);
            _getawayVehicle=SpawnVehicle("buffalo",road,CruiserHeadings[_sceneIndex]);
            Subject=SpawnPed("g_m_y_mexgoon_02",road,CruiserHeadings[_sceneIndex]);
            if(_getawayVehicle==null||!_getawayVehicle.Exists()||Subject==null||!Subject.Exists())return false;
            Subject.MaxHealth=250;Subject.Health=250;
            ConfigureArmedBankRobber(false);
            NativeFunction.Natives.SET_PED_INTO_VEHICLE(Subject,_getawayVehicle,-1);
            int accompliceCount=Random.Next(1,3);
            for(int i=0;i<accompliceCount;i++)
            {
                Ped accomplice=SpawnPed(i==0?"g_m_y_mexgoon_01":"g_m_y_salvagoon_01",road,CruiserHeadings[_sceneIndex]);
                ConfigureArmedAccomplice(accomplice,false);
                if(accomplice!=null&&accomplice.Exists())NativeFunction.Natives.SET_PED_INTO_VEHICLE(accomplice,_getawayVehicle,i);
            }
            if(!AcceptAndRouteToSelectedBank("The manager is safe with officers; armed suspects fled in a vehicle moments before arrival."))return false;
            DispatchUpdate("CalloutAccepted","Bank robbery vehicle escape","Local patrol jurisdiction","Dark Buffalo fleeing the bank","Armed bank-robbery suspect","Leaving the bank district by vehicle","Confirmed armed","Bank robbery in progress. The manager remains with officers at the bank while an armed suspect escapes in a dark Buffalo. Prepare for a vehicle pursuit and possible K9 bailout track.","WE_HAVE CRIME_BURGLARY IN_OR_ON_POSITION UNITS_RESPOND_CODE_3",Scene);
            return true;
        }

        private bool InitializeBankContainmentScenario(bool pacificInterior)
        {
            Vector3 suspectPosition=pacificInterior?new Vector3(253.55f,221.45f,101.68f):new Vector3(Scene.X+5f,Scene.Y+2f,Scene.Z);
            Subject=SpawnPed("g_m_y_mexgoon_02",suspectPosition,180f);
            _hostage=SpawnPed("a_f_y_business_02",new Vector3(suspectPosition.X+1.2f,suspectPosition.Y,suspectPosition.Z),180f);
            if(Subject==null||!Subject.Exists()||_hostage==null||!_hostage.Exists())return false;
            Subject.MaxHealth=300;Subject.Health=300;
            ConfigureArmedBankRobber(true);
            NativeFunction.Natives.TASK_HANDS_UP(_hostage,-1,Subject,-1,true);
            NativeFunction.Natives.TASK_AIM_GUN_AT_ENTITY(Subject,_hostage,-1,false);
            if(Random.Next(100)<55)
            {
                Ped accomplice=SpawnPed("g_m_y_mexgoon_01",new Vector3(suspectPosition.X-2.2f,suspectPosition.Y+1.2f,suspectPosition.Z),180f);
                ConfigureArmedAccomplice(accomplice,true);
                if(accomplice!=null&&accomplice.Exists())NativeFunction.Natives.TASK_GUARD_CURRENT_POSITION(accomplice,12f,12f,true);
            }
            if(!AcceptAndRouteToSelectedBank(pacificInterior?"An armed suspect is believed to be hiding inside the bank.":"An armed suspect is holding an employee; K9 deployment is optional."))return false;
            DispatchUpdate("CalloutAccepted",pacificInterior?"Pacific Standard interior suspect":"Bank hostage containment","Local patrol jurisdiction","No getaway vehicle located",pacificInterior?"Armed suspect hidden inside Pacific Standard":"Armed suspect with bank employee","Contained at the bank","Confirmed armed",pacificInterior?"Pacific Standard reports an armed robbery suspect still hidden inside. Establish a perimeter and use the K9 only when tactically appropriate.":"Armed bank robbery with an employee being held at the bank. Establish containment; K9 deployment is optional and must not endanger the hostage.","WE_HAVE CRIME_BURGLARY IN_OR_ON_POSITION UNITS_RESPOND_CODE_3",Scene);
            return true;
        }

        private string CurrentBankBriefing()
        {
            if(_bankScenario==1)return "The manager is secure with patrol. An armed suspect fled in a dark Buffalo. The vehicle was last seen leaving the bank district; prepare for a vehicle pursuit and possible K9 bailout track.";
            if(_bankScenario==2)return "An armed robbery suspect is holding a bank employee near the perimeter. Patrol has containment. Keep Rex staged unless the hostage gains separation.";
            if(_bankScenario==3)return "An armed suspect remains hidden inside Pacific Standard. Patrol has the exits covered. Rex may be used for the interior search when the entry team is ready.";
            return "The manager is secure with patrol. An armed suspect fled on foot. Officers preserved a torn shirt as the scent article; two officers can accompany the K9 team while the third maintains bank security.";
        }

        private void UpdateEstablishSceneBriefing(Ped player)
        {
            if(_establishScenePublished||OfficerOne==null||!OfficerOne.Exists()||player.DistanceTo(OfficerOne)>16f)return;
            _establishScenePublished=true;
            string briefing=CurrentBankBriefing();
            DispatchUpdate("EstablishScene",_bankScenario==1?"Bank robbery vehicle escape":_bankScenario==2?"Bank hostage containment":_bankScenario==3?"Pacific Standard interior suspect":"Bank robbery foot escape","Local patrol jurisdiction",_bankScenario==1?"Dark Buffalo getaway vehicle":"No active getaway vehicle",_bankScenario==2?"Armed suspect with bank employee":"Armed bank-robbery suspect",_bankScenario==1?"Leaving the bank district":_bankScenario==0?"Fled on foot from the bank":"Contained at the bank","Confirmed armed",BankNames[_sceneIndex]+" contact-officer briefing: "+briefing,"",Scene);
            Game.DisplayNotification("~b~Contact Officer — "+BankNames[_sceneIndex]+":~s~~n~"+briefing);
            Game.DisplayHelp("Face the contact officer and use ~b~NPCI Emergency PTT~s~. Say ~y~establish scene~s~ for the spoken briefing. The same incident dossier has been published to CalloutInterface/Nexus.");
            Game.LogTrivial("AdvancedK9 Callouts: EstablishScene dossier published for NPCI/Nexus officer interaction at "+BankNames[_sceneIndex]+".");
        }

        public override void Process()
        {
            ObserveSuspectLifecycle();
            if(Finished||Subject==null||!Subject.Exists()){if(!Finished)Resolve("~r~Armed Burglary ended: suspect unavailable.");return;}
            var player=Game.LocalPlayer.Character;
            MaintainSpawnedPoliceAssets();
            MaintainPoliceEmergencyLights();
            UpdateEstablishSceneBriefing(player);
            if(_bankScenario==1){ProcessVehicleEscape(player);base.Process();return;}
            if(_bankScenario==2||_bankScenario==3){ProcessBankContainment(player);base.Process();return;}
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
                if(_bankWeaponHash!=0)NativeFunction.Natives.SET_CURRENT_PED_WEAPON(Subject,_bankWeaponHash,true);
                for(int i=0;i<_bankAccomplices.Count;i++)
                {
                    Ped robber=_bankAccomplices[i];
                    if(robber!=null&&robber.Exists()&&!robber.IsDead)NativeFunction.Natives.TASK_COMBAT_PED(robber,player,0,16);
                }
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
                    if(_outcome<=1&&!ArrestProviderOwnsSubject)NativeFunction.Natives.TASK_HANDS_UP(Subject,120000,player,-1,true);
                    else if(_outcome<=3)NativeFunction.Natives.TASK_SMART_FLEE_PED(Subject,player,700f,-1,false,false);
                    else NativeFunction.Natives.TASK_COMBAT_PED(Subject,player,0,16);
                    Game.LogTrivial("AdvancedK9 Callouts: armed verbal challenge expired; scripted outcome resumed because no NPCI/LSPDFR compliance was detected.");
                }
                if(Game.GameTime>=_nextContainmentUpdate&&!NativeFunction.Natives.IS_PED_CUFFED<bool>(Subject)){_nextContainmentUpdate=Game.GameTime+1800;SupportOfficersContainSubject();}
                bool injured=Subject.Health<Subject.MaxHealth-5;
                if((injured||MedicalResponseStarted)&&!MedicalResponseComplete){ProcessPostApprehensionMedical("");}
                else if(ConfirmedSubjectDeath())PublishBankOutcome("SuspectDeceased","Primary suspect is deceased. Hold the bank perimeter for investigation and coroner response.");
                else if(SeriousMedicalTransport&&MedicalResponseComplete)PublishBankOutcome("MedicalTransport","EMS assumed hospital transport under police custody. Bank personnel remain on scene for investigation.");
                else if(custodyLease&&CustodyOwnerStable&&(!MedicalResponseStarted||MedicalResponseComplete)&&!_transportStarted)
                {
                    if(_custodyReadyAt==0)_custodyReadyAt=Game.GameTime;
                    if(Game.GameTime-_custodyReadyAt>=1500)
                    {
                        _transportStarted=RequestCustodyOwnerTransport();
                        Game.DisplayNotification("~b~Dispatch:~s~ "+CustodyOwner+" custody is stable and medically cleared. The owning provider has been asked for transport.");
                    }
                }
                else if(_transportStarted&&ProviderTransportLoaded())PublishBankOutcome("PrisonerTransport","Provider custody and prisoner transport are confirmed. Complete the bank-scene investigation before clearing.");
                else if(AllBankRobbersResolved())PublishBankOutcome("AllSuspectsResolved","All known bank-robbery suspects are secured or otherwise resolved.");
                else if(Game.GameTime-_locatedAt>300000)PublishBankOutcome("ExtendedScene","The armed-suspect scene remains active after an extended containment period.");
            }
            else if(Game.GameTime-StartedAt>420000)PublishBankOutcome("SuspectEscaped","The foot-trail suspect escaped the containment area. Preserve the bank scene and evidence for follow-up.");
            base.Process();
        }

        private void ProcessVehicleEscape(Ped player)
        {
            MaintainPoliceEmergencyLights();
            if(!_bankScenarioStarted&&player.DistanceTo(Scene)<85f)
            {
                _bankScenarioStarted=true;_scenarioStartedAt=Game.GameTime;
                Subject.BlockPermanentEvents=true;
                NativeFunction.Natives.SET_PED_KEEP_TASK(Subject,true);
                NativeFunction.Natives.TASK_VEHICLE_DRIVE_WANDER(Subject,_getawayVehicle,28f,786603);
                try
                {
                    _bankPursuit=Functions.CreatePursuit();
                    Functions.AddPedToPursuit(_bankPursuit,Subject);
                    for(int i=0;i<_bankAccomplices.Count;i++)if(_bankAccomplices[i]!=null&&_bankAccomplices[i].Exists())Functions.AddPedToPursuit(_bankPursuit,_bankAccomplices[i]);
                    Functions.SetPursuitIsActiveForPlayer(_bankPursuit,true);
                    _bankPursuitStarted=true;
                }
                catch(System.Exception ex){Game.LogTrivial("AdvancedK9 Callouts: LSPDFR bank pursuit setup contained; native flight remains active: "+ex.Message);}
                Game.DisplayNotification("~r~Bank robbery vehicle located.~s~ Join the pursuit. If the suspect bails out, Rex can transition to the recorded foot trail.");
                Game.LogTrivial("AdvancedK9 Callouts: bank vehicle-escape scenario started native high-speed flight; manager and scene officers remained at the bank.");
            }
            bool driverOnFoot=_bankScenarioStarted&&_getawayVehicle!=null&&_getawayVehicle.Exists()&&!Subject.IsInVehicle(_getawayVehicle,false);
            if(driverOnFoot&&!_bailoutTrackReady&&Subject.DistanceTo(_getawayVehicle)>6f&&!SubjectIsInCustody())
            {
                _bailoutTrackReady=true;
                try{if(_bankPursuitStarted&&Functions.IsPursuitStillRunning(_bankPursuit))Functions.ForceEndPursuit(_bankPursuit);}catch(System.Exception ex){Game.LogTrivial("AdvancedK9 Callouts: pursuit-to-track handoff contained: "+ex.Message);}
                EvidenceProp=SpawnProp("prop_cs_shoe_01",_getawayVehicle.Position);
                Vector3 article=EvidenceProp!=null&&EvidenceProp.Exists()?EvidenceProp.Position:_getawayVehicle.Position;
                AssignCalloutScent(Subject,article,"shoe recovered beside the abandoned bank getaway vehicle","The driver bailed out and left a shoe beside the getaway vehicle. Bring Rex to it and command COLLECT SCENT or TRACK.");
                Game.DisplayNotification("~b~Dispatch:~s~ Bank robber bailed out. The abandoned getaway vehicle now contains a viable K9 scent article.");
                DispatchUpdate("BailoutTrackReady","Bank robbery bailout track","Local patrol jurisdiction","Abandoned dark Buffalo","Armed bank-robbery suspects","Last seen fleeing from the vehicle","Confirmed armed","The getaway driver bailed out. A shoe beside the abandoned vehicle has been preserved for Rex.","",_getawayVehicle.Position);
            }
            if(_bailoutTrackReady&&!_suspectLocated&&K9TrackingActive())SupportOfficersFollowK9();
            if(_bailoutTrackReady&&!_suspectLocated&&System.Math.Min(K9DistanceTo(Subject.Position),player.DistanceTo(Subject))<18f)
            {
                _suspectLocated=true;EndSupportTracking();
                if(_bankWeaponHash!=0)NativeFunction.Natives.SET_CURRENT_PED_WEAPON(Subject,_bankWeaponHash,true);
                SupportOfficersContainSubject();
                Game.DisplayNotification("~r~Rex located the armed getaway driver.~s~ Establish containment and issue commands.");
            }
            if(SubjectIsInCustody()&&!_bankCustodyNotice){_bankCustodyNotice=true;PublishBankOutcome("SuspectInCustody","The primary getaway suspect is in custody. Confirm every accomplice before clearing the bank scene.");}
            else if(AllBankRobbersResolved())PublishBankOutcome("AllSuspectsResolved","All getaway suspects are secured or otherwise resolved. The bank scene remains held for investigation.");
            else if(_bankScenarioStarted&&Game.GameTime-_scenarioStartedAt>480000)PublishBankOutcome("SuspectsEscaped","The getaway suspects escaped after an extended pursuit. Preserve the originating bank scene for investigation.");
        }

        private void ProcessBankContainment(Ped player)
        {
            MaintainPoliceEmergencyLights();
            if(!_bankScenarioStarted&&player.DistanceTo(Scene)<45f)
            {
                _bankScenarioStarted=true;_scenarioStartedAt=Game.GameTime;
                SubjectBlip=Subject.AttachBlip();SubjectBlip.IsRouteEnabled=true;
                if(_bankScenario==3)Game.DisplayNotification("~o~Pacific Standard interior search.~s~ The armed suspect remains inside; K9 use is optional.");
                else Game.DisplayNotification("~r~Bank hostage containment.~s~ Maintain a safe angle. K9 deployment is optional while the employee remains exposed.");
            }
            if(_bankScenarioStarted&&!_outcomeTaskIssued&&player.DistanceTo(Subject)<22f)
            {
                _outcomeTaskIssued=true;
                if(_outcome<=1)
                {
                    if(_hostage!=null&&_hostage.Exists()){_hostage.Tasks.ClearImmediately();NativeFunction.Natives.TASK_SMART_FLEE_PED(_hostage,Subject,40f,8000,false,false);_hostageReleased=true;}
                    Subject.Tasks.ClearImmediately();NativeFunction.Natives.TASK_HANDS_UP(Subject,120000,player,-1,true);
                    for(int i=0;i<_bankAccomplices.Count;i++)if(_bankAccomplices[i]!=null&&_bankAccomplices[i].Exists())NativeFunction.Natives.TASK_HANDS_UP(_bankAccomplices[i],120000,player,-1,true);
                    Game.DisplayNotification("~g~The robber released the employee and is complying.~s~ Move in for the arrest.");
                }
                else
                {
                    if(_hostage!=null&&_hostage.Exists()){_hostage.Tasks.ClearImmediately();NativeFunction.Natives.TASK_SMART_FLEE_PED(_hostage,Subject,60f,10000,false,false);_hostageReleased=true;}
                    Subject.Tasks.ClearImmediately();NativeFunction.Natives.TASK_COMBAT_PED(Subject,player,0,16);
                    for(int i=0;i<_bankAccomplices.Count;i++)if(_bankAccomplices[i]!=null&&_bankAccomplices[i].Exists())NativeFunction.Natives.TASK_COMBAT_PED(_bankAccomplices[i],player,0,16);
                    Game.DisplayNotification("~r~The hostage has broken away. Armed robbers are engaging officers.~s~");
                }
            }
            if(SubjectIsInCustody()&&!_hostageReleased&&_hostage!=null&&_hostage.Exists())
            {
                _hostageReleased=true;
                _hostage.Tasks.ClearImmediately();
                NativeFunction.Natives.TASK_COWER(_hostage,5000);
                Game.DisplayNotification("~g~The bank employee is secure.~s~ Clear the callout manually after completing the scene investigation.");
            }
            if(AllBankRobbersResolved())PublishBankOutcome("AllSuspectsResolved",_hostageReleased?"All armed suspects are resolved and the bank employee is safe.":"All armed suspects are resolved. Officers are securing the bank employee.");
            else if(ConfirmedSubjectDeath())PublishBankOutcome("SuspectDeceased","The primary bank robber is deceased. Officers are checking the employee and searching for remaining accomplices.");
            else if(_bankScenarioStarted&&Game.GameTime-_scenarioStartedAt>480000)PublishBankOutcome("ExtendedContainment","The bank remains under extended armed containment. Scene units will remain until manually cleared.");
        }

        public override void End()
        {
            try{if(_bankPursuitStarted&&Functions.IsPursuitStillRunning(_bankPursuit))Functions.ForceEndPursuit(_bankPursuit);}catch{}
            for(int i=0;i<_bankAccomplices.Count;i++)if(_bankAccomplices[i]!=null&&_bankAccomplices[i].Exists())_bankAccomplices[i].Dismiss();
            _bankAccomplices.Clear();
            if(_hostage!=null&&_hostage.Exists())_hostage.Dismiss();
            if(_getawayVehicle!=null&&_getawayVehicle.Exists())_getawayVehicle.Dismiss();
            base.End();
        }
    }
}
