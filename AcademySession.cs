using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Rage;
using Rage.Native;

namespace AdvancedK9
{
    internal sealed class AcademySession
    {
        private readonly Ped _dog;
        private readonly string _name;
        private readonly Random _random = new Random();

        public AcademySession(Ped dog, string name) { _dog = dog; _name = name; }

        public void Run(Action sit, Action down, Action follow)
        {
            Game.DisplayNotification("~b~K9 TRAINING ACADEMY~s~~n~Five handler-controlled levels are beginning. Follow the prompts and press Y when ready.");
            var score = 0;
            score += ObedienceLevel(sit, down);
            score += PlaceAndStayLevel(sit);
            score += RecallLevel(follow);
            score += AgilityLevel();
            score += DetectionLineupLevel(sit);
            follow();
            var grade = score == 5 ? "PASS — Excellent" : score >= 4 ? "PASS" : "RETRAIN";
            Game.DisplayNotification("~b~ACADEMY COMPLETE~s~~n~Handler: Player~n~K9: " + _name + "~n~Score: " + score + "/5 — " + grade);
        }

        private int ObedienceLevel(Action sit, Action down)
        {
            if (!WaitForHandler("LEVEL 1/5 — OBEDIENCE", "Press ~y~Y~s~ to begin the sit/down sequence")) return 0;
            try
            {
                sit(); GameFiber.Wait(1800);
                Game.DisplaySubtitle("~b~Handler command: DOWN", 1800);
                down(); GameFiber.Wait(2200);
                return DogReady() ? 1 : 0;
            }
            catch { return 0; }
        }

        private int PlaceAndStayLevel(Action sit)
        {
            if (!WaitForHandler("LEVEL 2/5 — PLACE AND STAY", "Press ~y~Y~s~ to send the K9 to the marked position")) return 0;
            try
            {
                var handler = Game.LocalPlayer.Character;
                var place = handler.GetOffsetPosition(new Vector3(-4f, 7f, 0f));
                DrawMarkerFor(place, 1200);
                _dog.Tasks.Clear();
                _dog.Tasks.FollowNavigationMeshToPosition(place, handler.Heading + 180f, 1.8f).WaitForCompletion(7000);
                if (!DogReady() || _dog.DistanceTo(place) > 3.5f) return 0;
                sit();
                Game.DisplaySubtitle("~b~STAY~s~ — handler must move at least six metres away", 4500);
                var end = Game.GameTime + 8000;
                while (DogReady() && handler.DistanceTo(_dog) < 6f && Game.GameTime < end) GameFiber.Yield();
                if (handler.DistanceTo(_dog) < 6f) { Game.DisplayNotification("~o~Level incomplete: handler did not create enough distance."); return 0; }
                GameFiber.Wait(2500);
                return DogReady() ? 1 : 0;
            }
            catch { return 0; }
        }

        private int RecallLevel(Action follow)
        {
            if (!WaitForHandler("LEVEL 3/5 — DISTANCE RECALL", "Move into position, then press ~y~Y~s~ to recall the K9")) return 0;
            try
            {
                var handler = Game.LocalPlayer.Character;
                var startingDistance = _dog.DistanceTo(handler);
                if (startingDistance < 5f) { Game.DisplayNotification("~o~Recall begins from the academy start line."); _dog.Position=handler.GetOffsetPosition(new Vector3(0f,10f,0f)); }
                Game.DisplaySubtitle("~g~RECALL!~s~ Watch the K9 cover the distance to heel", 5000);
                follow();
                var timeout = Game.GameTime + 7000;
                while (DogReady() && _dog.DistanceTo(handler) > 2.5f && Game.GameTime < timeout) GameFiber.Yield();
                return DogReady() && _dog.DistanceTo(handler) <= 3.5f ? 1 : 0;
            }
            catch { return 0; }
        }

