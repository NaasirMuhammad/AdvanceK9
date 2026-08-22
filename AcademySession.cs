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

        public int Run(int level, Action sit, Action down, Action follow)
        {
            Game.DisplayNotification("~b~ADVANCED K9 ACADEMY~s~~n~Level " + level + "/5: " + LevelName(level) + "~n~Each completed exercise awards 20%.");
            int points;
            if (level == 1) points = ObedienceCourse(sit, down, follow);
            else if (level == 2) points = AgilityCourse();
            else if (level == 3) points = DetectionCourse(sit);
            else if (level == 4) points = TrackingCourse(sit);
            else points = ApprehensionCourse(sit, follow);
            follow();
            Game.DisplayNotification("~b~ACADEMY SESSION COMPLETE~s~~n~K9: " + _name + "~n~Progress earned: " + points + "%");
            return points;
        }

        public int RunSpecialty(DetectionSpecialty specialty, Action sit, Action follow)
        {
            string name=specialty==DetectionSpecialty.Narcotics?"NARCOTICS DETECTION":specialty==DetectionSpecialty.Explosives?"EXPLOSIVES DETECTION":"WEAPONS DETECTION";
            Game.DisplayNotification("~b~ADVANCED K9 SPECIALTY~s~~n~"+name+"~n~Five odor-recognition stations; 20% each.");
            int points=SpecialtyDetectionCourse(specialty,sit);
            follow();
            Game.DisplayNotification("~b~SPECIALTY SESSION COMPLETE~s~~n~"+name+" progress earned: "+points+"%");
            return points;
        }

        private int SpecialtyDetectionCourse(DetectionSpecialty specialty,Action sit)
        {
            string title=specialty==DetectionSpecialty.Narcotics?"NARCOTICS ODOR LINEUP":specialty==DetectionSpecialty.Explosives?"BOMB-SAFE STANDOFF LINEUP":"FIREARM ODOR LINEUP";
            if(!WaitForHandler(title,"Press ~y~Y~s~ to present the specialty scent article"))return 0;
            var stations=new List<Rage.Object>();Rage.Object source=null;var score=0;
            try
            {
                var handler=Game.LocalPlayer.Character;
                string stationModel=specialty==DetectionSpecialty.Explosives?"prop_box_wood02a_pu":"prop_cs_heist_bag_02";
                string sourceModel=specialty==DetectionSpecialty.Narcotics?"prop_drug_package_02":specialty==DetectionSpecialty.Explosives?"prop_c4_final":"w_pi_pistol";
                var container=new Model(stationModel);container.LoadAndWait();if(!container.IsLoaded)return 0;
                var odor=new Model(sourceModel);odor.LoadAndWait();if(!odor.IsLoaded){container.Dismiss();return 0;}
                int positive=_random.Next(0,5);float spacing=specialty==DetectionSpecialty.Explosives?5f:4f;
                for(int i=0;i<5;i++)stations.Add(new Rage.Object(container,handler.GetOffsetPosition(new Vector3((-2f+i)*spacing,15f,0f))));
                source=new Rage.Object(odor,stations[positive].Position);NativeFunction.Natives.SET_ENTITY_VISIBLE(source,false,false);NativeFunction.Natives.SET_ENTITY_COLLISION(source,false,false);
                container.Dismiss();odor.Dismiss();
                for(int i=0;i<stations.Count;i++)
                {
                    Game.DisplaySubtitle("~b~"+title+"~s~ — station "+(i+1)+"/5",1800);
                    if(!Navigate(stations[i].GetOffsetPosition(new Vector3(0f,-1.2f,0f)),handler.Heading,true,7000))continue;
                    NativeFunction.Natives.TASK_TURN_PED_TO_FACE_ENTITY(_dog,stations[i],800);GameFiber.Wait(800);PlaySniff();score+=20;
                }
                if(DogReady())
                {
                    Navigate(stations[positive].GetOffsetPosition(new Vector3(0f,-1.2f,0f)),handler.Heading,true,5500);
                    NativeFunction.Natives.TASK_TURN_PED_TO_FACE_ENTITY(_dog,stations[positive],800);GameFiber.Wait(800);sit();
                    NativeFunction.Natives.PLAY_PED_AMBIENT_SPEECH_NATIVE(_dog,"BARK","SPEECH_PARAMS_FORCE");
                    Game.DisplaySubtitle("~r~POSITIVE "+title+" ALERT~s~ — station "+(positive+1),2600);GameFiber.Wait(2600);
                }
                return score;
            }
            catch{return score;}
            finally{if(source!=null&&source.Exists())source.Delete();DeleteAll(stations);}
        }

        private int ObedienceCourse(Action sit, Action down, Action follow)
        {
            var score = 0;
            if (CommandExercise("1/5 SIT", sit)) score += 20;
            if (CommandExercise("2/5 DOWN", down)) score += 20;
            if (CommandExercise("3/5 SIT FROM DOWN", sit)) score += 20;
            if (PlaceStayExercise(sit)) score += 20;
            if (RecallExercise(follow)) score += 20;
            return score;
        }

        private bool CommandExercise(string title, Action action)
        {
            if (!WaitForHandler(title, "Press ~y~Y~s~ to issue the command")) return false;
            try { action(); GameFiber.Wait(2200); return DogReady(); } catch { return false; }
        }

        private bool PlaceStayExercise(Action sit)
        {
            if (!WaitForHandler("4/5 PLACE AND STAY", "Press ~y~Y~s~ to send the K9 to place")) return false;
            try
            {
                var handler = Game.LocalPlayer.Character;
                var place = handler.GetOffsetPosition(new Vector3(-4f, 8f, 0f));
                DrawMarkerFor(place, 1000);
                if (!Navigate(place, handler.Heading + 180f, false, 7000)) return false;
                sit();
                Game.DisplaySubtitle("~b~STAY~s~ — move at least six metres from the K9", 5000);
                var end = Game.GameTime + 9000;
                while (DogReady() && handler.DistanceTo(_dog) < 6f && Game.GameTime < end) GameFiber.Yield();
                if (handler.DistanceTo(_dog) < 6f) return false;
                GameFiber.Wait(2200);
                return DogReady();
            }
            catch { return false; }
        }

        private bool RecallExercise(Action follow)
        {
            if (!WaitForHandler("5/5 DISTANCE RECALL", "Create distance, then press ~y~Y~s~ to recall")) return false;
            try
            {
                var handler = Game.LocalPlayer.Character;
                if (_dog.DistanceTo(handler) < 6f) _dog.Position = handler.GetOffsetPosition(new Vector3(0f, 12f, 0f));
                follow();
                var end = Game.GameTime + 8000;
                while (DogReady() && _dog.DistanceTo(handler) > 2.8f && Game.GameTime < end) GameFiber.Yield();
                return DogReady() && _dog.DistanceTo(handler) <= 3.5f;
            }
            catch { return false; }
        }

        private int AgilityCourse()
        {
            if (!WaitForHandler("AGILITY COURSE", "Press ~y~Y~s~ to start the five-gate weave")) return 0;
            var props = new List<Rage.Object>();
            var score = 0;
            try
            {
                var handler = Game.LocalPlayer.Character;
                var model = new Model("prop_mp_cone_02"); model.LoadAndWait(); if (!model.IsLoaded) return 0;
                for (var i = 0; i < 5; i++) props.Add(new Rage.Object(model, handler.GetOffsetPosition(new Vector3(i % 2 == 0 ? -1.5f : 1.5f, 5f + i * 3f, 0f))));
                model.Dismiss();
                for (var i = 0; i < props.Count; i++)
                {
                    var gate = props[i].GetOffsetPosition(new Vector3(i % 2 == 0 ? 1.15f : -1.15f, 0f, 0f));
                    Game.DisplaySubtitle("~b~Agility gate " + (i + 1) + "/5", 1400);
                    if (Navigate(gate, handler.Heading, false, 5000)) score += 20;
                }
                return score;
            }
            catch { return score; }
            finally { DeleteAll(props); }
        }

        private int DetectionCourse(Action sit)
        {
            if (!WaitForHandler("DETECTION CERTIFICATION", "Press ~y~Y~s~ to start the blind five-station scent lineup")) return 0;
            var props = new List<Rage.Object>();
            var score = 0;
            try
            {
                var handler = Game.LocalPlayer.Character;
                var model = new Model("prop_cs_rub_binbag_01"); model.LoadAndWait(); if (!model.IsLoaded) return 0;
                var positive = _random.Next(0, 5);
                for (var i = 0; i < 5; i++) props.Add(new Rage.Object(model, handler.GetOffsetPosition(new Vector3(-8f + i * 4f, 13f, 0f))));
                model.Dismiss();
                for (var i = 0; i < props.Count; i++)
                {
                    Game.DisplaySubtitle("~b~Scent station " + (i + 1) + "/5", 1700);
                    if (!Navigate(props[i].Position, handler.Heading, true, 6500)) continue;
                    NativeFunction.Natives.TASK_TURN_PED_TO_FACE_ENTITY(_dog, props[i], 800); GameFiber.Wait(800);
                    PlaySniff(); score += 20;
                }
                if (DogReady())
                {
                    Navigate(props[positive].Position, handler.Heading, true, 5000);
                    sit();
                    NativeFunction.Natives.PLAY_PED_AMBIENT_SPEECH_NATIVE(_dog, "BARK", "SPEECH_PARAMS_FORCE");
                    Game.DisplaySubtitle("~r~Correct positive indication~s~ — station " + (positive + 1), 2500);
                    GameFiber.Wait(2500);
                }
                return score;
            }
            catch { return score; }
            finally { DeleteAll(props); }
        }

        private int TrackingCourse(Action sit)
        {
            if (!WaitForHandler("TRACKING CERTIFICATION", "Press ~y~Y~s~ to present the scent article")) return 0;
            var props = new List<Rage.Object>();
            var score = 0;
            try
            {
                var handler = Game.LocalPlayer.Character;
                var model = new Model("prop_cs_rub_binbag_01"); model.LoadAndWait(); if (!model.IsLoaded) return 0;
                var offsets = new[]{new Vector3(2f,7f,0f),new Vector3(7f,14f,0f),new Vector3(3f,22f,0f),new Vector3(-5f,29f,0f),new Vector3(-10f,38f,0f)};
                foreach (var offset in offsets) props.Add(new Rage.Object(model, handler.GetOffsetPosition(offset)));
                model.Dismiss();
                for (var i = 0; i < props.Count; i++)
                {
                    Game.DisplaySubtitle("~b~Tracking trail segment " + (i + 1) + "/5~s~ — follow your K9", 2200);
                    PlaySniff();
                    if (Navigate(props[i].Position, handler.Heading, true, 9000)) score += 20;
                }
                if (DogReady()) { sit(); NativeFunction.Natives.PLAY_PED_AMBIENT_SPEECH_NATIVE(_dog, "BARK", "SPEECH_PARAMS_FORCE"); }
                return score;
            }
            catch { return score; }
            finally { DeleteAll(props); }
        }

        private int ApprehensionCourse(Action sit, Action follow)
        {
            Ped suspect = null;
            var score = 0;
            try
            {
                var handler = Game.LocalPlayer.Character;
                var model = new Model("s_m_y_prisoner_01"); model.LoadAndWait(); if (!model.IsLoaded) return 0;
                suspect = new Ped(model, handler.GetOffsetPosition(new Vector3(0f, 18f, 0f)), handler.Heading + 180f); model.Dismiss();
                suspect.IsPersistent = true; NativeFunction.Natives.SET_ENTITY_INVINCIBLE(suspect, true); suspect.BlockPermanentEvents = true;
                if (WaitForAimedTarget(suspect, "1/5 THREAT IDENTIFICATION")) score += 20;
                if (WaitForHandler("2/5 CONTROLLED DEPLOYMENT", "Aim at the training suspect and press ~y~Y~s~ to send the K9") && Game.LocalPlayer.GetFreeAimingTarget() == suspect)
                {
                    NativeFunction.Natives.TASK_COMBAT_PED(_dog, suspect, 0, 16); GameFiber.Wait(2500); score += DogReady() ? 20 : 0;
                    _dog.Tasks.ClearImmediately(); suspect.Tasks.ClearImmediately();
                }
                if (WaitForHandler("3/5 EMERGENCY RECALL", "Press ~y~Y~s~ to recall before contact resumes"))
                {
                    follow(); var end=Game.GameTime+6000; while(DogReady()&&_dog.DistanceTo(handler)>3f&&Game.GameTime<end)GameFiber.Yield(); if(_dog.DistanceTo(handler)<=4f)score+=20;
                }
                if (WaitForHandler("4/5 SUSPECT GUARD", "Press ~y~Y~s~ to place the K9 on a controlled guard"))
                {
                    Navigate(suspect.GetOffsetPosition(new Vector3(0f,-2f,0f)),suspect.Heading, false,6000); sit(); NativeFunction.Natives.TASK_HANDS_UP(suspect,-1,handler,-1,true); if(_dog.DistanceTo(suspect)<4f)score+=20;
                }
                if (WaitForHandler("5/5 FINAL RELEASE", "Press ~y~Y~s~ to end the deployment and return to heel"))
                {
                    follow(); var end=Game.GameTime+6000; while(DogReady()&&_dog.DistanceTo(handler)>3f&&Game.GameTime<end)GameFiber.Yield(); if(_dog.DistanceTo(handler)<=4f)score+=20;
                }
                return score;
            }
            catch { return score; }
            finally { if (suspect != null && suspect.Exists()) suspect.Delete(); }
        }

        private bool WaitForAimedTarget(Ped target, string title)
        {
            Game.DisplayNotification("~b~"+title+"~s~~n~Aim your taser or firearm directly at the training suspect.");
            var end=Game.GameTime+20000;
            while(DogReady()&&target.Exists()&&Game.GameTime<end)
            {
                uint weapon=NativeFunction.Natives.GET_SELECTED_PED_WEAPON<uint>(Game.LocalPlayer.Character);
                uint unarmed=NativeFunction.Natives.GET_HASH_KEY<uint>("WEAPON_UNARMED");
                if(weapon!=unarmed&&Game.LocalPlayer.GetFreeAimingTarget()==target){Game.DisplaySubtitle("~g~Target positively identified",1200);GameFiber.Wait(1200);return true;}
                Game.DisplaySubtitle("Keep the sights on the training suspect",200);
                GameFiber.Yield();
            }
            return false;
        }

        private bool Navigate(Vector3 position, float heading, bool sniff, int timeout)
        {
            if (!DogReady()) return false;
            _dog.Tasks.Clear();
            if (sniff) PlaySniff();
            _dog.Tasks.FollowNavigationMeshToPosition(position, heading, 2.2f).WaitForCompletion(timeout);
            return DogReady() && _dog.DistanceTo(position) <= 4f;
        }

        private void PlaySniff(){if(!DogReady())return;_dog.Tasks.PlayAnimation("creatures@rottweiler@indication@","indicate_low",4f,AnimationFlags.None).WaitForCompletion(650);}

        private bool WaitForHandler(string title, string instruction)
        {
            if (!DogReady()) return false;
            Game.DisplayNotification("~b~" + title + "~s~~n~" + instruction);
            var end = Game.GameTime + 20000;
            while (DogReady() && Game.GameTime < end)
            {
                Game.DisplaySubtitle(instruction + "  ~c~(" + Math.Max(0, (int)((end - Game.GameTime) / 1000)) + "s)", 200);
                if (Game.IsKeyDown(Keys.Y)) { GameFiber.Wait(300); return true; }
                GameFiber.Yield();
            }
            return false;
        }

        private void DrawMarkerFor(Vector3 position, int duration){var end=Game.GameTime+(uint)duration;while(Game.GameTime<end){NativeFunction.Natives.DRAW_MARKER(1,position.X,position.Y,position.Z-.9f,0f,0f,0f,0f,0f,0f,1.2f,1.2f,.35f,30,120,220,170,false,false,2,false,0,0,false);GameFiber.Yield();}}
        private static string LevelName(int level)=>level==1?"Basic Obedience":level==2?"Agility / Handler Control":level==3?"Detection Certification":level==4?"Tracking Certification":"Apprehension Certification";
        private static void DeleteAll(List<Rage.Object> props){foreach(var prop in props)if(prop!=null&&prop.Exists())prop.Delete();}
        private bool DogReady()=>_dog!=null&&_dog.Exists()&&!_dog.IsDead;
    }
}
