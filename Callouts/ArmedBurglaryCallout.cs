using LSPD_First_Response;
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
        private int _bankScenario;
        private bool _bankScenarioStarted;
        private Vehicle _getawayVehicle;
        private Ped _hostage;
        private LHandle _bankPursuit;
        private bool _bankCustodyNotice;
        private bool _hostageReleased;

        private static readonly Vector3[] BusinessScenes={
            new Vector3(235.11f,216.83f,106.29f),       // Pacific Standard
            new Vector3(149.74f,-1040.42f,29.37f),      // Legion Square Fleeca
            new Vector3(313.41f,-279.15f,54.17f),       // Hawick Fleeca
            new Vector3(-1212.98f,-330.84f,37.79f),     // Rockford Hills Fleeca
            new Vector3(-2962.58f,482.63f,15.70f),      // Great Ocean Highway Fleeca
            new Vector3(1175.05f,2706.40f,38.09f),      // Route 68 Fleeca
            new Vector3(-111.24f,6469.31f,31.63f)       // Blaine County Savings
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
            _bankScenario=Random.Next(3); // foot trail, vehicle escape, or hostage containment
            if(_sceneIndex==0&&Random.Next(4)==0)_bankScenario=3; // Pacific Standard interior search only
            Reporter=SpawnPed("a_m_y_business_03",new Vector3(Scene.X+2f,Scene.Y,Scene.Z),0f);
            if(!StagePoliceScene(CruiserScenes[_sceneIndex],CruiserHeadings[_sceneIndex]))return false;
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
            NativeFunction.Natives.GIVE_WEAPON_TO_PED(Subject,NativeFunction.Natives.GET_HASH_KEY<uint>("WEAPON_PISTOL"),36,false,false);
            NativeFunction.Natives.TASK_COWER(Subject,-1);
            RouteToScene("Respond to the burglary scene. Officers recovered clothing torn from the fleeing suspect.");
            DispatchUpdate("CalloutAccepted","Bank robbery foot escape","Local patrol jurisdiction","No confirmed getaway vehicle","Armed bank-robbery suspect","Fled on foot from the bank","Possibly armed","Respond to a reported bank robbery. The manager remains at the bank and officers preserved a discarded clothing scent article from a suspect who fled on foot.","WE_HAVE CRIME_BURGLARY IN_OR_ON_POSITION UNITS_RESPOND_CODE_3",Scene);
            return base.OnCalloutAccepted();
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
            NativeFunction.Natives.GIVE_WEAPON_TO_PED(Subject,NativeFunction.Natives.GET_HASH_KEY<uint>("WEAPON_PISTOL"),36,false,false);
            NativeFunction.Natives.SET_PED_INTO_VEHICLE(Subject,_getawayVehicle,-1);
            RouteToScene("Respond to the bank. The manager is safe with officers; armed suspects fled in a vehicle moments before arrival.");
            DispatchUpdate("CalloutAccepted","Bank robbery vehicle escape","Local patrol jurisdiction","Dark Buffalo fleeing the bank","Armed bank-robbery suspect","Leaving the bank district by vehicle","Confirmed armed","Bank robbery in progress. The manager remains with officers at the bank while an armed suspect escapes in a dark Buffalo. Prepare for a vehicle pursuit and possible K9 bailout track.","WE_HAVE CRIME_BURGLARY IN_OR_ON_POSITION UNITS_RESPOND_CODE_3",Scene);
            return base.OnCalloutAccepted();
        }

        private bool InitializeBankContainmentScenario(bool pacificInterior)
        {
            Vector3 suspectPosition=pacificInterior?new Vector3(253.55f,221.45f,101.68f):new Vector3(Scene.X+5f,Scene.Y+2f,Scene.Z);
            Subject=SpawnPed("g_m_y_mexgoon_02",suspectPosition,180f);
            _hostage=SpawnPed("a_f_y_business_02",new Vector3(suspectPosition.X+1.2f,suspectPosition.Y,suspectPosition.Z),180f);
            if(Subject==null||!Subject.Exists()||_hostage==null||!_hostage.Exists())return false;
            Subject.MaxHealth=300;Subject.Health=300;
            NativeFunction.Natives.GIVE_WEAPON_TO_PED(Subject,NativeFunction.Natives.GET_HASH_KEY<uint>("WEAPON_PISTOL"),48,false,true);
            NativeFunction.Natives.TASK_HANDS_UP(_hostage,-1,Subject,-1,true);
            RouteToScene(pacificInterior?"Respond to Pacific Standard. An armed suspect is believed to be hiding inside the bank.":"Respond to the bank perimeter. An armed suspect is holding an employee; K9 deployment is optional.");
            DispatchUpdate("CalloutAccepted",pacificInterior?"Pacific Standard interior suspect":"Bank hostage containment","Local patrol jurisdiction","No getaway vehicle located",pacificInterior?"Armed suspect hidden inside Pacific Standard":"Armed suspect with bank employee","Contained at the bank","Confirmed armed",pacificInterior?"Pacific Standard reports an armed robbery suspect still hidden inside. Establish a perimeter and use the K9 only when tactically appropriate.":"Armed bank robbery with an employee being held at the bank. Establish containment; K9 deployment is optional and must not endanger the hostage.","WE_HAVE CRIME_BURGLARY IN_OR_ON_POSITION UNITS_RESPOND_CODE_3",Scene);
            return base.OnCalloutAccepted();
        }

        public override void Process()
        {
            ObserveSuspectLifecycle();
            if(Finished||Subject==null||!Subject.Exists()){if(!Finished)Resolve("~r~Armed Burglary ended: suspect unavailable.");return;}
            var player=Game.LocalPlayer.Character;
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
                else if(ConfirmedSubjectDeath())Resolve("~o~Armed Burglary concluded: suspect is deceased.");
                else if(SeriousMedicalTransport&&MedicalResponseComplete)Resolve("~g~Armed Burglary complete: EMS assumed hospital transport under police custody.");
                else if(custodyLease&&CustodyOwnerStable&&(!MedicalResponseStarted||MedicalResponseComplete)&&!_transportStarted)
                {
                    if(_custodyReadyAt==0)_custodyReadyAt=Game.GameTime;
                    if(Game.GameTime-_custodyReadyAt>=1500)
                    {
                        _transportStarted=RequestCustodyOwnerTransport();
                        Game.DisplayNotification("~b~Dispatch:~s~ "+CustodyOwner+" custody is stable and medically cleared. The owning provider has been asked for transport.");
                    }
                }
                else if(_transportStarted&&ProviderTransportLoaded())Resolve("~g~Armed Burglary complete: provider custody and prisoner transport confirmed.");
                else if(Game.GameTime-_locatedAt>300000)Resolve("~o~Armed Burglary concluded after suspect location.");
            }
            else if(Game.GameTime-StartedAt>420000)Resolve("~r~Armed Burglary: suspect escaped the containment area.");
            base.Process();
        }

        private void ProcessVehicleEscape(Ped player)
        {
            MaintainPoliceEmergencyLights();
            if(!_bankScenarioStarted&&player.DistanceTo(Scene)<85f)
            {
                _bankScenarioStarted=true;
                _bankPursuit=Functions.CreatePursuit();
                Functions.AddPedToPursuit(_bankPursuit,Subject);
                Functions.SetPursuitIsActiveForPlayer(_bankPursuit,true);
                Game.DisplayNotification("~r~Bank robbery vehicle located.~s~ Join the pursuit. If the suspect bails out, Rex can transition to the recorded foot trail.");
                Game.LogTrivial("AdvancedK9 Callouts: bank vehicle-escape scenario entered an LSPDFR pursuit; manager and scene officers remained at the bank.");
            }
            if(SubjectIsInCustody()&&!_bankCustodyNotice){_bankCustodyNotice=true;Game.DisplayHelp("Suspect secured. Clear the bank callout manually when the scene is complete.");}
        }

        private void ProcessBankContainment(Ped player)
        {
            MaintainPoliceEmergencyLights();
            if(!_bankScenarioStarted&&player.DistanceTo(Scene)<45f)
            {
                _bankScenarioStarted=true;
                SubjectBlip=Subject.AttachBlip();SubjectBlip.IsRouteEnabled=true;
                if(_bankScenario==3)Game.DisplayNotification("~o~Pacific Standard interior search.~s~ The armed suspect remains inside; K9 use is optional.");
                else Game.DisplayNotification("~r~Bank hostage containment.~s~ Maintain a safe angle. K9 deployment is optional while the employee remains exposed.");
            }
            if(_bankScenarioStarted&&!_outcomeTaskIssued&&player.DistanceTo(Subject)<22f)
            {
                _outcomeTaskIssued=true;
                if(_outcome<=1)NativeFunction.Natives.TASK_HANDS_UP(Subject,120000,player,-1,true);
                else NativeFunction.Natives.TASK_COMBAT_PED(Subject,player,0,16);
            }
            if(SubjectIsInCustody()&&!_hostageReleased&&_hostage!=null&&_hostage.Exists())
            {
                _hostageReleased=true;
                _hostage.Tasks.ClearImmediately();
                NativeFunction.Natives.TASK_COWER(_hostage,5000);
                Game.DisplayNotification("~g~The bank employee is secure.~s~ Clear the callout manually after completing the scene investigation.");
            }
        }

        public override void End()
        {
            if(_hostage!=null&&_hostage.Exists())_hostage.Dismiss();
            if(_getawayVehicle!=null&&_getawayVehicle.Exists())_getawayVehicle.Dismiss();
            base.End();
        }
    }
}
