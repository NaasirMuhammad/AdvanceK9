using System;
using System.IO;
using System.Linq;
using AdvancedK9.API;
using LSPD_First_Response.Mod.API;
using LSPD_First_Response.Mod.Callouts;
using Rage;
using Rage.Native;

namespace AdvancedK9.Callouts
{
    public abstract class AdvancedK9Callout : Callout
    {
        protected enum SuspectLifecycle{Located,Challenged,Surrendering,ArrestInProgress,CustodySettling,InCustody,AwaitingTransport,Transporting,Complete}
        protected readonly Random Random=new Random();
        protected string ContextId;
        protected Vector3 Scene;
        protected Ped Subject;
        protected Ped Reporter;
        protected Vehicle SceneVehicle;
        protected Vehicle PoliceVehicle;
        protected Ped OfficerOne;
        protected Ped OfficerTwo;
        protected Ped ParentTwo;
        protected Vehicle MedicalVehicle;
        protected Ped MedicOne;
        protected Ped MedicTwo;
        protected Rage.Object EvidenceProp;
        protected Rage.Object CoverProp;
        protected Blip SubjectBlip;
        protected Blip SceneBlip;
        protected bool ApiRequested;
        protected uint StartedAt;
        protected bool Finished;
        private bool _cleanupCompleted;
        private bool _lifecycleClosed;
        private int _lifecycleGeneration;
        private bool _trafficControlled;
        private uint _nextSupportMove;
        private bool _medicalResponseStarted;
        private int _cachedDogHandle;
        private bool _supportFiberStarted;
        private bool _supportTrackingEnded;
        private bool _supportFormationLogged;
        private bool _secondaryTrafficControlled;
        private Vector3 _secondaryTrafficCenter;
        private string _lastCooperativeState="";
        private uint _custodyLeaseStarted;
        private bool _custodyLeaseActive;
        private uint _nextEmergencyLightRefresh;
        private uint _custodyObservedAt;
        private string _custodyOwner="None";
        private uint _deathObservedAt;
        private uint _custodyOwnerSettleUntil;
        private SuspectLifecycle _suspectLifecycle=SuspectLifecycle.Located;
        private bool _arrestStartedPublished;
        private string _calloutBridgeRequestId="";
        private uint _nextCalloutBridgeObservation;
        private bool _supportContainmentLogged;
        private bool _supportWeaponsInitialized;
        private bool _supportContainmentAssigned;
        private bool _supportCustodyGuardAssigned;
        private bool _backupInvestigationActive;
        private bool _custodyObservationEnabled=true;
        protected bool _k9DisengageIssued;
        private string _incidentVariant="General",_incidentJurisdiction="Local patrol jurisdiction",_incidentVehicle="Not yet reported",_incidentSuspect="Not yet reported",_incidentDirection="Not yet reported",_incidentRisk="Not yet determined";
        private string _lastDispatchSignature="";
        private uint _lastDispatchAt;
        private string _medicalStage="not-requested";
        private string _transportStage="not-requested";
        private bool _suspectLocationPublished;
        private static bool _nexusAudioSurfacesLogged;
        protected bool MedicalResponseStarted{get{return _medicalResponseStarted;}}
        protected bool MedicalResponseComplete;
        protected bool SeriousMedicalTransport;
        protected string CustodyOwner{get{return _custodyOwner;}}
        protected bool CustodyLocked{get{return _custodyLeaseActive;}}
        protected bool CustodyOwnerStable{get{return _custodyLeaseActive&&Game.GameTime>=_custodyOwnerSettleUntil&&!string.Equals(_custodyOwner,"Determining provider",StringComparison.OrdinalIgnoreCase);}}
        protected bool ArrestProviderOwnsSubject{get{return _suspectLifecycle>=SuspectLifecycle.ArrestInProgress;}}
        protected void SetCustodyObservationEnabled(bool enabled){_custodyObservationEnabled=enabled;}
        protected bool SuspectControlLocked
        {
            get
            {
                if(Subject==null||!Subject.Exists())return true;
                K9ApiSnapshot snapshot;
                bool k9Contact=AdvancedK9Api.TryGetSnapshot(out snapshot)&&(string.Equals(snapshot.State,"Apprehending",StringComparison.OrdinalIgnoreCase)||string.Equals(snapshot.State,"HoldingSuspect",StringComparison.OrdinalIgnoreCase));
                ObserveSuspectLifecycle();
                return k9Contact||MedicalResponseStarted||SubjectIsComplying()||ArrestProviderOwnsSubject;
            }
        }

        protected int BeginAcceptanceWork()
        {
            if(_lifecycleClosed||_cleanupCompleted)return -1;
            return ++_lifecycleGeneration;
        }

        protected bool AcceptanceWorkIsActive(int generation)
        {
            return generation>0&&!_lifecycleClosed&&!_cleanupCompleted&&!Finished&&generation==_lifecycleGeneration;
        }

        protected bool Prepare(string message,Vector3 scene,float radius,bool snapToStreet=true)
        {
            var player=Game.LocalPlayer.Character;
            if(player==null||!player.Exists())
            {
                Game.LogTrivial("AdvancedK9 Callouts: "+GetType().Name+" rejected because the player ped is unavailable.");
                return false;
            }
            Scene=snapToStreet?World.GetNextPositionOnStreet(scene):scene;
            ContextId=GetType().Name+"-"+Guid.NewGuid().ToString("N");
            CalloutMessage=message;
            CalloutPosition=Scene;
            ShowCalloutAreaBlipBeforeAccepting(Scene,radius);
            AddMinimumDistanceCheck(50f,Scene);
            Game.LogTrivial("AdvancedK9 Callouts: "+GetType().Name+" prepared at "+Scene+".");
            return true;
        }

        protected Vector3 StreetOffset(float forward,float side)
        {
            return Game.LocalPlayer.Character.GetOffsetPosition(new Vector3(side,forward,0f));
        }

        protected Ped SpawnPed(string modelName,Vector3 position,float heading)
        {
            var model=new Model(modelName);if(!model.IsValid)return null;model.LoadAndWait();var ped=new Ped(model,position,heading);model.Dismiss();
            if(ped!=null&&ped.Exists()){ped.IsPersistent=true;ped.BlockPermanentEvents=true;}return ped;
        }

        protected Vehicle SpawnVehicle(string modelName,Vector3 position,float heading,bool exactPlacement=false)
        {
            Vector3 nodePosition;
            float nodeHeading;
            bool nodeFound=NativeFunction.Natives.GET_CLOSEST_VEHICLE_NODE_WITH_HEADING<bool>(
                position.X,position.Y,position.Z,out nodePosition,out nodeHeading,1,3f,0);
            Vector3 spawnPosition=exactPlacement?position:(nodeFound?nodePosition:position);
            float spawnHeading=exactPlacement?heading:(nodeFound?nodeHeading:heading);

            Game.LogTrivial("AdvancedK9 Callouts: vehicle spawn request model="+modelName+
                ", requested="+position+", exactPlacement="+exactPlacement+", nodeFound="+nodeFound+
                ", resolved="+spawnPosition+", heading="+spawnHeading+".");

            var model=new Model(modelName);
            if(!model.IsValid)
            {
                Game.LogTrivial("AdvancedK9 Callouts: vehicle spawn rejected; invalid model "+modelName+".");
                return null;
            }

            model.LoadAndWait();
            GameFiber.Yield();
            var vehicle=new Vehicle(model,spawnPosition,spawnHeading);
            model.Dismiss();
            GameFiber.Yield();

            if(vehicle==null||!vehicle.Exists())
            {
                Game.LogTrivial("AdvancedK9 Callouts: vehicle spawn failed after construction for "+modelName+".");
                return null;
            }

            vehicle.IsPersistent=true;
            vehicle.Position=spawnPosition;
            vehicle.Heading=spawnHeading;
            NativeFunction.Natives.SET_VEHICLE_ON_GROUND_PROPERLY(vehicle);
            Game.LogTrivial("AdvancedK9 Callouts: vehicle spawn complete model="+modelName+
                ", position="+vehicle.Position+", heading="+vehicle.Heading+".");
            return vehicle;
        }

        protected void StartBackupOfficerInvestigationLoops()
        {
            if(SceneVehicle==null||!SceneVehicle.Exists())return;

            Vector3 driverRear=SceneVehicle.GetOffsetPosition(new Vector3(-1.8f,-2.2f,0f));
            Vector3 driverDoor=SceneVehicle.GetOffsetPosition(new Vector3(-1.8f,.2f,0f));
            Vector3 frontCheck=SceneVehicle.GetOffsetPosition(new Vector3(-1.2f,2.4f,0f));
            Vector3 passengerRear=SceneVehicle.GetOffsetPosition(new Vector3(1.8f,-2.2f,0f));
            Vector3 passengerDoor=SceneVehicle.GetOffsetPosition(new Vector3(1.8f,.2f,0f));
            Vector3 trunkCheck=SceneVehicle.GetOffsetPosition(new Vector3(1.1f,-2.8f,0f));

            _backupInvestigationActive=true;
            StartOfficerInvestigationLoop(OfficerOne,new[]{driverRear,driverDoor,frontCheck},"driver-side");
            StartOfficerInvestigationLoop(OfficerTwo,new[]{passengerRear,passengerDoor,trunkCheck},"passenger-side");
        }

        protected void StopBackupOfficerInvestigation()
        {
            _backupInvestigationActive=false;
            Game.LogTrivial("AdvancedK9 Callouts: backup investigation ownership released for the next callout phase.");
        }

        private void StartOfficerInvestigationLoop(Ped officer,Vector3[] points,string role)
        {
            if(officer==null||!officer.Exists())return;
            GameFiber.StartNew(delegate
            {
                try
                {
                    while(!Finished&&_backupInvestigationActive&&officer.Exists()&&SceneVehicle!=null&&SceneVehicle.Exists())
                    {
                        officer.BlockPermanentEvents=true;
                        officer.Tasks.Clear();
                        using(var sequence=new TaskSequence(officer))
                        {
                            sequence.Tasks.FollowNavigationMeshToPosition(points[0],SceneVehicle.Heading,1.25f);
                            sequence.Tasks.PlayAnimation("amb@code_human_police_investigate@idle_a","idle_a",1.0f,AnimationFlags.Loop);
                            sequence.Tasks.StandStill(1200);
                            sequence.Tasks.FollowNavigationMeshToPosition(points[1],SceneVehicle.Heading,1.1f);
                            sequence.Tasks.PlayAnimation("amb@code_human_police_investigate@idle_a","idle_a",1.0f,AnimationFlags.Loop);
                            sequence.Tasks.StandStill(2200);
                            sequence.Tasks.FollowNavigationMeshToPosition(points[2],SceneVehicle.Heading,1.15f);
                            sequence.Tasks.PlayAnimation("amb@code_human_police_investigate@idle_a","idle_a",1.0f,AnimationFlags.Loop);
                            sequence.Tasks.StandStill(1600);
                        }
                        NativeFunction.Natives.SET_PED_KEEP_TASK(officer,true);
                        Game.LogTrivial("AdvancedK9 Callouts: "+role+" backup investigation sequence assigned.");
                        uint cycleStarted=Game.GameTime;
                        while(!Finished&&_backupInvestigationActive&&officer.Exists()&&Game.GameTime-cycleStarted<22000)GameFiber.Wait(250);
                    }
                }
                catch(System.Exception ex){Game.LogTrivial("AdvancedK9 Callouts: "+role+" investigation loop contained: "+ex.Message);}
            },"AdvancedK9 "+role+" investigation");
        }

        protected Rage.Object SpawnProp(string modelName,Vector3 position)
        {
            var model=new Model(modelName);if(!model.IsValid)return null;model.LoadAndWait();var prop=new Rage.Object(model,position);model.Dismiss();
            if(prop!=null&&prop.Exists()){prop.IsPersistent=true;NativeFunction.Natives.PLACE_OBJECT_ON_GROUND_PROPERLY(prop);}return prop;
        }

