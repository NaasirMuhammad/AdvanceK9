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
        private Vehicle _airOne;
        private readonly List<Ped> _airOneOfficers=new List<Ped>();
        private bool _negotiationStarted;
        private int _negotiationRound;
        private uint _nextNegotiationAt;
        private bool _negotiationKeyHeld;
        private bool _negotiationChoiceHeld;
        private int _negotiationScore;
        private bool _escapeTermsAccepted;
        private bool _negotiationForcedEntry;
        private bool _bankPerimeterStaged;
        private bool _managerIsHostage;
        private bool _bankEntryTeamActive;
        private bool _bankEntryAuthorized;
        private bool _bankEntryArrestsAssigned;
        private bool _bankPostArrestPostureAssigned;
        private uint _nextBankTacticalRefresh;
        private readonly List<Ped> _bankEntryTeam=new List<Ped>();
        private readonly List<Ped> _bankEscapeSuspects=new List<Ped>();
        private readonly List<Rage.Object> _bankScentArticles=new List<Rage.Object>();
        private readonly List<Blip> _bankScentBlips=new List<Blip>();
        private readonly List<Blip> _bankPursuitUnitBlips=new List<Blip>();
        private Blip _bailoutVehicleBlip;
        private int _selectedBailoutTarget=-1;
        private bool _scentChoiceHeld;
        private bool _bankPursuitUnitsDeployed;
        private static int _lastBankScene=-1;
        protected override bool SharedOfficerArrestReady{get{return !_bankEntryTeamActive||_bankEntryAuthorized;}}

        protected override void ReleaseSupportForCustody()
        {
            if(!_bankEntryTeamActive){base.ReleaseSupportForCustody();return;}
            if(_bankPostArrestPostureAssigned)return;
            _bankPostArrestPostureAssigned=true;
            if(Subject!=null&&Subject.Exists())NativeFunction.Natives.TASK_STAND_STILL(Subject,-1);
            for(int i=0;i<_bankAccomplices.Count;i++)if(_bankAccomplices[i]!=null&&_bankAccomplices[i].Exists())NativeFunction.Natives.TASK_STAND_STILL(_bankAccomplices[i],-1);
            for(int i=0;i<_bankEntryTeam.Count;i++)
            {
                Ped officer=_bankEntryTeam[i];if(officer==null||!officer.Exists())continue;
                Ped target=i<2?Subject:_bankAccomplices.Count>0?_bankAccomplices[0]:Subject;
                if(i%2==0)NativeFunction.Natives.TASK_GUARD_CURRENT_POSITION(officer,8f,8f,true);
                else if(target!=null&&target.Exists())NativeFunction.Natives.TASK_AIM_GUN_AT_ENTITY(officer,target,-1,false);
                NativeFunction.Natives.SET_PED_KEEP_TASK(officer,true);
            }
            Game.LogTrivial("AdvancedK9 Callouts: cuffed bank suspects held with their arrest officers; tactical cover remains assigned and no suspect follow task was issued.");
        }

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
            var player=Game.LocalPlayer.Character;_sceneIndex=-1;
            var eligible=new List<int>();
            for(int i=0;i<BusinessScenes.Length;i++)
            {
                float distance=player.DistanceTo(BusinessScenes[i]);
                if(distance>180f)eligible.Add(i);
            }
            if(eligible.Count==0)return false;
            if(eligible.Count>1&&_lastBankScene>=0)eligible.Remove(_lastBankScene);
            _sceneIndex=eligible[Random.Next(eligible.Count)];
            _lastBankScene=_sceneIndex;
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

        private void AddRoadblockPair(List<Vector3> positions,List<float> headings,Vector3 center,float roadHeading,float separation)
        {
            Vector3 forward=HeadingVector(roadHeading);Vector3 right=new Vector3(forward.Y,-forward.X,0f);
            positions.Add(center+right*separation);headings.Add(roadHeading+90f);
            positions.Add(center-right*separation);headings.Add(roadHeading-90f);
        }

        private void GetBankPerimeterLayout(out List<Vector3> positions,out List<float> headings)
        {
            positions=new List<Vector3>();headings=new List<float>();
            if(_sceneIndex==1)
            {
                // Legion Square is intentionally hand staged to match the approved
                // five-car reference: three frontage units and two opposing lane
                // blockers. Do not add the sixth unit facing toward Elgin Avenue
                // in front of the negotiation position.
                positions.Add(new Vector3(137.2f,-1031.1f,29.20f));headings.Add(250f);
                positions.Add(new Vector3(148.6f,-1031.1f,29.20f));headings.Add(250f);
                positions.Add(new Vector3(160.0f,-1031.1f,29.20f));headings.Add(250f);
                positions.Add(new Vector3(130.5f,-1023.7f,29.20f));headings.Add(160f);
                positions.Add(new Vector3(169.2f,-1023.7f,29.20f));headings.Add(340f);
                return;
            }
            Vector3 forward=HeadingVector(CruiserHeadings[_sceneIndex]);Vector3 right=new Vector3(forward.Y,-forward.X,0f);
            Vector3 anchor=CruiserScenes[_sceneIndex];
            int pairCount=_sceneIndex==0?4:2;
            for(int pair=0;pair<pairCount;pair++)
            {
                Vector3 center;
                if(pair==0)center=anchor;
                else if(pair==1)center=anchor-forward*18f;
                else if(pair==2)center=anchor+forward*20f;
                else center=anchor+right*22f;
                AddRoadblockPair(positions,headings,center,CruiserHeadings[_sceneIndex],3.25f);
            }
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
            int requiredVehicles=_sceneIndex==0?8:_sceneIndex==1?5:4;
            int requiredOfficers=_sceneIndex==0?20:8;
            float heading=CruiserHeadings[_sceneIndex];
            List<Vector3> positions;List<float> headings;GetBankPerimeterLayout(out positions,out headings);
            PoliceVehicle.Position=positions[0];PoliceVehicle.Heading=headings[0];NativeFunction.Natives.SET_VEHICLE_ON_GROUND_PROPERLY(PoliceVehicle);
            PoliceVehicleTwo.Position=positions[1];PoliceVehicleTwo.Heading=headings[1];NativeFunction.Natives.SET_VEHICLE_ON_GROUND_PROPERLY(PoliceVehicleTwo);
            for(int i=2;i<requiredVehicles;i++)
            {
                Vector3 position=positions[i];float vehicleHeading=headings[i];
                if(SpawnBankPoliceVehicle(position,vehicleHeading)==null)return false;
            }
            List<Vehicle> perimeterVehicles=AllBankPoliceVehicles();
            if(perimeterVehicles.Count!=requiredVehicles)
            {
                Game.LogTrivial("AdvancedK9 Callouts: rejected incomplete bank perimeter at "+BankNames[_sceneIndex]+"; expected "+requiredVehicles+" cruisers but only "+perimeterVehicles.Count+" exist.");
                return false;
            }
            for(int i=0;i<perimeterVehicles.Count;i++)Game.LogTrivial("AdvancedK9 Callouts: verified visible cruiser "+(i+1)+"/"+requiredVehicles+" at "+perimeterVehicles[i].Position+", heading="+perimeterVehicles[i].Heading+".");
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
                bool swat=_sceneIndex==0&&i>=16;
                Vehicle cover=perimeterVehicles[(i/2)%perimeterVehicles.Count];Vector3 position=BankCoverPosition(cover,i%2==0?-0.75f:0.75f);
                if(SpawnBankOfficer(position,heading,swat)==null)return false;
            }
            if(_sceneIndex==0)
            {
                _swatBearcat=SpawnVehicle("riot",CruiserScenes[_sceneIndex]-forward*25f+right*11f,CruiserHeadings[_sceneIndex],true);
                if(_swatBearcat==null||!_swatBearcat.Exists())_swatBearcat=SpawnVehicle("fbi2",CruiserScenes[_sceneIndex]-forward*25f+right*11f,CruiserHeadings[_sceneIndex],true);
                if(_swatBearcat==null||!_swatBearcat.Exists())return false;
                _swatBearcat.IsPersistent=true;NativeFunction.Natives.SET_VEHICLE_LIGHTS(_swatBearcat,2);
                float[] swatLateral={-2.1f,-0.7f,0.7f,2.1f};
                for(int i=0;i<4;i++)
                {
                    int index=_bankPerimeterOfficers.Count-4+i;if(index<0||index>=_bankPerimeterOfficers.Count)continue;
                    Ped swat=_bankPerimeterOfficers[index];if(swat==null||!swat.Exists())continue;
                    swat.Position=BankCoverPosition(_swatBearcat,swatLateral[i]);swat.Heading=heading;
                    NativeFunction.Natives.TASK_STAND_GUARD(swat,swat.Position.X,swat.Position.Y,swat.Position.Z,heading,"WORLD_HUMAN_GUARD_STAND");
                }
                _airOne=SpawnVehicle("polmav",new Vector3(Scene.X,Scene.Y,Scene.Z+68f),heading,true);
                if(_airOne==null||!_airOne.Exists())return false;
                _airOne.IsPersistent=true;NativeFunction.Natives.SET_HELI_BLADES_FULL_SPEED(_airOne);
                Ped pilot=SpawnPed("s_m_y_pilot_01",_airOne.Position,heading);Ped observer=SpawnPed("s_m_y_swat_01",_airOne.Position,heading);
                if(pilot==null||!pilot.Exists()||observer==null||!observer.Exists())return false;
                _airOneOfficers.Add(pilot);_airOneOfficers.Add(observer);
                NativeFunction.Natives.SET_PED_INTO_VEHICLE(pilot,_airOne,-1);NativeFunction.Natives.SET_PED_INTO_VEHICLE(observer,_airOne,0);
                NativeFunction.Natives.GIVE_WEAPON_TO_PED(observer,NativeFunction.Natives.GET_HASH_KEY<uint>("WEAPON_CARBINERIFLE"),180,false,true);
                NativeFunction.Natives.TASK_HELI_MISSION(pilot,_airOne,0,0,Scene.X,Scene.Y,Scene.Z,9,24f,65f,-1f,90,45,18f,0);
            }
            MaintainSpawnedPoliceAssets();
            _bankPerimeterStaged=true;
            Game.LogTrivial("AdvancedK9 Callouts: bank perimeter staged at "+BankNames[_sceneIndex]+" with "+requiredVehicles+" marked cruisers and "+requiredOfficers+" armed ground personnel behind vehicle cover"+(_sceneIndex==0?" (all Pacific approaches closed; 16 patrol officers, four-officer SWAT team with BearCat, and two-officer Air One orbit).":_sceneIndex==1?" (approved Legion Square five-car layout; no cruiser facing Elgin Avenue at negotiations).":" (two roadblock pairs close both incoming lanes)."));
            return true;
        }

        private bool StageGetawayVehicles(int suspectCount)
        {
            Vector3 forward=HeadingVector(CruiserHeadings[_sceneIndex]);
            Vector3 right=new Vector3(forward.Y,-forward.X,0f);
            string[] models={"buffalo","schafter2","granger"};
            for(int i=0;i<suspectCount;i++)
            {
                Vector3 seed=_sceneIndex==1
                    ?new Vector3(175.5f+i*6.5f,-1047.8f,29.20f)
                    :Scene-forward*(24f+i*7f)-right*(16f+i*2f);
                Vector3 road=World.GetNextPositionOnStreet(seed);
                Vehicle vehicle=SpawnVehicle(models[i%models.Length],road,CruiserHeadings[_sceneIndex]);
                if(vehicle==null||!vehicle.Exists())return false;
                vehicle.IsPersistent=true;
                NativeFunction.Natives.SET_VEHICLE_DOORS_LOCKED(vehicle,1);
                _bankGetawayVehicles.Add(vehicle);
                Game.LogTrivial("AdvancedK9 Callouts: getaway vehicle "+(i+1)+" staged at "+vehicle.Position+" for "+BankNames[_sceneIndex]+".");
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
            Game.DisplayNotification("~b~Bank arrest team:~s~ Four on-scene officers are holding tactical cover until you reach the surrender point.");
            Game.LogTrivial("AdvancedK9 Callouts: four existing bank-scene officers designated for arrest and cover; they remain tactical and do not follow the player into the bank.");
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
                    for(int i=0;i<_bankEntryTeam.Count;i++)
                    {
                        Ped officer=_bankEntryTeam[i];if(officer==null||!officer.Exists())continue;
                        uint weapon=NativeFunction.Natives.GET_HASH_KEY<uint>(i==0?"WEAPON_STUNGUN":"WEAPON_COMBATPISTOL");
                        NativeFunction.Natives.SET_CURRENT_PED_WEAPON(officer,weapon,true);
                        NativeFunction.Natives.TASK_AIM_GUN_AT_ENTITY(officer,i<2?Subject:_bankAccomplices.Count>0?_bankAccomplices[0]:Subject,-1,false);
                        NativeFunction.Natives.SET_PED_KEEP_TASK(officer,true);
                    }
                }
                if(player.DistanceTo(Subject)<=12f)
                {
                    _bankEntryAuthorized=true;
                    Game.DisplayNotification("~b~Arrest team:~s~ Designated officers are moving from cover to perform visible LSPDFR arrests.");
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
            if(_bankEscapeSuspects.Count>0)
            {
                for(int i=0;i<_bankEscapeSuspects.Count;i++)if(!BankRobberResolved(_bankEscapeSuspects[i]))return false;
                return true;
            }
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

        private static bool BankRobberResolved(Ped robber)
        {
            if(robber==null||!robber.Exists()||robber.IsDead)return true;
            try{return NativeFunction.Natives.IS_PED_CUFFED<bool>(robber)||NativeFunction.Natives.IS_PED_HANDCUFFED<bool>(robber);}
            catch{return false;}
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
            bool found=false;Vector3 bestOffRoad=Vector3.Zero;int viableRoutes=0;float[] offsets={10f,-10f,14f,-14f,18f,-18f,22f,-22f};
            for(int routeAttempt=0;routeAttempt<24&&!found;routeAttempt++)
            {
                float distance=Random.Next(65,111);float radians=(float)(angle*System.Math.PI/180.0);
                Vector3 streetTarget=World.GetNextPositionOnStreet(Scene+new Vector3((float)System.Math.Sin(radians)*distance,(float)System.Math.Cos(radians)*distance,0f));
                dx=streetTarget.X-Scene.X;dy=streetTarget.Y-Scene.Y;length=(float)System.Math.Sqrt(dx*dx+dy*dy);if(length<.1f){angle=(angle+41f)%360f;continue;}
                if(System.Math.Abs(streetTarget.Z-Scene.Z)>4f){angle=(angle+41f)%360f;continue;}
                for(int i=0;i<offsets.Length;i++)
                {
                    Vector3 candidate=new Vector3(streetTarget.X-dy/length*offsets[i],streetTarget.Y+dx/length*offsets[i],streetTarget.Z);
                    if(NativeFunction.Natives.GET_INTERIOR_AT_COORDS<int>(candidate.X,candidate.Y,candidate.Z)!=0||NativeFunction.Natives.IS_POINT_ON_ROAD<bool>(candidate.X,candidate.Y,candidate.Z,0))continue;
                    viableRoutes++;if(viableRoutes==1)bestOffRoad=candidate;
                    Vector3 concealed;
                    if((TryFindExistingCover(candidate,out concealed)||TryFindWorldGeometryCover(candidate,out concealed))&&
                       !NativeFunction.Natives.IS_POINT_ON_ROAD<bool>(concealed.X,concealed.Y,concealed.Z,0))
                    {
                        _hidePosition=concealed;found=true;
                        Game.LogTrivial("AdvancedK9 Callouts: bank foot-trail route "+(routeAttempt+1)+" accepted after evaluating "+viableRoutes+" viable off-road endpoints.");
                        break;
                    }
                }
                angle=(angle+41f)%360f;
            }
            if(!found&&viableRoutes>0)
            {
                _hidePosition=bestOffRoad;found=true;
                Game.LogTrivial("AdvancedK9 Callouts: no streamed concealment object was available after "+viableRoutes+" viable route checks; retaining the selected foot-trail scenario at the safest off-road endpoint instead of changing variants.");
            }
            if(!found){Game.LogTrivial("AdvancedK9 Callouts: bank foot-trail variant rejected only after 24 route directions produced no viable off-road endpoint.");return false;}
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
            _bankEscapeSuspects.Add(Subject);
            int accompliceCount=1;
            for(int i=0;i<accompliceCount;i++)
            {
                Ped accomplice=SpawnPed(i==0?"g_m_y_mexgoon_01":"g_m_y_salvagoon_01",road,CruiserHeadings[_sceneIndex]);
                ConfigureArmedAccomplice(accomplice,false);
                if(accomplice!=null&&accomplice.Exists())
                {
                    NativeFunction.Natives.SET_PED_INTO_VEHICLE(accomplice,_getawayVehicle,i);
                    _bankEscapeSuspects.Add(accomplice);
                }
            }
            if(!AcceptAndRouteToSelectedBank("The manager is safe with officers; armed suspects fled in a vehicle moments before arrival."))return false;
            DispatchUpdate("CalloutAccepted","Bank robbery vehicle escape","Local patrol jurisdiction","Dark Buffalo fleeing the bank","Armed bank-robbery suspect","Leaving the bank district by vehicle","Confirmed armed","Bank robbery in progress. The manager remains with officers at the bank while an armed suspect escapes in a dark Buffalo. Prepare for a vehicle pursuit and possible K9 bailout track.","WE_HAVE CRIME_BURGLARY IN_OR_ON_POSITION UNITS_RESPOND_CODE_3",Scene);
            return true;
        }

        private void DeployBankPursuitUnits()
        {
            if(_bankPursuitUnitsDeployed||Subject==null||!Subject.Exists())return;
            _bankPursuitUnitsDeployed=true;
            List<Vehicle> vehicles=AllBankPoliceVehicles();List<Ped> officers=AllBankOfficers();
            int officerIndex=2;int deployed=0;
            // Vehicle zero and the first two officers remain at the bank to secure
            // evidence and preserve the primary crime scene.
            for(int vehicleIndex=1;vehicleIndex<vehicles.Count&&officerIndex<officers.Count;vehicleIndex++,officerIndex++)
            {
                Vehicle cruiser=vehicles[vehicleIndex];Ped officer=officers[officerIndex];
                if(cruiser==null||!cruiser.Exists()||officer==null||!officer.Exists())continue;
                deployed++;
                NativeFunction.Natives.SET_VEHICLE_SIREN(cruiser,true);NativeFunction.Natives.SET_VEHICLE_LIGHTS(cruiser,2);
                Blip unitBlip=officer.AttachBlip();unitBlip.Name="Bank pursuit unit "+deployed;unitBlip.IsRouteEnabled=deployed==1;_bankPursuitUnitBlips.Add(unitBlip);
                Ped chaseTarget=Subject;Vehicle chaseVehicle=cruiser;Ped chaseOfficer=officer;
                GameFiber.StartNew(delegate
                {
                    try
                    {
                        NativeFunction.Natives.SET_PED_KEEP_TASK(chaseOfficer,true);
                        NativeFunction.Natives.TASK_ENTER_VEHICLE(chaseOfficer,chaseVehicle,10000,-1,4.5f,1,0);
                        uint entryDeadline=Game.GameTime+10000;
                        while(!Finished&&chaseOfficer.Exists()&&chaseVehicle.Exists()&&!chaseOfficer.IsInVehicle(chaseVehicle,false)&&Game.GameTime<entryDeadline)GameFiber.Wait(200);
                        if(!chaseOfficer.IsInVehicle(chaseVehicle,false))NativeFunction.Natives.SET_PED_INTO_VEHICLE(chaseOfficer,chaseVehicle,-1);
                        NativeFunction.Natives.TASK_VEHICLE_CHASE(chaseOfficer,chaseTarget);
                        while(!Finished&&chaseOfficer.Exists()&&chaseTarget.Exists()&&chaseTarget.IsInVehicle(_getawayVehicle,false))GameFiber.Wait(300);
                        if(!Finished&&chaseOfficer.Exists()&&chaseVehicle.Exists())
                        {
                            NativeFunction.Natives.TASK_LEAVE_VEHICLE(chaseOfficer,chaseVehicle,0);
                            GameFiber.Wait(1200);
                            Ped footTarget=NearestUnresolvedEscapeSuspect(chaseOfficer.Position);
                            if(footTarget!=null&&footTarget.Exists())NativeFunction.Natives.TASK_GO_TO_ENTITY(chaseOfficer,footTarget,-1,8f,4.2f,0f,0);
                        }
                    }
                    catch(System.Exception ex){Game.LogTrivial("AdvancedK9 Callouts: bank pursuit-unit task contained: "+ex.Message);}
                },"AdvancedK9 bank pursuit unit");
            }
            Game.LogTrivial("AdvancedK9 Callouts: "+deployed+" existing scene cruisers joined the bank pursuit; one marked cruiser and two officers remained at the primary evidence scene. Pursuing officer GPS blips remain active through vehicle and foot phases.");
        }

        private Ped NearestUnresolvedEscapeSuspect(Vector3 position)
        {
            Ped nearest=null;float best=float.MaxValue;
            for(int i=0;i<_bankEscapeSuspects.Count;i++)
            {
                Ped suspect=_bankEscapeSuspects[i];if(BankRobberResolved(suspect))continue;
                float distance=suspect.DistanceTo(position);if(distance<best){best=distance;nearest=suspect;}
            }
            return nearest;
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
            if(_bankScenario==3)return "Armed suspects and a hostage remain inside Pacific Standard. Eight cruisers block every incoming traffic lane; sixteen patrol officers, four SWAT officers with a BearCat, and two Air One officers have containment. Start negotiations before entry.";
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
                DeployBankPursuitUnits();
                Game.DisplayNotification("~r~Bank robbery vehicle located.~s~ Join the pursuit. If the suspect bails out, Rex can transition to the recorded foot trail.");
                Game.LogTrivial("AdvancedK9 Callouts: bank vehicle-escape scenario started native high-speed flight; pursuit units departed in marked cruisers while one cruiser remained at the bank evidence scene.");
            }
            bool driverOnFoot=_bankScenarioStarted&&_getawayVehicle!=null&&_getawayVehicle.Exists()&&!Subject.IsInVehicle(_getawayVehicle,false);
            if(driverOnFoot&&!_bailoutTrackReady&&Subject.DistanceTo(_getawayVehicle)>6f&&!SubjectIsInCustody())
            {
                _bailoutTrackReady=true;
                try{if(BankPursuitIsRunning())EndBankPursuit();}catch(System.Exception ex){Game.LogTrivial("AdvancedK9 Callouts: pursuit-to-track handoff contained: "+ex.Message);}
                StageBailoutScentArticles();
                Game.DisplayNotification("~b~Dispatch:~s~ Driver and passenger fled separately. Return to the abandoned getaway car and select which seat scent Rex should track first.");
                DispatchUpdate("BailoutTrackReady","Bank robbery bailout track","Local patrol jurisdiction","Abandoned dark Buffalo","Armed driver and passenger","Separate directions from the abandoned vehicle","Confirmed armed","Distinct driver-seat and passenger-seat scent articles are preserved. The handler must select one target for Rex first.","",_getawayVehicle.Position);
            }
            if(_bailoutTrackReady)ProcessBailoutScentSelection(player);
            if(_bailoutTrackReady&&!_suspectLocated&&K9TrackingActive())SupportOfficersFollowK9();
            if(_bailoutTrackReady&&_selectedBailoutTarget>=0&&!_suspectLocated&&System.Math.Min(K9DistanceTo(Subject.Position),player.DistanceTo(Subject))<18f)
            {
                _suspectLocated=true;EndSupportTracking();
                if(_bankWeaponHash!=0)NativeFunction.Natives.SET_CURRENT_PED_WEAPON(Subject,_bankWeaponHash,true);
                SupportOfficersContainSubject();
                if(SubjectBlip!=null&&SubjectBlip.Exists())SubjectBlip.Delete();SubjectBlip=Subject.AttachBlip();SubjectBlip.Name="Located bank suspect";SubjectBlip.IsRouteEnabled=true;
                Game.DisplayNotification("~r~Rex located the selected armed bank suspect.~s~ The live GPS marker is restored. Establish containment and issue commands.");
            }
            if(_selectedBailoutTarget>=0&&BankRobberResolved(Subject)&&!AllBankRobbersResolved())PrepareNextBailoutTrack();
            if(AllBankRobbersResolved())PublishBankOutcome("AllSuspectsResolved","Driver and passenger are both secured or otherwise resolved. The bank scene remains held for investigation.");
            else if(_bankScenarioStarted&&Game.GameTime-_scenarioStartedAt>480000)PublishBankOutcome("SuspectsEscaped","The getaway suspects escaped after an extended pursuit. Preserve the originating bank scene for investigation.");
        }

        private void StageBailoutScentArticles()
        {
            if(_getawayVehicle==null||!_getawayVehicle.Exists())return;
            Vector3 driverArticle=_getawayVehicle.GetOffsetPosition(new Vector3(-1.35f,.15f,0f));
            Vector3 passengerArticle=_getawayVehicle.GetOffsetPosition(new Vector3(1.35f,.15f,0f));
            string[] models={"prop_cs_shoe_01","prop_cs_shopping_bag"};
            Vector3[] positions={driverArticle,passengerArticle};
            int count=System.Math.Min(_bankEscapeSuspects.Count,2);
            for(int i=0;i<count;i++)
            {
                Rage.Object article=SpawnProp(models[i],positions[i]);_bankScentArticles.Add(article);
                Blip articleBlip=new Blip(positions[i]);articleBlip.Name=i==0?"Driver scent article":"Passenger scent article";_bankScentBlips.Add(articleBlip);
            }
            _bailoutVehicleBlip=new Blip(_getawayVehicle.Position);_bailoutVehicleBlip.Name="Abandoned bank getaway vehicle";
            Game.LogTrivial("AdvancedK9 Callouts: distinct driver and passenger scent articles staged at the abandoned getaway vehicle; no target is assigned until the player selects a scent.");
        }

        private void ProcessBailoutScentSelection(Ped player)
        {
            if(_selectedBailoutTarget>=0||_getawayVehicle==null||!_getawayVehicle.Exists()||player.DistanceTo(_getawayVehicle)>14f)return;
            bool one=Game.IsKeyDown(System.Windows.Forms.Keys.D1),two=Game.IsKeyDown(System.Windows.Forms.Keys.D2);bool any=one||two;
            Game.DisplayHelp("Select Rex's scent: [1] ~y~driver-seat article~s~  [2] ~b~passenger-seat article~s~. Rex tracks only the scent you present.");
            if(!any){_scentChoiceHeld=false;return;}if(_scentChoiceHeld)return;_scentChoiceHeld=true;
            int index=one?0:1;if(index>=_bankEscapeSuspects.Count||BankRobberResolved(_bankEscapeSuspects[index]))
            {
                Game.DisplayNotification("~y~That bank suspect is already resolved. Select the remaining scent article.");return;
            }
            _selectedBailoutTarget=index;Subject=_bankEscapeSuspects[index];_suspectLocated=false;_bankCustodyNotice=false;ApiRequested=false;ResetSuspectLifecycleForNewSubject();
            Rage.Object article=index<_bankScentArticles.Count?_bankScentArticles[index]:null;Vector3 collection=article!=null&&article.Exists()?article.Position:_getawayVehicle.Position;
            AssignCalloutScent(Subject,collection,index==0?"preserved driver-seat scent":"preserved passenger-seat scent",(index==0?"Driver":"Passenger")+" scent selected. Bring Rex to the marked article and command COLLECT SCENT or TRACK.");
            if(index<_bankScentBlips.Count&&_bankScentBlips[index]!=null&&_bankScentBlips[index].Exists())_bankScentBlips[index].IsRouteEnabled=true;
            Game.DisplayNotification("~b~K9 target selected:~s~ "+(index==0?"getaway driver":"front passenger")+". Pursuing officers remain marked on GPS.");
        }

        private void PrepareNextBailoutTrack()
        {
            int completed=_selectedBailoutTarget;_selectedBailoutTarget=-1;_suspectLocated=false;ApiRequested=false;
            if(SubjectBlip!=null&&SubjectBlip.Exists())SubjectBlip.Delete();SubjectBlip=null;
            if(completed>=0&&completed<_bankScentBlips.Count&&_bankScentBlips[completed]!=null&&_bankScentBlips[completed].Exists())_bankScentBlips[completed].IsRouteEnabled=false;
            Game.DisplayNotification("~b~First scent resolved.~s~ Return to the abandoned getaway vehicle and select the remaining driver or passenger article. Pursuing-unit GPS markers remain active.");
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
                        _negotiationStarted=true;_negotiationRound=0;_negotiationScore=2-_outcome;_escapeTermsAccepted=false;_negotiationForcedEntry=false;_nextNegotiationAt=Game.GameTime+1200;
                        Game.DisplayNotification("~b~You:~s~ Phone contact established. You are now handling negotiations.");
                        DispatchUpdate("NegotiationsStarted","Bank hostage negotiations","Local patrol jurisdiction","Suspect vehicles staged outside","Armed robbers holding an employee","Inside "+BankNames[_sceneIndex],"Negotiations active","Phone contact established. Patrol is maintaining containment while the negotiator seeks a peaceful surrender.","",Scene);
                    }
                }
                if(_negotiationStarted)ProcessPlayerNegotiation(player);
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

        private void ProcessPlayerNegotiation(Ped player)
        {
            bool choice1=Game.IsKeyDown(System.Windows.Forms.Keys.D1);
            bool choice2=Game.IsKeyDown(System.Windows.Forms.Keys.D2);
            bool choice3=Game.IsKeyDown(System.Windows.Forms.Keys.D3);
            bool anyChoice=choice1||choice2||choice3;
            if(Game.GameTime<_nextNegotiationAt){_negotiationChoiceHeld=anyChoice;return;}
            if(_negotiationRound==0)Game.DisplayHelp("~b~You are negotiating:~s~ [1] Demand proof of life  [2] Ask what they want  [3] Threaten immediate entry");
            else if(_negotiationRound==1)Game.DisplayHelp("~b~You are negotiating:~s~ [1] Offer fair treatment  [2] Accept a clear route and vehicle for the hostage  [3] Reject all demands");
            else Game.DisplayHelp("~b~You are negotiating:~s~ [1] Order the hostage released first  [2] Demand everyone surrender now  [3] End the call and authorize entry");
            if(!anyChoice){_negotiationChoiceHeld=false;return;}
            if(_negotiationChoiceHeld)return;
            _negotiationChoiceHeld=true;_nextNegotiationAt=Game.GameTime+900;
            if(_negotiationRound==0)
            {
                if(choice1){_negotiationScore+=2;Game.DisplayNotification("~b~You:~s~ I need proof the hostage is alive.~n~~r~Robber:~s~ They are alive. Nobody comes through that door.");}
                else if(choice2){_negotiationScore+=1;Game.DisplayNotification("~b~You:~s~ Tell me what you need to end this safely.~n~~r~Robber:~s~ A clear route and a vehicle. Then the hostage walks.");}
                else {_negotiationScore-=3;Game.DisplayNotification("~b~You:~s~ Release them now or we are coming in.~n~~r~Robber:~s~ Back off. That is your only warning.");}
            }
            else if(_negotiationRound==1)
            {
                if(choice1){_negotiationScore+=2;Game.DisplayNotification("~b~You:~s~ Release the hostage and I will make sure everyone is treated fairly.~n~~r~Robber:~s~ I am listening.");}
                else if(choice2){_negotiationScore+=1;_escapeTermsAccepted=true;Game.DisplayNotification("~b~You:~s~ Release the hostage and you get the clear route and vehicle.~n~~r~Robber:~s~ Agreed. The hostage comes out first.");}
                else {_negotiationScore-=2;Game.DisplayNotification("~b~You:~s~ No vehicle and no clear route. Surrender now.~n~~r~Robber:~s~ Then we have a problem.");}
            }
            else
            {
                if(choice1){_negotiationScore+=2;Game.DisplayNotification("~b~You:~s~ Send the hostage out now. Then we complete the agreement.~n~~r~Robber:~s~ They are coming out.");}
                else if(choice2){_negotiationScore+=1;Game.DisplayNotification("~b~You:~s~ This ends safely only if everyone surrenders.~n~~r~Robber:~s~ Give us a moment.");}
                else {_negotiationScore-=4;_negotiationForcedEntry=true;Game.DisplayNotification("~b~You:~s~ The call is over. Entry team, stand by.~n~~r~Robber:~s~ Then come and get us.");}
            }
            _negotiationRound++;
            if(_negotiationRound>=3)ResolveBankNegotiation(player);
        }

        private void ResolveBankNegotiation(Ped player)
        {
            _outcomeTaskIssued=true;
            if(_escapeTermsAccepted&&!_negotiationForcedEntry&&_negotiationScore>-4)
            {
                ReleaseHostageToPerimeter();
                Game.DisplayNotification("~o~Your terms are in effect.~s~ The hostage is clear. The robbers are attempting to reach the getaway vehicles you agreed to release them toward.");
                BeginBankVehicleEscape();
                DispatchUpdate("NegotiatedEscape","Bank hostage negotiations","Local patrol jurisdiction","Staged getaway vehicles active","Armed robbers attempting escape","Leaving the bank","Hostage released","The player negotiated the hostage's release in exchange for a clear route and vehicle. Suspects are attempting escape.","",Scene);
            }
            else if(_negotiationScore>=3)
            {
                ReleaseHostageToPerimeter();
                DisarmSurrenderingRobber(Subject);
                Subject.Tasks.ClearImmediately();NativeFunction.Natives.TASK_HANDS_UP(Subject,120000,player,-1,true);
                for(int i=0;i<_bankAccomplices.Count;i++)if(_bankAccomplices[i]!=null&&_bankAccomplices[i].Exists()){DisarmSurrenderingRobber(_bankAccomplices[i]);NativeFunction.Natives.TASK_HANDS_UP(_bankAccomplices[i],120000,player,-1,true);}
                ActivateBankEntryTeam();
                Game.DisplayNotification("~g~Negotiation successful.~s~ The hostage is coming out. Four existing perimeter officers are holding cover and will arrest both suspects when you reach them.");
                DispatchUpdate("PeacefulResolution","Bank hostage negotiations","Local patrol jurisdiction","Getaway vehicles unused","Robbers surrendering","Inside the bank","Hostage released","Negotiations achieved a peaceful release. The suspects are surrendering and staged getaway vehicles were not used.","",Scene);
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
            for(int i=0;i<_bankScentArticles.Count;i++)if(_bankScentArticles[i]!=null&&_bankScentArticles[i].Exists())_bankScentArticles[i].Delete();
            _bankScentArticles.Clear();
            for(int i=0;i<_bankScentBlips.Count;i++)if(_bankScentBlips[i]!=null&&_bankScentBlips[i].Exists())_bankScentBlips[i].Delete();
            _bankScentBlips.Clear();
            for(int i=0;i<_bankPursuitUnitBlips.Count;i++)if(_bankPursuitUnitBlips[i]!=null&&_bankPursuitUnitBlips[i].Exists())_bankPursuitUnitBlips[i].Delete();
            _bankPursuitUnitBlips.Clear();
            if(_bailoutVehicleBlip!=null&&_bailoutVehicleBlip.Exists())_bailoutVehicleBlip.Delete();
            if(_swatBearcat!=null&&_swatBearcat.Exists())_swatBearcat.Dismiss();
            for(int i=0;i<_airOneOfficers.Count;i++)if(_airOneOfficers[i]!=null&&_airOneOfficers[i].Exists())_airOneOfficers[i].Dismiss();
            _airOneOfficers.Clear();
            if(_airOne!=null&&_airOne.Exists())_airOne.Dismiss();
            if(_hostage!=null&&_hostage.Exists())_hostage.Dismiss();
            if(_getawayVehicle!=null&&_getawayVehicle.Exists())_getawayVehicle.Dismiss();
            base.End();
        }
    }
}
