using System;
using AdvancedK9.API;
using LSPD_First_Response.Mod.API;
using Rage;

namespace AdvancedK9.Callouts
{
    public sealed class EntryPoint : Plugin
    {
        public override void Initialize()
        {
            try
            {
                Functions.RegisterCallout(typeof(LostChildCallout));
                Functions.RegisterCallout(typeof(FugitiveTrailCallout));
                Functions.RegisterCallout(typeof(ArmedBurglaryCallout));
                Game.LogTrivial("AdvancedK9 Callouts: registered Lost Child, Fugitive Trail and Armed Burglary Suspect Hiding.");
            }
            catch(Exception ex){Game.LogTrivial("AdvancedK9 Callouts registration failed safely: "+ex.Message);}
        }
        public override void Finally(){Game.LogTrivial("AdvancedK9 Callouts: unloaded.");}
    }
}
