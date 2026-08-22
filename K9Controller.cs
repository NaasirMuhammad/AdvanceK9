using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Rage;
using Rage.Native;

namespace AdvancedK9
{
    internal sealed class K9Controller : IDisposable
    {
        private readonly ModConfig _config;
        private readonly PolicingRedefinedBridge _pr = new PolicingRedefinedBridge();
        private readonly DogCamera _camera = new DogCamera();
        private readonly ConcurrentQueue<K9Command> _voiceQueue = new ConcurrentQueue<K9Command>();
        private readonly Random _random = new Random();
        private readonly TrustProfile _trust;
        private readonly K9Profile _profile;
        private VoiceCommandService _voice;
        private Ped _dog;
        private Blip _blip;
        private K9State _state = K9State.Dismissed;
        private bool _running = true;
        private bool _pushToTalkHeld;
        private readonly K9Menu _menu = new K9Menu();
        private string _menuMode;
        private string _voiceStatus = "Off";
        private uint _nextVitalsUpdate;

        public K9Controller(ModConfig config)
        {
            _config = config;
            _trust = new TrustProfile(config.StartingTrust);
            _profile = new K9Profile(config);
            if (_config.VoiceEnabled)
            {
                _voice = new VoiceCommandService(_config.VoiceProvider, _config.VoiceModel, _config.VoiceLanguage, _config.VoiceApiKeyEnvironmentVariable, _profile.Name);
                _voice.CommandRecognized += c => _voiceQueue.Enqueue(c);
                _voice.StatusChanged += s => _voiceStatus=s;
                if(_config.ContinuousListening)_voice.StartContinuous();
            }
            _menu.Selected += OnMenuSelected;
        }

        public void Run()
        {
            Game.DisplayNotification("~b~Advanced K9 loaded~s~. Hold ~y~" + _config.ModifierKey + "~s~ + ~y~" + _config.SpawnKey + "~s~ to deploy " + _profile.Name + ".");
            while (_running)
            {
                GameFiber.Yield();
                _menu.Tick();
                DrawHud();
                if (ChordPressed(_config.ModifierKey, _config.SpawnKey)) Execute(K9Command.SpawnDismiss);
                if (ChordPressed(_config.ModifierKey, _config.KennelKey)) ShowKennelMenu();
                if(!_config.ContinuousListening) HandlePushToTalk();
                if (!DogExists()) { DrainVoice(false); continue; }
                if (Game.IsKeyDownRightNow(_config.ModifierKey) && Game.IsKeyDown(_config.CommandKey)) ShowCommandMenu();
                if (Game.IsKeyDownRightNow(_config.ModifierKey) && Game.IsKeyDown(_config.CameraKey)) Execute(K9Command.ToggleCamera);
                if (Game.IsKeyDownRightNow(_config.ModifierKey) && Game.IsKeyDown(_config.LeashKey)) Execute(K9Command.ToggleLeash);
                DrainVoice(true);
                MaintainState();
            }
        }

        private bool ChordPressed(Keys modifier, Keys key) => Game.IsKeyDownRightNow(modifier) && Game.IsKeyDown(key);

        private void HandlePushToTalk()
        {
            if (_voice == null || !_voice.IsAvailable) return;
            bool down = DogExists() && Game.IsKeyDownRightNow(_config.PushToTalkKey);
            if (down && !_pushToTalkHeld) _voice.StartRecording();
            else if (!down && _pushToTalkHeld) _voice.StopAndTranscribe();
            _pushToTalkHeld = down;
        }

        private void DrainVoice(bool dogExists)
        {
            K9Command command;
            while (_voiceQueue.TryDequeue(out command))
            {
                if (dogExists || command == K9Command.SpawnDismiss) Execute(command);
            }
        }

        private void ShowCommandMenu()
        {
            _menuMode="commands"; _menu.Open("ADVANCED K9 — COMMANDS",CommandRegistry.All.Select(x=>x.Label));
        }

        private void ShowKennelMenu()
        {
            _menuMode="profile"; RefreshProfileMenu();
        }

