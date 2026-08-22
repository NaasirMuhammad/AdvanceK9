using System;
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

        public static ModConfig Load()
        {
            var result = new ModConfig();
            var path = Path.Combine("Plugins", "LSPDFR", "AdvancedK9", "AdvancedK9.ini");
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
            return result;
        }

        private static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(max, value));
    }
}
