using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.IO;
using Rage;
using Rage.Native;

namespace AdvancedK9
{
    internal enum CompatibilityMode { Standalone, PolicingRedefined, StopThePed }
    internal sealed class CompatibilitySearchResult { public bool Positive; public DetectionSpecialty Specialty; public string Source; public string Detail; }

    // Optional PR/CDF and Stop The Ped adapter. No external assembly is referenced,
    // so AdvancedK9 remains loadable when either integration is absent or updated.
    internal sealed class PolicingRedefinedBridge
    {
        private readonly Assembly _pr,_cdf,_stp;
        private readonly string _configuredMode;
        private readonly bool _shareResults;
        private readonly string _bridgePath=Path.Combine("Plugins","LSPDFR","AdvancedK9","CompatibilityBridge.state");
        private readonly string _bridgeRequestPath=Path.Combine("Plugins","LSPDFR","AdvancedK9","CompatibilityBridge.request");
        private bool _bridgePr,_bridgeCdf,_bridgeStp;
        private string _bridgePrVersion="",_bridgeCdfVersion="",_bridgeStpVersion="",_bridgeVehicleHandle="",_bridgePedHandle="",_bridgePursuitHandle="",_bridgeVehicleData="",_bridgePedData="",_bridgeQueryHandle="",_bridgeQueryData="";
        private uint _nextDiagnostic;
        public CompatibilityMode Mode{get;private set;}
        public bool IsAvailable=>Mode!=CompatibilityMode.Standalone;
        public string ModeLabel=>Mode==CompatibilityMode.PolicingRedefined?"Policing Redefined / CDF":Mode==CompatibilityMode.StopThePed?"Stop The Ped":"Standalone";

        public PolicingRedefinedBridge(string configuredMode="Auto",bool shareResults=true)
        {
            _configuredMode=string.IsNullOrWhiteSpace(configuredMode)?"Auto":configuredMode.Trim();_shareResults=shareResults;
            _pr=Find("PolicingRedefined","Policing Redefined");_cdf=Find("CommonDataFramework");_stp=Find("StopThePed","Stop The Ped","STP");
            RefreshBridgeState();SelectMode();
            if(_pr!=null&&_stp!=null)Game.LogTrivial("AdvancedK9 compatibility WARNING: PR and STP are both loaded. PR mode selected; remove STP to prevent conflicting stop/arrest tasks.");
            LogDiagnostics();
        }

        public void TickDiagnostics(){RefreshBridgeState();SelectMode();if(Game.GameTime>=_nextDiagnostic)LogDiagnostics();}
        private void LogDiagnostics(){_nextDiagnostic=Game.GameTime+60000;Game.LogTrivial("AdvancedK9 compatibility: configured="+_configuredMode+", active="+ModeLabel+", PR="+DetectedVersion(_pr,_bridgePr,_bridgePrVersion)+", CDF="+DetectedVersion(_cdf,_bridgeCdf,_bridgeCdfVersion)+", STP="+DetectedVersion(_stp,_bridgeStp,_bridgeStpVersion)+", bridge="+BridgeAvailable()+", shareResults="+_shareResults+".");}

        public Vehicle GetActiveVehicle(Ped handler,float radius){var value=FindEntity<Vehicle>(VehicleNames(),handler)??FindWorldVehicle(_bridgeVehicleHandle);if(value!=null&&value.Exists()&&value.DistanceTo(handler)<=Math.Max(radius,35f)){LogUse("active vehicle",value);return value;}return null;}
        public Ped GetActivePed(Ped handler,float radius){var value=FindEntity<Ped>(PedNames(),handler)??FindWorldPed(_bridgePedHandle);if(value!=null&&value.Exists()&&!value.IsDead&&value!=handler&&value.DistanceTo(handler)<=Math.Max(radius,250f)){LogUse("active ped",value);return value;}return null;}
        public Ped GetPursuitSuspect(Ped handler){var value=FindEntity<Ped>(new[]{"GetActivePursuitSuspect","GetCurrentPursuitSuspect","ActivePursuitSuspect","CurrentPursuitSuspect","GetSuspect","PursuitSuspect"},handler)??FindWorldPed(_bridgePursuitHandle);if(value!=null&&value.Exists()&&!value.IsDead&&value!=handler){LogUse("pursuit suspect",value);return value;}return null;}

