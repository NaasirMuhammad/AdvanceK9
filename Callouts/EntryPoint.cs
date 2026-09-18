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
        private static readonly Random Random=new Random();
        private static readonly string[] AutomaticCallouts={
            "AdvancedK9: Missing Vulnerable Teen",
            "AdvancedK9: Fugitive Trail",
            "AdvancedK9: Armed Burglary Suspect Hiding"
        };
        private static bool _automaticDispatch=true;
        private static int _automaticMinimumSeconds=300;
        private static int _automaticMaximumSeconds=600;
        private static uint _nextAutomaticDispatch;
        private static int _lastAutomaticCallout=-1;

        public override void Initialize()
        {
            InstallDependencyResolver();
            LoadAutomaticDispatchSettings();
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
            GameFiber.StartNew(AutomaticDispatchLoop,"AdvancedK9 automatic callout dispatcher");
        }

        private static void CalloutRequestLoop()
        {
            while(_running)
            {
                GameFiber.Wait(100);
                string name;
                if(AdvancedK9Api.TryTakeCalloutRequest(out name)){StartSelectedCallout(name,"menu");ScheduleNextAutomaticDispatch();}
            }
        }

        private static void AutomaticDispatchLoop()
        {
            ScheduleNextAutomaticDispatch();
            while(_running)
            {
                GameFiber.Wait(1000);
                if(!_automaticDispatch||!_registered||Game.GameTime<_nextAutomaticDispatch)continue;
                K9ApiSnapshot snapshot;
                if(!AdvancedK9Api.TryGetSnapshot(out snapshot)||!snapshot.OnDuty||!snapshot.Deployed){ScheduleNextAutomaticDispatch(60);continue;}
                bool known;
                if(IsCalloutActive(out known)||!known){ScheduleNextAutomaticDispatch(45);continue;}
                int selected;
                do{selected=Random.Next(AutomaticCallouts.Length);}while(AutomaticCallouts.Length>1&&selected==_lastAutomaticCallout);
                if(StartSelectedCallout(AutomaticCallouts[selected],"automatic dispatch"))_lastAutomaticCallout=selected;
                ScheduleNextAutomaticDispatch();
            }
        }

        private static bool IsCalloutActive(out bool known)
        {
            known=false;
            try
            {
                foreach(string name in new[]{"IsCalloutRunning","IsCalloutActive"})
                {
                    MethodInfo method=typeof(Functions).GetMethod(name,BindingFlags.Public|BindingFlags.Static,null,Type.EmptyTypes,null);
                    if(method==null||method.ReturnType!=typeof(bool))continue;
                    known=true;return (bool)method.Invoke(null,null);
                }
                foreach(string name in new[]{"GetCurrentCallout","GetActiveCallout"})
                {
                    MethodInfo method=typeof(Functions).GetMethod(name,BindingFlags.Public|BindingFlags.Static,null,Type.EmptyTypes,null);
                    if(method==null)continue;
                    known=true;return method.Invoke(null,null)!=null;
                }
            }
            catch(Exception ex){Game.LogTrivial("AdvancedK9 Callouts: active-callout probe contained: "+ex.Message);known=true;return true;}
            return false;
        }

        private static void LoadAutomaticDispatchSettings()
        {
            try
            {
                string path=Path.Combine("Plugins","LSPDFR","AdvancedK9","AdvancedK9.ini");
                var ini=new InitializationFile(path);
                _automaticDispatch=ini.ReadBoolean("Callouts","AutomaticDispatch",true);
                _automaticMinimumSeconds=Math.Max(60,ini.ReadInt32("Callouts","AutomaticMinimumSeconds",300));
                _automaticMaximumSeconds=Math.Max(_automaticMinimumSeconds,ini.ReadInt32("Callouts","AutomaticMaximumSeconds",600));
                Game.LogTrivial("AdvancedK9 Callouts: automatic dispatch "+(_automaticDispatch?"enabled":"disabled")+"; interval="+_automaticMinimumSeconds+"-"+_automaticMaximumSeconds+" seconds.");
            }
            catch(Exception ex){Game.LogTrivial("AdvancedK9 Callouts: automatic-dispatch settings fallback contained: "+ex.Message);}
        }

        private static void ScheduleNextAutomaticDispatch(int fixedDelaySeconds=0)
        {
            int delay=fixedDelaySeconds>0?fixedDelaySeconds:Random.Next(_automaticMinimumSeconds,_automaticMaximumSeconds+1);
            _nextAutomaticDispatch=Game.GameTime+(uint)(delay*1000);
        }

        private static bool StartSelectedCallout(string name,string source)
        {
            try
            {
                Game.LogTrivial("AdvancedK9 Callouts: "+source+" requested "+name+".");
                var start=typeof(Functions).GetMethod("StartCallout",BindingFlags.Public|BindingFlags.Static,null,new[]{typeof(string)},null);
                if(start==null)throw new MissingMethodException("LSPDFR Functions.StartCallout(string) is unavailable.");
                start.Invoke(null,new object[]{name});
                if(source=="menu")Game.DisplayNotification("~b~AdvancedK9 Callouts:~s~ requested "+name+". Accept it through your normal LSPDFR/dispatch control.");
                return true;
            }
            catch(Exception ex)
            {
                Game.LogTrivial("AdvancedK9 Callouts: "+source+" could not start "+name+": "+ex);
                Game.DisplayNotification("~r~AdvancedK9 Callouts:~s~ unable to start that callout. Check RagePluginHook.log.");
                return false;
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