        private void RefreshProfileMenu(){int m=_profile.HudMode;_menu.Open("K9 PROFILE — "+_profile.Name,new[]{"Edit name: "+_profile.Name,"Breed/model: "+_profile.Breed,"Skin/coat: "+(_profile.CoatVariation+1),"Equipment: "+_profile.Vest,"Equipment texture: "+(_profile.VestTexture+1),"Training / certifications","HUD: "+(m==0?"Hidden":m==1?"Compact":"Expanded"),"Move HUD left","Move HUD right","Move HUD up","Move HUD down","HUD scale: "+_profile.HudScale.ToString("0.0"),"Inspect profile"});}
        private void OnMenuSelected(int index){if(_menuMode=="commands"){if(index>=0&&index<CommandRegistry.All.Count)Execute(CommandRegistry.All[index].Command);return;}if(_menuMode!="profile")return;switch(index){case 0:string n=Game.GetUserInput(24);if(!string.IsNullOrWhiteSpace(n)){_profile.SetName(n);_voice?.UpdateWakeWord(_profile.Name);Game.DisplayNotification("~b~Voice wake word changed immediately to:~s~ "+_profile.Name); }break;case 1:bool deployed=DogExists();if(deployed)Dismiss();_profile.NextBreed();if(deployed)Deploy();break;case 2:_profile.NextSkin(_dog);break;case 3:_profile.NextEquipment(_dog);break;case 4:_profile.NextEquipmentTexture(_dog);break;case 5:Execute(K9Command.Training);break;case 6:_profile.CycleHudMode();break;case 7:_profile.MoveHud(-.02f,0);break;case 8:_profile.MoveHud(.02f,0);break;case 9:_profile.MoveHud(0,-.02f);break;case 10:_profile.MoveHud(0,.02f);break;case 11:_profile.ScaleHud();break;case 12:Inspect();break;}RefreshProfileMenu();}

        private void Execute(K9Command command)
        {
            try
            {
                if(_profile.Health<=25 && command!=K9Command.Inspect && command!=K9Command.FirstAid && command!=K9Command.SpawnDismiss){Game.DisplayNotification("~r~K9 REMOVED FROM SERVICE~s~~n~Serious injury requires veterinary treatment. Earned certifications remain saved.");return;}
                if (RequiresTrustCheck(command) && !TrustAllowsCommand(command)) return;
                switch (command)
                {
                    case K9Command.SpawnDismiss: if (DogExists()) Dismiss(); else Deploy(); break;
                    case K9Command.Follow:
                    case K9Command.Heel: Follow(); break;
                    case K9Command.Sit: Sit(); break;
                    case K9Command.LieDown: LieDown(); break;
                    case K9Command.Stay: Stay(); break;
                    case K9Command.Recall: Follow(); break;
                    case K9Command.SearchArea: Search(false); break;
                    case K9Command.SearchVehicle: Search(true); break;
                    case K9Command.Track: Track(); break;
                    case K9Command.Apprehend: Apprehend(); break;
                    case K9Command.Release: Follow(); break;
                    case K9Command.Guard: Guard(); break;
                    case K9Command.Bark: Bark(2); break;
                    case K9Command.EnterVehicle: EnterVehicle(); break;
                    case K9Command.ExitVehicle: ExitVehicle(); break;
                    case K9Command.Fetch: Fetch(); break;
                    case K9Command.Pet: Pet(); break;
                    case K9Command.Feed: Feed(); break;
                    case K9Command.Inspect: Inspect(); break;
                    case K9Command.FirstAid: FirstAid(); break;
                    case K9Command.ToggleLeash: ToggleLeash(); break;
                    case K9Command.ToggleCamera: _camera.Toggle(_dog); break;
                    case K9Command.Training: RunAcademy(); break;
                }
            }
            catch (Exception ex)
            {
                Game.LogTrivial("AdvancedK9 command " + command + " failed: " + ex);
                Game.DisplayNotification("~r~K9 command failed.~s~ See RagePluginHook.log.");
            }
        }

