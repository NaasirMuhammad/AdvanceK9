using System;

namespace AdvancedK9
{
    internal enum ScentArticleType { DirectPerson, VehicleSeat, VehicleDoor, DroppedClothing, Weapon, Blood, PersonalProperty, LastKnownLocationPad }

    internal sealed class ScentSample
    {
        public ScentArticleType Type;
        public string Source;
        public string CollectionZone;
        public int BaseQuality;
        public uint CollectedAt;
        public float RainAtCollection;

        public static int QualityFor(ScentArticleType type)
        {
            switch(type)
            {
                case ScentArticleType.DirectPerson:return 100;
                case ScentArticleType.DroppedClothing:return 95;
                case ScentArticleType.VehicleSeat:return 90;
                case ScentArticleType.Blood:return 88;
                case ScentArticleType.PersonalProperty:return 82;
                case ScentArticleType.VehicleDoor:return 72;
                case ScentArticleType.Weapon:return 70;
                default:return 55;
            }
        }

        public static string Label(ScentArticleType type)
        {
            switch(type)
            {
                case ScentArticleType.DirectPerson:return "direct person scent";
                case ScentArticleType.VehicleSeat:return "vehicle seat scent";
                case ScentArticleType.VehicleDoor:return "vehicle door scent";
                case ScentArticleType.DroppedClothing:return "dropped clothing";
                case ScentArticleType.Weapon:return "weapon";
                case ScentArticleType.Blood:return "blood evidence";
                case ScentArticleType.PersonalProperty:return "personal property";
                default:return "last-known-location scent pad";
            }
        }

        public static ScentArticleType ClassifyObject(string modelName)
        {
            string name=(modelName??"").ToLowerInvariant();
            if(name.Contains("blood"))return ScentArticleType.Blood;
            if(name.Contains("gun")||name.Contains("pistol")||name.Contains("rifle")||name.Contains("weapon")||name.Contains("knife"))return ScentArticleType.Weapon;
            if(name.Contains("shirt")||name.Contains("cloth")||name.Contains("jacket")||name.Contains("shoe")||name.Contains("bag"))return ScentArticleType.DroppedClothing;
            return ScentArticleType.PersonalProperty;
        }
    }

    internal sealed class TrailEnvironment
    {
        public string Label="Open ground";
        public int QualityPenalty;
        public float SpeedMultiplier=1f;
        public int DirectionCheckChance;
    }
}
