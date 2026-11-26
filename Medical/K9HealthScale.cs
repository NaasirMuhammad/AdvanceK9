using System;
namespace AdvancedK9.Medical
{
    internal static class K9HealthScale
    {
        // GTA ped health reserves the lower 100 points for the terminal range.
        // Profile health is a percentage of the remaining living capacity.
        internal static int Maximum(int maximum)=>Math.Max(200,maximum);
        internal static int ToEntity(int maximum,int percent)
            => 100+(int)Math.Ceiling((Maximum(maximum)-100)*Math.Max(1,Math.Min(100,percent))/100d);
        internal static int ToProfile(int maximum,int health)
            => Math.Max(0,Math.Min(100,(int)(100d*(health-100)/(Maximum(maximum)-100))));
    }
}