        public bool IsProtectedPed(Ped ped)
        {
            if(ped==null||!ped.Exists()||ped.IsDead)return true;
            try{if(NativeFunction.Natives.IS_PED_CUFFED<bool>(ped)||NativeFunction.Natives.IS_PED_BEING_ARRESTED<bool>(ped)||NativeFunction.Natives.IS_PED_HANDCUFFED<bool>(ped))return true;}catch{}
            foreach(string name in new[]{"IsPedArrested","IsArrested","IsPedCuffed","IsCuffed","IsPedHandcuffed","IsHandcuffed","IsPedSurrendering","IsSurrendering","IsPedKneeling","IsKneeling","IsBeingTransported"})
            {bool? state=InvokeBool(name,ped);if(state==true){Game.LogTrivial("AdvancedK9 compatibility safety: "+ModeLabel+" reports "+name+"; contact rejected.");return true;}}
            return false;
        }

        public CompatibilitySearchResult GetSearchResult(Entity target,DetectionSpecialty requested,bool narcoticsCertified,bool explosivesCertified,bool weaponsCertified)
        {
            if(target==null||!target.Exists()||Mode==CompatibilityMode.Standalone)return null;
            object record=GetRecord(target);string inventory=Flatten(record,0,new HashSet<object>(ReferenceComparer.Instance));if(string.IsNullOrWhiteSpace(inventory))inventory=GetSearchText(target);if(string.IsNullOrWhiteSpace(inventory)){string handle=target.Handle.ToString();if(handle==_bridgeVehicleHandle)inventory=_bridgeVehicleData;else if(handle==_bridgePedHandle)inventory=_bridgePedData;}if(string.IsNullOrWhiteSpace(inventory))inventory=RequestBridgeRecord(target);
            if(string.IsNullOrWhiteSpace(inventory)){Game.LogTrivial("AdvancedK9 compatibility search: no inventory record for entity "+target.Handle+"; random fallback suppressed.");return new CompatibilitySearchResult{Positive=false,Specialty=DetectionSpecialty.General,Source=ModeLabel,Detail="Integration inventory unavailable"};}
            DetectionSpecialty detected=Classify(inventory);bool? positive=detected!=DetectionSpecialty.General;
            bool certified=detected==DetectionSpecialty.Narcotics?narcoticsCertified:detected==DetectionSpecialty.Explosives?explosivesCertified:detected==DetectionSpecialty.Weapons?weaponsCertified:true;
            bool matches=requested==DetectionSpecialty.General||detected==DetectionSpecialty.General||requested==detected;
            var result=new CompatibilitySearchResult{Positive=positive.Value&&certified&&matches,Specialty=detected,Source=ModeLabel,Detail=TrimDetail(inventory)};
            Game.LogTrivial("AdvancedK9 compatibility search: source="+result.Source+", entity="+target.Handle+", requested="+requested+", detected="+detected+", certified="+certified+", positive="+result.Positive+".");return result;
        }

