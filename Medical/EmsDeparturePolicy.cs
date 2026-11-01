namespace AdvancedK9.Medical
{
    // Shared by the runtime and regression replay; hospital need survives
    // equipment/boarding failures and scene-clear requests.
    internal static class EmsDeparturePolicy
    {
        internal static bool NeedsHospital(bool previouslyRequired,int healthBeforeTreatment,int roll,int transportChance)
            => previouslyRequired || healthBeforeTreatment < 60 || roll < transportChance;

        internal static bool CanDepart(bool driverInAssignedSeat,bool partnerAvailable,bool partnerInAssignedSeat,bool hospital,bool patientLoaded)
            => driverInAssignedSeat && (!partnerAvailable || partnerInAssignedSeat) && (!hospital || patientLoaded);

    }
}
