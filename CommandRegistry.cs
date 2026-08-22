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
            D(K9Command.SpawnDismiss,"Deploy / Dismiss",false,"deploy","dismiss","partner up","send out the dog"), D(K9Command.Follow,"Follow",true,"follow","follow me"), D(K9Command.Heel,"Heel",true,"heel","heal","at heel"), D(K9Command.Sit,"Sit",true,"sit down","take a seat","sit"), D(K9Command.LieDown,"Down",true,"lie down","lay down","get down","down on the ground","down"), D(K9Command.Stay,"Stay",true,"stay","hold position","hold"), D(K9Command.Recall,"Recall",true,"recall","come here","come back","return","come"), D(K9Command.Fetch,"Fetch / Play",true,"fetch the ball","fetch ball","get the ball","bring the ball","go fetch","fetch","play ball"), D(K9Command.SearchArea,"Search Area",true,"search the area","search area","area search","search around","search"), D(K9Command.SearchVehicle,"Search Vehicle",true,"search the vehicle","search vehicle","search the car","check the vehicle","check the car","sniff the vehicle","sniff vehicle"), D(K9Command.Track,"Track",true,"start tracking","pick up the scent","track","find him","find her","find them"), D(K9Command.Apprehend,"Apprehend",true,"apprehend","get him","get her","take him","take her"), D(K9Command.Release,"Release / Stop",true,"release","stop apprehension","let go","out"), D(K9Command.Guard,"Guard",true,"guard","watch him","watch her","watch"), D(K9Command.Bark,"Bark / Alert",true,"bark","alert","speak"), D(K9Command.EnterVehicle,"Enter Vehicle",true,"enter vehicle","load up","get in the car","get in"), D(K9Command.ExitVehicle,"Exit Vehicle",true,"exit vehicle","unload","get out of the car","get out"), D(K9Command.Pet,"Pet",true,"pet","good dog"), D(K9Command.Feed,"Treat / Feed",true,"give a treat","treat","feed"), D(K9Command.Inspect,"Inspect K9",true,"inspect","check injury","check health"), D(K9Command.FirstAid,"Field First Aid",true,"first aid","field treatment","treat injury"), D(K9Command.ToggleLeash,"Toggle Leash",true,"attach leash","remove leash","leash"), D(K9Command.ToggleCamera,"K9 Camera",true,"dog camera","k9 camera","camera"), D(K9Command.Training,"Training",true,"training","academy","certification")};
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
