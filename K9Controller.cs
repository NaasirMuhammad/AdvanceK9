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
        private readonly GlassTacticalHud _hud = new GlassTacticalHud();
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
        private uint _nextHudUpdate;
        private uint _nextKennelPrompt;
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
        private string _hudCommand="READY";
        private string _hudSearchLabel="";
        private int _hudSearchProgress;
        private string _hudAlert="";
        private uint _hudAlertUntil;
        private bool _hudPreviewSearch;
        private bool _hudPreviewAlert;
        private bool _hudDragMode;
        private bool _kennelDragMode;
        private StationKennel _activeKennel;
        private Vector3 _kennelEditOriginalPosition;
        private float _kennelEditOriginalHeading;
        private Point _lastEditorMouse;
        private bool _editorMouseHeld;
        private uint _nextLiveEditorInput;
        private bool _searchInProgress;
        private bool _deployed;
        private bool _downed;
        private Vector3 _lastDogPosition;
        private float _lastDogHeading;

        private sealed class ScentTrailPoint
        {
            public Vector3 Position;
            public uint Time;
            public ScentTrailPoint(Vector3 position,uint time){Position=position;Time=time;}
        }

        private sealed class StationKennel
        {
            public string Key;public string Name;public Vector3 Position;public float Heading;public Vector3 DefaultPosition;public float DefaultHeading;public bool SnapToGround;public bool PreciseGrounding;public float SurfaceLift;public bool ForceLevel;public Rage.Object Prop;public Blip Blip;
            public StationKennel(string key,string name,Vector3 position,float heading,bool snapToGround=true,bool preciseGrounding=false,float surfaceLift=0f,bool forceLevel=false){Key=key;Name=name;Position=position;Heading=heading;DefaultPosition=position;DefaultHeading=heading;SnapToGround=snapToGround;PreciseGrounding=preciseGrounding;SurfaceLift=surfaceLift;ForceLevel=forceLevel;}
        }

        public K9Controller(ModConfig config)
        {
            _config = config;
            _pr = new PolicingRedefinedBridge(config.CompatibilityMode,config.CompatibilityShareResults,config.CompatibilityUseCdfInventory,config.CompatibilityShareWithNexusMdt);
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
                HandleLiveMenuEditors();
                DrawHud();
                MaintainStationKennels();
                CaptureScentTrails();
                UpdateCompatibilityPursuit();
                if (ChordPressed(_config.ModifierKey, _config.SpawnKey)) Execute(K9Command.SpawnDismiss);
                if (ChordPressed(_config.ModifierKey, _config.KennelKey)) ShowKennelMenu();
                if (Game.IsKeyDownRightNow(_config.ModifierKey) && Game.IsKeyDown(_config.CommandKey)) ShowCommandMenu();
                HandlePushToTalk();
                MaintainK9Availability();
                if (!DogEntityExists()) { DrainVoice(false); continue; }
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
            _hud.Update(new GlassTacticalHud.Snapshot{Visible=false});
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

        private void RefreshProfileMenu(){_menu.Update("K9 PROFILE — "+_profile.Name,new[]{"Identity & Appearance","HUD & Display","Kennel Location Editor","Vehicle Seat Configuration","Profile, Health & Certifications",VoiceMenuLabel()});}
        private void OpenAppearanceMenu(){_menuMode="profile_appearance";_menu.Open("K9 PROFILE — APPEARANCE",new[]{"Edit name: "+_profile.Name,"Breed/model: "+_profile.Breed,"Skin/coat: "+(_profile.CoatVariation+1),"Equipment/vest: "+_profile.Vest,"Vest texture: "+_profile.VestTextureName(_dog),"← Back to K9 Profile"});}
        private void OnMenuSelected(int index)
        {
            if(_menuMode=="commands_root"){if(index>=0&&index<7){OpenCommandGroup(index);return;}if(index==7){_menu.Close();Execute(K9Command.SpawnDismiss);return;}if(index==8)ToggleVoice();return;}
            if(_menuMode!=null&&_menuMode.StartsWith("commands_group_")){int group;if(!int.TryParse(_menuMode.Substring(15),out group)||group<0||group>=CommandGroups.Length)return;if(index>=0&&index<CommandGroups[group].Length){_menu.Close();Execute(CommandGroups[group][index]);}else ShowCommandMenu();return;}
            if(_menuMode=="hud_config"){HandleHudMenu(index);return;}
            if(_menuMode=="kennel_list"){HandleKennelList(index);return;}
            if(_menuMode=="kennel_edit"){HandleKennelEditMenu(index);return;}
            if(_menuMode=="seat_config"){HandleSeatMenu(index);return;}
            if(_menuMode=="profile_appearance")
            {
                if(index==0){string n=PromptForDogName(24);if(!string.IsNullOrWhiteSpace(n)){_profile.SetName(n);_voice?.UpdateWakeWord(_profile.Name);}}
                else if(index==1)PreviewBreed(1);else if(index==2)_profile.NextSkin(_dog);else if(index==3)_profile.NextEquipment(_dog);else if(index==4)_profile.NextEquipmentTexture(_dog);else if(index==5){_menuMode="profile";RefreshProfileMenu();return;}
                OpenAppearanceMenu();return;
            }
            if(_menuMode!="profile")return;
            if(index==0)OpenAppearanceMenu();else if(index==1)OpenHudConfiguration();else if(index==2)OpenKennelLocationMenu();else if(index==3)OpenSeatConfiguration();else if(index==4)Inspect();else if(index==5)ToggleVoice();
        }

        private void InitializeVoice(){_voice=new VoiceCommandService(_config.VoiceProvider,_config.VoiceModel,_config.VoiceLanguage,_config.VoiceApiKey,_config.VoiceApiKeyEnvironmentVariable,_profile.Name,_config.ShowVoiceStatusText);_voice.CommandRecognized+=c=>_voiceQueue.Enqueue(c);_voice.StatusChanged+=s=>_voiceStatus=s;_voiceActive=_config.VoiceEnabled&&_voice.IsAvailable;_voiceStatus=_voice.IsAvailable?"Ready (hold V)":"Key missing";}
        private string VoiceMenuLabel()=>"Voice microphone: "+(_voice==null||!_voice.IsAvailable?"UNAVAILABLE — add ApiKey in INI":_voiceActive?"ON — hold "+_config.PushToTalkKey:"OFF — select to activate");
        private void ToggleVoice(){if(_voice==null)InitializeVoice();if(!_voice.IsAvailable){Game.DisplayNotification("~r~Voice cannot activate.~s~~n~Add your provider key after ~y~ApiKey=~s~ in AdvancedK9.ini, then reload the plugin.");return;}_voiceActive=!_voiceActive;if(_voiceActive){_voiceStatus="Ready (hold V)";ActionNotification("~g~K9 push-to-talk activated.~s~ Hold "+_config.PushToTalkKey+" while speaking.");}else{_voice.StopListening();_voiceStatus="Off";ActionNotification("~y~K9 voice microphone disabled.");}}

        private void OnMenuAdjusted(int index,int delta){if(_menuMode=="hud_config"){AdjustHudMenu(index,delta);return;}if(_menuMode=="kennel_edit"){AdjustKennel(index,delta);return;}if(_menuMode=="seat_config"){AdjustSeat(index,delta);return;}if(_menuMode!="profile_appearance")return;if(index==1)PreviewBreed(delta);else if(index==2)_profile.AdjustSkin(_dog,delta);else if(index==3)_profile.AdjustEquipment(_dog,delta);else if(index==4)_profile.AdjustEquipmentTexture(_dog,delta);else return;OpenAppearanceMenu();}

        private static string OnOff(bool value)=>value?"ON":"OFF";
        private void OpenHudConfiguration(){_menuMode="hud_config";RefreshHudMenu();}
        private void RefreshHudMenu(){_menu.Update("K9 HUD — GLASS TACTICAL",new[]{"HUD: "+(_profile.HudMode==0?"OFF":_profile.HudMode==1?"COMPACT":"FULL"),"Portrait: "+OnOff(_profile.HudShowPortrait),"State: "+OnOff(_profile.HudShowState),"Health: "+OnOff(_profile.HudShowHealth),"Stamina: "+OnOff(_profile.HudShowStamina),"Distance: "+OnOff(_profile.HudShowDistance),"Current command: "+OnOff(_profile.HudShowCommand),"Behavior: "+OnOff(_profile.HudShowBehavior),"Automatic collapse: "+OnOff(_profile.HudAutoCollapse),"Search progress: "+OnOff(_profile.HudSearchProgress),"Distance units: "+(_profile.HudMetricDistance?"METRIC":"IMPERIAL"),"Food: "+OnOff(_profile.HudShowFood),"Water: "+OnOff(_profile.HudShowWater),"Certifications: "+OnOff(_profile.HudShowCertifications),"Trust: "+OnOff(_profile.HudShowTrust),"Training: "+OnOff(_profile.HudShowTraining),"Injury: "+OnOff(_profile.HudShowInjury),"Voice: "+OnOff(_profile.HudShowVoice),"Move left","Move right","Move up","Move down","Scale: "+_profile.HudScale.ToString("0.00"),"Opacity: "+_profile.HudOpacity.ToString("0.00"),"Preview search: "+OnOff(_hudPreviewSearch),"Preview alert: "+OnOff(_hudPreviewAlert),"Portrait frame: "+(_profile.HudPortraitShape==0?"CIRCLE":"SQUARE"),"Live HUD drag: "+OnOff(_hudDragMode),"Reset HUD position and size","← Back to K9 Profile"});}
        private void HandleHudMenu(int index){if(index==0)_profile.CycleHudMode();else if(index==1)_profile.ToggleHudOption(1);else if(index>=2&&index<=4)_profile.ToggleHudField(index-2);else if(index>=5&&index<=7)_profile.ToggleHudOption(index-3);else if(index==8)_profile.ToggleHudOption(0);else if(index==9)_profile.ToggleHudOption(5);else if(index==10)_profile.ToggleHudOption(6);else if(index>=11&&index<=17)_profile.ToggleHudField(index-8);else if(index==18)_profile.MoveHud(-.01f,0);else if(index==19)_profile.MoveHud(.01f,0);else if(index==20)_profile.MoveHud(0,-.01f);else if(index==21)_profile.MoveHud(0,.01f);else if(index==24)_hudPreviewSearch=!_hudPreviewSearch;else if(index==25)_hudPreviewAlert=!_hudPreviewAlert;else if(index==26)_profile.TogglePortraitShape();else if(index==27){_hudDragMode=!_hudDragMode;_editorMouseHeld=false;Game.DisplaySubtitle("~b~HUD drag~s~: hold left mouse and move, or use W/A/S/D. Q/E resizes.",2500);}else if(index==28)_profile.ResetHud();else if(index==29){_hudDragMode=false;_profile.SaveHudLayout();_hudPreviewSearch=false;_hudPreviewAlert=false;_menuMode="profile";RefreshProfileMenu();return;}RefreshHudMenu();}
        private void AdjustHudMenu(int index,int delta){if(index==18||index==19)_profile.MoveHud(delta*.005f,0);else if(index==20||index==21)_profile.MoveHud(0,delta*.005f);else if(index==22)_profile.AdjustHudScale(delta*.05f);else if(index==23)_profile.AdjustHudOpacity(delta*.05f);else return;RefreshHudMenu();}

        private void OpenKennelLocationMenu()
        {
            _hudDragMode=false;_kennelDragMode=false;_activeKennel=null;_menuMode="kennel_list";
            _menu.Open("K9 KENNEL LOCATIONS",_stationKennels.Select(k=>k.Name).Concat(new[]{"← Back to K9 Profile"}));
        }

        private void HandleKennelList(int index)
        {
            if(index<0)return;
            if(index>=_stationKennels.Count){_menuMode="profile";RefreshProfileMenu();return;}
            _activeKennel=_stationKennels[index];_kennelEditOriginalPosition=_activeKennel.Position;_kennelEditOriginalHeading=_activeKennel.Heading;
            if(_activeKennel.Prop==null||!_activeKennel.Prop.Exists())SpawnNearbyKennelProp(_activeKennel);
            _menuMode="kennel_edit";RefreshKennelEditMenu();
        }

        private void RefreshKennelEditMenu()
        {
            if(_activeKennel==null){OpenKennelLocationMenu();return;}
            _menu.Update("KENNEL — "+_activeKennel.Name,new[]{
                "X east/west: "+_activeKennel.Position.X.ToString("0.000"),
                "Y north/south: "+_activeKennel.Position.Y.ToString("0.000"),
                "Z up/down: "+_activeKennel.Position.Z.ToString("0.000"),
                "Heading: "+_activeKennel.Heading.ToString("0.0")+"°",
                "Live kennel drag: "+OnOff(_kennelDragMode),
                "Place two metres in front of player",
                "Snap down to loaded ground",
                "Save location to AdvancedK9.ini",
                "Revert unsaved changes",
                "Reset to built-in default",
                "← Back to Kennel List"});
        }

        private void AdjustKennel(int index,int delta)
        {
            if(_activeKennel==null||delta==0)return;
            Vector3 p=_activeKennel.Position;
            if(index==0)p.X+=delta*.10f;else if(index==1)p.Y+=delta*.10f;else if(index==2)p.Z+=delta*.05f;else if(index==3)_activeKennel.Heading=NormalizeHeading(_activeKennel.Heading+delta*5f);else return;
            _activeKennel.Position=p;ApplyKennelPreview();RefreshKennelEditMenu();
        }

        private void HandleKennelEditMenu(int index)
        {
            if(_activeKennel==null){OpenKennelLocationMenu();return;}
            if(index==4){_kennelDragMode=!_kennelDragMode;_editorMouseHeld=false;Game.DisplaySubtitle("~b~Kennel drag~s~: hold left mouse and move, or use W/A/S/D. R/F changes height; Q/E rotates.",3200);}
            else if(index==5){Ped player=Game.LocalPlayer.Character;_activeKennel.Position=player.Position+HeadingOffset(player.Heading,2f);_activeKennel.Heading=NormalizeHeading(player.Heading+90f);EnsureKennelPreview();ApplyKennelPreview();}
            else if(index==6){SnapActiveKennelToGround();}
            else if(index==7){try{_config.SaveKennelLocation(_activeKennel.Key,_activeKennel.Position,_activeKennel.Heading);_kennelEditOriginalPosition=_activeKennel.Position;_kennelEditOriginalHeading=_activeKennel.Heading;Game.DisplayNotification("~g~K9 kennel location saved.~s~~n~"+_activeKennel.Name);}catch{Game.DisplayNotification("~r~Unable to save kennel location.~s~ See RagePluginHook.log.");}}
            else if(index==8){_activeKennel.Position=_kennelEditOriginalPosition;_activeKennel.Heading=_kennelEditOriginalHeading;ApplyKennelPreview();}
            else if(index==9){_activeKennel.Position=_activeKennel.DefaultPosition;_activeKennel.Heading=_activeKennel.DefaultHeading;EnsureKennelPreview();SnapActiveKennelToGround();}
            else if(index==10){_activeKennel.Position=_kennelEditOriginalPosition;_activeKennel.Heading=_kennelEditOriginalHeading;ApplyKennelPreview();_activeKennel=null;_kennelDragMode=false;OpenKennelLocationMenu();return;}
            RefreshKennelEditMenu();
        }

        private void EnsureKennelPreview()
        {
            if(_activeKennel==null)return;
            if(_activeKennel.Prop!=null&&_activeKennel.Prop.Exists())return;
            SpawnNearbyKennelProp(_activeKennel);
        }

        private void ApplyKennelPreview()
        {
            if(_activeKennel==null)return;EnsureKennelPreview();
            try
            {
                if(_activeKennel.Prop!=null&&_activeKennel.Prop.Exists())
                {
                    NativeFunction.Natives.FREEZE_ENTITY_POSITION(_activeKennel.Prop,false);_activeKennel.Prop.Position=_activeKennel.Position;_activeKennel.Prop.Heading=_activeKennel.Heading;
                    NativeFunction.Natives.SET_ENTITY_ROTATION(_activeKennel.Prop,0f,0f,_activeKennel.Heading,2,true);NativeFunction.Natives.FREEZE_ENTITY_POSITION(_activeKennel.Prop,true);
                }
                if(_activeKennel.Blip!=null&&_activeKennel.Blip.Exists())_activeKennel.Blip.Position=_activeKennel.Position;
            }
            catch(Exception ex){Game.LogTrivial("AdvancedK9 live kennel preview recovered: "+ex.Message);}
        }

        private void SnapActiveKennelToGround()
        {
            if(_activeKennel==null)return;EnsureKennelPreview();
            try
            {
                NativeFunction.Natives.REQUEST_COLLISION_AT_COORD(_activeKennel.Position.X,_activeKennel.Position.Y,_activeKennel.Position.Z);
                if(_activeKennel.Prop!=null&&_activeKennel.Prop.Exists())
                {
                    NativeFunction.Natives.FREEZE_ENTITY_POSITION(_activeKennel.Prop,false);NativeFunction.Natives.PLACE_OBJECT_ON_GROUND_PROPERLY(_activeKennel.Prop);_activeKennel.Position=_activeKennel.Prop.Position;
                    NativeFunction.Natives.SET_ENTITY_ROTATION(_activeKennel.Prop,0f,0f,_activeKennel.Heading,2,true);NativeFunction.Natives.FREEZE_ENTITY_POSITION(_activeKennel.Prop,true);
                }
                ApplyKennelPreview();
            }
            catch(Exception ex){Game.LogTrivial("AdvancedK9 kennel ground snap failed: "+ex.Message);}
        }

        private void HandleLiveMenuEditors()
        {
            if(!_menu.Visible)
            {
                if(_hudDragMode)_profile.SaveHudLayout();
                if(_kennelDragMode&&_activeKennel!=null){_activeKennel.Position=_kennelEditOriginalPosition;_activeKennel.Heading=_kennelEditOriginalHeading;ApplyKennelPreview();}
                _hudDragMode=false;_kennelDragMode=false;_editorMouseHeld=false;return;
            }
            if(!_hudDragMode&&!_kennelDragMode){_editorMouseHeld=false;return;}
            if(Game.GameTime<_nextLiveEditorInput)return;_nextLiveEditorInput=Game.GameTime+30;
            Point current=Cursor.Position;bool mouse=Game.IsKeyDownRightNow(Keys.LButton);int dx=0,dy=0;
            if(mouse&&_editorMouseHeld){dx=current.X-_lastEditorMouse.X;dy=current.Y-_lastEditorMouse.Y;}_lastEditorMouse=current;_editorMouseHeld=mouse;
            if(_hudDragMode&&_menuMode=="hud_config")
            {
                float x=dx/(float)Math.Max(1,Game.Resolution.Width),y=dy/(float)Math.Max(1,Game.Resolution.Height);
                if(Game.IsKeyDownRightNow(Keys.A))x-=.004f;if(Game.IsKeyDownRightNow(Keys.D))x+=.004f;if(Game.IsKeyDownRightNow(Keys.W))y-=.004f;if(Game.IsKeyDownRightNow(Keys.S))y+=.004f;
                if(x!=0f||y!=0f)_profile.MoveHudPreview(x,y);if(Game.IsKeyDownRightNow(Keys.Q))_profile.AdjustHudScalePreview(-.01f);if(Game.IsKeyDownRightNow(Keys.E))_profile.AdjustHudScalePreview(.01f);
            }
            if(_kennelDragMode&&_menuMode=="kennel_edit"&&_activeKennel!=null)
            {
                float right=dx*.006f,forward=-dy*.006f;if(Game.IsKeyDownRightNow(Keys.A))right-=.06f;if(Game.IsKeyDownRightNow(Keys.D))right+=.06f;if(Game.IsKeyDownRightNow(Keys.W))forward+=.06f;if(Game.IsKeyDownRightNow(Keys.S))forward-=.06f;
                Vector3 p=_activeKennel.Position;if(right!=0f)p+=HeadingOffset(Game.LocalPlayer.Character.Heading+90f,right);if(forward!=0f)p+=HeadingOffset(Game.LocalPlayer.Character.Heading,forward);if(Game.IsKeyDownRightNow(Keys.R))p.Z+=.03f;if(Game.IsKeyDownRightNow(Keys.F))p.Z-=.03f;
                if(Game.IsKeyDownRightNow(Keys.Q))_activeKennel.Heading=NormalizeHeading(_activeKennel.Heading-1f);if(Game.IsKeyDownRightNow(Keys.E))_activeKennel.Heading=NormalizeHeading(_activeKennel.Heading+1f);_activeKennel.Position=p;ApplyKennelPreview();
            }
        }

        private static float NormalizeHeading(float heading){heading%=360f;if(heading<0f)heading+=360f;return heading;}

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
                _hudCommand=CommandLabel(command);
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
                    case K9Command.SearchArea: BeginSearch(false,DetectionSpecialty.General); break;
                    case K9Command.SearchBuilding: BeginBuildingSearch(); break;
                    case K9Command.SearchVehicle: BeginSearch(true,DetectionSpecialty.General); break;
                    case K9Command.SearchNarcotics: BeginSearch(false,DetectionSpecialty.Narcotics); break;
                    case K9Command.SearchExplosives: BeginSearch(false,DetectionSpecialty.Explosives); break;
                    case K9Command.SearchWeapons: BeginSearch(false,DetectionSpecialty.Weapons); break;
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
            return command==K9Command.Follow||command==K9Command.Heel||command==K9Command.Sit||command==K9Command.LieDown||command==K9Command.Stay||command==K9Command.Recall||command==K9Command.SearchArea||command==K9Command.SearchBuilding||command==K9Command.SearchVehicle||command==K9Command.SearchNarcotics||command==K9Command.SearchExplosives||command==K9Command.SearchWeapons||command==K9Command.Track||command==K9Command.FindTrail||command==K9Command.Fetch||command==K9Command.Apprehend;
        }

        private bool TrustAllowsCommand(K9Command command)
        {
            if (command == K9Command.Apprehend && _trust.Level < 25)
            {
                Game.DisplayNotification("~y~K9 trust is too low for safe apprehension training.~s~~n~Pet, feed and train together first.");
                return false;
            }
            bool trained=_profile.IsTrainedFor(command);bool fit=_profile.Health>=70&&_profile.Stamina>=35&&_profile.Food>=20&&_profile.Water>=20;
            if(trained&&fit&&_trust.Level>=90&&_profile.Confidence>=85)return true;
            int delay=trained?Math.Max(80,_trust.ResponseDelay/2):_trust.ResponseDelay+250;GameFiber.Wait(delay);
            double bond=(_trust.Level/100.0*.55)+(_profile.Confidence/100.0*.35)+(trained?.10:0);
            double condition=Math.Max(.35,Math.Min(1.0,(_profile.Health/100.0)*(.65+.35*_profile.Stamina/100.0)*_profile.NeedsFactor));
            double chance=Math.Max(.25,Math.Min(trained?.99:.88,bond*condition));if(_random.NextDouble()<=chance)return true;
            Game.DisplayNotification("~o~"+_profile.Name+" hesitated.~s~ Bond "+_trust.Level+"/100 • Confidence "+_profile.Confidence+"/100"+(trained?"":"~n~This command is still being learned in training."));
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
                ConfiguredKennel("MissionRow","Downtown / Mission Row Police Station",new Vector3(436.4405f,-974.9838f,29.78568f),359.8824f),
                ConfiguredKennel("Davis","Davis Police Station",new Vector3(354.2758f,-1591.15f,28.31161f),320.5168f),
                ConfiguredKennel("Vespucci","Vespucci Police Station",new Vector3(-1082.498f,-803.2653f,18.24856f),280.9947f),
                ConfiguredKennel("RockfordHills","Rockford Hills Police Station",new Vector3(-555.8f,-132.2f,38.2f),115f,true),
                ConfiguredKennel("Vinewood","Vinewood Police Station",new Vector3(636.7312f,-2.824063f,81.80692f),161.6745f),
                ConfiguredKennel("LaMesa","La Mesa Police Station",new Vector3(840.4388f,-1276.518f,25.46601f),267.7128f),
                ConfiguredKennel("SandyShores","Sandy Shores Sheriff Station",new Vector3(1873.2f,3692.601f,32.66043f),115f),
                ConfiguredKennel("Paleto","Paleto Police Station",new Vector3(-445.2472f,6022.968f,30.55979f),224.0662f),
                ConfiguredKennel("Ranger","Ranger Police Station",new Vector3(370.1926f,793.9409f,186.6179f),96.3364f),
                ConfiguredKennel("LSIA","LSIA Field Office",new Vector3(-870.6f,-2417.4f,14.6f),150f,true),
                ConfiguredKennel("Bolingbroke","Bolingbroke Penitentiary",new Vector3(1848.9f,2604.6f,45.6f),180f,true),
                ConfiguredKennel("DelPerro","Del Perro Police Station",new Vector3(-1621.023f,-1013.941f,12.17308f),320.2082f),
                ConfiguredKennel("PortOfLosSantos","Port Of Los Santos Police Station",new Vector3(-343.8605f,-2788.374f,4.0199f),265.8435f),
                ConfiguredKennel("GreatOceanHighway","Great Ocean Highway Police Station",new Vector3(-1490.288f,4975.141f,62.78698f),354.9519f),
                ConfiguredKennel("FortZancudo","Fort Zancudo Police Station",new Vector3(-2363.756f,3274.542f,32.01595f),60.3562f),
                ConfiguredKennel("FIB","FIB Police Station",new Vector3(110.5135f,-759.2312f,44.77443f),246.6973f),
                ConfiguredKennel("BrookTrail","Brook Trail Police Station",new Vector3(1744.612f,3035.371f,60.83065f),335.9466f)
            });
            foreach(StationKennel kennel in _stationKennels)if(kennel.Blip==null||!kennel.Blip.Exists())
            {
                kennel.Blip=new Blip(kennel.Position);kennel.Blip.Name="K9 Kennel — "+kennel.Name;kennel.Blip.Color=Color.DodgerBlue;
                try{NativeFunction.Natives.SET_BLIP_SPRITE(kennel.Blip,273);NativeFunction.Natives.SET_BLIP_SCALE(kennel.Blip,.75f);NativeFunction.Natives.SET_BLIP_AS_SHORT_RANGE(kennel.Blip,true);}catch{}
            }
            UpdateNearbyKennelProps();
        }

        private StationKennel ConfiguredKennel(string key,string name,Vector3 defaultPosition,float defaultHeading,bool retainGrounding=false)
        {
            Vector3 position;float heading;bool overridden=_config.TryGetKennelLocation(key,out position,out heading);
            if(!overridden){position=defaultPosition;heading=defaultHeading;}
            // Measured placements retain their X/Y coordinates while nearby collision
            // allows the doghouse to settle onto the surface before it is forced level.
            StationKennel kennel=retainGrounding&&!overridden?new StationKennel(key,name,position,heading):new StationKennel(key,name,position,heading,false,false,0f,true);
            kennel.DefaultPosition=defaultPosition;kennel.DefaultHeading=defaultHeading;return kennel;
        }

        private void UpdateNearbyKennelProps()
        {
            Ped handler=Game.LocalPlayer.Character;if(handler==null||!handler.Exists())return;
            foreach(StationKennel kennel in _stationKennels)
            {
                float distance=kennel.Position.DistanceTo(handler.Position);
                if(distance>350f){try{if(kennel.Prop!=null&&kennel.Prop.Exists())kennel.Prop.Delete();}catch{}kennel.Prop=null;continue;}
                if(kennel.Prop!=null&&kennel.Prop.Exists())continue;
                SpawnNearbyKennelProp(kennel);
            }
        }

        private void SpawnNearbyKennelProp(StationKennel kennel)
        {
            var model=new Model("prop_doghouse_01");if(!model.IsValid)return;model.LoadAndWait();
            Vector3 configuredPosition=kennel.Position;
            try
            {
                int collisionAttempts=kennel.PreciseGrounding?12:1;
                for(int attempt=0;attempt<collisionAttempts;attempt++){NativeFunction.Natives.REQUEST_COLLISION_AT_COORD(configuredPosition.X,configuredPosition.Y,configuredPosition.Z);if(kennel.PreciseGrounding)GameFiber.Yield();}
                Vector3 spawnPosition=configuredPosition;
                if(kennel.PreciseGrounding)try
                {
                    float groundZ=0f;
                    bool foundGround=NativeFunction.Natives.GET_GROUND_Z_FOR_3D_COORD<bool>(configuredPosition.X,configuredPosition.Y,configuredPosition.Z+25f,out groundZ,false);
                    if(foundGround&&Math.Abs(groundZ-configuredPosition.Z)<=3f)spawnPosition=new Vector3(configuredPosition.X,configuredPosition.Y,groundZ+kennel.SurfaceLift);
                }
                catch{}
                kennel.Prop=new Rage.Object(model,spawnPosition);kennel.Prop.Heading=kennel.Heading;kennel.Prop.IsPersistent=true;
                if(kennel.SnapToGround)
                {
                    NativeFunction.Natives.PLACE_OBJECT_ON_GROUND_PROPERLY(kennel.Prop);
                    Vector3 snapped=kennel.Prop.Position;
                    if(Math.Abs(snapped.Z-configuredPosition.Z)<=3.0f){if(kennel.SurfaceLift>0f)snapped.Z+=kennel.SurfaceLift;kennel.Prop.Position=snapped;kennel.Position=snapped;}
                    else{kennel.Prop.Position=configuredPosition;Game.LogTrivial("AdvancedK9 kennel ground-snap rejected unsafe Z change for "+kennel.Name+".");}
                }
                if(kennel.ForceLevel)NativeFunction.Natives.SET_ENTITY_ROTATION(kennel.Prop,0f,0f,kennel.Heading,2,true);
                NativeFunction.Natives.FREEZE_ENTITY_POSITION(kennel.Prop,true);kennel.Position=kennel.Prop.Position;
                int interior=NativeFunction.Natives.GET_INTERIOR_FROM_ENTITY<int>(kennel.Prop);
                if(interior!=0)Game.LogTrivial("AdvancedK9 kennel exterior audit WARNING: "+kennel.Name+" resolved inside interior "+interior+" at "+kennel.Position+".");
                else Game.LogTrivial("AdvancedK9 kennel exterior audit passed: "+kennel.Name+" at "+kennel.Position+".");
                Game.LogTrivial("AdvancedK9 nearby kennel spawned: "+kennel.Name+" at "+kennel.Position+".");
            }
            catch(Exception ex){try{if(kennel.Prop!=null&&kennel.Prop.Exists())kennel.Prop.Delete();}catch{}kennel.Prop=null;Game.LogTrivial("AdvancedK9 nearby kennel spawn failed for "+kennel.Name+": "+ex.Message);}
            finally{model.Dismiss();}
        }

        private void MaintainStationKennels()
        {
            if(Game.GameTime<_nextKennelPrompt)return;_nextKennelPrompt=Game.GameTime+250;
            UpdateNearbyKennelProps();
            StationKennel kennel=NearestKennel(3.2f);if(kennel==null)return;
            Game.DisplayHelp((DogExists()?"Return "+_profile.Name+" to":"Pick up "+_profile.Name+" from")+" the K9 kennel: hold "+_config.ModifierKey+" and press "+_config.SpawnKey+".");
        }

        private StationKennel NearestKennel(float radius){var handler=Game.LocalPlayer.Character;if(handler==null||!handler.Exists())return null;return _stationKennels.Where(k=>k.Prop!=null&&k.Prop.Exists()&&k.Position.DistanceTo(handler.Position)<=radius).OrderBy(k=>k.Position.DistanceTo(handler.Position)).FirstOrDefault();}
        private void DeleteStationKennels(){foreach(StationKennel kennel in _stationKennels)try{if(kennel.Prop!=null&&kennel.Prop.Exists())kennel.Prop.Delete();if(kennel.Blip!=null&&kennel.Blip.Exists())kennel.Blip.Delete();}catch{}foreach(StationKennel kennel in _stationKennels){kennel.Prop=null;kennel.Blip=null;}}
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
            _deployed = true;
            _downed = false;
            _lastDogPosition = position;
            _lastDogHeading = heading;
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

        private void EnterVehicle()
        {
            if(!DogExists())return;var handler=Game.LocalPlayer.Character;Vehicle vehicle=handler.CurrentVehicle;
            if(vehicle==null||!vehicle.Exists())vehicle=World.GetAllVehicles().Where(v=>v.Exists()&&v.DistanceTo(handler)<8f).OrderBy(v=>v.DistanceTo(handler)).FirstOrDefault();
            if(vehicle==null){Game.DisplayNotification("~y~No vehicle nearby.");return;}
            int seat=-99;foreach(int candidate in new[]{2,1,0})if(NativeFunction.Natives.IS_VEHICLE_SEAT_FREE<bool>(vehicle,candidate,false)){seat=candidate;break;}
            if(seat==-99){Game.DisplayNotification("~y~No open rear/passenger seat for the K9.");return;}
            _dogVehicleDoor=seat==2?3:seat==1?2:1;NativeFunction.Natives.SET_VEHICLE_DOOR_OPEN(vehicle,_dogVehicleDoor,false,false);
            Vector3 doorPosition=vehicle.GetOffsetPosition(new Vector3(seat==1?-1.15f:1.15f,-1.25f,0f));
            _dog.Tasks.Clear();_dog.Tasks.FollowNavigationMeshToPosition(doorPosition,vehicle.Heading,1.6f).WaitForCompletion(4500);
            if(!DogExists()||!vehicle.Exists())return;
            NativeFunction.Natives.TASK_TURN_PED_TO_FACE_ENTITY(_dog,vehicle,500);GameFiber.Wait(500);
            Game.DisplaySubtitle("~b~K9 jumping into the calibrated seat...",700);
            bool visibleJump=PlayDogVehicleJump(vehicle,seat,doorPosition);
            if(!visibleJump)
            {
                Game.LogTrivial("AdvancedK9 vehicle load: animal jump unavailable for "+_profile.ModelName+"; using hidden safe-seat fallback.");
                _dog.Tasks.ClearImmediately();NativeFunction.Natives.SET_ENTITY_VISIBLE(_dog,false,false);
                NativeFunction.Natives.TASK_WARP_PED_INTO_VEHICLE(_dog,vehicle,seat);GameFiber.Wait(150);
            }
            _dogVehicle=vehicle;_activeSeatProfile=_seatProfiles.Get(vehicle);ApplySeatCalibration();
            NativeFunction.Natives.SET_ENTITY_VISIBLE(_dog,true,false);NativeFunction.Natives.RESET_ENTITY_ALPHA(_dog);
            NativeFunction.Natives.SET_VEHICLE_DOOR_SHUT(vehicle,_dogVehicleDoor,false);NativeFunction.Natives.SET_ENTITY_INVINCIBLE(_dog,true);
            _state=K9State.InVehicle;K9IncidentLog.Write(_profile.Name,"Kennel","Dog-safe direct load using saved "+_seatProfiles.VehicleName(vehicle)+" seat profile",vehicle.Position);Acknowledge("Sitting safely in the saved right-rear position.");
        }

        private bool PlayDogVehicleJump(Vehicle vehicle,int seat,Vector3 doorPosition)
        {
            if(!DogExists()||vehicle==null||!vehicle.Exists())return false;
            const string dictionary="creatures@rottweiler@move";const string animation="jump";
            try
            {
                NativeFunction.Natives.REQUEST_ANIM_DICT(dictionary);uint timeout=Game.GameTime+1200;
                while(!NativeFunction.Natives.HAS_ANIM_DICT_LOADED<bool>(dictionary)&&Game.GameTime<timeout)GameFiber.Yield();
                if(!NativeFunction.Natives.HAS_ANIM_DICT_LOADED<bool>(dictionary))return false;
                string boneName=seat==2?"seat_pside_r":seat==1?"seat_dside_r":"seat_pside_f";
                int bone=NativeFunction.Natives.GET_ENTITY_BONE_INDEX_BY_NAME<int>(vehicle,boneName);if(bone<0)return false;
                Vector3 seatPosition=NativeFunction.Natives.GET_WORLD_POSITION_OF_ENTITY_BONE<Vector3>(vehicle,bone);
                Vector3 start=doorPosition;Vector3 apex=new Vector3((start.X+seatPosition.X)*.5f,(start.Y+seatPosition.Y)*.5f,Math.Max(start.Z,seatPosition.Z)+.65f);
                _dog.Tasks.ClearImmediately();NativeFunction.Natives.SET_ENTITY_COLLISION(_dog,false,false);
                NativeFunction.Natives.TASK_PLAY_ANIM(_dog,dictionary,animation,5f,-3f,800,0,0f,false,false,false);
                const int frames=18;
                for(int i=1;i<=frames&&DogExists()&&vehicle.Exists();i++)
                {
                    float t=i/(float)frames;float one=1f-t;
                    float x=one*one*start.X+2f*one*t*apex.X+t*t*seatPosition.X;
                    float y=one*one*start.Y+2f*one*t*apex.Y+t*t*seatPosition.Y;
                    float z=one*one*start.Z+2f*one*t*apex.Z+t*t*(seatPosition.Z+.12f);
                    NativeFunction.Natives.SET_ENTITY_COORDS_NO_OFFSET(_dog,x,y,z,false,false,false);_dog.Heading=vehicle.Heading;GameFiber.Wait(32);
                }
                NativeFunction.Natives.REMOVE_ANIM_DICT(dictionary);
                Game.LogTrivial("AdvancedK9 vehicle load: visible animal jump completed for "+_profile.ModelName+".");
                return DogExists()&&vehicle.Exists();
            }
            catch(Exception ex){Game.LogTrivial("AdvancedK9 vehicle jump fallback: "+ex.Message);return false;}
        }
        private void ExitVehicle(){if(_dog==null||!_dog.Exists())return;var vehicle=_dogVehicle!=null&&_dogVehicle.Exists()?_dogVehicle:_dog.CurrentVehicle;if(vehicle==null||!vehicle.Exists()){ReleaseVehicleSeat();Follow();return;}if(vehicle.Speed>1.5f){Game.DisplayNotification("~y~Stop the vehicle before unloading the K9.");return;}NativeFunction.Natives.SET_VEHICLE_DOOR_OPEN(vehicle,_dogVehicleDoor,false,false);GameFiber.Wait(650);int savedHealth=Math.Max(100,_dog.Health);Vector3 exit=vehicle.GetOffsetPosition(new Vector3(_dogVehicleDoor==2?-1.35f:1.35f,-1.25f,.15f));_dog.Tasks.ClearImmediately();ReleaseVehicleSeat();_dog.Position=exit;_dog.Heading=vehicle.Heading;if(_dog.IsDead)NativeFunction.Natives.RESURRECT_PED(_dog);_dog.Health=savedHealth;GameFiber.Wait(650);NativeFunction.Natives.SET_VEHICLE_DOOR_SHUT(vehicle,_dogVehicleDoor,false);K9IncidentLog.Write(_profile.Name,"Kennel","Unloaded",exit);Follow();}

        private void ReleaseVehicleSeat(){CloseSeatCalibrationDoor();if(_dog!=null&&_dog.Exists()){if(_dogSeatAttached)NativeFunction.Natives.DETACH_ENTITY(_dog,true,true);NativeFunction.Natives.SET_ENTITY_COLLISION(_dog,true,true);NativeFunction.Natives.SET_ENTITY_INVINCIBLE(_dog,false);}_dogSeatAttached=false;_dogVehicle=null;}

        private void Inspect(){Game.DisplayNotification("~b~K9 "+_profile.Name+" — FIELD INSPECTION~s~~n~Health: "+_profile.Health+"%  Stamina: "+_profile.Stamina+"%~n~Bond: "+_trust.Level+"/100 ("+_trust.Rank+")  Confidence: "+_profile.Confidence+"/100~n~Training: Level "+_profile.TrainingLevel+"/5 • "+_profile.TrainingLevelProgress+"/"+_profile.CurrentTrainingRequirement+" XP~n~~g~Completed certifications:~s~ "+Certifications());Game.DisplayNotification("~b~DUTY EQUIPMENT~s~~n~Meals "+_profile.FoodMeals+"  Water "+_profile.WaterBottles+"  First aid "+_profile.FirstAidKits+"~n~Scent bags "+_profile.ScentBags+"  Treats "+_profile.Treats+"~n~~b~Integration:~s~ "+_pr.ModeLabel);}
        private string Certifications(){string s="";if(_profile.ObedienceCertified)s+="OB ";if(_profile.AgilityCertified)s+="AGI ";if(_profile.DetectionCertified)s+="DET ";if(_profile.NarcoticsCertified)s+="NAR ";if(_profile.ExplosivesCertified)s+="BOMB ";if(_profile.WeaponsCertified)s+="WPN ";if(_profile.TrackingCertified)s+="TRK ";if(_profile.ApprehensionCertified)s+="APP ";return s.Length==0?"In training":s.Trim();}
        private void FirstAid(){if(!DogEntityExists()){Game.DisplayNotification("~y~No deployed K9 is available for treatment.");return;}if(!_downed&&_profile.Health>=95){Game.DisplayNotification("~g~No field treatment required.");return;}if(!_profile.UseFirstAid()){Game.DisplayNotification("~r~No first-aid kits. Restock at the patrol vehicle.");return;}if(_dog.IsDead)NativeFunction.Natives.RESURRECT_PED(_dog);_dog.IsInvincible=false;int restored=Math.Max(35,_profile.Health);_profile.SetInjury("Serious — stabilized; veterinary treatment required",restored);_dog.Health=Math.Max(1,(int)(_dog.MaxHealth*restored/100f));_downed=false;_state=K9State.Injured;if(_blip!=null&&_blip.Exists()){_blip.Color=Color.DodgerBlue;_blip.Name="K9 "+_profile.Name;}Sit();GameFiber.Wait(1800);_profile.ChangeTrust(2);K9IncidentLog.Write(_profile.Name,"Medical","Emergency field revival and stabilization",_dog.Position);Game.LogTrivial("AdvancedK9: downed K9 revived by field first aid at "+_dog.Position+".");Game.DisplayNotification("~g~K9 stabilized and revived.~s~~n~Return to the veterinarian before resuming duty.");}

        private void Rest(){if(!DogExists())return;LieDown();ActionNotification("~b~K9 rest cycle started.~s~ Maintain a safe perimeter.");GameFiber.Wait(8000);_profile.Rest();K9IncidentLog.Write(_profile.Name,"Care","Rest cycle",_dog.Position);ActionNotification("~g~K9 rested.~s~ Stamina restored.");}
        private void Bathroom()
        {
            if(!DogExists()||_state==K9State.InVehicle||_state==K9State.Searching||_state==K9State.Tracking||_state==K9State.Apprehending)return;
            bool urinate=_bladder<_bowel||(_bladder==_bowel&&_random.Next(2)==0);_dog.Tasks.Clear();_state=K9State.Staying;Vector3 reliefPoint=_dog.GetOffsetPosition(new Vector3(0f,-.35f,0f));
            if(urinate){PlayDogAnimation("creatures@rottweiler@move","pee",3200,0);try{NativeFunction.Natives.REQUEST_NAMED_PTFX_ASSET("core");GameFiber.Wait(100);NativeFunction.Natives.USE_PARTICLE_FX_ASSET("core");NativeFunction.Natives.START_PARTICLE_FX_NON_LOOPED_AT_COORD("ent_sht_water",reliefPoint.X,reliefPoint.Y,reliefPoint.Z+.12f,0f,0f,0f,.35f,false,false,false);}catch{} _bladder=100;K9IncidentLog.Write(_profile.Name,"Care","Urinated automatically",reliefPoint);}
            else{PlayDogAnimation("creatures@rottweiler@amb@world_dog_sitting@base","base",3000,0);SpawnDogWaste(reliefPoint);_bowel=100;K9IncidentLog.Write(_profile.Name,"Care","Defecated automatically",reliefPoint);}Follow();
        }
        private void SpawnDogWaste(Vector3 position){try{var model=new Model("prop_big_shit_02");if(!model.IsValid)model=new Model("prop_big_shit_01");if(!model.IsValid)return;model.LoadAndWait();var waste=new Rage.Object(model,position);model.Dismiss();if(waste==null||!waste.Exists())return;waste.IsPersistent=true;GameFiber.StartNew(()=>{GameFiber.Wait(180000);if(waste.Exists())waste.Delete();});}catch(Exception ex){Game.LogTrivial("AdvancedK9 dog waste prop: "+ex.Message);}}
        private void VeterinaryCare(){if(!DogEntityExists())return;var handler=Game.LocalPlayer.Character;Vector3 p=handler.Position;float h=handler.Heading;if(_dog.IsDead)NativeFunction.Natives.RESURRECT_PED(_dog);NativeFunction.Natives.DO_SCREEN_FADE_OUT(450);GameFiber.Wait(550);handler.Position=new Vector3(306.7f,-595.2f,43.3f);_dog.Position=handler.GetOffsetPosition(new Vector3(1f,0f,0f));GameFiber.Wait(1800);_profile.VeterinaryTreat();_dog.Health=_dog.MaxHealth;_dog.IsInvincible=false;_downed=false;if(_blip!=null&&_blip.Exists()){_blip.Color=Color.DodgerBlue;_blip.Name="K9 "+_profile.Name;}handler.Position=p;handler.Heading=h;_dog.Position=handler.GetOffsetPosition(new Vector3(-1f,-1f,0f));NativeFunction.Natives.DO_SCREEN_FADE_IN(450);K9IncidentLog.Write(_profile.Name,"Medical","Veterinary cleared",p);Game.DisplayNotification("~g~Veterinary clearance complete.~s~ K9 returned to service.");Follow();}
        private void Restock(){var handler=Game.LocalPlayer.Character;var vehicle=World.GetAllVehicles().Where(v=>v.Exists()&&v.DistanceTo(handler)<5f).FirstOrDefault();if(vehicle==null){Game.DisplayNotification("~y~Stand beside a patrol vehicle to restock.");return;}_profile.Restock();K9IncidentLog.Write(_profile.Name,"Equipment","Restocked",handler.Position);Game.DisplayNotification("~g~K9 duty equipment restocked.");}
        private void WhistleRecall(){NativeFunction.Natives.PLAY_SOUND_FRONTEND(-1,"NAV_UP_DOWN","HUD_FRONTEND_DEFAULT_SOUNDSET",true);Game.DisplaySubtitle("~b~Handler whistle recall",1200);Follow();}
        private void HandSignal(){var handler=Game.LocalPlayer.Character;NativeFunction.Natives.REQUEST_ANIM_DICT("gestures@m@standing@casual");GameFiber.Wait(150);handler.Tasks.PlayAnimation("gestures@m@standing@casual","gesture_come_here_soft",4f,AnimationFlags.None);GameFiber.Wait(700);Follow();}

        private void CaptureScentTrails()
        {
            if(!_deployed||Game.GameTime<_nextTrailCapture)return;_nextTrailCapture=Game.GameTime+2500;var handler=Game.LocalPlayer.Character;uint cutoff=Game.GameTime>600000?Game.GameTime-600000:0;int captured=0;
            foreach(var ped in World.GetAllPeds())
            {
                if(ped==null||!ped.Exists()||ped==handler||ped==_dog||ped.IsDead||ped.DistanceTo(handler)>200f||LspdfrBridge.IsPedCop(ped))continue;
                List<ScentTrailPoint> trail;if(!_recordedTrails.TryGetValue(ped.Handle,out trail)){trail=new List<ScentTrailPoint>();_recordedTrails[ped.Handle]=trail;}
                if(trail.Count==0||trail[trail.Count-1].Position.DistanceTo(ped.Position)>=3f)trail.Add(new ScentTrailPoint(ped.Position,Game.GameTime));
                trail.RemoveAll(p=>p.Time<cutoff);if(trail.Count>220)trail.RemoveRange(0,trail.Count-220);
                if(++captured>=96)break;
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
            _hudSearchLabel=target is Vehicle?"VEHICLE SEARCH":"AREA SEARCH";
            _hudSearchProgress=0;
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
                    _hudSearchProgress=(i*100)/3;
                    var sniffPoint=target.GetOffsetPosition(new Vector3(i==0?-.8f:i==1?.8f:0f,-.45f,0f));
                    _dog.Tasks.FollowNavigationMeshToPosition(sniffPoint,target.Heading,1.2f).WaitForCompletion(2500);
                    PlayDogAnimation("creatures@rottweiler@indication@","indicate_low",650,0);
                    GameFiber.Wait(700);
                }
            }
            var compatibility=_pr.GetSearchResult(target,specialty,_profile.NarcoticsCertified,_profile.ExplosivesCertified,_profile.WeaponsCertified);
            var positive=compatibility!=null?compatibility.Positive:(_pr.IsAvailable?false:_random.NextDouble()<_config.PositiveChance);
            var resultSpecialty=compatibility!=null&&compatibility.Specialty!=DetectionSpecialty.General?compatibility.Specialty:positive&&specialty==DetectionSpecialty.General?CertifiedGeneralSearchSpecialty():specialty;
            if (positive && _random.NextDouble() > _trust.DetectionReliability)
            {
                Game.DisplayNotification("~o~Uncertain K9 response.~s~ Build trust and repeat the search.");
                Follow();
                return;
            }
            if (positive)
            {
                SetHudAlert(SpecialtyLabel(resultSpecialty).ToUpperInvariant());
                Sit();
                Bark(3);
                _trust.Change(1, "successful detection");
                _profile.RecordSearch();
                _pr.RecordK9Indication(target,true,resultSpecialty,_profile.Name);
                Game.LogTrivial("AdvancedK9 search result: positive "+SpecialtyLabel(resultSpecialty)+" indication on "+TargetLabel(target)+"; three-bark alert authorized.");
            }
            else
            {
                SetHudAlert("NEGATIVE — NO "+(specialty==DetectionSpecialty.General?"CERTIFIED ODOR":SpecialtyLabel(specialty).ToUpperInvariant()));
                Sit();
                _pr.RecordK9Indication(target,false,specialty,_profile.Name);
                Game.LogTrivial("AdvancedK9 search result: negative indication on "+TargetLabel(target)+"; K9 remains silent.");
            }
            _hudSearchProgress=100;
            _hudSearchLabel="";
        }

        private void BeginSearch(bool vehicleOnly,DetectionSpecialty specialty)
        {
            if(_searchInProgress){Game.DisplayNotification("~y~K9 search already in progress.");return;}
            _searchInProgress=true;_state=K9State.Searching;_hudSearchLabel=vehicleOnly?"VEHICLE SEARCH":"AREA SEARCH";_hudSearchProgress=0;
            GameFiber.StartNew(()=>{try{Search(vehicleOnly,specialty);}catch(Exception ex){Game.LogTrivial("AdvancedK9 asynchronous search failed: "+ex);Game.DisplayNotification("~r~K9 search failed.~s~ See RagePluginHook.log.");Follow();}finally{_hudSearchLabel="";_searchInProgress=false;}},"AdvancedK9 Search");
        }

        private void BeginBuildingSearch()
        {
            if(_searchInProgress){Game.DisplayNotification("~y~K9 search already in progress.");return;}
            _searchInProgress=true;_state=K9State.Searching;_hudSearchLabel="BUILDING SEARCH";_hudSearchProgress=0;
            GameFiber.StartNew(()=>{try{SearchBuilding();}catch(Exception ex){Game.LogTrivial("AdvancedK9 asynchronous building search failed: "+ex);Game.DisplayNotification("~r~K9 building search failed.~s~ See RagePluginHook.log.");Follow();}finally{_hudSearchLabel="";_searchInProgress=false;}},"AdvancedK9 Building Search");
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
            var points=new[]{new Vector3(-1.45f,2.45f,0f),new Vector3(1.45f,2.45f,0f),new Vector3(1.45f,-2.45f,0f),new Vector3(-1.45f,-2.45f,0f)};
            for(var i=0;i<points.Length;i++)
            {
                _hudSearchLabel="VEHICLE SEARCH";
                _hudSearchProgress=i*25;
                if(!DogExists()||!vehicle.Exists()||_state!=K9State.Searching)return false;
                var point=vehicle.GetOffsetPosition(points[i]);
                _dog.Tasks.Clear();
                _dog.Tasks.FollowNavigationMeshToPosition(point,vehicle.Heading,1.45f).WaitForCompletion(5000);
                if(_dog.DistanceTo(point)>3.5f)continue;
                NativeFunction.Natives.TASK_TURN_PED_TO_FACE_ENTITY(_dog,vehicle,900);GameFiber.Wait(900);
                NativeFunction.Natives.TASK_PAUSE(_dog,900);GameFiber.Wait(900);
            }
            _hudSearchProgress=100;
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
            // Never play a human-authored petting clip on an animal skeleton.
            // The K9 remains in its native seated idle while the handler kneels.
            PlayDogAnimation("creatures@rottweiler@amb@world_dog_sitting@base","base",4200,1);
            handler.Tasks.ClearImmediately();Sit();
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

        private bool DogEntityExists()=>_dog!=null&&_dog.Exists();

        private void MaintainK9Availability()
        {
            if(!_deployed)return;
            if(DogEntityExists())
            {
                _lastDogPosition=_dog.Position;_lastDogHeading=_dog.Heading;
                if(!_dog.IsDead)return;
                NativeFunction.Natives.RESURRECT_PED(_dog);
                NativeFunction.Natives.CLEAR_PED_TASKS_IMMEDIATELY(_dog);
                _dog.Health=1;_dog.IsInvincible=true;_dog.BlockPermanentEvents=true;
                _profile.SetInjury("Critical — downed; field first aid required",1);
                _state=K9State.Injured;_downed=true;DeleteLeashRope();ReleaseVehicleSeat();_camera.Disable();
                if(_blip==null||!_blip.Exists()){_blip=_dog.AttachBlip();_blip.Name="K9 "+_profile.Name+" — DOWNED";}
                _blip.Color=Color.Red;
                K9IncidentLog.Write(_profile.Name,"Medical","K9 downed in field",_dog.Position);
                Game.LogTrivial("AdvancedK9: K9 entered recoverable downed state at "+_dog.Position+".");
                Game.DisplayNotification("~r~K9 DOWN — EMERGENCY TREATMENT REQUIRED~s~~n~Use Care & Medical > First Aid near "+_profile.Name+".");
                return;
            }
            Game.LogTrivial("AdvancedK9: deployed K9 entity disappeared; recreating a recoverable downed K9 at last known position.");
            if(CreateDogAt(_lastDogPosition,_lastDogHeading))
            {
                _dog.Health=1;_dog.IsInvincible=true;_profile.SetInjury("Critical — downed; field first aid required",1);
                _state=K9State.Injured;_downed=true;if(_blip!=null&&_blip.Exists()){_blip.Color=Color.Red;_blip.Name="K9 "+_profile.Name+" — DOWNED";}
            }
        }

        private void MaintainState()
        {
            if (!DogEntityExists()||_downed) return;
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
            if(Game.GameTime<_nextHudUpdate)return;_nextHudUpdate=Game.GameTime+50;
            bool inactive=!_deployed||_state==K9State.Dismissed||_state==K9State.InVehicle;
            bool collapsed=_profile.HudMode==1&&_profile.HudAutoCollapse&&inactive&&!_hudPreviewSearch&&!_hudPreviewAlert;
            bool hudSearching=_hudPreviewSearch||_searchInProgress||_state==K9State.Searching;
            string search=_hudPreviewSearch?"SEARCH PREVIEW":hudSearching&&!string.Equals(_hudSearchLabel,"VEHICLE SEARCH",StringComparison.OrdinalIgnoreCase)?(_hudSearchLabel.Length>0?_hudSearchLabel:"SCANNING"):_state==K9State.Tracking?"SCENT TRACK":"";
            int searchProgress=_hudPreviewSearch?64:_hudSearchProgress;
            string alert=_hudPreviewAlert?"NARCOTICS":Game.GameTime<_hudAlertUntil?_hudAlert:"";
            float distance=DogEntityExists()?_dog.DistanceTo(Game.LocalPlayer.Character):0f;
            string displayState=_downed?"DOWNED":_deployed?(hudSearching?"SEARCHING":_state.ToString()):"KENNELED";
            _hud.Update(new GlassTacticalHud.Snapshot{Visible=_profile.HudMode!=0,Collapsed=collapsed,ShowPortrait=_profile.HudShowPortrait,CircularPortrait=_profile.HudPortraitShape==0,ShowState=_profile.HudShowState,ShowHealth=_profile.HudShowHealth,ShowStamina=_profile.HudShowStamina,ShowDistance=_profile.HudShowDistance&&DogEntityExists(),ShowCommand=_profile.HudShowCommand,ShowBehavior=_profile.HudShowBehavior,ShowSearchProgress=_profile.HudSearchProgress,X=_profile.HudX,Y=_profile.HudY,Scale=_profile.HudScale,Opacity=_profile.HudOpacity,Distance=distance,Health=_profile.Health,Stamina=_profile.Stamina,SearchProgress=searchProgress,Coat=_profile.CoatVariation,Vest=_profile.VestIndex,VestTexture=_profile.VestTexture,Name=_profile.Name,State=displayState,Command=_hudCommand,Behavior=hudSearching?"SEARCHING":HudBehavior(),SearchLabel=search,Alert=alert,PortraitFile=_profile.PortraitFile,Breed=_profile.Breed,Model=_profile.ModelName,AppearanceKey=_profile.CoatVariation+":"+_profile.VestIndex+":"+_profile.VestTexture,Metric=_profile.HudMetricDistance});
            if(_camera.Active&&DogExists()){float d=_dog.DistanceTo(Game.LocalPlayer.Character);NativeFunction.Natives.DRAW_RECT(.5f,.91f,.52f,.09f,0,0,0,180);DrawText("K9 CAM  GPS "+_dog.Position.X.ToString("0")+","+_dog.Position.Y.ToString("0")+"  HDG "+HeadingCardinal(_dog.Heading)+"  HANDLER "+d.ToString("0.0")+"m",.25f,.875f,.31f);DrawText("STATE "+_state+"  HP "+_profile.Health+"  STA "+_profile.Stamina+"  H2O "+_profile.Water,.25f,.91f,.27f);}
        }
        private string HudBehavior(){if(_downed)return "NEEDS FIRST AID";if(!_deployed)return "INACTIVE";switch(_state){case K9State.Following:return "FOLLOWING";case K9State.Heeling:return "AT HEEL";case K9State.Searching:return "SEARCHING";case K9State.Tracking:return "TRACKING";case K9State.Apprehending:return "DEPLOYED";case K9State.InVehicle:return "SECURED";case K9State.Leashed:return "LEASHED";default:return _state.ToString().ToUpperInvariant();}}
        private void SetHudAlert(string result){_hudAlert=result??"";_hudAlertUntil=Game.GameTime+(uint)_profile.HudAlertDuration;}
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
                Game.DisplayNotification("~b~Arrived at the Advanced K9 training ground.~s~~n~Level "+level+"/5 — "+_profile.CurrentTrainingName+"~n~XP "+_profile.TrainingLevelProgress+"/"+_profile.CurrentTrainingRequirement+" • Confidence "+_profile.Confidence+"/100");
                var academy=new AcademySession(_dog,_profile.Name);
                int performance=academy.Run(level,Sit,LieDown,Follow),xp=CalculateTrainingXp(level,performance);
                bool completed=_profile.ApplyTrainingProgress(level,xp);
                _trust.Change(xp>0?Math.Max(1,xp/10):0,"academy training");
                if(completed)Game.DisplayNotification("~g~LEVEL "+level+" CERTIFICATION COMPLETE~s~~n~"+(level<5?"Level "+_profile.TrainingLevel+" is now unlocked.":"All K9 certifications completed."));
                else Game.DisplayNotification("~b~Training saved:~s~ +"+xp+" XP from "+performance+"% performance~n~Level "+_profile.TrainingLevel+": "+_profile.TrainingLevelProgress+"/"+_profile.CurrentTrainingRequirement+" XP");
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
                Game.DisplayNotification("~b~Specialty academy:~s~ "+SpecialtyLabel(specialty)+" detection • "+_profile.SpecialtyProgress(specialty)+"/250 XP.");
                var academy=new AcademySession(_dog,_profile.Name);int performance=academy.RunSpecialty(specialty,Sit,Follow);int xp=CalculateSpecialtyXp(performance);
                bool completed=_profile.ApplySpecialtyProgress(specialty,xp);_trust.Change(xp>0?Math.Max(1,xp/12):0,"specialty detection training");
                if(completed)Game.DisplayNotification("~g~"+SpecialtyLabel(specialty).ToUpperInvariant()+" DETECTION CERTIFIED — 250 XP~s~~n~Other detection specialties remain independently trainable.");
                else Game.DisplayNotification("~b~Specialty saved:~s~ +"+xp+" XP from "+performance+"% performance~n~"+SpecialtyLabel(specialty)+" "+_profile.SpecialtyProgress(specialty)+"/250 XP.");
            }
            finally{NativeFunction.Natives.DO_SCREEN_FADE_OUT(500);GameFiber.Wait(650);handler.Position=returnPosition;handler.Heading=returnHeading;_dog.Position=handler.GetOffsetPosition(new Vector3(-1f,-2f,0f));NativeFunction.Natives.DO_SCREEN_FADE_IN(700);Follow();}
        }

        private int CalculateTrainingXp(int level,int performance){int cap=level<=2?10:level<=4?20:30;int earnedCap=(int)Math.Round(cap*Math.Max(0,Math.Min(100,performance))/100.0);return earnedCap<=0?0:_random.Next(0,earnedCap+1);}
        private int CalculateSpecialtyXp(int performance){int earnedCap=(int)Math.Round(20*Math.Max(0,Math.Min(100,performance))/100.0);return earnedCap<=0?0:_random.Next(0,earnedCap+1);}

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
            _deployed=false;_downed=false;
            if (notify) ActionNotification("~b~"+_profile.Name+" returned to the station kennel.");
        }

        public void Dispose()
        {
            _running = false;
            _voice?.Dispose();
            _voice = null;
            _hud.Dispose();
            _trust.Save();
            _profile.Save();
            Dismiss(false);
            DeleteStationKennels();
        }
    }
}
