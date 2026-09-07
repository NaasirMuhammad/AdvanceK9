using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Rage;

namespace AdvancedK9
{
    // Versioned, file-based optional adapter boundary. Third-party assemblies remain untouched
    // and are never referenced, copied, or required by AdvancedK9.
    internal sealed class OperationalFrameworkAdapters
    {
        private const int ContractVersion=1;
        private readonly bool _blrEnabled,_pdCompEnabled,_damageEnabled;
        private readonly string _statePath=Path.Combine("Plugins","LSPDFR","AdvancedK9","OperationalAdapters.state");
        private readonly string _eventPath=Path.Combine("Plugins","LSPDFR","AdvancedK9","OperationalAdapters.event");
        private Assembly _blr,_pdComp,_damage,_npci;
        private string _damageFile="",_npciFile="";
        private string _activePedHandle="",_activeVehicleHandle="";
        private readonly HashSet<string> _protectedHandles=new HashSet<string>();
        private uint _nextRefresh,_nextDiagnostic;
        private int _handlerHealth=-1,_dogHealth=-1;

        public OperationalFrameworkAdapters(bool blrEnabled,bool pdCompEnabled,bool damageEnabled)
        {_blrEnabled=blrEnabled;_pdCompEnabled=pdCompEnabled;_damageEnabled=damageEnabled;RefreshAssemblies();}

        public void Tick(Ped handler,Ped dog)
        {
            if(Game.GameTime>=_nextRefresh){_nextRefresh=Game.GameTime+1000;RefreshAssemblies();ReadState();}
            if(Game.GameTime>=_nextDiagnostic)
            {
                _nextDiagnostic=Game.GameTime+60000;
                Game.LogTrivial("AdvancedK9 optional adapters: contract=v"+ContractVersion+", BLR="+Label(_blr,_blrEnabled)+", PD Comp="+Label(_pdComp,_pdCompEnabled)+", Damage Tracker="+DamageLabel()+", NPCI="+NpcInfoLabel()+".");
            }
            if(_damageEnabled&&(_damage!=null||!string.IsNullOrWhiteSpace(_damageFile)))
            {
                TrackHealth("Handler",handler,ref _handlerHealth);
                TrackHealth("K9",dog,ref _dogHealth);
            }
        }

        public Ped GetActivePed()=>FindPed(_activePedHandle);
        public Vehicle GetActiveVehicle()=>FindVehicle(_activeVehicleHandle);
        public bool IsProtected(Ped ped)=>ped!=null&&ped.Exists()&&_protectedHandles.Contains(ped.Handle.ToString());

        public void Publish(string frameworkEvent,Entity subject,string detail)
        {
            try
            {
                string directory=Path.GetDirectoryName(_eventPath);if(!Directory.Exists(directory))Directory.CreateDirectory(directory);
                string[] lines={"ContractVersion="+ContractVersion,"EventId="+Guid.NewGuid().ToString("N"),"Event="+Safe(frameworkEvent),"SubjectType="+(subject is Ped?"Ped":subject is Vehicle?"Vehicle":"None"),"SubjectHandle="+(subject!=null&&subject.Exists()?subject.Handle.ToString():""),"Detail="+Safe(detail),"UtcTicks="+DateTime.UtcNow.Ticks};
                string temp=_eventPath+".tmp";File.WriteAllLines(temp,lines);File.Copy(temp,_eventPath,true);File.Delete(temp);
            }
            catch(Exception ex){Game.LogTrivial("AdvancedK9 optional adapter publish isolated: "+ex.Message);}
        }

        private void TrackHealth(string role,Ped ped,ref int previous)
        {
            if(ped==null||!ped.Exists()){previous=-1;return;}int current=ped.Health;
            if(previous>=0&&current<previous)Publish("Damage",ped,role+" health "+previous+"->"+current);
            previous=current;
        }