        private bool RequiresTrustCheck(K9Command command)
        {
            return command == K9Command.Sit || command == K9Command.LieDown || command == K9Command.SearchArea || command == K9Command.SearchVehicle ||
                   command == K9Command.Track || command == K9Command.Apprehend || command == K9Command.Fetch;
        }

        private bool TrustAllowsCommand(K9Command command)
        {
            if (command == K9Command.Apprehend && _trust.Level < 25)
            {
                Game.DisplayNotification("~y~K9 trust is too low for safe apprehension training.~s~~n~Pet, feed and train together first.");
                return false;
            }
            GameFiber.Wait(_trust.ResponseDelay);
            double condition=Math.Max(.25,Math.Min(1.0,(_profile.Health/100.0)*(.55+.45*_profile.Stamina/100.0))); if (_random.NextDouble() <= _trust.ObedienceChance*condition) return true;
            Game.DisplayNotification("~o~" + _profile.Name + " hesitated.~s~ Trust " + _trust.Level + "/100 — " + _trust.Rank);
            if (DogExists()) NativeFunction.Natives.TASK_TURN_PED_TO_FACE_ENTITY(_dog, Game.LocalPlayer.Character, 900);
            return false;
        }

        private void Deploy()
        {
            var selectedModel = _profile.ModelName;
            var model = new Model(selectedModel);
            if (!model.IsValid && !string.IsNullOrWhiteSpace(_profile.FallbackModelName))
            {
                Game.LogTrivial("AdvancedK9: " + selectedModel + " unavailable; trying " + _profile.FallbackModelName + " replacement fallback.");
                model = new Model(_profile.FallbackModelName);
            }
            if (!model.IsValid)
            {
                Game.DisplayNotification("~r~Could not load K9 model:~s~ " + selectedModel + "~n~Install its model files or choose another breed.");
                return;
            }
            model.LoadAndWait();
            var officer = Game.LocalPlayer.Character;
            _dog = new Ped(model, officer.GetOffsetPosition(new Vector3(-1.2f, -1.8f, 0f)), officer.Heading);
            model.Dismiss();
            _dog.IsPersistent = true;
            _dog.BlockPermanentEvents = true;
            _dog.RelationshipGroup = officer.RelationshipGroup;
            NativeFunction.Natives.SET_CAN_ATTACK_FRIENDLY(_dog, false, false);
            _profile.Apply(_dog);
            _blip = _dog.AttachBlip();
            _blip.Color = Color.DodgerBlue;
            _blip.Name = "K9 " + _profile.Name;
            _state = K9State.Following;
            _profile.RecordDeployment();
            Follow();
            Game.DisplayNotification("~b~K9 " + _profile.Name + "~s~ is deployed.~n~" + _profile.Breed + " • " + _profile.Vest + " vest~n~Trust: " + _trust.Level + "/100 — " + _trust.Rank);
        }

        private void Follow()
        {
            if (!DogExists()) return;
            _dog.Tasks.Clear();
            NativeFunction.Natives.TASK_FOLLOW_TO_OFFSET_OF_ENTITY(_dog, Game.LocalPlayer.Character, -0.7f, -1.15f, 0f, 2.2f, -1, 1.2f, true);
            _state = K9State.Following;
            Acknowledge("Following.");
        }

        private void Sit()
        {
            PlayDogAnimation("creatures@rottweiler@amb@world_dog_sitting@base", "base", -1, 1);
            _state = K9State.Sitting;
            Acknowledge("Sitting.");
        }

        private void LieDown()
        {
            PlayDogAnimation("creatures@rottweiler@amb@sleep_in_kennel@", "sleep_in_kennel", -1, 1);
            _state = K9State.Lying;
            Acknowledge("Lying down.");
        }

        private void Stay(){if(!DogExists())return;_dog.Tasks.Clear();_state=K9State.Staying;Acknowledge("Staying.");}
        private void Guard(){if(!DogExists())return;_dog.Tasks.Clear();NativeFunction.Natives.TASK_GUARD_CURRENT_POSITION(_dog,25f,25f,true);_state=K9State.Guarding;Acknowledge("Guarding this position.");}

