using System;
using System.IO;
using System.Reflection;
using Rage;

namespace AdvancedK9
{
    public static class EntryPoint
    {
        private static K9Controller _controller;
        private static bool _running;
        private static ResolveEventHandler _dependencyResolver;

        public static void Main()
        {
            InstallDependencyResolver();
            _running = true;
            Game.LogTrivial("AdvancedK9: initialized in RPH mode; starting controller.");
            try
            {
                _controller = new K9Controller(ModConfig.Load());
                _controller.Run();
            }
            catch (Exception ex) { Game.LogTrivial("AdvancedK9 controller failure: " + ex); }
            finally { _running = false; }
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
                    if(!File.Exists(path)){Game.LogTrivial("AdvancedK9 dependency resolver: missing "+path+".");return null;}
                    Game.LogTrivial("AdvancedK9 dependency resolver: loading shared API from "+path+".");
                    return Assembly.LoadFrom(path);
                }
                catch(Exception ex){Game.LogTrivial("AdvancedK9 dependency resolver failed: "+ex.Message);return null;}
            };
            AppDomain.CurrentDomain.AssemblyResolve+=_dependencyResolver;
        }

        public static void Finally()
        {
            _running = false;
            _controller?.Dispose();
            _controller = null;
            if(_dependencyResolver!=null){AppDomain.CurrentDomain.AssemblyResolve-=_dependencyResolver;_dependencyResolver=null;}
            Game.LogTrivial("AdvancedK9: unloaded.");
        }
    }
}
