using System;
using System.Globalization;
using System.IO;
using Rage;

namespace AdvancedK9
{
    internal sealed class TrustProfile
    {
        private readonly string _path = Path.Combine("Plugins", "LSPDFR", "AdvancedK9", "trust.dat");
        public int Level { get; private set; }
        public string Rank => Level >= 90 ? "Elite Bond" : Level >= 70 ? "Trusted" : Level >= 45 ? "Partner" : Level >= 25 ? "Developing" : "New Team";
        public float ObedienceChance => 0.55f + (Level * 0.0045f);
        public int ResponseDelay => Math.Max(100, 850 - (Level * 7));
        public float DetectionReliability => 0.60f + (Level * 0.004f);

        public TrustProfile(int startingTrust)
        {
            Level = Clamp(startingTrust);
            try
            {
                int stored;
                if (File.Exists(_path) && int.TryParse(File.ReadAllText(_path).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out stored))
                    Level = Clamp(stored);
            }
            catch (Exception ex) { Game.LogTrivial("AdvancedK9 trust load: " + ex.Message); }
        }

        public void Change(int amount, string reason)
        {
            var old = Level;
            Level = Clamp(Level + amount);
            Save();
            if (Level != old)
                Game.DisplayNotification("~b~K9 trust " + (amount > 0 ? "+" : "") + (Level - old) + "~s~ (" + reason + ")~n~" + Level + "/100 — " + Rank);
        }

        public void Save()
        {
            try
            {
                var directory = Path.GetDirectoryName(_path);
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
                File.WriteAllText(_path, Level.ToString(CultureInfo.InvariantCulture));
            }
            catch (Exception ex) { Game.LogTrivial("AdvancedK9 trust save: " + ex.Message); }
        }

        private static int Clamp(int value) => Math.Max(0, Math.Min(100, value));
    }
}
