using System;
using System.Linq;
using System.Reflection;
using System.Collections;
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

        public static Ped GetPursuitSuspect(Ped handler)
        {
            try
            {
                var type=FunctionsType;if(type==null)return null;
                var active=type.GetMethods(BindingFlags.Public|BindingFlags.Static).FirstOrDefault(m=>m.Name=="GetActivePursuit"&&m.GetParameters().Length==0);
                if(active==null)return null;var handle=active.Invoke(null,null);if(handle==null)return null;
                var getPeds=type.GetMethods(BindingFlags.Public|BindingFlags.Static).FirstOrDefault(m=>m.Name=="GetPursuitPeds"&&m.GetParameters().Length==1&&m.GetParameters()[0].ParameterType.IsInstanceOfType(handle));
                if(getPeds==null)return null;var values=getPeds.Invoke(null,new[]{handle}) as IEnumerable;if(values==null)return null;
                Ped nearest=null;float distance=float.MaxValue;
                foreach(var value in values){var ped=value as Ped;if(ped==null||!ped.Exists()||ped.IsDead||ped==handler||IsPedCop(ped))continue;float d=ped.DistanceTo(handler);if(d<distance){nearest=ped;distance=d;}}
                return nearest;
            }
            catch{return null;}
        }
    }
}
