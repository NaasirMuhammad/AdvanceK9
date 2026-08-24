namespace AdvancedK9
{
    internal enum K9State { Dismissed, Following, Heeling, Sitting, Lying, Staying, Guarding, Searching, Tracking, Apprehending, Fetching, InVehicle, Leashed, Academy, Injured }
    internal enum DetectionSpecialty { General, Narcotics, Explosives, Weapons }
    internal enum K9Command { SpawnDismiss, Follow, Heel, Sit, LieDown, Stay, Recall, WhistleRecall, HandSignal, Fetch, SearchArea, SearchBuilding, SearchVehicle, SearchNarcotics, SearchExplosives, SearchWeapons, CollectScent, Track, FindTrail, K9Warning, Apprehend, HandoffArrest, RequestPerimeter, RequestTransport, RequestMedical, RequestBombSquad, DoorPop, Release, Guard, Bark, EnterVehicle, ExitVehicle, Pet, Feed, Drink, Bathroom, Rest, Inspect, FirstAid, VeterinaryCare, Restock, ToggleLeash, ToggleCamera, Training, TrainNarcotics, TrainExplosives, TrainWeapons }
}