        private void EnterVehicle(){if(!DogExists())return;var handler=Game.LocalPlayer.Character;Vehicle vehicle=handler.CurrentVehicle;if(vehicle==null||!vehicle.Exists()){vehicle=World.GetAllVehicles().Where(v=>v.Exists()&&v.DistanceTo(handler)<8f).OrderBy(v=>v.DistanceTo(handler)).FirstOrDefault();}if(vehicle==null){Game.DisplayNotification("~y~No vehicle nearby.");return;}int[] seats={2,1,0};int seat=0;foreach(int candidate in seats)if(NativeFunction.Natives.IS_VEHICLE_SEAT_FREE<bool>(vehicle,candidate,false)){seat=candidate;break;}NativeFunction.Natives.TASK_ENTER_VEHICLE(_dog,vehicle,8000,seat,2f,1,0);_state=K9State.InVehicle;Acknowledge("Loading into vehicle.");}
        private void ExitVehicle(){if(!DogExists())return;if(_dog.CurrentVehicle!=null)NativeFunction.Natives.TASK_LEAVE_VEHICLE(_dog,_dog.CurrentVehicle,0);GameFiber.Wait(800);Follow();}

        private void Inspect(){Game.DisplayNotification("~b~K9 "+_profile.Name+" — FIELD INSPECTION~s~~n~Health: "+_profile.Health+"%  Stamina: "+_profile.Stamina+"%~n~Injury: "+_profile.Injury+"~n~Trust: "+_profile.Trust+"  XP: "+_profile.TrainingXp+"~n~Certifications: "+Certifications());}
        private string Certifications(){string s="";if(_profile.ObedienceCertified)s+="OB ";if(_profile.DetectionCertified)s+="DET ";if(_profile.TrackingCertified)s+="TRK ";if(_profile.ApprehensionCertified)s+="APP ";return s.Length==0?"In training":s.Trim();}
        private void FirstAid(){if(!DogExists())return;if(_profile.Health>=95){Game.DisplayNotification("~g~No field treatment required.");return;}Sit();GameFiber.Wait(1800);_profile.FirstAid();_dog.Health=Math.Max(_dog.Health,(int)(_dog.MaxHealth*_profile.Health/100f));_profile.ChangeTrust(2);Game.DisplayNotification("~g~Field first aid applied.~s~~n~Serious injuries still require veterinary care.");}

        private void Search(bool vehicleOnly=false)
        {
            var officer = Game.LocalPlayer.Character;
            Entity target = vehicleOnly ? (Entity)World.GetAllVehicles().Where(v=>v.Exists()&&v.DistanceTo(officer)<=_config.SearchRadius).OrderBy(v=>v.DistanceTo(officer)).FirstOrDefault() : FindSearchTarget(officer.Position, _config.SearchRadius);
            if (target == null)
            {
                Game.DisplayNotification("~y~No nearby pedestrian or vehicle to search.");
                return;
            }
            _state = K9State.Searching;
            _dog.Tasks.Clear();
            _dog.Tasks.FollowNavigationMeshToPosition(target.GetOffsetPosition(new Vector3(0f, -1f, 0f)), target.Heading, 2f).WaitForCompletion(9000);
            if (!DogExists() || !target.Exists()) { Follow(); return; }
            for (var i = 0; i < 3; i++)
            {
                PlayDogAnimation("creatures@rottweiler@indication@", "indicate_high", 1300, 0);
                GameFiber.Wait(1350);
            }
            var positive = _pr.HasK9Odor(target) ?? (_random.NextDouble() < _config.PositiveChance);
            if (positive && _random.NextDouble() > _trust.DetectionReliability)
            {
                Game.DisplayNotification("~o~Uncertain K9 response.~s~ Build trust and repeat the search.");
                Follow();
                return;
            }
            if (positive)
            {
                Sit();
                Bark(3);
                Game.DisplayNotification("~r~POSITIVE K9 INDICATION~s~ — " + TargetLabel(target) + ".");
                _trust.Change(1, "successful detection");
                _profile.RecordSearch();
            }
            else
            {
                Bark(1);
                Game.DisplayNotification("~g~No K9 indication~s~ on " + TargetLabel(target) + ".");
                Follow();
            }
        }