        private Vehicle SpawnPoliceVehicle(Vector3 position,float heading,bool exactPlacement=false)
        {
            string[] models={"police3","police","sheriff"};
            foreach(string model in models)
            {
                var vehicle=SpawnVehicle(model,position,heading,exactPlacement);
                if(vehicle!=null&&vehicle.Exists())return vehicle;
            }
            return null;
        }

        private Ped SpawnPoliceOfficer(Vector3 position,float heading)
        {
            string[] models={"s_m_y_cop_01","s_m_y_sheriff_01","s_f_y_cop_01"};
            foreach(string model in models)
            {
                var officer=SpawnPed(model,position,heading);
                if(officer!=null&&officer.Exists())return officer;
            }
            return null;
        }

        protected bool StagePoliceScene()
        {
            Vector3 cruiserPosition=SceneVehicle!=null&&SceneVehicle.Exists()
                ?SceneVehicle.GetOffsetPosition(new Vector3(0f,-9f,0f))
                :new Vector3(Scene.X-10f,Scene.Y-7f,Scene.Z);
            Vector3 officerOnePosition=SceneVehicle!=null&&SceneVehicle.Exists()
                ?SceneVehicle.GetOffsetPosition(new Vector3(-2.5f,-2.5f,0f))
                :new Vector3(Scene.X-4f,Scene.Y-3f,Scene.Z);
            Vector3 officerTwoPosition=SceneVehicle!=null&&SceneVehicle.Exists()
                ?SceneVehicle.GetOffsetPosition(new Vector3(2.5f,-3.5f,0f))
                :new Vector3(Scene.X+4f,Scene.Y-3f,Scene.Z);
            float sceneHeading=SceneVehicle!=null&&SceneVehicle.Exists()?SceneVehicle.Heading:Game.LocalPlayer.Character.Heading;
            if(PoliceVehicle==null||!PoliceVehicle.Exists())
            {
                PoliceVehicle=SpawnPoliceVehicle(cruiserPosition,sceneHeading);
                if(PoliceVehicle==null||!PoliceVehicle.Exists())
                    PoliceVehicle=SpawnPoliceVehicle(new Vector3(cruiserPosition.X+3f,cruiserPosition.Y+3f,cruiserPosition.Z),sceneHeading);
            }
            if(OfficerOne==null||!OfficerOne.Exists())OfficerOne=SpawnPoliceOfficer(officerOnePosition,0f);
            if(OfficerTwo==null||!OfficerTwo.Exists())OfficerTwo=SpawnPoliceOfficer(officerTwoPosition,180f);
            AssignSceneSecurityRoles(sceneHeading);
            bool vehicleReady=PoliceVehicle!=null&&PoliceVehicle.Exists();
            bool officersReady=OfficerOne!=null&&OfficerOne.Exists()&&OfficerTwo!=null&&OfficerTwo.Exists();
            Game.LogTrivial("AdvancedK9 Callouts: police scene verification for "+GetType().Name+
                ": cruiser="+vehicleReady+", officer1="+(OfficerOne!=null&&OfficerOne.Exists())+
                ", officer2="+(OfficerTwo!=null&&OfficerTwo.Exists())+".");
            return vehicleReady&&officersReady;
        }

        protected bool StagePoliceScene(Vector3 cruiserPosition,float heading,bool exactVehiclePlacement=false)
        {
            Vector3 officerOnePosition=new Vector3(Scene.X-2.5f,Scene.Y-2f,Scene.Z);
            Vector3 officerTwoPosition=new Vector3(Scene.X+2.5f,Scene.Y-2f,Scene.Z);
            if(PoliceVehicle==null||!PoliceVehicle.Exists())PoliceVehicle=SpawnPoliceVehicle(cruiserPosition,heading,exactVehiclePlacement);
            if(OfficerOne==null||!OfficerOne.Exists())OfficerOne=SpawnPoliceOfficer(officerOnePosition,heading);
            if(OfficerTwo==null||!OfficerTwo.Exists())OfficerTwo=SpawnPoliceOfficer(officerTwoPosition,heading);
            if(PoliceVehicle!=null&&PoliceVehicle.Exists())
            {
                PoliceVehicle.IsPersistent=true;
                NativeFunction.Natives.SET_VEHICLE_ON_GROUND_PROPERLY(PoliceVehicle);
                NativeFunction.Natives.SET_VEHICLE_SIREN(PoliceVehicle,true);
                NativeFunction.Natives.SET_VEHICLE_LIGHTS(PoliceVehicle,2);
            }
            AssignSceneSecurityRoles(heading);
            bool ready=PoliceVehicle!=null&&PoliceVehicle.Exists()&&OfficerOne!=null&&OfficerOne.Exists()&&OfficerTwo!=null&&OfficerTwo.Exists();
            Game.LogTrivial("AdvancedK9 Callouts: verified business-scene staging for "+GetType().Name+": cruiser="+(PoliceVehicle!=null&&PoliceVehicle.Exists())+", officers="+ready+".");
            return ready;
        }

        private void AssignSceneSecurityRoles(float heading)
        {
            uint taser=NativeFunction.Natives.GET_HASH_KEY<uint>("WEAPON_STUNGUN");
            uint pistol=NativeFunction.Natives.GET_HASH_KEY<uint>("WEAPON_COMBATPISTOL");
            if(OfficerOne!=null&&OfficerOne.Exists())
            {
                OfficerOne.BlockPermanentEvents=true;OfficerOne.Tasks.Clear();
                NativeFunction.Natives.GIVE_WEAPON_TO_PED(OfficerOne,taser,2,false,false);
                NativeFunction.Natives.SET_CURRENT_PED_WEAPON(OfficerOne,taser,false);
                NativeFunction.Natives.TASK_ACHIEVE_HEADING(OfficerOne,heading,1500);
            }
            if(OfficerTwo!=null&&OfficerTwo.Exists())
            {
                OfficerTwo.BlockPermanentEvents=true;OfficerTwo.Tasks.Clear();
                NativeFunction.Natives.GIVE_WEAPON_TO_PED(OfficerTwo,pistol,60,false,false);
                NativeFunction.Natives.SET_CURRENT_PED_WEAPON(OfficerTwo,pistol,false);
                NativeFunction.Natives.TASK_ACHIEVE_HEADING(OfficerTwo,heading,1500);
            }
            Game.LogTrivial("AdvancedK9 Callouts: contact and cover officers assigned scene-security roles; passive phone scenarios disabled.");
        }

        protected void MaintainPoliceEmergencyLights()
        {
            if(PoliceVehicle==null||!PoliceVehicle.Exists()||Game.GameTime<_nextEmergencyLightRefresh)return;
            _nextEmergencyLightRefresh=Game.GameTime+500;
            try
            {
                PoliceVehicle.IsPersistent=true;
                NativeFunction.Natives.SET_VEHICLE_HAS_MUTED_SIRENS(PoliceVehicle,true);
                NativeFunction.Natives.SET_VEHICLE_SIREN(PoliceVehicle,true);
                NativeFunction.Natives.SET_VEHICLE_LIGHTS(PoliceVehicle,2);
            }
            catch(System.Exception ex){Game.LogTrivial("AdvancedK9 Callouts: emergency-light maintenance contained: "+ex.Message);}
        }

        protected void DispatchUpdate(string message,string scannerAudio,Vector3 position)
        {
            DispatchUpdate("StatusUpdate",_incidentVariant,_incidentJurisdiction,_incidentVehicle,_incidentSuspect,_incidentDirection,_incidentRisk,message,scannerAudio,position);
        }

        protected void DispatchUpdate(string eventName,string scenarioVariant,string jurisdiction,string vehicleDescription,string suspectDescription,string lastKnownDirection,string risk,string message,string scannerAudio,Vector3 position)
        {
            if(_incidentVariant=="General"&&!string.IsNullOrWhiteSpace(scenarioVariant)&&!scenarioVariant.Equals("General",StringComparison.OrdinalIgnoreCase))_incidentVariant=scenarioVariant;
            if(_incidentJurisdiction=="Local patrol jurisdiction"&&!string.IsNullOrWhiteSpace(jurisdiction)&&!jurisdiction.Equals("Unknown",StringComparison.OrdinalIgnoreCase)&&!jurisdiction.Equals("Current jurisdiction",StringComparison.OrdinalIgnoreCase))_incidentJurisdiction=jurisdiction;
            if(_incidentVehicle=="Not yet reported"&&!string.IsNullOrWhiteSpace(vehicleDescription)&&!vehicleDescription.Equals("Unknown",StringComparison.OrdinalIgnoreCase))_incidentVehicle=vehicleDescription;
            if(_incidentSuspect=="Not yet reported"&&!string.IsNullOrWhiteSpace(suspectDescription)&&!suspectDescription.Equals("Unknown",StringComparison.OrdinalIgnoreCase))_incidentSuspect=suspectDescription;
            if(!string.IsNullOrWhiteSpace(lastKnownDirection)&&!lastKnownDirection.Equals("Unknown",StringComparison.OrdinalIgnoreCase))_incidentDirection=lastKnownDirection;
            if(_incidentRisk=="Not yet determined"&&!string.IsNullOrWhiteSpace(risk)&&(risk.IndexOf("armed",StringComparison.OrdinalIgnoreCase)>=0||risk.IndexOf("weapon",StringComparison.OrdinalIgnoreCase)>=0))_incidentRisk=risk.IndexOf("unknown",StringComparison.OrdinalIgnoreCase)>=0?"Not yet determined":risk;
            if(eventName=="TransportRequested")_transportStage="requested";else if(eventName=="TransportArrived")_transportStage="arrived";else if(eventName=="TransportLoaded")_transportStage="loaded";else if(eventName=="TransportComplete")_transportStage="complete";else if(eventName=="TransportFailed")_transportStage="failed";
            string k9State="not-tracking";K9ApiSnapshot snapshot;
            if(AdvancedK9Api.TryGetSnapshot(out snapshot)&&!string.IsNullOrWhiteSpace(snapshot.State))k9State=snapshot.State;
            if(eventName=="SuspectLocated"||eventName=="K9Apprehension"||eventName=="SuspectSurrender"||_custodyLeaseActive)_suspectLocationPublished=true;
            string located=_suspectLocationPublished?"located":"not-located";
            string apprehension=eventName=="K9Apprehension"?"k9-controlled":_custodyLeaseActive?"arrest-provider-control":"none";
            string surrender=_suspectLifecycle>=SuspectLifecycle.ArrestInProgress?"secured":eventName=="SuspectSurrender"||SubjectIsComplying()?"complying":"not-compliant";
            string bite=eventName=="K9Apprehension"||_medicalStage!="not-requested"?"injured":"none";
            string structured="AK9_EVENT|eventId="+CleanDispatchValue(ContextId+":"+eventName)+"|event="+CleanDispatchValue(eventName)+"|callout="+CleanDispatchValue(GetType().Name)+"|variant="+CleanDispatchValue(_incidentVariant)+"|location="+CleanDispatchValue(ReadableLocation(position))+"|jurisdiction="+CleanDispatchValue(_incidentJurisdiction)+"|vehicle="+CleanDispatchValue(_incidentVehicle)+"|suspect="+CleanDispatchValue(_incidentSuspect)+"|direction="+CleanDispatchValue(_incidentDirection)+"|armedStatus="+CleanDispatchValue(_incidentRisk)+"|scentSource="+CleanDispatchValue(ApiRequested?"preserved driver-seat article":"pending")+"|scentStatus="+CleanDispatchValue(ApiRequested?(K9TrackingActive()?"tracking":"preserved"):"pending")+"|k9TrackingStatus="+CleanDispatchValue(k9State)+"|suspectLocatedStatus="+located+"|apprehensionStatus="+apprehension+"|surrenderStatus="+surrender+"|biteInjuryStatus="+bite+"|emsRequestStatus="+(_medicalStage=="not-requested"?"not-requested":"requested")+"|emsTreatmentStatus="+CleanDispatchValue(_medicalStage)+"|custodyOwner="+CleanDispatchValue(_custodyOwner)+"|arrestStatus="+(_suspectLifecycle>=SuspectLifecycle.InCustody?"in-custody":_suspectLifecycle>=SuspectLifecycle.ArrestInProgress?"arrest-in-progress":"not-arrested")+"|transportRequestStatus="+CleanDispatchValue(_transportStage)+"|transportOwner="+CleanDispatchValue(_custodyOwner)+"|transportStatus="+CleanDispatchValue(_transportStage)+"|narrative="+CleanDispatchValue(message);
            string signature=eventName+"|"+scenarioVariant+"|"+k9State+"|"+_medicalStage+"|"+_custodyOwner+"|"+message;
            if(signature==_lastDispatchSignature&&Game.GameTime-_lastDispatchAt<15000){Game.LogTrivial("AdvancedK9 Callouts: duplicate dispatch transition suppressed: "+eventName+".");return;}
            _lastDispatchSignature=signature;_lastDispatchAt=Game.GameTime;
            Game.DisplayNotification("~b~Dispatch:~s~ "+message);
            PublishCalloutInterfaceMessage(structured);
            bool nexusOwnsAudio=AppDomain.CurrentDomain.GetAssemblies().Any(a=>a.GetName().Name.IndexOf("Nexus",StringComparison.OrdinalIgnoreCase)>=0);
            bool dynamicQueued=nexusOwnsAudio&&TryRequestNexusDynamicAudio(structured,message);
            if(!nexusOwnsAudio&&!string.IsNullOrWhiteSpace(scannerAudio))
            {
                try{Functions.PlayScannerAudioUsingPosition(scannerAudio,position);}
                catch(System.Exception ex){Game.LogTrivial("AdvancedK9 Callouts: scanner update contained: "+ex.Message);}
            }
            else if(nexusOwnsAudio&&!dynamicQueued)Game.LogTrivial("AdvancedK9 Callouts: Nexus owns dispatch audio; structured Gemini narrative published through CalloutInterface and text retained as fallback.");
            Game.LogTrivial("AdvancedK9 Callouts: dispatch incident update ["+ContextId+"]: "+message);
        }

