using System;
using System.IO;
using Rage;

namespace AdvancedK9
{
    internal sealed class K9Profile
    {
        private static readonly string[] Breeds = { "German Shepherd", "Belgian Malinois", "Dalmatian", "Rottweiler", "Husky", "Retriever", "Poodle", "Pug", "Westie", "Doberman", "Cane Corso" };
        private static readonly string[] Models = { "a_c_shepherd", "a_c_shepherd", "a_c_dalmatian", "a_c_rottweiler", "a_c_husky", "a_c_retriever", "a_c_poodle", "a_c_pug", "a_c_westy", "doberman", "a_c_chop" };
        private static readonly string[] Vests = { "None", "Police", "BCSO/Sheriff", "Trooper", "Fire", "Off-duty", "Service", "Medic", "Black", "Blue", "High Visibility", "Custom" };
        private static readonly string[] VestTextures = { "Multicam", "Tan", "Black", "Green", "Grey", "Blue", "Yellow", "Red", "Orange" };
        private readonly string _path = Path.Combine("Plugins", "LSPDFR", "AdvancedK9", "profile.dat");

        public string Name { get; private set; }
        public int BreedIndex { get; private set; }
        public int CoatVariation { get; private set; }
        public int VestIndex { get; private set; }
        public int VestTexture { get; private set; }
        public string CustomModel { get; private set; }
        public int VestComponent { get; private set; }
        public int TrainingXp { get; private set; }
        public int Trust { get; private set; } = 40;
        public int Health { get; private set; } = 100;
        public int Stamina { get; private set; } = 100;
        public string Injury { get; private set; } = "None";
        public bool ObedienceCertified { get; private set; }
        public bool AgilityCertified { get; private set; }
        public bool DetectionCertified { get; private set; }
        public bool TrackingCertified { get; private set; }
        public bool ApprehensionCertified { get; private set; }
        public bool NarcoticsCertified { get; private set; }
        public bool ExplosivesCertified { get; private set; }
        public bool WeaponsCertified { get; private set; }
        public int NarcoticsProgress { get; private set; }
        public int ExplosivesProgress { get; private set; }
        public int WeaponsProgress { get; private set; }
        public int Food { get; private set; } = 100;
        public int Water { get; private set; } = 100;
        public int FoodMeals { get; private set; } = 4;
        public int WaterBottles { get; private set; } = 4;
        public int FirstAidKits { get; private set; } = 2;
        public int ScentBags { get; private set; } = 5;
        public int Treats { get; private set; } = 6;
        public int TrainingLevel { get; private set; } = 1;
        public int TrainingLevelProgress { get; private set; }
        public int Deployments { get; private set; }
        public int SuccessfulSearches { get; private set; }
        public float HudX { get; private set; } = .975f;
        public float HudY { get; private set; } = .955f;
        public float HudScale { get; private set; } = .85f;
        public float HudOpacity { get; private set; } = .90f;
        public float HudCollapsedScale { get; private set; } = .70f;
        public bool HudAutoCollapse { get; private set; } = true;
        public bool HudShowPortrait { get; private set; } = true;
        public bool HudShowDistance { get; private set; } = true;
        public bool HudShowCommand { get; private set; } = true;
        public bool HudShowBehavior { get; private set; } = true;
        public bool HudSearchProgress { get; private set; } = true;
        public bool HudMetricDistance { get; private set; } = true;
        public int HudAlertDuration { get; private set; } = 4500;
        public string PortraitFile { get; private set; } = "";
        public int HudMode { get; private set; } = 1;
        public int HudDesign { get; private set; }
        public int HudIconSet { get; private set; }
        public int HudColorTheme { get; private set; }
        public int HudTextStyle { get; private set; }
        public bool HudShowState { get; private set; } = true;
        public bool HudShowHealth { get; private set; } = true;
        public bool HudShowStamina { get; private set; } = true;
        public bool HudShowFood { get; private set; }
        public bool HudShowWater { get; private set; }
        public bool HudShowCertifications { get; private set; }
        public bool HudShowTrust { get; private set; }
        public bool HudShowTraining { get; private set; }
        public bool HudShowInjury { get; private set; }
        public bool HudShowVoice { get; private set; }

