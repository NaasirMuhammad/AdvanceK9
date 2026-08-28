using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using LSPD_First_Response.Mod.API;
using Rage;

namespace AdvancedK9.LSPDFRBridge
{
    public sealed class EntryPoint : Plugin
    {
        private volatile bool _running;
        private static readonly string DirectoryPath=Path.Combine("Plugins","LSPDFR","AdvancedK9");
        private static readonly string StatePath=Path.Combine(DirectoryPath,"CompatibilityBridge.state");
        private static readonly string TempPath=StatePath+".tmp";
        private static readonly string RequestPath=Path.Combine(DirectoryPath,"CompatibilityBridge.request");
        private static readonly string[] VehicleNames={"GetActiveStopVehicle","GetCurrentStopVehicle","GetTrafficStopVehicle","GetPulloverVehicle","ActiveStopVehicle","CurrentStopVehicle","TrafficStopVehicle","PulloverVehicle","ContextVehicle","SelectedVehicle"};
        private static readonly string[] PedNames={"GetActiveInteractionPed","GetCurrentStopPed","GetStoppedPed","GetSelectedPed","ActiveInteractionPed","CurrentStopPed","StoppedPed","ContextPed","SelectedPed"};
        private static readonly string[] PursuitNames={"GetActivePursuitSuspect","GetCurrentPursuitSuspect","ActivePursuitSuspect","CurrentPursuitSuspect","GetSuspect","PursuitSuspect"};
        private static string _lastActionRequestId="",_actionRequestId="",_actionSink="",_actionDetail="";
        private static string _lastQueryRequestId="",_queryRequestId="",_queryHandle="",_queryData="",_querySource="";
        private static bool _actionSucceeded;
        private static bool _queryAvailable;
        private static string _surfaceSignature="";
        private static readonly Dictionary<string,Type[]> TypeCache=new Dictionary<string,Type[]>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string,PendingOfficerSearch> PendingOfficerSearches=new Dictionary<string,PendingOfficerSearch>(StringComparer.OrdinalIgnoreCase);
        private static DateTime _nextOfficerSearchReconciliationUtc=DateTime.MinValue;

        public override void Initialize()
        {
            _running=true;
            GameFiber.StartNew(Run,"AdvancedK9 LSPDFR compatibility bridge");
        }

        private void Run()
        {
            Game.LogTrivial("AdvancedK9 bridge: started inside the LSPDFR plugin AppDomain.");
            while(_running)
            {
                try{Publish();}catch(Exception ex){Game.LogTrivial("AdvancedK9 bridge publish error: "+ex.Message);}
                GameFiber.Sleep(1000);
            }
        }

        public override void Finally()
        {
            _running=false;
            try{if(File.Exists(StatePath))File.Delete(StatePath);if(File.Exists(TempPath))File.Delete(TempPath);if(File.Exists(RequestPath))File.Delete(RequestPath);}catch{}
        }

