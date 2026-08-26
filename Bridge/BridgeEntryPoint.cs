using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Rage;

namespace AdvancedK9.LSPDFRBridge
{
    public static class EntryPoint
    {
        private static readonly string DirectoryPath=Path.Combine("Plugins","LSPDFR","AdvancedK9");
        private static readonly string StatePath=Path.Combine(DirectoryPath,"CompatibilityBridge.state");
        private static readonly string TempPath=StatePath+".tmp";
        private static readonly string[] VehicleNames={"GetActiveStopVehicle","GetCurrentStopVehicle","GetTrafficStopVehicle","GetPulloverVehicle","ActiveStopVehicle","CurrentStopVehicle","TrafficStopVehicle","PulloverVehicle","ContextVehicle","SelectedVehicle"};
        private static readonly string[] PedNames={"GetActiveInteractionPed","GetCurrentStopPed","GetStoppedPed","GetSelectedPed","ActiveInteractionPed","CurrentStopPed","StoppedPed","ContextPed","SelectedPed"};
        private static readonly string[] PursuitNames={"GetActivePursuitSuspect","GetCurrentPursuitSuspect","ActivePursuitSuspect","CurrentPursuitSuspect","GetSuspect","PursuitSuspect"};

        public static void Main()
        {
            Game.LogTrivial("AdvancedK9 bridge: started inside the LSPDFR plugin AppDomain.");
            while(true)
            {
                try{Publish();}catch(Exception ex){Game.LogTrivial("AdvancedK9 bridge publish error: "+ex.Message);}
                GameFiber.Sleep(1000);
            }
        }

        public static void Finally()
        {
            try{if(File.Exists(StatePath))File.Delete(StatePath);if(File.Exists(TempPath))File.Delete(TempPath);}catch{}
        }

        private static void Publish()
        {
            Assembly pr=Find("PolicingRedefined","Policing Redefined"),cdf=Find("CommonDataFramework"),stp=Find("StopThePed","Stop The Ped","STP");
            var active=(pr!=null||cdf!=null)?new[]{pr,cdf}.Where(a=>a!=null).ToArray():stp!=null?new[]{stp}:Array.Empty<Assembly>();
            Vehicle vehicle=FindEntity<Vehicle>(active,VehicleNames);Ped ped=FindEntity<Ped>(active,PedNames);Ped pursuit=FindEntity<Ped>(active,PursuitNames);
            string vehicleData=Flatten(GetRecord(active,vehicle),0,new HashSet<object>(ReferenceComparer.Instance));
            string pedData=Flatten(GetRecord(active,ped),0,new HashSet<object>(ReferenceComparer.Instance));
            var lines=new[]{
                "Protocol=1",
                "HeartbeatUtcTicks="+DateTime.UtcNow.Ticks,
                "PR="+(pr!=null),"PRVersion="+VersionOf(pr),
                "CDF="+(cdf!=null),"CDFVersion="+VersionOf(cdf),
                "STP="+(stp!=null),"STPVersion="+VersionOf(stp),
                "ActiveVehicleHandle="+HandleOf(vehicle),
                "ActivePedHandle="+HandleOf(ped),
                "PursuitPedHandle="+HandleOf(pursuit),
                "ActiveVehicleData="+Encode(vehicleData),
                "ActivePedData="+Encode(pedData)
            };
            if(!Directory.Exists(DirectoryPath))Directory.CreateDirectory(DirectoryPath);
            File.WriteAllLines(TempPath,lines);
            File.Copy(TempPath,StatePath,true);File.Delete(TempPath);
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

        private static IEnumerable<Type> Types(IEnumerable<Assembly> assemblies){foreach(Assembly assembly in assemblies){Type[] types;try{types=assembly.GetTypes();}catch(ReflectionTypeLoadException ex){types=ex.Types.Where(t=>t!=null).ToArray();}catch{continue;}foreach(Type type in types)yield return type;}}
        private static IEnumerable<MethodInfo> Methods(Type type){try{return type.GetMethods(BindingFlags.Public|BindingFlags.Static);}catch{return Array.Empty<MethodInfo>();}}
        private static IEnumerable<PropertyInfo> Properties(Type type){try{return type.GetProperties(BindingFlags.Public|BindingFlags.Static);}catch{return Array.Empty<PropertyInfo>();}}
        private static Assembly Find(params string[] names)=>AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a=>names.Any(n=>a.GetName().Name.IndexOf(n,StringComparison.OrdinalIgnoreCase)>=0));
        private static string VersionOf(Assembly assembly)=>assembly==null?"":assembly.GetName().Name+" "+assembly.GetName().Version;
        private static string HandleOf(Entity entity)=>entity!=null&&entity.Exists()?entity.Handle.ToString():"";
        private static string Encode(string value)=>Convert.ToBase64String(Encoding.UTF8.GetBytes(value??""));
        private static bool ContainsAny(string value,params string[] terms)=>terms.Any(t=>value.IndexOf(t,StringComparison.OrdinalIgnoreCase)>=0);
        private static string Flatten(object value,int depth,HashSet<object> visited){if(value==null||depth>3)return "";Type type=value.GetType();if(type.IsPrimitive||value is string||value is decimal||type.IsEnum)return Convert.ToString(value);if(!type.IsValueType&&!visited.Add(value))return "";var sb=new StringBuilder();if(value is IEnumerable enumerable){int count=0;foreach(object item in enumerable){if(count++>=40)break;sb.Append(' ').Append(Flatten(item,depth+1,visited));}return sb.ToString();}foreach(PropertyInfo property in type.GetProperties(BindingFlags.Public|BindingFlags.Instance).Where(p=>p.CanRead&&p.GetIndexParameters().Length==0).Take(40)){try{sb.Append(' ').Append(property.Name).Append('=').Append(Flatten(property.GetValue(value,null),depth+1,visited));}catch{}}return sb.ToString();}
        private sealed class ReferenceComparer:IEqualityComparer<object>{public static readonly ReferenceComparer Instance=new ReferenceComparer();public new bool Equals(object x,object y)=>ReferenceEquals(x,y);public int GetHashCode(object obj)=>System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);}
    }
}