        private static string CleanDispatchValue(string value){return (value??"").Replace("|","/").Replace("\r"," ").Replace("\n"," ").Trim();}

        private static string ReadableLocation(Vector3 position)
        {
            try
            {
                uint streetHash=0,crossHash=0;NativeFunction.Natives.GET_STREET_NAME_AT_COORD(position.X,position.Y,position.Z,out streetHash,out crossHash);
                string street=streetHash==0?"":NativeFunction.Natives.GET_STREET_NAME_FROM_HASH_KEY<string>(streetHash);
                string cross=crossHash==0?"":NativeFunction.Natives.GET_STREET_NAME_FROM_HASH_KEY<string>(crossHash);
                if(!string.IsNullOrWhiteSpace(street))return string.IsNullOrWhiteSpace(cross)?street:street+" at "+cross;
            }
            catch{}
            return "Live incident location";
        }

        private bool TryRequestNexusDynamicAudio(string structured,string narrative)
        {
            try
            {
                var nexus=AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a=>a.GetName().Name.IndexOf("Nexus",StringComparison.OrdinalIgnoreCase)>=0);
                if(nexus==null)return false;
                string[] approvedNames={"RequestDispatchNarration","QueueDispatchNarrative","PublishDispatchEvent","SpeakIncidentUpdate"};
                foreach(var type in nexus.GetTypes())
                foreach(var method in type.GetMethods(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Instance))
                {
                    bool candidate=approvedNames.Contains(method.Name)||method.Name.IndexOf("Narrat",StringComparison.OrdinalIgnoreCase)>=0||method.Name.IndexOf("SpeakIncident",StringComparison.OrdinalIgnoreCase)>=0||method.Name.IndexOf("DispatchEvent",StringComparison.OrdinalIgnoreCase)>=0;
                    if(!candidate)continue;
                    var parameters=method.GetParameters();
                    if(!_nexusAudioSurfacesLogged)Game.LogTrivial("AdvancedK9 Callouts: Nexus dynamic-audio surface: "+type.FullName+"."+method.Name+"("+string.Join(",",parameters.Select(p=>p.ParameterType.Name+" "+p.Name))+").");
                    object instance=null;if(!method.IsStatic){var property=type.GetProperty("Instance",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static)??type.GetProperty("Current",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static);if(property==null)continue;instance=property.GetValue(null,null);if(instance==null)continue;}
                    object result=null;if(parameters.Length==1&&parameters[0].ParameterType==typeof(string))result=method.Invoke(instance,new object[]{structured});else if(parameters.Length==2&&parameters.All(p=>p.ParameterType==typeof(string)))result=method.Invoke(instance,new object[]{narrative,structured});else continue;
                    _nexusAudioSurfacesLogged=true;if(method.ReturnType==typeof(bool)&&!(bool)result)continue;Game.LogTrivial("AdvancedK9 Callouts: Gemini dispatch narrative accepted by "+type.FullName+"."+method.Name+".");return true;
                }
                _nexusAudioSurfacesLogged=true;
            }
            catch(System.Exception ex){Game.LogTrivial("AdvancedK9 Callouts: Nexus dynamic dispatch request contained: "+ex.Message);}
            return false;
        }