        private void RefreshAssemblies()
        {
            if(_blrEnabled&&_blr==null)_blr=FindAssembly("BLR","BetterLawEnforcement","Better Law Response");
            if(_pdCompEnabled&&_pdComp==null)_pdComp=FindAssembly("PDComp","PD Comp","PoliceDepartmentComputer");
            if(_damageEnabled&&_damage==null)_damage=FindAssembly("DamageTracker","DamageTrackingFramework","Damage Tracker Framework","DamageTrackerFramework");
            if(_npci==null)_npci=FindAssembly("NPCI");
            if(_damageEnabled&&_damage==null&&string.IsNullOrWhiteSpace(_damageFile))_damageFile=FindPluginFile("*Damage*Track*.dll");
            if(_npci==null&&string.IsNullOrWhiteSpace(_npciFile))_npciFile=FindPluginFile("NPCI.dll");
        }

        private void ReadState()
        {
            try
            {
                if(!File.Exists(_statePath))return;var map=File.ReadAllLines(_statePath).Select(x=>new{x,p=x.IndexOf('=')}).Where(x=>x.p>0).ToDictionary(x=>x.x.Substring(0,x.p),x=>x.x.Substring(x.p+1),StringComparer.OrdinalIgnoreCase);
                int contract;if(!int.TryParse(Read(map,"ContractVersion"),out contract)||contract!=ContractVersion)return;
                long ticks;if(!long.TryParse(Read(map,"HeartbeatUtcTicks"),out ticks)||DateTime.UtcNow-new DateTime(ticks,DateTimeKind.Utc)>TimeSpan.FromSeconds(8))return;
                _activePedHandle=_blrEnabled?Read(map,"BLR.ActivePedHandle"):"";
                _activeVehicleHandle=_blrEnabled?Read(map,"BLR.ActiveVehicleHandle"):"";
                _protectedHandles.Clear();
                if(_pdCompEnabled)foreach(string handle in Read(map,"PDComp.ProtectedPedHandles").Split(new[]{','},StringSplitOptions.RemoveEmptyEntries))_protectedHandles.Add(handle.Trim());
            }
            catch(Exception ex){Game.LogTrivial("AdvancedK9 optional adapter state isolated: "+ex.Message);}
        }

        private static string Read(IDictionary<string,string> map,string key){string value;return map.TryGetValue(key,out value)?value:"";}
        private static string Safe(string value)=>(value??"").Replace("\r"," ").Replace("\n"," ").Replace("=","-").Trim();
        private string DamageLabel()
        {
            if(!_damageEnabled)return "disabled";
            if(_damage!=null)return _damage.GetName().Name+" "+_damage.GetName().Version;
            return string.IsNullOrWhiteSpace(_damageFile)?"not detected":"file detected: "+Path.GetFileName(_damageFile);
        }
        private string NpcInfoLabel()
        {
            if(_npci!=null)return _npci.GetName().Name+" "+_npci.GetName().Version+" (informational)";
            return string.IsNullOrWhiteSpace(_npciFile)?"not detected":"file detected: "+Path.GetFileName(_npciFile)+" (informational)";
        }
        private static string FindPluginFile(string pattern)
        {
            try{return Directory.Exists("Plugins")?Directory.GetFiles("Plugins",pattern,SearchOption.AllDirectories).FirstOrDefault()??"":"";}catch{return "";}
        }
        private static string Label(Assembly assembly,bool enabled)=>!enabled?"disabled":assembly==null?"not detected":assembly.GetName().Name+" "+assembly.GetName().Version;
        private static Assembly FindAssembly(params string[] names)=>AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a=>names.Any(n=>a.GetName().Name.IndexOf(n,StringComparison.OrdinalIgnoreCase)>=0));
        private static Ped FindPed(string handle)=>string.IsNullOrWhiteSpace(handle)?null:World.GetAllPeds().FirstOrDefault(x=>x!=null&&x.Exists()&&x.Handle.ToString()==handle);
        private static Vehicle FindVehicle(string handle)=>string.IsNullOrWhiteSpace(handle)?null:World.GetAllVehicles().FirstOrDefault(x=>x!=null&&x.Exists()&&x.Handle.ToString()==handle);
    }
}
