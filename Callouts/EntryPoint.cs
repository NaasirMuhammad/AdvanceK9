using System;
using System.IO;
using System.Reflection;
using AdvancedK9.API;
using LSPD_First_Response.Mod.API;
using Rage;

namespace AdvancedK9.Callouts
{
    public sealed class EntryPoint : Plugin
    {
        private static bool _registered;
        private static bool _running;
        private static ResolveEventHandler _dependencyResolver;

        public override void Initialize()
        {
            InstallDependencyResolver();
            _running=true;
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
            GameFiber.StartNew(CalloutRequestLoop,"AdvancedK9 callout request listener");
        }

        private static void CalloutRequestLoop()
        {
            while(_running)
            {
                GameFiber.Wait(100);
                string name;
                if(AdvancedK9Api.TryTakeCalloutRequest(out name))StartSelectedCallout(name);
            }
        }

        private static void StartSelectedCallout(string name)
        {
            try
            {
                Game.LogTrivial("AdvancedK9 Callouts: menu requested "+name+".");
                var start=typeof(Functions).GetMethod("StartCallout",BindingFlags.Public|BindingFlags.Static,null,new[]{typeof(string)},null);
                if(start==null)throw new MissingMethodException("LSPDFR Functions.StartCallout(string) is unavailable.");
                start.Invoke(null,new object[]{name});
                Game.DisplayNotification("~b~AdvancedK9 Callouts:~s~ requested "+name+". Accept it through your normal LSPDFR/dispatch control.");
            }
            catch(Exception ex)
            {
                Game.LogTrivial("AdvancedK9 Callouts: menu could not start "+name+": "+ex);
                Game.DisplayNotification("~r~AdvancedK9 Callouts:~s~ unable to start that callout. Check RagePluginHook.log.");
            }
        }

        private static void InstallDependencyResolver()
        {
            if(_dependencyResolver!=null)return;
            _dependencyResolver=(sender,args)=>{
                try
                {
                    var requested=new AssemblyName(args.Name);
                    if(!requested.Name.Equals("AdvancedK9.API",StringComparison.OrdinalIgnoreCase))return null;
                    string path=Path.Combine("Plugins","LSPDFR","AdvancedK9","AdvancedK9.API.dll");
                    return File.Exists(path)?Assembly.LoadFrom(path):null;
                }
                catch(Exception ex){Game.LogTrivial("AdvancedK9 Callouts API resolver failed safely: "+ex.Message);return null;}
            };
            AppDomain.CurrentDomain.AssemblyResolve+=_dependencyResolver;
        }

        private static bool TryRegister(Type calloutType)
        {
            try{Functions.RegisterCallout(calloutType);Game.LogTrivial("AdvancedK9 Callouts: registered "+calloutType.Name+".");return true;}
            catch(Exception ex){Game.LogTrivial("AdvancedK9 Callouts: "+calloutType.Name+" registration failed safely: "+ex);return false;}
        }

        public override void Finally()
        {
            _running=false;
            _registered=false;
            if(_dependencyResolver!=null){AppDomain.CurrentDomain.AssemblyResolve-=_dependencyResolver;_dependencyResolver=null;}
            Game.LogTrivial("AdvancedK9 Callouts: unloaded.");
        }
    }
}