        private void Track()
        {
            var target = FindNearestPed(_config.TrackRadius, true);
            if (target == null)
            {
                Game.DisplayNotification("~y~No trackable person nearby. Face the wanted/missing person and retry.");
                return;
            }
            _state = K9State.Tracking;
            Game.DisplayNotification("~b~K9 tracking started.~s~ Keep pace with " + _profile.Name + ".");
            var end = Game.GameTime + 120000;
            while (_running && DogExists() && target.Exists() && !target.IsDead && Game.GameTime < end && _state == K9State.Tracking)
            {
                _dog.Tasks.FollowNavigationMeshToPosition(target.Position, target.Heading, 2.8f);
                NativeFunction.Natives.DRAW_LINE(_dog.Position.X, _dog.Position.Y, _dog.Position.Z + .25f, target.Position.X, target.Position.Y, target.Position.Z + .25f, 30, 120, 255, 90);
                if (_dog.DistanceTo(target) < 3f)
                {
                    Bark(2);
                    Sit();
                    Game.DisplayNotification("~g~Track complete.~s~ Person located.");
                    _trust.Change(2, "successful track");
                    return;
                }
                GameFiber.Wait(750);
            }
            Follow();
        }

        private void Apprehend()
        {
            var target = FindNearestPed(35f, true);
            if (target == null) { Game.DisplayNotification("~y~No suspect selected nearby."); return; }
            _state = K9State.Apprehending;
            _dog.Tasks.Clear();
            NativeFunction.Natives.TASK_COMBAT_PED(_dog, target, 0, 16);
            Game.DisplayNotification("~o~K9 deployed. Recall automatically engages at the safety threshold.");
            var end = Game.GameTime + 25000;
            while (DogExists() && target.Exists() && !target.IsDead && Game.GameTime < end && _state == K9State.Apprehending)
            {
                if (target.Health <= _config.NonLethalHealthFloor || target.IsRagdoll)
                {
                    _dog.Tasks.ClearImmediately();
                    if (target.Health < _config.NonLethalHealthFloor) target.Health = _config.NonLethalHealthFloor;
                    NativeFunction.Natives.TASK_HANDS_UP(target, -1, Game.LocalPlayer.Character, -1, true);
                    Game.DisplayNotification("~g~Suspect neutralized without lethal force.~s~ Move in for arrest.");
                    _trust.Change(1, "controlled apprehension");
                    Follow();
                    return;
                }
                GameFiber.Yield();
            }
            Follow();
        }

        private void Fetch()
        {
            _state = K9State.Fetching;
            var officer = Game.LocalPlayer.Character;
            var ballModel = new Model("w_am_baseball");
            if (!ballModel.IsValid) { Follow(); return; }
            ballModel.LoadAndWait();
            var landing = officer.GetOffsetPosition(new Vector3(0f, 9f, .5f));
            var ball = new Rage.Object(ballModel, landing);
            ballModel.Dismiss();
            ball.IsPersistent = true;
            _dog.Tasks.FollowNavigationMeshToPosition(landing, officer.Heading, 3f).WaitForCompletion(9000);
            if (DogExists() && ball.Exists())
            {
                NativeFunction.Natives.ATTACH_ENTITY_TO_ENTITY(ball, _dog, 0, 0f, .38f, .28f, 0f, 0f, 0f, false, false, false, false, 2, true);
                _dog.Tasks.FollowNavigationMeshToPosition(officer.GetOffsetPosition(new Vector3(0f, 1.2f, 0f)), officer.Heading, 2.5f).WaitForCompletion(9000);
                NativeFunction.Natives.DETACH_ENTITY(ball, true, true);
                ball.Position = officer.GetOffsetPosition(new Vector3(.5f, .7f, 0f));
                GameFiber.Wait(1200);
                ball.Delete();
            }
            Follow();
        }

