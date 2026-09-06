using LSPD_First_Response.Mod.Callouts;
using Rage;
using Rage.Native;

namespace AdvancedK9.Callouts
{
    [CalloutInfo("AdvancedK9: Fugitive Trail",CalloutProbability.Medium)]
    public sealed class FugitiveTrailCallout : AdvancedK9Callout
    {
        private int _outcome;
        public override bool OnBeforeCalloutDisplayed()
        {
            return Prepare("Fugitive trail from abandoned vehicle",StreetOffset(520f,Random.Next(-160,161)),110f)&&base.OnBeforeCalloutDisplayed();
        }
        public override bool OnCalloutAccepted()
        {
            StartedAt=Game.GameTime;_outcome=Random.Next(4);
            SceneVehicle=SpawnVehicle("primo",Scene,Random.Next(360));
            Vector3 suspectPosition=new Vector3(Scene.X+Random.Next(260,430),Scene.Y+Random.Next(-260,261),Scene.Z);
            Subject=SpawnPed("a_m_m_hillbilly_01",suspectPosition,Random.Next(360));if(Subject==null)return false;
            NativeFunction.Natives.TASK_WANDER_STANDARD(Subject,10f,10);
            Functions.PlayScannerAudioUsingPosition("WE_HAVE CRIME_RESIST_ARREST IN_OR_ON_POSITION",Scene);
            Game.DisplayNotification("~b~Fugitive Trail:~s~ Inspect the abandoned vehicle, then deploy the K9 from the last occupied door.");
            return base.OnCalloutAccepted();
        }
        public override void Process()
        {
            if(Finished||Subject==null||!Subject.Exists()){if(!Finished)Resolve("~r~Fugitive Trail ended: suspect unavailable.");return;}
            var player=Game.LocalPlayer.Character;
            if(!ApiRequested&&player.DistanceTo(Scene)<35f)RequestK9("Track",Subject,"fugitive scent from abandoned vehicle door");
            if(player.DistanceTo(Subject)<28f)
            {
                if(_outcome==0)NativeFunction.Natives.TASK_HANDS_UP(Subject,120000,player,-1,true);
                else if(_outcome==1)NativeFunction.Natives.TASK_SMART_FLEE_PED(Subject,player,600f,-1,false,false);
                else if(_outcome==2)NativeFunction.Natives.TASK_COWER(Subject,-1);
                else NativeFunction.Natives.TASK_WANDER_STANDARD(Subject,10f,10);
                SubjectBlip=Subject.AttachBlip();SubjectBlip.IsRouteEnabled=true;
                Resolve(_outcome==0?"~g~Fugitive surrendered at the end of the K9 trail.":"~o~Fugitive located; take appropriate police action.");
            }
            else if(Game.GameTime-StartedAt>480000)Resolve("~r~Fugitive Trail: scent trail expired.");
            base.Process();
        }
    }
}
