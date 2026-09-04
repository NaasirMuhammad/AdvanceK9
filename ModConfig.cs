using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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
        public bool ModifierEnabled = true;
        public bool VoiceEnabled = true;
        public bool ContinuousListening = false;
        public bool ShowVoiceStatusText = false;
        public bool ShowActionNotifications = false;
        public string VoiceProvider = "Groq";
        public string VoiceModel = "whisper-large-v3-turbo";
        public string VoiceLanguage = "en";
        public string MenuLanguage = "en";
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
        public bool CompatibilityUseCdfInventory = true;
        public bool CompatibilityShareWithNexusMdt = true;
        public bool CompatibilityProtectManagedPeds = true;
        public string PortraitFile = "";
        public readonly Dictionary<K9Command,string[]> CustomCommandPhrases=new Dictionary<K9Command,string[]>();
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
            string modifier=ini.ReadString("Keys","Modifier",result.ModifierKey.ToString()).Trim();
            result.ModifierEnabled=!modifier.Equals("None",StringComparison.OrdinalIgnoreCase)&&!modifier.Equals("Disabled",StringComparison.OrdinalIgnoreCase)&&!modifier.Equals("Off",StringComparison.OrdinalIgnoreCase);
            if(result.ModifierEnabled){Keys parsed;if(Enum.TryParse(modifier,true,out parsed)&&parsed!=Keys.None)result.ModifierKey=parsed;}
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
            result.MenuLanguage = Localization.NormalizeCode(ini.ReadString("Localization", "Language", result.MenuLanguage));
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
            result.CompatibilityUseCdfInventory = ini.ReadBoolean("Compatibility", "UseCdfInventory", result.CompatibilityUseCdfInventory);
            result.CompatibilityShareWithNexusMdt = ini.ReadBoolean("Compatibility", "ShareWithNexusMDT", result.CompatibilityShareWithNexusMdt);
            result.CompatibilityProtectManagedPeds = ini.ReadBoolean("Compatibility", "ProtectRestrainedPeds", result.CompatibilityProtectManagedPeds);
            result.PortraitFile = ini.ReadString("HUD", "PortraitFile", result.PortraitFile);
            foreach(var definition in CommandRegistry.All)
            {
                string aliases=ini.ReadString("CommandPhrases",definition.Command.ToString(),"");
                if(string.IsNullOrWhiteSpace(aliases))continue;
                string[] phrases=aliases.Split(new[]{'|',';'},StringSplitOptions.RemoveEmptyEntries).Select(x=>x.Trim()).Where(x=>!string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                if(phrases.Length>0)result.CustomCommandPhrases[definition.Command]=phrases;
            }
            foreach(string key in KennelKeys)result._kennelLocations[key]=ini.ReadString("KennelLocations",key,"");
            result.MigrateLegacyKennelDefaults();
            return result;
        }

        private void MigrateLegacyKennelDefaults()
        {
            // Each final entry is last. Earlier entries are only the exact defaults shipped
            // by the two preceding v0.22.10 packages; genuinely edited values do not match
            // and are therefore never overwritten.
            var placements=new Dictionary<string,string[]>(StringComparer.OrdinalIgnoreCase){
                {"MissionRow",new[]{"435.4404,-974.9838,30.71601,84.88242","435.4404,-974.9838,30.71601,174.88242","436.4405,-974.9838,29.78568,359.8824"}},
                {"Davis",new[]{"354.2758,-1591.15,29.29195,45.5168","354.2758,-1591.15,29.29195,135.5168","354.2758,-1591.15,28.31161,320.5168"}},
                {"Vespucci",new[]{"-1082.498,-802.5654,19.22887,10.9947","-1082.498,-802.5654,19.22887,100.9947","-1082.498,-803.2653,18.24856,280.9947"}},
                {"Vinewood",new[]{"637.631,-3.024063,82.78731,246.6745","637.631,-3.024063,82.78731,336.6745","636.7312,-2.824063,81.80692,161.6745"}},
                {"LaMesa",new[]{"840.4388,-1276.318,26.44634,2.712838","840.4388,-1276.318,26.44634,92.712838","840.4388,-1276.518,25.46601,267.7128"}},
                {"SandyShores",new[]{"1871.8,3691.7,33.7,210","1873.2,3692.601,32.66043,115"}},
                {"Paleto",new[]{"-445.2472,6023.268,31.49012,314.0662","-445.2472,6023.268,31.49012,44.0662","-445.2472,6022.968,30.55979,224.0662"}},
                {"Ranger",new[]{"370.1926,793.9409,187.5991,166.3364","370.1926,793.9409,187.5991,256.3364","370.1926,793.9409,186.6179,96.3364"}},
                {"DelPerro",new[]{"-1621.023,-1013.941,13.15342,55.20818","-1621.023,-1013.941,13.15342,145.20818","-1621.023,-1013.941,12.17308,320.2082"}},
                {"PortOfLosSantos",new[]{"-343.7605,-2787.573,5.000235,5.843473","-343.7605,-2787.573,5.000235,95.843473","-343.8605,-2788.374,4.0199,265.8435"}},
                {"GreatOceanHighway",new[]{"-1490.288,4975.141,63.71766,94.95189","-1490.288,4975.141,63.71766,184.95189","-1490.288,4975.141,62.78698,354.9519"}},
                {"FortZancudo",new[]{"-2363.956,3274.042,32.99627,145.3562","-2363.956,3274.042,32.99627,235.3562","-2363.756,3274.542,32.01595,60.3562"}},
                {"FIB",new[]{"110.5135,-759.2312,45.75479,331.6973","110.5135,-759.2312,45.75479,61.6973","110.5135,-759.2312,44.77443,246.6973"}},
                {"BrookTrail",new[]{"1744.612,3035.371,61.8116,80.94661","1744.612,3035.371,61.8116,170.94661","1744.612,3035.371,60.83065,335.9466"}}
            };
            foreach(var pair in placements)
            {
                string current;if(!_kennelLocations.TryGetValue(pair.Key,out current)||string.IsNullOrWhiteSpace(current))continue;bool legacy=false;
                for(int i=0;i<pair.Value.Length-1;i++)if(SamePlacement(current,pair.Value[i])){legacy=true;break;}
                if(!legacy)continue;Vector3 position;float heading;if(!TryParsePlacement(pair.Value[pair.Value.Length-1],out position,out heading))continue;
                try{SaveKennelLocation(pair.Key,position,heading);Game.LogTrivial("AdvancedK9 migrated finalized kennel default: "+pair.Key+".");}catch{}
            }
        }

        private static bool SamePlacement(string left,string right)
        {
            Vector3 a,b;float ah,bh;if(!TryParsePlacement(left,out a,out ah)||!TryParsePlacement(right,out b,out bh))return false;
            return Math.Abs(a.X-b.X)<.002f&&Math.Abs(a.Y-b.Y)<.002f&&Math.Abs(a.Z-b.Z)<.002f&&Math.Abs(ah-bh)<.002f;
        }

        private static bool TryParsePlacement(string value,out Vector3 position,out float heading)
        {
            position=new Vector3();heading=0f;if(string.IsNullOrWhiteSpace(value))return false;string[] parts=value.Split(',');float x,y,z,h;
            if(parts.Length!=4||!float.TryParse(parts[0].Trim(),NumberStyles.Float,CultureInfo.InvariantCulture,out x)||!float.TryParse(parts[1].Trim(),NumberStyles.Float,CultureInfo.InvariantCulture,out y)||!float.TryParse(parts[2].Trim(),NumberStyles.Float,CultureInfo.InvariantCulture,out z)||!float.TryParse(parts[3].Trim(),NumberStyles.Float,CultureInfo.InvariantCulture,out h))return false;
            position=new Vector3(x,y,z);heading=h;return true;
        }

        public bool TryGetKennelLocation(string key,out Vector3 position,out float heading)
        {
            position=new Vector3();heading=0f;string value;
            if(!_kennelLocations.TryGetValue(key,out value)||string.IsNullOrWhiteSpace(value))return false;
            if(!TryParsePlacement(value,out position,out heading))
            {
                Game.LogTrivial("AdvancedK9 ignored invalid kennel override '"+key+"'. Expected X,Y,Z,Heading.");
                return false;
            }
            return true;
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

        public void SaveLanguage(string code)
        {
            code=Localization.NormalizeCode(code);MenuLanguage=code;
            try
            {
                var lines=File.Exists(_configPath)?new List<string>(File.ReadAllLines(_configPath)):new List<string>();
                int section=-1,end=lines.Count,keyLine=-1;
                for(int i=0;i<lines.Count;i++)
                {
                    string value=lines[i].Trim();
                    if(value.Equals("[Localization]",StringComparison.OrdinalIgnoreCase)){section=i;continue;}
                    if(section>=0&&i>section&&value.StartsWith("[")&&value.EndsWith("]")){end=i;break;}
                    if(section>=0&&i>section&&value.StartsWith("Language=",StringComparison.OrdinalIgnoreCase))keyLine=i;
                }
                if(section<0){if(lines.Count>0&&lines[lines.Count-1].Length>0)lines.Add("");lines.Add("[Localization]");section=lines.Count-1;end=lines.Count;}
                if(keyLine>=0)lines[keyLine]="Language="+code;else lines.Insert(end,"Language="+code);
                string temporary=_configPath+".tmp";File.WriteAllLines(temporary,lines);File.Copy(temporary,_configPath,true);File.Delete(temporary);
            }
            catch(Exception ex){Game.LogTrivial("AdvancedK9 language save failed: "+ex.Message);}
        }
    }
}

