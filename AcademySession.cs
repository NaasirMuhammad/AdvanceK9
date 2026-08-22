using System;
using Rage;

namespace AdvancedK9
{
    internal sealed class AcademySession
    {
        private readonly Ped _dog;
        private readonly string _name;

        public AcademySession(Ped dog, string name) { _dog = dog; _name = name; }

        public void Run(Action sit, Action down, Action follow, Action search)
        {
            Game.DisplayNotification("~b~K9 TRAINING ACADEMY~s~~n~Four guided evaluations are beginning.");
            var score = 0;
            score += Exercise("Obedience 1/4: SIT", sit, 1800);
            score += Exercise("Obedience 2/4: DOWN", down, 1800);
            score += Exercise("Recall 3/4: FOLLOW", follow, 2500);
            score += Exercise("Detection 4/4: SEARCH nearest training subject", search, 5000);
            var grade = score == 4 ? "PASS — Excellent" : score >= 3 ? "PASS" : "RETRAIN";
            Game.DisplayNotification("~b~ACADEMY COMPLETE~s~~n~Handler: Player~n~K9: " + _name + "~n~Score: " + score + "/4 — " + grade);
        }

        private int Exercise(string title, Action action, int observationTime)
        {
            if (_dog == null || !_dog.Exists()) return 0;
            Game.DisplaySubtitle("~y~" + title, observationTime);
            try { action(); GameFiber.Wait(observationTime); return _dog.Exists() && !_dog.IsDead ? 1 : 0; }
            catch { return 0; }
        }
    }
}
