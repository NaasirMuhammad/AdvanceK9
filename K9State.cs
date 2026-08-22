namespace AdvancedK9
{
    internal enum K9State { Dismissed, Following, Heeling, Sitting, Lying, Staying, Guarding, Searching, Tracking, Apprehending, Fetching, InVehicle, Leashed, Academy, Injured }
    internal enum DetectionSpecialty { General, Narcotics, Explosives, Weapons }
    internal enum K9Command { SpawnDismiss, Follow, Heel, Sit, LieDown, Stay, Recall, Fetch, SearchArea, SearchVehicle, SearchNarcotics, SearchExplosives, SearchWeapons, Track, Apprehend, Release, Guard, Bark, EnterVehicle, ExitVehicle, Pet, Feed, Drink, Inspect, FirstAid, ToggleLeash, ToggleCamera, Training, TrainNarcotics, TrainExplosives, TrainWeapons }
}
