using System;
using AdvancedK9.LSPDFRBridge;

internal static class BiteCustodyPolicyTests
{
    private static void Check(bool result,string message)
    {
        if(!result)throw new Exception(message);
    }

    public static void Main()
    {
        // Regression: repeatedly registering a physical patient as a stopped ped
        // made PR immediately release that state and resume the pursuit.
        foreach(var physical in new[]{"BiteHold","MedicalHold"})
        {
            Check(BiteCustodyPolicy.IsHeld(physical),physical+" must retain custody");
            Check(!BiteCustodyPolicy.RegisterStop(physical),physical+" must not manufacture a stop");
            Check(!BiteCustodyPolicy.IsTerminal(physical),physical+" must not restore pursuit AI");
        }
        foreach(var finalPhase in new[]{"Cuffed","Released"})
        {
            Check(BiteCustodyPolicy.IsTerminal(finalPhase),finalPhase+" must restore temporary controls");
            Check(!BiteCustodyPolicy.IsHeld(finalPhase),finalPhase+" must not reacquire custody");
            Check(!BiteCustodyPolicy.RegisterStop(finalPhase),finalPhase+" must not register a new stop");
        }
        Check(BiteCustodyPolicy.IsHeld("TreatedAwaitingArrest"),"Treatment must retain compliance pending real cuffs");
        Check(BiteCustodyPolicy.RegisterStop("TreatedAwaitingArrest"),"Only treated compliance may request a stop");
        foreach(var invalid in new string[]{null,"","Arrested","Unknown"})
            Check(!BiteCustodyPolicy.IsHeld(invalid)&&!BiteCustodyPolicy.IsTerminal(invalid)&&!BiteCustodyPolicy.RegisterStop(invalid),"Unknown phase must not acquire or fabricate custody");
        Console.WriteLine("Bite custody regression checks passed (physical hold, treatment, cuffs, release, invalid phases).");
    }
}