        private string RequestBridgeRecord(Entity target)
        {
            if(!BridgeAvailable()||target==null||!target.Exists())return "";
            string handle=target.Handle.ToString();try
            {
                string directory=Path.GetDirectoryName(_bridgeRequestPath);if(!Directory.Exists(directory))Directory.CreateDirectory(directory);
                File.WriteAllLines(_bridgeRequestPath,new[]{"Handle="+handle,"Type="+(target is Vehicle?"Vehicle":"Ped"),"UtcTicks="+DateTime.UtcNow.Ticks});
                uint timeout=Game.GameTime+1800;
                while(Game.GameTime<timeout){GameFiber.Yield();RefreshBridgeState();if(_bridgeQueryHandle==handle&&!string.IsNullOrWhiteSpace(_bridgeQueryData)){Game.LogTrivial("AdvancedK9 compatibility: bridge returned requested inventory for entity "+handle+".");return _bridgeQueryData;}}
            }
            catch(Exception ex){Game.LogTrivial("AdvancedK9 compatibility request failed: "+ex.Message);}
            return "";
        }

        public void RecordK9Indication(Entity target,bool positive,DetectionSpecialty specialty)
        {if(!_shareResults||target==null||!target.Exists()||Mode==CompatibilityMode.Standalone)return;if(TryInvokeAction(new[]{"RecordK9Indication","SetK9Indication","AddK9Indication","SetK9Alert","RecordCanineAlert"},target,positive,specialty.ToString()))Game.LogTrivial("AdvancedK9 compatibility: shared indication with "+ModeLabel+".");else Game.LogTrivial("AdvancedK9 compatibility: no compatible indication writer; local log retained.");}
        public void RecordLocatedSuspect(Ped suspect){if(_shareResults&&suspect!=null&&suspect.Exists())TryInvokeAction(new[]{"RecordK9Locate","SetLocatedSuspect","AddLocatedSuspect","RecordCanineLocate"},suspect,true,"Tracking");}
        public void RecordApprehension(Ped suspect){if(_shareResults&&suspect!=null&&suspect.Exists())TryInvokeAction(new[]{"RecordK9Apprehension","SetApprehendedPed","AddApprehendedPed","RecordCanineApprehension"},suspect,true,"Apprehension");}
        public bool TryArrestHandoff(Ped suspect){if(suspect==null||!suspect.Exists()||Mode==CompatibilityMode.Standalone)return false;return TryInvokeAction(new[]{"StartArrest","ArrestPed","SetPedArrested","BeginArrest","OpenPedMenu","OpenContextMenu"},suspect,true,"AdvancedK9 handoff");}
        public bool TryRequestService(string service,Ped subject)
        {
            if(Mode==CompatibilityMode.Standalone)return false;string[] names;
            if(service=="Perimeter")names=new[]{"RequestPerimeter","CallPerimeter","RequestContainment","CallBackup"};
            else if(service=="Transport")names=new[]{"RequestTransport","CallTransport","RequestPrisonerTransport","CallPrisonerTransport"};
            else if(service=="Medical")names=new[]{"RequestEMS","CallEMS","RequestMedical","CallAmbulance"};
            else names=new[]{"RequestBombSquad","CallBombSquad","RequestExplosiveUnit","CallBombDisposal"};
            return TryInvokeAction(names,subject??Game.LocalPlayer.Character,true,service);
        }

        private object GetRecord(Entity target)
        {foreach(var type in Types())foreach(var method in Methods(type).Where(m=>m.Name.IndexOf("Get",StringComparison.OrdinalIgnoreCase)>=0&&ContainsAny(m.Name,"Data","Record","Inventory","Items"))){object value;if(TryInvoke(method,target,null,null,out value)&&value!=null){LogMethod("record",method);return value;}}return null;}
        private string GetSearchText(Entity target)
        {var sb=new StringBuilder();foreach(var type in Types())foreach(var method in Methods(type).Where(m=>ContainsAny(m.Name,"Search","Contraband","Inventory"))){object value;if(TryInvoke(method,target,null,null,out value)&&value!=null&&!(value is bool))sb.Append(' ').Append(Flatten(value,0,new HashSet<object>(ReferenceComparer.Instance)));}return sb.ToString();}
        private bool? InvokeSearchBoolean(Entity target,DetectionSpecialty specialty)
        {foreach(var type in Types())foreach(var method in Methods(type).Where(m=>m.ReturnType==typeof(bool)&&ContainsAny(m.Name,"K9","Canine","Contraband","Search","Narcotic","Drug","Weapon","Gun","Explosive","Bomb"))){object value;if(TryInvoke(method,target,specialty.ToString(),true,out value)&&value is bool){LogMethod("search",method);return (bool)value;}}return null;}
        private bool? InvokeBool(string methodName,Ped ped){foreach(var type in Types())foreach(var method in Methods(type).Where(m=>m.ReturnType==typeof(bool)&&m.Name.Equals(methodName,StringComparison.OrdinalIgnoreCase))){object value;if(TryInvoke(method,ped,null,null,out value)&&value is bool)return (bool)value;}return null;}

