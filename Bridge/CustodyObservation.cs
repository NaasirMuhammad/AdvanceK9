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
        private static bool EndMedicalPursuit(Ped target,out string detail)
        {
            detail="Pursuit API unavailable; ambulance departure deferred.";
            try
            {
                var flags=BindingFlags.Public|BindingFlags.Static;
                var functions=typeof(LSPD_First_Response.Mod.API.Functions);
                var get=functions.GetMethods(flags).FirstOrDefault(m=>m.Name=="GetActivePursuit"&&m.GetParameters().Length==0);
                if(get==null)return false;
                object pursuit=get.Invoke(null,null);
                if(pursuit==null||pursuit.Equals(Activator.CreateInstance(pursuit.GetType())))
                {detail="No active player pursuit.";return true;}
                var running=functions.GetMethod("IsPursuitStillRunning",flags,null,new[]{pursuit.GetType()},null);
                var members=functions.GetMethod("GetPursuitPeds",flags,null,new[]{pursuit.GetType()},null);
                if(running==null||members==null)return false;
                if(!(bool)running.Invoke(null,new[]{pursuit})){detail="Pursuit already ended.";return true;}
                var roster=members.Invoke(null,new[]{pursuit}) as IEnumerable<Ped>;
                if(roster==null)return false;
                var suspects=roster.ToArray();
                if(!suspects.Any(p=>p==target)){detail="Patient is not a member of the active pursuit.";return true;}
                var remove=functions.GetMethod("RemovePedFromPursuit",flags,null,new[]{pursuit.GetType(),typeof(Ped)},null);
                if(remove!=null)
                {
                    remove.Invoke(null,new[]{pursuit,(object)target});
                    if(!(bool)running.Invoke(null,new[]{pursuit})||
                        (members.Invoke(null,new[]{pursuit}) is IEnumerable<Ped> remaining&&!remaining.Any(p=>p==target)))
                    {detail="EMS patient removed from pursuit; other members preserved.";return true;}
                }
                if(suspects.Length==1)
                {
                    var end=functions.GetMethod("ForceEndPursuit",flags,null,new[]{pursuit.GetType()},null);
                    if(end==null)return false;
                    end.Invoke(null,new[]{pursuit});
                    bool ended=!(bool)running.Invoke(null,new[]{pursuit});
                    detail=ended?"Single-suspect pursuit ended at medical handoff.":"Single-suspect pursuit ending; awaiting confirmation.";
                    return ended;
                }
                detail="Patient remains in multi-suspect pursuit; no supported per-suspect removal. Other suspects were not cleared.";
                return false;
            }
            catch(Exception ex){detail="Pursuit transition failed: "+ex.GetType().Name;return false;}
        }

        private static string K9ProviderStates()=>string.Join(";",WatchedSuspects.Values.Select(w=>w.Identity.Handle+"~"+w.Identity.Model+"~"+w.Identity.Address+"~"+w.State));
    }
}
