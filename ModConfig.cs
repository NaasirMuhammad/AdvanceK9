using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using Rage;

namespace AdvancedK9
{
    internal sealed class ModConfig
    {
        public Keys SpawnKey = Keys.K;
        public Keys ModifierKey = Keys.LControlKey;
        public Keys CommandKey = Keys.J;
        public Keys CameraKey = Keys.C;
        public Keys LeashKey = Keys.L;
        public Keys PushToTalkKey = Keys.V;
        public Keys KennelKey = Keys.U;
        public bool VoiceEnabled = true;
        public bool ContinuousListening = false;
        public bool ShowVoiceStatusText = false;
        public bool ShowActionNotifications = false;
        public string VoiceProvider = "Groq";
        public string VoiceModel = "whisper-large-v3-turbo";
        public string VoiceLanguage = "en";
        public string VoiceApiKey = "";
        public string VoiceApiKeyEnvironmentVariable = "GROQ_API_KEY";
        public string DogName = "Rex";
        public string DogBreed = "German Shepherd";
        public string CustomDogModel = "";
        public int CoatVariation = 0;
        public int VestStyle = 1;
        public int VestColor = 0;
        public int VestComponent = -1;
        public float PositiveChance = 0.28f;
        public float SearchRadius = 12f;
        public float TrackRadius = 80f;
        public int NonLethalHealthFloor = 35;
        public int StartingTrust = 40;
        public float FetchBallOffsetX = 0.12f;
        public float FetchBallOffsetY = 0.01f;
        public float FetchBallOffsetZ = -0.025f;
        public float VehicleSeatOffsetX = 0f;
        public float VehicleSeatOffsetY = -0.38f;
        public float VehicleSeatOffsetZ = 0.42f;
        public string CompatibilityMode = "Auto";
        public bool CompatibilityUseActiveTargets = true;
        public bool CompatibilityShareResults = true;
        public bool CompatibilityProtectManagedPeds = true;
        public string PortraitFile = "";
        private readonly Dictionary<string,string> _kennelLocations=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        private readonly string _configPath=Path.Combine("Plugins","LSPDFR","AdvancedK9","AdvancedK9.ini");

        private static readonly string[] KennelKeys={"MissionRow","Davis","Vespucci","RockfordHills","Vinewood","LaMesa","SandyShores","Paleto","Ranger","LSIA","Bolingbroke","DelPerro","PortOfLosSantos","GreatOceanHighway","FortZancudo","FIB","BrookTrail"};