        public string Breed => Breeds[BreedIndex];
        public string ModelName => !string.IsNullOrWhiteSpace(CustomModel) && VestIndex == 11 ? CustomModel : Models[BreedIndex];
        public string FallbackModelName => Breed == "Dalmatian" ? "a_c_husky" : Breed == "Doberman" ? "a_c_retriever" : null;
        public string Vest => Vests[VestIndex];
        public string VestTextureName(Ped dog){int count=GetVestTextureCount(dog);if(count<=0)return "Unavailable";return VestTexture<VestTextures.Length?VestTextures[VestTexture]:"Texture "+(VestTexture+1);}

        public K9Profile(ModConfig config)
        {
            Name = config.DogName;
            BreedIndex = FindBreed(config.DogBreed);
            CoatVariation = Clamp(config.CoatVariation, 0, 7);
            VestIndex = Clamp(config.VestStyle, 0, Vests.Length - 1);
            VestTexture = Clamp(config.VestColor, 0, 15);
            CustomModel = config.CustomDogModel;
            VestComponent = config.VestComponent;
            PortraitFile = config.PortraitFile;
            Load();
            LoadSpecialtyProgress();
            LoadNeeds();
            LoadOperationalData();
            LoadHudPreferences();
            MigrateTrainingProgress();
        }

