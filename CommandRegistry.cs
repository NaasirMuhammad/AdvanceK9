using System;
using System.Collections.Generic;
using System.Linq;
namespace AdvancedK9
{
    internal sealed class CommandDefinition { public K9Command Command{get;} public string Label{get;} public string[] Phrases{get;} public bool RequiresDog{get;} public CommandDefinition(K9Command c,string l,bool r,params string[] p){Command=c;Label=l;RequiresDog=r;Phrases=p;} }
    internal static class CommandRegistry
    {
        public static readonly IReadOnlyList<CommandDefinition> All=new[]{
            D(K9Command.SpawnDismiss,"Deploy / Dismiss",false,"deploy","dismiss","partner up"), D(K9Command.Follow,"Follow",true,"follow"), D(K9Command.Heel,"Heel",true,"heel"), D(K9Command.Sit,"Sit",true,"sit"), D(K9Command.LieDown,"Down",true,"lie down","lay down","down"), D(K9Command.Stay,"Stay",true,"stay","hold"), D(K9Command.Recall,"Recall",true,"recall","come","return"), D(K9Command.Fetch,"Fetch / Play",true,"fetch","get the ball","play"), D(K9Command.SearchArea,"Search Area",true,"search area","area search","search"), D(K9Command.SearchVehicle,"Search Vehicle",true,"search vehicle","check the car","sniff vehicle"), D(K9Command.Track,"Track",true,"track","find him","find her","find them"), D(K9Command.Apprehend,"Apprehend",true,"apprehend","get him","get her"), D(K9Command.Release,"Release / Stop",true,"release","stop apprehension","out","let go"), D(K9Command.Guard,"Guard",true,"guard","watch"), D(K9Command.Bark,"Bark / Alert",true,"bark","alert","speak"), D(K9Command.EnterVehicle,"Enter Vehicle",true,"enter vehicle","load up","get in"), D(K9Command.ExitVehicle,"Exit Vehicle",true,"exit vehicle","unload","get out"), D(K9Command.Pet,"Pet",true,"pet","good dog"), D(K9Command.Feed,"Treat / Feed",true,"treat","feed"), D(K9Command.Inspect,"Inspect K9",true,"inspect","check injury","check health"), D(K9Command.FirstAid,"Field First Aid",true,"first aid","field treatment","treat injury"), D(K9Command.ToggleLeash,"Toggle Leash",true,"leash"), D(K9Command.ToggleCamera,"K9 Camera",true,"camera"), D(K9Command.Training,"Training",true,"training","academy","certification")};
        static CommandDefinition D(K9Command c,string l,bool r,params string[] p)=>new CommandDefinition(c,l,r,p);
        public static bool TryMatch(string text,string dogName,out K9Command command){command=default(K9Command);if(string.IsNullOrWhiteSpace(text))return false;string t=text.ToLowerInvariant(),wake=(dogName??"").Trim().ToLowerInvariant();if((!string.IsNullOrEmpty(wake)&&!t.Contains(wake))&&!t.Contains("k9")&&!t.Contains("k nine"))return false;foreach(var item in All.OrderByDescending(x=>x.Phrases.Max(p=>p.Length)))foreach(string phrase in item.Phrases)if(t.Contains(phrase)){command=item.Command;return true;}return false;}
    }
}