        public static ModConfig Load()
        {
            var result = new ModConfig();
            var path = Path.Combine("Plugins", "LSPDFR", "AdvancedK9", "AdvancedK9.ini");
            var defaults = Path.Combine("Plugins", "LSPDFR", "AdvancedK9", "AdvancedK9.default.ini");
            if (!File.Exists(path) && File.Exists(defaults))
            {
                var directory = Path.GetDirectoryName(path);
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
                File.Copy(defaults, path, false);
            }
            var ini = new InitializationFile(path);
            if (!File.Exists(path)) ini.Create();
            result.SpawnKey = ini.ReadEnum("Keys", "SpawnDismiss", result.SpawnKey);
            result.ModifierKey = ini.ReadEnum("Keys", "Modifier", result.ModifierKey);
            result.CommandKey = ini.ReadEnum("Keys", "CommandWheel", result.CommandKey);
            result.CameraKey = ini.ReadEnum("Keys", "DogCamera", result.CameraKey);
            result.LeashKey = ini.ReadEnum("Keys", "Leash", result.LeashKey);
            result.PushToTalkKey = ini.ReadEnum("Keys", "PushToTalk", result.PushToTalkKey);
            result.KennelKey = ini.ReadEnum("Keys", "KennelProfile", result.KennelKey);
            result.VoiceEnabled = ini.ReadBoolean("Voice", "Enabled", result.VoiceEnabled);
            // Continuous capture is intentionally disabled. Voice is push-to-talk only so
            // ambient game, radio and room audio cannot trigger K9 commands.
            result.ContinuousListening = false;
            result.ShowVoiceStatusText = ini.ReadBoolean("Notifications", "ShowVoiceStatusText", result.ShowVoiceStatusText);
            result.ShowActionNotifications = ini.ReadBoolean("Notifications", "ShowActionNotifications", result.ShowActionNotifications);
            result.VoiceProvider = ini.ReadString("Voice", "Provider", result.VoiceProvider);
            result.VoiceModel = ini.ReadString("Voice", "Model", result.VoiceModel);
            result.VoiceLanguage = ini.ReadString("Voice", "Language", result.VoiceLanguage);
            result.VoiceApiKey = ini.ReadString("Voice", "ApiKey", result.VoiceApiKey);
            result.VoiceApiKeyEnvironmentVariable = ini.ReadString("Voice", "ApiKeyEnvironmentVariable", result.VoiceApiKeyEnvironmentVariable);
            result.DogName = ini.ReadString("Dog", "Name", result.DogName);
            result.DogBreed = ini.ReadString("Dog", "Breed", result.DogBreed);
            result.CustomDogModel = ini.ReadString("Dog", "CustomModel", result.CustomDogModel);
            result.CoatVariation = ini.ReadInt32("Dog", "CoatVariation", result.CoatVariation);
            result.VestStyle = ini.ReadInt32("Dog", "VestStyle", result.VestStyle);
            result.VestColor = ini.ReadInt32("Dog", "VestColor", result.VestColor);
            result.VestComponent = ini.ReadInt32("Dog", "VestComponent", result.VestComponent);
            result.PositiveChance = Clamp(ini.ReadSingle("Search", "FallbackPositiveChance", result.PositiveChance), 0f, 1f);
            result.SearchRadius = Math.Max(3f, ini.ReadSingle("Search", "Radius", result.SearchRadius));
            result.TrackRadius = Math.Max(20f, ini.ReadSingle("Tracking", "AcquisitionRadius", result.TrackRadius));
            result.NonLethalHealthFloor = Math.Max(10, ini.ReadInt32("Apprehension", "HealthFloor", result.NonLethalHealthFloor));
            result.StartingTrust = Math.Max(0, Math.Min(100, ini.ReadInt32("Trust", "StartingLevel", result.StartingTrust)));
            result.FetchBallOffsetX = ini.ReadSingle("Fetch", "BallOffsetX", result.FetchBallOffsetX);
            result.FetchBallOffsetY = ini.ReadSingle("Fetch", "BallOffsetY", result.FetchBallOffsetY);
            result.FetchBallOffsetZ = ini.ReadSingle("Fetch", "BallOffsetZ", result.FetchBallOffsetZ);
            result.VehicleSeatOffsetX = ini.ReadSingle("Vehicle", "SeatOffsetX", result.VehicleSeatOffsetX);
            result.VehicleSeatOffsetY = ini.ReadSingle("Vehicle", "SeatOffsetY", result.VehicleSeatOffsetY);
            result.VehicleSeatOffsetZ = ini.ReadSingle("Vehicle", "SeatOffsetZ", result.VehicleSeatOffsetZ);
            result.CompatibilityMode = ini.ReadString("Compatibility", "Mode", result.CompatibilityMode);
            result.CompatibilityUseActiveTargets = ini.ReadBoolean("Compatibility", "UseActiveTargets", result.CompatibilityUseActiveTargets);
            result.CompatibilityShareResults = ini.ReadBoolean("Compatibility", "ShareK9Results", result.CompatibilityShareResults);
            result.CompatibilityProtectManagedPeds = ini.ReadBoolean("Compatibility", "ProtectRestrainedPeds", result.CompatibilityProtectManagedPeds);
            result.PortraitFile = ini.ReadString("HUD", "PortraitFile", result.PortraitFile);
            foreach(string key in KennelKeys)result._kennelLocations[key]=ini.ReadString("KennelLocations",key,"");
            result.MigrateMeasuredKennelHeadings();
            return result;
        }

