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
                ?SceneVehicle.GetOffsetPosition(new Vector3(-4f,-9f,0f))
                :new Vector3(Scene.X-10f,Scene.Y-7f,Scene.Z);
            Vector3 officerOnePosition=SceneVehicle!=null&&SceneVehicle.Exists()
                ?SceneVehicle.GetOffsetPosition(new Vector3(-2.5f,-2.5f,0f))
                :new Vector3(Scene.X-4f,Scene.Y-3f,Scene.Z);
            Vector3 officerTwoPosition=SceneVehicle!=null&&SceneVehicle.Exists()
                ?SceneVehicle.GetOffsetPosition(new Vector3(2.5f,-3.5f,0f))
                :new Vector3(Scene.X+4f,Scene.Y-3f,Scene.Z);
            if(PoliceVehicle==null||!PoliceVehicle.Exists())PoliceVehicle=SpawnPoliceVehicle(cruiserPosition,Game.LocalPlayer.Character.Heading);
            if(OfficerOne==null||!OfficerOne.Exists())OfficerOne=SpawnPoliceOfficer(officerOnePosition,0f);
            if(OfficerTwo==null||!OfficerTwo.Exists())OfficerTwo=SpawnPoliceOfficer(officerTwoPosition,180f);
            if(OfficerOne!=null&&OfficerOne.Exists())NativeFunction.Natives.TASK_STAND_STILL(OfficerOne,-1);
            if(OfficerTwo!=null&&OfficerTwo.Exists())NativeFunction.Natives.TASK_STAND_STILL(OfficerTwo,-1);
            bool vehicleReady=PoliceVehicle!=null&&PoliceVehicle.Exists();
            bool officersReady=OfficerOne!=null&&OfficerOne.Exists()&&OfficerTwo!=null&&OfficerTwo.Exists();
            Game.LogTrivial("AdvancedK9 Callouts: police scene verification for "+GetType().Name+
                ": cruiser="+vehicleReady+", officer1="+(OfficerOne!=null&&OfficerOne.Exists())+
                ", officer2="+(OfficerTwo!=null&&OfficerTwo.Exists())+".");
            return vehicleReady&&officersReady;
        }

        protected bool K9ReadyOnFoot()
        {
            K9ApiSnapshot snapshot;
            return AdvancedK9Api.TryGetSnapshot(out snapshot)&&snapshot.OnDuty&&snapshot.Deployed&&
                snapshot.DogHandle>0&&!string.Equals(snapshot.State,"InVehicle",StringComparison.OrdinalIgnoreCase)&&
                !string.Equals(snapshot.State,"Dismissed",StringComparison.OrdinalIgnoreCase);
        }

        protected void ControlSceneTraffic()
        {
            if(_trafficControlled)return;
            NativeFunction.Natives.SET_ROADS_IN_AREA(Scene.X-32f,Scene.Y-32f,Scene.Z-8f,Scene.X+32f,Scene.Y+32f,Scene.Z+8f,false,true);
            _trafficControlled=true;
            Game.LogTrivial("AdvancedK9 Callouts: traffic control established around "+GetType().Name+" scene.");
        }

        protected float K9DistanceTo(Vector3 position)
        {
            K9ApiSnapshot snapshot;
            if(!AdvancedK9Api.TryGetSnapshot(out snapshot)||snapshot.DogHandle<=0)return float.MaxValue;
            if(!NativeFunction.Natives.DOES_ENTITY_EXIST<bool>(snapshot.DogHandle))return float.MaxValue;
            Vector3 dogPosition=NativeFunction.Natives.GET_ENTITY_COORDS<Vector3>(snapshot.DogHandle,true);
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

        protected int HandleOf(Entity entity){int value;return entity!=null&&entity.Exists()&&int.TryParse(entity.Handle.ToString(),out value)?value:0;}

        protected bool K9Available()
        {
            K9ApiSnapshot snapshot;return AdvancedK9Api.TryGetSnapshot(out snapshot)&&snapshot.OnDuty&&snapshot.Deployed;
        }

        protected void AssignCalloutScent(Ped target,string details)
        {
            if(ApiRequested||target==null||!target.Exists())return;
            ApiRequested=true;
            AdvancedK9Api.SendCommand("AssignScent",ContextId,HandleOf(target),"Ped",Scene.X,Scene.Y,Scene.Z,details,target.Position.X,target.Position.Y,target.Position.Z);
            Game.DisplayNotification("~b~AdvancedK9 callout:~s~ preserved vehicle scent is ready. Command Rex to COLLECT SCENT or TRACK beside the abandoned vehicle.");
        }

        protected void AssignCalloutScent(Ped target,Vector3 collectionPosition,string details,string instruction)
        {
            if(ApiRequested||target==null||!target.Exists())return;
            ApiRequested=true;
            AdvancedK9Api.SendCommand("AssignScent",ContextId,HandleOf(target),"Ped",collectionPosition.X,collectionPosition.Y,collectionPosition.Z,details,target.Position.X,target.Position.Y,target.Position.Z);
            Game.DisplayNotification("~b~AdvancedK9 callout:~s~ "+instruction);
        }

        protected void SupportOfficersFollowK9()
        {
            if(Game.GameTime<_nextSupportMove)return;
            K9ApiSnapshot snapshot;
            Vector3 dogPosition=Game.LocalPlayer.Character.Position;
            if(AdvancedK9Api.TryGetSnapshot(out snapshot)&&snapshot.DogHandle>0&&NativeFunction.Natives.DOES_ENTITY_EXIST<bool>(snapshot.DogHandle))
                dogPosition=NativeFunction.Natives.GET_ENTITY_COORDS<Vector3>(snapshot.DogHandle,true);
            _nextSupportMove=Game.GameTime+3000;
            if(OfficerOne!=null&&OfficerOne.Exists()&&OfficerOne.DistanceTo(dogPosition)>6f)
            {
                OfficerOne.Tasks.Clear();
                OfficerOne.Tasks.FollowNavigationMeshToPosition(dogPosition,OfficerOne.Heading,4.2f);
            }
            if(OfficerTwo!=null&&OfficerTwo.Exists()&&PoliceVehicle!=null&&PoliceVehicle.Exists())
            {
                if(!OfficerTwo.IsInVehicle(PoliceVehicle,false))NativeFunction.Natives.SET_PED_INTO_VEHICLE(OfficerTwo,PoliceVehicle,-1);
                NativeFunction.Natives.TASK_VEHICLE_DRIVE_TO_COORD_LONGRANGE(OfficerTwo,PoliceVehicle,dogPosition.X,dogPosition.Y,dogPosition.Z,18f,786603,10f);
            }
            Game.LogTrivial("AdvancedK9 Callouts: support officers committed to live K9 position "+dogPosition+".");
        }

        protected bool TryFindExistingCover(Vector3 center,out Vector3 hidingPosition)
        {
            hidingPosition=center;
            try
            {
                var cover=World.GetAllObjects().Where(o=>o.Exists()&&o.DistanceTo(center)<70f).OrderBy(o=>o.DistanceTo(center)).FirstOrDefault(o=>{
                    string name=(o.Model.Name??"").ToLowerInvariant();
                    return name.Contains("bush")||name.Contains("hedge")||name.Contains("dumpster")||name.Contains("wall")||name.Contains("fence")||name.Contains("crate")||name.Contains("container");
                });
                if(cover==null||!cover.Exists())return false;
                Vector3 objectPosition=cover.Position;float dx=objectPosition.X-Scene.X,dy=objectPosition.Y-Scene.Y;float length=(float)Math.Sqrt(dx*dx+dy*dy);
                if(length<.1f){dx=1f;dy=0f;length=1f;}
                hidingPosition=new Vector3(objectPosition.X+dx/length*1.4f,objectPosition.Y+dy/length*1.4f,objectPosition.Z);
                Game.LogTrivial("AdvancedK9 Callouts: existing environmental cover selected: "+cover.Model.Name+" at "+hidingPosition+".");
                return true;
            }
            catch(System.Exception ex){Game.LogTrivial("AdvancedK9 Callouts: cover search contained: "+ex.Message);return false;}
        }

        protected void BeginAutomaticTransport(string completionMessage)
        {
            if(Subject==null||!Subject.Exists()||PoliceVehicle==null||!PoliceVehicle.Exists()){Resolve(completionMessage);return;}
            Game.DisplayNotification("~b~On-scene units:~s~ Suspect secured. We will handle transport.");
            var suspect=Subject;var officer=OfficerTwo!=null&&OfficerTwo.Exists()?OfficerTwo:OfficerOne;var transport=PoliceVehicle;
            GameFiber.StartNew(delegate
            {
                try
                {
                    if(officer!=null&&officer.Exists())officer.Tasks.EnterVehicle(transport,-1).WaitForCompletion(7000);
                    if(suspect.Exists())suspect.Tasks.EnterVehicle(transport,1).WaitForCompletion(7000);
                    if(officer!=null&&officer.Exists()&&transport.Exists())NativeFunction.Natives.TASK_VEHICLE_DRIVE_WANDER(officer,transport,20f,786603);
                    GameFiber.Wait(1800);
                }
                catch(System.Exception ex){Game.LogTrivial("AdvancedK9 Callouts: automatic transport fallback contained: "+ex);}
                Resolve(completionMessage);
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
            GameFiber.StartNew(delegate
            {
                try
                {
                    Vector3 downedPosition=suspect.Position;
                    Vector3 ambulancePosition=World.GetNextPositionOnStreet(new Vector3(downedPosition.X+28f,downedPosition.Y+18f,downedPosition.Z));
                    MedicalVehicle=SpawnVehicle("ambulance",ambulancePosition,0f);
                    MedicOne=SpawnPed("s_m_m_paramedic_01",new Vector3(ambulancePosition.X+1.5f,ambulancePosition.Y,ambulancePosition.Z),0f);
                    MedicTwo=SpawnPed("s_m_m_paramedic_01",new Vector3(ambulancePosition.X-1.5f,ambulancePosition.Y,ambulancePosition.Z),0f);
                    Game.DisplayNotification("~b~Dispatch:~s~ EMS responding to the K9 apprehension at the suspect's current location.");
                    Game.LogTrivial("AdvancedK9 Callouts: EMS routed to live downed suspect position "+downedPosition+" instead of scene origin "+Scene+".");
                    if(MedicOne!=null&&MedicOne.Exists())MedicOne.Tasks.FollowNavigationMeshToPosition(suspect.Position,MedicOne.Heading,3.2f).WaitForCompletion(12000);
                    if(MedicTwo!=null&&MedicTwo.Exists())MedicTwo.Tasks.FollowNavigationMeshToPosition(suspect.Position,MedicTwo.Heading,3.0f).WaitForCompletion(12000);
                    if(!suspect.Exists())return;
                    if(MedicOne!=null&&MedicOne.Exists())NativeFunction.Natives.TASK_TURN_PED_TO_FACE_ENTITY(MedicOne,suspect,1000);
                    if(MedicTwo!=null&&MedicTwo.Exists())NativeFunction.Natives.TASK_TURN_PED_TO_FACE_ENTITY(MedicTwo,suspect,1000);
                    Game.DisplayNotification("~b~EMS:~s~ Treating K9 apprehension injuries on scene.");
                    GameFiber.Wait(5000);
                    bool serious=suspect.Health<=System.Math.Max(25,suspect.MaxHealth*45/100);
                    suspect.Health=System.Math.Max(suspect.Health,serious?System.Math.Max(50,suspect.MaxHealth/2):System.Math.Max(75,suspect.MaxHealth*3/4));
                    if(serious&&MedicalVehicle!=null&&MedicalVehicle.Exists()&&MedicOne!=null&&MedicOne.Exists())
                    {
                        Vector3 hospital=NearestHospital(suspect.Position);
                        NativeFunction.Natives.SET_PED_INTO_VEHICLE(suspect,MedicalVehicle,2);
                        NativeFunction.Natives.SET_PED_INTO_VEHICLE(MedicOne,MedicalVehicle,-1);
                        if(MedicTwo!=null&&MedicTwo.Exists())NativeFunction.Natives.SET_PED_INTO_VEHICLE(MedicTwo,MedicalVehicle,1);
                        Ped patrolDriver=OfficerTwo!=null&&OfficerTwo.Exists()?OfficerTwo:OfficerOne;
                        if(patrolDriver!=null&&patrolDriver.Exists()&&PoliceVehicle!=null&&PoliceVehicle.Exists())
                        {
                            NativeFunction.Natives.SET_PED_INTO_VEHICLE(patrolDriver,PoliceVehicle,-1);
                            NativeFunction.Natives.TASK_VEHICLE_FOLLOW(patrolDriver,PoliceVehicle,MedicalVehicle,20f,786603,8);
                        }
                        NativeFunction.Natives.TASK_VEHICLE_DRIVE_TO_COORD_LONGRANGE(MedicOne,MedicalVehicle,hospital.X,hospital.Y,hospital.Z,22f,786603,8f);
                        Game.DisplayNotification("~o~EMS:~s~ Serious injuries require hospital transport. The on-scene patrol unit is following to complete the arrest.");
                        Game.LogTrivial("AdvancedK9 Callouts: serious suspect injury transported toward hospital "+hospital+" with patrol follow.");
                        GameFiber.Wait(5000);
                    }
                    else
                    {
                        NativeFunction.Natives.SET_ENABLE_HANDCUFFS(suspect,true);
                        NativeFunction.Natives.TASK_HANDS_UP(suspect,-1,Game.LocalPlayer.Character,-1,true);
                        Game.DisplayNotification("~g~EMS:~s~ Suspect treated and cleared on scene. Patrol will complete arrest and transport.");
                        GameFiber.Wait(1200);
                        BeginAutomaticTransport(completionMessage);
                        return;
                    }
                }
                catch(System.Exception ex){Game.LogTrivial("AdvancedK9 Callouts: live-position EMS response contained: "+ex);}
                Resolve(completionMessage);
            },"AdvancedK9 live-position EMS response");
            return true;
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

        public override void End()
        {
            if(_cleanupCompleted)return;
            _cleanupCompleted=true;
            ClearSceneRoute();
            if(SubjectBlip!=null&&SubjectBlip.Exists())SubjectBlip.Delete();
            if(Reporter!=null&&Reporter.Exists())Reporter.Dismiss();
            if(ParentTwo!=null&&ParentTwo.Exists())ParentTwo.Dismiss();
            if(Subject!=null&&Subject.Exists())Subject.Dismiss();
            if(SceneVehicle!=null&&SceneVehicle.Exists())SceneVehicle.Dismiss();
            if(EvidenceProp!=null&&EvidenceProp.Exists())EvidenceProp.Dismiss();
            if(CoverProp!=null&&CoverProp.Exists())CoverProp.Dismiss();
            if(MedicOne!=null&&MedicOne.Exists())MedicOne.Dismiss();
            if(MedicTwo!=null&&MedicTwo.Exists())MedicTwo.Dismiss();
            if(MedicalVehicle!=null&&MedicalVehicle.Exists())MedicalVehicle.Dismiss();
            if(OfficerOne!=null&&OfficerOne.Exists())OfficerOne.Dismiss();
            if(OfficerTwo!=null&&OfficerTwo.Exists())OfficerTwo.Dismiss();
            if(PoliceVehicle!=null&&PoliceVehicle.Exists())PoliceVehicle.Dismiss();
            if(_trafficControlled)
            {
                NativeFunction.Natives.SET_ROADS_IN_AREA(Scene.X-32f,Scene.Y-32f,Scene.Z-8f,Scene.X+32f,Scene.Y+32f,Scene.Z+8f,true,true);
                _trafficControlled=false;
            }
            if(!string.IsNullOrWhiteSpace(ContextId))AdvancedK9Api.SendCommand("ClearEvidenceMarkers",ContextId,0,"Scene",Scene.X,Scene.Y,Scene.Z,"callout scene cleared");
            Game.LogTrivial("AdvancedK9 Callouts: cleared scene and evidence markers for "+GetType().Name+".");
            base.End();
        }

        public override void OnCalloutNotAccepted(){End();}
    }
}
