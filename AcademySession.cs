using System;
using Rage;
using Rage.Native;

namespace AdvancedK9
{
    internal sealed class AcademySession
    {
        private readonly Ped _dog;
        private readonly string _name;

        public AcademySession(Ped dog, string name) { _dog = dog; _name = name; }

        public void Run(Action sit, Action down, Action follow)
        {
            Game.DisplayNotification("~b~K9 TRAINING ACADEMY~s~~n~A visible four-stage field course is beginning.");
            var score = 0;
            score += CommandStage("Stage 1/4 — SIT", sit, 2200);
            score += CommandStage("Stage 2/4 — DOWN", down, 2200);
            score += RecallStage(follow);
            score += DetectionStage();
            follow();
            var grade = score == 4 ? "PASS — Excellent" : score >= 3 ? "PASS" : "RETRAIN";
            Game.DisplayNotification("~b~ACADEMY COMPLETE~s~~n~Handler: Player~n~K9: " + _name + "~n~Score: " + score + "/4 — " + grade);
        }

        private int CommandStage(string title, Action action, int observationTime)
        {
            if (!DogReady()) return 0;
            Game.DisplaySubtitle("~y~" + title + "~s~ — observe the K9 perform the command", observationTime);
            try { action(); GameFiber.Wait(observationTime); return DogReady() ? 1 : 0; }
            catch { return 0; }
        }

        private int RecallStage(Action follow)
        {
            if (!DogReady()) return 0;
            try
            {
                var handler = Game.LocalPlayer.Character;
                var start = handler.GetOffsetPosition(new Vector3(0f, 10f, 0f));
                Game.DisplaySubtitle("~y~Stage 3/4 — RECALL~s~ — K9 moving to the start marker", 3500);
                _dog.Tasks.Clear();
                _dog.Tasks.FollowNavigationMeshToPosition(start, handler.Heading + 180f, 2f).WaitForCompletion(7000);
                if (!DogReady()) return 0;
                Game.DisplaySubtitle("~g~RECALL!~s~ Watch the K9 cover the distance back to the handler", 4500);
                follow();
                var timeout = Game.GameTime + 6000;
                while (DogReady() && _dog.DistanceTo(handler) > 2.5f && Game.GameTime < timeout) GameFiber.Yield();
                return DogReady() && _dog.DistanceTo(handler) <= 3.5f ? 1 : 0;
            }
            catch { return 0; }
        }

        private int DetectionStage()
        {
            Rage.Object scent = null;
            try
            {
                if (!DogReady()) return 0;
                var handler = Game.LocalPlayer.Character;
                var position = handler.GetOffsetPosition(new Vector3(2f, 9f, 0f));
                var model = new Model("prop_cs_rub_binbag_01");
                model.LoadAndWait();
                if (!model.IsLoaded) return 0;
                scent = new Rage.Object(model, position);
                model.Dismiss();
                Game.DisplaySubtitle("~y~Stage 4/4 — DETECTION~s~ — follow the K9 to the visible training scent", 5000);
                _dog.Tasks.Clear();
                _dog.Tasks.FollowNavigationMeshToPosition(position, handler.Heading, 1.8f).WaitForCompletion(9000);
                if (!DogReady()) return 0;
                for (var i = 0; i < 3; i++)
                {
                    _dog.Tasks.PlayAnimation("creatures@rottweiler@indication@", "indicate_low", 4f, AnimationFlags.None).WaitForCompletion(700);
                    GameFiber.Wait(250);
                }
                _dog.Tasks.PlayAnimation("creatures@rottweiler@amb@world_dog_sitting@base", "base", 4f, AnimationFlags.Loop);
                NativeFunction.Natives.PLAY_PED_AMBIENT_SPEECH_NATIVE(_dog, "BARK", "SPEECH_PARAMS_FORCE");
                Game.DisplaySubtitle("~g~POSITIVE TRAINING ALERT~s~ — sit and bark", 2500);
                GameFiber.Wait(2500);
                return DogReady() && _dog.DistanceTo(position) < 4.5f ? 1 : 0;
            }
            catch { return 0; }
            finally { if (scent != null && scent.Exists()) scent.Delete(); }
        }

        private bool DogReady() => _dog != null && _dog.Exists() && !_dog.IsDead;
    }
}