        private void PublishCalloutInterfaceMessage(string message)
        {
            try
            {
                var assembly=System.AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a=>
                    string.Equals(a.GetName().Name,"CalloutInterfaceAPI",StringComparison.OrdinalIgnoreCase));
                if(assembly==null)return;
                var type=assembly.GetType("CalloutInterfaceAPI.Functions",false);
                if(type==null)return;
                var method=type.GetMethods(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static)
                    .FirstOrDefault(m=>m.Name=="SendMessage"&&m.GetParameters().Length==2);
                if(method==null)return;
                method.Invoke(null,new object[]{this,message});
                Game.LogTrivial("AdvancedK9 Callouts: incident narrative published through CalloutInterface for NexusDispatch/MDT consumption.");
            }
            catch(System.Exception ex){Game.LogTrivial("AdvancedK9 Callouts: CalloutInterface incident update contained: "+ex.Message);}
        }

        protected bool TryResolveSafePedPosition(Vector3 requested,out Vector3 safe)
        {
            safe=requested;
            try
            {
                Vector3 nav;
                if(!NativeFunction.Natives.GET_SAFE_COORD_FOR_PED<bool>(requested.X,requested.Y,requested.Z,true,out nav,16))return false;
                float ground;
                if(!NativeFunction.Natives.GET_GROUND_Z_FOR_3D_COORD<bool>(nav.X,nav.Y,nav.Z+75f,out ground,false))return false;
                safe=new Vector3(nav.X,nav.Y,ground+0.15f);
                return NativeFunction.Natives.GET_INTERIOR_AT_COORDS<int>(safe.X,safe.Y,safe.Z)==0&&System.Math.Abs(safe.Z-Scene.Z)<8f;
            }
            catch(System.Exception ex){Game.LogTrivial("AdvancedK9 Callouts: safe pedestrian coordinate validation contained: "+ex.Message);return false;}
        }

        protected bool K9ReadyOnFoot()
        {
            K9ApiSnapshot snapshot;
            if(AdvancedK9Api.TryGetSnapshot(out snapshot)&&snapshot.OnDuty&&snapshot.Deployed&&snapshot.DogHandle>0)
            {
                _cachedDogHandle=snapshot.DogHandle;
                return !string.Equals(snapshot.State,"InVehicle",StringComparison.OrdinalIgnoreCase)&&
                    !string.Equals(snapshot.State,"Dismissed",StringComparison.OrdinalIgnoreCase);
            }
            return _cachedDogHandle>0&&NativeFunction.Natives.DOES_ENTITY_EXIST<bool>(_cachedDogHandle);
        }

        protected void ControlSceneTraffic()
        {
            if(_trafficControlled)return;
            NativeFunction.Natives.SET_ROADS_IN_AREA(Scene.X-32f,Scene.Y-32f,Scene.Z-8f,Scene.X+32f,Scene.Y+32f,Scene.Z+8f,false,true);
            _trafficControlled=true;
            Game.LogTrivial("AdvancedK9 Callouts: traffic control established around "+GetType().Name+" scene.");
        }

        protected void ControlLiveTraffic(Vector3 center,float radius)
        {
            try
            {
                if(_secondaryTrafficControlled&&center.DistanceTo(_secondaryTrafficCenter)<12f)radius=Math.Max(radius,35f);
                Vehicle playerVehicle=Game.LocalPlayer.Character.CurrentVehicle;
                foreach(Vehicle vehicle in World.GetAllVehicles())
                {
                    if(vehicle==null||!vehicle.Exists()||vehicle.DistanceTo(center)>radius||vehicle==SceneVehicle||vehicle==PoliceVehicle||vehicle==playerVehicle)continue;
                    Ped driver=vehicle.Driver;
                    if(driver==null||!driver.Exists())continue;
                    NativeFunction.Natives.TASK_VEHICLE_TEMP_ACTION(driver,vehicle,6,5000);
                }
            }
            catch(System.Exception ex){Game.LogTrivial("AdvancedK9 Callouts: live traffic containment fallback: "+ex.Message);}
        }

        protected float K9DistanceTo(Vector3 position)
        {
            K9ApiSnapshot snapshot;
            if(AdvancedK9Api.TryGetSnapshot(out snapshot)&&snapshot.DogHandle>0)_cachedDogHandle=snapshot.DogHandle;
            if(_cachedDogHandle<=0||!NativeFunction.Natives.DOES_ENTITY_EXIST<bool>(_cachedDogHandle))
                return Game.LocalPlayer.Character.Position.DistanceTo(position);
            Vector3 dogPosition=NativeFunction.Natives.GET_ENTITY_COORDS<Vector3>(_cachedDogHandle,true);
            return dogPosition.DistanceTo(position);
        }

        protected void RouteToScene(string instruction)
        {
            if(SceneBlip!=null&&SceneBlip.Exists())SceneBlip.Delete();
            SceneBlip=new Blip(Scene);SceneBlip.IsRouteEnabled=true;
            Game.DisplayNotification("~b~AdvancedK9 callout:~s~ "+instruction+"~n~Follow the GPS route to the investigation start point.");
            Game.LogTrivial("AdvancedK9 Callouts: routed player to "+GetType().Name+" start at "+Scene+".");
        }

        protected void ClearSceneRoute()
        {
            if(SceneBlip!=null&&SceneBlip.Exists()){SceneBlip.IsRouteEnabled=false;SceneBlip.Delete();}
            SceneBlip=null;
        }

        protected int HandleOf(Entity entity)
        {
            if(entity==null||!entity.Exists())return 0;
            string raw=entity.Handle.ToString();int value;
            if(int.TryParse(raw,out value))return value;
            raw=raw.StartsWith("0x",StringComparison.OrdinalIgnoreCase)?raw.Substring(2):raw;
            return int.TryParse(raw,System.Globalization.NumberStyles.HexNumber,System.Globalization.CultureInfo.InvariantCulture,out value)?value:0;
        }

        protected bool K9Available()
        {
            K9ApiSnapshot snapshot;return AdvancedK9Api.TryGetSnapshot(out snapshot)&&snapshot.OnDuty&&snapshot.Deployed;
        }

        protected void AssignCalloutScent(Ped target,string details)
        {
            if(ApiRequested||target==null||!target.Exists())return;
            ApiRequested=true;
            AdvancedK9Api.SendCommand("AssignScent",ContextId,HandleOf(target),"Ped",Scene.X,Scene.Y,Scene.Z,details,target.Position.X,target.Position.Y,target.Position.Z);
            BeginSupportTrackingFiber();
            Game.DisplayNotification("~b~AdvancedK9 callout:~s~ preserved vehicle scent is ready. Command Rex to COLLECT SCENT or TRACK beside the abandoned vehicle.");
        }

        protected void AssignCalloutScent(Ped target,Vector3 stableTargetPosition,string details)
        {
            if(ApiRequested||target==null||!target.Exists())return;
            ApiRequested=true;
            AdvancedK9Api.SendCommand("AssignScent",ContextId,HandleOf(target),"Ped",Scene.X,Scene.Y,Scene.Z,details,stableTargetPosition.X,stableTargetPosition.Y,stableTargetPosition.Z);
            BeginSupportTrackingFiber();
            Game.DisplayNotification("~b~AdvancedK9 callout:~s~ the preserved driver-seat scent is viable for this response. Deploy Rex beside the abandoned vehicle and command TRACK.");
            Game.LogTrivial("AdvancedK9 Callouts: preserved scent published on handler arrival with stable endpoint "+stableTargetPosition+"; travel time does not age the callout article.");
        }

        protected void AssignCalloutScent(Ped target,Vector3 collectionPosition,string details,string instruction)
        {
            if(ApiRequested||target==null||!target.Exists())return;
            ApiRequested=true;
            AdvancedK9Api.SendCommand("AssignScent",ContextId,HandleOf(target),"Ped",collectionPosition.X,collectionPosition.Y,collectionPosition.Z,details,target.Position.X,target.Position.Y,target.Position.Z);
            BeginSupportTrackingFiber();
            Game.DisplayNotification("~b~AdvancedK9 callout:~s~ "+instruction);
        }

        protected bool K9TrackingActive()
        {
            K9ApiSnapshot snapshot;
            return AdvancedK9Api.TryGetSnapshot(out snapshot)&&string.Equals(snapshot.State,"Tracking",StringComparison.OrdinalIgnoreCase);
        }

        protected bool SubjectIsInCustody()
        {
            if(Subject==null||!Subject.Exists())return false;
            try
            {
                if(NativeFunction.Natives.IS_PED_CUFFED<bool>(Subject)||NativeFunction.Natives.IS_PED_HANDCUFFED<bool>(Subject))return true;
                foreach(string name in new[]{"IsPedArrested"})
                {
                    var method=typeof(Functions).GetMethod(name,System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static);
                    if(method!=null&&(bool)method.Invoke(null,new object[]{Subject}))return true;
                }
                return false;
            }
            catch{return false;}
        }

        protected bool SubjectIsComplying()
        {
            if(Subject==null||!Subject.Exists()||Subject.IsDead)return false;
            try{return SubjectIsInCustody()||NativeFunction.Natives.IS_PED_HANDS_UP<bool>(Subject);}
            catch{return SubjectIsInCustody();}
        }

        protected bool UpdateCustodyLease()
        {
            if(Subject==null||!Subject.Exists())return false;
            ObserveSuspectLifecycle();
            if(_custodyLeaseActive)
            {
                if(Game.GameTime>=_custodyOwnerSettleUntil&&string.Equals(_custodyOwner,"Determining provider",StringComparison.OrdinalIgnoreCase))_custodyOwner=DetectCustodyOwner();
                return true;
            }
            if(Subject.IsDead)return false;
            bool active=SubjectIsInCustody();
            if(active&&!_custodyLeaseActive)
            {
                _custodyLeaseActive=true;_custodyLeaseStarted=Game.GameTime;_custodyObservedAt=Game.GameTime;_custodyOwnerSettleUntil=Game.GameTime+1800;
                Game.LogTrivial("AdvancedK9 Callouts: custody lease acquired without changing suspect tasks, health, ragdoll, or invincibility; health="+Subject.Health+"/"+Subject.MaxHealth+".");
                _custodyOwner="Determining provider";
                _suspectLifecycle=SuspectLifecycle.CustodySettling;
                Game.LogTrivial("AdvancedK9 Callouts: AdvancedK9 suspect tasking suspended for the external arrest provider.");
            }
            if(active&&Subject.Health>0&&Subject.IsDead&&Game.GameTime-_custodyObservedAt<3000)
                Game.LogTrivial("AdvancedK9 Callouts: transient provider handoff death flag ignored; no health, task, or resurrection write was issued.");
            return active;
        }

        protected void ObserveSuspectLifecycle()
        {
            if(!_custodyObservationEnabled||Subject==null||!Subject.Exists())return;
            bool arresting=false;
            try{arresting=NativeFunction.Natives.IS_PED_BEING_ARRESTED<bool>(Subject);}catch{}
            try
            {
                var method=typeof(Functions).GetMethod("IsPedGettingArrested",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static);
                arresting=arresting||(method!=null&&(bool)method.Invoke(null,new object[]{Subject}));
            }
            catch{}
            if((arresting||SubjectIsInCustody())&&_suspectLifecycle<SuspectLifecycle.ArrestInProgress)
            {
                _suspectLifecycle=SuspectLifecycle.ArrestInProgress;
                Game.LogTrivial("AdvancedK9 Callouts: arrest initiation observed; all queued surrender, flee, task, health, ragdoll, and invincibility writes are now blocked.");
                if(!_arrestStartedPublished){_arrestStartedPublished=true;DispatchUpdate("ArrestStarted","Provider arrest handoff",_incidentJurisdiction,_incidentVehicle,_incidentSuspect,"Live arrest location",_incidentRisk,"The arrest provider has begun taking the suspect into custody.","",Subject.Position);}
            }
            if(Game.GameTime>=_nextCalloutBridgeObservation){_nextCalloutBridgeObservation=Game.GameTime+750;WriteCalloutBridgeRequest("ObserveCustody");ReadCalloutBridgeState();}
            if(SubjectIsInCustody()&&_suspectLifecycle<SuspectLifecycle.CustodySettling)_suspectLifecycle=SuspectLifecycle.CustodySettling;
        }

        private void WriteCalloutBridgeRequest(string action)
        {
            try{if(string.IsNullOrWhiteSpace(_calloutBridgeRequestId)||action=="RequestTransport")_calloutBridgeRequestId=ContextId+":"+action+":"+Guid.NewGuid().ToString("N");string path=Path.Combine("Plugins","LSPDFR","AdvancedK9","CalloutBridge.request");Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllLines(path,new[]{"Action="+action,"RequestId="+_calloutBridgeRequestId,"PedHandle="+HandleOf(Subject)});}catch(System.Exception ex){Game.LogTrivial("AdvancedK9 Callouts: custody bridge request contained: "+ex.Message);}
        }

        private void ReadCalloutBridgeState()
        {
            try{string path=Path.Combine("Plugins","LSPDFR","AdvancedK9","CalloutBridge.state");if(!File.Exists(path))return;var map=File.ReadAllLines(path).Select(line=>new{line,split=line.IndexOf('=')}).Where(x=>x.split>0).ToDictionary(x=>x.line.Substring(0,x.split),x=>x.line.Substring(x.split+1),StringComparer.OrdinalIgnoreCase);string handle,owner,stage;map.TryGetValue("ObservedPedHandle",out handle);if(handle!=HandleOf(Subject).ToString())return;map.TryGetValue("CustodyOwner",out owner);map.TryGetValue("CustodyStage",out stage);if(!string.IsNullOrWhiteSpace(owner)&&owner!="None")_custodyOwner=owner;if(stage=="InCustody"){_custodyLeaseActive=true;_suspectLifecycle=SuspectLifecycle.InCustody;_custodyOwnerSettleUntil=Game.GameTime;}map.TryGetValue("TransportStage",out stage);if(!string.IsNullOrWhiteSpace(stage)&&stage!="NotRequested")_transportStage=stage.ToLowerInvariant();}catch(System.Exception ex){Game.LogTrivial("AdvancedK9 Callouts: custody bridge state contained: "+ex.Message);}
        }

        private string DetectCustodyOwner()
        {
            ReadCalloutBridgeState();if(!string.IsNullOrWhiteSpace(_custodyOwner)&&_custodyOwner!="None"&&_custodyOwner!="Determining provider")return _custodyOwner;
            try
            {
                string path=Path.Combine("Plugins","LSPDFR","AdvancedK9","CompatibilityBridge.state");
                if(File.Exists(path))
                {
                    var map=File.ReadAllLines(path).Select(line=>new{line,split=line.IndexOf('=')}).Where(x=>x.split>0).ToDictionary(x=>x.line.Substring(0,x.split),x=>x.line.Substring(x.split+1),StringComparer.OrdinalIgnoreCase);
                    string handle,active;map.TryGetValue("ActivePedHandle",out handle);map.TryGetValue("PR",out active);
                    if(string.Equals(handle,HandleOf(Subject).ToString(),StringComparison.OrdinalIgnoreCase)&&string.Equals(active,"True",StringComparison.OrdinalIgnoreCase))return "Policing Redefined";
                    map.TryGetValue("STP",out active);if(string.Equals(handle,HandleOf(Subject).ToString(),StringComparison.OrdinalIgnoreCase)&&string.Equals(active,"True",StringComparison.OrdinalIgnoreCase))return "Stop The Ped";
                }
            }
            catch(System.Exception ex){Game.LogTrivial("AdvancedK9 Callouts: custody owner probe contained: "+ex.Message);}
            return "LSPDFR";
        }

        protected bool ConfirmedSubjectDeath()
        {
            if(Subject==null||!Subject.Exists())return false;
            bool raw=Subject.Health<=0&&NativeFunction.Natives.IS_PED_DEAD_OR_DYING<bool>(Subject,true);
            if(!raw){_deathObservedAt=0;return false;}
            if(_deathObservedAt==0)_deathObservedAt=Game.GameTime;
            if(_custodyLeaseActive||ArrestProviderOwnsSubject)return false;
            return Game.GameTime-_deathObservedAt>=5000;
        }

        protected void ObserveCooperativeControl(string phase)
        {
            if(Subject==null||!Subject.Exists())return;
            string state=Subject==null||!Subject.Exists()?"unavailable":UpdateCustodyLease()?"external-provider custody lease":Subject.IsDead?"deceased":SubjectIsComplying()?"NPCI/verbal compliance":phase;
            if(string.Equals(state,_lastCooperativeState,StringComparison.Ordinal))return;
            _lastCooperativeState=state;
            Game.LogTrivial("AdvancedK9 Callouts: cooperative control state -> "+state+"; callout retains lifecycle ownership without replacing approved suspect actions.");
        }

        protected void BeginSupportTrackingFiber()
        {
            if(_supportFiberStarted)return;_supportFiberStarted=true;
            GameFiber.StartNew(delegate
            {
                bool committed=false;uint expires=Game.GameTime+600000;
                try
                {
                    while(!Finished&&!_supportTrackingEnded&&Game.GameTime<expires)
                    {
                        if(!committed&&K9TrackingActive())
                        {
                            committed=true;
                            Game.DisplayNotification("~b~On-scene officers:~s~ Moving behind Rex and the handler.");
                            Game.LogTrivial("AdvancedK9 Callouts: dedicated support fiber detected K9 departure and committed both officers.");
                        }
                        if(committed)SupportOfficersFollowK9();
                        GameFiber.Wait(750);
                    }
                }
                catch(System.Exception ex){Game.LogTrivial("AdvancedK9 Callouts: support tracking fiber contained: "+ex);}
            },"AdvancedK9 callout support tracking");
        }

        protected void EndSupportTracking()
        {
            if(_supportTrackingEnded)return;
            _supportTrackingEnded=true;
            Game.LogTrivial("AdvancedK9 Callouts: search-follow controller stopped; later phases now own officer tasks.");
        }

        protected void SupportOfficersFollowK9()
        {
            _backupInvestigationActive=false;
            if(_supportTrackingEnded||_custodyLeaseActive||ArrestProviderOwnsSubject)return;
            if(Game.GameTime<_nextSupportMove)return;
            K9ApiSnapshot snapshot;
            Vector3 dogPosition=Game.LocalPlayer.Character.Position;
            if(AdvancedK9Api.TryGetSnapshot(out snapshot)&&snapshot.DogHandle>0)_cachedDogHandle=snapshot.DogHandle;
            if(_cachedDogHandle>0&&NativeFunction.Natives.DOES_ENTITY_EXIST<bool>(_cachedDogHandle))
                dogPosition=NativeFunction.Natives.GET_ENTITY_COORDS<Vector3>(_cachedDogHandle,true);
            _nextSupportMove=Game.GameTime+2200;
            var handler=Game.LocalPlayer.Character;
            uint taser=NativeFunction.Natives.GET_HASH_KEY<uint>("WEAPON_STUNGUN");
            uint pistol=NativeFunction.Natives.GET_HASH_KEY<uint>("WEAPON_COMBATPISTOL");
            if(OfficerOne!=null&&OfficerOne.Exists())
            {
                OfficerOne.BlockPermanentEvents=true;
                if(!_supportFormationLogged){NativeFunction.Natives.SET_PED_KEEP_TASK(OfficerOne,false);OfficerOne.Tasks.ClearImmediately();}
                if(!_supportWeaponsInitialized){NativeFunction.Natives.GIVE_WEAPON_TO_PED(OfficerOne,taser,2,false,true);NativeFunction.Natives.SET_CURRENT_PED_WEAPON(OfficerOne,taser,true);}
                NativeFunction.Natives.SET_PED_USING_ACTION_MODE(OfficerOne,true);
                NativeFunction.Natives.SET_PED_COMBAT_ATTRIBUTES(OfficerOne,1,true);
                NativeFunction.Natives.SET_PED_COMBAT_ABILITY(OfficerOne,2);
                NativeFunction.Natives.SET_PED_COMBAT_MOVEMENT(OfficerOne,2);
                float distance=OfficerOne.DistanceTo(handler);
                if(distance>28f){Vector3 catchup=handler.GetOffsetPosition(new Vector3(-3.2f,-9f,0f));Vector3 safe;if(TryResolveSafePedPosition(catchup,out safe))OfficerOne.Position=safe;}
                if(!_supportFormationLogged||distance>6.5f)
                {
                    NativeFunction.Natives.SET_PED_AS_GROUP_MEMBER(OfficerOne,NativeFunction.Natives.GET_PED_GROUP_INDEX<int>(handler));
                    NativeFunction.Natives.TASK_FOLLOW_TO_OFFSET_OF_ENTITY(OfficerOne,handler,-1.5f,-2.0f,0f,distance>25f?7.5f:5.2f,-1,2.5f,true);
                    NativeFunction.Natives.SET_PED_KEEP_TASK(OfficerOne,true);
                }
            }
            if(OfficerTwo!=null&&OfficerTwo.Exists())
            {
                OfficerTwo.BlockPermanentEvents=true;
                if(!_supportFormationLogged){NativeFunction.Natives.SET_PED_KEEP_TASK(OfficerTwo,false);OfficerTwo.Tasks.ClearImmediately();}
                if(!_supportWeaponsInitialized){NativeFunction.Natives.GIVE_WEAPON_TO_PED(OfficerTwo,pistol,60,false,true);NativeFunction.Natives.SET_CURRENT_PED_WEAPON(OfficerTwo,pistol,true);}
                NativeFunction.Natives.SET_PED_USING_ACTION_MODE(OfficerTwo,true);
                NativeFunction.Natives.SET_PED_COMBAT_ATTRIBUTES(OfficerTwo,1,true);
                NativeFunction.Natives.SET_PED_COMBAT_ABILITY(OfficerTwo,2);
                NativeFunction.Natives.SET_PED_COMBAT_MOVEMENT(OfficerTwo,2);
                float distance=OfficerTwo.DistanceTo(handler);
                if(distance>28f){Vector3 catchup=handler.GetOffsetPosition(new Vector3(3.2f,-11f,0f));Vector3 safe;if(TryResolveSafePedPosition(catchup,out safe))OfficerTwo.Position=safe;}
                if(!_supportFormationLogged||distance>6.5f)
                {
                    NativeFunction.Natives.SET_PED_AS_GROUP_MEMBER(OfficerTwo,NativeFunction.Natives.GET_PED_GROUP_INDEX<int>(handler));
                    NativeFunction.Natives.TASK_FOLLOW_TO_OFFSET_OF_ENTITY(OfficerTwo,handler,1.5f,-2.0f,0f,distance>25f?7.2f:5.0f,-1,2.5f,true);
                    NativeFunction.Natives.SET_PED_KEEP_TASK(OfficerTwo,true);
                }
            }
            if(!_supportFormationLogged){_supportFormationLogged=true;_supportWeaponsInitialized=true;Game.LogTrivial("AdvancedK9 Callouts: one-time tactical search formation and weapon roles assigned; K9 handle="+_cachedDogHandle+".");}
        }

        protected void ControlApprehensionTraffic(Vector3 center)
        {
            if(_secondaryTrafficControlled)return;
            _secondaryTrafficCenter=center;
            NativeFunction.Natives.SET_ROADS_IN_AREA(center.X-35f,center.Y-35f,center.Z-7f,center.X+35f,center.Y+35f,center.Z+7f,false,true);
            _secondaryTrafficControlled=true;
            Game.LogTrivial("AdvancedK9 Callouts: moving traffic exclusion established around the apprehension area.");
        }

        protected void SupportOfficersContainSubject()
        {
            if(_supportContainmentAssigned||_custodyLeaseActive||ArrestProviderOwnsSubject||Subject==null||!Subject.Exists())return;
            _supportContainmentAssigned=true;
            uint taser=NativeFunction.Natives.GET_HASH_KEY<uint>("WEAPON_STUNGUN");
            uint pistol=NativeFunction.Natives.GET_HASH_KEY<uint>("WEAPON_COMBATPISTOL");
            var handler=Game.LocalPlayer.Character;float dx=handler.Position.X-Subject.Position.X,dy=handler.Position.Y-Subject.Position.Y;
            float length=(float)Math.Sqrt(dx*dx+dy*dy);if(length<.1f){dx=0f;dy=-1f;length=1f;}dx/=length;dy/=length;
            Vector3 lessLethal=new Vector3(Subject.Position.X+dx*8f-dy*3.5f,Subject.Position.Y+dy*8f+dx*3.5f,Subject.Position.Z);
            Vector3 lethalCover=new Vector3(Subject.Position.X+dx*11f+dy*4.5f,Subject.Position.Y+dy*11f-dx*4.5f,Subject.Position.Z);
            if(OfficerOne!=null&&OfficerOne.Exists())
            {
                if(!_supportWeaponsInitialized)NativeFunction.Natives.GIVE_WEAPON_TO_PED(OfficerOne,taser,2,false,false);
                NativeFunction.Natives.SET_CURRENT_PED_WEAPON(OfficerOne,taser,true);
                OfficerOne.Tasks.ClearImmediately();
                NativeFunction.Natives.TASK_FOLLOW_NAV_MESH_TO_COORD(OfficerOne,lessLethal.X,lessLethal.Y,lessLethal.Z,3.5f,9000,1.5f,0,0f);
            }
            if(OfficerTwo!=null&&OfficerTwo.Exists())
            {
                if(!_supportWeaponsInitialized)NativeFunction.Natives.GIVE_WEAPON_TO_PED(OfficerTwo,pistol,60,false,false);
                NativeFunction.Natives.SET_CURRENT_PED_WEAPON(OfficerTwo,pistol,true);
                OfficerTwo.Tasks.ClearImmediately();
                NativeFunction.Natives.TASK_FOLLOW_NAV_MESH_TO_COORD(OfficerTwo,lethalCover.X,lethalCover.Y,lethalCover.Z,3.2f,9000,1.5f,0,0f);
            }
            var contactOfficer=OfficerOne;var coverOfficer=OfficerTwo;var containedSubject=Subject;
            GameFiber.StartNew(delegate
            {
                uint deadline=Game.GameTime+9000;
                while(!Finished&&!_custodyLeaseActive&&!ArrestProviderOwnsSubject&&containedSubject!=null&&containedSubject.Exists()&&Game.GameTime<deadline&&
                      ((contactOfficer!=null&&contactOfficer.Exists()&&contactOfficer.DistanceTo(lessLethal)>3f)||(coverOfficer!=null&&coverOfficer.Exists()&&coverOfficer.DistanceTo(lethalCover)>3f)))GameFiber.Wait(200);
                if(Finished||_custodyLeaseActive||ArrestProviderOwnsSubject||containedSubject==null||!containedSubject.Exists())return;
                if(contactOfficer!=null&&contactOfficer.Exists())NativeFunction.Natives.TASK_AIM_GUN_AT_ENTITY(contactOfficer,containedSubject,-1,false);
                if(coverOfficer!=null&&coverOfficer.Exists())NativeFunction.Natives.TASK_AIM_GUN_AT_ENTITY(coverOfficer,containedSubject,-1,false);
                Game.LogTrivial("AdvancedK9 Callouts: backup officers established one-time entity aim locks from separated containment positions.");
            },"AdvancedK9 tactical containment positions");
            if(!_supportContainmentLogged){_supportContainmentLogged=true;Game.LogTrivial("AdvancedK9 Callouts: support officers transitioned from search movement to persistent armed containment.");}
        }

        protected void MaintainSupportContainment(bool custody)
        {
            if(Subject==null||!Subject.Exists())return;
            if(!custody){SupportOfficersContainSubject();return;}
            ReleaseSupportForCustody();
        }

        protected void ReleaseSupportForCustody()
        {
            if(_supportCustodyGuardAssigned)return;
            _supportCustodyGuardAssigned=true;_supportTrackingEnded=true;_backupInvestigationActive=false;
            Ped[] officers={OfficerOne,OfficerTwo};
            foreach(Ped officer in officers)
            {
                if(officer==null||!officer.Exists())continue;
                NativeFunction.Natives.SET_PED_KEEP_TASK(officer,false);
                NativeFunction.Natives.SET_PED_USING_ACTION_MODE(officer,false);
                officer.Tasks.ClearImmediately();
                NativeFunction.Natives.TASK_STAND_STILL(officer,5000);
            }
            Game.LogTrivial("AdvancedK9 Callouts: custody/arrest interception cleared backup combat tasks once; weapon and movement reassignment is disabled.");
        }

        protected bool ValidateTrafficStopFormation(Vehicle stoppedVehicle,Vehicle cruiser,float expectedHeading)
        {
            if(stoppedVehicle==null||!stoppedVehicle.Exists()||cruiser==null||!cruiser.Exists())return false;
            float delta=Math.Abs(stoppedVehicle.Heading-cruiser.Heading);if(delta>180f)delta=360f-delta;
            float expectedDelta=Math.Abs(stoppedVehicle.Heading-expectedHeading);if(expectedDelta>180f)expectedDelta=360f-expectedDelta;
            float gap=stoppedVehicle.DistanceTo(cruiser);
            bool roadA=NativeFunction.Natives.IS_POINT_ON_ROAD<bool>(stoppedVehicle.Position.X,stoppedVehicle.Position.Y,stoppedVehicle.Position.Z,0);
            bool roadB=NativeFunction.Natives.IS_POINT_ON_ROAD<bool>(cruiser.Position.X,cruiser.Position.Y,cruiser.Position.Z,0);
            Vector3 expectedCruiser=stoppedVehicle.GetOffsetPosition(new Vector3(0f,-9f,0f));float laneOffset=cruiser.DistanceTo(expectedCruiser);Vector3 curbA,curbB;bool curbResolvedA=NativeFunction.Natives.GET_ROAD_BOUNDARY_USING_HEADING<bool>(stoppedVehicle.Position.X,stoppedVehicle.Position.Y,stoppedVehicle.Position.Z,expectedHeading,out curbA),curbResolvedB=NativeFunction.Natives.GET_ROAD_BOUNDARY_USING_HEADING<bool>(cruiser.Position.X,cruiser.Position.Y,cruiser.Position.Z,expectedHeading,out curbB);float curbDifference=curbResolvedA&&curbResolvedB?Math.Abs(curbA.DistanceTo(stoppedVehicle.Position)-curbB.DistanceTo(cruiser.Position)):99f;
            // GET_ROAD_BOUNDARY_USING_HEADING may select opposite boundaries for two
            // points in the same lane. Boundary availability is useful, but comparing
            // the returned distances is not a stable same-lane test. The explicit
            // behind-vehicle offset is the authoritative lateral/longitudinal check.
            bool valid=roadA&&roadB&&curbResolvedA&&curbResolvedB&&delta<=8f&&expectedDelta<=8f&&gap>=7f&&gap<=12f&&laneOffset<=1.5f;
            Game.LogTrivial("AdvancedK9 Callouts: traffic-stop formation audit: road="+roadA+"/"+roadB+", boundary="+curbResolvedA+"/"+curbResolvedB+", headingDelta="+delta+", expectedDelta="+expectedDelta+", gap="+gap+", laneOffset="+laneOffset+", diagnosticCurbDifference="+curbDifference+", valid="+valid+".");
            return valid;
        }

        protected bool TryFindExistingCover(Vector3 center,out Vector3 hidingPosition)
        {
            hidingPosition=center;
            try
            {
                Entity[] solidObjects=World.GetAllObjects().Where(o=>o!=null&&o.Exists()&&o.DistanceTo(center)<48f&&o.DistanceTo(Scene)>45f).Cast<Entity>().ToArray();
                Entity[] parkedVehicles=World.GetAllVehicles().Where(v=>v!=null&&v.Exists()&&v!=SceneVehicle&&v!=PoliceVehicle&&v.DistanceTo(center)<40f&&v.DistanceTo(Scene)>45f&&v.Speed<1.0f).Cast<Entity>().ToArray();
                foreach(Entity cover in solidObjects.Concat(parkedVehicles).OrderBy(o=>o.DistanceTo(center)))
                {
                    Vector3 candidate;
                    if(!TryBuildOccludedCoverCandidate(cover,out candidate))continue;
                    hidingPosition=candidate;
                    Game.LogTrivial("AdvancedK9 Callouts: solid environmental cover selected: "+(cover.Model.Name??"world geometry")+" at "+hidingPosition+".");
                    return true;
                }
                return false;
            }
            catch(System.Exception ex){Game.LogTrivial("AdvancedK9 Callouts: cover search contained: "+ex.Message);return false;}
        }

        protected bool TryFindWorldGeometryCover(Vector3 center,out Vector3 hidingPosition)
        {
            hidingPosition=Vector3.Zero;
            try
            {
                Entity roadObserver=SceneVehicle!=null&&SceneVehicle.Exists()?(Entity)SceneVehicle:
                    OfficerOne!=null&&OfficerOne.Exists()?(Entity)OfficerOne:null;
                Ped player=Game.LocalPlayer.Character;
                if(roadObserver==null||player==null||!player.Exists())return false;
                Ped[] streamedPeds=World.GetAllPeds();

                float[] radii={8f,12f,17f,23f,30f,38f,46f};
                int acceptedGroundPoints=0;
                int occludedFromRoad=0;
                for(int ring=0;ring<radii.Length;ring++)
                {
                    int samples=ring<2?12:18;
                    float phase=(ring%2)*10f;
                    for(int sample=0;sample<samples;sample++)
                    {
                        float degrees=phase+sample*(360f/samples);
                        float radians=(float)(degrees*Math.PI/180.0);
                        Vector3 requested=center+new Vector3((float)Math.Sin(radians)*radii[ring],(float)Math.Cos(radians)*radii[ring],0f);
                        Vector3 candidate=Vector3.Zero;
                        if(!TryResolveSafePedPosition(requested,out candidate)||candidate.DistanceTo(requested)>5f)continue;
                        if(candidate.DistanceTo(Scene)<45f||NativeFunction.Natives.IS_POINT_ON_ROAD<bool>(candidate.X,candidate.Y,candidate.Z,0))continue;
                        if(NativeFunction.Natives.GET_INTERIOR_AT_COORDS<int>(candidate.X,candidate.Y,candidate.Z)!=0)continue;
                        Vector3 candidatePosition=candidate;
                        if(streamedPeds.Any(p=>p!=null&&p.Exists()&&p!=Subject&&p!=OfficerOne&&p!=OfficerTwo&&p!=player&&p.DistanceTo(candidatePosition)<4.5f))continue;
                        acceptedGroundPoints++;

                        float[] heights={0.2f,0.7f,1.2f};
                        bool roadFullyOccluded=true;
                        for(int h=0;h<heights.Length;h++)
                        {
                            if(NativeFunction.Natives.HAS_ENTITY_CLEAR_LOS_TO_COORD<bool>(roadObserver,candidate.X,candidate.Y,candidate.Z+heights[h],17))
                            {
                                roadFullyOccluded=false;
                                break;
                            }
                        }
                        if(!roadFullyOccluded)continue;
                        occludedFromRoad++;

                        bool approachFullyOccluded=true;
                        for(int h=0;h<heights.Length;h++)
                        {
                            if(NativeFunction.Natives.HAS_ENTITY_CLEAR_LOS_TO_COORD<bool>(player,candidate.X,candidate.Y,candidate.Z+heights[h],17))
                            {
                                approachFullyOccluded=false;
                                break;
                            }
                        }
                        if(!approachFullyOccluded)continue;

                        hidingPosition=candidate;
                        Game.LogTrivial("AdvancedK9 Callouts: solid map-geometry concealment selected at "+candidate+"; road and approach occlusion passed at 0.2m, 0.7m, and 1.2m.");
                        return true;
                    }
                }
                Game.LogTrivial("AdvancedK9 Callouts: map-geometry cover scan found "+acceptedGroundPoints+" off-road ground candidates and "+occludedFromRoad+" road-occluded candidates, but none passed full approach occlusion.");
                return false;
            }
            catch(System.Exception ex)
            {
                Game.LogTrivial("AdvancedK9 Callouts: map-geometry cover scan contained: "+ex.Message);
                return false;
            }
        }

        private bool TryBuildOccludedCoverCandidate(Entity cover,out Vector3 candidate)
        {
            candidate=Vector3.Zero;
            if(cover==null||!cover.Exists())return false;
            string name=cover.Model.Name??"";string lower=name.ToLowerInvariant();
            if(lower.Contains("fnclink")||lower.Contains("fence")||lower.Contains("gate")||lower.Contains("bush")||lower.Contains("hedge")||lower.Contains("tree")||
               lower.Contains("sign")||lower.Contains("lamp")||lower.Contains("light")||lower.Contains("pole")||lower.Contains("glass"))return false;
            bool solidName=cover is Vehicle||lower.Contains("wall")||lower.Contains("pillar")||lower.Contains("column")||lower.Contains("barrier")||
                           lower.Contains("dumpster")||lower.Contains("crate")||lower.Contains("container")||lower.Contains("rock")||lower.Contains("building");
            if(!solidName)return false;
            Vector3 min,max;
            try{NativeFunction.Natives.GET_MODEL_DIMENSIONS(cover.Model.Hash,out min,out max);}catch{return false;}
            float width=max.X-min.X,depth=max.Y-min.Y,height=max.Z-min.Z;
            if(height<1.0f||Math.Max(width,depth)<1.4f)return false;
            Vector3 objectPosition=cover.Position;float dx=objectPosition.X-Scene.X,dy=objectPosition.Y-Scene.Y;float length=(float)Math.Sqrt(dx*dx+dy*dy);
            if(length<.1f)return false;dx/=length;dy/=length;
            float clearance=Math.Max(1.25f,Math.Min(2.6f,Math.Max(width,depth)*.55f+.6f));
            Vector3 requested=new Vector3(objectPosition.X+dx*clearance,objectPosition.Y+dy*clearance,objectPosition.Z);
            if(!TryResolveSafePedPosition(requested,out candidate))return false;
            if(candidate.DistanceTo(requested)>4f||NativeFunction.Natives.IS_POINT_ON_ROAD<bool>(candidate.X,candidate.Y,candidate.Z,0))return false;
            Vector3 candidatePosition=candidate;
            bool occupied=World.GetAllPeds().Any(p=>p!=null&&p.Exists()&&p!=Subject&&p!=OfficerOne&&p!=OfficerTwo&&p!=Game.LocalPlayer.Character&&p.DistanceTo(candidatePosition)<6f);
            if(occupied)return false;
            Entity observer=OfficerOne!=null&&OfficerOne.Exists()?(Entity)OfficerOne:SceneVehicle!=null&&SceneVehicle.Exists()?(Entity)SceneVehicle:null;
            if(observer==null)return false;
            bool clearAtCrouch=NativeFunction.Natives.HAS_ENTITY_CLEAR_LOS_TO_COORD<bool>(observer,candidate.X,candidate.Y,candidate.Z+0.8f,17);
            bool clearAtShoulder=NativeFunction.Natives.HAS_ENTITY_CLEAR_LOS_TO_COORD<bool>(observer,candidate.X,candidate.Y,candidate.Z+1.2f,17);
            if(clearAtCrouch||clearAtShoulder)return false;
            return true;
        }

        protected void BeginAutomaticTransport(string completionMessage)
        {
            if(Subject==null||!Subject.Exists())return;
            Vector3 spawn=World.GetNextPositionOnStreet(Subject.GetOffsetPosition(new Vector3(0f,-48f,0f)));
            if(spawn.DistanceTo(Subject)<25f||spawn.DistanceTo(Subject)>75f)spawn=World.GetNextPositionOnStreet(Subject.GetOffsetPosition(new Vector3(42f,-28f,0f)));
            Vehicle dedicated=SpawnPoliceVehicle(spawn,Subject.Heading);Ped dedicatedOfficer=SpawnPoliceOfficer(spawn,Subject.Heading);Ped dedicatedCover=SpawnPoliceOfficer(spawn,Subject.Heading);
            if(dedicated==null||!dedicated.Exists()||dedicatedOfficer==null||!dedicatedOfficer.Exists()){Game.LogTrivial("AdvancedK9 Callouts: dedicated fallback transport could not be staged; scene remains active.");return;}
            Game.DisplayNotification("~b~Dispatch:~s~ A dedicated prisoner transport unit is responding to the live arrest location.");
            DispatchUpdate("TransportRequested","On-scene patrol fallback","Current jurisdiction","Marked patrol transport","Restrained prisoner","Live arrest location","Medically cleared","The active custody provider did not produce transport. On-scene patrol is assuming prisoner transport.","",Subject.Position);
            var suspect=Subject;var officer=dedicatedOfficer;var coverOfficer=dedicatedCover;var transport=dedicated;
            GameFiber.StartNew(delegate
            {
                bool arrived=false,loaded=false;
                try
                {
                    if(officer!=null&&officer.Exists())
                    {
                        officer.Tasks.EnterVehicle(transport,-1).WaitForCompletion(12000);
                        if(!officer.IsInVehicle(transport,false))NativeFunction.Natives.SET_PED_INTO_VEHICLE(officer,transport,-1);
                        NativeFunction.Natives.TASK_VEHICLE_DRIVE_TO_COORD_LONGRANGE(officer,transport,suspect.Position.X,suspect.Position.Y,suspect.Position.Z,18f,786603,8f);
                        uint arrivalDeadline=Game.GameTime+22000;
                        while(Game.GameTime<arrivalDeadline&&transport.Exists()&&suspect.Exists()&&transport.DistanceTo(suspect)>12f)GameFiber.Wait(250);
                        arrived=transport.Exists()&&suspect.Exists()&&transport.DistanceTo(suspect)<=15f;
                        if(!arrived&&transport.Exists()&&officer.Exists())
                        {
                            Vector3 retry=World.GetNextPositionOnStreet(suspect.GetOffsetPosition(new Vector3(-28f,-38f,0f)));transport.Position=retry;NativeFunction.Natives.SET_VEHICLE_ON_GROUND_PROPERLY(transport);NativeFunction.Natives.SET_PED_INTO_VEHICLE(officer,transport,-1);NativeFunction.Natives.TASK_VEHICLE_DRIVE_TO_COORD_LONGRANGE(officer,transport,suspect.Position.X,suspect.Position.Y,suspect.Position.Z,16f,786603,7f);
                            uint retryDeadline=Game.GameTime+18000;while(Game.GameTime<retryDeadline&&transport.Exists()&&suspect.Exists()&&transport.DistanceTo(suspect)>12f)GameFiber.Wait(250);arrived=transport.Exists()&&suspect.Exists()&&transport.DistanceTo(suspect)<=15f;
                            Game.LogTrivial("AdvancedK9 Callouts: dedicated transport completed its single alternate-node retry; arrived="+arrived+".");
                        }
                        if(arrived)DispatchUpdate("TransportArrived","On-scene patrol fallback","Current jurisdiction","Marked patrol transport","Restrained prisoner","Live arrest location","Medically cleared","The prisoner transport unit has arrived at the custody location.","",suspect.Position);
                    }
                    if(arrived&&suspect.Exists()&&transport.Exists())
                    {
                        suspect.Tasks.EnterVehicle(transport,1).WaitForCompletion(10000);
                        if(!suspect.IsInVehicle(transport,false)&&transport.DistanceTo(suspect)<15f)NativeFunction.Natives.SET_PED_INTO_VEHICLE(suspect,transport,1);
                        loaded=suspect.IsInVehicle(transport,false);
                        if(loaded)DispatchUpdate("TransportLoaded","On-scene patrol fallback","Current jurisdiction","Marked patrol transport","Prisoner secured in transport","Departing arrest location","Medically cleared","The prisoner is secured in the transport vehicle.","",suspect.Position);
                    }
                    if(coverOfficer!=null&&coverOfficer.Exists()&&transport.Exists())
                    {
                        coverOfficer.Tasks.EnterVehicle(transport,0).WaitForCompletion(8000);
                        if(!coverOfficer.IsInVehicle(transport,false)&&transport.DistanceTo(coverOfficer)<15f)NativeFunction.Natives.SET_PED_INTO_VEHICLE(coverOfficer,transport,0);
                    }
                    if(loaded&&officer!=null&&officer.Exists()&&transport.Exists())NativeFunction.Natives.TASK_VEHICLE_DRIVE_WANDER(officer,transport,20f,786603);
                    if(loaded)GameFiber.Wait(1800);
                }
                catch(System.Exception ex){Game.LogTrivial("AdvancedK9 Callouts: automatic transport fallback contained: "+ex);}
                if(!loaded)
                {
                    DispatchUpdate("TransportFailed","On-scene patrol fallback","Current jurisdiction","Marked patrol transport","Restrained prisoner","Live arrest location","Medically cleared","Transport could not complete the prisoner loading sequence. The scene remains active for another provider request.","",suspect!=null&&suspect.Exists()?suspect.Position:Scene);
                    Game.LogTrivial("AdvancedK9 Callouts: transport fallback did not verify suspect loading; callout completion was blocked.");
                    return;
                }
                DispatchUpdate("TransportComplete","On-scene patrol fallback","Current jurisdiction","Marked patrol transport","Prisoner secured in transport","Departing jurisdiction","Medically cleared","Prisoner transport is complete and the unit is leaving the scene.","",transport.Position);
                Subject=null;
                Resolve(completionMessage);
                GameFiber.Wait(7000);
                if(suspect!=null&&suspect.Exists())suspect.Dismiss();
                if(officer!=null&&officer.Exists())officer.Dismiss();
                if(coverOfficer!=null&&coverOfficer.Exists())coverOfficer.Dismiss();
                if(transport!=null&&transport.Exists())transport.Dismiss();
            },"AdvancedK9 automatic prisoner transport");
        }

        protected bool RequestCustodyOwnerTransport()
        {
            if(Subject==null||!Subject.Exists()||!CustodyOwnerStable)return false;
            WriteCalloutBridgeRequest("RequestTransport");_transportStage="requested";_suspectLifecycle=SuspectLifecycle.AwaitingTransport;
            DispatchUpdate("TransportRequested","Provider custody transport",_incidentJurisdiction,"Provider transport unit",_incidentSuspect,"Live arrest location","Medically cleared",CustodyOwner+" has been asked to transport the prisoner from the live custody location.","",Subject.Position);
            return true;
        }

        protected bool ProviderTransportLoaded()
        {
            ReadCalloutBridgeState();if(Subject==null||!Subject.Exists())return false;bool loaded=NativeFunction.Natives.IS_PED_IN_ANY_VEHICLE<bool>(Subject,false)||_transportStage=="loaded";if(loaded)_suspectLifecycle=SuspectLifecycle.Transporting;return loaded;
        }

        protected bool ProcessPostApprehensionMedical(string completionMessage)
        {
            if(_medicalResponseStarted||Subject==null||!Subject.Exists()||Subject.IsDead)return _medicalResponseStarted;
            bool injured=Subject.Health<Subject.MaxHealth-5;
            if(!injured)return false;
            K9ApiSnapshot snapshot;
            if(AdvancedK9Api.TryGetSnapshot(out snapshot)&&(string.Equals(snapshot.State,"Apprehending",StringComparison.OrdinalIgnoreCase)||string.Equals(snapshot.State,"HoldingSuspect",StringComparison.OrdinalIgnoreCase)))return false;
            _medicalResponseStarted=true;
            _medicalStage="requested";
            var suspect=Subject;
            int[] existingPedHandles=World.GetAllPeds().Where(p=>p!=null&&p.Exists()).Select(p=>HandleOf(p)).ToArray();
            GameFiber.StartNew(delegate
            {
                try
                {
                    Vector3 downedPosition=suspect.Position;
                    RequestAmbulanceBackup(downedPosition);
                    DispatchUpdate("MedicalRequested","K9 apprehension injury","Current jurisdiction","EMS response","Injured restrained suspect","Live apprehension location","K9 bite injury", "K9 apprehension injury reported. EMS is responding Code 3 to the live suspect location.","ATTENTION_ALL_UNITS AMBULANCE_RESPOND_CODE_3",downedPosition);
                    Game.DisplayNotification("~b~Dispatch:~s~ K9 apprehension injury reported. EMS has been requested Code 3 to the suspect's live location.");
                    Game.LogTrivial("AdvancedK9 Callouts: requested LSPDFR ambulance response to live downed suspect position "+downedPosition+".");
                    Ped respondingMedic=null;uint responseDeadline=Game.GameTime+25000;
                    while(suspect.Exists()&&Game.GameTime<responseDeadline)
                    {
                        respondingMedic=FindRespondingMedic(suspect,existingPedHandles);
                        if(respondingMedic!=null)break;
                        GameFiber.Wait(1800);
                    }
                    if(!suspect.Exists())return;
                    if(respondingMedic==null)
                    {
                        Game.DisplayNotification("~o~Dispatch:~s~ EMS could not reach the live location. A monitored on-scene medical fallback is beginning.");
                        Vector3 fallbackPosition=suspect.GetOffsetPosition(new Vector3(1.8f,-1.8f,0f));Vector3 safeFallback;
                        if(TryResolveSafePedPosition(fallbackPosition,out safeFallback))respondingMedic=SpawnPed("s_m_m_paramedic_01",safeFallback,0f);
                        if(respondingMedic==null||!respondingMedic.Exists())
                        {
                            _medicalStage="failed";
                            DispatchUpdate("MedicalFailed","K9 apprehension injury","Current jurisdiction","EMS unavailable","Injured restrained suspect","Live apprehension location","Treatment not completed","EMS could not establish patient contact. The suspect has not been medically cleared.","",suspect.Position);
                            return;
                        }
                        Game.LogTrivial("AdvancedK9 Callouts: dedicated fallback medic created because the dispatched unit could not establish patient contact.");
                    }
                    respondingMedic.BlockPermanentEvents=true;
                    NativeFunction.Natives.TASK_FOLLOW_NAV_MESH_TO_COORD(respondingMedic,suspect.Position.X,suspect.Position.Y,suspect.Position.Z,2.2f,12000,1.4f,0,0f);
                    uint approachDeadline=Game.GameTime+12000;
                    while(respondingMedic.Exists()&&suspect.Exists()&&respondingMedic.DistanceTo(suspect)>2.2f&&Game.GameTime<approachDeadline)GameFiber.Wait(250);
                    if(respondingMedic!=null&&respondingMedic.Exists()&&suspect.Exists()&&respondingMedic.DistanceTo(suspect)>2.2f)
                    {
                        Vector3 closeContact=suspect.GetOffsetPosition(new Vector3(1.5f,-1.2f,0f));Vector3 safeContact;
                        if(TryResolveSafePedPosition(closeContact,out safeContact))respondingMedic.Position=safeContact;
                        GameFiber.Yield();
                        Game.LogTrivial("AdvancedK9 Callouts: EMS pathing timed out; medic was safely restaged at the live patient position.");
                    }
                    if(!respondingMedic.Exists()||!suspect.Exists()||respondingMedic.DistanceTo(suspect)>3.2f)
                    {
                        _medicalStage="failed";
                        DispatchUpdate("MedicalFailed","K9 apprehension injury","Current jurisdiction","EMS on scene","Injured restrained suspect","Live apprehension location","No patient contact","EMS arrived but did not reach the suspect. Medical clearance is withheld.","",suspect.Position);
                        return;
                    }
                    NativeFunction.Natives.TASK_TURN_PED_TO_FACE_ENTITY(respondingMedic,suspect,1000);
                    GameFiber.Wait(1000);
                    NativeFunction.Natives.TASK_START_SCENARIO_IN_PLACE(respondingMedic,"CODE_HUMAN_MEDIC_TEND_TO_DEAD",0,true);
                    _medicalStage="treating";
                    Game.DisplayNotification("~b~Dispatch:~s~ EMS is physically on scene and treating the suspect.");
                    DispatchUpdate("MedicalTreatmentStarted","K9 apprehension injury","Current jurisdiction","EMS on scene","Injured restrained suspect","Live apprehension location","Active treatment","A medic has established patient contact and begun treatment.","",suspect.Position);
                    uint treatmentUntil=Game.GameTime+6500;bool contactMaintained=true;
                    while(Game.GameTime<treatmentUntil&&respondingMedic.Exists()&&suspect.Exists())
                    {
                        if(respondingMedic.DistanceTo(suspect)>3f){contactMaintained=false;break;}
                        GameFiber.Wait(250);
                    }
                    if(!contactMaintained||!respondingMedic.Exists()||!suspect.Exists())
                    {
                        _medicalStage="failed";
                        DispatchUpdate("MedicalFailed","K9 apprehension injury","Current jurisdiction","EMS on scene","Injured restrained suspect","Live apprehension location","Treatment interrupted","Medic contact was interrupted. The suspect has not been medically cleared.","",suspect.Exists()?suspect.Position:downedPosition);
                        return;
                    }
                    bool serious=suspect.Health<=System.Math.Max(25,suspect.MaxHealth*45/100);
                    if(!ArrestProviderOwnsSubject&&!UpdateCustodyLease())suspect.Health=System.Math.Max(suspect.Health,serious?System.Math.Max(50,suspect.MaxHealth/2):System.Math.Max(75,suspect.MaxHealth*3/4));
                    if(serious)
                    {
                        Game.DisplayNotification("~o~Dispatch:~s~ EMS reports serious injuries. Request medical transport manually if hospital care is required.");
                        SeriousMedicalTransport=true;
                        _medicalStage="serious-treated";
                        MedicalResponseComplete=true;
                        DispatchUpdate("MedicalComplete","K9 apprehension injury","Current jurisdiction","EMS on scene","Seriously injured restrained suspect","Live apprehension location","Serious but treated","EMS completed on-scene stabilization. Any hospital or prisoner transport must be requested manually.","",suspect.Position);
                        return;
                    }
                    else
                    {
                        Game.DisplayNotification("~g~Dispatch:~s~ EMS confirms treatment complete. On-scene patrol is cleared to transport the prisoner.");
                        _medicalStage="complete";
                        MedicalResponseComplete=true;
                        DispatchUpdate("MedicalComplete","K9 apprehension injury","Current jurisdiction","EMS on scene","Injured restrained suspect","Live apprehension location","Stable", "EMS confirms treatment complete. The prisoner is medically cleared for transport.","",suspect.Position);
                        return;
                    }
                }
                catch(System.Exception ex){Game.LogTrivial("AdvancedK9 Callouts: live-position EMS response contained: "+ex);}
                if(!MedicalResponseComplete&&_medicalStage!="failed")_medicalResponseStarted=false;
            },"AdvancedK9 live-position EMS response");
            return true;
        }

        private Ped FindRespondingMedic(Ped suspect,int[] existingPedHandles)
        {
            Vehicle ambulance=World.GetAllVehicles().Where(v=>v!=null&&v.Exists()&&string.Equals(v.Model.Name,"ambulance",StringComparison.OrdinalIgnoreCase)&&v.DistanceTo(suspect)<55f).OrderBy(v=>v.DistanceTo(suspect)).FirstOrDefault();
            foreach(Ped ped in World.GetAllPeds().Where(p=>p!=null&&p.Exists()&&p!=suspect&&p.DistanceTo(suspect)<35f))
            {
                string model=(ped.Model.Name??"").ToLowerInvariant();int handle=HandleOf(ped);
                bool knownMedic=model.Contains("paramedic")||model.Contains("medic")||model.Contains("lsfd");
                bool newResponder=!existingPedHandles.Contains(handle)&&ambulance!=null&&ped.DistanceTo(ambulance)<22f;
                if(knownMedic||newResponder)return ped;
            }
            return null;
        }

        private static void RequestAmbulanceBackup(Vector3 position)
        {
            try
            {
                var methods=typeof(Functions).GetMethods(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Static);
                foreach(var method in methods)
                {
                    if(!string.Equals(method.Name,"RequestBackup",StringComparison.Ordinal)||method.GetParameters().Length!=3)continue;
                    var parameters=method.GetParameters();
                    if(parameters[0].ParameterType!=typeof(Vector3)||!parameters[1].ParameterType.IsEnum||!parameters[2].ParameterType.IsEnum)continue;
                    object response=System.Enum.Parse(parameters[1].ParameterType,"Code3",true);
                    object unit=System.Enum.Parse(parameters[2].ParameterType,"Ambulance",true);
                    method.Invoke(null,new object[]{position,response,unit});
                    Game.LogTrivial("AdvancedK9 Callouts: LSPDFR ambulance backup request accepted through the installed API.");
                    return;
                }
                Game.LogTrivial("AdvancedK9 Callouts: installed LSPDFR API exposes no compatible ambulance backup request; scanner dispatch remains active.");
            }
            catch(System.Exception ex){Game.LogTrivial("AdvancedK9 Callouts: ambulance backup request fallback contained: "+ex.Message);}
        }

        private static Vector3 NearestHospital(Vector3 position)
        {
            Vector3[] hospitals={new Vector3(295f,-1446f,29f),new Vector3(360f,-585f,28f),new Vector3(-676f,310f,83f),new Vector3(1839f,3672f,34f),new Vector3(-247f,6331f,32f)};
            Vector3 nearest=hospitals[0];float best=position.DistanceTo(nearest);
            for(int i=1;i<hospitals.Length;i++){float distance=position.DistanceTo(hospitals[i]);if(distance<best){best=distance;nearest=hospitals[i];}}
            return nearest;
        }

        protected void RequestK9(string command,Ped target,string details)
        {
            if(ApiRequested||!K9Available())return;ApiRequested=true;
            AdvancedK9Api.SendCommand(command,ContextId,HandleOf(target),"Ped",target==null?0:target.Position.X,target==null?0:target.Position.Y,target==null?0:target.Position.Z,details);
            Game.DisplayNotification("~b~AdvancedK9 callout:~s~ "+command+" request sent to the active K9 team.");
        }

        protected void Resolve(string message)
        {
            if(Finished)return;Finished=true;Game.DisplayNotification(message);End();
        }

        private void BeginPoliceSceneDeparture()
        {
            var officerOne=OfficerOne;var officerTwo=OfficerTwo;var cruiser=PoliceVehicle;
            OfficerOne=null;OfficerTwo=null;PoliceVehicle=null;
            if(cruiser==null||!cruiser.Exists())
            {
                if(officerOne!=null&&officerOne.Exists())officerOne.Dismiss();
                if(officerTwo!=null&&officerTwo.Exists())officerTwo.Dismiss();
                return;
            }
            GameFiber.StartNew(delegate
            {
                try
                {
                    if(officerOne!=null&&officerOne.Exists()){NativeFunction.Natives.REMOVE_PED_FROM_GROUP(officerOne);officerOne.BlockPermanentEvents=false;officerOne.Tasks.Clear();officerOne.Tasks.EnterVehicle(cruiser,0);}
                    if(officerTwo!=null&&officerTwo.Exists()){NativeFunction.Natives.REMOVE_PED_FROM_GROUP(officerTwo);officerTwo.BlockPermanentEvents=false;officerTwo.Tasks.Clear();officerTwo.Tasks.EnterVehicle(cruiser,-1);}
                    uint entryDeadline=Game.GameTime+8000;
                    while(Game.GameTime<entryDeadline&&cruiser.Exists()&&
                        ((officerOne!=null&&officerOne.Exists()&&!officerOne.IsInVehicle(cruiser,false))||(officerTwo!=null&&officerTwo.Exists()&&!officerTwo.IsInVehicle(cruiser,false))))GameFiber.Yield();
                    if(officerOne!=null&&officerOne.Exists()&&!officerOne.IsInVehicle(cruiser,false))NativeFunction.Natives.SET_PED_INTO_VEHICLE(officerOne,cruiser,0);
                    if(officerTwo!=null&&officerTwo.Exists()&&!officerTwo.IsInVehicle(cruiser,false))NativeFunction.Natives.SET_PED_INTO_VEHICLE(officerTwo,cruiser,-1);
                    Ped driver=officerTwo!=null&&officerTwo.Exists()?officerTwo:officerOne;
                    if(driver!=null&&driver.Exists()&&cruiser.Exists())
                    {
                        NativeFunction.Natives.SET_VEHICLE_SIREN(cruiser,false);
                        NativeFunction.Natives.TASK_VEHICLE_DRIVE_WANDER(driver,cruiser,18f,786603);
                        Game.LogTrivial("AdvancedK9 Callouts: both scene officers secured in their cruiser and began the staged drive-away.");
                        GameFiber.Wait(7000);
                    }
                }
                catch(System.Exception ex){Game.LogTrivial("AdvancedK9 Callouts: police departure fallback contained: "+ex);}
                if(officerOne!=null&&officerOne.Exists())officerOne.Dismiss();
                if(officerTwo!=null&&officerTwo.Exists())officerTwo.Dismiss();
                if(cruiser.Exists())cruiser.Dismiss();
            },"AdvancedK9 police scene departure");
        }

        private void PreserveSceneVehicleForReturn(Vehicle vehicle)
        {
            if(vehicle==null||!vehicle.Exists())return;vehicle.IsPersistent=true;
            GameFiber.StartNew(delegate
            {
                try
                {
                    uint expires=Game.GameTime+600000;bool playerReturned=false;
                    while(vehicle.Exists()&&Game.GameTime<expires)
                    {
                        float distance=Game.LocalPlayer.Character.DistanceTo(vehicle);
                        if(distance<45f)playerReturned=true;
                        if(playerReturned&&distance>120f)break;
                        GameFiber.Wait(1000);
                    }
                }
                catch(System.Exception ex){Game.LogTrivial("AdvancedK9 Callouts: preserved scene vehicle cleanup contained: "+ex.Message);}
                if(vehicle.Exists())vehicle.Dismiss();
            },"AdvancedK9 preserved scene vehicle");
            Game.LogTrivial("AdvancedK9 Callouts: abandoned vehicle preserved for scene return; cleanup waits until the handler returns and leaves or ten minutes pass.");
        }

        public override void End()
        {
            if(_cleanupCompleted)return;
            bool completedBeforeCleanup=Finished;
            _lifecycleClosed=true;
            _lifecycleGeneration++;
            Finished=true;
            _backupInvestigationActive=false;
            _cleanupCompleted=true;
            ClearSceneRoute();
            if(SubjectBlip!=null&&SubjectBlip.Exists())SubjectBlip.Delete();
            if(Reporter!=null&&Reporter.Exists())Reporter.Dismiss();
            if(ParentTwo!=null&&ParentTwo.Exists())ParentTwo.Dismiss();
            if(Subject!=null&&Subject.Exists())Subject.Dismiss();
            if(SceneVehicle!=null&&SceneVehicle.Exists()){if(completedBeforeCleanup)PreserveSceneVehicleForReturn(SceneVehicle);else SceneVehicle.Dismiss();}SceneVehicle=null;
            if(EvidenceProp!=null&&EvidenceProp.Exists())EvidenceProp.Dismiss();
            if(CoverProp!=null&&CoverProp.Exists())CoverProp.Dismiss();
            if(MedicOne!=null&&MedicOne.Exists())MedicOne.Dismiss();
            if(MedicTwo!=null&&MedicTwo.Exists())MedicTwo.Dismiss();
            if(MedicalVehicle!=null&&MedicalVehicle.Exists())MedicalVehicle.Dismiss();
            BeginPoliceSceneDeparture();
            try
            {
                if(_trafficControlled)NativeFunction.Natives.SET_ROADS_IN_AREA(Scene.X-32f,Scene.Y-32f,Scene.Z-8f,Scene.X+32f,Scene.Y+32f,Scene.Z+8f,true,true);
                if(_secondaryTrafficControlled){var center=_secondaryTrafficCenter;NativeFunction.Natives.SET_ROADS_IN_AREA(center.X-22f,center.Y-22f,center.Z-7f,center.X+22f,center.Y+22f,center.Z+7f,true,true);}
            }
            catch(System.Exception ex){Game.LogTrivial("AdvancedK9 Callouts: traffic restoration contained: "+ex.Message);}
            finally{_trafficControlled=false;_secondaryTrafficControlled=false;}
            if(!string.IsNullOrWhiteSpace(ContextId))AdvancedK9Api.SendCommand("ClearEvidenceMarkers",ContextId,0,"Scene",Scene.X,Scene.Y,Scene.Z,"callout scene cleared");
            Game.LogTrivial("AdvancedK9 Callouts: cleared scene and evidence markers for "+GetType().Name+".");
            base.End();
        }

        public override void OnCalloutNotAccepted(){End();}
    }
}
