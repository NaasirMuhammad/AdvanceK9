using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Rage;

namespace AdvancedK9
{
    internal sealed class VehicleSeatProfile
    {
        public float X; public float Y; public float Z;
        public VehicleSeatProfile(float x,float y,float z){X=x;Y=y;Z=z;}
    }

    internal sealed class VehicleSeatProfiles
    {
        private readonly string _path;
        private readonly string _log=Path.Combine("Plugins","LSPDFR","AdvancedK9","VehicleSeatConfigurationLog.csv");
        private readonly Dictionary<string,VehicleSeatProfile> _profiles=new Dictionary<string,VehicleSeatProfile>(StringComparer.OrdinalIgnoreCase);
        private readonly float _defaultX,_defaultY,_defaultZ;
        public VehicleSeatProfiles(float x,float y,float z,string profileId="rex")
        {
            string safe=new string((profileId??"rex").ToLowerInvariant().Where(c=>char.IsLetterOrDigit(c)||c=='-'||c=='_').ToArray());if(string.IsNullOrWhiteSpace(safe))safe="rex";
            _path=Path.Combine("Plugins","LSPDFR","AdvancedK9","Profiles",safe+".seats.ini");_defaultX=x;_defaultY=y;_defaultZ=z;
            try{string legacy=Path.Combine("Plugins","LSPDFR","AdvancedK9","VehicleSeatConfigurations.ini");string directory=Path.GetDirectoryName(_path);if(!Directory.Exists(directory))Directory.CreateDirectory(directory);if(safe=="rex"&&!File.Exists(_path)&&File.Exists(legacy))File.Copy(legacy,_path,false);}catch(Exception ex){Game.LogTrivial("AdvancedK9 seat profile migration: "+ex.Message);}Load();
        }
        public VehicleSeatProfile Get(Vehicle vehicle){Load();string key=Key(vehicle);VehicleSeatProfile value;if(_profiles.TryGetValue(key,out value)){Game.LogTrivial("AdvancedK9 seat profile loaded: "+VehicleName(vehicle)+" ["+key+"] X="+F(value.X)+" Y="+F(value.Y)+" Z="+F(value.Z));return new VehicleSeatProfile(value.X,value.Y,value.Z);}Game.LogTrivial("AdvancedK9 seat profile default used: "+VehicleName(vehicle)+" ["+key+"]");return new VehicleSeatProfile(_defaultX,_defaultY,_defaultZ);}
        public string VehicleName(Vehicle vehicle){if(vehicle==null||!vehicle.Exists())return "UNKNOWN";try{return Rage.Native.NativeFunction.Natives.GET_DISPLAY_NAME_FROM_VEHICLE_MODEL<string>(vehicle.Model.Hash)??Key(vehicle);}catch{return Key(vehicle);}}
        public void Save(Vehicle vehicle,VehicleSeatProfile value)
        {
            if(vehicle==null||!vehicle.Exists())return;string key=Key(vehicle);_profiles[key]=new VehicleSeatProfile(value.X,value.Y,value.Z);
            string directory=Path.GetDirectoryName(_path);if(!Directory.Exists(directory))Directory.CreateDirectory(directory);
            var lines=new List<string>{"; AdvancedK9 per-vehicle rear-seat calibration","; Format: modelHash=X,Y,Z"};foreach(var pair in _profiles)lines.Add(pair.Key+"="+F(pair.Value.X)+","+F(pair.Value.Y)+","+F(pair.Value.Z));File.WriteAllLines(_path,lines.ToArray());
            if(!File.Exists(_log))File.AppendAllText(_log,"Timestamp,Vehicle,ModelHash,OffsetX,OffsetY,OffsetZ"+Environment.NewLine);
            File.AppendAllText(_log,string.Join(",",DateTime.Now.ToString("s"),Quote(VehicleName(vehicle)),key,F(value.X),F(value.Y),F(value.Z))+Environment.NewLine);
            Game.LogTrivial("AdvancedK9 seat configuration saved: "+VehicleName(vehicle)+" ["+key+"] X="+F(value.X)+" Y="+F(value.Y)+" Z="+F(value.Z));
        }
        private void Load(){try{_profiles.Clear();if(!File.Exists(_path))return;foreach(string raw in File.ReadAllLines(_path)){string line=raw.Trim();if(line.Length==0||line.StartsWith(";")||line.StartsWith("#"))continue;int split=line.IndexOf('=');if(split<1)continue;string[] p=line.Substring(split+1).Split(',');float x,y,z;if(p.Length==3&&float.TryParse(p[0],NumberStyles.Float,CultureInfo.InvariantCulture,out x)&&float.TryParse(p[1],NumberStyles.Float,CultureInfo.InvariantCulture,out y)&&float.TryParse(p[2],NumberStyles.Float,CultureInfo.InvariantCulture,out z))_profiles[line.Substring(0,split).Trim()]=new VehicleSeatProfile(x,y,z);}}catch(Exception ex){Game.LogTrivial("AdvancedK9 seat profile load: "+ex.Message);}}
        private static string Key(Vehicle vehicle){return vehicle==null||!vehicle.Exists()?"0":((uint)vehicle.Model.Hash).ToString("X8");}
        private static string F(float v){return v.ToString("0.000",CultureInfo.InvariantCulture);}private static string Quote(string v){return "\""+(v??"").Replace("\"","\"\"")+"\"";}
    }
}