        public void NextBreed() { BreedIndex = (BreedIndex + 1) % Breeds.Length; Save(); }
        public void NextCoat() { CoatVariation = (CoatVariation + 1) % 8; Save(); }
        public void NextVest() { VestIndex = (VestIndex + 1) % Vests.Length; Save(); }
        public void NextVestColor() { VestTexture = (VestTexture + 1) % 16; Save(); }
        public void NextSkin(Ped dog){int count=dog!=null&&dog.Exists()?Rage.Native.NativeFunction.Natives.GET_NUMBER_OF_PED_TEXTURE_VARIATIONS<int>(dog,0,0):8;CoatVariation=(CoatVariation+1)%Math.Max(1,count);Save();Apply(dog);}
        public void NextEquipment(Ped dog){int component=dog!=null&&dog.Exists()?GetVestComponent(dog):VestComponent;int count=IsDalmatian?8:(dog!=null&&dog.Exists()?Rage.Native.NativeFunction.Natives.GET_NUMBER_OF_PED_DRAWABLE_VARIATIONS<int>(dog,component):Vests.Length);VestIndex=(VestIndex+1)%Math.Max(1,Math.Min(count,Vests.Length));Save();Apply(dog);}
        public void NextEquipmentTexture(Ped dog){int count=GetVestTextureCount(dog);VestTexture=(VestTexture+1)%Math.Max(1,count);Save();Apply(dog);}
        public void AdjustBreed(int delta){BreedIndex=Wrap(BreedIndex+delta,Breeds.Length);Save();}
        public void AdjustSkin(Ped dog,int delta){int count=dog!=null&&dog.Exists()?Rage.Native.NativeFunction.Natives.GET_NUMBER_OF_PED_TEXTURE_VARIATIONS<int>(dog,0,0):8;CoatVariation=Wrap(CoatVariation+delta,Math.Max(1,count));Save();Apply(dog);}
        public void AdjustEquipment(Ped dog,int delta){int component=dog!=null&&dog.Exists()?GetVestComponent(dog):VestComponent;int count=IsDalmatian?8:(dog!=null&&dog.Exists()?Rage.Native.NativeFunction.Natives.GET_NUMBER_OF_PED_DRAWABLE_VARIATIONS<int>(dog,component):Vests.Length);VestIndex=Wrap(VestIndex+delta,Math.Max(1,Math.Min(count,Vests.Length)));Save();Apply(dog);}
        public void AdjustEquipmentTexture(Ped dog,int delta){int count=GetVestTextureCount(dog);VestTexture=Wrap(VestTexture+delta,Math.Max(1,count));Save();Apply(dog);}
        public void PrepareDeployment(){if(Health<=25){Health=100;Stamina=100;Injury="None";Save();}}
        public void SetName(string value) { if (!string.IsNullOrWhiteSpace(value)) { Name=value.Trim(); Save(); } }
        public void AddXp(int value) { TrainingXp=Math.Max(0,TrainingXp+value); Save(); }
        public bool ApplyTrainingProgress(int level,int points){if(level!=TrainingLevel||TrainingLevelProgress>=100)return false;TrainingLevelProgress=Clamp(TrainingLevelProgress+Math.Max(0,points),0,100);TrainingXp+=Math.Max(0,points/4);bool completed=TrainingLevelProgress>=100;if(completed){if(level==1)ObedienceCertified=true;else if(level==2)AgilityCertified=true;else if(level==3)DetectionCertified=true;else if(level==4)TrackingCertified=true;else if(level==5)ApprehensionCertified=true;if(level<5){TrainingLevel++;TrainingLevelProgress=0;}}Save();return completed;}
        public string CurrentTrainingName=>TrainingLevel==1?"Basic Obedience":TrainingLevel==2?"Agility / Handler Control":TrainingLevel==3?"Detection":TrainingLevel==4?"Tracking":"Apprehension";
        public int SpecialtyProgress(DetectionSpecialty specialty)=>specialty==DetectionSpecialty.Narcotics?NarcoticsProgress:specialty==DetectionSpecialty.Explosives?ExplosivesProgress:specialty==DetectionSpecialty.Weapons?WeaponsProgress:0;
        public bool HasSpecialty(DetectionSpecialty specialty)=>specialty==DetectionSpecialty.Narcotics?NarcoticsCertified:specialty==DetectionSpecialty.Explosives?ExplosivesCertified:specialty==DetectionSpecialty.Weapons?WeaponsCertified:DetectionCertified;
        public bool ApplySpecialtyProgress(DetectionSpecialty specialty,int points){if(!DetectionCertified)return false;int value=Clamp(SpecialtyProgress(specialty)+Math.Max(0,points),0,100);if(specialty==DetectionSpecialty.Narcotics){NarcoticsProgress=value;NarcoticsCertified=value>=100;}else if(specialty==DetectionSpecialty.Explosives){ExplosivesProgress=value;ExplosivesCertified=value>=100;}else if(specialty==DetectionSpecialty.Weapons){WeaponsProgress=value;WeaponsCertified=value>=100;}TrainingXp+=Math.Max(0,points/4);Save();return value>=100;}
        public void ChangeTrust(int value){Trust=Clamp(Trust+value,0,100);Save();}
        public void UseStamina(int value){Stamina=Clamp(Stamina-value,0,100);if(Stamina<15)ChangeTrust(-1);Save();}
        public void Recover(int value){Stamina=Clamp(Stamina+value,0,100);Health=Clamp(Health+value/2,0,100);Save();}
        public void UseNeeds(int food,int water){Food=Clamp(Food-Math.Max(0,food),0,100);Water=Clamp(Water-Math.Max(0,water),0,100);if(Food<20||Water<20)Stamina=Clamp(Stamina-2,0,100);Save();}
        public bool FeedMeal(){if(FoodMeals<=0)return false;FoodMeals--;Food=100;Stamina=Clamp(Stamina+12,0,100);Save();return true;}
        public bool GiveWater(){if(WaterBottles<=0)return false;WaterBottles--;Water=100;Stamina=Clamp(Stamina+10,0,100);Save();return true;}
        public bool UseFirstAid(){if(FirstAidKits<=0)return false;FirstAidKits--;FirstAid();return true;}
        public bool UseScentBag(){if(ScentBags<=0)return false;ScentBags--;Save();return true;}
        public void Restock(){FoodMeals=4;WaterBottles=4;FirstAidKits=2;ScentBags=5;Treats=6;Save();}
        public void Rest(){Stamina=100;Food=Clamp(Food-4,0,100);Water=Clamp(Water-6,0,100);Save();}
        public void VeterinaryTreat(){Health=100;Stamina=90;Injury="None";Save();}
        public double NeedsFactor=>Math.Max(.35,Math.Min(1.0,(Food/100.0*.45)+(Water/100.0*.55)));
        public void RecordDeployment(){Deployments++;Save();} public void RecordSearch(){SuccessfulSearches++;AddXp(3);}
        public void SetInjury(string injury,int health){Injury=injury??"None";Health=Clamp(health,0,100);Save();}
        public void FirstAid(){Health=Clamp(Health+20,0,100);if(Health>=70)Injury="Minor/treated";Save();}
        public void CycleHudMode(){HudMode=(HudMode+1)%3;Save();}
        public void MoveHud(float x,float y){HudX=Math.Max(.12f,Math.Min(.985f,HudX+x));HudY=Math.Max(.08f,Math.Min(.975f,HudY+y));Save();}
        public void MoveHudPreview(float x,float y){HudX=Math.Max(.12f,Math.Min(.985f,HudX+x));HudY=Math.Max(.08f,Math.Min(.975f,HudY+y));}
        public void ScaleHud(){AdjustHudScale(.05f);}
        public void AdjustHudScale(float delta){HudScale=Math.Max(.55f,Math.Min(1.35f,HudScale+delta));Save();}
        public void AdjustHudScalePreview(float delta){HudScale=Math.Max(.55f,Math.Min(1.35f,HudScale+delta));}
        public void SaveHudLayout(){Save();}
        public void AdjustHudOpacity(float delta){HudOpacity=Math.Max(.35f,Math.Min(1f,HudOpacity+delta));Save();}
        public void ResetHud(){HudX=.975f;HudY=.955f;HudScale=.85f;HudOpacity=.90f;HudDesign=0;HudIconSet=0;HudColorTheme=0;HudTextStyle=0;HudMode=1;HudShowPortrait=true;HudShowState=true;HudShowHealth=true;HudShowStamina=true;HudShowDistance=true;HudShowCommand=true;HudShowBehavior=true;HudAutoCollapse=true;HudSearchProgress=true;HudShowFood=false;HudShowWater=false;HudShowCertifications=false;HudShowTrust=false;HudShowTraining=false;HudShowInjury=false;HudShowVoice=false;Save();}
        public void ToggleHudOption(int option){switch(option){case 0:HudAutoCollapse=!HudAutoCollapse;break;case 1:HudShowPortrait=!HudShowPortrait;break;case 2:HudShowDistance=!HudShowDistance;break;case 3:HudShowCommand=!HudShowCommand;break;case 4:HudShowBehavior=!HudShowBehavior;break;case 5:HudSearchProgress=!HudSearchProgress;break;case 6:HudMetricDistance=!HudMetricDistance;break;}Save();}
        public void AdjustHudDesign(int delta){HudDesign=Wrap(HudDesign+delta,5);Save();}
        public void AdjustHudIcons(int delta){HudIconSet=Wrap(HudIconSet+delta,5);Save();}
        public void AdjustHudColor(int delta){HudColorTheme=Wrap(HudColorTheme+delta,5);Save();}
        public void AdjustHudText(int delta){HudTextStyle=Wrap(HudTextStyle+delta,5);Save();}
        public void ToggleHudField(int field){switch(field){case 0:HudShowState=!HudShowState;break;case 1:HudShowHealth=!HudShowHealth;break;case 2:HudShowStamina=!HudShowStamina;break;case 3:HudShowFood=!HudShowFood;break;case 4:HudShowWater=!HudShowWater;break;case 5:HudShowCertifications=!HudShowCertifications;break;case 6:HudShowTrust=!HudShowTrust;break;case 7:HudShowTraining=!HudShowTraining;break;case 8:HudShowInjury=!HudShowInjury;break;case 9:HudShowVoice=!HudShowVoice;break;}Save();}