        private void Pet()
        {
            if (_dog.DistanceTo(Game.LocalPlayer.Character) > 2.2f) { Game.DisplayNotification("~y~Move closer to your K9."); return; }
            Sit();
            NativeFunction.Natives.TASK_TURN_PED_TO_FACE_ENTITY(Game.LocalPlayer.Character, _dog, 750);
            GameFiber.Wait(750);
            NativeFunction.Natives.TASK_START_SCENARIO_IN_PLACE(Game.LocalPlayer.Character, "WORLD_HUMAN_CROUCH_INSPECT", 0, true);
            GameFiber.Wait(2500);
            Game.LocalPlayer.Character.Tasks.Clear();
            Game.DisplayNotification("~b~" + _profile.Name + "~s~ enjoyed that.");
            _trust.Change(2, "handler bonding");
            _profile.ChangeTrust(2);_profile.Recover(3);
        }

        private void Feed()
        {
            if (_dog.DistanceTo(Game.LocalPlayer.Character) > 2.5f) { Game.DisplayNotification("~y~Move closer to your K9."); return; }
            Sit();
            Game.DisplayNotification("~b~You fed " + _profile.Name + " a treat.~s~ Health restored.");
            _dog.Health = _dog.MaxHealth;
            Bark(1);
            _trust.Change(2, "care and feeding");
            _profile.ChangeTrust(2);_profile.Recover(15);
        }

        private void ToggleLeash()
        {
            if (_state == K9State.Leashed) { Follow(); Game.DisplayNotification("~b~K9 leash removed."); return; }
            _dog.Tasks.Clear();
            _state = K9State.Leashed;
            Game.DisplayNotification("~b~K9 leash attached.~s~ Movement is limited to two metres.");
        }

        private void MaintainState()
        {
            if (!DogExists()) { _state = K9State.Dismissed; _camera.Disable(); return; }
            EnforceHandlerSafety();
            if(Game.GameTime>=_nextVitalsUpdate){_nextVitalsUpdate=Game.GameTime+5000;int liveHealth=(int)(100f*_dog.Health/Math.Max(1,_dog.MaxHealth));if(liveHealth<_profile.Health){string injury=liveHealth<=25?"Serious — veterinary treatment required":liveHealth<=55?"Moderate":"Minor";_profile.SetInjury(injury,liveHealth);}if(_state==K9State.Searching||_state==K9State.Tracking||_state==K9State.Apprehending)_profile.UseStamina(2);else _profile.Recover(1);}
            if (_state == K9State.Leashed)
            {
                var officer = Game.LocalPlayer.Character;
                NativeFunction.Natives.DRAW_LINE(officer.Position.X, officer.Position.Y, officer.Position.Z + .65f, _dog.Position.X, _dog.Position.Y, _dog.Position.Z + .45f, 65, 45, 25, 220);
                if (_dog.DistanceTo(officer) > 1.9f)
                    _dog.Tasks.FollowNavigationMeshToPosition(officer.GetOffsetPosition(new Vector3(-.5f, -.9f, 0f)), officer.Heading, 1.5f);
            }
            if (_dog.DistanceTo(Game.LocalPlayer.Character) > 75f && _state == K9State.Following)
                _dog.Position = Game.LocalPlayer.Character.GetOffsetPosition(new Vector3(-1f, -2f, 0f));
        }

        private void DrawHud(){int mode=_profile.HudMode;if(mode==0)return;float x=_profile.HudX,y=_profile.HudY,s=_profile.HudScale,w=.245f*s,h=(mode==1?.074f:.142f)*s;NativeFunction.Natives.DRAW_RECT(x,y,w,h,5,16,25,210);DrawText("K9 "+_profile.Name+"  |  "+_state,x-w/2+.01f,y-h/2+.008f,.31f*s);DrawText("HP "+_profile.Health+"  STA "+_profile.Stamina+"  VOICE "+_voiceStatus,x-w/2+.01f,y-h/2+.040f*s,.255f*s);if(mode==2){DrawText("Trust "+_profile.Trust+"  XP "+_profile.TrainingXp+"  "+Certifications(),x-w/2+.01f,y-h/2+.072f*s,.255f*s);DrawText("Injury: "+_profile.Injury,x-w/2+.01f,y-h/2+.102f*s,.255f*s);}}
        private static void DrawText(string value,float x,float y,float scale){NativeFunction.Natives.SET_TEXT_FONT(0);NativeFunction.Natives.SET_TEXT_SCALE(scale,scale);NativeFunction.Natives.SET_TEXT_COLOUR(235,245,255,255);NativeFunction.Natives.SET_TEXT_OUTLINE();NativeFunction.Natives.BEGIN_TEXT_COMMAND_DISPLAY_TEXT("STRING");NativeFunction.Natives.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME(value);NativeFunction.Natives.END_TEXT_COMMAND_DISPLAY_TEXT(x,y);}

