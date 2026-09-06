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
        public override bool OnBeforeCalloutDisplayed()
        {
            try
            {
                return Prepare("Armed burglary suspect hiding — K9 requested",StreetOffset(460f,Random.Next(-130,131)),85f);
            }
            catch(System.Exception ex)
            {
                Game.LogTrivial("AdvancedK9 Callouts: "+GetType().Name+" primary scene calculation failed; using safe fallback: "+ex);
                var playerPosition=Game.LocalPlayer.Character.Position;
                return Prepare("Armed burglary suspect hiding — K9 requested",new Vector3(playerPosition.X+260f,playerPosition.Y,playerPosition.Z),85f);
            }
        }
        public override bool OnCalloutAccepted()
        {
            StartedAt=Game.GameTime;_outcome=Random.Next(5);
            Reporter=SpawnPed("a_m_y_business_03",Scene,0f);
            Vector3 hidePosition=new Vector3(Scene.X+Random.Next(55,100),Scene.Y+Random.Next(-70,71),Scene.Z);
            Subject=SpawnPed("g_m_y_mexgoon_02",hidePosition,Random.Next(360));if(Subject==null)return false;
            NativeFunction.Natives.GIVE_WEAPON_TO_PED(Subject,NativeFunction.Natives.GET_HASH_KEY<uint>("WEAPON_PISTOL"),36,false,false);
            NativeFunction.Natives.TASK_COWER(Subject,-1);
            Functions.PlayScannerAudioUsingPosition("WE_HAVE CRIME_BURGLARY IN_OR_ON_POSITION UNITS_RESPOND_CODE_3",Scene);
            RouteToScene("Respond to the burglary location and establish containment.");
            return base.OnCalloutAccepted();
        }
        public override void Process()
        {
            if(Finished||Subject==null||!Subject.Exists()){if(!Finished)Resolve("~r~Armed Burglary ended: suspect unavailable.");return;}
            var player=Game.LocalPlayer.Character;
            if(!ApiRequested&&player.DistanceTo(Scene)<55f)RequestK9("SearchBuilding",Subject,"armed burglary suspect hiding near structure");
            if(ApiRequested)ClearSceneRoute();
            if(ApiRequested&&player.DistanceTo(Subject)<24f)
            {
                if(_outcome<=1)NativeFunction.Natives.TASK_HANDS_UP(Subject,120000,player,-1,true);
                else if(_outcome==2)NativeFunction.Natives.TASK_SMART_FLEE_PED(Subject,player,700f,-1,false,false);
                else if(_outcome==3)NativeFunction.Natives.TASK_COMBAT_PED(Subject,player,0,16);
                else NativeFunction.Natives.TASK_COWER(Subject,-1);
                SubjectBlip=Subject.AttachBlip();SubjectBlip.IsRouteEnabled=true;
                Resolve(_outcome<=1?"~g~Burglary suspect surrendered during the K9 search.":"~o~Burglary suspect located; resolve the incident using department policy.");
            }
            else if(Game.GameTime-StartedAt>420000)Resolve("~r~Armed Burglary: suspect escaped the containment area.");
            base.Process();
        }
    }
}