        private static void Publish()
        {
            Assembly pr=Find("PolicingRedefined","Policing Redefined"),cdf=Find("CommonDataFramework"),stp=Find("StopThePed","Stop The Ped","STP"),nexus=Find("NexusMDT");
            var active=(pr!=null||cdf!=null)?new[]{pr,cdf}.Where(a=>a!=null).ToArray():stp!=null?new[]{stp}:Array.Empty<Assembly>();
            var request=ReadRequest();
            string surfaceSignature=(cdf==null?"":cdf.FullName)+"|"+(pr==null?"":pr.FullName)+"|"+(nexus==null?"":nexus.FullName);
            if(surfaceSignature!=_surfaceSignature){LogIntegrationSurfaces(cdf,pr,nexus);_surfaceSignature=surfaceSignature;}
            Vehicle vehicle=FindEntity<Vehicle>(active,VehicleNames);Ped ped=FindEntity<Ped>(active,PedNames);Ped pursuit=FindEntity<Ped>(active,PursuitNames);
            string vehicleData=Flatten(GetRecord(active,vehicle),0,new HashSet<object>(ReferenceComparer.Instance));
            string pedData=Flatten(GetRecord(active,ped),0,new HashSet<object>(ReferenceComparer.Instance));
            Entity query=FindRequestedEntity(request);
            ProcessInventoryQuery(request,cdf,pr,query);
            ProcessK9Indication(request,nexus,pr,query);
            ProcessPendingOfficerSearches(pr,nexus);
            var lines=new[]{
                "Protocol=2",
                "HeartbeatUtcTicks="+DateTime.UtcNow.Ticks,
                "PR="+(pr!=null),"PRVersion="+VersionOf(pr),
                "CDF="+(cdf!=null),"CDFVersion="+VersionOf(cdf),
                "NexusMDT="+(nexus!=null),"NexusMDTVersion="+VersionOf(nexus),
                "STP="+(stp!=null),"STPVersion="+VersionOf(stp),
                "ActiveVehicleHandle="+HandleOf(vehicle),
                "ActivePedHandle="+HandleOf(ped),
                "PursuitPedHandle="+HandleOf(pursuit),
                "ActiveVehicleData="+Encode(vehicleData),
                "ActivePedData="+Encode(pedData),
                "QueryRequestId="+_queryRequestId,
                "QueryHandle="+_queryHandle,
                "QueryAvailable="+_queryAvailable,
                "QuerySource="+_querySource,
                "QueryData="+Encode(_queryData),
                "ActionRequestId="+_actionRequestId,
                "ActionSucceeded="+_actionSucceeded,
                "ActionSink="+_actionSink,
                "ActionDetail="+Encode(_actionDetail)
            };
            if(!Directory.Exists(DirectoryPath))Directory.CreateDirectory(DirectoryPath);
            File.WriteAllLines(TempPath,lines);
            File.Copy(TempPath,StatePath,true);File.Delete(TempPath);
        }

        private static Dictionary<string,string> ReadRequest()
        {
            try
            {
                if(!File.Exists(RequestPath))return new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
                return File.ReadAllLines(RequestPath).Select(line=>new{line,split=line.IndexOf('=')}).Where(x=>x.split>0).ToDictionary(x=>x.line.Substring(0,x.split),x=>x.line.Substring(x.split+1),StringComparer.OrdinalIgnoreCase);
            }
            catch{return new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);}
        }

        private static Entity FindRequestedEntity(IDictionary<string,string> request)
        {
            string handle=Read(request,"Handle"),type=Read(request,"Type");if(string.IsNullOrWhiteSpace(handle))return null;
            if(type.Equals("Vehicle",StringComparison.OrdinalIgnoreCase))return World.GetAllVehicles().FirstOrDefault(v=>v!=null&&v.Exists()&&v.Handle.ToString()==handle);
            return World.GetAllPeds().FirstOrDefault(p=>p!=null&&p.Exists()&&p.Handle.ToString()==handle);
        }

        private static void ProcessInventoryQuery(IDictionary<string,string> request,Assembly cdf,Assembly pr,Entity query)
        {
            if(!Read(request,"Action").Equals("InventoryQuery",StringComparison.OrdinalIgnoreCase))return;
            string id=Read(request,"RequestId");if(string.IsNullOrWhiteSpace(id)||id==_lastQueryRequestId)return;
            _lastQueryRequestId=_queryRequestId=id;_queryHandle=Read(request,"Handle");_queryData="";_querySource="Requested entity unavailable";_queryAvailable=false;
            if(query!=null&&query.Exists())_queryData=GetUnifiedInventory(ReadBool(request,"UseCdfInventory",true)?cdf:null,pr,query,out _querySource,out _queryAvailable);
        }

