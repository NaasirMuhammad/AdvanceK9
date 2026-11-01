using System;
using AdvancedK9.Medical;

static class EmsDepartureTests
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static void Main()
    {
        Check(EmsDeparturePolicy.NeedsHospital(true,100,99,0),"A failed transport must not lose hospital need on reassessment.");
        Check(EmsDeparturePolicy.NeedsHospital(false,59,99,0),"Critical health requires hospital regardless of optional transport chance.");
        Check(!EmsDeparturePolicy.NeedsHospital(false,75,99,30),"Ordinary on-scene clearance remains available.");
        Check(EmsDeparturePolicy.NeedsHospital(false,75,0,30),"Configured transport assignment remains available.");
        Check(!EmsDeparturePolicy.CanDepart(true,true,false,true,true),"Driver must wait for available partner to board.");
        Check(!EmsDeparturePolicy.CanDepart(true,true,true,true,false),"Crew cannot depart without hospital patient.");
        Check(!EmsDeparturePolicy.CanDepart(false,true,true,false,true),"Wrong/missing driver seat cannot pass boarding.");
        Check(EmsDeparturePolicy.CanDepart(true,true,true,true,true),"Loaded hospital crew can depart.");
        Check(EmsDeparturePolicy.CanDepart(true,false,false,false,false),"A missing partner must not strand an empty ambulance with a valid driver.");
        var signal=new NexusSceneClearSignal();
        string prefix="[11/1/2026 1:36:34 AM.052] LSPD First Response: NexusDispatch: ";
        Check(!signal.Feed(prefix+"[DispatchPipeline] Transcript: \"Code four\"\n"),"Transcript alone must not dismiss EMS.");
        Check(!signal.Feed(prefix+"[IntentClient] Intent → [code4, available]\n"),"AI intent alone must not dismiss EMS.");
        Check(!signal.Feed(prefix+"[DispatchPipeline] Firing action: code"),"A split line must wait for completion.");
        Check(signal.Feed("4\r\n"),"Actual Code 4 action must survive chunk boundaries.");
        Check(!signal.Feed("Advanced K9: NexusDispatch: [DispatchPipeline] Firing action: code4\n"),"Other plugin text must not trigger the Nexus signal.");
        Check(signal.Feed(prefix+"[BackupLifecycle] Nexus requested global backup dismissal: Code4; incident=\r\n"),"Observed backup dismissal must reach AdvancedK9 responders.");
        signal.Feed(prefix+"[DispatchPipeline] Firing action: code");signal.Reset();
        Check(!signal.Feed("4\n"),"Log rollover cannot combine an old partial command with fresh text.");
        Console.WriteLine("EMS hospital disposition, assigned-seat departure and Nexus Code 4 regressions passed.");
    }
}
