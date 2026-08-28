using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
namespace AdvancedK9
{
    internal sealed class CommandDefinition { public K9Command Command{get;} public string Label{get;} public string[] Phrases{get;} public bool RequiresDog{get;} public CommandDefinition(K9Command c,string l,bool r,params string[] p){Command=c;Label=l;RequiresDog=r;Phrases=p;} }
    internal static class CommandRegistry
    {
        public static readonly IReadOnlyList<CommandDefinition> All=new[]{
            D(K9Command.SpawnDismiss,"Deploy / Dismiss",false,"deploy k9","deploy the dog","bring out the dog","partner up","send out the dog","dismiss k9","kennel up","end shift","dismiss"),
            D(K9Command.Follow,"Follow",true,"follow me","stay with me","move with me","on me","with me","follow"),
            D(K9Command.Heel,"Heel",true,"come to heel","heel up","get to heel","by my side","at heel","heel","heal"),
            D(K9Command.Sit,"Sit",true,"sit down","take a seat","park it","sit"),
            D(K9Command.LieDown,"Down",true,"lie down","lay down","get low","down on the ground","go down","down"),
            D(K9Command.Stay,"Stay",true,"stay there","hold position","do not move","remain","stand fast","stay","hold"),
            D(K9Command.Recall,"Recall",true,"return to me","back to me","come here","come back","return","recall","disengage and return","come"),
            D(K9Command.WhistleRecall,"Whistle Recall",true,"whistle recall","recall whistle","come on whistle"),
            D(K9Command.HandSignal,"Hand Signal",true,"hand signal","signal recall","silent recall"),
            D(K9Command.Fetch,"Fetch / Play",true,"fetch the ball","retrieve the ball","get the ball","bring the ball","go fetch","play fetch","play ball","retrieve","fetch"),
            D(K9Command.SearchArea,"Search Area",true,"search the area","clear the area","sweep the area","check the area","area search","search around","find the odor","search"),
            D(K9Command.SearchBuilding,"Search Building",true,"search the building","clear the building","building search","clear the rooms","search inside","check the building"),
            D(K9Command.SearchVehicle,"Search Vehicle",true,"search the vehicle","search this vehicle","search the car","check the vehicle","check the car","sniff the vehicle","sweep the vehicle","vehicle search","search vehicle"),
            D(K9Command.SearchNarcotics,"Narcotics Search",true,"search for narcotics","narcotics search","search for drugs","drug search","find the drugs","check for narcotics","narcotics sweep","find dope"),
            D(K9Command.SearchExplosives,"Explosives Search",true,"search for explosives","explosives search","bomb search","search for a bomb","find the bomb","check for explosives","explosive sweep","bomb sweep"),
            D(K9Command.SearchWeapons,"Weapons Search",true,"search for weapons","weapons search","gun search","search for a gun","find the weapon","check for firearms","firearm sweep","weapons sweep"),
            D(K9Command.CollectScent,"Collect Scent Article",true,"collect scent article","bag the scent","take scent sample","collect scent"),
            D(K9Command.Track,"Track",true,"start tracking","pick up the scent","follow the scent","find the trail","track the suspect","locate them","find him","find her","find them","track"),
            D(K9Command.FindTrail,"Reacquire Trail",true,"reacquire the trail","find the trail again","pick the trail back up","recover the scent","find scent","reacquire scent"),
            D(K9Command.K9Warning,"K9 Warning",true,"give k9 warning","give the warning","police k9 warning","announce the dog","warn the suspect","k9 warning"),
            D(K9Command.Apprehend,"Apprehend",true,"apprehend the suspect","engage the suspect","take the suspect","send the dog","attack","bite","get him","get her","take him","take her","apprehend","engage"),
            D(K9Command.HandoffArrest,"PR/STP Arrest Handoff",true,"handoff arrest","start arrest handoff","give suspect to policing menu","process suspect","arrest handoff"),
            D(K9Command.RequestPerimeter,"Request Perimeter",true,"request perimeter","set a perimeter","call perimeter units","containment units"),
            D(K9Command.RequestTransport,"Request Transport",true,"request prisoner transport","call transport","prisoner transport","transport suspect"),
            D(K9Command.RequestMedical,"Request Medical",true,"request ems","call ems","request medical","medical assistance"),
            D(K9Command.RequestBombSquad,"Request Bomb Squad",true,"request bomb squad","call bomb squad","request explosive unit","bomb disposal"),
            D(K9Command.DoorPop,"Door Pop",true,"door pop","deploy from vehicle","release from car","pop the door"),
            D(K9Command.Release,"Release / Stop",true,"release the suspect","stop the dog","stop apprehension","disengage","break contact","leave it","let go","release","out"),
            D(K9Command.Guard,"Guard",true,"guard the suspect","watch the suspect","cover him","cover her","hold the suspect","watch him","watch her","stand guard","guard","watch"),
            D(K9Command.Bark,"Bark / Alert",true,"give an alert","sound off","make noise","bark","alert","speak"),
            D(K9Command.EnterVehicle,"Enter Vehicle",true,"enter the vehicle","enter vehicle","load into the car","load up","mount up","get in the vehicle","get in the car","get inside","get in"),
            D(K9Command.ExitVehicle,"Exit Vehicle",true,"exit the vehicle","exit vehicle","unload from the car","dismount","come out","get out of the vehicle","get out of the car","unload","get out"),
            D(K9Command.Pet,"Pet",true,"pet the dog","praise the dog","reward him","reward her","show affection","good dog","pet"),
            D(K9Command.Feed,"Treat / Feed",true,"give the dog a treat","give a treat","reward with a treat","give food","feed the dog","treat","feed"),
            D(K9Command.Drink,"Give Water",true,"give the dog water","give water","water the dog","get a drink","drink water","water break","hydrate","drink"),
            D(K9Command.Rest,"Rest K9",true,"rest the dog","take a rest","sleep","rest"),
            D(K9Command.Inspect,"Inspect K9",true,"inspect the dog","check the dog","check status","check injury","check health","medical check","inspect"),
            D(K9Command.FirstAid,"Field First Aid",true,"give first aid","apply first aid","provide treatment","field treatment","treat the injury","treat injury","first aid"),
            D(K9Command.VeterinaryCare,"Veterinary Care",true,"go to the vet","veterinary care","vet treatment","visit veterinarian","vet"),
            D(K9Command.Restock,"Restock Equipment",true,"restock equipment","reload k9 gear","replenish supplies","restock"),
            D(K9Command.ToggleLeash,"Toggle Leash",true,"attach the leash","put on the leash","take off the leash","remove the leash","leash on","leash off","attach leash","remove leash","leash"),
            D(K9Command.ToggleCamera,"K9 Camera",true,"activate dog camera","turn on k9 camera","disable dog camera","turn off k9 camera","dog camera","k9 camera","body camera","camera"),
            D(K9Command.Training,"Core Training",true,"go to training","start core training","training ground","begin academy","academy training","core certification course","core training","academy","certification"),
            D(K9Command.TrainNarcotics,"Narcotics Training",true,"start narcotics training","narcotics certification","drug detection training","train for drugs","narcotics academy"),
            D(K9Command.TrainExplosives,"Explosives Training",true,"start explosives training","bomb certification","bomb detection training","train for explosives","explosives academy"),
            D(K9Command.TrainWeapons,"Weapons Training",true,"start weapons training","weapons certification","gun detection training","firearm training","weapons academy")};
        static CommandDefinition D(K9Command c,string l,bool r,params string[] p)=>new CommandDefinition(c,l,r,p);
        public static bool TryMatch(string text,string dogName,out K9Command command)
        {
            command=default(K9Command);if(string.IsNullOrWhiteSpace(text))return false;
            string t=Normalize(text),wake=Normalize(dogName??"");
            bool hasWake=HasPhrase(t,wake)||HasPhrase(t,"k9")||HasPhrase(t,"k 9")||HasPhrase(t,"k nine")||HasPhrase(t,"kay nine")||HasPhrase(t,"canine");
            if(!hasWake)return false;
            // Spoken "sit down" means sit; do not let the generic word "down" turn it
            // into the separate lie-down command.
            if(HasPhrase(t,"sit down")||HasPhrase(t,"take a seat")){command=K9Command.Sit;return true;}
            foreach(var item in All.SelectMany(x=>x.Phrases.Select(p=>new{Item=x,Phrase=Normalize(p)})).OrderByDescending(x=>x.Phrase.Length))
                if(HasPhrase(t,item.Phrase)){command=item.Item.Command;return true;}
            return false;
        }
        private static bool HasPhrase(string text,string phrase)=>!string.IsNullOrWhiteSpace(phrase)&&(" "+text+" ").Contains(" "+phrase+" ");
        private static string Normalize(string value){var b=new StringBuilder();bool space=false;foreach(char c in value.ToLowerInvariant()){if(char.IsLetterOrDigit(c)){b.Append(c);space=false;}else if(!space){b.Append(' ');space=true;}}return b.ToString().Trim();}
    }
}