        private int AgilityLevel()
        {
            if (!WaitForHandler("LEVEL 4/5 — AGILITY", "Press ~y~Y~s~ to send the K9 through the cone weave")) return 0;
            var props = new List<Rage.Object>();
            try
            {
                var handler = Game.LocalPlayer.Character;
                var model = new Model("prop_mp_cone_02");
                model.LoadAndWait();
                if (!model.IsLoaded) return 0;
                for (var i = 0; i < 5; i++)
                {
                    var conePosition = handler.GetOffsetPosition(new Vector3(i % 2 == 0 ? -1.4f : 1.4f, 4f + i * 2.2f, 0f));
                    props.Add(new Rage.Object(model, conePosition));
                }
                model.Dismiss();
                for (var i = 0; i < props.Count; i++)
                {
                    if (!DogReady()) return 0;
                    var passPoint = props[i].GetOffsetPosition(new Vector3(i % 2 == 0 ? 1.1f : -1.1f, 0f, 0f));
                    Game.DisplaySubtitle("~b~Agility gate " + (i + 1) + "/5", 1300);
                    _dog.Tasks.FollowNavigationMeshToPosition(passPoint, handler.Heading, 2.5f).WaitForCompletion(4500);
                }
                return DogReady() ? 1 : 0;
            }
            catch { return 0; }
            finally { foreach (var prop in props) if (prop != null && prop.Exists()) prop.Delete(); }
        }

        private int DetectionLineupLevel(Action sit)
        {
            if (!WaitForHandler("LEVEL 5/5 — SCENT LINEUP", "Press ~y~Y~s~ to begin a three-station blind search")) return 0;
            var props = new List<Rage.Object>();
            try
            {
                var handler = Game.LocalPlayer.Character;
                var model = new Model("prop_cs_rub_binbag_01");
                model.LoadAndWait();
                if (!model.IsLoaded) return 0;
                var positive = _random.Next(0, 3);
                for (var i = 0; i < 3; i++) props.Add(new Rage.Object(model, handler.GetOffsetPosition(new Vector3(-4f + i * 4f, 10f, 0f))));
                model.Dismiss();
                Game.DisplayNotification("~b~Blind scent lineup:~s~ one of three stations contains the training odor.");
                for (var i = 0; i < props.Count; i++)
                {
                    if (!DogReady()) return 0;
                    var station = props[i].Position;
                    Game.DisplaySubtitle("~b~Inspecting scent station " + (i + 1) + "/3", 2200);
                    _dog.Tasks.Clear();
                    _dog.Tasks.FollowNavigationMeshToPosition(station, handler.Heading, 1.7f).WaitForCompletion(6500);
                    _dog.Tasks.PlayAnimation("creatures@rottweiler@indication@", "indicate_low", 4f, AnimationFlags.None).WaitForCompletion(900);
                    if (i != positive) Game.DisplaySubtitle("~g~Clear station~s~ — continue searching", 1300);
                }
                Game.DisplaySubtitle("~b~K9 returning to the trained odor source", 1800);
                _dog.Tasks.FollowNavigationMeshToPosition(props[positive].Position, handler.Heading, 1.5f).WaitForCompletion(5000);
                sit();
                NativeFunction.Natives.PLAY_PED_AMBIENT_SPEECH_NATIVE(_dog, "BARK", "SPEECH_PARAMS_FORCE");
                Game.DisplaySubtitle("~r~POSITIVE TRAINING ALERT~s~ at station " + (positive + 1), 2800);
                GameFiber.Wait(2800);
                return DogReady() && _dog.DistanceTo(props[positive]) < 4.5f ? 1 : 0;
            }
            catch { return 0; }
            finally { foreach (var prop in props) if (prop != null && prop.Exists()) prop.Delete(); }
        }

        private bool WaitForHandler(string level, string instruction)
        {
            if (!DogReady()) return false;
            Game.DisplayNotification("~b~" + level + "~s~~n~" + instruction);
            var timeout = Game.GameTime + 20000;
            while (DogReady() && Game.GameTime < timeout)
            {
                Game.DisplaySubtitle(instruction + "  ~c~(" + Math.Max(0, (int)((timeout - Game.GameTime) / 1000)) + "s)", 200);
                if (Game.IsKeyDown(Keys.Y)) { GameFiber.Wait(250); return true; }
                GameFiber.Yield();
            }
            Game.DisplayNotification("~o~Training level skipped: handler response timed out.");
            return false;
        }

        private void DrawMarkerFor(Vector3 position, int duration)
        {
            var end = Game.GameTime + (uint)duration;
            while (Game.GameTime < end)
            {
                NativeFunction.Natives.DRAW_MARKER(1, position.X, position.Y, position.Z - .9f, 0f, 0f, 0f, 0f, 0f, 0f, 1.2f, 1.2f, .35f, 30, 120, 220, 170, false, false, 2, false, 0, 0, false);
                GameFiber.Yield();
            }
        }

        private bool DogReady() => _dog != null && _dog.Exists() && !_dog.IsDead;
    }
}