        private static string GetUnifiedInventory(Assembly cdf,Assembly pr,Entity entity,out string source,out bool available)
        {
            source="";available=false;object record;
            // PR owns the generated search-item list shown by the officer search UI. CDF's
            // GetVehicleData/GetPedData returns a database record, not contraband contents;
            // flattening that record can turn empty schema/category names into false odors.
            if(pr!=null&&TryExactInventoryCall(pr,entity,"SearchItemsAPI",entity is Vehicle?"GetVehicleSearchItems":"GetPedSearchItems",out record))
            {
                available=record!=null;source="PR.SearchItemsAPI";
                return ExtractSearchItemNames(record);
            }
            // CDF remains the identity/record provider. Only an explicitly named inventory
            // collection is accepted; generic Items and the full record are never scanned.
            if(cdf!=null&&TryExactInventoryCall(cdf,entity,entity is Vehicle?"VehicleDataController":"PedDataController",entity is Vehicle?"GetVehicleData":"GetPedData",out record))
            {
                object items=ReadInstanceMember(record,"SearchItems")??ReadInstanceMember(record,"VehicleSearchItems")??ReadInstanceMember(record,"PedSearchItems")??ReadInstanceMember(record,"InventoryItems");
                if(items!=null){available=true;source="CDF explicit search items";return ExtractSearchItemNames(items);}
                source="CDF record has no public search-item collection";return "";
            }
            source=cdf!=null?"CDF inventory API unavailable":pr!=null?"PR inventory API unavailable":"No inventory provider";
            return "";
        }

        private static bool TryExactInventoryCall(Assembly assembly,Entity entity,string typeName,string methodName,out object result)
        {
            result=null;
            foreach(Type type in Types(new[]{assembly}).Where(t=>t.Name.IndexOf(typeName,StringComparison.OrdinalIgnoreCase)>=0||(t.FullName??"").IndexOf(typeName,StringComparison.OrdinalIgnoreCase)>=0))
            foreach(MethodInfo method in type.GetMethods(BindingFlags.Public|BindingFlags.Static).Where(m=>m.Name.Equals(methodName,StringComparison.OrdinalIgnoreCase)))
            {
                ParameterInfo[] p=method.GetParameters();object argument;if(p.Length!=1||!TryEntityArgument(p[0],entity,out argument))continue;
                try{result=method.Invoke(null,new[]{argument});Game.LogTrivial("AdvancedK9 bridge: inventory reader "+type.FullName+"."+method.Name+" selected.");return true;}catch(Exception ex){Game.LogTrivial("AdvancedK9 bridge: inventory reader failed: "+Unwrap(ex).Message);}
            }
            return false;
        }

        private static bool TryEntityArgument(ParameterInfo parameter,Entity entity,out object value)
        {
            value=null;Type type=parameter.ParameterType;if(type.IsInstanceOfType(entity)){value=entity;return true;}
            long handle;if(!long.TryParse(entity.Handle.ToString(),out handle))return false;
            if(type==typeof(int)){value=(int)handle;return true;}if(type==typeof(long)){value=handle;return true;}
            return false;
        }

        private static object ReadInstanceMember(object value,string name)
        {
            if(value==null)return null;Type type=value.GetType();
            try{PropertyInfo property=type.GetProperty(name,BindingFlags.Public|BindingFlags.Instance|BindingFlags.IgnoreCase);if(property!=null&&property.CanRead)return property.GetValue(value,null);}catch{}
            try{FieldInfo field=type.GetField(name,BindingFlags.Public|BindingFlags.Instance|BindingFlags.IgnoreCase);if(field!=null)return field.GetValue(value);}catch{}
            return null;
        }

        private static string ExtractSearchItemNames(object value)
        {
            var names=new List<string>();CollectSearchItemNames(value,names,0);return string.Join(" | ",names.Where(n=>!string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.OrdinalIgnoreCase).Take(40));
        }

