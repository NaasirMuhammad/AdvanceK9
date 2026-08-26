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
        private readonly PolicingRedefinedBridge _pr;
        private readonly DogCamera _camera = new DogCamera();
        private readonly ConcurrentQueue<K9Command> _voiceQueue = new ConcurrentQueue<K9Command>();
        private readonly Random _random = new Random();
        private readonly TrustProfile _trust;
        private readonly K9Profile _profile;
        private readonly VehicleSeatProfiles _seatProfiles;
        private VoiceCommandService _voice;
        private Ped _dog;
        private readonly List<StationKennel> _stationKennels=new List<StationKennel>();
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
        private int _leashRope = -1;
        private uint _nextLeashFollow;
        private uint _nextNeedsUpdate;
        private uint _nextNeedsWarning;
        private VehicleSeatProfile _activeSeatProfile;
        private bool _seatCalibrationDoorOpen;
        private int _bladder=100;
        private int _bowel=100;
        private uint _nextReliefUpdate;
        private uint _nextReliefWarning;
        private readonly Dictionary<PoolHandle,List<ScentTrailPoint>> _recordedTrails=new Dictionary<PoolHandle,List<ScentTrailPoint>>();
        private uint _nextTrailCapture;
        private uint _nextPursuitProbe;
        private Ped _compatibilityPursuitSuspect;
        private Vehicle _pursuitLastVehicle;
        private bool _trailLost;
        private float _activeTrackDistance;
        private uint _activeTrackStarted;
        private string _activeScentSource="None";
        private bool _warningGiven;
        private Ped _warnedTarget;
        private bool _warningSurrendered;
        private uint _biteStarted;

        private sealed class ScentTrailPoint
        {
            public Vector3 Position;
            public uint Time;
            public ScentTrailPoint(Vector3 position,uint time){Position=position;Time=time;}
        }

        private sealed class StationKennel
        {
            public string Name;public Vector3 Position;public float Heading;public Rage.Object Prop;
            public StationKennel(string name,Vector3 position,float heading){Name=name;Position=position;Heading=heading;}
        }

        public K9Controller(ModConfig config)
        {
            _config = config;
            _pr = new PolicingRedefinedBridge(config.CompatibilityMode,config.CompatibilityShareResults);
            _trust = new TrustProfile(config.StartingTrust,config.ShowActionNotifications);
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
                _pr.TickDiagnostics();
                _voice?.Tick();
                _menu.Tick();
                DrawHud();
                MaintainStationKennels();
                CaptureScentTrails();
                UpdateCompatibilityPursuit();
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
            SpawnStationKennels();
            ActionNotification("~b~Advanced K9 Beta active~s~. Hold ~y~" + _config.ModifierKey + "~s~ + ~y~" + _config.SpawnKey + "~s~ to deploy " + _profile.Name + ".");
        }

        private void DeactivateForDuty()
        {
            _menu.Close();
            _voice?.StopListening();
            _voiceActive = false;
            _voiceStatus = "Off duty";
            if (_dog!=null&&_dog.Exists()) Dismiss(false);
            DeleteStationKennels();
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
            CloseSeatCalibrationDoor();_menuMode="commands_root";_menu.Open("ADVANCED K9 — COMMANDS",new[]{"Partner Control","Search & Detection","Tracking & Scent","Tactical Deployment","Vehicle & Equipment","Care & Medical","Training & Certifications","Deploy / Dismiss",VoiceMenuLabel()});
        }

        private static readonly K9Command[][] CommandGroups={
            new[]{K9Command.Follow,K9Command.Heel,K9Command.Sit,K9Command.LieDown,K9Command.Stay,K9Command.Recall,K9Command.WhistleRecall,K9Command.HandSignal,K9Command.Fetch,K9Command.Pet},
            new[]{K9Command.SearchArea,K9Command.SearchBuilding,K9Command.SearchVehicle,K9Command.SearchNarcotics,K9Command.SearchExplosives,K9Command.SearchWeapons},
            new[]{K9Command.CollectScent,K9Command.Track,K9Command.FindTrail},
            new[]{K9Command.K9Warning,K9Command.Apprehend,K9Command.HandoffArrest,K9Command.RequestPerimeter,K9Command.RequestTransport,K9Command.RequestMedical,K9Command.RequestBombSquad,K9Command.DoorPop,K9Command.Release,K9Command.Guard,K9Command.Bark},
            new[]{K9Command.EnterVehicle,K9Command.ExitVehicle,K9Command.ToggleLeash,K9Command.ToggleCamera,K9Command.Restock},
            new[]{K9Command.Feed,K9Command.Drink,K9Command.Rest,K9Command.Inspect,K9Command.FirstAid,K9Command.VeterinaryCare},
            new[]{K9Command.Training,K9Command.TrainNarcotics,K9Command.TrainExplosives,K9Command.TrainWeapons}};
        private static readonly string[] CommandGroupTitles={"PARTNER CONTROL","SEARCH & DETECTION","TRACKING & SCENT","TACTICAL DEPLOYMENT","VEHICLE & EQUIPMENT","CARE & MEDICAL","TRAINING & CERTIFICATIONS"};
        private void OpenCommandGroup(int group){_menuMode="commands_group_"+group;var labels=CommandGroups[group].Select(CommandLabel).Concat(new[]{"← Back to Command Categories"});_menu.Open("ADVANCED K9 — "+CommandGroupTitles[group],labels);}
        private static string CommandLabel(K9Command command){var definition=CommandRegistry.All.FirstOrDefault(x=>x.Command==command);return definition==null?command.ToString():definition.Label;}

        private void ShowKennelMenu()
        {
            CloseSeatCalibrationDoor();_menuMode="profile"; RefreshProfileMenu();
        }

        private void RefreshProfileMenu(){int m=_profile.HudMode;_menu.Update("K9 PROFILE — "+_profile.Name,new[]{"Edit name: "+_profile.Name,"Breed/model: "+_profile.Breed,"Skin/coat: "+(_profile.CoatVariation+1),"Equipment: "+_profile.Vest,"Vest texture: "+_profile.VestTextureName(_dog),"HUD: "+(m==0?"Hidden":m==1?"Compact":"Expanded"),"Customize HUD","Move HUD left","Move HUD right","Move HUD up","Move HUD down","HUD scale: "+_profile.HudScale.ToString("0.0"),"Vehicle Seat Configuration","Inspect Profile / Certifications",VoiceMenuLabel()});}
        private void OnMenuSelected(int index){if(_menuMode=="commands_root"){if(index>=0&&index<7){OpenCommandGroup(index);return;}if(index==7){Execute(K9Command.SpawnDismiss);return;}if(index==8)ToggleVoice();return;}if(_menuMode!=null&&_menuMode.StartsWith("commands_group_")){int group;if(!int.TryParse(_menuMode.Substring(15),out group)||group<0||group>=CommandGroups.Length)return;if(index>=0&&index<CommandGroups[group].Length)Execute(CommandGroups[group][index]);else ShowCommandMenu();return;}if(_menuMode=="hud_config"){HandleHudMenu(index);return;}if(_menuMode=="seat_config"){HandleSeatMenu(index);return;}if(_menuMode!="profile")return;switch(index){case 0:string n=PromptForDogName(24);if(!string.IsNullOrWhiteSpace(n)){_profile.SetName(n);_voice?.UpdateWakeWord(_profile.Name);ActionNotification("~b~Voice wake word changed immediately to:~s~ "+_profile.Name);}break;case 1:PreviewBreed(1);break;case 2:_profile.NextSkin(_dog);break;case 3:_profile.NextEquipment(_dog);break;case 4:_profile.NextEquipmentTexture(_dog);break;case 5:_profile.CycleHudMode();break;case 6:OpenHudConfiguration();return;case 7:_profile.MoveHud(-.02f,0);break;case 8:_profile.MoveHud(.02f,0);break;case 9:_profile.MoveHud(0,-.02f);break;case 10:_profile.MoveHud(0,.02f);break;case 11:_profile.ScaleHud();break;case 12:OpenSeatConfiguration();return;case 13:Inspect();break;case 14:ToggleVoice();break;}RefreshProfileMenu();}

        private void InitializeVoice(){_voice=new VoiceCommandService(_config.VoiceProvider,_config.VoiceModel,_config.VoiceLanguage,_config.VoiceApiKey,_config.VoiceApiKeyEnvironmentVariable,_profile.Name,_config.ShowVoiceStatusText);_voice.CommandRecognized+=c=>_voiceQueue.Enqueue(c);_voice.StatusChanged+=s=>_voiceStatus=s;_voiceActive=_config.VoiceEnabled&&_voice.IsAvailable;_voiceStatus=_voice.IsAvailable?"Ready (hold V)":"Key missing";}
        private string VoiceMenuLabel()=>"Voice microphone: "+(_voice==null||!_voice.IsAvailable?"UNAVAILABLE — add ApiKey in INI":_voiceActive?"ON — hold "+_config.PushToTalkKey:"OFF — select to activate");
        private void ToggleVoice(){if(_voice==null)InitializeVoice();if(!_voice.IsAvailable){Game.DisplayNotification("~r~Voice cannot activate.~s~~n~Add your provider key after ~y~ApiKey=~s~ in AdvancedK9.ini, then reload the plugin.");return;}_voiceActive=!_voiceActive;if(_voiceActive){_voiceStatus="Ready (hold V)";ActionNotification("~g~K9 push-to-talk activated.~s~ Hold "+_config.PushToTalkKey+" while speaking.");}else{_voice.StopListening();_voiceStatus="Off";ActionNotification("~y~K9 voice microphone disabled.");}}

        private void OnMenuAdjusted(int index,int delta){if(_menuMode=="hud_config"){AdjustHudMenu(index,delta);return;}if(_menuMode=="seat_config"){AdjustSeat(index,delta);return;}if(_menuMode!="profile")return;switch(index){case 1:PreviewBreed(delta);break;case 2:_profile.AdjustSkin(_dog,delta);break;case 3:_profile.AdjustEquipment(_dog,delta);break;case 4:_profile.AdjustEquipmentTexture(_dog,delta);break;case 5:_profile.CycleHudMode();break;case 7:case 8:_profile.MoveHud(delta*.01f,0);break;case 9:case 10:_profile.MoveHud(0,delta*.01f);break;case 11:_profile.ScaleHud();break;default:return;}RefreshProfileMenu();}

        private static readonly string[] HudDesignNames={"EUP Panel","Minimal Strip","Tactical Card","Rounded Modern","Transparent Compact"};
        private static readonly string[] HudIconNames={"Labels","Police Symbols","Paw Set","Minimal Dots","No Icons"};
        private static readonly string[] HudColorNames={"Police Blue","Sheriff Gold","Tactical Green","Emergency Red","Monochrome"};
        private static readonly string[] HudTextNames={"Standard","Condensed","Bold","Digital","Minimal"};
        private static string OnOff(bool value)=>value?"ON":"OFF";
        private void OpenHudConfiguration(){_menuMode="hud_config";RefreshHudMenu();}
        private void RefreshHudMenu(){_menu.Update("K9 HUD — CUSTOMIZE",new[]{"Design: "+HudDesignNames[_profile.HudDesign],"Icons: "+HudIconNames[_profile.HudIconSet],"Color: "+HudColorNames[_profile.HudColorTheme],"Text: "+HudTextNames[_profile.HudTextStyle],"State: "+OnOff(_profile.HudShowState),"Health: "+OnOff(_profile.HudShowHealth),"Stamina: "+OnOff(_profile.HudShowStamina),"Food: "+OnOff(_profile.HudShowFood),"Water: "+OnOff(_profile.HudShowWater),"Certifications: "+OnOff(_profile.HudShowCertifications),"Trust: "+OnOff(_profile.HudShowTrust),"Training: "+OnOff(_profile.HudShowTraining),"Injury: "+OnOff(_profile.HudShowInjury),"Voice: "+OnOff(_profile.HudShowVoice),"← Back to K9 Profile"});}
        private void HandleHudMenu(int index){if(index>=4&&index<=13){_profile.ToggleHudField(index-4);RefreshHudMenu();}else if(index==14){_menuMode="profile";RefreshProfileMenu();}}
        private void AdjustHudMenu(int index,int delta){if(index==0)_profile.AdjustHudDesign(delta);else if(index==1)_profile.AdjustHudIcons(delta);else if(index==2)_profile.AdjustHudColor(delta);else if(index==3)_profile.AdjustHudText(delta);else return;RefreshHudMenu();}

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
            _activeSeatProfile=_seatProfiles.Get(_dogVehicle);NativeFunction.Natives.SET_VEHICLE_DOOR_OPEN(_dogVehicle,_dogVehicleDoor,false,false);_seatCalibrationDoorOpen=true;_menuMode="seat_config";RefreshSeatMenu();
        }
        private void RefreshSeatMenu(){if(_activeSeatProfile==null)return;_menu.Update("SEAT — "+_seatProfiles.VehicleName(_dogVehicle),new[]{"X left/right: "+_activeSeatProfile.X.ToString("0.000"),"Y forward/back: "+_activeSeatProfile.Y.ToString("0.000"),"Z up/down: "+_activeSeatProfile.Z.ToString("0.000"),"Save for this vehicle model","Reset to global defaults","← Back to K9 Profile"});}
        private void AdjustSeat(int index,int delta){if(index<0||index>2||_activeSeatProfile==null)return;float step=.02f*delta;if(index==0)_activeSeatProfile.X+=step;else if(index==1)_activeSeatProfile.Y+=step;else _activeSeatProfile.Z+=step;ApplySeatCalibration();Game.DisplaySubtitle("~b~Live K9 seat~s~  X "+_activeSeatProfile.X.ToString("0.000")+"  Y "+_activeSeatProfile.Y.ToString("0.000")+"  Z "+_activeSeatProfile.Z.ToString("0.000"),1000);RefreshSeatMenu();}
        private void HandleSeatMenu(int index){if(index==3){_seatProfiles.Save(_dogVehicle,_activeSeatProfile);Game.DisplayNotification("~g~Seat position saved for "+_seatProfiles.VehicleName(_dogVehicle)+".~s~~n~This model will use the calibration automatically.");RefreshSeatMenu();}else if(index==4){_activeSeatProfile=new VehicleSeatProfile(_config.VehicleSeatOffsetX,_config.VehicleSeatOffsetY,_config.VehicleSeatOffsetZ);ApplySeatCalibration();RefreshSeatMenu();}else if(index==5){CloseSeatCalibrationDoor();_menuMode="profile";RefreshProfileMenu();}}
        private void CloseSeatCalibrationDoor(){if(_seatCalibrationDoorOpen&&_dogVehicle!=null&&_dogVehicle.Exists())NativeFunction.Natives.SET_VEHICLE_DOOR_SHUT(_dogVehicle,_dogVehicleDoor,false);_seatCalibrationDoorOpen=false;}
        private void ApplySeatCalibration(){if(_dog==null||!_dog.Exists()||_dogVehicle==null||!_dogVehicle.Exists()||_activeSeatProfile==null)return;string boneName=_dogVehicleDoor==2?"seat_dside_r":_dogVehicleDoor==3?"seat_pside_r":"seat_pside_f";int bone=NativeFunction.Natives.GET_ENTITY_BONE_INDEX_BY_NAME<int>(_dogVehicle,boneName);if(bone<0){Game.DisplayNotification("~r~This vehicle has no compatible rear-seat bone.");return;}Vector3 bonePosition=NativeFunction.Natives.GET_WORLD_POSITION_OF_ENTITY_BONE<Vector3>(_dogVehicle,bone);_dog.Tasks.ClearImmediately();NativeFunction.Natives.DETACH_ENTITY(_dog,true,true);NativeFunction.Natives.SET_ENTITY_COORDS_NO_OFFSET(_dog,bonePosition.X,bonePosition.Y,bonePosition.Z,false,false,false);NativeFunction.Natives.SET_ENTITY_COLLISION(_dog,false,false);NativeFunction.Natives.ATTACH_ENTITY_TO_ENTITY(_dog,_dogVehicle,bone,_activeSeatProfile.X,_activeSeatProfile.Y,_activeSeatProfile.Z,0f,0f,0f,false,false,false,false,2,true);PlayDogAnimation("creatures@rottweiler@amb@world_dog_sitting@base","base",-1,1);_dogSeatAttached=true;Game.LogTrivial("AdvancedK9 live seat preview: "+_seatProfiles.VehicleName(_dogVehicle)+" X="+_activeSeatProfile.X.ToString("0.000")+" Y="+_activeSeatProfile.Y.ToString("0.000")+" Z="+_activeSeatProfile.Z.ToString("0.000"));}

        private void Execute(K9Command command)
        {
            try
            {
                if(_state==K9State.Leashed&&(command==K9Command.SearchArea||command==K9Command.SearchBuilding||command==K9Command.SearchVehicle||command==K9Command.SearchNarcotics||command==K9Command.SearchExplosives||command==K9Command.SearchWeapons||command==K9Command.Track||command==K9Command.FindTrail||command==K9Command.Apprehend||command==K9Command.Fetch)){DeleteLeashRope();_state=K9State.Following;ActionNotification("~b~Leash automatically released for K9 deployment.");}
                if(_state==K9State.Leashed&&(command==K9Command.Training||command==K9Command.TrainNarcotics||command==K9Command.TrainExplosives||command==K9Command.TrainWeapons)){Game.DisplayNotification("~y~Remove the leash before traveling to the academy.");return;}
                if(_profile.Health<=25 && command!=K9Command.Inspect && command!=K9Command.FirstAid && command!=K9Command.SpawnDismiss){Game.DisplayNotification("~r~K9 REMOVED FROM SERVICE~s~~n~Serious injury requires veterinary treatment. Earned certifications remain saved.");return;}
                if (RequiresTrustCheck(command) && !TrustAllowsCommand(command)) return;
                switch (command)
                {
                    case K9Command.SpawnDismiss: HandleKennelDeployment(); break;
                    case K9Command.Follow:
                    case K9Command.Heel: Follow(); break;
                    case K9Command.Sit: Sit(); break;
                    case K9Command.LieDown: LieDown(); break;
                    case K9Command.Stay: Stay(); break;
                    case K9Command.Recall: Follow(); break;
                    case K9Command.WhistleRecall: WhistleRecall(); break;
                    case K9Command.HandSignal: HandSignal(); break;
                    case K9Command.SearchArea: Search(false); break;
                    case K9Command.SearchBuilding: SearchBuilding(); break;
                    case K9Command.SearchVehicle: Search(true); break;
                    case K9Command.SearchNarcotics: Search(false,DetectionSpecialty.Narcotics); break;
                    case K9Command.SearchExplosives: Search(false,DetectionSpecialty.Explosives); break;
                    case K9Command.SearchWeapons: Search(false,DetectionSpecialty.Weapons); break;
                    case K9Command.CollectScent: CollectScent(); break;
                    case K9Command.Track: Track(); break;
                    case K9Command.FindTrail: ReacquireTrail(); break;
                    case K9Command.K9Warning: K9Warning(); break;
                    case K9Command.Apprehend: Apprehend(); break;
                    case K9Command.HandoffArrest: CompatibilityArrestHandoff(); break;
                    case K9Command.RequestPerimeter: CompatibilityService("Perimeter"); break;
                    case K9Command.RequestTransport: CompatibilityService("Transport"); break;
                    case K9Command.RequestMedical: CompatibilityService("Medical"); break;
                    case K9Command.RequestBombSquad: CompatibilityService("BombSquad"); break;
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
            return command == K9Command.Sit || command == K9Command.LieDown || command == K9Command.SearchArea || command==K9Command.SearchBuilding || command == K9Command.SearchVehicle || command==K9Command.SearchNarcotics || command==K9Command.SearchExplosives || command==K9Command.SearchWeapons ||
                   command == K9Command.Track || command==K9Command.FindTrail || command == K9Command.Fetch;
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

        private void HandleKennelDeployment()
        {
            StationKennel kennel=NearestKennel(7f);if(kennel==null){Game.DisplayNotification("~y~K9 kennel required.~s~~n~Pick up and return your K9 at a police-station doghouse.");return;}
            if(DogExists())Dismiss(true);else Deploy(kennel);
        }

        private void SpawnStationKennels()
        {
            if(_stationKennels.Count==0)_stationKennels.AddRange(new[]{
                new StationKennel("Mission Row Police Station",new Vector3(452.15f,-1011.45f,28.48f),90f),
                new StationKennel("Davis Police Station",new Vector3(368.72f,-1602.36f,29.29f),320f),
                new StationKennel("Vespucci Police Station",new Vector3(-1112.76f,-846.47f,13.44f),126f),
                new StationKennel("Rockford Hills Police Station",new Vector3(-564.82f,-128.64f,38.22f),205f),
                new StationKennel("Vinewood Police Station",new Vector3(631.64f,2.16f,82.79f),250f),
                new StationKennel("La Mesa Police Station",new Vector3(816.81f,-1290.32f,26.28f),91f),
                new StationKennel("Sandy Shores Sheriff Station",new Vector3(1844.67f,3690.84f,34.27f),210f),
                new StationKennel("Paleto Bay Sheriff Station",new Vector3(-442.36f,6012.58f,31.72f),315f),
                new StationKennel("LSIA Police Station",new Vector3(-1037.76f,-2732.31f,20.17f),240f)
            });
            var model=new Model("prop_doghouse_01");if(!model.IsValid){Game.LogTrivial("AdvancedK9 kennel prop unavailable: prop_doghouse_01.");return;}model.LoadAndWait();
            foreach(StationKennel kennel in _stationKennels)if(kennel.Prop==null||!kennel.Prop.Exists()){kennel.Prop=new Rage.Object(model,kennel.Position);kennel.Prop.Heading=kennel.Heading;kennel.Prop.IsPersistent=true;}
            model.Dismiss();
        }

        private void MaintainStationKennels()
        {
            StationKennel kennel=NearestKennel(3.2f);if(kennel==null)return;
            Game.DisplayHelp((DogExists()?"Return "+_profile.Name+" to":"Pick up "+_profile.Name+" from")+" the K9 kennel: hold "+_config.ModifierKey+" and press "+_config.SpawnKey+".");
        }

        private StationKennel NearestKennel(float radius){var handler=Game.LocalPlayer.Character;if(handler==null||!handler.Exists())return null;return _stationKennels.Where(k=>k.Prop!=null&&k.Prop.Exists()&&k.Position.DistanceTo(handler.Position)<=radius).OrderBy(k=>k.Position.DistanceTo(handler.Position)).FirstOrDefault();}
        private void DeleteStationKennels(){foreach(StationKennel kennel in _stationKennels)try{if(kennel.Prop!=null&&kennel.Prop.Exists())kennel.Prop.Delete();}catch{}foreach(StationKennel kennel in _stationKennels)kennel.Prop=null;}
        private static Vector3 HeadingOffset(float heading,float distance){double radians=heading*Math.PI/180.0;return new Vector3((float)(-Math.Sin(radians)*distance),(float)(Math.Cos(radians)*distance),0f);}

        private void Deploy(StationKennel kennel)
        {
            var officer = Game.LocalPlayer.Character;
            Vector3 release=kennel.Position+HeadingOffset(kennel.Heading,1.4f);
            if(!CreateDogAt(release,kennel.Heading))return;
            _profile.RecordDeployment();
            Follow();
            ActionNotification("~b~K9 " + _profile.Name + "~s~ picked up from " + kennel.Name + ".~n~" + _profile.Breed + " • " + _profile.Vest + " vest");
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

        private void EnterVehicle(){if(!DogExists())return;var handler=Game.LocalPlayer.Character;Vehicle vehicle=handler.CurrentVehicle;if(vehicle==null||!vehicle.Exists())vehicle=World.GetAllVehicles().Where(v=>v.Exists()&&v.DistanceTo(handler)<8f).OrderBy(v=>v.DistanceTo(handler)).FirstOrDefault();if(vehicle==null){Game.DisplayNotification("~y~No vehicle nearby.");return;}int seat=-99;foreach(int candidate in new[]{2,1,0})if(NativeFunction.Natives.IS_VEHICLE_SEAT_FREE<bool>(vehicle,candidate,false)){seat=candidate;break;}if(seat==-99){Game.DisplayNotification("~y~No open rear/passenger seat for the K9.");return;}_dogVehicleDoor=seat==2?3:seat==1?2:1;NativeFunction.Natives.SET_VEHICLE_DOOR_OPEN(vehicle,_dogVehicleDoor,false,false);Vector3 kennel=vehicle.GetOffsetPosition(new Vector3(seat==1?-1.15f:1.15f,-1.25f,0f));_dog.Tasks.FollowNavigationMeshToPosition(kennel,vehicle.Heading,1.6f).WaitForCompletion(4500);Sit();Game.DisplaySubtitle("~b~K9 waiting at the open kennel door. Loading...",900);GameFiber.Wait(750);NativeFunction.Natives.TASK_ENTER_VEHICLE(_dog,vehicle,8000,seat,2f,1,0);uint timeout=Game.GameTime+8000;while(DogExists()&&_dog.CurrentVehicle!=vehicle&&Game.GameTime<timeout)GameFiber.Yield();if(_dog.CurrentVehicle!=vehicle)NativeFunction.Natives.TASK_WARP_PED_INTO_VEHICLE(_dog,vehicle,seat);GameFiber.Wait(250);_dogVehicle=vehicle;_activeSeatProfile=_seatProfiles.Get(vehicle);ApplySeatCalibration();NativeFunction.Natives.SET_VEHICLE_DOOR_SHUT(vehicle,_dogVehicleDoor,false);NativeFunction.Natives.SET_ENTITY_INVINCIBLE(_dog,true);_state=K9State.InVehicle;K9IncidentLog.Write(_profile.Name,"Kennel","Loaded saved "+_seatProfiles.VehicleName(vehicle)+" seat profile",vehicle.Position);Acknowledge("Sitting safely in the saved right-rear position.");}
        private void ExitVehicle(){if(_dog==null||!_dog.Exists())return;var vehicle=_dogVehicle!=null&&_dogVehicle.Exists()?_dogVehicle:_dog.CurrentVehicle;if(vehicle==null||!vehicle.Exists()){ReleaseVehicleSeat();Follow();return;}if(vehicle.Speed>1.5f){Game.DisplayNotification("~y~Stop the vehicle before unloading the K9.");return;}NativeFunction.Natives.SET_VEHICLE_DOOR_OPEN(vehicle,_dogVehicleDoor,false,false);GameFiber.Wait(650);int savedHealth=Math.Max(100,_dog.Health);Vector3 exit=vehicle.GetOffsetPosition(new Vector3(_dogVehicleDoor==2?-1.35f:1.35f,-1.25f,.15f));_dog.Tasks.ClearImmediately();ReleaseVehicleSeat();_dog.Position=exit;_dog.Heading=vehicle.Heading;if(_dog.IsDead)NativeFunction.Natives.RESURRECT_PED(_dog);_dog.Health=savedHealth;GameFiber.Wait(650);NativeFunction.Natives.SET_VEHICLE_DOOR_SHUT(vehicle,_dogVehicleDoor,false);K9IncidentLog.Write(_profile.Name,"Kennel","Unloaded",exit);Follow();}

        private void ReleaseVehicleSeat(){CloseSeatCalibrationDoor();if(_dog!=null&&_dog.Exists()){if(_dogSeatAttached)NativeFunction.Natives.DETACH_ENTITY(_dog,true,true);NativeFunction.Natives.SET_ENTITY_COLLISION(_dog,true,true);NativeFunction.Natives.SET_ENTITY_INVINCIBLE(_dog,false);}_dogSeatAttached=false;_dogVehicle=null;}

        private void Inspect(){Game.DisplayNotification("~b~K9 "+_profile.Name+" — FIELD INSPECTION~s~~n~Health: "+_profile.Health+"%  Stamina: "+_profile.Stamina+"%~n~Food: "+_profile.Food+"%  Water: "+_profile.Water+"%~n~Training: Level "+_profile.TrainingLevel+"/5 "+_profile.TrainingLevelProgress+"%~n~~g~Completed certifications:~s~ "+Certifications());Game.DisplayNotification("~b~DUTY EQUIPMENT~s~~n~Meals "+_profile.FoodMeals+"  Water "+_profile.WaterBottles+"  First aid "+_profile.FirstAidKits+"~n~Scent bags "+_profile.ScentBags+"  Treats "+_profile.Treats+"~n~~b~Integration:~s~ "+_pr.ModeLabel);}
        private string Certifications(){string s="";if(_profile.ObedienceCertified)s+="OB ";if(_profile.AgilityCertified)s+="AGI ";if(_profile.DetectionCertified)s+="DET ";if(_profile.NarcoticsCertified)s+="NAR ";if(_profile.ExplosivesCertified)s+="BOMB ";if(_profile.WeaponsCertified)s+="WPN ";if(_profile.TrackingCertified)s+="TRK ";if(_profile.ApprehensionCertified)s+="APP ";return s.Length==0?"In training":s.Trim();}
        private void FirstAid(){if(!DogExists())return;if(_profile.Health>=95){Game.DisplayNotification("~g~No field treatment required.");return;}if(!_profile.UseFirstAid()){Game.DisplayNotification("~r~No first-aid kits. Restock at the patrol vehicle.");return;}Sit();GameFiber.Wait(1800);_dog.Health=Math.Max(_dog.Health,(int)(_dog.MaxHealth*_profile.Health/100f));_profile.ChangeTrust(2);K9IncidentLog.Write(_profile.Name,"Medical","Field first aid",_dog.Position);Game.DisplayNotification("~g~Field first aid applied.~s~~n~Serious injuries still require veterinary care.");}

        private void Rest(){if(!DogExists())return;LieDown();ActionNotification("~b~K9 rest cycle started.~s~ Maintain a safe perimeter.");GameFiber.Wait(8000);_profile.Rest();K9IncidentLog.Write(_profile.Name,"Care","Rest cycle",_dog.Position);ActionNotification("~g~K9 rested.~s~ Stamina restored.");}
        private void Bathroom()
        {
            if(!DogExists()||_state==K9State.InVehicle||_state==K9State.Searching||_state==K9State.Tracking||_state==K9State.Apprehending)return;
            bool urinate=_bladder<_bowel||(_bladder==_bowel&&_random.Next(2)==0);_dog.Tasks.Clear();_state=K9State.Staying;Vector3 reliefPoint=_dog.GetOffsetPosition(new Vector3(0f,-.35f,0f));
            if(urinate){PlayDogAnimation("creatures@rottweiler@move","pee",3200,0);try{NativeFunction.Natives.REQUEST_NAMED_PTFX_ASSET("core");GameFiber.Wait(100);NativeFunction.Natives.USE_PARTICLE_FX_ASSET("core");NativeFunction.Natives.START_PARTICLE_FX_NON_LOOPED_AT_COORD("ent_sht_water",reliefPoint.X,reliefPoint.Y,reliefPoint.Z+.12f,0f,0f,0f,.35f,false,false,false);}catch{} _bladder=100;K9IncidentLog.Write(_profile.Name,"Care","Urinated automatically",reliefPoint);}
            else{PlayDogAnimation("creatures@rottweiler@amb@world_dog_sitting@base","base",3000,0);SpawnDogWaste(reliefPoint);_bowel=100;K9IncidentLog.Write(_profile.Name,"Care","Defecated automatically",reliefPoint);}Follow();
        }
        private void SpawnDogWaste(Vector3 position){try{var model=new Model("prop_big_shit_02");if(!model.IsValid)model=new Model("prop_big_shit_01");if(!model.IsValid)return;model.LoadAndWait();var waste=new Rage.Object(model,position);model.Dismiss();if(waste==null||!waste.Exists())return;waste.IsPersistent=true;GameFiber.StartNew(()=>{GameFiber.Wait(180000);if(waste.Exists())waste.Delete();});}catch(Exception ex){Game.LogTrivial("AdvancedK9 dog waste prop: "+ex.Message);}}
        private void VeterinaryCare(){if(!DogExists())return;var handler=Game.LocalPlayer.Character;Vector3 p=handler.Position;float h=handler.Heading;NativeFunction.Natives.DO_SCREEN_FADE_OUT(450);GameFiber.Wait(550);handler.Position=new Vector3(306.7f,-595.2f,43.3f);_dog.Position=handler.GetOffsetPosition(new Vector3(1f,0f,0f));GameFiber.Wait(1800);_profile.VeterinaryTreat();_dog.Health=_dog.MaxHealth;handler.Position=p;handler.Heading=h;_dog.Position=handler.GetOffsetPosition(new Vector3(-1f,-1f,0f));NativeFunction.Natives.DO_SCREEN_FADE_IN(450);K9IncidentLog.Write(_profile.Name,"Medical","Veterinary cleared",p);Game.DisplayNotification("~g~Veterinary clearance complete.~s~ K9 returned to service.");Follow();}
        private void Restock(){var handler=Game.LocalPlayer.Character;var vehicle=World.GetAllVehicles().Where(v=>v.Exists()&&v.DistanceTo(handler)<5f).FirstOrDefault();if(vehicle==null){Game.DisplayNotification("~y~Stand beside a patrol vehicle to restock.");return;}_profile.Restock();K9IncidentLog.Write(_profile.Name,"Equipment","Restocked",handler.Position);Game.DisplayNotification("~g~K9 duty equipment restocked.");}
        private void WhistleRecall(){NativeFunction.Natives.PLAY_SOUND_FRONTEND(-1,"NAV_UP_DOWN","HUD_FRONTEND_DEFAULT_SOUNDSET",true);Game.DisplaySubtitle("~b~Handler whistle recall",1200);Follow();}
        private void HandSignal(){var handler=Game.LocalPlayer.Character;NativeFunction.Natives.REQUEST_ANIM_DICT("gestures@m@standing@casual");GameFiber.Wait(150);handler.Tasks.PlayAnimation("gestures@m@standing@casual","gesture_come_here_soft",4f,AnimationFlags.None);GameFiber.Wait(700);Follow();}

        private void CaptureScentTrails()
        {
            if(Game.GameTime<_nextTrailCapture)return;_nextTrailCapture=Game.GameTime+1200;var handler=Game.LocalPlayer.Character;uint cutoff=Game.GameTime>600000?Game.GameTime-600000:0;
            foreach(var ped in World.GetAllPeds())
            {
                if(ped==null||!ped.Exists()||ped==handler||ped==_dog||ped.IsDead||ped.DistanceTo(handler)>350f||LspdfrBridge.IsPedCop(ped))continue;
                List<ScentTrailPoint> trail;if(!_recordedTrails.TryGetValue(ped.Handle,out trail)){trail=new List<ScentTrailPoint>();_recordedTrails[ped.Handle]=trail;}
                if(trail.Count==0||trail[trail.Count-1].Position.DistanceTo(ped.Position)>=3f)trail.Add(new ScentTrailPoint(ped.Position,Game.GameTime));
                trail.RemoveAll(p=>p.Time<cutoff);if(trail.Count>220)trail.RemoveRange(0,trail.Count-220);
            }
        }

        private void UpdateCompatibilityPursuit()
        {
            if(!_config.CompatibilityUseActiveTargets||Game.GameTime<_nextPursuitProbe)return;_nextPursuitProbe=Game.GameTime+1000;var suspect=_pr.GetPursuitSuspect(Game.LocalPlayer.Character);
            if(suspect==null||!suspect.Exists()||suspect.IsDead){_compatibilityPursuitSuspect=null;_pursuitLastVehicle=null;return;}
            if(_compatibilityPursuitSuspect!=suspect){_compatibilityPursuitSuspect=suspect;_pursuitLastVehicle=suspect.CurrentVehicle;Game.LogTrivial("AdvancedK9 pursuit integration: suspect assigned without requiring a PR/STP stop.");}
            var current=suspect.CurrentVehicle;
            if(current!=null&&current.Exists())_pursuitLastVehicle=current;
            else if(_pursuitLastVehicle!=null&&_pursuitLastVehicle.Exists()&&_scentTarget!=suspect)
            {
                _scentTarget=suspect;_scentCollectedAt=Game.GameTime;_scentRainAtCollection=NativeFunction.Natives.GET_RAIN_LEVEL<float>();_activeScentSource="Pursuit vehicle bailout";_trailLost=false;
                Game.DisplayNotification("~b~K9 pursuit update:~s~ suspect bailed out.~n~Vehicle scent and recorded foot trail assigned; command TRACK when ready.");
                K9IncidentLog.Write(_profile.Name,"Pursuit scent","Vehicle bailout assigned",suspect.Position);
            }
        }
        private void CollectScent()
        {
            var handler=Game.LocalPlayer.Character;
            Ped target=GetValidAimedSuspect(false);
            Vehicle sourceVehicle=null;
            if(target==null)
            {
                sourceVehicle=Game.LocalPlayer.GetFreeAimingTarget() as Vehicle;
                if(sourceVehicle==null||!sourceVehicle.Exists()||sourceVehicle.DistanceTo(handler)>25f)
                    sourceVehicle=World.GetAllVehicles().Where(v=>v.Exists()&&v.DistanceTo(handler)<=6f).OrderBy(v=>v.DistanceTo(handler)).FirstOrDefault();
                if(sourceVehicle!=null)target=FindVehicleScentSubject(sourceVehicle);
            }
            if(target==null)
            {
                Game.DisplayNotification("~y~No unique track subject identified.~s~~n~Aim at the suspect, or aim at/stand beside the vehicle they fled from, then collect scent.");
                return;
            }
            if(!_profile.UseScentBag()){Game.DisplayNotification("~r~No clean scent bags. Restock equipment.");return;}
            _scentTarget=target;_scentCollectedAt=Game.GameTime;_scentRainAtCollection=NativeFunction.Natives.GET_RAIN_LEVEL<float>();
            string source=sourceVehicle==null?"person":"vehicle "+sourceVehicle.Model.Name;_activeScentSource=source;_trailLost=false;
            K9IncidentLog.Write(_profile.Name,"Scent article","Collected from "+source,target.Position);
            Game.DisplayNotification("~g~Scent article bagged from "+source+".~s~~n~Track subject locked for "+_profile.Name+".");
        }

        private Ped FindVehicleScentSubject(Vehicle vehicle)
        {
            if(vehicle==null||!vehicle.Exists())return null;
            var handler=Game.LocalPlayer.Character;
            var candidates=new List<Ped>();
            foreach(var ped in World.GetAllPeds())
            {
                if(ped==null||!ped.Exists()||ped==handler||ped==_dog||ped.IsDead||LspdfrBridge.IsPedCop(ped))continue;
                try
                {
                    var lastVehicle=NativeFunction.Natives.GET_VEHICLE_PED_IS_IN<Vehicle>(ped,true);
                    if(lastVehicle!=null&&lastVehicle.Exists()&&lastVehicle.Handle==vehicle.Handle)candidates.Add(ped);
                }
                catch{}
            }
            if(candidates.Count==1)return candidates[0];
            if(candidates.Count>1)
            {
                Game.DisplayNotification("~y~Multiple recent occupants detected.~s~~n~Aim directly at the person to identify the correct track subject.");
                return null;
            }
            Game.DisplayNotification("~y~No recent non-officer occupant is available for that vehicle.~s~~n~Collect vehicle scent before the fleeing ped despawns.");
            return null;
        }

        private void Search(bool vehicleOnly=false,DetectionSpecialty specialty=DetectionSpecialty.General)
        {
            if(specialty!=DetectionSpecialty.General&&!_profile.HasSpecialty(specialty)){Game.DisplayNotification("~y~K9 is not certified for "+SpecialtyLabel(specialty)+" detection.~s~~n~Complete that specialty course at the academy.");return;}
            var officer = Game.LocalPlayer.Character;
            Entity target = FindCompatibilitySearchTarget(officer,vehicleOnly);
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
            var compatibility=_pr.GetSearchResult(target,specialty,_profile.NarcoticsCertified,_profile.ExplosivesCertified,_profile.WeaponsCertified);
            var positive=compatibility!=null?compatibility.Positive:(_random.NextDouble()<_config.PositiveChance);
            var resultSpecialty=compatibility!=null&&compatibility.Specialty!=DetectionSpecialty.General?compatibility.Specialty:positive&&specialty==DetectionSpecialty.General?CertifiedGeneralSearchSpecialty():specialty;
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
                Game.DisplayNotification("~r~POSITIVE "+SpecialtyLabel(resultSpecialty).ToUpperInvariant()+" K9 INDICATION~s~ — " + TargetLabel(target) + ".");
                _trust.Change(1, "successful detection");
                _profile.RecordSearch();
                _pr.RecordK9Indication(target,true,resultSpecialty);
            }
            else
            {
                Game.DisplayNotification("~g~No "+(specialty==DetectionSpecialty.General?"certified odor":SpecialtyLabel(specialty))+" K9 indication~s~ on " + TargetLabel(target) + ".");
                Sit();
                _pr.RecordK9Indication(target,false,specialty);
            }
        }

        private void SearchBuilding()
        {
            if(!DogExists())return;var handler=Game.LocalPlayer.Character;var target=_config.CompatibilityUseActiveTargets?_pr.GetActivePed(handler,60f)??_pr.GetPursuitSuspect(handler):null;
            if(target==null)target=World.GetAllPeds().Where(p=>p.Exists()&&p!=handler&&p!=_dog&&!p.IsDead&&!LspdfrBridge.IsPedCop(p)&&p.DistanceTo(handler)<=45f).OrderBy(p=>p.DistanceTo(handler)).FirstOrDefault();
            _state=K9State.Searching;_dog.Tasks.Clear();Vector3 origin=handler.GetOffsetPosition(new Vector3(0f,3f,0f));
            var points=new[]{origin,handler.GetOffsetPosition(new Vector3(-3f,7f,0f)),handler.GetOffsetPosition(new Vector3(3f,10f,0f)),handler.GetOffsetPosition(new Vector3(-2f,14f,0f)),handler.GetOffsetPosition(new Vector3(2f,18f,0f)),target!=null?target.Position:handler.GetOffsetPosition(new Vector3(0f,22f,0f))};
            Game.DisplayNotification("~b~Building search started.~s~~n~K9 will clear six sectors, then bark and hold only if a subject is located.");
            for(int i=0;i<points.Length;i++)
            {
                if(!DogExists()||_state!=K9State.Searching){Follow();return;}Game.DisplaySubtitle("~b~Building sector "+(i+1)+"/6",1300);_dog.Tasks.FollowNavigationMeshToPosition(points[i],handler.Heading,2.4f).WaitForCompletion(5500);PlayDogAnimation("creatures@rottweiler@indication@","indicate_low",500,0);GameFiber.Wait(200);
                if(target!=null&&target.Exists()&&_dog.DistanceTo(target)<4f){_dog.Tasks.Clear();Bark(2);Sit();_pr.RecordLocatedSuspect(target);K9IncidentLog.Write(_profile.Name,"Building search","Subject located; alert and hold",target.Position);K9DeploymentReport.Write("Player",_profile.Name,"Building search","Structure clearance","None",false,0f,0,0,"Located","None","Alert bark and hold",target.Position);Game.DisplayNotification("~g~Building search: subject located.~s~~n~K9 is sitting and holding; Apprehend requires a separate aimed command.");return;}
            }
            Sit();K9DeploymentReport.Write("Player",_profile.Name,"Building search","Structure clearance","None",false,0f,0,0,"None","None","No subject located",handler.Position);Game.DisplayNotification("~g~Building search complete.~s~ No subject located in the cleared sectors.");
        }

        private void K9Warning()
        {
            if(!DogExists())return;var handler=Game.LocalPlayer.Character;var target=GetValidAimedSuspect(false)??(_voiceAimedTarget!=null&&_voiceAimedTarget.Exists()?_voiceAimedTarget:null)??(_config.CompatibilityUseActiveTargets?_pr.GetActivePed(handler,250f)??_pr.GetPursuitSuspect(handler):null);_voiceAimedTarget=null;
            if(target==null||!target.Exists()||target.IsDead){Game.DisplayNotification("~y~No suspect identified for the K9 warning.~s~~n~Aim at the suspect or use the active PR/STP target.");return;}
            Follow();GameFiber.Wait(350);Bark(1);_warningGiven=true;_warnedTarget=target;_warningSurrendered=false;Game.DisplaySubtitle("~r~POLICE K9! SHOW ME YOUR HANDS! COME OUT NOW OR THE DOG WILL BE RELEASED!",4200);NativeFunction.Natives.PLAY_SOUND_FRONTEND(-1,"TIMER_STOP","HUD_MINI_GAME_SOUNDSET",true);
            int roll=_random.Next(100);string outcome;
            if(roll<48){NativeFunction.Natives.TASK_HANDS_UP(target,20000,handler,-1,true);_warningSurrendered=true;Sit();outcome="Surrendered";Game.DisplayNotification("~g~Suspect surrendered following the K9 warning.~s~~n~K9 is holding; move in for arrest.");}
            else if(roll<73){NativeFunction.Natives.TASK_SMART_FLEE_PED(target,handler,180f,-1,false,false);outcome="Fled";Game.DisplayNotification("~o~Suspect fled following the K9 warning.~s~~n~Aim and command Apprehend, or collect/assign scent and Track.");}
            else if(roll<92){NativeFunction.Natives.TASK_STAND_STILL(target,7000);Sit();outcome="Froze";Game.DisplayNotification("~y~Suspect froze but has not surrendered.~s~ Maintain cover and issue lawful commands.");}
            else{NativeFunction.Natives.TASK_COMBAT_PED(target,handler,0,16);outcome="Attacked handler";Game.DisplayNotification("~r~Suspect attacked following the K9 warning.");}
            K9IncidentLog.Write(_profile.Name,"K9 warning",outcome,target.Position);
        }

        private Ped CompatibilitySubject()
        {
            var handler=Game.LocalPlayer.Character;return GetValidAimedSuspect(false)??(_config.CompatibilityUseActiveTargets?_pr.GetActivePed(handler,250f)??_pr.GetPursuitSuspect(handler):null)??(_scentTarget!=null&&_scentTarget.Exists()?_scentTarget:null);
        }
        private void CompatibilityArrestHandoff()
        {
            var target=CompatibilitySubject();if(target==null){Game.DisplayNotification("~y~No suspect identified for PR/STP arrest handoff.");return;}
            if(_pr.TryArrestHandoff(target)){Game.DisplayNotification("~g~Suspect handed to "+_pr.ModeLabel+" arrest workflow.");K9IncidentLog.Write(_profile.Name,"Compatibility","Arrest handoff to "+_pr.ModeLabel,target.Position);}
            else Game.DisplayNotification("~y~"+_pr.ModeLabel+" does not expose a compatible arrest handoff API.~s~~n~Use that plugin's normal interaction key on the located suspect.");
        }
        private void CompatibilityService(string service)
        {
            var target=CompatibilitySubject();if(_pr.TryRequestService(service,target)){Game.DisplayNotification("~g~"+service+" request sent through "+_pr.ModeLabel+".");K9IncidentLog.Write(_profile.Name,"Compatibility",service+" requested",target!=null?target.Position:Game.LocalPlayer.Character.Position);}
            else Game.DisplayNotification("~y~"+service+" service is unavailable through the detected "+_pr.ModeLabel+" API.~s~~n~Use the external plugin's normal backup/service menu.");
        }

        private Entity FindCompatibilitySearchTarget(Ped officer,bool vehicleOnly)
        {
            if(_config.CompatibilityUseActiveTargets)
            {
                if(vehicleOnly){var activeVehicle=_pr.GetActiveVehicle(officer,_config.SearchRadius);if(activeVehicle!=null)return activeVehicle;}
                else
                {
                    var activePed=_pr.GetActivePed(officer,_config.SearchRadius);if(activePed!=null)return activePed;
                    var activeVehicle=_pr.GetActiveVehicle(officer,_config.SearchRadius);if(activeVehicle!=null)return activeVehicle;
                }
            }
            return vehicleOnly?(Entity)World.GetAllVehicles().Where(v=>v.Exists()&&v.DistanceTo(officer)<=_config.SearchRadius).OrderBy(v=>v.DistanceTo(officer)).FirstOrDefault():FindSearchTarget(officer.Position,_config.SearchRadius);
        }

        private static string SpecialtyLabel(DetectionSpecialty specialty)=>specialty==DetectionSpecialty.Narcotics?"narcotics":specialty==DetectionSpecialty.Explosives?"explosives":specialty==DetectionSpecialty.Weapons?"weapons":"general odor";
        private DetectionSpecialty CertifiedGeneralSearchSpecialty(){var certified=new List<DetectionSpecialty>();if(_profile.NarcoticsCertified)certified.Add(DetectionSpecialty.Narcotics);if(_profile.ExplosivesCertified)certified.Add(DetectionSpecialty.Explosives);if(_profile.WeaponsCertified)certified.Add(DetectionSpecialty.Weapons);return certified.Count==0?DetectionSpecialty.General:certified[_random.Next(certified.Count)];}

        private bool SearchVehiclePerimeter(Vehicle vehicle)
        {
            Game.DisplayNotification("~b~K9 four-corner vehicle sweep started.~s~~n~A positive K9 barks only after all four corners are checked.");
            var points=new[]{new Vector3(-1.45f,2.45f,0f),new Vector3(1.45f,2.45f,0f),new Vector3(1.45f,-2.45f,0f),new Vector3(-1.45f,-2.45f,0f)};
            var labels=new[]{"front-left","front-right","rear-right","rear-left"};
            for(var i=0;i<points.Length;i++)
            {
                if(!DogExists()||!vehicle.Exists()||_state!=K9State.Searching)return false;
                var point=vehicle.GetOffsetPosition(points[i]);
                Game.DisplaySubtitle("~b~Vehicle search~s~ — "+labels[i]+" corner "+(i+1)+"/4",1800);
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
            var aimed=GetValidAimedSuspect(false);
            if(aimed==null&&_voiceAimedTarget!=null&&_voiceAimedTarget.Exists()&&!_voiceAimedTarget.IsDead&&!LspdfrBridge.IsPedCop(_voiceAimedTarget))aimed=_voiceAimedTarget;
            var target=aimed??(_scentTarget!=null&&_scentTarget.Exists()&&!_scentTarget.IsDead?_scentTarget:null)??(_config.CompatibilityUseActiveTargets?_pr.GetPursuitSuspect(Game.LocalPlayer.Character):null);
            _voiceAimedTarget=null;
            if (target == null)
            {
                Game.DisplayNotification("~y~No track subject assigned.~s~~n~Aim at the suspect when issuing TRACK, or collect their scent from a person/vehicle first.");
                return;
            }
            if(aimed!=null){_scentTarget=aimed;_scentCollectedAt=Game.GameTime;_scentRainAtCollection=NativeFunction.Natives.GET_RAIN_LEVEL<float>();_activeScentSource="Handler aim";_trailLost=false;Game.DisplayNotification("~b~Track subject identified by handler aim.~s~~n~"+_profile.Name+" is acquiring that person's recorded trail.");}
            _state = K9State.Tracking;
            float rain=NativeFunction.Natives.GET_RAIN_LEVEL<float>();float ageMinutes=_scentCollectedAt==0?0:(Game.GameTime-_scentCollectedAt)/60000f;float initialDistance=target.DistanceTo(Game.LocalPlayer.Character);bool inVehicle=target.CurrentVehicle!=null;int scentQuality=Math.Max(5,100-(int)(ageMinutes*8)-(int)(rain*35)-(int)(initialDistance/12)-(inVehicle?22:0));
            if(scentQuality<18){Game.DisplayNotification("~r~Scent trail is too degraded.~s~~n~Collect a fresh scent article; rain, age, distance, and vehicles weaken odor.");return;}
            Game.DisplayNotification("~b~K9 recorded scent track started.~s~ Quality "+scentQuality+"%~n~The K9 follows trail points instead of continuously reading the suspect's live position.");K9IncidentLog.Write(_profile.Name,"Track","Started quality "+scentQuality+"% from "+_activeScentSource,target.Position);
            _dog.Tasks.Clear();
            PlayDogAnimation("creatures@rottweiler@indication@","indicate_low",700,0);
            GameFiber.Wait(250);
            var end = Game.GameTime + 120000;
            uint nextScentCheck=Game.GameTime+(uint)_random.Next(18000,28001);
            var route=BuildRecordedTrailRoute(target);int routeIndex=0;_activeTrackDistance=0f;_activeTrackStarted=Game.GameTime;Vector3 previous=_dog.Position;
            while (_running && DogExists() && target.Exists() && !target.IsDead && Game.GameTime < end && _state == K9State.Tracking)
            {
                if (_dog.DistanceTo(target) < 3f)
                {
                    _dog.Tasks.Clear();
                    Bark(2);
                    Sit();
                    Game.DisplayNotification("~g~Track complete — person located.~s~~n~K9 is sitting and holding. Aim at the suspect and command APPREHEND only if deployment is required.");K9IncidentLog.Write(_profile.Name,"Track","Subject located; alert bark and hold only",target.Position);
                    _pr.RecordLocatedSuspect(target);
                    _trust.Change(2, "successful track");
                    int seconds=(int)((Game.GameTime-_activeTrackStarted)/1000);K9DeploymentReport.Write("Player",_profile.Name,"Track","Locate person",_activeScentSource,_warningGiven,_activeTrackDistance,seconds,0,"Located","None","Alert bark and hold",target.Position);
                    return;
                }
                if(routeIndex>=route.Count)
                {
                    route=BuildRecordedTrailRoute(target);routeIndex=0;
                    if(route.Count==0&&_dog.DistanceTo(target)>25f)
                    {
                        _trailLost=true;_dog.Tasks.Clear();Sit();Game.DisplayNotification("~o~K9 lost the recorded scent trail.~s~~n~Move to the last-known area and command REACQUIRE TRAIL.");K9IncidentLog.Write(_profile.Name,"Track","Trail lost",_dog.Position);return;
                    }
                }
                var destination=routeIndex<route.Count?route[routeIndex++]:target.Position;
                float dx = destination.X - _dog.Position.X, dy = destination.Y - _dog.Position.Y;
                float distance = (float)Math.Sqrt(dx * dx + dy * dy);
                float step = Math.Min(28f, Math.Max(12f, distance - 2f));
                float inv = distance > .01f ? 1f / distance : 0f;
                float scentJitter = (float)(_random.NextDouble() * 1.4 - .7);
                var waypoint = new Vector3(_dog.Position.X + dx * inv * step - dy * inv * scentJitter,
                                           _dog.Position.Y + dy * inv * step + dx * inv * scentJitter,
                                           destination.Z);
                _dog.Tasks.Clear();
                if(Game.GameTime>=nextScentCheck)
                {
                    PlayDogAnimation("creatures@rottweiler@indication@","indicate_low",550,0);
                    GameFiber.Wait(150);
                    nextScentCheck=Game.GameTime+(uint)_random.Next(18000,28001);
                }
                _dog.Tasks.FollowNavigationMeshToPosition(waypoint,target.Heading,rain>.35f?2.8f:4.2f).WaitForCompletion(8500);
                _activeTrackDistance+=previous.DistanceTo(_dog.Position);previous=_dog.Position;
                _profile.UseStamina(1);
                GameFiber.Yield();
            }
            int elapsed=(int)((Game.GameTime-_activeTrackStarted)/1000);K9DeploymentReport.Write("Player",_profile.Name,"Track","Locate person",_activeScentSource,_warningGiven,_activeTrackDistance,elapsed,0,"Not located","None","Track ended",_dog.Position);
            Follow();
        }

        private List<Vector3> BuildRecordedTrailRoute(Ped target)
        {
            var result=new List<Vector3>();if(target==null||!target.Exists())return result;List<ScentTrailPoint> trail;if(!_recordedTrails.TryGetValue(target.Handle,out trail)||trail.Count==0)return result;
            uint cutoff=Game.GameTime>300000?Game.GameTime-300000:0;var available=trail.Where(p=>p.Time>=cutoff).ToList();if(available.Count==0)return result;
            int start=0;float best=float.MaxValue;for(int i=0;i<available.Count;i++){float d=available[i].Position.DistanceTo(_dog.Position);if(d<best){best=d;start=i;}}
            for(int i=start;i<available.Count;i++)if(result.Count==0||result[result.Count-1].DistanceTo(available[i].Position)>=5f)result.Add(available[i].Position);
            Game.LogTrivial("AdvancedK9 recorded trail: target="+target.Handle+", points="+result.Count+", nearestDistance="+best.ToString("0.0")+"m.");return result;
        }

        private void ReacquireTrail()
        {
            if(!DogExists()||_scentTarget==null||!_scentTarget.Exists()||_scentTarget.IsDead){Game.DisplayNotification("~y~No scent target is available to reacquire.");return;}
            _state=K9State.Tracking;var center=_dog.Position;Game.DisplayNotification("~b~K9 trail reacquisition started.~s~ The dog will cast around the last-known point.");
            foreach(var offset in new[]{new Vector3(-3f,2f,0f),new Vector3(3f,2f,0f),new Vector3(0f,-3f,0f)}){_dog.Tasks.FollowNavigationMeshToPosition(new Vector3(center.X+offset.X,center.Y+offset.Y,center.Z),_dog.Heading,1.7f).WaitForCompletion(3000);PlayDogAnimation("creatures@rottweiler@indication@","indicate_low",550,0);GameFiber.Wait(150);}
            var route=BuildRecordedTrailRoute(_scentTarget);if(route.Count==0){_trailLost=true;Sit();Game.DisplayNotification("~r~Trail reacquisition unsuccessful.~s~ Move closer to the last-known route or collect a fresher scent article.");return;}
            _trailLost=false;Game.DisplayNotification("~g~Recorded trail reacquired.~s~ K9 is committing to the recovered direction.");Track();
        }

        private void Apprehend()
        {
            var handler=Game.LocalPlayer.Character;
            var target=GetValidAimedSuspect(false);
            if(target==null&&_voiceAimedTarget!=null&&_voiceAimedTarget.Exists()&&!_voiceAimedTarget.IsDead&&_voiceAimedTarget.DistanceTo(handler)<=250f&&!LspdfrBridge.IsPedCop(_voiceAimedTarget))target=_voiceAimedTarget;
            _voiceAimedTarget=null;
            if(target==null){Game.DisplayNotification("~y~No valid target identified.~s~~n~Aim your taser or firearm directly at a non-officer, then issue APPREHEND. No ped stop is required.");return;}
            if(_warnedTarget==target&&_warningSurrendered){Game.DisplayNotification("~r~K9 safety interlock: the warned suspect surrendered.~s~~n~Move in for arrest; apprehension was not deployed.");return;}
            if(_config.CompatibilityProtectManagedPeds&&_pr.IsProtectedPed(target)){Game.DisplayNotification("~r~K9 safety interlock: restrained or surrendered suspect rejected.~s~~n~PR/STP stop status is not required for deployment, but protected peds cannot be bitten.");return;}
            if(_state==K9State.InVehicle)DoorPop(false);
            _state = K9State.Apprehending;
            _dog.Tasks.Clear();
            string reaction="Immediate aimed deployment";K9IncidentLog.Write(_profile.Name,"Apprehension",reaction,target.Position);_biteStarted=Game.GameTime;
            NativeFunction.Natives.TASK_COMBAT_PED(_dog, target, 0, 16);
            Game.DisplayNotification("~o~K9 deploying immediately on aimed target.~s~~n~No traffic stop or close-range contact is required.");
            var end = Game.GameTime + 25000;
            while (DogExists() && target.Exists() && !target.IsDead && Game.GameTime < end && _state == K9State.Apprehending)
            {
                if (target.Health <= _config.NonLethalHealthFloor || target.IsRagdoll)
                {
                    _dog.Tasks.ClearImmediately();
                    if (target.Health < _config.NonLethalHealthFloor) target.Health = _config.NonLethalHealthFloor;
                    NativeFunction.Natives.TASK_HANDS_UP(target, -1, Game.LocalPlayer.Character, -1, true);
                    Game.DisplayNotification("~g~Suspect neutralized without lethal force.~s~ Move in for arrest.");
                    _pr.RecordApprehension(target);
                    _trust.Change(1, "controlled apprehension");
                    int biteSeconds=(int)((Game.GameTime-_biteStarted)/1000);K9DeploymentReport.Write("Player",_profile.Name,"Apprehension",reaction,_activeScentSource,_warningGiven,_activeTrackDistance,_activeTrackStarted==0?0:(int)((Game.GameTime-_activeTrackStarted)/1000),biteSeconds,"Controlled surrender",_profile.Injury,"Suspect ready for PR/STP arrest",target.Position);
                    Follow();
                    return;
                }
                GameFiber.Yield();
            }
            int finalBiteSeconds=(int)((Game.GameTime-_biteStarted)/1000);bool targetExists=target!=null&&target.Exists();K9DeploymentReport.Write("Player",_profile.Name,"Apprehension",reaction,_activeScentSource,_warningGiven,_activeTrackDistance,_activeTrackStarted==0?0:(int)((Game.GameTime-_activeTrackStarted)/1000),finalBiteSeconds,targetExists?(target.IsDead?"Deceased":"Not controlled"):"Entity unavailable",_profile.Injury,"Deployment ended",targetExists?target.Position:_dog.Position);Follow();
        }
        private void DoorPop(){DoorPop(true);}
        private void DoorPop(bool followAfter){if(!DogExists()||_state!=K9State.InVehicle){if(followAfter)Game.DisplayNotification("~y~K9 is not secured in the vehicle.");return;}var vehicle=_dogVehicle;if(vehicle==null||!vehicle.Exists()||vehicle.Speed>2f){Game.DisplayNotification("~y~Vehicle must be stopped for door-pop deployment.");return;}NativeFunction.Natives.SET_VEHICLE_DOOR_OPEN(vehicle,_dogVehicleDoor,false,false);GameFiber.Wait(450);Vector3 exit=vehicle.GetOffsetPosition(new Vector3(_dogVehicleDoor==2?-1.4f:1.4f,-1.2f,.15f));ReleaseVehicleSeat();_dog.Position=exit;_dog.Health=Math.Max(_dog.Health,100);GameFiber.Wait(350);NativeFunction.Natives.SET_VEHICLE_DOOR_SHUT(vehicle,_dogVehicleDoor,false);K9IncidentLog.Write(_profile.Name,"Door pop","Deployed",exit);if(followAfter)Follow();}

        private Ped GetValidAimedSuspect(bool notify)
        {
            var handler=Game.LocalPlayer.Character;
            uint weapon=NativeFunction.Natives.GET_SELECTED_PED_WEAPON<uint>(handler),unarmed=NativeFunction.Natives.GET_HASH_KEY<uint>("WEAPON_UNARMED");
            var target=weapon==unarmed?null:Game.LocalPlayer.GetFreeAimingTarget() as Ped;
            if(target==null||!target.Exists()||target==handler||target==_dog||target.IsDead||target.DistanceTo(handler)>250f)return null;
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
            handler.Tasks.PlayAnimation("amb@medic@standing@kneel@base","base",4f,AnimationFlags.Loop);
            _dog.Tasks.PlayAnimation("creatures@rottweiler@tricks@","petting_franklin",4f,AnimationFlags.Loop);GameFiber.Wait(4200);
            handler.Tasks.Clear();Sit();
            ActionNotification("~b~" + _profile.Name + "~s~ enjoyed that.");
            _trust.Change(2, "handler bonding");
            _profile.ChangeTrust(2);_profile.Recover(3);
        }

        private void Feed()
        {
            if (_dog.DistanceTo(Game.LocalPlayer.Character) > 2.5f) { Game.DisplayNotification("~y~Move closer to your K9."); return; }
            if(!_profile.FeedMeal()){Game.DisplayNotification("~r~No K9 meals remaining. Restock at the patrol vehicle.");return;}UseBowl(false);_bowel=Math.Max(0,_bowel-12);
            ActionNotification("~b~You fed " + _profile.Name + ".~s~ Food restored to 100%.");
            _trust.Change(2, "care and feeding");
            _profile.ChangeTrust(2);_profile.Recover(8);
        }

        private void Drink(){if(_dog.DistanceTo(Game.LocalPlayer.Character)>2.5f){Game.DisplayNotification("~y~Move closer to your K9.");return;}if(!_profile.GiveWater()){Game.DisplayNotification("~r~No water bottles remaining. Restock at the patrol vehicle.");return;}UseBowl(true);_bladder=Math.Max(0,_bladder-15);ActionNotification("~b~"+_profile.Name+" drank fresh water.~s~ Water restored to 100%.");_trust.Change(1,"handler care");_profile.ChangeTrust(1);}

        private void UseBowl(bool water)
        {
            Rage.Object bowl=null;try{var model=new Model("prop_cs_bowl_01");if(!model.IsValid)model=new Model("prop_bowl_crisps");Vector3 bowlPosition=_dog.GetOffsetPosition(new Vector3(0f,.75f,0f));if(model.IsValid){model.LoadAndWait();bowl=new Rage.Object(model,bowlPosition);model.Dismiss();}NativeFunction.Natives.TASK_TURN_PED_TO_FACE_COORD(_dog,bowlPosition.X,bowlPosition.Y,bowlPosition.Z,650);GameFiber.Wait(650);for(int i=0;i<5;i++){PlayDogAnimation("creatures@rottweiler@indication@","indicate_low",650,0);GameFiber.Wait(300);}Sit();}finally{if(bowl!=null&&bowl.Exists())bowl.Delete();}
        }

        private void ToggleLeash()
        {
            if (_state == K9State.Leashed || _leashRope >= 0) { DeleteLeashRope(); Follow(); ActionNotification("~b~K9 leash removed."); return; }
            _dog.Tasks.Clear();
            CreateLeashRope();
            _state = K9State.Leashed;
            NativeFunction.Natives.TASK_FOLLOW_TO_OFFSET_OF_ENTITY(_dog,Game.LocalPlayer.Character,-.55f,-.85f,0f,1.8f,-1,1.15f,true);
            ActionNotification("~b~K9 leash attached.~s~ The K9 will walk at the handler's left side.");
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
            if(_seatCalibrationDoorOpen&&(!_menu.Visible||_menuMode!="seat_config"||_state!=K9State.InVehicle))CloseSeatCalibrationDoor();
            EnforceHandlerSafety();
            UpdateNeeds();
            UpdateReliefNeeds();
            UpdateEnvironment();
            if(Game.GameTime>=_nextVitalsUpdate){_nextVitalsUpdate=Game.GameTime+5000;int liveHealth=(int)(100f*_dog.Health/Math.Max(1,_dog.MaxHealth));if(liveHealth<_profile.Health){string injury=liveHealth<=25?"Serious — veterinary treatment required":liveHealth<=55?"Moderate":"Minor";_profile.SetInjury(injury,liveHealth);K9IncidentLog.Write(_profile.Name,"Injury",injury,_dog.Position);}NativeFunction.Natives.SET_PED_MOVE_RATE_OVERRIDE(_dog,_profile.Health<=55?.65f:1f);if(_state==K9State.Searching||_state==K9State.Tracking||_state==K9State.Apprehending)_profile.UseStamina(2);else _profile.Recover(1);}
            if (_state == K9State.Leashed)
            {
                var officer = Game.LocalPlayer.Character;
                if(Game.GameTime>=_nextLeashFollow){_nextLeashFollow=Game.GameTime+900;if(_dog.DistanceTo(officer)>1.05f)NativeFunction.Natives.TASK_FOLLOW_TO_OFFSET_OF_ENTITY(_dog,officer,-.55f,-.85f,0f,1.9f,-1,1.15f,true);}
                if(_dog.DistanceTo(officer)>3.2f)_dog.Tasks.FollowNavigationMeshToPosition(officer.GetOffsetPosition(new Vector3(-.55f,-.85f,0f)),officer.Heading,2.4f);
            }
            if (_dog.DistanceTo(Game.LocalPlayer.Character) > 150f && _state == K9State.Following)
                _dog.Position = Game.LocalPlayer.Character.GetOffsetPosition(new Vector3(-1f, -2f, 0f));
        }
        private void UpdateNeeds(){if(Game.GameTime<_nextNeedsUpdate)return;_nextNeedsUpdate=Game.GameTime+180000;bool working=_state==K9State.Searching||_state==K9State.Tracking||_state==K9State.Apprehending||_state==K9State.Fetching;_profile.UseNeeds(working?1:0,working?2:1);if((_profile.Food<=25||_profile.Water<=25)&&Game.GameTime>=_nextNeedsWarning){_nextNeedsWarning=Game.GameTime+180000;Game.DisplayNotification("~o~K9 care needed:~s~~n~Food "+_profile.Food+"%  Water "+_profile.Water+"%~n~Use Feed or Give Water from the command menu.");}}
        private void UpdateReliefNeeds(){if(Game.GameTime<_nextReliefUpdate)return;_nextReliefUpdate=Game.GameTime+180000;_bladder=Math.Max(0,_bladder-_random.Next(6,11));_bowel=Math.Max(0,_bowel-_random.Next(3,8));if((_bladder<=30||_bowel<=30)&&_state!=K9State.InVehicle&&_state!=K9State.Searching&&_state!=K9State.Tracking&&_state!=K9State.Apprehending&&_random.Next(100)<45)Bathroom();}

        private void UpdateEnvironment(){if(Game.GameTime<_nextEnvironmentUpdate)return;_nextEnvironmentUpdate=Game.GameTime+60000;float rain=NativeFunction.Natives.GET_RAIN_LEVEL<float>();int hour=World.DateTime.Hour;bool hot=rain<.1f&&hour>=11&&hour<=18;if(hot)_profile.UseNeeds(0,2);if(hot&&_state==K9State.InVehicle&&_dogVehicle!=null&&_dogVehicle.Exists()&&!NativeFunction.Natives.GET_IS_VEHICLE_ENGINE_RUNNING<bool>(_dogVehicle)){_profile.UseNeeds(0,5);if(Game.GameTime>=_nextHeatWarning){_nextHeatWarning=Game.GameTime+90000;Game.DisplayNotification("~r~K9 VEHICLE HEAT WARNING~s~~n~Engine is off during peak heat. Remove the K9 or provide water.");K9IncidentLog.Write(_profile.Name,"Safety","Vehicle heat warning",_dogVehicle.Position);}}}

        private void DrawHud()
        {
            int mode=_profile.HudMode;
            if(mode!=0)
            {
                float[] designWidths={.30f,.24f,.32f,.29f,.26f};float x=_profile.HudX,y=_profile.HudY,s=_profile.HudScale,w=designWidths[_profile.HudDesign]*s;
                var lines=new List<string>();
                if(_profile.HudShowCertifications&&mode==2)lines.Add("CERTIFIED  "+Certifications());
                string details="";
                if(_profile.HudShowTrust)details+="TRUST "+_profile.Trust+"%  ";
                if(_profile.HudShowTraining)details+="LEVEL "+_profile.TrainingLevel+"/5  ";
                if(_profile.HudShowInjury)details+="INJURY "+_profile.Injury;
                if(mode==2&&!string.IsNullOrWhiteSpace(details))lines.Add(details.Trim());
                if(mode==2&&_profile.HudShowVoice)lines.Add("VOICE  "+_voiceStatus);
                int barRows=(_profile.HudShowHealth?1:0)+(_profile.HudShowStamina?1:0)+(_profile.HudShowFood?1:0)+(_profile.HudShowWater?1:0);
                float h=(.055f+barRows*.032f+lines.Count*.027f)*s,left=x-w/2+.012f*s,top=y-h/2;
                int[][] themes={new[]{7,13,20,8,76,116,26,181,232},new[]{25,21,12,128,86,16,236,181,55},new[]{8,20,14,22,91,50,61,206,111},new[]{25,8,10,132,24,31,238,67,79},new[]{18,18,18,58,58,58,210,210,210}};
                int[] c=themes[_profile.HudColorTheme];int alpha=_profile.HudDesign==4?155:225;
                NativeFunction.Natives.DRAW_RECT(x,y,w,h,c[0],c[1],c[2],alpha);
                NativeFunction.Natives.DRAW_RECT(x,top+.019f*s,w,.038f*s,c[3],c[4],c[5],245);
                if(_profile.HudDesign!=1)NativeFunction.Natives.DRAW_RECT(x-w/2+.003f*s,y,.006f*s,h,c[6],c[7],c[8],255);
                string[] icons={"K9  ","UNIT  ","PAW  ","•  ",""};string icon=icons[_profile.HudIconSet];
                float[] textScales={.30f,.27f,.33f,.29f,.24f};
                DrawText(icon+_profile.Name.ToUpper()+(_profile.HudShowState?"     "+_state.ToString().ToUpper():""),left,top+.006f*s,textScales[_profile.HudTextStyle]*s);
                float row=top+.052f*s;
                if(_profile.HudShowHealth){DrawStatusBar("HEALTH",_profile.Health,left,row,w-.024f*s,34,197,94);row+=.032f*s;}
                if(_profile.HudShowStamina){DrawStatusBar("STAMINA",_profile.Stamina,left,row,w-.024f*s,241,196,15);row+=.032f*s;}
                if(_profile.HudShowFood){DrawStatusBar("FOOD",_profile.Food,left,row,w-.024f*s,230,126,34);row+=.032f*s;}
                if(_profile.HudShowWater){DrawStatusBar("WATER",_profile.Water,left,row,w-.024f*s,37,162,232);row+=.032f*s;}
                foreach(string line in lines){DrawText(line,left,row-.006f*s,.235f*s);row+=.027f*s;}
            }
            if(_camera.Active&&DogExists()){float d=_dog.DistanceTo(Game.LocalPlayer.Character);NativeFunction.Natives.DRAW_RECT(.5f,.91f,.52f,.09f,0,0,0,180);DrawText("K9 CAM  GPS "+_dog.Position.X.ToString("0")+","+_dog.Position.Y.ToString("0")+"  HDG "+HeadingCardinal(_dog.Heading)+"  HANDLER "+d.ToString("0.0")+"m",.25f,.875f,.31f);DrawText("STATE "+_state+"  HP "+_profile.Health+"  STA "+_profile.Stamina+"  H2O "+_profile.Water,.25f,.91f,.27f);}
        }
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

        private void ActionNotification(string text){if(_config.ShowActionNotifications)Game.DisplayNotification(text);}
        private void Acknowledge(string text) => ActionNotification("~b~K9 " + _profile.Name + ":~s~ " + text);
        private string TargetLabel(Entity e) => e is Vehicle ? "vehicle" : "person";
        private bool DogExists() => _dog != null && _dog.Exists() && !_dog.IsDead;

        private void Dismiss(bool notify = true)
        {
            _camera.Disable();
            DeleteLeashRope();
            ReleaseVehicleSeat();
            if (_blip != null && _blip.Exists()) _blip.Delete();
            Ped dog=_dog;_dog=null;
            if(dog!=null&&dog.Exists())
            {
                try{dog.Tasks.ClearImmediately();}catch{}
                try{NativeFunction.Natives.DETACH_ENTITY(dog,true,true);NativeFunction.Natives.SET_ENTITY_COLLISION(dog,false,false);NativeFunction.Natives.SET_BLOCKING_OF_NON_TEMPORARY_EVENTS(dog,true);NativeFunction.Natives.SET_PED_KEEP_TASK(dog,false);NativeFunction.Natives.SET_ENTITY_AS_MISSION_ENTITY(dog,true,true);}catch{}
                try{dog.Delete();}catch(Exception ex){Game.LogTrivial("AdvancedK9 hard-dismiss delete failed: "+ex.Message);}
            }
            _state = K9State.Dismissed;
            if (notify) ActionNotification("~b~"+_profile.Name+" returned to the station kennel.");
        }

        public void Dispose()
        {
            _running = false;
            _voice?.Dispose();
            _voice = null;
            _trust.Save();
            _profile.Save();
            Dismiss(false);
            DeleteStationKennels();
        }
    }
}
