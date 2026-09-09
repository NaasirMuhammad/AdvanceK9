namespace AdvancedK9
{
    internal enum K9State { Dismissed, Following, Heeling, Sitting, Lying, Staying, Guarding, Containing, Searching, Tracking, Apprehending, HoldingSuspect, MedicalStandby, Fetching, InVehicle, Leashed, Academy, Injured }
    internal enum DetectionSpecialty { General, Narcotics, Explosives, Weapons }
    internal enum K9Command { SpawnDismiss, Follow, Heel, Sit, LieDown, Stay, Recall, WhistleRecall, HandSignal, Fetch, SearchArea, SearchBuilding, SearchVehicle, SearchNarcotics, SearchExplosives, SearchWeapons, ClearEvidenceMarkers, AssignScent, CollectScent, Track, FindTrail, K9Warning, Apprehend, HandoffArrest, RequestPerimeter, HoldPerimeter, ContainSuspect, SaveContainmentPosition, SendContainmentPosition, NextContainmentPosition, ClearContainmentPosition, RequestTransport, RequestMedical, RequestBombSquad, DoorPop, Release, Guard, Bark, EnterVehicle, ExitVehicle, Pet, Feed, Drink, Rest, Inspect, FirstAid, CarryK9, EmergencyLoadK9, VeterinaryTransport, Rehabilitation, VeterinaryCare, Restock, ToggleLeash, ToggleCamera, Training, TrainNarcotics, TrainExplosives, TrainWeapons }
}