        public void Apply(Ped dog)
        {
            if (dog == null || !dog.Exists()) return;
            int coatCount = Rage.Native.NativeFunction.Natives.GET_NUMBER_OF_PED_TEXTURE_VARIATIONS<int>(dog, 0, 0);
            int coat = coatCount > 0 ? Math.Min(CoatVariation, coatCount - 1) : 0;
            Rage.Native.NativeFunction.Natives.SET_PED_COMPONENT_VARIATION(dog, 0, 0, coat, 2);
            int component = GetVestComponent(dog);
            int drawableCount = Rage.Native.NativeFunction.Natives.GET_NUMBER_OF_PED_DRAWABLE_VARIATIONS<int>(dog, component);
            // Bug's Mods Dalmatian and its a_c_husky replacement keep the seven
            // hand_diff vest variants as textures on drawable 0. Drawable 1 is
            // the unvested body, so these variants must not be cycled as drawables.
            int drawable = IsDalmatian ? (VestIndex == 0 && drawableCount > 1 ? 1 : 0) :
                (VestIndex == 0 || drawableCount <= 1 ? 0 : Math.Min(VestIndex, drawableCount - 1));
            int textureCount = Rage.Native.NativeFunction.Natives.GET_NUMBER_OF_PED_TEXTURE_VARIATIONS<int>(dog, component, drawable);
            int texture = IsDalmatian && VestIndex > 0 ? Math.Min(VestIndex - 1, Math.Max(0, textureCount - 1)) :
                (textureCount > 0 ? Math.Min(VestTexture, textureCount - 1) : 0);
            Rage.Native.NativeFunction.Natives.SET_PED_COMPONENT_VARIATION(dog, component, drawable, texture, 2);
        }

