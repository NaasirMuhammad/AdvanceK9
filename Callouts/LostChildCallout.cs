using LSPD_First_Response.Mod.API;
using LSPD_First_Response.Mod.Callouts;
using Rage;
using Rage.Native;

namespace AdvancedK9.Callouts
{
    [CalloutInfo("AdvancedK9: Lost Child",CalloutProbability.Medium)]
    public sealed class LostChildCallout : AdvancedK9Callout
    {
        private int _outcome;
        public override bool OnBeforeCalloutDisplayed()
        {
            try
            {
                return Prepare("Lost child — K9 requested",StreetOffset(420f,Random.Next(-120,121)),90f);
            }
            catch(System.Exception ex)
            {
                Game.LogTrivial("AdvancedK9 Callouts: "+GetType().Name+" primary scene calculation failed; using safe fallback: "+ex);
                var playerPosition=Game.LocalPlayer.Character.Position;
                return Prepare("Lost child — K9 requested",new Vector3(playerPosition.X+260f,playerPosition.Y,playerPosition.Z),90f);
            }
        }
        public override bool OnCalloutAccepted()
        {
            StartedAt=Game.GameTime;_outcome=Random.Next(4);
            Reporter=SpawnPed("a_f_y_business_02",Scene,0f);
            Vector3 childPosition=new Vector3(Scene.X+Random.Next(180,320),Scene.Y+Random.Next(-180,181),Scene.Z);
            Subject=SpawnPed("a_m_y_skater_01",childPosition,Random.Next(360));
            if(Subject==null)return false;
            SubjectBlip=Subject.AttachBlip();SubjectBlip.IsRouteEnabled=false;SubjectBlip.Alpha=0;
            NativeFunction.Natives.TASK_COWER(Subject,-1);
            Functions.PlayScannerAudioUsingPosition("CITIZENS_REPORT CRIME_MISSING_PERSON IN_OR_ON_POSITION UNITS_RESPOND_CODE_2",Scene);
            RouteToScene("Respond to the reporting party at the child’s last-known location.");
            return base.OnCalloutAccepted();
        }
        public override void Process()
        {
            if(Finished||Subject==null||!Subject.Exists()){if(!Finished)Resolve("~r~Lost Child ended: subject unavailable.");return;}
            var player=Game.LocalPlayer.Character;
            if(!ApiRequested&&player.DistanceTo(Scene)<45f)RequestK9("Track",Subject,"lost child last-known-location scent pad");
            if(ApiRequested)ClearSceneRoute();
            if(ApiRequested&&player.DistanceTo(Subject)<12f)
            {
                string result=_outcome==0?"~g~Child located safely after wandering away.":_outcome==1?"~g~Child located hiding and frightened.":_outcome==2?"~o~Child located with a minor injury; medical assistance requested.":"~g~Child located with a concerned adult; identity verification required.";
                Resolve(result);
            }
            else if(Game.GameTime-StartedAt>480000)Resolve("~r~Lost Child: trail went cold before recovery.");
            base.Process();
        }
    }
}
