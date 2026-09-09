using System;
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
        private bool _healthyCustodyProtection;
        private uint _nextEmergencyLightRefresh;
        protected bool MedicalResponseStarted{get{return _medicalResponseStarted;}}
        protected bool MedicalResponseComplete;
        protected bool SeriousMedicalTransport;

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

        protected Vehicle SpawnVehicle(string modelName,Vector3 position,float heading)
        {
            var model=new Model(modelName);if(!model.IsValid)return null;model.LoadAndWait();var vehicle=new Vehicle(model,position,heading);model.Dismiss();
            if(vehicle!=null&&vehicle.Exists())vehicle.IsPersistent=true;return vehicle;
        }

        protected Rage.Object SpawnProp(string modelName,Vector3 position)
        {
            var model=new Model(modelName);if(!model.IsValid)return null;model.LoadAndWait();var prop=new Rage.Object(model,position);model.Dismiss();
            if(prop!=null&&prop.Exists()){prop.IsPersistent=true;NativeFunction.Natives.PLACE_OBJECT_ON_GROUND_PROPERLY(prop);}return prop;
        }

        private Vehicle SpawnPoliceVehicle(Vector3 position,float heading)
        {
            string[] models={"police3","police","sheriff"};
            foreach(string model in models)
            {
                var vehicle=SpawnVehicle(model,position,heading);
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

        protected bool StagePoliceScene(Vector3 cruiserPosition,float heading)
        {
            Vector3 officerOnePosition=new Vector3(Scene.X-2.5f,Scene.Y-2f,Scene.Z);
            Vector3 officerTwoPosition=new Vector3(Scene.X+2.5f,Scene.Y-2f,Scene.Z);
            if(PoliceVehicle==null||!PoliceVehicle.Exists())PoliceVehicle=SpawnPoliceVehicle(cruiserPosition,heading);
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
            Game.DisplayNotification("~b~Dispatch:~s~ "+message);
            PublishCalloutInterfaceMessage(message);
            if(!string.IsNullOrWhiteSpace(scannerAudio))
            {
                try{Functions.PlayScannerAudioUsingPosition(scannerAudio,position);}
                catch(System.Exception ex){Game.LogTrivial("AdvancedK9 Callouts: scanner update contained: "+ex.Message);}
            }
            Game.LogTrivial("AdvancedK9 Callouts: dispatch incident update ["+ContextId+"]: "+message);
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
            NativeFunction.Natives.SET_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME(0f);
            NativeFunction.Natives.SET_RANDOM_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME(0f);
            NativeFunction.Natives.SET_PARKED_VEHICLE_DENSITY_MULTIPLIER_THIS_FRAME(0f);
            try
            {
                Vehicle playerVehicle=Game.LocalPlayer.Character.CurrentVehicle;
                foreach(Vehicle vehicle in World.GetAllVehicles())
                {
                    if(vehicle==null||!vehicle.Exists()||vehicle.DistanceTo(center)>radius||vehicle==SceneVehicle||vehicle==PoliceVehicle||vehicle==playerVehicle)continue;
                    Ped driver=vehicle.Driver;
                    if(driver==null||!driver.Exists())continue;
                    NativeFunction.Natives.TASK_VEHICLE_TEMP_ACTION(driver,vehicle,6,2500);
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
                if(NativeFunction.Natives.IS_PED_CUFFED<bool>(Subject)||NativeFunction.Natives.IS_PED_BEING_ARRESTED<bool>(Subject)||NativeFunction.Natives.IS_PED_HANDCUFFED<bool>(Subject))return true;
                foreach(string name in new[]{"IsPedGettingArrested","IsPedArrested"})
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
            if(Subject==null||!Subject.Exists()||Subject.IsDead)return false;
            bool active=SubjectIsInCustody();
            if(active&&!_custodyLeaseActive)
            {
                _custodyLeaseActive=true;_custodyLeaseStarted=Game.GameTime;
                bool healthy=Subject.Health>System.Math.Max(100,Subject.MaxHealth*3/4)&&!Subject.IsRagdoll;
                Game.LogTrivial("AdvancedK9 Callouts: custody lease acquired; health="+Subject.Health+"/"+Subject.MaxHealth+", dead="+Subject.IsDead+", healthyProtection="+healthy+".");
                if(healthy)
                {
                    _healthyCustodyProtection=true;Subject.IsInvincible=true;
                    var protectedSubject=Subject;
                    GameFiber.StartNew(delegate
                    {
                        GameFiber.Wait(8000);
                        if(protectedSubject!=null&&protectedSubject.Exists()&&!protectedSubject.IsDead)protectedSubject.IsInvincible=false;
                        _healthyCustodyProtection=false;
                        Game.LogTrivial("AdvancedK9 Callouts: healthy custody handoff protection released after LSPDFR/PR transition window.");
                    },"AdvancedK9 custody handoff protection");
                }
                Game.LogTrivial("AdvancedK9 Callouts: AdvancedK9 suspect tasking suspended for the external arrest provider.");
            }
            return active||(_custodyLeaseActive&&Game.GameTime-_custodyLeaseStarted<15000);
        }

        protected void ObserveCooperativeControl(string phase)
        {
            if(Subject==null||!Subject.Exists())return;
            string state=Subject==null||!Subject.Exists()?"unavailable":Subject.IsDead?"deceased":UpdateCustodyLease()?"external-provider custody lease":SubjectIsComplying()?"NPCI/verbal compliance":phase;
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
            if(_supportTrackingEnded)return;
            if(Game.GameTime<_nextSupportMove)return;
            K9ApiSnapshot snapshot;
            Vector3 dogPosition=Game.LocalPlayer.Character.Position;
            if(AdvancedK9Api.TryGetSnapshot(out snapshot)&&snapshot.DogHandle>0)_cachedDogHandle=snapshot.DogHandle;
            if(_cachedDogHandle>0&&NativeFunction.Natives.DOES_ENTITY_EXIST<bool>(_cachedDogHandle))
                dogPosition=NativeFunction.Natives.GET_ENTITY_COORDS<Vector3>(_cachedDogHandle,true);
            _nextSupportMove=Game.GameTime+1500;
            var handler=Game.LocalPlayer.Character;
            uint taser=NativeFunction.Natives.GET_HASH_KEY<uint>("WEAPON_STUNGUN");
            uint pistol=NativeFunction.Natives.GET_HASH_KEY<uint>("WEAPON_COMBATPISTOL");
            if(OfficerOne!=null&&OfficerOne.Exists())
            {
                OfficerOne.BlockPermanentEvents=true;
                OfficerOne.Tasks.Clear();
                NativeFunction.Natives.GIVE_WEAPON_TO_PED(OfficerOne,taser,2,false,true);
                NativeFunction.Natives.SET_CURRENT_PED_WEAPON(OfficerOne,taser,true);
                NativeFunction.Natives.SET_PED_COMBAT_ABILITY(OfficerOne,2);
                NativeFunction.Natives.SET_PED_COMBAT_MOVEMENT(OfficerOne,2);
                float distance=OfficerOne.DistanceTo(handler);
                NativeFunction.Natives.SET_PED_AS_GROUP_MEMBER(OfficerOne,NativeFunction.Natives.GET_PED_GROUP_INDEX<int>(handler));
                NativeFunction.Natives.TASK_FOLLOW_TO_OFFSET_OF_ENTITY(OfficerOne,handler,-3.2f,-7.5f,0f,distance>25f?7.5f:5.8f,-1,3.5f,true);
                NativeFunction.Natives.SET_PED_KEEP_TASK(OfficerOne,true);
            }
            if(OfficerTwo!=null&&OfficerTwo.Exists())
            {
                OfficerTwo.BlockPermanentEvents=true;
                OfficerTwo.Tasks.Clear();
                NativeFunction.Natives.GIVE_WEAPON_TO_PED(OfficerTwo,pistol,60,false,true);
                NativeFunction.Natives.SET_CURRENT_PED_WEAPON(OfficerTwo,pistol,true);
                NativeFunction.Natives.SET_PED_COMBAT_ABILITY(OfficerTwo,2);
                NativeFunction.Natives.SET_PED_COMBAT_MOVEMENT(OfficerTwo,2);
                float distance=OfficerTwo.DistanceTo(handler);
                NativeFunction.Natives.SET_PED_AS_GROUP_MEMBER(OfficerTwo,NativeFunction.Natives.GET_PED_GROUP_INDEX<int>(handler));
                NativeFunction.Natives.TASK_FOLLOW_TO_OFFSET_OF_ENTITY(OfficerTwo,handler,3.2f,-9f,0f,distance>25f?7.2f:5.6f,-1,3.8f,true);
                NativeFunction.Natives.SET_PED_KEEP_TASK(OfficerTwo,true);
            }
            if(!_supportFormationLogged){_supportFormationLogged=true;Game.LogTrivial("AdvancedK9 Callouts: support search formation assigned behind the K9 handler; K9 handle="+_cachedDogHandle+".");}
        }

        protected void ControlApprehensionTraffic(Vector3 center)
        {
            if(_secondaryTrafficControlled)return;
            _secondaryTrafficCenter=center;
            NativeFunction.Natives.SET_ROADS_IN_AREA(center.X-38f,center.Y-38f,center.Z-10f,center.X+38f,center.Y+38f,center.Z+10f,false,true);
            _secondaryTrafficControlled=true;
            Game.LogTrivial("AdvancedK9 Callouts: moving traffic exclusion established around the apprehension area.");
        }

        protected void SupportOfficersContainSubject()
        {
            if(Subject==null||!Subject.Exists())return;
            uint taser=NativeFunction.Natives.GET_HASH_KEY<uint>("WEAPON_STUNGUN");
            uint pistol=NativeFunction.Natives.GET_HASH_KEY<uint>("WEAPON_COMBATPISTOL");
            var handler=Game.LocalPlayer.Character;float dx=handler.Position.X-Subject.Position.X,dy=handler.Position.Y-Subject.Position.Y;
            float length=(float)Math.Sqrt(dx*dx+dy*dy);if(length<.1f){dx=0f;dy=-1f;length=1f;}dx/=length;dy/=length;
            Vector3 lessLethal=new Vector3(Subject.Position.X+dx*8f-dy*3.5f,Subject.Position.Y+dy*8f+dx*3.5f,Subject.Position.Z);
            Vector3 lethalCover=new Vector3(Subject.Position.X+dx*11f+dy*4.5f,Subject.Position.Y+dy*11f-dx*4.5f,Subject.Position.Z);
            if(OfficerOne!=null&&OfficerOne.Exists())
            {
                NativeFunction.Natives.GIVE_WEAPON_TO_PED(OfficerOne,taser,2,false,true);
                NativeFunction.Natives.SET_CURRENT_PED_WEAPON(OfficerOne,taser,true);
                if(OfficerOne.DistanceTo(lessLethal)>2.5f)NativeFunction.Natives.TASK_FOLLOW_NAV_MESH_TO_COORD(OfficerOne,lessLethal.X,lessLethal.Y,lessLethal.Z,3.5f,7000,1.5f,0,0f);
                else NativeFunction.Natives.TASK_AIM_GUN_AT_ENTITY(OfficerOne,Subject,-1,false);
            }
            if(OfficerTwo!=null&&OfficerTwo.Exists())
            {
                NativeFunction.Natives.GIVE_WEAPON_TO_PED(OfficerTwo,pistol,60,false,true);
                NativeFunction.Natives.SET_CURRENT_PED_WEAPON(OfficerTwo,pistol,true);
                if(OfficerTwo.DistanceTo(lethalCover)>2.5f)NativeFunction.Natives.TASK_FOLLOW_NAV_MESH_TO_COORD(OfficerTwo,lethalCover.X,lethalCover.Y,lethalCover.Z,3.2f,7000,1.5f,0,0f);
                else NativeFunction.Natives.TASK_AIM_GUN_AT_ENTITY(OfficerTwo,Subject,-1,false);
            }
            Game.LogTrivial("AdvancedK9 Callouts: support officers transitioned from search movement to armed containment.");
        }

        protected bool TryFindExistingCover(Vector3 center,out Vector3 hidingPosition)
        {
            hidingPosition=center;
            try
            {
                var cover=World.GetAllObjects().Where(o=>o.Exists()&&o.DistanceTo(center)<28f&&o.DistanceTo(Scene)>45f).OrderBy(o=>o.DistanceTo(center)).FirstOrDefault(o=>{
                    string name=(o.Model.Name??"").ToLowerInvariant();
                    return name.Contains("bush")||name.Contains("hedge")||name.Contains("tree")||name.Contains("planter")||name.Contains("pillar")||name.Contains("column")||name.Contains("dumpster")||name.Contains("wall")||name.Contains("fence")||name.Contains("crate")||name.Contains("container");
                });
                if(cover==null||!cover.Exists())return false;
                Vector3 objectPosition=cover.Position;float dx=objectPosition.X-Scene.X,dy=objectPosition.Y-Scene.Y;float length=(float)Math.Sqrt(dx*dx+dy*dy);
                if(length<.1f){dx=1f;dy=0f;length=1f;}
                hidingPosition=new Vector3(objectPosition.X+dx/length*1.4f,objectPosition.Y+dy/length*1.4f,objectPosition.Z);
                if(NativeFunction.Natives.IS_POINT_ON_ROAD<bool>(hidingPosition.X,hidingPosition.Y,hidingPosition.Z,0))return false;
                Game.LogTrivial("AdvancedK9 Callouts: existing environmental cover selected: "+cover.Model.Name+" at "+hidingPosition+".");
                return true;
            }
            catch(System.Exception ex){Game.LogTrivial("AdvancedK9 Callouts: cover search contained: "+ex.Message);return false;}
        }

        protected void BeginAutomaticTransport(string completionMessage)
        {
            if(Subject==null||!Subject.Exists()||PoliceVehicle==null||!PoliceVehicle.Exists()){Resolve(completionMessage);return;}
            Game.DisplayNotification("~b~Dispatch:~s~ On-scene patrol is assuming prisoner transport. The cruiser is responding to the arrest location.");
            var suspect=Subject;var officer=OfficerTwo!=null&&OfficerTwo.Exists()?OfficerTwo:OfficerOne;
            var coverOfficer=officer==OfficerTwo?OfficerOne:OfficerTwo;var transport=PoliceVehicle;
            GameFiber.StartNew(delegate
            {
                try
                {
                    if(officer!=null&&officer.Exists())
                    {
                        officer.Tasks.EnterVehicle(transport,-1).WaitForCompletion(12000);
                        if(!officer.IsInVehicle(transport,false))NativeFunction.Natives.SET_PED_INTO_VEHICLE(officer,transport,-1);
                        NativeFunction.Natives.TASK_VEHICLE_DRIVE_TO_COORD_LONGRANGE(officer,transport,suspect.Position.X,suspect.Position.Y,suspect.Position.Z,18f,786603,8f);
                        uint arrivalDeadline=Game.GameTime+20000;
                        while(Game.GameTime<arrivalDeadline&&transport.Exists()&&suspect.Exists()&&transport.DistanceTo(suspect)>12f)GameFiber.Wait(250);
                    }
                    if(suspect.Exists()&&transport.Exists())
                    {
                        suspect.Tasks.EnterVehicle(transport,1).WaitForCompletion(10000);
                        if(!suspect.IsInVehicle(transport,false)&&transport.DistanceTo(suspect)<15f)NativeFunction.Natives.SET_PED_INTO_VEHICLE(suspect,transport,1);
                    }
                    if(coverOfficer!=null&&coverOfficer.Exists()&&transport.Exists())
                    {
                        coverOfficer.Tasks.EnterVehicle(transport,0).WaitForCompletion(8000);
                        if(!coverOfficer.IsInVehicle(transport,false)&&transport.DistanceTo(coverOfficer)<15f)NativeFunction.Natives.SET_PED_INTO_VEHICLE(coverOfficer,transport,0);
                    }
                    if(officer!=null&&officer.Exists()&&transport.Exists())NativeFunction.Natives.TASK_VEHICLE_DRIVE_WANDER(officer,transport,20f,786603);
                    GameFiber.Wait(1800);
                }
                catch(System.Exception ex){Game.LogTrivial("AdvancedK9 Callouts: automatic transport fallback contained: "+ex);}
                OfficerOne=null;OfficerTwo=null;PoliceVehicle=null;Subject=null;
                Resolve(completionMessage);
                GameFiber.Wait(7000);
                if(suspect!=null&&suspect.Exists())suspect.Dismiss();
                if(officer!=null&&officer.Exists())officer.Dismiss();
                if(coverOfficer!=null&&coverOfficer.Exists())coverOfficer.Dismiss();
                if(transport!=null&&transport.Exists())transport.Dismiss();
            },"AdvancedK9 automatic prisoner transport");
        }

        protected bool ProcessPostApprehensionMedical(string completionMessage)
        {
            if(_medicalResponseStarted||Subject==null||!Subject.Exists()||Subject.IsDead)return _medicalResponseStarted;
            bool injured=Subject.Health<Subject.MaxHealth-5||Subject.IsRagdoll;
            if(!injured)return false;
            K9ApiSnapshot snapshot;
            if(AdvancedK9Api.TryGetSnapshot(out snapshot)&&string.Equals(snapshot.State,"Apprehending",StringComparison.OrdinalIgnoreCase))return false;
            _medicalResponseStarted=true;
            var suspect=Subject;
            int[] existingPedHandles=World.GetAllPeds().Where(p=>p!=null&&p.Exists()).Select(p=>HandleOf(p)).ToArray();
            GameFiber.StartNew(delegate
            {
                try
                {
                    Vector3 downedPosition=suspect.Position;
                    RequestAmbulanceBackup(downedPosition);
                    Functions.PlayScannerAudioUsingPosition("ATTENTION_ALL_UNITS AMBULANCE_RESPOND_CODE_3",downedPosition);
                    Game.DisplayNotification("~b~Dispatch:~s~ K9 apprehension injury reported. EMS has been requested Code 3 to the suspect's live location.");
                    Game.LogTrivial("AdvancedK9 Callouts: requested LSPDFR ambulance response to live downed suspect position "+downedPosition+".");
                    Ped respondingMedic=null;uint responseDeadline=Game.GameTime+60000;
                    while(suspect.Exists()&&Game.GameTime<responseDeadline)
                    {
                        respondingMedic=FindRespondingMedic(suspect,existingPedHandles);
                        if(respondingMedic!=null)break;
                        GameFiber.Wait(1800);
                    }
                    if(!suspect.Exists())return;
                    if(respondingMedic==null)
                    {
                        Game.DisplayNotification("~o~Dispatch:~s~ EMS has not reached the suspect yet. Medical treatment remains pending.");
                        return;
                    }
                    respondingMedic.BlockPermanentEvents=true;
                    NativeFunction.Natives.TASK_FOLLOW_NAV_MESH_TO_COORD(respondingMedic,suspect.Position.X,suspect.Position.Y,suspect.Position.Z,2.2f,12000,1.4f,0,0f);
                    uint approachDeadline=Game.GameTime+12000;
                    while(respondingMedic.Exists()&&suspect.Exists()&&respondingMedic.DistanceTo(suspect)>2.2f&&Game.GameTime<approachDeadline)GameFiber.Wait(250);
                    NativeFunction.Natives.TASK_TURN_PED_TO_FACE_ENTITY(respondingMedic,suspect,1000);
                    GameFiber.Wait(1000);
                    NativeFunction.Natives.TASK_START_SCENARIO_IN_PLACE(respondingMedic,"CODE_HUMAN_MEDIC_TEND_TO_DEAD",0,true);
                    Game.DisplayNotification("~b~Dispatch:~s~ EMS is physically on scene and treating the suspect.");
                    GameFiber.Wait(6500);
                    bool serious=suspect.Health<=System.Math.Max(25,suspect.MaxHealth*45/100);
                    if(!UpdateCustodyLease())suspect.Health=System.Math.Max(suspect.Health,serious?System.Math.Max(50,suspect.MaxHealth/2):System.Math.Max(75,suspect.MaxHealth*3/4));
                    if(serious)
                    {
                        Game.DisplayNotification("~o~Dispatch:~s~ EMS reports serious injuries and will assume hospital transport; patrol retains the arrest hold.");
                        SeriousMedicalTransport=true;
                        MedicalResponseComplete=true;
                        return;
                    }
                    else
                    {
                        Game.DisplayNotification("~g~Dispatch:~s~ EMS confirms treatment complete. On-scene patrol is cleared to transport the prisoner.");
                        MedicalResponseComplete=true;
                        return;
                    }
                }
                catch(System.Exception ex){Game.LogTrivial("AdvancedK9 Callouts: live-position EMS response contained: "+ex);}
                if(!MedicalResponseComplete)_medicalResponseStarted=false;
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
            _cleanupCompleted=true;
            ClearSceneRoute();
            if(SubjectBlip!=null&&SubjectBlip.Exists())SubjectBlip.Delete();
            if(Reporter!=null&&Reporter.Exists())Reporter.Dismiss();
            if(ParentTwo!=null&&ParentTwo.Exists())ParentTwo.Dismiss();
            if(Subject!=null&&Subject.Exists())Subject.Dismiss();
            if(SceneVehicle!=null&&SceneVehicle.Exists()){if(Finished)PreserveSceneVehicleForReturn(SceneVehicle);else SceneVehicle.Dismiss();}SceneVehicle=null;
            if(EvidenceProp!=null&&EvidenceProp.Exists())EvidenceProp.Dismiss();
            if(CoverProp!=null&&CoverProp.Exists())CoverProp.Dismiss();
            if(MedicOne!=null&&MedicOne.Exists())MedicOne.Dismiss();
            if(MedicTwo!=null&&MedicTwo.Exists())MedicTwo.Dismiss();
            if(MedicalVehicle!=null&&MedicalVehicle.Exists())MedicalVehicle.Dismiss();
            BeginPoliceSceneDeparture();
            if(_trafficControlled)
            {
                NativeFunction.Natives.SET_ROADS_IN_AREA(Scene.X-32f,Scene.Y-32f,Scene.Z-8f,Scene.X+32f,Scene.Y+32f,Scene.Z+8f,true,true);
                _trafficControlled=false;
            }
            if(_secondaryTrafficControlled)
            {
                var center=_secondaryTrafficCenter;
                NativeFunction.Natives.SET_ROADS_IN_AREA(center.X-38f,center.Y-38f,center.Z-10f,center.X+38f,center.Y+38f,center.Z+10f,true,true);
                _secondaryTrafficControlled=false;
            }
            if(!string.IsNullOrWhiteSpace(ContextId))AdvancedK9Api.SendCommand("ClearEvidenceMarkers",ContextId,0,"Scene",Scene.X,Scene.Y,Scene.Z,"callout scene cleared");
            Game.LogTrivial("AdvancedK9 Callouts: cleared scene and evidence markers for "+GetType().Name+".");
            base.End();
        }

        public override void OnCalloutNotAccepted(){End();}
    }
}