        private void MigrateMeasuredKennelHeadings()
        {
            var headings=new Dictionary<string,float[]>(StringComparer.OrdinalIgnoreCase){
                {"MissionRow",new[]{84.88242f,174.88242f}},{"Davis",new[]{45.5168f,135.5168f}},{"Vespucci",new[]{10.9947f,100.9947f}},
                {"Vinewood",new[]{246.6745f,336.6745f}},{"LaMesa",new[]{2.712838f,92.712838f}},{"Paleto",new[]{314.0662f,44.0662f}},
                {"Ranger",new[]{166.3364f,256.3364f}},{"DelPerro",new[]{55.20818f,145.20818f}},{"PortOfLosSantos",new[]{5.843473f,95.843473f}},
                {"GreatOceanHighway",new[]{94.95189f,184.95189f}},{"FortZancudo",new[]{145.3562f,235.3562f}},{"FIB",new[]{331.6973f,61.6973f}},{"BrookTrail",new[]{80.94661f,170.94661f}}
            };
            foreach(var pair in headings)
            {
                Vector3 position;float heading;if(!TryGetKennelLocation(pair.Key,out position,out heading)||Math.Abs(heading-pair.Value[0])>.001f)continue;
                try{SaveKennelLocation(pair.Key,position,pair.Value[1]);Game.LogTrivial("AdvancedK9 rotated measured kennel default 90 degrees right: "+pair.Key+".");}catch{}
            }
        }

        public bool TryGetKennelLocation(string key,out Vector3 position,out float heading)
        {
            position=new Vector3();heading=0f;string value;
            if(!_kennelLocations.TryGetValue(key,out value)||string.IsNullOrWhiteSpace(value))return false;
            string[] parts=value.Split(',');float x,y,z,h;
            if(parts.Length!=4||!float.TryParse(parts[0].Trim(),NumberStyles.Float,CultureInfo.InvariantCulture,out x)||!float.TryParse(parts[1].Trim(),NumberStyles.Float,CultureInfo.InvariantCulture,out y)||!float.TryParse(parts[2].Trim(),NumberStyles.Float,CultureInfo.InvariantCulture,out z)||!float.TryParse(parts[3].Trim(),NumberStyles.Float,CultureInfo.InvariantCulture,out h))
            {
                Game.LogTrivial("AdvancedK9 ignored invalid kennel override '"+key+"'. Expected X,Y,Z,Heading.");
                return false;
            }
            position=new Vector3(x,y,z);heading=h;return true;
        }

        public void SaveKennelLocation(string key,Vector3 position,float heading)
        {
            string value=position.X.ToString("0.######",CultureInfo.InvariantCulture)+","+position.Y.ToString("0.######",CultureInfo.InvariantCulture)+","+position.Z.ToString("0.######",CultureInfo.InvariantCulture)+","+heading.ToString("0.######",CultureInfo.InvariantCulture);
            try
            {
                var lines=File.Exists(_configPath)?new List<string>(File.ReadAllLines(_configPath)):new List<string>();
                int section=-1,end=lines.Count,keyLine=-1;
                for(int i=0;i<lines.Count;i++)
                {
                    string trimmed=lines[i].Trim();
                    if(trimmed.Equals("[KennelLocations]",StringComparison.OrdinalIgnoreCase)){section=i;continue;}
                    if(section>=0&&i>section&&trimmed.StartsWith("[")&&trimmed.EndsWith("]")){end=i;break;}
                    if(section>=0&&i>section&&trimmed.StartsWith(key+"=",StringComparison.OrdinalIgnoreCase))keyLine=i;
                }
                if(section<0){if(lines.Count>0&&lines[lines.Count-1].Length>0)lines.Add("");lines.Add("[KennelLocations]");section=lines.Count-1;end=lines.Count;}
                if(keyLine>=0)lines[keyLine]=key+"="+value;else lines.Insert(end,key+"="+value);
                string temporary=_configPath+".tmp";File.WriteAllLines(temporary,lines);File.Copy(temporary,_configPath,true);File.Delete(temporary);
                _kennelLocations[key]=value;
            }
            catch(Exception ex){Game.LogTrivial("AdvancedK9 kennel location save failed for "+key+": "+ex.Message);throw;}
        }

        private static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(max, value));
    }
}
