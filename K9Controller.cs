using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.IO;
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
        private readonly VehicleSeatProfiles _seatProfiles;
        private VoiceCommandService _voice;
        private Ped _dog;
        private Vehicle _dogVehicle;
        private bool _dogSeatAttached;
        private Blip _blip;
        private K9State _state = K9State.Dismissed;
        private bool _running = true;
        private bool _pushToTalkHeld;
        private readonly K9Menu _menu = new K9Menu();
        private string _menuMode;
        private string _voiceStatus = "Off";
        private bool _voiceActive;
        private bool _onDuty;
        private uint _nextDutyDiagnostic;
        private uint _nextDutyLogRead;
        private long _dutyLogPosition;
        private bool? _lspdfrDutyFromLog;
        private uint _nextVitalsUpdate;
        private Ped _voiceAimedTarget;
        private Ped _scentTarget;
        private uint _scentCollectedAt;
        private float _scentRainAtCollection;
        private uint _nextEnvironmentUpdate;
        private uint _nextHeatWarning;
        private int _dogVehicleDoor=3;
        private readonly Queue<Vector3> _handlerTrail = new Queue<Vector3>();
        private uint _nextTrailSample;
        private uint _nextTrailRecovery;
        private int _leashRope = -1;
        private uint _nextLeashFollow;
        private uint _nextNeedsUpdate;
        private uint _nextNeedsWarning;
        private VehicleSeatProfile _activeSeatProfile;

        public K9Controller(ModConfig config)
        {
            _config = config;
            _trust = new TrustProfile(config.StartingTrust);
            _profile = new K9Profile(config);
            _seatProfiles = new VehicleSeatProfiles(config.VehicleSeatOffsetX,config.VehicleSeatOffsetY,config.VehicleSeatOffsetZ);
            _menu.Selected += OnMenuSelected;
            _menu.Adjusted += OnMenuAdjusted;
        }

        public void Run()
        {
            while (_running)
            {
                GameFiber.Yield();
                bool dutyNow = IsPlayerOnDuty();
                if (dutyNow != _onDuty)
                {
                    _onDuty = dutyNow;
                    if (_onDuty) ActivateForDuty(); else DeactivateForDuty();
                }
                if (!_onDuty) continue;
                _voice?.Tick();
                _menu.Tick();
                DrawHud();
                if (ChordPressed(_config.ModifierKey, _config.SpawnKey)) Execute(K9Command.SpawnDismiss);
                if (ChordPressed(_config.ModifierKey, _config.KennelKey)) ShowKennelMenu();
                if (Game.IsKeyDownRightNow(_config.ModifierKey) && Game.IsKeyDown(_config.CommandKey)) ShowCommandMenu();
                HandlePushToTalk();
                if (!DogExists()) { DrainVoice(false); continue; }
                if (Game.IsKeyDownRightNow(_config.ModifierKey) && Game.IsKeyDown(_config.CameraKey)) Execute(K9Command.ToggleCamera);
                if (Game.IsKeyDownRightNow(_config.ModifierKey) && Game.IsKeyDown(_config.LeashKey)) Execute(K9Command.ToggleLeash);
                DrainVoice(true);
                MaintainState();
            }
        }

        private bool IsPlayerOnDuty()
        {
            UpdateLspdfrDutyLogState();
            bool nativeCop = false;
            uint current = 0, police = 0;
            try
            {
                var player = Game.LocalPlayer.Character;
                if (player != null && player.Exists())
                {
                    current = NativeFunction.Natives.GET_PED_RELATIONSHIP_GROUP_HASH<uint>(player);
                    police = NativeFunction.Natives.GET_HASH_KEY<uint>("COP");
                    nativeCop = NativeFunction.CallByHash<bool>(0x12534C348C6CB68B, player);
                }
            }
            catch (Exception ex)
            {
                if (Game.GameTime >= _nextDutyDiagnostic)
                    Game.LogTrivial("AdvancedK9 duty native probe unavailable; continuing with LSPDFR log signal: " + ex.Message);
            }
            // Native cop state is not a valid duty signal: LSPDFR can mark the player as a
            // cop while its duty controller is still off duty. Only LSPDFR's own state wins.
            bool detected = _lspdfrDutyFromLog == true;
            if (Game.GameTime >= _nextDutyDiagnostic)
            {
                _nextDutyDiagnostic = Game.GameTime + 15000;
                Game.LogTrivial("AdvancedK9 duty probe: lspdfrLog=" + (_lspdfrDutyFromLog.HasValue ? _lspdfrDutyFromLog.Value.ToString() : "unknown") + ", nativeCop=" + nativeCop + ", relationship=0x" + current.ToString("X8") + ", active=" + detected + ".");
            }
            return detected;
        }

        private void UpdateLspdfrDutyLogState()
        {
            if (Game.GameTime < _nextDutyLogRead) return;
            _nextDutyLogRead = Game.GameTime + 500;
            try
            {
                const string path = "RagePluginHook.log";
                if (!File.Exists(path)) return;
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    if (_dutyLogPosition == 0) _dutyLogPosition = Math.Max(0, stream.Length - 262144);
                    if (_dutyLogPosition > stream.Length) _dutyLogPosition = 0;
                    stream.Seek(_dutyLogPosition, SeekOrigin.Begin);
                    string added;
                    using (var reader = new StreamReader(stream))
                    {
                        added = reader.ReadToEnd();
                        _dutyLogPosition = stream.Length;
                    }
                    int on = added.LastIndexOf("Player went on duty", StringComparison.OrdinalIgnoreCase);
                    int off = added.LastIndexOf("Player went off duty", StringComparison.OrdinalIgnoreCase);
                    off = Math.Max(off, added.LastIndexOf("Player is going off duty", StringComparison.OrdinalIgnoreCase));
                    off = Math.Max(off, added.LastIndexOf("Player is off duty", StringComparison.OrdinalIgnoreCase));
                    off = Math.Max(off, added.LastIndexOf("Going off duty", StringComparison.OrdinalIgnoreCase));
                    off = Math.Max(off, added.LastIndexOf("LSPDFR has shut down", StringComparison.OrdinalIgnoreCase));
                    if (on >= 0 || off >= 0) _lspdfrDutyFromLog = on > off;
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private void ActivateForDuty()
        {
            if (_voice == null) InitializeVoice();
            Game.DisplayNotification("~b~Advanced K9 Beta active~s~. Hold ~y~" + _config.ModifierKey + "~s~ + ~y~" + _config.SpawnKey + "~s~ to deploy " + _profile.Name + ".");
        }

        private void DeactivateForDuty()
        {
            _menu.Close();
            _voice?.StopListening();
            _voiceActive = false;
            _voiceStatus = "Off duty";
            if (DogExists()) Dismiss(false);
            Game.LogTrivial("AdvancedK9: player is off duty; K9, UI and voice are inactive.");
        }

        private bool ChordPressed(Keys modifier, Keys key) => Game.IsKeyDownRightNow(modifier) && Game.IsKeyDown(key);

        private void HandlePushToTalk()
        {
            if (!_voiceActive || _voice == null || !_voice.IsAvailable) return;
            bool down = DogExists() && Game.IsKeyDownRightNow(_config.PushToTalkKey);
            if (down && !_pushToTalkHeld) { _voiceAimedTarget=GetValidAimedSuspect(false); _voice.StartRecording(); }
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
            _menuMode="commands_root";_menu.Open("ADVANCED K9 — COMMANDS",new[]{"Partner Control","Search & Detection","Tracking & Scent","Tactical Deployment","Vehicle & Equipment","Care & Medical","Training & Certifications","Deploy / Dismiss",VoiceMenuLabel()});
        }

        private static readonly K9Command[][] CommandGroups={
            new[]{K9Command.Follow,K9Command.Heel,K9Command.Sit,K9Command.LieDown,K9Command.Stay,K9Command.Recall,K9Command.WhistleRecall,K9Command.HandSignal,K9Command.Fetch,K9Command.Pet},
            new[]{K9Command.SearchArea,K9Command.SearchVehicle,K9Command.SearchNarcotics,K9Command.SearchExplosives,K9Command.SearchWeapons},
            new[]{K9Command.CollectScent,K9Command.Track},
            new[]{K9Command.Apprehend,K9Command.DoorPop,K9Command.Release,K9Command.Guard,K9Command.Bark},
            new[]{K9Command.EnterVehicle,K9Command.ExitVehicle,K9Command.ToggleLeash,K9Command.ToggleCamera,K9Command.Restock},
            new[]{K9Command.Feed,K9Command.Drink,K9Command.Rest,K9Command.Inspect,K9Command.FirstAid,K9Command.VeterinaryCare},
            new[]{K9Command.Training,K9Command.TrainNarcotics,K9Command.TrainExplosives,K9Command.TrainWeapons}};
        private static readonly string[] CommandGroupTitles={"PARTNER CONTROL","SEARCH & DETECTION","TRACKING & SCENT","TACTICAL DEPLOYMENT","VEHICLE & EQUIPMENT","CARE & MEDICAL","TRAINING & CERTIFICATIONS"};
        private void OpenCommandGroup(int group){_menuMode="commands_group_"+group;var labels=CommandGroups[group].Select(CommandLabel).Concat(new[]{"← Back to Command Categories"});_menu.Open("ADVANCED K9 — "+CommandGroupTitles[group],labels);}
        private static string CommandLabel(K9Command command){var definition=CommandRegistry.All.FirstOrDefault(x=>x.Command==command);return definition==null?command.ToString():definition.Label;}

        private void ShowKennelMenu()
        {
            _menuMode="profile"; RefreshProfileMenu();
        }

        private void RefreshProfileMenu(){int m=_profile.HudMode;_menu.Update("K9 PROFILE — "+_profile.Name,new[]{"Edit name: "+_profile.Name,"Breed/model: "+_profile.Breed,"Skin/coat: "+(_profile.CoatVariation+1),"Equipment: "+_profile.Vest,"Vest texture: "+_profile.VestTextureName(_dog),"HUD: "+(m==0?"Hidden":m==1?"Compact":"Expanded"),"Move HUD left","Move HUD right","Move HUD up","Move HUD down","HUD scale: "+_profile.HudScale.ToString("0.0"),"Vehicle Seat Configuration","Inspect Profile / Certifications",VoiceMenuLabel()});}
        private void OnMenuSelected(int index){if(_menuMode=="commands_root"){if(index>=0&&index<7){OpenCommandGroup(index);return;}if(index==7){Execute(K9Command.SpawnDismiss);return;}if(index==8)ToggleVoice();return;}if(_menuMode!=null&&_menuMode.StartsWith("commands_group_")){int group;if(!int.TryParse(_menuMode.Substring(15),out group)||group<0||group>=CommandGroups.Length)return;if(index>=0&&index<CommandGroups[group].Length)Execute(CommandGroups[group][index]);else ShowCommandMenu();return;}if(_menuMode=="seat_config"){HandleSeatMenu(index);return;}if(_menuMode!="profile")return;switch(index){case 0:string n=PromptForDogName(24);if(!string.IsNullOrWhiteSpace(n)){_profile.SetName(n);_voice?.UpdateWakeWord(_profile.Name);Game.DisplayNotification("~b~Voice wake word changed immediately to:~s~ "+_profile.Name);}break;case 1:PreviewBreed(1);break;case 2:_profile.NextSkin(_dog);break;case 3:_profile.NextEquipment(_dog);break;case 4:_profile.NextEquipmentTexture(_dog);break;case 5:_profile.CycleHudMode();break;case 6:_profile.MoveHud(-.02f,0);break;case 7:_profile.MoveHud(.02f,0);break;case 8:_profile.MoveHud(0,-.02f);break;case 9:_profile.MoveHud(0,.02f);break;case 10:_profile.ScaleHud();break;case 11:OpenSeatConfiguration();return;case 12:Inspect();break;case 13:ToggleVoice();break;}RefreshProfileMenu();}

        private void InitializeVoice(){_voice=new VoiceCommandService(_config.VoiceProvider,_config.VoiceModel,_config.VoiceLanguage,_config.VoiceApiKey,_config.VoiceApiKeyEnvironmentVariable,_profile.Name);_voice.CommandRecognized+=c=>_voiceQueue.Enqueue(c);_voice.StatusChanged+=s=>_voiceStatus=s;_voiceActive=_config.VoiceEnabled&&_voice.IsAvailable;_voiceStatus=_voice.IsAvailable?"Ready (hold V)":"Key missing";}
        private string VoiceMenuLabel()=>"Voice microphone: "+(_voice==null||!_voice.IsAvailable?"UNAVAILABLE — add ApiKey in INI":_voiceActive?"ON — hold "+_config.PushToTalkKey:"OFF — select to activate");
        private void ToggleVoice(){if(_voice==null)InitializeVoice();if(!_voice.IsAvailable){Game.DisplayNotification("~r~Voice cannot activate.~s~~n~Add your provider key after ~y~ApiKey=~s~ in AdvancedK9.ini, then reload the plugin.");return;}_voiceActive=!_voiceActive;if(_voiceActive){_voiceStatus="Ready (hold V)";Game.DisplayNotification("~g~K9 push-to-talk activated.~s~ Hold "+_config.PushToTalkKey+" while speaking.");}else{_voice.StopListening();_voiceStatus="Off";Game.DisplayNotification("~y~K9 voice microphone disabled.");}}

        private void OnMenuAdjusted(int index,int delta){if(_menuMode=="seat_config"){AdjustSeat(index,delta);return;}if(_menuMode!="profile")return;switch(index){case 1:PreviewBreed(delta);break;case 2:_profile.AdjustSkin(_dog,delta);break;case 3:_profile.AdjustEquipment(_dog,delta);break;case 4:_profile.AdjustEquipmentTexture(_dog,delta);break;case 5:_profile.CycleHudMode();break;case 6:case 7:_profile.MoveHud(delta*.01f,0);break;case 8:case 9:_profile.MoveHud(0,delta*.01f);break;case 10:_profile.ScaleHud();break;default:return;}RefreshProfileMenu();}

        private static string PromptForDogName(int maxLength)
        {
            NativeFunction.Natives.DISPLAY_ONSCREEN_KEYBOARD(1, "FMMC_KEY_TIP8", "", "", "", "", "", maxLength + 1);
            int status;
            while ((status = NativeFunction.Natives.UPDATE_ONSCREEN_KEYBOARD<int>()) == 0) GameFiber.Yield();
            return status == 1 ? NativeFunction.Natives.GET_ONSCREEN_KEYBOARD_RESULT<string>() : null;
        }

        private void OpenSeatConfiguration()
        {
            if(!DogExists()||_state!=K9State.InVehicle||_dogVehicle==null||!_dogVehicle.Exists()){Game.DisplayNotification("~y~Load the K9 into the vehicle before calibrating its seat.");return;}
            _activeSeatProfile=_seatProfiles.Get(_dogVehicle);_menuMode="seat_config";RefreshSeatMenu();
        }
        private void RefreshSeatMenu(){if(_activeSeatProfile==null)return;_menu.Update("SEAT — "+_seatProfiles.VehicleName(_dogVehicle),new[]{"X left/right: "+_activeSeatProfile.X.ToString("0.000"),"Y forward/back: "+_activeSeatProfile.Y.ToString("0.000"),"Z up/down: "+_activeSeatProfile.Z.ToString("0.000"),"Save for this vehicle model","Reset to global defaults","← Back to K9 Profile"});}
        private void AdjustSeat(int index,int delta){if(index<0||index>2||_activeSeatProfile==null)return;float step=.02f*delta;if(index==0)_activeSeatProfile.X+=step;else if(index==1)_activeSeatProfile.Y+=step;else _activeSeatProfile.Z+=step;ApplySeatCalibration();RefreshSeatMenu();}
        private void HandleSeatMenu(int index){if(index==3){_seatProfiles.Save(_dogVehicle,_activeSeatProfile);Game.DisplayNotification("~g~Seat position saved for "+_seatProfiles.VehicleName(_dogVehicle)+".~s~~n~This model will use the calibration automatically.");RefreshSeatMenu();}else if(index==4){_activeSeatProfile=new VehicleSeatProfile(_config.VehicleSeatOffsetX,_config.VehicleSeatOffsetY,_config.VehicleSeatOffsetZ);ApplySeatCalibration();RefreshSeatMenu();}else if(index==5){_menuMode="profile";RefreshProfileMenu();}}
        private void ApplySeatCalibration(){if(_dog==null||!_dog.Exists()||_dogVehicle==null||!_dogVehicle.Exists()||_activeSeatProfile==null)return;string boneName=_dogVehicleDoor==2?"seat_dside_r":_dogVehicleDoor==3?"seat_pside_r":"seat_pside_f";int bone=NativeFunction.Natives.GET_ENTITY_BONE_INDEX_BY_NAME<int>(_dogVehicle,boneName);if(bone<0)return;NativeFunction.Natives.DETACH_ENTITY(_dog,true,true);NativeFunction.Natives.ATTACH_ENTITY_TO_ENTITY(_dog,_dogVehicle,bone,_activeSeatProfile.X,_activeSeatProfile.Y,_activeSeatProfile.Z,0f,0f,0f,false,false,false,false,2,true);_dogSeatAttached=true;}

        private void Execute(K9Command command)
        {
            try
            {
                if(_state==K9State.Leashed&&(command==K9Command.SearchArea||command==K9Command.SearchVehicle||command==K9Command.SearchNarcotics||command==K9Command.SearchExplosives||command==K9Command.SearchWeapons||command==K9Command.Track||command==K9Command.Apprehend||command==K9Command.Fetch)){DeleteLeashRope();_state=K9State.Following;Game.DisplayNotification("~b~Leash automatically released for K9 deployment.");}
                if(_state==K9State.Leashed&&(command==K9Command.Training||command==K9Command.TrainNarcotics||command==K9Command.TrainExplosives||command==K9Command.TrainWeapons)){Game.DisplayNotification("~y~Remove the leash before traveling to the academy.");return;}
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
                    case K9Command.WhistleRecall: WhistleRecall(); break;
                    case K9Command.HandSignal: HandSignal(); break;
                    case K9Command.SearchArea: Search(false); break;
                    case K9Command.SearchVehicle: Search(true); break;
                    case K9Command.SearchNarcotics: Search(false,DetectionSpecialty.Narcotics); break;
                    case K9Command.SearchExplosives: Search(false,DetectionSpecialty.Explosives); break;
                    case K9Command.SearchWeapons: Search(false,DetectionSpecialty.Weapons); break;
                    case K9Command.CollectScent: CollectScent(); break;
                    case K9Command.Track: Track(); break;
                    case K9Command.Apprehend: Apprehend(); break;
                    case K9Command.DoorPop: DoorPop(); break;
                    case K9Command.Release: Follow(); break;
                    case K9Command.Guard: Guard(); break;
                    case K9Command.Bark: Bark(2); break;
                    case K9Command.EnterVehicle: EnterVehicle(); break;
                    case K9Command.ExitVehicle: ExitVehicle(); break;
                    case K9Command.Fetch: Fetch(); break;
                    case K9Command.Pet: Pet(); break;
                    case K9Command.Feed: Feed(); break;
                    case K9Command.Drink: Drink(); break;
                    case K9Command.Rest: Rest(); break;
                    case K9Command.Inspect: Inspect(); break;
                    case K9Command.FirstAid: FirstAid(); break;
                    case K9Command.VeterinaryCare: VeterinaryCare(); break;
                    case K9Command.Restock: Restock(); break;
                    case K9Command.ToggleLeash: ToggleLeash(); break;
                    case K9Command.ToggleCamera: _camera.Toggle(_dog); break;
                    case K9Command.Training: RunAcademy(); break;
                    case K9Command.TrainNarcotics: RunAcademySpecialty(DetectionSpecialty.Narcotics); break;
                    case K9Command.TrainExplosives: RunAcademySpecialty(DetectionSpecialty.Explosives); break;
                    case K9Command.TrainWeapons: RunAcademySpecialty(DetectionSpecialty.Weapons); break;
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
            return command == K9Command.Sit || command == K9Command.LieDown || command == K9Command.SearchArea || command == K9Command.SearchVehicle || command==K9Command.SearchNarcotics || command==K9Command.SearchExplosives || command==K9Command.SearchWeapons ||
                   command == K9Command.Track || command == K9Command.Fetch;
        }

        private bool TrustAllowsCommand(K9Command command)
        {
            if (command == K9Command.Apprehend && _trust.Level < 25)
            {
                Game.DisplayNotification("~y~K9 trust is too low for safe apprehension training.~s~~n~Pet, feed and train together first.");
                return false;
            }
            GameFiber.Wait(_trust.ResponseDelay);
            double condition=Math.Max(.25,Math.Min(1.0,(_profile.Health/100.0)*(.55+.45*_profile.Stamina/100.0)*_profile.NeedsFactor)); if (_random.NextDouble() <= _trust.ObedienceChance*condition) return true;
            Game.DisplayNotification("~o~" + _profile.Name + " hesitated.~s~ Trust " + _trust.Level + "/100 — " + _trust.Rank);
            if (DogExists()) NativeFunction.Natives.TASK_TURN_PED_TO_FACE_ENTITY(_dog, Game.LocalPlayer.Character, 900);
            return false;
        }

        private void Deploy()
        {
            var officer = Game.LocalPlayer.Character;
            if(!CreateDogAt(officer.GetOffsetPosition(new Vector3(-1.2f,-1.8f,0f)),officer.Heading))return;
            _profile.RecordDeployment();
            Follow();
            Game.DisplayNotification("~b~K9 " + _profile.Name + "~s~ is deployed.~n~" + _profile.Breed + " • " + _profile.Vest + " vest~n~Trust: " + _trust.Level + "/100 — " + _trust.Rank);
        }

        private bool CreateDogAt(Vector3 position,float heading)
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
                return false;
            }
            model.LoadAndWait();
            var officer = Game.LocalPlayer.Character;
            _dog = new Ped(model, position, heading);
            model.Dismiss();
            NativeFunction.Natives.RESURRECT_PED(_dog);
            NativeFunction.Natives.CLEAR_PED_TASKS_IMMEDIATELY(_dog);
            _profile.PrepareDeployment();
            _dog.Health = Math.Max(100, (int)(_dog.MaxHealth * _profile.Health / 100f));
            _dog.IsPersistent = true;
            _dog.BlockPermanentEvents = true;
            _dog.RelationshipGroup = officer.RelationshipGroup;
            NativeFunction.Natives.SET_CAN_ATTACK_FRIENDLY(_dog, false, false);
            _profile.Apply(_dog);
            _blip = _dog.AttachBlip();
            _blip.Color = Color.DodgerBlue;
            _blip.Name = "K9 " + _profile.Name;
            _state = K9State.Following;
            return true;
        }

        private void PreviewBreed(int delta){if(!DogExists()){_profile.AdjustBreed(delta);return;}Vector3 position=_dog.Position;float heading=_dog.Heading;K9State previous=_state;if(_blip!=null&&_blip.Exists())_blip.Delete();_dog.Delete();_dog=null;_profile.AdjustBreed(delta);if(!CreateDogAt(position,heading))return;if(previous==K9State.Leashed)_state=K9State.Leashed;else{NativeFunction.Natives.TASK_FOLLOW_TO_OFFSET_OF_ENTITY(_dog,Game.LocalPlayer.Character,-.7f,-1.15f,0f,2.2f,-1,1.2f,true);_state=K9State.Following;}}

        private void Follow()
        {
            if (!DogExists()) return;
            _dog.Tasks.Clear();
            NativeFunction.Natives.TASK_FOLLOW_TO_OFFSET_OF_ENTITY(_dog, Game.LocalPlayer.Character, -0.7f, -1.15f, 0f, 2.2f, -1, 1.2f, true);
            _state = _leashRope >= 0 ? K9State.Leashed : K9State.Following;
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

        private void EnterVehicle(){if(!DogExists())return;var handler=Game.LocalPlayer.Character;Vehicle vehicle=handler.CurrentVehicle;if(vehicle==null||!vehicle.Exists())vehicle=World.GetAllVehicles().Where(v=>v.Exists()&&v.DistanceTo(handler)<8f).OrderBy(v=>v.DistanceTo(handler)).FirstOrDefault();if(vehicle==null){Game.DisplayNotification("~y~No vehicle nearby.");return;}int seat=-99;foreach(int candidate in new[]{1,2,0})if(NativeFunction.Natives.IS_VEHICLE_SEAT_FREE<bool>(vehicle,candidate,false)){seat=candidate;break;}if(seat==-99){Game.DisplayNotification("~y~No open rear/passenger seat for the K9.");return;}_dogVehicleDoor=seat==2?2:seat==1?3:1;NativeFunction.Natives.SET_VEHICLE_DOOR_OPEN(vehicle,_dogVehicleDoor,false,false);Vector3 kennel=vehicle.GetOffsetPosition(new Vector3(seat==2?-1.15f:1.15f,-1.25f,0f));_dog.Tasks.FollowNavigationMeshToPosition(kennel,vehicle.Heading,1.6f).WaitForCompletion(4500);Sit();Game.DisplaySubtitle("~b~K9 waiting at the open kennel door. Loading...",900);GameFiber.Wait(750);NativeFunction.Natives.TASK_ENTER_VEHICLE(_dog,vehicle,8000,seat,2f,1,0);uint timeout=Game.GameTime+8000;while(DogExists()&&_dog.CurrentVehicle!=vehicle&&Game.GameTime<timeout)GameFiber.Yield();if(_dog.CurrentVehicle!=vehicle)NativeFunction.Natives.TASK_WARP_PED_INTO_VEHICLE(_dog,vehicle,seat);GameFiber.Wait(250);_dogVehicle=vehicle;_activeSeatProfile=_seatProfiles.Get(vehicle);string boneName=seat==1?"seat_pside_r":seat==2?"seat_dside_r":"seat_pside_f";int seatBone=NativeFunction.Natives.GET_ENTITY_BONE_INDEX_BY_NAME<int>(vehicle,boneName);PlayDogAnimation("creatures@rottweiler@amb@world_dog_sitting@base","base",-1,1);if(seatBone>=0){NativeFunction.Natives.SET_ENTITY_COLLISION(_dog,false,false);NativeFunction.Natives.ATTACH_ENTITY_TO_ENTITY(_dog,vehicle,seatBone,_activeSeatProfile.X,_activeSeatProfile.Y,_activeSeatProfile.Z,0f,0f,0f,false,false,false,false,2,true);_dogSeatAttached=true;}NativeFunction.Natives.SET_VEHICLE_DOOR_SHUT(vehicle,_dogVehicleDoor,false);NativeFunction.Natives.SET_ENTITY_INVINCIBLE(_dog,true);_state=K9State.InVehicle;K9IncidentLog.Write(_profile.Name,"Kennel","Loaded using "+_seatProfiles.VehicleName(vehicle)+" seat profile",vehicle.Position);Acknowledge("Sitting safely in the rear kennel.");}
        private void ExitVehicle(){if(_dog==null||!_dog.Exists())return;var vehicle=_dogVehicle!=null&&_dogVehicle.Exists()?_dogVehicle:_dog.CurrentVehicle;if(vehicle==null||!vehicle.Exists()){ReleaseVehicleSeat();Follow();return;}if(vehicle.Speed>1.5f){Game.DisplayNotification("~y~Stop the vehicle before unloading the K9.");return;}NativeFunction.Natives.SET_VEHICLE_DOOR_OPEN(vehicle,_dogVehicleDoor,false,false);GameFiber.Wait(650);int savedHealth=Math.Max(100,_dog.Health);Vector3 exit=vehicle.GetOffsetPosition(new Vector3(_dogVehicleDoor==2?-1.35f:1.35f,-1.25f,.15f));_dog.Tasks.ClearImmediately();ReleaseVehicleSeat();_dog.Position=exit;_dog.Heading=vehicle.Heading;if(_dog.IsDead)NativeFunction.Natives.RESURRECT_PED(_dog);_dog.Health=savedHealth;GameFiber.Wait(650);NativeFunction.Natives.SET_VEHICLE_DOOR_SHUT(vehicle,_dogVehicleDoor,false);K9IncidentLog.Write(_profile.Name,"Kennel","Unloaded",exit);Follow();}

        private void ReleaseVehicleSeat(){if(_dog!=null&&_dog.Exists()){if(_dogSeatAttached)NativeFunction.Natives.DETACH_ENTITY(_dog,true,true);NativeFunction.Natives.SET_ENTITY_COLLISION(_dog,true,true);NativeFunction.Natives.SET_ENTITY_INVINCIBLE(_dog,false);}_dogSeatAttached=false;_dogVehicle=null;}

        private void Inspect(){Game.DisplayNotification("~b~K9 "+_profile.Name+" — FIELD INSPECTION~s~~n~Health: "+_profile.Health+"%  Stamina: "+_profile.Stamina+"%~n~Food: "+_profile.Food+"%  Water: "+_profile.Water+"%~n~Training: Level "+_profile.TrainingLevel+"/5 "+_profile.TrainingLevelProgress+"%~n~~g~Completed certifications:~s~ "+Certifications());Game.DisplayNotification("~b~DUTY EQUIPMENT~s~~n~Meals "+_profile.FoodMeals+"  Water "+_profile.WaterBottles+"  First aid "+_profile.FirstAidKits+"~n~Scent bags "+_profile.ScentBags+"  Treats "+_profile.Treats);}
        private string Certifications(){string s="";if(_profile.ObedienceCertified)s+="OB ";if(_profile.AgilityCertified)s+="AGI ";if(_profile.DetectionCertified)s+="DET ";if(_profile.NarcoticsCertified)s+="NAR ";if(_profile.ExplosivesCertified)s+="BOMB ";if(_profile.WeaponsCertified)s+="WPN ";if(_profile.TrackingCertified)s+="TRK ";if(_profile.ApprehensionCertified)s+="APP ";return s.Length==0?"In training":s.Trim();}
        private void FirstAid(){if(!DogExists())return;if(_profile.Health>=95){Game.DisplayNotification("~g~No field treatment required.");return;}if(!_profile.UseFirstAid()){Game.DisplayNotification("~r~No first-aid kits. Restock at the patrol vehicle.");return;}Sit();GameFiber.Wait(1800);_dog.Health=Math.Max(_dog.Health,(int)(_dog.MaxHealth*_profile.Health/100f));_profile.ChangeTrust(2);K9IncidentLog.Write(_profile.Name,"Medical","Field first aid",_dog.Position);Game.DisplayNotification("~g~Field first aid applied.~s~~n~Serious injuries still require veterinary care.");}

        private void Rest(){if(!DogExists())return;LieDown();Game.DisplayNotification("~b~K9 rest cycle started.~s~ Maintain a safe perimeter.");GameFiber.Wait(8000);_profile.Rest();K9IncidentLog.Write(_profile.Name,"Care","Rest cycle",_dog.Position);Game.DisplayNotification("~g~K9 rested.~s~ Stamina restored.");}
        private void VeterinaryCare(){if(!DogExists())return;var handler=Game.LocalPlayer.Character;Vector3 p=handler.Position;float h=handler.Heading;NativeFunction.Natives.DO_SCREEN_FADE_OUT(450);GameFiber.Wait(550);handler.Position=new Vector3(306.7f,-595.2f,43.3f);_dog.Position=handler.GetOffsetPosition(new Vector3(1f,0f,0f));GameFiber.Wait(1800);_profile.VeterinaryTreat();_dog.Health=_dog.MaxHealth;handler.Position=p;handler.Heading=h;_dog.Position=handler.GetOffsetPosition(new Vector3(-1f,-1f,0f));NativeFunction.Natives.DO_SCREEN_FADE_IN(450);K9IncidentLog.Write(_profile.Name,"Medical","Veterinary cleared",p);Game.DisplayNotification("~g~Veterinary clearance complete.~s~ K9 returned to service.");Follow();}
        private void Restock(){var handler=Game.LocalPlayer.Character;var vehicle=World.GetAllVehicles().Where(v=>v.Exists()&&v.DistanceTo(handler)<5f).FirstOrDefault();if(vehicle==null){Game.DisplayNotification("~y~Stand beside a patrol vehicle to restock.");return;}_profile.Restock();K9IncidentLog.Write(_profile.Name,"Equipment","Restocked",handler.Position);Game.DisplayNotification("~g~K9 duty equipment restocked.");}
        private void WhistleRecall(){NativeFunction.Natives.PLAY_SOUND_FRONTEND(-1,"NAV_UP_DOWN","HUD_FRONTEND_DEFAULT_SOUNDSET",true);Game.DisplaySubtitle("~b~Handler whistle recall",1200);Follow();}
        private void HandSignal(){var handler=Game.LocalPlayer.Character;NativeFunction.Natives.REQUEST_ANIM_DICT("gestures@m@standing@casual");GameFiber.Wait(150);handler.Tasks.PlayAnimation("gestures@m@standing@casual","gesture_come_here_soft",4f,AnimationFlags.None);GameFiber.Wait(700);Follow();}
        private void CollectScent(){var handler=Game.LocalPlayer.Character;Ped target=GetValidAimedSuspect(false)??FindNearestPed(_config.TrackRadius,true);if(target==null){Game.DisplayNotification("~y~Aim at or stand near the track subject first.");return;}if(!_profile.UseScentBag()){Game.DisplayNotification("~r~No clean scent bags. Restock equipment.");return;}_scentTarget=target;_scentCollectedAt=Game.GameTime;_scentRainAtCollection=NativeFunction.Natives.GET_RAIN_LEVEL<float>();K9IncidentLog.Write(_profile.Name,"Scent article","Collected",target.Position);Game.DisplayNotification("~g~Scent article bagged.~s~~n~Subject trail is now assigned to "+_profile.Name+".");}

        private void Search(bool vehicleOnly=false,DetectionSpecialty specialty=DetectionSpecialty.General)
        {
            if(specialty!=DetectionSpecialty.General&&!_profile.HasSpecialty(specialty)){Game.DisplayNotification("~y~K9 is not certified for "+SpecialtyLabel(specialty)+" detection.~s~~n~Complete that specialty course at the academy.");return;}
            var officer = Game.LocalPlayer.Character;
            Entity target = vehicleOnly ? (Entity)World.GetAllVehicles().Where(v=>v.Exists()&&v.DistanceTo(officer)<=_config.SearchRadius).OrderBy(v=>v.DistanceTo(officer)).FirstOrDefault() : FindSearchTarget(officer.Position, _config.SearchRadius);
            if (target == null)
            {
                Game.DisplayNotification("~y~No nearby pedestrian or vehicle to search.");
                return;
            }
            _state = K9State.Searching;
            _dog.Tasks.Clear();
            if(target is Vehicle)
            {
                if(!SearchVehiclePerimeter((Vehicle)target)){Follow();return;}
            }
            else
            {
                _dog.Tasks.FollowNavigationMeshToPosition(target.GetOffsetPosition(new Vector3(0f,-1f,0f)),target.Heading,2f).WaitForCompletion(9000);
                if(!DogExists()||!target.Exists()){Follow();return;}
                for(var i=0;i<3;i++)
                {
                    var sniffPoint=target.GetOffsetPosition(new Vector3(i==0?-.8f:i==1?.8f:0f,-.45f,0f));
                    _dog.Tasks.FollowNavigationMeshToPosition(sniffPoint,target.Heading,1.2f).WaitForCompletion(2500);
                    PlayDogAnimation("creatures@rottweiler@indication@","indicate_low",650,0);
                    GameFiber.Wait(700);
                }
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
                Game.DisplayNotification("~r~POSITIVE "+SpecialtyLabel(specialty).ToUpperInvariant()+" K9 INDICATION~s~ — " + TargetLabel(target) + ".");
                _trust.Change(1, "successful detection");
                _profile.RecordSearch();
            }
            else
            {
                Game.DisplayNotification("~g~No "+SpecialtyLabel(specialty)+" K9 indication~s~ on " + TargetLabel(target) + ".");
                Sit();
            }
        }

        private static string SpecialtyLabel(DetectionSpecialty specialty)=>specialty==DetectionSpecialty.Narcotics?"narcotics":specialty==DetectionSpecialty.Explosives?"explosives":specialty==DetectionSpecialty.Weapons?"weapons":"general odor";

        private bool SearchVehiclePerimeter(Vehicle vehicle)
        {
            Game.DisplayNotification("~b~K9 vehicle sweep started.~s~~n~Keep the perimeter clear until every side is checked.");
            var points=new[]{new Vector3(1.45f,-2.45f,0f),new Vector3(1.55f,0f,0f),new Vector3(1.35f,2.45f,0f),new Vector3(0f,2.8f,0f),new Vector3(-1.35f,2.45f,0f),new Vector3(-1.55f,0f,0f),new Vector3(-1.45f,-2.45f,0f),new Vector3(0f,-2.8f,0f)};
            for(var i=0;i<points.Length;i++)
            {
                if(!DogExists()||!vehicle.Exists()||_state!=K9State.Searching)return false;
                var point=vehicle.GetOffsetPosition(points[i]);
                Game.DisplaySubtitle("~b~Vehicle search~s~ — perimeter checkpoint "+(i+1)+"/"+points.Length,1800);
                _dog.Tasks.Clear();
                _dog.Tasks.FollowNavigationMeshToPosition(point,vehicle.Heading,1.45f).WaitForCompletion(5000);
                if(_dog.DistanceTo(point)>3.5f)continue;
                NativeFunction.Natives.TASK_TURN_PED_TO_FACE_ENTITY(_dog,vehicle,900);GameFiber.Wait(900);
                PlayDogAnimation("creatures@rottweiler@indication@","indicate_low",800,0);
                GameFiber.Wait(300);
            }
            return DogExists()&&vehicle.Exists();
        }

        private void Track()
        {
            var target = _scentTarget!=null&&_scentTarget.Exists()&&!_scentTarget.IsDead?_scentTarget:FindNearestPed(_config.TrackRadius, true);
            if (target == null)
            {
                Game.DisplayNotification("~y~No trackable person nearby. Face the wanted/missing person and retry.");
                return;
            }
            _state = K9State.Tracking;
            float rain=NativeFunction.Natives.GET_RAIN_LEVEL<float>();float ageMinutes=_scentCollectedAt==0?0:(Game.GameTime-_scentCollectedAt)/60000f;float initialDistance=target.DistanceTo(Game.LocalPlayer.Character);bool inVehicle=target.CurrentVehicle!=null;int scentQuality=Math.Max(5,100-(int)(ageMinutes*8)-(int)(rain*35)-(int)(initialDistance/12)-(inVehicle?22:0));
            if(scentQuality<18){Game.DisplayNotification("~r~Scent trail is too degraded.~s~~n~Collect a fresh scent article; rain, age, distance, and vehicles weaken odor.");return;}
            Game.DisplayNotification("~b~K9 scent track started.~s~ Quality "+scentQuality+"%~n~Rain, trail age, distance, and vehicle travel affect the track.");K9IncidentLog.Write(_profile.Name,"Track","Started quality "+scentQuality+"%",target.Position);
            var end = Game.GameTime + 120000;
            while (_running && DogExists() && target.Exists() && !target.IsDead && Game.GameTime < end && _state == K9State.Tracking)
            {
                if (_dog.DistanceTo(target) < 3f)
                {
                    Bark(2);
                    Sit();
                    Game.DisplayNotification("~g~Track complete.~s~ Person located.");K9IncidentLog.Write(_profile.Name,"Track","Subject located",target.Position);
                    _trust.Change(2, "successful track");
                    return;
                }
                var destination = target.Position;
                float dx = destination.X - _dog.Position.X, dy = destination.Y - _dog.Position.Y;
                float distance = (float)Math.Sqrt(dx * dx + dy * dy);
                float step = Math.Min(13f, Math.Max(5f, distance - 2f));
                float inv = distance > .01f ? 1f / distance : 0f;
                float scentJitter = (float)(_random.NextDouble() * 3.0 - 1.5);
                var waypoint = new Vector3(_dog.Position.X + dx * inv * step - dy * inv * scentJitter,
                                           _dog.Position.Y + dy * inv * step + dx * inv * scentJitter,
                                           destination.Z);
                _dog.Tasks.Clear();
                PlayDogAnimation("creatures@rottweiler@indication@", "indicate_low", 650, 0);
                _dog.Tasks.FollowNavigationMeshToPosition(waypoint, target.Heading, rain>.35f?2.25f:3.25f).WaitForCompletion(10000);
                _profile.UseStamina(1);
                GameFiber.Wait(150);
            }
            Follow();
        }

        private void Apprehend()
        {
            var handler=Game.LocalPlayer.Character;
            var target=GetValidAimedSuspect(false);
            if(target==null&&_voiceAimedTarget!=null&&_voiceAimedTarget.Exists()&&!_voiceAimedTarget.IsDead&&_voiceAimedTarget.DistanceTo(handler)<=80f&&!LspdfrBridge.IsPedCop(_voiceAimedTarget))target=_voiceAimedTarget;
            _voiceAimedTarget=null;
            if(target==null){Game.DisplayNotification("~y~No valid suspect identified.~s~~n~Aim your taser or firearm directly at the intended person, then issue APPREHEND.");return;}
            if(_state==K9State.InVehicle)DoorPop(false);
            _state = K9State.Apprehending;
            _dog.Tasks.Clear();
            string reaction=ApplySuspectReaction(target,handler);K9IncidentLog.Write(_profile.Name,"Apprehension","Suspect reaction: "+reaction,target.Position);
            NativeFunction.Natives.TASK_COMBAT_PED(_dog, target, 0, 16);
            Game.DisplayNotification("~o~K9 deployed on positively identified aimed suspect.~s~~n~Recall automatically engages at the safety threshold.");
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

        private string ApplySuspectReaction(Ped target,Ped handler){int roll=_random.Next(100);if(roll<30){NativeFunction.Natives.TASK_HANDS_UP(target,12000,handler,-1,true);Game.DisplayNotification("~g~Suspect surrendered to K9 warning.");return "surrender";}if(roll<55){target.Tasks.Clear();NativeFunction.Natives.TASK_STAND_STILL(target,5000);Game.DisplayNotification("~y~Suspect froze on K9 deployment.");return "freeze";}if(roll<88){NativeFunction.Natives.TASK_SMART_FLEE_PED(target,handler,120f,-1,false,false);Game.DisplayNotification("~o~Suspect fled from K9.");return "flee";}NativeFunction.Natives.TASK_COMBAT_PED(target,handler,0,16);Game.DisplayNotification("~r~Suspect chose to fight.");return "fight";}
        private void DoorPop(){DoorPop(true);}
        private void DoorPop(bool followAfter){if(!DogExists()||_state!=K9State.InVehicle){if(followAfter)Game.DisplayNotification("~y~K9 is not secured in the vehicle.");return;}var vehicle=_dogVehicle;if(vehicle==null||!vehicle.Exists()||vehicle.Speed>2f){Game.DisplayNotification("~y~Vehicle must be stopped for door-pop deployment.");return;}NativeFunction.Natives.SET_VEHICLE_DOOR_OPEN(vehicle,_dogVehicleDoor,false,false);GameFiber.Wait(450);Vector3 exit=vehicle.GetOffsetPosition(new Vector3(_dogVehicleDoor==2?-1.4f:1.4f,-1.2f,.15f));ReleaseVehicleSeat();_dog.Position=exit;_dog.Health=Math.Max(_dog.Health,100);GameFiber.Wait(350);NativeFunction.Natives.SET_VEHICLE_DOOR_SHUT(vehicle,_dogVehicleDoor,false);K9IncidentLog.Write(_profile.Name,"Door pop","Deployed",exit);if(followAfter)Follow();}

        private Ped GetValidAimedSuspect(bool notify)
        {
            var handler=Game.LocalPlayer.Character;
            uint weapon=NativeFunction.Natives.GET_SELECTED_PED_WEAPON<uint>(handler),unarmed=NativeFunction.Natives.GET_HASH_KEY<uint>("WEAPON_UNARMED");
            var target=weapon==unarmed?null:Game.LocalPlayer.GetFreeAimingTarget() as Ped;
            if(target==null||!target.Exists()||target==handler||target==_dog||target.IsDead||target.DistanceTo(handler)>80f)return null;
            if(LspdfrBridge.IsPedCop(target)){if(notify)Game.DisplayNotification("~r~K9 safety interlock: officer target rejected.");return null;}
            return target;
        }

        private void Fetch()
        {
            _state = K9State.Fetching;
            var officer = Game.LocalPlayer.Character;
            var ballModel = new Model("w_am_baseball");
            if (!ballModel.IsValid) { Follow(); return; }
            ballModel.LoadAndWait();
            var landing = officer.GetOffsetPosition(new Vector3(0f, 7f, 0f));
            var ball = new Rage.Object(ballModel, landing);
            ballModel.Dismiss();
            ball.IsPersistent = true;
            _dog.Tasks.FollowNavigationMeshToPosition(landing, officer.Heading, 3f).WaitForCompletion(9000);
            if (DogExists() && ball.Exists())
            {
                int mouthBone = NativeFunction.Natives.GET_PED_BONE_INDEX<int>(_dog, 31086);
                NativeFunction.Natives.SET_ENTITY_COLLISION(ball, false, false);
                NativeFunction.Natives.ATTACH_ENTITY_TO_ENTITY(ball, _dog, mouthBone, _config.FetchBallOffsetX, _config.FetchBallOffsetY, _config.FetchBallOffsetZ, 0f, 0f, 0f, false, false, false, false, 2, true);
                _dog.Tasks.FollowNavigationMeshToPosition(officer.GetOffsetPosition(new Vector3(0f, 1.2f, 0f)), officer.Heading, 2.5f).WaitForCompletion(9000);
                NativeFunction.Natives.DETACH_ENTITY(ball, true, true);
                NativeFunction.Natives.SET_ENTITY_COLLISION(ball, true, true);
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
            var handler=Game.LocalPlayer.Character;NativeFunction.Natives.TASK_TURN_PED_TO_FACE_ENTITY(handler,_dog,900);NativeFunction.Natives.TASK_TURN_PED_TO_FACE_ENTITY(_dog,handler,900);GameFiber.Wait(900);
            NativeFunction.Natives.TASK_START_SCENARIO_IN_PLACE(handler,"WORLD_HUMAN_GARDENER_PLANT",0,true);
            _dog.Tasks.PlayAnimation("creatures@rottweiler@tricks@","petting_franklin",4f,AnimationFlags.Loop);GameFiber.Wait(4200);
            handler.Tasks.Clear();Sit();
            Game.DisplayNotification("~b~" + _profile.Name + "~s~ enjoyed that.");
            _trust.Change(2, "handler bonding");
            _profile.ChangeTrust(2);_profile.Recover(3);
        }

        private void Feed()
        {
            if (_dog.DistanceTo(Game.LocalPlayer.Character) > 2.5f) { Game.DisplayNotification("~y~Move closer to your K9."); return; }
            if(!_profile.FeedMeal()){Game.DisplayNotification("~r~No K9 meals remaining. Restock at the patrol vehicle.");return;}UseBowl(false);
            Game.DisplayNotification("~b~You fed " + _profile.Name + ".~s~ Food restored to 100%.");
            _trust.Change(2, "care and feeding");
            _profile.ChangeTrust(2);_profile.Recover(8);
        }

        private void Drink(){if(_dog.DistanceTo(Game.LocalPlayer.Character)>2.5f){Game.DisplayNotification("~y~Move closer to your K9.");return;}if(!_profile.GiveWater()){Game.DisplayNotification("~r~No water bottles remaining. Restock at the patrol vehicle.");return;}UseBowl(true);Game.DisplayNotification("~b~"+_profile.Name+" drank fresh water.~s~ Water restored to 100%.");_trust.Change(1,"handler care");_profile.ChangeTrust(1);}

        private void UseBowl(bool water)
        {
            Rage.Object bowl=null;try{var model=new Model("prop_cs_bowl_01");if(!model.IsValid)model=new Model("prop_bowl_crisps");if(model.IsValid){model.LoadAndWait();bowl=new Rage.Object(model,_dog.GetOffsetPosition(new Vector3(0f,.75f,0f)));model.Dismiss();}var handler=Game.LocalPlayer.Character;NativeFunction.Natives.TASK_TURN_PED_TO_FACE_ENTITY(handler,_dog,700);NativeFunction.Natives.TASK_START_SCENARIO_IN_PLACE(handler,"WORLD_HUMAN_GARDENER_PLANT",0,true);GameFiber.Wait(1200);handler.Tasks.Clear();if(bowl!=null&&bowl.Exists())_dog.Tasks.FollowNavigationMeshToPosition(bowl.Position,_dog.Heading,1f).WaitForCompletion(2500);for(int i=0;i<3;i++){PlayDogAnimation("creatures@rottweiler@indication@","indicate_low",650,0);GameFiber.Wait(250);}Sit();}finally{if(bowl!=null&&bowl.Exists())bowl.Delete();}
        }

        private void ToggleLeash()
        {
            if (_state == K9State.Leashed || _leashRope >= 0) { DeleteLeashRope(); Follow(); Game.DisplayNotification("~b~K9 leash removed."); return; }
            _dog.Tasks.Clear();
            CreateLeashRope();
            _state = K9State.Leashed;
            NativeFunction.Natives.TASK_FOLLOW_TO_OFFSET_OF_ENTITY(_dog,Game.LocalPlayer.Character,-.55f,-.85f,0f,1.8f,-1,1.15f,true);
            Game.DisplayNotification("~b~K9 leash attached.~s~ The K9 will walk at the handler's left side.");
        }

        private void CreateLeashRope()
        {
            DeleteLeashRope();
            var handler=Game.LocalPlayer.Character;
            var hand=NativeFunction.Natives.GET_PED_BONE_COORDS<Vector3>(handler,57005,0f,0f,0f);
            var collar=NativeFunction.Natives.GET_PED_BONE_COORDS<Vector3>(_dog,39317,0f,.03f,0f);
            NativeFunction.Natives.ROPE_LOAD_TEXTURES();GameFiber.Wait(100);
            _leashRope=NativeFunction.Natives.ADD_ROPE<int>(hand.X,hand.Y,hand.Z,0f,0f,0f,2.6f,4,2.6f,.35f,0f,false,false,true,1f,false,0);
            if(_leashRope>=0)NativeFunction.Natives.ATTACH_ENTITIES_TO_ROPE(_leashRope,handler,_dog,hand.X,hand.Y,hand.Z,collar.X,collar.Y,collar.Z,2.25f,false,false,0,0);
        }

        private void DeleteLeashRope(){if(_leashRope>=0){try{NativeFunction.Natives.DELETE_ROPE(ref _leashRope);}catch{} }_leashRope=-1;}

        private void MaintainState()
        {
            if (!DogExists()) { _state = K9State.Dismissed; _camera.Disable(); return; }
            EnforceHandlerSafety();
            RecordHandlerTrail();
            UpdateNeeds();
            UpdateEnvironment();
            if(Game.GameTime>=_nextVitalsUpdate){_nextVitalsUpdate=Game.GameTime+5000;int liveHealth=(int)(100f*_dog.Health/Math.Max(1,_dog.MaxHealth));if(liveHealth<_profile.Health){string injury=liveHealth<=25?"Serious — veterinary treatment required":liveHealth<=55?"Moderate":"Minor";_profile.SetInjury(injury,liveHealth);K9IncidentLog.Write(_profile.Name,"Injury",injury,_dog.Position);}NativeFunction.Natives.SET_PED_MOVE_RATE_OVERRIDE(_dog,_profile.Health<=55?.65f:1f);if(_state==K9State.Searching||_state==K9State.Tracking||_state==K9State.Apprehending)_profile.UseStamina(2);else _profile.Recover(1);}
            if (_state == K9State.Leashed)
            {
                var officer = Game.LocalPlayer.Character;
                if(Game.GameTime>=_nextLeashFollow){_nextLeashFollow=Game.GameTime+900;if(_dog.DistanceTo(officer)>1.05f)NativeFunction.Natives.TASK_FOLLOW_TO_OFFSET_OF_ENTITY(_dog,officer,-.55f,-.85f,0f,1.9f,-1,1.15f,true);}
                if(_dog.DistanceTo(officer)>3.2f)_dog.Tasks.FollowNavigationMeshToPosition(officer.GetOffsetPosition(new Vector3(-.55f,-.85f,0f)),officer.Heading,2.4f);
            }
            RecoverHandlerTrail();
            if (_dog.DistanceTo(Game.LocalPlayer.Character) > 150f && _state == K9State.Following)
                _dog.Position = Game.LocalPlayer.Character.GetOffsetPosition(new Vector3(-1f, -2f, 0f));
        }

        private void RecordHandlerTrail(){if(Game.GameTime<_nextTrailSample)return;_nextTrailSample=Game.GameTime+1200;var p=Game.LocalPlayer.Character.Position;if(_handlerTrail.Count==0||Distance2D(_handlerTrail.Last(),p)>2.5f){_handlerTrail.Enqueue(p);while(_handlerTrail.Count>35)_handlerTrail.Dequeue();}}
        private void RecoverHandlerTrail(){if(_state!=K9State.Following||_dog.DistanceTo(Game.LocalPlayer.Character)<12f)return;if(Game.GameTime<_nextTrailRecovery)return;_nextTrailRecovery=Game.GameTime+2200;Vector3 waypoint=_handlerTrail.Count>0?_handlerTrail.Dequeue():Game.LocalPlayer.Character.Position;PlayDogAnimation("creatures@rottweiler@indication@","indicate_low",500,0);_dog.Tasks.FollowNavigationMeshToPosition(waypoint,Game.LocalPlayer.Character.Heading,3.5f);Game.DisplaySubtitle("~b~"+_profile.Name+"~s~ is following the handler's scent trail.",1200);}
        private static float Distance2D(Vector3 a,Vector3 b){float x=a.X-b.X,y=a.Y-b.Y;return(float)Math.Sqrt(x*x+y*y);}

        private void UpdateNeeds(){if(Game.GameTime<_nextNeedsUpdate)return;_nextNeedsUpdate=Game.GameTime+180000;bool working=_state==K9State.Searching||_state==K9State.Tracking||_state==K9State.Apprehending||_state==K9State.Fetching;_profile.UseNeeds(working?1:0,working?2:1);if((_profile.Food<=25||_profile.Water<=25)&&Game.GameTime>=_nextNeedsWarning){_nextNeedsWarning=Game.GameTime+180000;Game.DisplayNotification("~o~K9 care needed:~s~~n~Food "+_profile.Food+"%  Water "+_profile.Water+"%~n~Use Feed or Give Water from the command menu.");}}

        private void UpdateEnvironment(){if(Game.GameTime<_nextEnvironmentUpdate)return;_nextEnvironmentUpdate=Game.GameTime+60000;float rain=NativeFunction.Natives.GET_RAIN_LEVEL<float>();int hour=World.DateTime.Hour;bool hot=rain<.1f&&hour>=11&&hour<=18;if(hot)_profile.UseNeeds(0,2);if(hot&&_state==K9State.InVehicle&&_dogVehicle!=null&&_dogVehicle.Exists()&&!NativeFunction.Natives.GET_IS_VEHICLE_ENGINE_RUNNING<bool>(_dogVehicle)){_profile.UseNeeds(0,5);if(Game.GameTime>=_nextHeatWarning){_nextHeatWarning=Game.GameTime+90000;Game.DisplayNotification("~r~K9 VEHICLE HEAT WARNING~s~~n~Engine is off during peak heat. Remove the K9 or provide water.");K9IncidentLog.Write(_profile.Name,"Safety","Vehicle heat warning",_dogVehicle.Position);}}}

        private void DrawHud(){int mode=_profile.HudMode;if(mode!=0){float x=_profile.HudX,y=_profile.HudY,s=_profile.HudScale,w=.30f*s,h=(mode==1?.145f:.215f)*s,left=x-w/2+.012f*s,top=y-h/2;NativeFunction.Natives.DRAW_RECT(x,y,w,h,7,13,20,225);NativeFunction.Natives.DRAW_RECT(x,top+.019f*s,w,.038f*s,8,76,116,255);NativeFunction.Natives.DRAW_RECT(x-w/2+.003f*s,y,.006f*s,h,26,181,232,255);DrawText("K9  "+_profile.Name.ToUpper()+"     "+_state.ToString().ToUpper(),left,top+.006f*s,.31f*s);DrawStatusBar("HEALTH",_profile.Health,left,top+.052f*s,.125f*s,34,197,94);DrawStatusBar("STAMINA",_profile.Stamina,left+.143f*s,top+.052f*s,.125f*s,241,196,15);DrawStatusBar("FOOD",_profile.Food,left,top+.091f*s,.125f*s,230,126,34);DrawStatusBar("WATER",_profile.Water,left+.143f*s,top+.091f*s,.125f*s,37,162,232);if(mode==2){DrawText("CERTIFIED  "+Certifications(),left,top+.132f*s,.245f*s);DrawText("TRUST "+_profile.Trust+"%   LEVEL "+_profile.TrainingLevel+"/5   INJURY "+_profile.Injury,left,top+.162f*s,.235f*s);DrawText("VOICE  "+_voiceStatus,left,top+.190f*s,.225f*s);}}if(_camera.Active&&DogExists()){float d=_dog.DistanceTo(Game.LocalPlayer.Character);NativeFunction.Natives.DRAW_RECT(.5f,.91f,.52f,.09f,0,0,0,180);DrawText("K9 CAM  GPS "+_dog.Position.X.ToString("0")+","+_dog.Position.Y.ToString("0")+"  HDG "+HeadingCardinal(_dog.Heading)+"  HANDLER "+d.ToString("0.0")+"m",.25f,.875f,.31f);DrawText("STATE "+_state+"  HP "+_profile.Health+"  STA "+_profile.Stamina+"  H2O "+_profile.Water,.25f,.91f,.27f);}}
        private static void DrawStatusBar(string label,int value,float x,float y,float width,int r,int g,int b){DrawText(label+" "+value+"%",x,y-.015f,.22f);NativeFunction.Natives.DRAW_RECT(x+width/2,y+.012f,width,.009f,32,40,48,235);float fill=width*Math.Max(0,Math.Min(100,value))/100f;if(fill>0)NativeFunction.Natives.DRAW_RECT(x+fill/2,y+.012f,fill,.009f,r,g,b,255);}
        private static string HeadingCardinal(float h){h=(h%360+360)%360;return h<22.5f||h>=337.5f?"N":h<67.5f?"NE":h<112.5f?"E":h<157.5f?"SE":h<202.5f?"S":h<247.5f?"SW":h<292.5f?"W":"NW";}
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
            var handler=Game.LocalPlayer.Character;
            if(handler.CurrentVehicle!=null){Game.DisplayNotification("~y~Exit your vehicle before traveling to the K9 academy.");return;}
            Vector3 returnPosition=handler.Position;float returnHeading=handler.Heading;
            int level=_profile.TrainingLevel;
            try
            {
                _state=K9State.Academy;
                NativeFunction.Natives.DO_SCREEN_FADE_OUT(500);GameFiber.Wait(650);
                var academyGround=new Vector3(-1018.4f,-3003.1f,13.95f);
                handler.Position=academyGround;handler.Heading=60f;_dog.Position=handler.GetOffsetPosition(new Vector3(-1.5f,-2f,0f));_dog.Heading=handler.Heading;
                NativeFunction.Natives.DO_SCREEN_FADE_IN(700);GameFiber.Wait(800);
                Game.DisplayNotification("~b~Arrived at the Advanced K9 training ground.~s~~n~Level "+level+"/5 — "+_profile.CurrentTrainingName+" ("+_profile.TrainingLevelProgress+"%)");
                var academy=new AcademySession(_dog,_profile.Name);
                int points=academy.Run(level,Sit,LieDown,Follow);
                bool completed=_profile.ApplyTrainingProgress(level,points);
                _trust.Change(Math.Max(1,points/20),"academy training");
                if(completed)Game.DisplayNotification("~g~LEVEL "+level+" CERTIFICATION COMPLETE — 100%~s~~n~"+(level<5?"Level "+_profile.TrainingLevel+" is now unlocked.":"All K9 certifications completed."));
                else Game.DisplayNotification("~b~Training saved:~s~ Level "+_profile.TrainingLevel+" — "+_profile.TrainingLevelProgress+"%");
            }
            finally
            {
                NativeFunction.Natives.DO_SCREEN_FADE_OUT(500);GameFiber.Wait(650);handler.Position=returnPosition;handler.Heading=returnHeading;_dog.Position=handler.GetOffsetPosition(new Vector3(-1f,-2f,0f));NativeFunction.Natives.DO_SCREEN_FADE_IN(700);Follow();
            }
        }

        private void RunAcademySpecialty(DetectionSpecialty specialty)
        {
            if(!_profile.DetectionCertified){Game.DisplayNotification("~y~Detection foundation is locked.~s~~n~Complete core academy Level 3 to unlock specialty training.");return;}
            if(_profile.HasSpecialty(specialty)){Game.DisplayNotification("~g~"+SpecialtyLabel(specialty)+" certification already completed.~s~~n~This K9 may still repeat the course for maintenance training.");}
            var handler=Game.LocalPlayer.Character;if(handler.CurrentVehicle!=null){Game.DisplayNotification("~y~Exit your vehicle before traveling to the K9 academy.");return;}
            Vector3 returnPosition=handler.Position;float returnHeading=handler.Heading;
            try
            {
                _state=K9State.Academy;NativeFunction.Natives.DO_SCREEN_FADE_OUT(500);GameFiber.Wait(650);
                handler.Position=new Vector3(-1018.4f,-3003.1f,13.95f);handler.Heading=60f;_dog.Position=handler.GetOffsetPosition(new Vector3(-1.5f,-2f,0f));_dog.Heading=handler.Heading;
                NativeFunction.Natives.DO_SCREEN_FADE_IN(700);GameFiber.Wait(800);
                Game.DisplayNotification("~b~Specialty academy:~s~ "+SpecialtyLabel(specialty)+" detection ("+_profile.SpecialtyProgress(specialty)+"%).");
                var academy=new AcademySession(_dog,_profile.Name);int points=academy.RunSpecialty(specialty,Sit,Follow);
                bool completed=_profile.ApplySpecialtyProgress(specialty,points);_trust.Change(Math.Max(1,points/20),"specialty detection training");
                if(completed)Game.DisplayNotification("~g~"+SpecialtyLabel(specialty).ToUpperInvariant()+" DETECTION CERTIFIED — 100%~s~~n~Other detection specialties remain independently trainable.");
                else Game.DisplayNotification("~b~Specialty saved:~s~ "+SpecialtyLabel(specialty)+" "+_profile.SpecialtyProgress(specialty)+"%.");
            }
            finally{NativeFunction.Natives.DO_SCREEN_FADE_OUT(500);GameFiber.Wait(650);handler.Position=returnPosition;handler.Heading=returnHeading;_dog.Position=handler.GetOffsetPosition(new Vector3(-1f,-2f,0f));NativeFunction.Natives.DO_SCREEN_FADE_IN(700);Follow();}
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

        private void Dismiss(bool notify = true)
        {
            _camera.Disable();
            DeleteLeashRope();
            ReleaseVehicleSeat();
            if (_blip != null && _blip.Exists()) _blip.Delete();
            if (_dog != null && _dog.Exists()) _dog.Dismiss();
            _dog = null;
            _state = K9State.Dismissed;
            if (notify) Game.DisplayNotification("~b~Advanced K9 dismissed.");
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
