using LSPD_First_Response.Mod.API;
using LSPD_First_Response.Mod.Callouts;
using Rage;
using Rage.Native;

namespace AdvancedK9.Callouts
{
    [CalloutInfo("AdvancedK9: Missing Vulnerable Teen",CalloutProbability.Medium)]
    public sealed class LostChildCallout : AdvancedK9Callout
    {
        private int _outcome;
        private bool _sceneBriefed;
        private bool _subjectLocated;
        private Vector3 _plannedSubjectPosition;

        private Vector3 NearestPedestrianScene()
        {
            Vector3[] scenes={new Vector3(215f,-920f,30f),new Vector3(1080f,-690f,57f),new Vector3(-1250f,-1500f,4f),new Vector3(116f,-1942f,20f),new Vector3(-1500f,-790f,10f),new Vector3(1850f,3700f,34f),new Vector3(1700f,4800f,42f),new Vector3(-105f,6465f,31f),new Vector3(-3150f,1100f,20f)};
            Vector3[] subjects={new Vector3(320f,-950f,29f),new Vector3(1170f,-760f,57f),new Vector3(-1360f,-1560f,4f),new Vector3(210f,-2020f,18f),new Vector3(-1580f,-900f,10f),new Vector3(1960f,3770f,32f),new Vector3(1810f,4890f,42f),new Vector3(-210f,6515f,29f),new Vector3(-3230f,1190f,20f)};
            Vector3 player=Game.LocalPlayer.Character.Position;int best=0;float distance=player.DistanceTo(scenes[0]);
            for(int i=1;i<scenes.Length;i++){float candidate=player.DistanceTo(scenes[i]);if(candidate<distance){best=i;distance=candidate;}}
            _plannedSubjectPosition=subjects[best];return scenes[best];
        }

        public override bool OnBeforeCalloutDisplayed()
        {
            try{return Prepare("Missing vulnerable teen — K9 requested",NearestPedestrianScene(),90f,false);}
            catch(System.Exception ex){Game.LogTrivial("AdvancedK9 Callouts: Missing Teen scene preparation failed safely: "+ex);return false;}
        }

        public override bool OnCalloutAccepted()
        {
            try
            {
                StartedAt=Game.GameTime;_outcome=Random.Next(3);
                Reporter=SpawnPed("a_f_y_business_02",new Vector3(Scene.X-1.5f,Scene.Y,Scene.Z),0f);
                ParentTwo=SpawnPed("a_m_y_business_02",new Vector3(Scene.X+1.5f,Scene.Y,Scene.Z),0f);
                StagePoliceScene();
                EvidenceProp=SpawnProp("prop_ld_shirt_01",new Vector3(Scene.X,Scene.Y+1.2f,Scene.Z));
                CoverProp=SpawnProp(Random.Next(2)==0?"prop_bush_med_03":"prop_dumpster_01a",new Vector3(_plannedSubjectPosition.X+2f,_plannedSubjectPosition.Y,_plannedSubjectPosition.Z));
                Subject=SpawnPed("a_f_y_hipster_02",_plannedSubjectPosition,Random.Next(360));
                if(Reporter==null||!Reporter.Exists()||Subject==null||!Subject.Exists()){End();return false;}
                SubjectBlip=Subject.AttachBlip();SubjectBlip.IsRouteEnabled=false;SubjectBlip.Alpha=0;
                NativeFunction.Natives.TASK_STAND_STILL(Subject,-1);
                Functions.PlayScannerAudioUsingPosition("CITIZENS_REPORT IN_OR_ON_POSITION",Scene);
                RouteToScene("Respond to the parents and officers at the teen's last-known pedestrian location.");
                return base.OnCalloutAccepted();
            }
            catch(System.Exception ex){Game.LogTrivial("AdvancedK9 Callouts: Missing Teen acceptance failed safely: "+ex);End();return false;}
        }

        public override void Process()
        {
            try
            {
                if(Finished||Subject==null||!Subject.Exists()){if(!Finished)Resolve("~r~Missing Teen ended: subject unavailable.");return;}
                var player=Game.LocalPlayer.Character;
                if(!_sceneBriefed&&player.DistanceTo(Scene)<35f)
                {
                    _sceneBriefed=true;
                    Vector3 article=EvidenceProp!=null&&EvidenceProp.Exists()?EvidenceProp.Position:Scene;
                    AssignCalloutScent(Subject,article,"parent-provided shirt belonging to the missing teen","The parents placed the teen's shirt on the ground. Bring Rex to the visible clothing and command COLLECT SCENT or TRACK.");
                    if(ApiRequested){ClearSceneRoute();Game.LogTrivial("AdvancedK9 Callouts: Missing Teen physical clothing scent article registered.");}
                }
                if(ApiRequested&&!_subjectLocated)SupportOfficersFollowK9();
                if(ApiRequested&&!_subjectLocated&&K9DistanceTo(Subject.Position)<12f)
                {
                    _subjectLocated=true;SubjectBlip.Alpha=1;SubjectBlip.IsRouteEnabled=true;
                    string result=_outcome==0?"~g~Rex located the missing teen safely behind cover.":_outcome==1?"~g~Rex located the frightened teen hiding nearby.":"~o~Rex located the teen with a minor injury; request medical assistance.";
                    Game.DisplayNotification(result);Game.LogTrivial("AdvancedK9 Callouts: Rex located Missing Teen subject; marker revealed.");
                }
                if(_subjectLocated&&player.DistanceTo(Subject)<5f)Resolve("~g~Missing Vulnerable Teen callout complete: teen reunited with family.");
                else if(Game.GameTime-StartedAt>480000)Resolve("~r~Missing Teen: trail went cold before recovery.");
                base.Process();
            }
            catch(System.Exception ex){Game.LogTrivial("AdvancedK9 Callouts: Missing Teen process error contained: "+ex);Resolve("~r~Missing Teen ended safely after an internal scene error.");}
        }
    }
}
