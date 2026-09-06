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
        private uint _locatedAt;

        public override bool OnBeforeCalloutDisplayed()
        {
            try{if(!Prepare("Armed burglary suspect hiding — K9 requested",StreetOffset(460f,Random.Next(-130,131)),85f))return false;}
            catch(System.Exception ex)
            {
                Game.LogTrivial("AdvancedK9 Callouts: "+GetType().Name+" primary scene calculation failed; using safe fallback: "+ex);
                var playerPosition=Game.LocalPlayer.Character.Position;
                if(!Prepare("Armed burglary suspect hiding — K9 requested",new Vector3(playerPosition.X+260f,playerPosition.Y,playerPosition.Z),85f))return false;
            }
            StagePoliceScene();ControlSceneTraffic();return true;
        }

        public override bool OnCalloutAccepted()
        {
            StartedAt=Game.GameTime;_outcome=Random.Next(5);
            Reporter=SpawnPed("a_m_y_business_03",new Vector3(Scene.X+2f,Scene.Y,Scene.Z),0f);
            StagePoliceScene();
            EvidenceProp=SpawnProp("prop_ld_shirt_01",new Vector3(Scene.X-1.5f,Scene.Y+1f,Scene.Z));
            float angle=Random.Next(360);float distance=Random.Next(65,111);
            Vector3 hidePosition=Scene+new Vector3((float)System.Math.Sin(angle*System.Math.PI/180.0)*distance,(float)System.Math.Cos(angle*System.Math.PI/180.0)*distance,0f);
            hidePosition=World.GetNextPositionOnStreet(hidePosition);
            CoverProp=SpawnProp(Random.Next(2)==0?"prop_dumpster_01a":"prop_bush_med_03",new Vector3(hidePosition.X+2f,hidePosition.Y,hidePosition.Z));
            Subject=SpawnPed("g_m_y_mexgoon_02",hidePosition,Random.Next(360));if(Subject==null)return false;
            NativeFunction.Natives.GIVE_WEAPON_TO_PED(Subject,NativeFunction.Natives.GET_HASH_KEY<uint>("WEAPON_PISTOL"),36,false,false);
            NativeFunction.Natives.TASK_STAND_STILL(Subject,-1);
            Functions.PlayScannerAudioUsingPosition("WE_HAVE CRIME_BURGLARY IN_OR_ON_POSITION UNITS_RESPOND_CODE_3",Scene);
            RouteToScene("Respond to the burglary scene. Officers recovered clothing torn from the fleeing suspect.");
            return base.OnCalloutAccepted();
        }

        public override void Process()
        {
            if(Finished||Subject==null||!Subject.Exists()){if(!Finished)Resolve("~r~Armed Burglary ended: suspect unavailable.");return;}
            var player=Game.LocalPlayer.Character;
            if(!_sceneBriefed&&player.DistanceTo(Scene)<35f)
            {
                _sceneBriefed=true;
                Vector3 article=EvidenceProp!=null&&EvidenceProp.Exists()?EvidenceProp.Position:Scene;
                AssignCalloutScent(Subject,article,"torn shirt recovered during armed burglary","A visible piece of the suspect's torn shirt is on the ground. Bring Rex to it and command COLLECT SCENT or TRACK.");
                if(ApiRequested){ClearSceneRoute();Game.LogTrivial("AdvancedK9 Callouts: Armed Burglary clothing scent source registered; patrol search bypassed.");}
            }

            if(ApiRequested&&!_suspectLocated)SupportOfficersFollowK9();
            if(ApiRequested&&!_suspectLocated&&K9DistanceTo(Subject.Position)<18f)
            {
                _suspectLocated=true;_locatedAt=Game.GameTime;
                SubjectBlip=Subject.AttachBlip();SubjectBlip.IsRouteEnabled=true;
                if(_outcome<=1)NativeFunction.Natives.TASK_HANDS_UP(Subject,120000,player,-1,true);
                else if(_outcome<=3)NativeFunction.Natives.TASK_SMART_FLEE_PED(Subject,player,700f,-1,false,false);
                else NativeFunction.Natives.TASK_COMBAT_PED(Subject,player,0,16);
                Game.DisplayNotification(_outcome<=1?"~g~Rex located the armed burglary suspect behind cover. The suspect is surrendering.":"~o~Rex flushed the armed suspect from cover. Suspect marked; officers are moving with the K9 team.");
            }

            if(_suspectLocated)
            {
                if(Subject.IsDead)Resolve("~o~Armed Burglary concluded: suspect is deceased.");
                else if(NativeFunction.Natives.IS_PED_CUFFED<bool>(Subject)&&!_transportStarted){_transportStarted=true;BeginAutomaticTransport("~g~Armed Burglary complete: on-scene units transported the prisoner.");}
                else if(Game.GameTime-_locatedAt>300000)Resolve("~o~Armed Burglary concluded after suspect location.");
            }
            else if(Game.GameTime-StartedAt>420000)Resolve("~r~Armed Burglary: suspect escaped the containment area.");
            base.Process();
        }
    }
}