        private T FindEntity<T>(IEnumerable<string> names,Ped handler) where T:Entity
        {foreach(var type in Types()){foreach(var property in Properties(type).Where(p=>names.Any(n=>p.Name.Equals(n,StringComparison.OrdinalIgnoreCase))&&typeof(T).IsAssignableFrom(p.PropertyType))){try{var value=property.GetValue(null,null) as T;if(value!=null){LogMember("target",type.FullName+"."+property.Name);return value;}}catch{}}foreach(var method in Methods(type).Where(m=>names.Any(n=>m.Name.Equals(n,StringComparison.OrdinalIgnoreCase))&&typeof(T).IsAssignableFrom(m.ReturnType))){object value;if(TryInvoke(method,handler,null,null,out value)&&value is T){LogMethod("target",method);return (T)value;}}}return null;}
        private bool TryInvokeAction(IEnumerable<string> names,Entity target,bool positive,string detail){foreach(var type in Types())foreach(var method in Methods(type).Where(m=>names.Any(n=>m.Name.Equals(n,StringComparison.OrdinalIgnoreCase)))){object value;if(TryInvoke(method,target,detail,positive,out value)){LogMethod("writer",method);return true;}}return false;}
        private static bool TryInvoke(MethodInfo method,Entity target,string text,bool? flag,out object result)
        {result=null;try{var p=method.GetParameters();var args=new object[p.Length];for(int i=0;i<p.Length;i++){Type t=p[i].ParameterType;if(target!=null&&t.IsInstanceOfType(target))args[i]=target;else if(t==typeof(string))args[i]=text??"AdvancedK9";else if(t==typeof(bool))args[i]=flag??true;else if(t.IsEnum){try{args[i]=Enum.Parse(t,text??"General",true);}catch{args[i]=Enum.GetValues(t).GetValue(0);}}else if(p[i].HasDefaultValue)args[i]=p[i].DefaultValue;else return false;}result=method.Invoke(null,args);return true;}catch{return false;}}