        public string Summary => Name + " — " + Breed + ", skin " + (CoatVariation + 1) + ", equipment " + Vest + " / texture " + (VestTexture + 1);

        public void Save()
        {
            try
            {
                var directory = Path.GetDirectoryName(_path);
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
                File.WriteAllLines(_path, new[]{"Version=9","Name="+Name,"Breed="+BreedIndex,"Skin="+CoatVariation,"Equipment="+VestIndex,"Texture="+VestTexture,"XP="+TrainingXp,"Trust="+Trust,"Health="+Health,"Stamina="+Stamina,"Injury="+Injury,"Food="+Food,"Water="+Water,"FoodMeals="+FoodMeals,"WaterBottles="+WaterBottles,"FirstAidKits="+FirstAidKits,"ScentBags="+ScentBags,"Treats="+Treats,"TrainingLevel="+TrainingLevel,"TrainingProgress="+TrainingLevelProgress,"CertObedience="+ObedienceCertified,"CertAgility="+AgilityCertified,"CertDetection="+DetectionCertified,"CertTracking="+TrackingCertified,"CertApprehension="+ApprehensionCertified,"NarcoticsProgress="+NarcoticsProgress,"ExplosivesProgress="+ExplosivesProgress,"WeaponsProgress="+WeaponsProgress,"CertNarcotics="+NarcoticsCertified,"CertExplosives="+ExplosivesCertified,"CertWeapons="+WeaponsCertified,"Deployments="+Deployments,"Searches="+SuccessfulSearches,"HudX="+HudX,"HudY="+HudY,"HudScale="+HudScale,"HudOpacity="+HudOpacity,"HudCollapsedScale="+HudCollapsedScale,"HudAutoCollapse="+HudAutoCollapse,"HudShowPortrait="+HudShowPortrait,"HudShowDistance="+HudShowDistance,"HudShowCommand="+HudShowCommand,"HudShowBehavior="+HudShowBehavior,"HudSearchProgress="+HudSearchProgress,"HudMetricDistance="+HudMetricDistance,"HudAlertDuration="+HudAlertDuration,"PortraitFile="+PortraitFile,"HudMode="+HudMode,"HudDesign="+HudDesign,"HudIconSet="+HudIconSet,"HudColorTheme="+HudColorTheme,"HudTextStyle="+HudTextStyle,"HudShowState="+HudShowState,"HudShowHealth="+HudShowHealth,"HudShowStamina="+HudShowStamina,"HudShowFood="+HudShowFood,"HudShowWater="+HudShowWater,"HudShowCertifications="+HudShowCertifications,"HudShowTrust="+HudShowTrust,"HudShowTraining="+HudShowTraining,"HudShowInjury="+HudShowInjury,"HudShowVoice="+HudShowVoice});
            }
            catch (Exception ex) { Game.LogTrivial("AdvancedK9 profile save: " + ex.Message); }
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(_path)) return;
                var values = File.ReadAllLines(_path);
                if(values.Length>0 && values[0].StartsWith("Version=")) { foreach(string line in values){int split=line.IndexOf('=');if(split<1)continue;string k=line.Substring(0,split),v=line.Substring(split+1);int n;float f;bool flag;switch(k){case "Name":if(!string.IsNullOrWhiteSpace(v))Name=v;break;case "Breed":if(int.TryParse(v,out n))BreedIndex=Clamp(n,0,Breeds.Length-1);break;case "Skin":if(int.TryParse(v,out n))CoatVariation=Math.Max(0,n);break;case "Equipment":if(int.TryParse(v,out n))VestIndex=Clamp(n,0,Vests.Length-1);break;case "Texture":if(int.TryParse(v,out n))VestTexture=Math.Max(0,n);break;case "XP":if(int.TryParse(v,out n))TrainingXp=Math.Max(0,n);break;case "Trust":if(int.TryParse(v,out n))Trust=Clamp(n,0,100);break;case "Health":if(int.TryParse(v,out n))Health=Clamp(n,0,100);break;case "Stamina":if(int.TryParse(v,out n))Stamina=Clamp(n,0,100);break;case "Injury":Injury=v;break;case "TrainingLevel":if(int.TryParse(v,out n))TrainingLevel=Clamp(n,1,5);break;case "TrainingProgress":if(int.TryParse(v,out n))TrainingLevelProgress=Clamp(n,0,100);break;case "CertObedience":if(bool.TryParse(v,out flag))ObedienceCertified=flag;break;case "CertAgility":if(bool.TryParse(v,out flag))AgilityCertified=flag;break;case "CertDetection":if(bool.TryParse(v,out flag))DetectionCertified=flag;break;case "CertTracking":if(bool.TryParse(v,out flag))TrackingCertified=flag;break;case "CertApprehension":if(bool.TryParse(v,out flag))ApprehensionCertified=flag;break;case "Deployments":if(int.TryParse(v,out n))Deployments=n;break;case "Searches":if(int.TryParse(v,out n))SuccessfulSearches=n;break;case "HudX":if(float.TryParse(v,out f))HudX=f;break;case "HudY":if(float.TryParse(v,out f))HudY=f;break;case "HudScale":if(float.TryParse(v,out f))HudScale=f;break;case "HudMode":if(int.TryParse(v,out n))HudMode=Clamp(n,0,2);break;}} return; }
                int number;
                // Name remains sourced from AdvancedK9.ini so renaming does not
                // require deleting the persisted appearance profile.
                if (values.Length > 1 && int.TryParse(values[1], out number)) BreedIndex = Clamp(number, 0, Breeds.Length - 1);
                if (values.Length > 2 && int.TryParse(values[2], out number)) CoatVariation = Clamp(number, 0, 7);
                if (values.Length > 3 && int.TryParse(values[3], out number)) VestIndex = Clamp(number, 0, Vests.Length - 1);
                if (values.Length > 4 && int.TryParse(values[4], out number)) VestTexture = Clamp(number, 0, 15);
                if (values.Length < 6)
                {
                    // Migrate v1.3 indices after Malinois and Dalmatian presets were inserted.
                    if (BreedIndex > 0) BreedIndex = Math.Min(BreedIndex + 2, Breeds.Length - 1);
                    if (VestIndex == 1) VestIndex = 8;
                    else if (VestIndex == 2) VestIndex = 9;
                    else if (VestIndex == 3) VestIndex = 10;
                    else if (VestIndex == 4) VestIndex = 11;
                    Save();
                }
            }
            catch (Exception ex) { Game.LogTrivial("AdvancedK9 profile load: " + ex.Message); }
        }

        private static int FindBreed(string value)
        {
            for (int i = 0; i < Breeds.Length; i++) if (Breeds[i].Equals(value, StringComparison.OrdinalIgnoreCase)) return i;
            return 0;
        }

        private void MigrateTrainingProgress(){if(TrainingLevel!=1||TrainingLevelProgress!=0)return;if(ApprehensionCertified){TrainingLevel=5;TrainingLevelProgress=100;AgilityCertified=true;}else if(TrackingCertified){TrainingLevel=5;AgilityCertified=true;}else if(DetectionCertified){TrainingLevel=4;AgilityCertified=true;}else if(ObedienceCertified){TrainingLevel=2;}Save();}
        private void LoadSpecialtyProgress(){try{if(!File.Exists(_path))return;foreach(var line in File.ReadAllLines(_path)){int split=line.IndexOf('=');if(split<1)continue;string key=line.Substring(0,split),value=line.Substring(split+1);int number;bool flag;if(key=="NarcoticsProgress"&&int.TryParse(value,out number))NarcoticsProgress=Clamp(number,0,100);else if(key=="ExplosivesProgress"&&int.TryParse(value,out number))ExplosivesProgress=Clamp(number,0,100);else if(key=="WeaponsProgress"&&int.TryParse(value,out number))WeaponsProgress=Clamp(number,0,100);else if(key=="CertNarcotics"&&bool.TryParse(value,out flag))NarcoticsCertified=flag;else if(key=="CertExplosives"&&bool.TryParse(value,out flag))ExplosivesCertified=flag;else if(key=="CertWeapons"&&bool.TryParse(value,out flag))WeaponsCertified=flag;}}catch(Exception ex){Game.LogTrivial("AdvancedK9 specialty profile load: "+ex.Message);}}
        private void LoadNeeds(){try{if(!File.Exists(_path))return;foreach(var line in File.ReadAllLines(_path)){int split=line.IndexOf('=');if(split<1)continue;string key=line.Substring(0,split),value=line.Substring(split+1);int number;if(key=="Food"&&int.TryParse(value,out number))Food=Clamp(number,0,100);else if(key=="Water"&&int.TryParse(value,out number))Water=Clamp(number,0,100);}}catch(Exception ex){Game.LogTrivial("AdvancedK9 needs profile load: "+ex.Message);}}
        private void LoadOperationalData(){try{if(!File.Exists(_path))return;foreach(var line in File.ReadAllLines(_path)){int split=line.IndexOf('=');if(split<1)continue;string key=line.Substring(0,split),value=line.Substring(split+1);int n;if(!int.TryParse(value,out n))continue;if(key=="FoodMeals")FoodMeals=Math.Max(0,n);else if(key=="WaterBottles")WaterBottles=Math.Max(0,n);else if(key=="FirstAidKits")FirstAidKits=Math.Max(0,n);else if(key=="ScentBags")ScentBags=Math.Max(0,n);else if(key=="Treats")Treats=Math.Max(0,n);}}catch(Exception ex){Game.LogTrivial("AdvancedK9 equipment load: "+ex.Message);}}
        private void LoadHudPreferences(){try{if(!File.Exists(_path))return;bool glassProfile=false;foreach(var line in File.ReadAllLines(_path)){int split=line.IndexOf('=');if(split<1)continue;string key=line.Substring(0,split),value=line.Substring(split+1);int n;float f;bool flag;if(key=="Version"&&int.TryParse(value,out n)&&n>=9)glassProfile=true;else if(key=="HudDesign"&&int.TryParse(value,out n))HudDesign=Clamp(n,0,4);else if(key=="HudIconSet"&&int.TryParse(value,out n))HudIconSet=Clamp(n,0,4);else if(key=="HudColorTheme"&&int.TryParse(value,out n))HudColorTheme=Clamp(n,0,4);else if(key=="HudTextStyle"&&int.TryParse(value,out n))HudTextStyle=Clamp(n,0,4);else if(key=="HudOpacity"&&float.TryParse(value,out f))HudOpacity=Math.Max(.35f,Math.Min(1f,f));else if(key=="HudCollapsedScale"&&float.TryParse(value,out f))HudCollapsedScale=Math.Max(.5f,Math.Min(1f,f));else if(key=="HudAutoCollapse"&&bool.TryParse(value,out flag))HudAutoCollapse=flag;else if(key=="HudShowPortrait"&&bool.TryParse(value,out flag))HudShowPortrait=flag;else if(key=="HudShowDistance"&&bool.TryParse(value,out flag))HudShowDistance=flag;else if(key=="HudShowCommand"&&bool.TryParse(value,out flag))HudShowCommand=flag;else if(key=="HudShowBehavior"&&bool.TryParse(value,out flag))HudShowBehavior=flag;else if(key=="HudSearchProgress"&&bool.TryParse(value,out flag))HudSearchProgress=flag;else if(key=="HudMetricDistance"&&bool.TryParse(value,out flag))HudMetricDistance=flag;else if(key=="HudAlertDuration"&&int.TryParse(value,out n))HudAlertDuration=Clamp(n,1500,12000);else if(key=="PortraitFile")PortraitFile=value??"";else if(key=="HudShowState"&&bool.TryParse(value,out flag))HudShowState=flag;else if(key=="HudShowHealth"&&bool.TryParse(value,out flag))HudShowHealth=flag;else if(key=="HudShowStamina"&&bool.TryParse(value,out flag))HudShowStamina=flag;else if(key=="HudShowFood"&&bool.TryParse(value,out flag))HudShowFood=flag;else if(key=="HudShowWater"&&bool.TryParse(value,out flag))HudShowWater=flag;else if(key=="HudShowCertifications"&&bool.TryParse(value,out flag))HudShowCertifications=flag;else if(key=="HudShowTrust"&&bool.TryParse(value,out flag))HudShowTrust=flag;else if(key=="HudShowTraining"&&bool.TryParse(value,out flag))HudShowTraining=flag;else if(key=="HudShowInjury"&&bool.TryParse(value,out flag))HudShowInjury=flag;else if(key=="HudShowVoice"&&bool.TryParse(value,out flag))HudShowVoice=flag;}if(!glassProfile){HudX=.975f;HudY=.955f;HudScale=.85f;HudOpacity=.90f;HudShowFood=false;HudShowWater=false;HudShowCertifications=false;HudShowTrust=false;HudShowTraining=false;HudShowInjury=false;Save();}}catch(Exception ex){Game.LogTrivial("AdvancedK9 HUD preference load: "+ex.Message);}}

        private static int FindVestComponent(Ped dog)
        {
            int bestComponent = 8, bestScore = 0;
            for (int component = 1; component <= 11; component++)
            {
                int drawables = Rage.Native.NativeFunction.Natives.GET_NUMBER_OF_PED_DRAWABLE_VARIATIONS<int>(dog, component);
                int textures = 0;
                for(int drawable=0;drawable<Math.Max(1,drawables);drawable++) textures=Math.Max(textures,Rage.Native.NativeFunction.Natives.GET_NUMBER_OF_PED_TEXTURE_VARIATIONS<int>(dog,component,drawable));
                int score = drawables * 10 + textures;
                if (score > bestScore) { bestScore = score; bestComponent = component; }
            }
            return bestComponent;
        }

        private bool IsDalmatian => Breed.Equals("Dalmatian", StringComparison.OrdinalIgnoreCase);
        private int GetVestComponent(Ped dog){if(VestComponent>=0)return VestComponent;return IsDalmatian?5:FindVestComponent(dog);}
        private int GetVestTextureCount(Ped dog){if(dog==null||!dog.Exists())return IsDalmatian?8:16;int component=GetVestComponent(dog);int drawables=Rage.Native.NativeFunction.Natives.GET_NUMBER_OF_PED_DRAWABLE_VARIATIONS<int>(dog,component);int drawable=IsDalmatian?(VestIndex==0&&drawables>1?1:0):(VestIndex==0||drawables<=1?0:Math.Min(VestIndex,drawables-1));return Rage.Native.NativeFunction.Natives.GET_NUMBER_OF_PED_TEXTURE_VARIATIONS<int>(dog,component,drawable);}

        private static int Clamp(int value, int min, int max) => Math.Max(min, Math.Min(max, value));
        private static int Wrap(int value,int count) => count<=0?0:(value%count+count)%count;
    }
}