        private static void CollectSearchItemNames(object value,ICollection<string> names,int depth)
        {
            if(value==null||depth>2)return;if(value is string){AddItemName(names,(string)value);return;}
            if(value is IEnumerable enumerable){int count=0;foreach(object item in enumerable){if(count++>=40)break;CollectSearchItemNames(item,names,depth+1);}return;}
            Type type=value.GetType();
            bool found=false;foreach(string member in new[]{"ItemName","DisplayName","Name","Label","Text"})
            {
                object candidate=ReadInstanceMember(value,member);if(candidate is string&&!string.IsNullOrWhiteSpace((string)candidate)){AddItemName(names,(string)candidate);found=true;break;}
            }
            if(!found){object nested=ReadInstanceMember(value,"Item");if(nested!=null&&!ReferenceEquals(nested,value))CollectSearchItemNames(nested,names,depth+1);}
        }

        private static void AddItemName(ICollection<string> names,string value)
        {
            value=(value??"").Trim();if(value.Length>0&&value.Length<=160)names.Add(value);
        }

        private static void ProcessK9Indication(IDictionary<string,string> request,Assembly nexus,Assembly pr,Entity target)
        {
            if(!Read(request,"Action").Equals("K9Indication",StringComparison.OrdinalIgnoreCase))return;
            string id=Read(request,"RequestId");if(string.IsNullOrWhiteSpace(id)||id==_lastActionRequestId)return;
            _lastActionRequestId=_actionRequestId=id;_actionSucceeded=false;_actionSink="Local AdvancedK9 log";
            bool positive;bool.TryParse(Read(request,"Positive"),out positive);
            string specialty=Read(request,"Specialty"),k9=Read(request,"K9Name"),targetLabel=Read(request,"TargetLabel");
            if(string.IsNullOrWhiteSpace(k9))k9="K9";if(string.IsNullOrWhiteSpace(specialty))specialty="General";if(string.IsNullOrWhiteSpace(targetLabel))targetLabel=target is Vehicle?"vehicle":"person";
            string result=positive?"POSITIVE "+specialty.ToUpperInvariant()+" indication":"NEGATIVE indication";
            string narrative="AdvancedK9: K9 "+k9+" completed a "+targetLabel+" sniff — "+result+". Inventory remains undisclosed pending officer search.";
            _actionDetail=narrative;
            if(target!=null&&target.Exists()&&pr!=null)QueueOfficerSearchReconciliation(pr,target,positive,specialty,k9);
            if(ReadBool(request,"ShareWithNexusMDT",true)&&nexus!=null&&TryAppendNexusIncidentNote(nexus,narrative,out _actionSink))_actionSucceeded=true;
            else if(pr!=null&&TryNamedWriter(pr,new[]{"RecordK9Indication","SetK9Indication","AddK9Indication","SetK9Alert","RecordCanineAlert"},target,positive,specialty,k9,id,narrative,out _actionSink))_actionSucceeded=true;
            Game.LogTrivial("AdvancedK9 bridge: K9 indication "+(_actionSucceeded?"published to "+_actionSink:"retained locally; no compatible public NexusMDT/PR writer")+". "+narrative);
        }

        private static void QueueOfficerSearchReconciliation(Assembly pr,Entity target,bool positive,string specialty,string k9)
        {
            string source;bool available;string items=GetUnifiedInventory(null,pr,target,out source,out available);if(!available)return;
            string key=(target is Vehicle?"Vehicle:":"Ped:")+target.Handle;
            PendingOfficerSearches[key]=new PendingOfficerSearch{Target=target,TargetType=target is Vehicle?"vehicle":"person",Items=items,Positive=positive,Specialty=specialty,K9Name=k9,ExpiresUtc=DateTime.UtcNow.AddMinutes(15)};
        }

