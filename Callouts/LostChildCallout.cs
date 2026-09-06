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
        private bool _sceneBriefed;
        private Vector3 NearestPedestrianScene()
        {
            Vector3[] scenes={
                new Vector3(215f,-920f,30f),new Vector3(1080f,-690f,57f),
                new Vector3(-1250f,-1500f,4f),new Vector3(116f,-1942f,20f),
                new Vector3(-1500f,-790f,10f),new Vector3(1850f,3700f,34f),
                new Vector3(1700f,4800f,42f),new Vector3(-105f,6465f,31f),
                new Vector3(-3150f,1100f,20f)
            };
            Vector3 player=Game.LocalPlayer.Character.Position,best=scenes[0];float distance=player.DistanceTo(best);
            for(int i=1;i<scenes.Length;i++){float candidate=player.DistanceTo(scenes[i]);if(candidate<distance){best=scenes[i];distance=candidate;}}
            return best;
        }

        public override bool OnBeforeCalloutDisplayed()
        {
            return Prepare("Lost child — K9 requested",NearestPedestrianScene(),90f,false);
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
            if(!_sceneBriefed&&player.DistanceTo(Scene)<35f)
            {
                _sceneBriefed=true;
                AssignCalloutScent(Subject,"parent-provided clothing article from the child's last-known location");
                if(ApiRequested)
                {
                    ClearSceneRoute();
                    Game.DisplayNotification("~b~Reporting parent:~s~ This clothing item belongs to my child. Deploy Rex beside me, then command COLLECT SCENT or TRACK.");
                    Game.LogTrivial("AdvancedK9 Callouts: Lost Child scent article registered; awaiting handler command.");
                }
            }
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
