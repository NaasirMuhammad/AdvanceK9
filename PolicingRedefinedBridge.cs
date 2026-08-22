using System;
using System.Linq;
using System.Reflection;
using Rage;

namespace AdvancedK9
{
    // PR's public API is optional. Reflection keeps AdvancedK9 loadable when PR/CDF is absent
    // and isolates API changes to this one adapter.
    internal sealed class PolicingRedefinedBridge
    {
        private readonly Assembly _pr;
        private readonly Assembly _cdf;

        public bool IsAvailable => _pr != null || _cdf != null;

        public PolicingRedefinedBridge()
        {
            _pr = Find("PolicingRedefined");
            _cdf = Find("CommonDataFramework");
            Game.LogTrivial("AdvancedK9: Policing Redefined bridge " + (IsAvailable ? "available." : "not found; fallback search tables active."));
        }

        public bool? HasK9Odor(Entity target)
        {
            if (target == null || !target.Exists()) return null;
            try
            {
                // Supports PR releases that expose a static K9/Search API without binding to
                // a particular prerelease signature. Unknown versions safely use fallback odds.
                var types = (_pr?.GetTypes() ?? Array.Empty<Type>()).Concat(_cdf?.GetTypes() ?? Array.Empty<Type>());
                foreach (var type in types.Where(t => t.Name.IndexOf("Search", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .Where(m => m.ReturnType == typeof(bool) &&
                                    (m.Name.IndexOf("K9", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     m.Name.IndexOf("Contraband", StringComparison.OrdinalIgnoreCase) >= 0)))
                    {
                        var p = method.GetParameters();
                        if (p.Length == 1 && p[0].ParameterType.IsInstanceOfType(target))
                            return (bool)method.Invoke(null, new object[] { target });
                    }
                }
            }
            catch (Exception ex) { Game.LogTrivial("AdvancedK9: PR search adapter: " + ex.Message); }
            return null;
        }

        private static Assembly Find(string partialName) => AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name.IndexOf(partialName, StringComparison.OrdinalIgnoreCase) >= 0);
    }
}
