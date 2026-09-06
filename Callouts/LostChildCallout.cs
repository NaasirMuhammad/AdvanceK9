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
        private bool _childLocated;
        private Vector3 _plannedChildPosition;

        private Vector3 NearestPedestrianScene()
        {
            Vector3[] scenes={
                new Vector3(215f,-920f,30f),new Vector3(1080f,-690f,57f),
                new Vector3(-1250f,-1500f,4f),new Vector3(116f,-1942f,20f),
                new Vector3(-1500f,-790f,10f),new Vector3(1850f,3700f,34f),
                new Vector3(1700f,4800f,42f),new Vector3(-105f,6465f,31f),
                new Vector3(-3150f,1100f,20f)
            };
            Vector3[] children={
                new Vector3(250f,-900f,29f),new Vector3(1130f,-700f,56f),
                new Vector3(-1300f,-1550f,4f),new Vector3(150f,-1980f,18f),
                new Vector3(-1550f,-850f,10f),new Vector3(1900f,3720f,32f),
                new Vector3(1750f,4850f,42f),new Vector3(-150f,6500f,29f),
                new Vector3(-3200f,1050f,20f)
            };
            Vector3 player=Game.LocalPlayer.Character.Position;int best=0;float distance=player.DistanceTo(scenes[0]);
            for(int i=1;i<scenes.Length;i++){float candidate=player.DistanceTo(scenes[i]);if(candidate<distance){best=i;distance=candidate;}}
            _plannedChildPosition=children[best];
            return scenes[best];
        }

        public override bool OnBeforeCalloutDisplayed()
        {
            try{return Prepare("Lost child — K9 requested",NearestPedestrianScene(),90f,false);}
            catch(System.Exception ex){Game.LogTrivial("AdvancedK9 Callouts: Lost Child scene preparation failed safely: "+ex);return false;}
        }

        public override bool OnCalloutAccepted()
        {
            try
            {
                StartedAt=Game.GameTime;_outcome=Random.Next(4);
                Reporter=SpawnPed("a_f_y_business_02",Scene,0f);
                Subject=SpawnPed("a_m_y_skater_01",_plannedChildPosition,Random.Next(360));
                if(Reporter==null||!Reporter.Exists()||Subject==null||!Subject.Exists()){End();return false;}
                SubjectBlip=Subject.AttachBlip();SubjectBlip.IsRouteEnabled=false;SubjectBlip.Alpha=0;
                NativeFunction.Natives.TASK_COWER(Subject,-1);
                Functions.PlayScannerAudioUsingPosition("CITIZENS_REPORT IN_OR_ON_POSITION",Scene);
                RouteToScene("Respond to the reporting parent at the child’s last-known pedestrian location.");
                return base.OnCalloutAccepted();
            }
            catch(System.Exception ex)
            {
                Game.LogTrivial("AdvancedK9 Callouts: Lost Child acceptance failed safely: "+ex);
                End();return false;
            }
        }

        public override void Process()
        {
            try
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
                        Game.DisplayNotification("~b~Reporting parent:~s~ This clothing item belongs to my child. Bring Rex beside me, then command COLLECT SCENT or TRACK.");
                        Game.LogTrivial("AdvancedK9 Callouts: Lost Child scent article registered; awaiting handler command.");
                    }
                }
                if(ApiRequested&&!_childLocated&&K9DistanceTo(Subject.Position)<12f)
                {
                    _childLocated=true;
                    SubjectBlip.Alpha=1;SubjectBlip.IsRouteEnabled=true;
                    string result=_outcome==0?"~g~Rex located the child safely after they wandered away.":_outcome==1?"~g~Rex located the frightened child hiding nearby.":_outcome==2?"~o~Rex located the child with a minor injury; request medical assistance.":"~g~Rex located the child with a concerned adult; verify identities.";
                    Game.DisplayNotification(result);
                    Game.LogTrivial("AdvancedK9 Callouts: Rex located Lost Child subject; marker revealed.");
                }
                if(_childLocated&&player.DistanceTo(Subject)<5f)Resolve("~g~Lost Child callout complete: child recovered.");
                else if(Game.GameTime-StartedAt>480000)Resolve("~r~Lost Child: trail went cold before recovery.");
                base.Process();
            }
            catch(System.Exception ex)
            {
                Game.LogTrivial("AdvancedK9 Callouts: Lost Child process error contained: "+ex);
                Resolve("~r~Lost Child ended safely after an internal scene error.");
            }
        }
    }
}