        private static void ProcessPendingOfficerSearches(Assembly pr,Assembly nexus)
        {
            if(PendingOfficerSearches.Count==0||DateTime.UtcNow<_nextOfficerSearchReconciliationUtc)return;_nextOfficerSearchReconciliationUtc=DateTime.UtcNow.AddSeconds(2);
            foreach(string key in PendingOfficerSearches.Keys.ToArray())
            {
                PendingOfficerSearch pending=PendingOfficerSearches[key];if(DateTime.UtcNow>pending.ExpiresUtc){PendingOfficerSearches.Remove(key);continue;}
                if(!pending.SearchCompleted&&pending.Target!=null&&pending.Target.Exists())pending.SearchCompleted=HasOfficerSearched(pr,pending.Target);
                if(!pending.SearchCompleted||nexus==null)continue;
                string itemSummary=string.IsNullOrWhiteSpace(pending.Items)?"No items found.":"Items found: "+pending.Items+".";
                string match=pending.Positive?" Prior K9 indication: positive "+pending.Specialty.ToLowerInvariant()+".":" Prior K9 indication: negative.";
                string sink;if(TryAppendNexusIncidentNote(nexus,"AdvancedK9 officer "+pending.TargetType+" search — "+itemSummary+match,out sink))
                {
                    Game.LogTrivial("AdvancedK9 bridge: reconciled officer search with "+sink+" ("+(string.IsNullOrWhiteSpace(pending.Items)?0:pending.Items.Split('|').Length)+" item entries).");PendingOfficerSearches.Remove(key);
                }
            }
        }

        private static bool HasOfficerSearched(Assembly pr,Entity target)
        {
            if(pr==null)return false;string methodName=target is Vehicle?"GetHasVehicleBeenSearched":"GetHasPedBeenSearched";
            foreach(Type type in Types(new[]{pr}))foreach(MethodInfo method in type.GetMethods(BindingFlags.Public|BindingFlags.Static).Where(m=>m.Name.Equals(methodName,StringComparison.OrdinalIgnoreCase)&&m.ReturnType==typeof(bool)))
            {
                ParameterInfo[] p=method.GetParameters();object argument;if(p.Length!=1||!TryEntityArgument(p[0],target,out argument))continue;
                try{return (bool)method.Invoke(null,new[]{argument});}catch{}
            }
            return false;
        }

        private static bool TryAppendNexusIncidentNote(Assembly nexus,string narrative,out string sink)
        {
            sink="";string number=ResolveNexusIncidentNumber(nexus);if(string.IsNullOrWhiteSpace(number)){sink="NexusMDT active report unavailable";return false;}
            foreach(Type type in Types(new[]{nexus}).Where(t=>(t.FullName??"").IndexOf("NexusMDT.Api",StringComparison.OrdinalIgnoreCase)>=0||t.Name.Equals("DispatchBridge",StringComparison.OrdinalIgnoreCase)))
            foreach(MethodInfo method in type.GetMethods(BindingFlags.Public|BindingFlags.Static|BindingFlags.Instance).Where(m=>m.Name.Equals("AppendIncidentNote",StringComparison.OrdinalIgnoreCase)))
            {
                object instance=null;if(!method.IsStatic&&!TryGetPublicSingleton(type,out instance))continue;ParameterInfo[] p=method.GetParameters();if(p.Length!=2||p.Any(x=>x.ParameterType!=typeof(string)))continue;
                object[] args=new object[2];for(int i=0;i<2;i++)args[i]=(p[i].Name??"").IndexOf("number",StringComparison.OrdinalIgnoreCase)>=0?number:narrative;
                try{method.Invoke(instance,args);sink=type.FullName+"."+method.Name+" report "+number;return true;}catch(Exception ex){Game.LogTrivial("AdvancedK9 bridge: Nexus incident-note writer failed: "+Unwrap(ex).Message);}
            }
            sink="NexusMDT public AppendIncidentNote unavailable";return false;
        }

