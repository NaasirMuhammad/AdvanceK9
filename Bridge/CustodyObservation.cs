using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AdvancedK9.Runtime;
using Rage;
using Rage.Native;

namespace AdvancedK9.LSPDFRBridge
{
    public sealed partial class EntryPoint
    {
        private sealed class WatchedSuspect
        {
            internal Ped Ped;
            internal PedIdentity Identity;
            internal string State="None";
        }
        private static readonly Dictionary<PoolHandle,WatchedSuspect> WatchedSuspects=new Dictionary<PoolHandle,WatchedSuspect>();
        private static void WatchSuspect(Ped ped)
        {
            PedIdentity identity;if(!PedIdentity.TryCapture(ped,out identity))return;
            WatchedSuspect watched;
            if(!WatchedSuspects.TryGetValue(ped.Handle,out watched)||!watched.Identity.Matches(ped))
                WatchedSuspects[ped.Handle]=new WatchedSuspect{Ped=ped,Identity=identity};
        }
        private static void ObserveK9ProviderOwnership(Assembly pr)
        {
            foreach(var pair in WatchedSuspects.ToArray())
            {
                var watched=pair.Value;
                if(!watched.Identity.Matches(watched.Ped)){WatchedSuspects.Remove(pair.Key);continue;}
                try
                {
                    Ped target=watched.Ped;string next=watched.State;
                    bool cuffed=NativeFunction.Natives.IS_PED_CUFFED<bool>(target)||NativeFunction.Natives.IS_PED_HANDCUFFED<bool>(target)||TryLspdfrCustody(target,"IsPedArrested")||(ReadPrPedValue(pr,"IsPedArrested",target) as bool?)==true||(ReadPrPedValue(pr,"IsPedCuffed",target) as bool?)==true;
                    bool arresting=TryLspdfrCustody(target,"IsPedGettingArrested")||NativeFunction.Natives.IS_PED_BEING_ARRESTED<bool>(target)||(ReadPrPedValue(pr,"IsPedGettingArrested",target) as bool?)==true;
                    bool stopped=(ReadPrPedValue(pr,"IsPedStopped",target) as bool?)==true;
                    if(cuffed)next="Cuffed";
                    else if(arresting&&next!="Cuffed")next="Arresting";
                    else if(stopped&&next=="None")next="ProviderInteraction";
                    if(next!=watched.State)
                    {
                        Game.LogTrivial("AdvancedK9 custody observer: ped="+watched.Identity.Handle+", "+watched.State+" -> "+next+"; health="+target.Health+"; no suspect task assigned.");
                        watched.State=next;
                    }
                    if(next!="None")
                    {
                        K9PhysicalCustody hold;
                        if(K9PhysicalHolds.TryGetValue(pair.Key,out hold))
                        {
                            RestoreK9PhysicalCustody(pr,hold,true,true);K9PhysicalHolds.Remove(pair.Key);
                            Game.LogTrivial("AdvancedK9 bridge: temporary K9 controls handed to external provider for ped "+watched.Identity.Handle+"; prior resistance/combat action not replayed.");
                        }
                    }
                }
                catch(Exception ex){Game.LogTrivial("AdvancedK9 custody observer retired invalid suspect: "+ex.GetType().Name);WatchedSuspects.Remove(pair.Key);K9PhysicalHolds.Remove(pair.Key);}
            }
        }
        private static string K9ProviderStates()=>string.Join(";",WatchedSuspects.Values.Select(w=>w.Identity.Handle+"~"+w.Identity.Model+"~"+w.Identity.Address+"~"+w.State));
    }
}