        private IEnumerable<Type> Types(){foreach(var assembly in ActiveAssemblies()){Type[] values;try{values=assembly.GetTypes();}catch(ReflectionTypeLoadException ex){values=ex.Types.Where(t=>t!=null).ToArray();}catch{continue;}foreach(var type in values)yield return type;}}
        private IEnumerable<Assembly> ActiveAssemblies(){if(Mode==CompatibilityMode.PolicingRedefined){if(_pr!=null)yield return _pr;if(_cdf!=null)yield return _cdf;}else if(Mode==CompatibilityMode.StopThePed&&_stp!=null)yield return _stp;}
        private static IEnumerable<MethodInfo> Methods(Type t){try{return t.GetMethods(BindingFlags.Public|BindingFlags.Static);}catch{return Array.Empty<MethodInfo>();}}
        private static IEnumerable<PropertyInfo> Properties(Type t){try{return t.GetProperties(BindingFlags.Public|BindingFlags.Static);}catch{return Array.Empty<PropertyInfo>();}}
        private static string[] VehicleNames()=>new[]{"GetActiveStopVehicle","GetCurrentStopVehicle","GetTrafficStopVehicle","GetPulloverVehicle","ActiveStopVehicle","CurrentStopVehicle","TrafficStopVehicle","PulloverVehicle","ContextVehicle","SelectedVehicle"};
        private static string[] PedNames()=>new[]{"GetActiveInteractionPed","GetCurrentStopPed","GetStoppedPed","GetSelectedPed","ActiveInteractionPed","CurrentStopPed","StoppedPed","ContextPed","SelectedPed"};
        private static DetectionSpecialty Classify(string text){string t=(text??"").ToLowerInvariant();if(ContainsAny(t,"explosive","bomb","ied","dynamite","c4","detonator","grenade"))return DetectionSpecialty.Explosives;if(ContainsAny(t,"firearm","weapon","pistol","rifle","shotgun","smg","revolver","ammo","ammunition","gun"))return DetectionSpecialty.Weapons;if(ContainsAny(t,"narcotic","drug","cocaine","heroin","fentanyl","meth","marijuana","cannabis","ecstasy","lsd","crack","opioid"))return DetectionSpecialty.Narcotics;return DetectionSpecialty.General;}
        private static string Flatten(object value,int depth,HashSet<object> visited){if(value==null||depth>3)return "";Type type=value.GetType();if(type.IsPrimitive||value is string||value is decimal||type.IsEnum)return Convert.ToString(value);if(!type.IsValueType&&!visited.Add(value))return "";var sb=new StringBuilder();if(value is IEnumerable enumerable){int count=0;foreach(var item in enumerable){if(count++>=40)break;sb.Append(' ').Append(Flatten(item,depth+1,visited));}return sb.ToString();}foreach(var property in type.GetProperties(BindingFlags.Public|BindingFlags.Instance).Where(p=>p.CanRead&&p.GetIndexParameters().Length==0).Take(40)){try{sb.Append(' ').Append(property.Name).Append('=').Append(Flatten(property.GetValue(value,null),depth+1,visited));}catch{}}return sb.ToString();}
        private static string TrimDetail(string value){value=(value??"").Trim();return value.Length<=180?value:value.Substring(0,180);}
        private static bool ContainsAny(string value,params string[] terms)=>terms.Any(term=>value.IndexOf(term,StringComparison.OrdinalIgnoreCase)>=0);
        private static Assembly Find(params string[] names)=>AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a=>names.Any(n=>a.GetName().Name.IndexOf(n,StringComparison.OrdinalIgnoreCase)>=0));
        private static string VersionOf(Assembly assembly)=>assembly==null?"not loaded":assembly.GetName().Name+" "+assembly.GetName().Version;
        private static string DetectedVersion(Assembly assembly,bool bridged,string version)=>assembly!=null?VersionOf(assembly):bridged?(string.IsNullOrWhiteSpace(version)?"detected by bridge":version+" (bridge)"):"not loaded";
        private bool BridgeAvailable()=>_bridgePr||_bridgeCdf||_bridgeStp;
        private void SelectMode(){bool pr=_pr!=null||_cdf!=null||_bridgePr||_bridgeCdf,stp=_stp!=null||_bridgeStp;if(_configuredMode.Equals("Standalone",StringComparison.OrdinalIgnoreCase))Mode=CompatibilityMode.Standalone;else if(_configuredMode.Equals("StopThePed",StringComparison.OrdinalIgnoreCase)||_configuredMode.Equals("STP",StringComparison.OrdinalIgnoreCase))Mode=stp?CompatibilityMode.StopThePed:CompatibilityMode.Standalone;else if(_configuredMode.Equals("PolicingRedefined",StringComparison.OrdinalIgnoreCase)||_configuredMode.Equals("PR",StringComparison.OrdinalIgnoreCase))Mode=pr?CompatibilityMode.PolicingRedefined:CompatibilityMode.Standalone;else Mode=pr?CompatibilityMode.PolicingRedefined:stp?CompatibilityMode.StopThePed:CompatibilityMode.Standalone;}
        private void RefreshBridgeState(){try{if(!File.Exists(_bridgePath)){ClearBridgeState();return;}var map=File.ReadAllLines(_bridgePath).Select(line=>new{line,split=line.IndexOf('=')}).Where(x=>x.split>0).ToDictionary(x=>x.line.Substring(0,x.split),x=>x.line.Substring(x.split+1),StringComparer.OrdinalIgnoreCase);string heartbeat;if(!map.TryGetValue("HeartbeatUtcTicks",out heartbeat)){ClearBridgeState();return;}long ticks;if(!long.TryParse(heartbeat,out ticks)||DateTime.UtcNow-new DateTime(ticks,DateTimeKind.Utc)>TimeSpan.FromSeconds(8)){ClearBridgeState();return;}_bridgePr=ReadBool(map,"PR");_bridgeCdf=ReadBool(map,"CDF");_bridgeStp=ReadBool(map,"STP");_bridgePrVersion=Read(map,"PRVersion");_bridgeCdfVersion=Read(map,"CDFVersion");_bridgeStpVersion=Read(map,"STPVersion");_bridgeVehicleHandle=Read(map,"ActiveVehicleHandle");_bridgePedHandle=Read(map,"ActivePedHandle");_bridgePursuitHandle=Read(map,"PursuitPedHandle");_bridgeVehicleData=Decode(Read(map,"ActiveVehicleData"));_bridgePedData=Decode(Read(map,"ActivePedData"));_bridgeQueryHandle=Read(map,"QueryHandle");_bridgeQueryData=Decode(Read(map,"QueryData"));}catch(Exception ex){ClearBridgeState();Game.LogTrivial("AdvancedK9 bridge state read: "+ex.Message);}}
        private void ClearBridgeState(){_bridgePr=_bridgeCdf=_bridgeStp=false;_bridgePrVersion=_bridgeCdfVersion=_bridgeStpVersion=_bridgeVehicleHandle=_bridgePedHandle=_bridgePursuitHandle=_bridgeVehicleData=_bridgePedData=_bridgeQueryHandle=_bridgeQueryData="";}
        private static bool ReadBool(IDictionary<string,string> map,string key){bool value;return bool.TryParse(Read(map,key),out value)&&value;}
        private static string Read(IDictionary<string,string> map,string key){string value;return map.TryGetValue(key,out value)?value:"";}
        private static string Decode(string value){try{return string.IsNullOrWhiteSpace(value)?"":Encoding.UTF8.GetString(Convert.FromBase64String(value));}catch{return "";}}
        private static Vehicle FindWorldVehicle(string handle)=>string.IsNullOrWhiteSpace(handle)?null:World.GetAllVehicles().FirstOrDefault(v=>v!=null&&v.Exists()&&v.Handle.ToString()==handle);
        private static Ped FindWorldPed(string handle)=>string.IsNullOrWhiteSpace(handle)?null:World.GetAllPeds().FirstOrDefault(p=>p!=null&&p.Exists()&&p.Handle.ToString()==handle);
        private static void LogMethod(string purpose,MethodInfo method)=>Game.LogTrivial("AdvancedK9 compatibility: "+purpose+" API "+method.DeclaringType.FullName+"."+method.Name+".");
        private static void LogMember(string purpose,string member)=>Game.LogTrivial("AdvancedK9 compatibility: "+purpose+" member "+member+".");
        private void LogUse(string purpose,Entity entity)=>Game.LogTrivial("AdvancedK9 compatibility: "+ModeLabel+" "+purpose+" selected entity "+entity.Handle+".");
        private sealed class ReferenceComparer:IEqualityComparer<object>{public static readonly ReferenceComparer Instance=new ReferenceComparer();public new bool Equals(object x,object y)=>ReferenceEquals(x,y);public int GetHashCode(object obj)=>System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);}
    }
}