        private static string ResolveNexusIncidentNumber(Assembly nexus)
        {
            if(nexus==null)return "";string[] names={"GetCurrentIncidentNumber","GetActiveIncidentNumber","GetCurrentReportNumber","GetActiveReportNumber","GetCurrentCallNumber","GetActiveCallNumber","CurrentIncidentNumber","ActiveIncidentNumber","CurrentReportNumber","ActiveReportNumber","CurrentCallNumber","ActiveCallNumber"};
            foreach(Type type in Types(new[]{nexus}).Where(t=>(t.FullName??"").IndexOf("NexusMDT.Api",StringComparison.OrdinalIgnoreCase)>=0||t.Name.Equals("DispatchBridge",StringComparison.OrdinalIgnoreCase)||t.Name.Equals("NexusApi",StringComparison.OrdinalIgnoreCase)))
            {
                foreach(PropertyInfo property in type.GetProperties(BindingFlags.Public|BindingFlags.Static).Where(p=>names.Any(n=>p.Name.Equals(n,StringComparison.OrdinalIgnoreCase))&&p.PropertyType==typeof(string)))try{string value=property.GetValue(null,null) as string;if(IsPlausibleIncidentNumber(value))return value;}catch{}
                foreach(MethodInfo method in type.GetMethods(BindingFlags.Public|BindingFlags.Static).Where(m=>names.Any(n=>m.Name.Equals(n,StringComparison.OrdinalIgnoreCase))&&m.ReturnType==typeof(string)&&m.GetParameters().Length==0))try{string value=method.Invoke(null,null) as string;if(IsPlausibleIncidentNumber(value))return value;}catch{}
                foreach(PropertyInfo property in type.GetProperties(BindingFlags.Public|BindingFlags.Static).Where(p=>ContainsAny(p.Name,"CurrentCall","ActiveCall","CurrentIncident","ActiveIncident")))try{string value=ReadNumberFromObject(property.GetValue(null,null));if(IsPlausibleIncidentNumber(value))return value;}catch{}
            }
            return "";
        }

        private static string ReadNumberFromObject(object value)
        {
            if(value==null)return "";foreach(string name in new[]{"Number","ReportNumber","IncidentNumber","CallNumber"}){object candidate=ReadInstanceMember(value,name);if(candidate!=null)return Convert.ToString(candidate);}return "";
        }

        private static bool IsPlausibleIncidentNumber(string value)=>!string.IsNullOrWhiteSpace(value)&&value.Length<=80&&value.IndexOf("AdvancedK9",StringComparison.OrdinalIgnoreCase)<0;

        private static bool TryNamedWriter(Assembly assembly,IEnumerable<string> names,Entity target,bool positive,string specialty,string k9,string id,string narrative,out string sink)
        {
            sink="";
            foreach(Type type in Types(new[]{assembly}).OrderByDescending(t=>(t.FullName??"").IndexOf("NexusApi",StringComparison.OrdinalIgnoreCase)>=0))
            foreach(MethodInfo method in type.GetMethods(BindingFlags.Public|BindingFlags.Static|BindingFlags.Instance).Where(m=>names.Any(n=>m.Name.Equals(n,StringComparison.OrdinalIgnoreCase))))
            {
                object instance=null;if(!method.IsStatic&&!TryGetPublicSingleton(type,out instance))continue;
                object[] args;if(!TryBindWriter(method,target,positive,specialty,k9,id,narrative,out args))continue;
                try{method.Invoke(instance,args);sink=type.FullName+"."+method.Name;return true;}catch(Exception ex){Game.LogTrivial("AdvancedK9 bridge: writer "+type.FullName+"."+method.Name+" rejected request: "+Unwrap(ex).Message);}
            }
            return false;
        }

