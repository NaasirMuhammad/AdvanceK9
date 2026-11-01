using System;
using System.IO;
using AdvancedK9.API;

static class SuspectControlTests
{
    static void Check(bool condition,string why){if(!condition)throw new Exception(why);}
    static void Main()
    {
        var state=new SuspectControlState();
        Check(state.Set(SuspectControlPhase.Bite)&&state.CanWritePose,"Contact acquires pose control.");
        Check(state.Set(SuspectControlPhase.MedicalHold)&&state.CanWritePose,"Release retains medical ground hold.");
        Check(state.Set(SuspectControlPhase.Treatment)&&!state.CanWritePose,"EMS treatment cannot replay ground/standing animations.");
        Check(state.Set(SuspectControlPhase.ProviderInteraction)&&state.ProviderOwns,"A real external stop hands over control.");
        Check(!state.Set(SuspectControlPhase.MedicalHold)&&!state.Set(SuspectControlPhase.Treatment),"Stale medical fibers must not reacquire a provider-owned patient.");
        Check(state.Set(SuspectControlPhase.Arresting),"Actual arrest upgrades interaction.");
        Check(!state.Set(SuspectControlPhase.ProviderInteraction),"An older stop observation cannot downgrade an arrest.");
        Check(state.Set(SuspectControlPhase.Cuffed),"Observed restraint completes the arrest stage.");
        Check(!state.Set(SuspectControlPhase.Arresting)&&!state.Set(SuspectControlPhase.Bite),"Completed restraint cannot regress into bite/arrest animation tasking.");
        Check(state.Set(SuspectControlPhase.Unavailable),"Deleted/replaced entities retire the session.");
        Check(!state.Set(SuspectControlPhase.MedicalHold),"A retired entity cannot be reactivated by stale work.");
        var released=new SuspectControlState();released.Set(SuspectControlPhase.Released);
        Check(!released.Set(SuspectControlPhase.Bite),"A cancelled chase requires a new explicit session, not an old contact callback.");
        string original=Directory.GetCurrentDirectory(),temp=Path.Combine(Path.GetTempPath(),"AdvancedK9-control-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);Directory.SetCurrentDirectory(temp);
        try
        {
            var first=new SuspectControlLease{Handle=123,Model=456,Address=789,Phase=SuspectControlPhase.MedicalHold,Episode="case-one"};
            SuspectControlApi.Publish(first);SuspectControlLease lease;
            Check(SuspectControlApi.TryRead(123,456,789,out lease)&&lease.BlocksCalloutTasks&&lease.MedicalOwner,"Callout must pause the exact bite patient's tasks and duplicate EMS.");
            Check(!SuspectControlApi.TryRead(124,456,789,out lease),"Unrelated suspect must remain independently controlled.");
            Check(!SuspectControlApi.TryRead(123,457,789,out lease)&&!SuspectControlApi.TryRead(123,456,790,out lease),"Recycled handle/model or address cannot inherit an older patient's lease.");
            first.Phase=SuspectControlPhase.Released;SuspectControlApi.Publish(first);
            Check(SuspectControlApi.TryRead(123,456,789,out lease)&&!lease.BlocksCalloutTasks,"Cancelled pre-contact chase releases cooperating callout tasking.");
            string path=Path.Combine("Plugins","LSPDFR","AdvancedK9","SuspectControl","123.state");
            string[] lines=File.ReadAllLines(path);lines[3]=DateTime.UtcNow.AddSeconds(-10).Ticks.ToString();File.WriteAllLines(path,lines);
            Check(!SuspectControlApi.TryRead(123,456,789,out lease),"A stopped/crashed core cannot retain callout control indefinitely.");
        }
        finally{Directory.SetCurrentDirectory(original);Directory.Delete(temp,true);}
        Console.WriteLine("Per-suspect ownership, stale-fiber handoff, recycled-entity identity and lease expiry regressions passed.");
    }
}
