namespace AdvancedK9.LSPDFRBridge
{
    internal static class BiteCustodyPolicy
    {
        internal static bool IsTerminal(string phase)=>phase=="Cuffed"||phase=="Released";
        internal static bool IsHeld(string phase)=>phase=="BiteHold"||phase=="MedicalHold"||phase=="TreatedAwaitingArrest";
        internal static bool RegisterStop(string phase)=>phase=="TreatedAwaitingArrest";
    }
}