        private static bool TryBindWriter(MethodInfo method,Entity target,bool positive,string specialty,string k9,string id,string narrative,out object[] args)
        {
            ParameterInfo[] p=method.GetParameters();args=new object[p.Length];bool meaningful=false;
            for(int i=0;i<p.Length;i++)
            {
                Type type=p[i].ParameterType;string name=(p[i].Name??"").ToLowerInvariant();
                if(target!=null&&type.IsInstanceOfType(target)){args[i]=target;meaningful=true;}
                else if(type==typeof(string))
                {
                    if(ContainsAny(name,"specialty","category","result","alerttype"))args[i]=specialty;
                    else if(ContainsAny(name,"dog","k9name"))args[i]=k9;
                    else if(ContainsAny(name,"request","correlation","eventid"))args[i]=id;
                    else if(ContainsAny(name,"source","plugin","origin"))args[i]="AdvancedK9";
                    else args[i]=narrative;
                    meaningful=true;
                }
                else if(type==typeof(bool)){args[i]=positive;meaningful=true;}
                else if(type==typeof(DateTime)){args[i]=DateTime.UtcNow;}
                else if(type.IsEnum){try{args[i]=Enum.Parse(type,specialty,true);}catch{args[i]=Enum.GetValues(type).GetValue(0);}}
                else if((type==typeof(int)||type==typeof(long))&&ContainsAny(name,"handle","entity","ped","vehicle")){long handle;long.TryParse(target==null?"0":target.Handle.ToString(),out handle);args[i]=type==typeof(int)?(object)(int)handle:handle;meaningful=true;}
                else if(p[i].HasDefaultValue)args[i]=p[i].DefaultValue;
                else return false;
            }
            return meaningful;
        }

        private static bool TryGetPublicSingleton(Type type,out object value)
        {
            value=null;foreach(string name in new[]{"Instance","Current"})try{PropertyInfo p=type.GetProperty(name,BindingFlags.Public|BindingFlags.Static);if(p!=null&&type.IsAssignableFrom(p.PropertyType)){value=p.GetValue(null,null);if(value!=null)return true;}}catch{}
            return false;
        }

        private static void LogIntegrationSurfaces(Assembly cdf,Assembly pr,Assembly nexus)
        {
            Game.LogTrivial("AdvancedK9 bridge inventory integration: CDF="+(cdf!=null)+", PR="+(pr!=null)+", NexusMDT="+(nexus!=null)+"; AdvancedK9 never modifies third-party inventory files or records.");
            foreach(Assembly assembly in new[]{cdf,pr,nexus}.Where(a=>a!=null))foreach(Type type in Types(new[]{assembly}))foreach(MethodInfo method in type.GetMethods(BindingFlags.Public|BindingFlags.Static|BindingFlags.Instance).Where(m=>ContainsAny(m.Name,"GetPedData","GetVehicleData","GetPedSearchItems","GetVehicleSearchItems","AppendIncidentNote","CaptureSearch","RecordK9Indication")))Game.LogTrivial("AdvancedK9 bridge API surface: "+type.FullName+"."+method.Name+"("+string.Join(",",method.GetParameters().Select(p=>p.ParameterType.Name+" "+p.Name))+ ").");
        }

        private sealed class PendingOfficerSearch
        {
            public Entity Target;public string TargetType,Items,Specialty,K9Name;public bool Positive,SearchCompleted;public DateTime ExpiresUtc;
        }

        private static T FindEntity<T>(IEnumerable<Assembly> assemblies,IEnumerable<string> names) where T:Entity
        {
            foreach(Type type in Types(assemblies))
            {
                foreach(PropertyInfo property in Properties(type).Where(p=>names.Any(n=>p.Name.Equals(n,StringComparison.OrdinalIgnoreCase))&&typeof(T).IsAssignableFrom(p.PropertyType)))
                    try{var value=property.GetValue(null,null) as T;if(value!=null&&value.Exists())return value;}catch{}
                foreach(MethodInfo method in Methods(type).Where(m=>names.Any(n=>m.Name.Equals(n,StringComparison.OrdinalIgnoreCase))&&typeof(T).IsAssignableFrom(m.ReturnType)&&m.GetParameters().Length==0))
                    try{var value=method.Invoke(null,null) as T;if(value!=null&&value.Exists())return value;}catch{}
            }
            return null;
        }

