using System;
using System.Linq;
using System.Reflection;
using Rage;

namespace AdvancedK9
{
    internal static class LspdfrBridge
    {
        private static Type FunctionsType => AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name.Equals("LSPD First Response", StringComparison.OrdinalIgnoreCase))?
            .GetType("LSPD_First_Response.Mod.API.Functions");

        public static bool IsPlayerOnDuty()
        {
            try
            {
                var method = FunctionsType?.GetMethod("IsPlayerOnDuty", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
                return method != null && (bool)method.Invoke(null, null);
            }
            catch { return false; }
        }

        public static bool IsPedCop(Ped ped)
        {
            try
            {
                var method = FunctionsType?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "IsPedACop" && m.GetParameters().Length == 1);
                return method != null && (bool)method.Invoke(null, new object[] { ped });
            }
            catch { return false; }
        }
    }
}