        private void EnforceHandlerSafety()
        {
            var handler = Game.LocalPlayer.Character;
            NativeFunction.Natives.SET_CAN_ATTACK_FRIENDLY(_dog, false, false);
            if (NativeFunction.Natives.IS_PED_IN_COMBAT<bool>(_dog, handler))
            {
                _dog.Tasks.ClearImmediately();
                _dog.RelationshipGroup = handler.RelationshipGroup;
                NativeFunction.Natives.SET_CAN_ATTACK_FRIENDLY(_dog, false, false);
                _state = K9State.Following;
                NativeFunction.Natives.TASK_FOLLOW_TO_OFFSET_OF_ENTITY(_dog, handler, -0.7f, -1.15f, 0f, 2.2f, -1, 1.2f, true);
                Game.LogTrivial("AdvancedK9: handler-safety interlock recalled dog from invalid combat state.");
            }
        }

        private void RunAcademy()
        {
            _state = K9State.Academy;
            var academy = new AcademySession(_dog, _profile.Name);
            academy.Run(Sit, LieDown, Follow, ()=>Search(false));
            _trust.Change(5, "academy training");
            _profile.AddXp(15);
            Follow();
        }

        private Entity FindSearchTarget(Vector3 center, float radius)
        {
            var vehicle = World.GetAllVehicles().Where(v => v.Exists() && v.DistanceTo(center) <= radius).OrderBy(v => v.DistanceTo(center)).FirstOrDefault();
            var ped = FindNearestPed(radius, false);
            if (vehicle == null) return ped;
            if (ped == null) return vehicle;
            return vehicle.DistanceTo(center) < ped.DistanceTo(center) ? (Entity)vehicle : ped;
        }

        private Ped FindNearestPed(float radius, bool excludeCops)
        {
            var officer = Game.LocalPlayer.Character;
            return World.GetAllPeds().Where(p => p.Exists() && p != officer && p != _dog && !p.IsDead && p.DistanceTo(officer) <= radius && (!excludeCops || !LspdfrBridge.IsPedCop(p)))
                .OrderBy(p => p.DistanceTo(officer)).FirstOrDefault();
        }

        private void Bark(int count)
        {
            for (var i = 0; i < count && DogExists(); i++)
            {
                NativeFunction.Natives.PLAY_PED_AMBIENT_SPEECH_NATIVE(_dog, "BARK", "SPEECH_PARAMS_FORCE");
                GameFiber.Wait(450);
            }
        }

        private void PlayDogAnimation(string dictionary, string name, int duration, int flags)
        {
            _dog.Tasks.Clear();
            _dog.Tasks.PlayAnimation(dictionary, name, 4f, (AnimationFlags)flags).WaitForCompletion(duration > 0 ? duration : 1);
        }

        private void Acknowledge(string text) => Game.DisplayNotification("~b~K9 " + _profile.Name + ":~s~ " + text);
        private string TargetLabel(Entity e) => e is Vehicle ? "vehicle" : "person";
        private bool DogExists() => _dog != null && _dog.Exists() && !_dog.IsDead;

        private void Dismiss()
        {
            _camera.Disable();
            if (_blip != null && _blip.Exists()) _blip.Delete();
            if (_dog != null && _dog.Exists()) _dog.Dismiss();
            _dog = null;
            _state = K9State.Dismissed;
            Game.DisplayNotification("~b~Advanced K9 dismissed.");
        }

        public void Dispose()
        {
            _running = false;
            _voice?.Dispose();
            _voice = null;
            _trust.Save();
            _profile.Save();
            Dismiss();
        }
    }
}
