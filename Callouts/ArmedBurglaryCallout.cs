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
        // Keep the LSPDFR pursuit token opaque. LHandle's runtime representation is
        // owned by the installed LSPDFR build and must never be re-declared in our
        // compile-time shim. Reflection safely carries the boxed token between API
        // calls without baking an incompatible LHandle TypeRef into this assembly.
        private object _bankPursuit;
        private bool _bankPursuitStarted;
        private bool _bailoutTrackReady;
        private bool _bankOutcomeReported;
        private uint _scenarioStartedAt;
        private readonly List<Vehicle> _bankPerimeterVehicles=new List<Vehicle>();
        private readonly List<Ped> _bankPerimeterOfficers=new List<Ped>();
        private readonly List<Vehicle> _bankGetawayVehicles=new List<Vehicle>();
        private Vehicle _swatBearcat;
        private bool _negotiationStarted;
        private int _negotiationRound;
        private uint _nextNegotiationAt;
        private bool _negotiationKeyHeld;
        private bool _bankPerimeterStaged;
        private bool _managerIsHostage;
        private bool _bankEntryTeamActive;
        private bool _bankEntryAuthorized;
        private bool _bankEntryArrestsAssigned;
        private uint _nextBankTacticalRefresh;
        private readonly List<Ped> _bankEntryTeam=new List<Ped>();
        protected override bool SharedOfficerArrestReady{get{return !_bankEntryTeamActive||_bankEntryAuthorized;}}

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
        private static readonly Vector3[] InteriorRobberPositions={
            new Vector3(253.55f,221.45f,101.68f),new Vector3(146.25f,-1044.05f,29.37f),
            new Vector3(310.35f,-283.85f,54.17f),new Vector3(-1209.65f,-335.35f,37.79f),
            new Vector3(-2957.25f,481.15f,15.70f),new Vector3(1171.85f,2711.30f,38.09f),
            new Vector3(-107.75f,6474.35f,31.63f)
        };

        private static System.Reflection.MethodInfo FindLspdfrFunction(string name,int parameterCount)
        {
            var methods=typeof(Functions).GetMethods(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static);
            for(int i=0;i<methods.Length;i++)if(methods[i].Name==name&&methods[i].GetParameters().Length==parameterCount)return methods[i];
            return null;
        }

        private bool TryStartBankPursuit()
        {
            var create=FindLspdfrFunction("CreatePursuit",0);
            var add=FindLspdfrFunction("AddPedToPursuit",2);
            var activate=FindLspdfrFunction("SetPursuitIsActiveForPlayer",2);
            if(create==null||add==null||activate==null)throw new System.MissingMethodException("Installed LSPDFR pursuit API is incomplete.");
            object pursuit=create.Invoke(null,null);
            if(pursuit==null)throw new System.InvalidOperationException("LSPDFR returned no pursuit handle.");
            add.Invoke(null,new object[]{pursuit,Subject});
            for(int i=0;i<_bankAccomplices.Count;i++)if(_bankAccomplices[i]!=null&&_bankAccomplices[i].Exists())add.Invoke(null,new object[]{pursuit,_bankAccomplices[i]});
            activate.Invoke(null,new object[]{pursuit,true});
            _bankPursuit=pursuit;
            _bankPursuitStarted=true;
            return true;
        }

        private bool BankPursuitIsRunning()
        {
            if(!_bankPursuitStarted||_bankPursuit==null)return false;
            var method=FindLspdfrFunction("IsPursuitStillRunning",1);
            return method!=null&&(bool)method.Invoke(null,new object[]{_bankPursuit});
        }

        private void EndBankPursuit()
        {
            if(!_bankPursuitStarted||_bankPursuit==null)return;
            var method=FindLspdfrFunction("ForceEndPursuit",1);
            if(method!=null)method.Invoke(null,new object[]{_bankPursuit});
            _bankPursuitStarted=false;
            _bankPursuit=null;
        }

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

        private static Vector3 HeadingVector(float heading)
        {
            float radians=(float)(heading*System.Math.PI/180.0);
            return new Vector3((float)System.Math.Sin(radians),(float)System.Math.Cos(radians),0f);
        }

        private Vehicle SpawnBankPoliceVehicle(Vector3 position,float heading)
        {
            string[] models={"police3","police","sheriff"};
            for(int i=0;i<models.Length;i++)
            {
                Vehicle vehicle=SpawnVehicle(models[i],position,heading,true);
                if(vehicle==null||!vehicle.Exists())continue;
                vehicle.IsPersistent=true;
                NativeFunction.Natives.SET_VEHICLE_HAS_MUTED_SIRENS(vehicle,true);
                NativeFunction.Natives.SET_VEHICLE_SIREN(vehicle,true);
                NativeFunction.Natives.SET_VEHICLE_LIGHTS(vehicle,2);
                _bankPerimeterVehicles.Add(vehicle);
                return vehicle;
            }
            return null;
        }

        private Ped SpawnBankOfficer(Vector3 position,float heading,bool swat)
        {
            Ped officer=SpawnPed(swat?"s_m_y_swat_01":"s_m_y_cop_01",position,heading);
            if(officer==null||!officer.Exists())return null;
            uint weapon=NativeFunction.Natives.GET_HASH_KEY<uint>(swat?"WEAPON_CARBINERIFLE":"WEAPON_COMBATPISTOL");
            NativeFunction.Natives.GIVE_WEAPON_TO_PED(officer,weapon,swat?180:60,false,true);
            NativeFunction.Natives.SET_CURRENT_PED_WEAPON(officer,weapon,true);
            NativeFunction.Natives.SET_PED_USING_ACTION_MODE(officer,true);
            NativeFunction.Natives.SET_PED_COMBAT_ATTRIBUTES(officer,1,true);
            NativeFunction.Natives.SET_PED_COMBAT_ABILITY(officer,2);
            NativeFunction.Natives.TASK_STAND_GUARD(officer,position.X,position.Y,position.Z,heading,"WORLD_HUMAN_GUARD_STAND");
            _bankPerimeterOfficers.Add(officer);
            return officer;
        }

        private Vector3 BankCoverPosition(Vehicle vehicle,float lateral)
        {
            Vector3 car=vehicle.Position;float dx=car.X-Scene.X,dy=car.Y-Scene.Y;
            float length=(float)System.Math.Sqrt(dx*dx+dy*dy);if(length<.1f){dx=0f;dy=-1f;length=1f;}dx/=length;dy/=length;
            return new Vector3(car.X+dx*2.15f-dy*lateral,car.Y+dy*2.15f+dx*lateral,car.Z);
        }

        private List<Vehicle> AllBankPoliceVehicles()
        {
            var vehicles=new List<Vehicle>();
            if(PoliceVehicle!=null&&PoliceVehicle.Exists())vehicles.Add(PoliceVehicle);
            if(PoliceVehicleTwo!=null&&PoliceVehicleTwo.Exists())vehicles.Add(PoliceVehicleTwo);
            for(int i=0;i<_bankPerimeterVehicles.Count;i++)if(_bankPerimeterVehicles[i]!=null&&_bankPerimeterVehicles[i].Exists())vehicles.Add(_bankPerimeterVehicles[i]);
            return vehicles;
        }

        private List<Ped> AllBankOfficers()
        {
            var officers=new List<Ped>();
            if(OfficerOne!=null&&OfficerOne.Exists())officers.Add(OfficerOne);
            if(OfficerTwo!=null&&OfficerTwo.Exists())officers.Add(OfficerTwo);
            if(OfficerThree!=null&&OfficerThree.Exists())officers.Add(OfficerThree);
            for(int i=0;i<_bankPerimeterOfficers.Count;i++)if(_bankPerimeterOfficers[i]!=null&&_bankPerimeterOfficers[i].Exists())officers.Add(_bankPerimeterOfficers[i]);
            return officers;
        }

        private bool StageBankPerimeter()
        {
            if(_bankPerimeterStaged)return true;
            if(!StagePoliceScene(CruiserScenes[_sceneIndex],CruiserHeadings[_sceneIndex],true))return false;
            Vector3 forward=HeadingVector(CruiserHeadings[_sceneIndex]);
            Vector3 right=new Vector3(forward.Y,-forward.X,0f);
            int requiredVehicles=_sceneIndex==0?6:4;
            int requiredOfficers=_sceneIndex==0?12:8;
            Vector3 anchor=CruiserScenes[_sceneIndex];float heading=CruiserHeadings[_sceneIndex];
            Vector3[] positions={anchor+right*3.2f,anchor-right*3.2f,anchor-forward*13f+right*3.2f,anchor-forward*13f-right*3.2f};
            float[] headings={heading+90f,heading+90f,heading,heading};
            PoliceVehicle.Position=positions[0];PoliceVehicle.Heading=headings[0];NativeFunction.Natives.SET_VEHICLE_ON_GROUND_PROPERLY(PoliceVehicle);
            PoliceVehicleTwo.Position=positions[1];PoliceVehicleTwo.Heading=headings[1];NativeFunction.Natives.SET_VEHICLE_ON_GROUND_PROPERLY(PoliceVehicleTwo);
            for(int i=2;i<requiredVehicles;i++)
            {
                Vector3 position=i<4?positions[i]:anchor-forward*(23f+(i-4)*8f)+right*(i%2==0?6f:-6f);
                float vehicleHeading=i<4?headings[i]:heading+90f;
                if(SpawnBankPoliceVehicle(position,vehicleHeading)==null)return false;
            }
            List<Vehicle> perimeterVehicles=AllBankPoliceVehicles();
            var baseOfficers=new[]{OfficerOne,OfficerTwo,OfficerThree};
            for(int i=0;i<baseOfficers.Length;i++)if(baseOfficers[i]!=null&&baseOfficers[i].Exists())
            {
                Vehicle cover=perimeterVehicles[i/2];Vector3 position=BankCoverPosition(cover,i%2==0?-0.75f:0.75f);
                baseOfficers[i].Position=position;baseOfficers[i].Heading=heading;
                uint weapon=NativeFunction.Natives.GET_HASH_KEY<uint>(i==0?"WEAPON_STUNGUN":"WEAPON_COMBATPISTOL");
                NativeFunction.Natives.GIVE_WEAPON_TO_PED(baseOfficers[i],weapon,60,false,true);NativeFunction.Natives.SET_CURRENT_PED_WEAPON(baseOfficers[i],weapon,true);
                NativeFunction.Natives.TASK_STAND_GUARD(baseOfficers[i],position.X,position.Y,position.Z,heading,"WORLD_HUMAN_GUARD_STAND");
            }
            for(int i=3;i<requiredOfficers;i++)
            {
                bool swat=_sceneIndex==0&&i>=8;
                Vehicle cover=perimeterVehicles[(i/2)%perimeterVehicles.Count];Vector3 position=BankCoverPosition(cover,i%2==0?-0.75f:0.75f);
                if(SpawnBankOfficer(position,heading,swat)==null)return false;
            }
            if(_sceneIndex==0)
            {
                _swatBearcat=SpawnVehicle("riot",CruiserScenes[_sceneIndex]-forward*25f+right*11f,CruiserHeadings[_sceneIndex],true);
                if(_swatBearcat==null||!_swatBearcat.Exists())_swatBearcat=SpawnVehicle("fbi2",CruiserScenes[_sceneIndex]-forward*25f+right*11f,CruiserHeadings[_sceneIndex],true);
                if(_swatBearcat==null||!_swatBearcat.Exists())return false;
                _swatBearcat.IsPersistent=true;NativeFunction.Natives.SET_VEHICLE_LIGHTS(_swatBearcat,2);
            }
            MaintainSpawnedPoliceAssets();
            _bankPerimeterStaged=true;
            Game.LogTrivial("AdvancedK9 Callouts: bank perimeter staged at "+BankNames[_sceneIndex]+" with "+requiredVehicles+" marked cruisers in two perpendicular lane-blocking pairs and "+requiredOfficers+" armed officers behind vehicle cover"+(_sceneIndex==0?", including SWAT and a BearCat.":"."));
            return true;
        }

        private bool StageGetawayVehicles(int suspectCount)
        {
            Vector3 forward=HeadingVector(CruiserHeadings[_sceneIndex]);
            Vector3 right=new Vector3(forward.Y,-forward.X,0f);
            string[] models={"buffalo","schafter2","granger"};
            for(int i=0;i<suspectCount;i++)
            {
                Vector3 seed=Scene-forward*(18f+i*7f)-right*(13f+i*2f);
                Vector3 road=World.GetNextPositionOnStreet(seed);
                Vehicle vehicle=SpawnVehicle(models[i%models.Length],road,CruiserHeadings[_sceneIndex]);
                if(vehicle==null||!vehicle.Exists())return false;
                vehicle.IsPersistent=true;
                _bankGetawayVehicles.Add(vehicle);
            }
            Game.LogTrivial("AdvancedK9 Callouts: "+_bankGetawayVehicles.Count+" suspect getaway vehicle(s) staged outside "+BankNames[_sceneIndex]+"; negotiation outcome determines whether they are used.");
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
            Vector3 managerPosition=PoliceVehicle!=null&&PoliceVehicle.Exists()?BankCoverPosition(PoliceVehicle,0f):new Vector3(Scene.X+12f,Scene.Y+12f,Scene.Z);
            float dx=managerPosition.X-Scene.X,dy=managerPosition.Y-Scene.Y,length=(float)System.Math.Sqrt(dx*dx+dy*dy);if(length<.1f)length=1f;
            managerPosition=new Vector3(managerPosition.X+dx/length*3.5f,managerPosition.Y+dy/length*3.5f,managerPosition.Z);
            Reporter.Position=managerPosition;
            Reporter.Heading=OfficerOne!=null&&OfficerOne.Exists()?OfficerOne.Heading:0f;
            NativeFunction.Natives.TASK_START_SCENARIO_IN_PLACE(Reporter,"WORLD_HUMAN_STAND_MOBILE",0,true);
            Game.LogTrivial("AdvancedK9 Callouts: bank manager secured behind the outer police-vehicle cover line and assigned as the command-post witness.");
        }

        private void MaintainBankTacticalPosture()
        {
            if(Subject==null||!Subject.Exists()||_bankEntryTeamActive||Game.GameTime<_nextBankTacticalRefresh)return;
            _nextBankTacticalRefresh=Game.GameTime+2200;
            List<Ped> officers=AllBankOfficers();
            for(int i=0;i<officers.Count;i++)
            {
                Ped officer=officers[i];if(officer==null||!officer.Exists())continue;
                uint weapon=NativeFunction.Natives.GET_HASH_KEY<uint>(_sceneIndex==0&&i>=8?"WEAPON_CARBINERIFLE":i%3==0?"WEAPON_PUMPSHOTGUN":"WEAPON_COMBATPISTOL");
                NativeFunction.Natives.GIVE_WEAPON_TO_PED(officer,weapon,i>=8?180:60,false,true);
                NativeFunction.Natives.SET_CURRENT_PED_WEAPON(officer,weapon,true);
                NativeFunction.Natives.SET_PED_USING_ACTION_MODE(officer,true);
                NativeFunction.Natives.TASK_AIM_GUN_AT_ENTITY(officer,Subject,-1,false);
                NativeFunction.Natives.SET_PED_KEEP_TASK(officer,true);
            }
        }

        private void ActivateBankEntryTeam()
        {
            if(_bankEntryTeamActive)return;
            _bankEntryTeamActive=true;_bankEntryAuthorized=false;
            List<Ped> officers=AllBankOfficers();
            for(int i=0;i<officers.Count&&_bankEntryTeam.Count<4;i++)_bankEntryTeam.Add(officers[i]);
            Game.DisplayNotification("~b~Bank entry team:~s~ Four perimeter officers are moving with you. Four officers remain on exterior security.");
            Game.LogTrivial("AdvancedK9 Callouts: four existing bank-scene officers reassigned from perimeter cover to the surrender entry/arrest team; no additional officers spawned.");
            UpdateBankEntryTeam(Game.LocalPlayer.Character,true);
        }

        private void UpdateBankEntryTeam(Ped player,bool force)
        {
            if(!_bankEntryTeamActive||player==null||!player.Exists())return;
            if(!_bankEntryAuthorized)
            {
                if(force||Game.GameTime>=_nextBankTacticalRefresh)
                {
                    _nextBankTacticalRefresh=Game.GameTime+1800;
                    Vector3[] offsets={new Vector3(-1.8f,-2.4f,0f),new Vector3(1.8f,-2.4f,0f),new Vector3(-2.8f,-4.2f,0f),new Vector3(2.8f,-4.2f,0f)};
                    for(int i=0;i<_bankEntryTeam.Count;i++)
                    {
                        Ped officer=_bankEntryTeam[i];if(officer==null||!officer.Exists())continue;
                        uint weapon=NativeFunction.Natives.GET_HASH_KEY<uint>(i==0?"WEAPON_STUNGUN":"WEAPON_COMBATPISTOL");
                        NativeFunction.Natives.SET_CURRENT_PED_WEAPON(officer,weapon,true);
                        NativeFunction.Natives.TASK_FOLLOW_TO_OFFSET_OF_ENTITY(officer,player,offsets[i].X,offsets[i].Y,0f,4.2f,-1,1.8f,true);
                        NativeFunction.Natives.SET_PED_KEEP_TASK(officer,true);
                    }
                }
                if(player.DistanceTo(Subject)<=12f)
                {
                    _bankEntryAuthorized=true;
                    Game.DisplayNotification("~b~Entry team:~s~ Moving to secure both surrendering suspects through LSPDFR arrest procedures.");
                }
            }
            if(_bankEntryAuthorized&&!_bankEntryArrestsAssigned)
            {
                _bankEntryArrestsAssigned=true;
                if(_bankEntryTeam.Count>=2)BeginNativeLspdfrOfficerArrest(Subject,_bankEntryTeam[0],_bankEntryTeam[1]);
                for(int i=0;i<_bankAccomplices.Count;i++)
                {
                    int arrestIndex=2+(i*2)%System.Math.Max(2,_bankEntryTeam.Count-2);
                    int coverIndex=System.Math.Min(_bankEntryTeam.Count-1,arrestIndex+1);
                    if(arrestIndex<_bankEntryTeam.Count)BeginNativeLspdfrOfficerArrest(_bankAccomplices[i],_bankEntryTeam[arrestIndex],_bankEntryTeam[coverIndex]);
                }
            }
        }

        private static void DisarmSurrenderingRobber(Ped robber)
        {
            if(robber==null||!robber.Exists())return;
            uint weapon=NativeFunction.Natives.GET_SELECTED_PED_WEAPON<uint>(robber);uint unarmed=NativeFunction.Natives.GET_HASH_KEY<uint>("WEAPON_UNARMED");
            if(weapon!=0&&weapon!=unarmed){Vector3 p=robber.Position;NativeFunction.Natives.SET_PED_DROPS_INVENTORY_WEAPON(robber,weapon,p.X,p.Y,p.Z+.15f,0);}
            NativeFunction.Natives.SET_CURRENT_PED_WEAPON(robber,unarmed,true);
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
            Vector3 suspectPosition=InteriorRobberPositions[_sceneIndex];
            Subject=SpawnPed("g_m_y_mexgoon_02",suspectPosition,180f);
            _managerIsHostage=Reporter!=null&&Reporter.Exists()&&Random.Next(100)<35;
            if(_managerIsHostage)
            {
                _hostage=Reporter;_hostage.Tasks.ClearImmediately();
                _hostage.Position=new Vector3(suspectPosition.X+0.9f,suspectPosition.Y-0.4f,suspectPosition.Z);
            }
            else _hostage=SpawnPed("a_f_y_business_02",new Vector3(suspectPosition.X+0.9f,suspectPosition.Y-0.4f,suspectPosition.Z),180f);
            if(Subject==null||!Subject.Exists()||_hostage==null||!_hostage.Exists())return false;
            Subject.MaxHealth=300;Subject.Health=300;
            ConfigureArmedBankRobber(true);
            NativeFunction.Natives.TASK_COWER(_hostage,-1);
            NativeFunction.Natives.TASK_AIM_GUN_AT_ENTITY(Subject,_hostage,-1,false);
            if(Random.Next(100)<55)
            {
                Ped accomplice=SpawnPed("g_m_y_mexgoon_01",new Vector3(suspectPosition.X-2.2f,suspectPosition.Y+1.2f,suspectPosition.Z),180f);
                ConfigureArmedAccomplice(accomplice,true);
                if(accomplice!=null&&accomplice.Exists())NativeFunction.Natives.TASK_GUARD_CURRENT_POSITION(accomplice,12f,12f,true);
            }
            MaintainBankTacticalPosture();
            if(!StageGetawayVehicles(1+_bankAccomplices.Count))return false;
            if(!AcceptAndRouteToSelectedBank(pacificInterior?"An armed suspect is believed to be hiding inside the bank.":"An armed suspect is holding an employee; K9 deployment is optional."))return false;
            DispatchUpdate("CalloutAccepted",pacificInterior?"Pacific Standard interior suspect":"Bank hostage containment","Local patrol jurisdiction","Suspect vehicles staged outside",_managerIsHostage?"Armed suspect holding the bank manager":"Armed suspect holding a bank employee at gunpoint","Contained inside the bank","Confirmed armed",pacificInterior?"Pacific Standard reports armed robbery suspects and a hostage inside. SWAT and patrol have established containment; begin negotiations before entry.":(_managerIsHostage?"Armed bank robbery with the manager held at gunpoint inside. Patrol has established containment; begin negotiations before entry.":"Armed bank robbery with an employee held at gunpoint inside. The manager is safe at the protected command post and is briefing police."),"WE_HAVE CRIME_BURGLARY IN_OR_ON_POSITION UNITS_RESPOND_CODE_3",Scene);
            return true;
        }

        private string CurrentBankBriefing()
        {
            if(_bankScenario==1)return "The manager is secure with patrol. An armed suspect fled in a dark Buffalo. The vehicle was last seen leaving the bank district; prepare for a vehicle pursuit and possible K9 bailout track.";
            if(_bankScenario==2)return _managerIsHostage?"Armed robbers are holding the bank manager at gunpoint inside. Eight armed officers are behind vehicle cover. Start negotiations before the four-officer entry team commits.":"Armed robbers are holding an employee at gunpoint inside. The manager is safe behind the command-post vehicle and provided the briefing. Eight armed officers are holding covered positions.";
            if(_bankScenario==3)return "Armed suspects and a hostage remain inside Pacific Standard. Twelve officers, including SWAT, have the exits covered. Start negotiations before the four-officer entry team or K9 commits.";
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
            if(Subject.Health<Subject.MaxHealth-5&&!Subject.IsDead&&(Subject.IsRagdoll||SubjectIsInCustody()||SuspectControlLocked))ProcessPostApprehensionMedical("");
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
                else if(custodyLease&&CustodyOwnerStable&&(!MedicalResponseStarted||MedicalResponseComplete)&&!_bankCustodyNotice)
                {
                    _bankCustodyNotice=true;
                    Game.DisplayNotification("~b~Custody stable:~s~ Request prisoner transport manually when ready. Bank personnel and perimeter units remain until you clear the callout.");
                }
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
                    TryStartBankPursuit();
                }
                catch(System.Exception ex){Game.LogTrivial("AdvancedK9 Callouts: LSPDFR bank pursuit setup contained; native flight remains active: "+ex.Message);}
                Game.DisplayNotification("~r~Bank robbery vehicle located.~s~ Join the pursuit. If the suspect bails out, Rex can transition to the recorded foot trail.");
                Game.LogTrivial("AdvancedK9 Callouts: bank vehicle-escape scenario started native high-speed flight; manager and scene officers remained at the bank.");
            }
            bool driverOnFoot=_bankScenarioStarted&&_getawayVehicle!=null&&_getawayVehicle.Exists()&&!Subject.IsInVehicle(_getawayVehicle,false);
            if(driverOnFoot&&!_bailoutTrackReady&&Subject.DistanceTo(_getawayVehicle)>6f&&!SubjectIsInCustody())
            {
                _bailoutTrackReady=true;
                try{if(BankPursuitIsRunning())EndBankPursuit();}catch(System.Exception ex){Game.LogTrivial("AdvancedK9 Callouts: pursuit-to-track handoff contained: "+ex.Message);}
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
            if(_bankEntryTeamActive)UpdateBankEntryTeam(player,false);else MaintainBankTacticalPosture();
            if(!_bankScenarioStarted&&player.DistanceTo(Scene)<45f)
            {
                _bankScenarioStarted=true;_scenarioStartedAt=Game.GameTime;
                SubjectBlip=Subject.AttachBlip();SubjectBlip.IsRouteEnabled=true;
                if(_bankScenario==3)Game.DisplayNotification("~o~Pacific Standard hostage incident.~s~ Patrol and SWAT are holding the perimeter. Begin negotiations before entry.");
                else Game.DisplayNotification("~r~Bank hostage containment.~s~ The robber and hostage are inside. Begin negotiations before entry.");
            }
            if(_bankScenarioStarted&&!_outcomeTaskIssued)
            {
                bool keyDown=Game.IsKeyDown(System.Windows.Forms.Keys.Y);
                if(!_negotiationStarted&&player.DistanceTo(Scene)<38f)
                {
                    Game.DisplayHelp("Hold the perimeter. Press ~y~Y~s~ to open bank-robbery negotiations.");
                    if(keyDown&&!_negotiationKeyHeld)
                    {
                        _negotiationStarted=true;_negotiationRound=0;_nextNegotiationAt=Game.GameTime+2500;
                        Game.DisplayNotification("~b~Negotiator:~s~ Phone contact established. Nobody moves; we are working toward a safe release.");
                        DispatchUpdate("NegotiationsStarted","Bank hostage negotiations","Local patrol jurisdiction","Suspect vehicles staged outside","Armed robbers holding an employee","Inside "+BankNames[_sceneIndex],"Negotiations active","Phone contact established. Patrol is maintaining containment while the negotiator seeks a peaceful surrender.","",Scene);
                    }
                }
                if(_negotiationStarted&&Game.GameTime>=_nextNegotiationAt)
                {
                    _negotiationRound++;_nextNegotiationAt=Game.GameTime+5000;
                    if(_negotiationRound==1)Game.DisplayNotification("~b~Negotiator:~s~ The robber wants a clear route and a vehicle. Keeping him talking.");
                    else if(_negotiationRound==2)Game.DisplayNotification("~b~Negotiator:~s~ We have demanded proof of life and the employee's immediate release.");
                    else ResolveBankNegotiation(player);
                }
                if(player.DistanceTo(Subject)<8f&&!_negotiationStarted)
                {
                    Game.DisplayNotification("~r~Premature entry:~s~ negotiations have collapsed.");
                    BeginHostileBankResponse(player);
                }
                _negotiationKeyHeld=keyDown;
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

        private void ResolveBankNegotiation(Ped player)
        {
            _outcomeTaskIssued=true;
            if(_outcome<=2)
            {
                ReleaseHostageToPerimeter();
                DisarmSurrenderingRobber(Subject);
                Subject.Tasks.ClearImmediately();NativeFunction.Natives.TASK_HANDS_UP(Subject,120000,player,-1,true);
                for(int i=0;i<_bankAccomplices.Count;i++)if(_bankAccomplices[i]!=null&&_bankAccomplices[i].Exists()){DisarmSurrenderingRobber(_bankAccomplices[i]);NativeFunction.Natives.TASK_HANDS_UP(_bankAccomplices[i],120000,player,-1,true);}
                ActivateBankEntryTeam();
                Game.DisplayNotification("~g~Negotiation successful.~s~ The hostage is coming out. Four existing perimeter officers are moving with you to arrest both suspects.");
                DispatchUpdate("PeacefulResolution","Bank hostage negotiations","Local patrol jurisdiction","Getaway vehicles unused","Robbers surrendering","Inside the bank","Hostage released","Negotiations achieved a peaceful release. The suspects are surrendering and staged getaway vehicles were not used.","",Scene);
            }
            else if(_outcome==3)
            {
                ReleaseHostageToPerimeter();
                Game.DisplayNotification("~o~Negotiations broke down.~s~ The employee is clear, but the robbers are attempting to reach their getaway cars.");
                BeginBankVehicleEscape();
            }
            else BeginHostileBankResponse(player);
        }

        private void ReleaseHostageToPerimeter()
        {
            if(_hostage==null||!_hostage.Exists())return;
            _hostageReleased=true;_hostage.Tasks.ClearImmediately();
            Vector3 safe=OfficerOne!=null&&OfficerOne.Exists()?OfficerOne.GetOffsetPosition(new Vector3(0f,-3f,0f)):CruiserScenes[_sceneIndex];
            NativeFunction.Natives.TASK_FOLLOW_NAV_MESH_TO_COORD(_hostage,safe.X,safe.Y,safe.Z,2.2f,30000,1.2f,0,0f);
        }

        private void BeginBankVehicleEscape()
        {
            var robbers=new List<Ped>();robbers.Add(Subject);robbers.AddRange(_bankAccomplices);
            for(int i=0;i<robbers.Count;i++)
            {
                Ped robber=robbers[i];if(robber==null||!robber.Exists())continue;
                Vehicle vehicle=i<_bankGetawayVehicles.Count?_bankGetawayVehicles[i]:null;
                robber.Tasks.ClearImmediately();
                if(vehicle!=null&&vehicle.Exists())NativeFunction.Natives.TASK_ENTER_VEHICLE(robber,vehicle,20000,-1,3.5f,1,0);
                else NativeFunction.Natives.TASK_SMART_FLEE_PED(robber,Game.LocalPlayer.Character,500f,-1,false,false);
            }
            GameFiber.StartNew(delegate
            {
                GameFiber.Wait(8500);
                for(int i=0;i<robbers.Count;i++)
                {
                    Ped robber=robbers[i];Vehicle vehicle=i<_bankGetawayVehicles.Count?_bankGetawayVehicles[i]:null;
                    if(robber!=null&&robber.Exists()&&vehicle!=null&&vehicle.Exists()&&robber.IsInVehicle(vehicle,false))NativeFunction.Natives.TASK_VEHICLE_DRIVE_WANDER(robber,vehicle,30f,786603);
                }
                try{TryStartBankPursuit();}catch(System.Exception ex){Game.LogTrivial("AdvancedK9 Callouts: negotiated getaway pursuit setup contained: "+ex.Message);}
            },"AdvancedK9 negotiated bank escape");
        }

        private void BeginHostileBankResponse(Ped player)
        {
            _outcomeTaskIssued=true;
            if(_hostage!=null&&_hostage.Exists())NativeFunction.Natives.TASK_COWER(_hostage,-1);
            Subject.Tasks.ClearImmediately();NativeFunction.Natives.TASK_COMBAT_PED(Subject,player,0,16);
            for(int i=0;i<_bankAccomplices.Count;i++)if(_bankAccomplices[i]!=null&&_bankAccomplices[i].Exists())NativeFunction.Natives.TASK_COMBAT_PED(_bankAccomplices[i],player,0,16);
            Game.DisplayNotification("~r~Negotiations failed.~s~ The robbers are hostile; the employee remains down inside. Use a controlled tactical response.");
        }

        public override void End()
        {
            try{if(BankPursuitIsRunning())EndBankPursuit();}catch{}
            for(int i=0;i<_bankAccomplices.Count;i++)if(_bankAccomplices[i]!=null&&_bankAccomplices[i].Exists())_bankAccomplices[i].Dismiss();
            _bankAccomplices.Clear();
            for(int i=0;i<_bankPerimeterOfficers.Count;i++)if(_bankPerimeterOfficers[i]!=null&&_bankPerimeterOfficers[i].Exists())_bankPerimeterOfficers[i].Dismiss();
            _bankPerimeterOfficers.Clear();
            for(int i=0;i<_bankPerimeterVehicles.Count;i++)if(_bankPerimeterVehicles[i]!=null&&_bankPerimeterVehicles[i].Exists())_bankPerimeterVehicles[i].Dismiss();
            _bankPerimeterVehicles.Clear();
            for(int i=0;i<_bankGetawayVehicles.Count;i++)if(_bankGetawayVehicles[i]!=null&&_bankGetawayVehicles[i].Exists())_bankGetawayVehicles[i].Dismiss();
            _bankGetawayVehicles.Clear();
            if(_swatBearcat!=null&&_swatBearcat.Exists())_swatBearcat.Dismiss();
            if(_hostage!=null&&_hostage.Exists())_hostage.Dismiss();
            if(_getawayVehicle!=null&&_getawayVehicle.Exists())_getawayVehicle.Dismiss();
            base.End();
        }
    }
}