        private static object GetRecord(IEnumerable<Assembly> assemblies,Entity entity)
        {
            if(entity==null||!entity.Exists())return null;
            foreach(Type type in Types(assemblies))foreach(MethodInfo method in Methods(type).Where(m=>m.Name.IndexOf("Get",StringComparison.OrdinalIgnoreCase)>=0&&ContainsAny(m.Name,"Data","Record","Inventory","Items")))
            {
                ParameterInfo[] p=method.GetParameters();if(p.Length!=1||!p[0].ParameterType.IsInstanceOfType(entity))continue;
                try{object value=method.Invoke(null,new object[]{entity});if(value!=null)return value;}catch{}
            }
            return null;
        }

        private static IEnumerable<Type> Types(IEnumerable<Assembly> assemblies){foreach(Assembly assembly in assemblies){Type[] types;string key=assembly.FullName??assembly.GetName().Name;if(!TypeCache.TryGetValue(key,out types)){try{types=assembly.GetTypes();}catch(ReflectionTypeLoadException ex){types=ex.Types.Where(t=>t!=null).ToArray();}catch{continue;}TypeCache[key]=types;}foreach(Type type in types)yield return type;}}
        private static IEnumerable<MethodInfo> Methods(Type type){try{return type.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static);}catch{return Array.Empty<MethodInfo>();}}
        private static IEnumerable<PropertyInfo> Properties(Type type){try{return type.GetProperties(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static);}catch{return Array.Empty<PropertyInfo>();}}
        private static Assembly Find(params string[] names)=>AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a=>names.Any(n=>a.GetName().Name.IndexOf(n,StringComparison.OrdinalIgnoreCase)>=0));
        private static string VersionOf(Assembly assembly)=>assembly==null?"":assembly.GetName().Name+" "+assembly.GetName().Version;
        private static string HandleOf(Entity entity)=>entity!=null&&entity.Exists()?entity.Handle.ToString():"";
        private static string Encode(string value)=>Convert.ToBase64String(Encoding.UTF8.GetBytes(value??""));
        private static string Read(IDictionary<string,string> map,string key){string value;return map!=null&&map.TryGetValue(key,out value)?value:"";}
        private static bool ReadBool(IDictionary<string,string> map,string key,bool fallback){bool value;string text=Read(map,key);return string.IsNullOrWhiteSpace(text)?fallback:bool.TryParse(text,out value)?value:fallback;}
        private static Exception Unwrap(Exception ex)=>ex is TargetInvocationException&&ex.InnerException!=null?ex.InnerException:ex;
        private static bool ContainsAny(string value,params string[] terms)=>terms.Any(t=>value.IndexOf(t,StringComparison.OrdinalIgnoreCase)>=0);
        private static string Flatten(object value,int depth,HashSet<object> visited){if(value==null||depth>3)return "";Type type=value.GetType();if(type.IsPrimitive||value is string||value is decimal||type.IsEnum)return Convert.ToString(value);if(!type.IsValueType&&!visited.Add(value))return "";var sb=new StringBuilder();if(value is IEnumerable enumerable){int count=0;foreach(object item in enumerable){if(count++>=40)break;sb.Append(' ').Append(Flatten(item,depth+1,visited));}return sb.ToString();}foreach(PropertyInfo property in type.GetProperties(BindingFlags.Public|BindingFlags.Instance).Where(p=>p.CanRead&&p.GetIndexParameters().Length==0).Take(40)){try{sb.Append(' ').Append(property.Name).Append('=').Append(Flatten(property.GetValue(value,null),depth+1,visited));}catch{}}return sb.ToString();}
        private sealed class ReferenceComparer:IEqualityComparer<object>{public static readonly ReferenceComparer Instance=new ReferenceComparer();public new bool Equals(object x,object y)=>ReferenceEquals(x,y);public int GetHashCode(object obj)=>System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);}
    }
}
