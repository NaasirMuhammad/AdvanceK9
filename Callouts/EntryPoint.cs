using System;
using AdvancedK9.API;
using LSPD_First_Response.Mod.API;
using Rage;

namespace AdvancedK9.Callouts
{
    public sealed class EntryPoint : Plugin
    {
        private static bool _registered;

        public override void Initialize()
        {
            GameFiber.StartNew(()=>{
                GameFiber.Wait(2500);
                if(_registered)return;
                int count=0;
                count+=TryRegister(typeof(LostChildCallout))?1:0;
                count+=TryRegister(typeof(FugitiveTrailCallout))?1:0;
                count+=TryRegister(typeof(ArmedBurglaryCallout))?1:0;
                _registered=count>0;
                Game.LogTrivial("AdvancedK9 Callouts: registered "+count+"/3 callouts after LSPDFR duty initialization.");
            },"AdvancedK9 delayed callout registration");
        }

        private static bool TryRegister(Type calloutType)
        {
            try{Functions.RegisterCallout(calloutType);Game.LogTrivial("AdvancedK9 Callouts: registered "+calloutType.Name+".");return true;}
            catch(Exception ex){Game.LogTrivial("AdvancedK9 Callouts: "+calloutType.Name+" registration failed safely: "+ex);return false;}
        }

        public override void Finally(){_registered=false;Game.LogTrivial("AdvancedK9 Callouts: unloaded.");}
    }
}
