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

        private Vector3 NearestWildernessScene()
        {
            Vector3[] scenes={
                new Vector3(-741.49f,5594.84f,41.65f),
                new Vector3(-537.31f,5376.12f,70.44f),
                new Vector3(-1044.00f,4912.00f,206.00f),
                new Vector3(-207.00f,5148.00f,148.00f),
                new Vector3(501.77f,5604.74f,797.91f)
            };
            Vector3[] subjects={
                new Vector3(-847.00f,5528.00f,34.60f),
                new Vector3(-704.00f,5278.00f,74.10f),
                new Vector3(-1248.00f,4858.00f,223.00f),
                new Vector3(-91.00f,5264.00f,161.00f),
                new Vector3(390.00f,5652.00f,785.00f)
            };
            Vector3 player=Game.LocalPlayer.Character.Position;int best=0;float distance=player.DistanceTo(scenes[0]);
            for(int i=1;i<scenes.Length;i++){float candidate=player.DistanceTo(scenes[i]);if(candidate<distance){best=i;distance=candidate;}}
            _plannedSubjectPosition=subjects[best];return scenes[best];
        }

        public override bool OnBeforeCalloutDisplayed()
        {
            try{return Prepare("Missing vulnerable teen in Mount Chiliad / Paleto Forest — K9 requested",NearestWildernessScene(),90f,false);}
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
                Vector3 safeTeenPosition;
                if(TryResolveSafePedPosition(_plannedSubjectPosition,out safeTeenPosition))_plannedSubjectPosition=safeTeenPosition;
                CoverProp=SpawnProp("prop_bush_med_03",new Vector3(_plannedSubjectPosition.X+2f,_plannedSubjectPosition.Y,_plannedSubjectPosition.Z));
                Subject=SpawnPed("a_f_y_hipster_02",_plannedSubjectPosition,Random.Next(360));
                if(Reporter==null||!Reporter.Exists()||Subject==null||!Subject.Exists()){End();return false;}
                SubjectBlip=Subject.AttachBlip();SubjectBlip.IsRouteEnabled=false;SubjectBlip.Alpha=0;
                Subject.BlockPermanentEvents=true;Subject.IsPersistent=true;
                Subject.Tasks.ClearImmediately();
                Subject.Tasks.PlayAnimation("amb@code_human_cower@male@base","base",1.0f,AnimationFlags.Loop);
                NativeFunction.Natives.SET_PED_KEEP_TASK(Subject,true);
                Game.LogTrivial("AdvancedK9 Callouts: missing teen spawned once in persistent crouched concealment at "+_plannedSubjectPosition+" in the Mount Chiliad / Paleto Forest search region.");
                Functions.PlayScannerAudioUsingPosition("CITIZENS_REPORT IN_OR_ON_POSITION",Scene);
                RouteToScene("Respond to the parents and officers at the teen's last-known wilderness trail location.");
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
                if(ApiRequested&&!_subjectLocated&&K9TrackingActive())SupportOfficersFollowK9();
                if(ApiRequested&&!_subjectLocated&&System.Math.Min(K9DistanceTo(Subject.Position),player.DistanceTo(Subject))<12f)
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
