using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace AdvancedK9.API
{
    public sealed class K9ApiSnapshot
    {
        public int Protocol { get; set; } = 1;
        public long UpdatedUtcTicks { get; set; }
        public bool OnDuty { get; set; }
        public bool Deployed { get; set; }
        public string K9Name { get; set; } = "";
        public string State { get; set; } = "";
        public int DogHandle { get; set; }
        public int HandlerHandle { get; set; }
        public int Health { get; set; }
        public int Stamina { get; set; }
        public int Trust { get; set; }
        public int TrainingLevel { get; set; }
        public string Certifications { get; set; } = "";
        public string ActiveContextId { get; set; } = "";
    }

    public sealed class K9ApiCommandRequest
    {
        public string RequestId { get; set; } = Guid.NewGuid().ToString("N");
        public long CreatedUtcTicks { get; set; } = DateTime.UtcNow.Ticks;
        public string Command { get; set; } = "";
        public string ContextId { get; set; } = "";
        public int TargetHandle { get; set; }
        public string TargetType { get; set; } = "";
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float TargetX { get; set; }
        public float TargetY { get; set; }
        public float TargetZ { get; set; }
        public string Details { get; set; } = "";
    }

    public sealed class K9ApiCommandResult
    {
        public string RequestId { get; set; } = "";
        public long CompletedUtcTicks { get; set; } = DateTime.UtcNow.Ticks;
        public bool Accepted { get; set; }
        public string Detail { get; set; } = "";
    }

    public sealed class K9TrackingEvent
    {
        public string ContextId { get; set; } = "";
        public int DogHandle { get; set; }
        public int TargetHandle { get; set; }
        public float DogX { get; set; }
        public float DogY { get; set; }
        public float DogZ { get; set; }
    }

    public static class AdvancedK9TrackingEvents
    {
        public static event Action<K9TrackingEvent> TrackingTick;
        public static event Action<K9TrackingEvent> TrackingCompleted;

        internal static void PublishTrackingTick(K9TrackingEvent tracking)
        {
            PublishSafely(TrackingTick,tracking);
        }

        internal static void PublishTrackingCompleted(K9TrackingEvent tracking)
        {
            PublishSafely(TrackingCompleted,tracking);
        }

        private static void PublishSafely(Action<K9TrackingEvent> handlers,K9TrackingEvent tracking)
        {
            if(handlers==null||tracking==null)return;
            foreach(Delegate subscriber in handlers.GetInvocationList())
            {
                var handler=(Action<K9TrackingEvent>)subscriber;
                try{handler(tracking);}catch{}
            }
        }
    }

    public static class AdvancedK9Api
    {
        public const int ProtocolVersion = 1;
        public static readonly string DirectoryPath=Path.Combine("Plugins","LSPDFR","AdvancedK9");
        public static readonly string SnapshotPath=Path.Combine(DirectoryPath,"AdvancedK9Api.state");
        public static readonly string RequestPath=Path.Combine(DirectoryPath,"AdvancedK9Api.request");
        public static readonly string ResultPath=Path.Combine(DirectoryPath,"AdvancedK9Api.result");
        public static readonly string CalloutRequestPath=Path.Combine(DirectoryPath,"AdvancedK9Callout.request");

        public static bool TryGetSnapshot(out K9ApiSnapshot snapshot)
        {
            snapshot=null;
            try
            {
                if(!File.Exists(SnapshotPath))return false;
                var values=ReadValues(SnapshotPath);
                long updated=ReadLong(values,"UpdatedUtcTicks");
                if(updated<=0||DateTime.UtcNow.Ticks-updated>TimeSpan.FromSeconds(5).Ticks)return false;
                snapshot=new K9ApiSnapshot{
                    Protocol=ReadInt(values,"Protocol"),
                    UpdatedUtcTicks=updated,
                    OnDuty=ReadBool(values,"OnDuty"),
                    Deployed=ReadBool(values,"Deployed"),
                    K9Name=Read(values,"K9Name"),
                    State=Read(values,"State"),
                    DogHandle=ReadInt(values,"DogHandle"),
                    HandlerHandle=ReadInt(values,"HandlerHandle"),
                    Health=ReadInt(values,"Health"),
                    Stamina=ReadInt(values,"Stamina"),
                    Trust=ReadInt(values,"Trust"),
                    TrainingLevel=ReadInt(values,"TrainingLevel"),
                    Certifications=Read(values,"Certifications"),
                    ActiveContextId=Read(values,"ActiveContextId")
                };
                return snapshot.Protocol==ProtocolVersion;
            }
            catch{return false;}
        }

        public static string SendCommand(string command,string contextId="",int targetHandle=0,string targetType="",float x=0,float y=0,float z=0,string details="",float targetX=0,float targetY=0,float targetZ=0)
        {
            var request=new K9ApiCommandRequest{Command=command??"",ContextId=contextId??"",TargetHandle=targetHandle,TargetType=targetType??"",X=x,Y=y,Z=z,TargetX=targetX,TargetY=targetY,TargetZ=targetZ,Details=details??""};
            WriteValues(RequestPath,new[]{
                Pair("Protocol",ProtocolVersion),Pair("RequestId",request.RequestId),Pair("CreatedUtcTicks",request.CreatedUtcTicks),
                Pair("Command",request.Command),Pair("ContextId",request.ContextId),Pair("TargetHandle",request.TargetHandle),
                Pair("TargetType",request.TargetType),Pair("X",request.X),Pair("Y",request.Y),Pair("Z",request.Z),
                Pair("TargetX",request.TargetX),Pair("TargetY",request.TargetY),Pair("TargetZ",request.TargetZ),Pair("Details",request.Details)
            });
            return request.RequestId;
        }

        public static void RequestCallout(string calloutName)
        {
            WriteValues(CalloutRequestPath,new[]{Pair("RequestedUtcTicks",DateTime.UtcNow.Ticks),Pair("CalloutName",calloutName??"")});
        }

        public static bool TryTakeCalloutRequest(out string calloutName)
        {
            calloutName="";
            try
            {
                if(!File.Exists(CalloutRequestPath))return false;
                var values=ReadValues(CalloutRequestPath);
                File.Delete(CalloutRequestPath);
                long requested=ReadLong(values,"RequestedUtcTicks");
                calloutName=Read(values,"CalloutName");
                return !string.IsNullOrWhiteSpace(calloutName)&&requested>0&&DateTime.UtcNow.Ticks-requested<TimeSpan.FromSeconds(15).Ticks;
            }
            catch{return false;}
        }

        public static bool TryGetResult(string requestId,out K9ApiCommandResult result)
        {
            result=null;
            try
            {
                if(!File.Exists(ResultPath))return false;var values=ReadValues(ResultPath);
                if(!Read(values,"RequestId").Equals(requestId??"",StringComparison.OrdinalIgnoreCase))return false;
                result=new K9ApiCommandResult{RequestId=requestId,CompletedUtcTicks=ReadLong(values,"CompletedUtcTicks"),Accepted=ReadBool(values,"Accepted"),Detail=Read(values,"Detail")};return true;
            }
            catch{return false;}
        }

        internal static Dictionary<string,string> ReadValues(string path)
        {
            var values=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            foreach(string line in File.ReadAllLines(path)){int split=line.IndexOf('=');if(split>0)values[line.Substring(0,split)]=Decode(line.Substring(split+1));}
            return values;
        }
        internal static void WriteValues(string path,IEnumerable<KeyValuePair<string,string>> values)
        {
            if(!Directory.Exists(DirectoryPath))Directory.CreateDirectory(DirectoryPath);
            string temp=path+".tmp";var lines=new List<string>();foreach(var pair in values)lines.Add(pair.Key+"="+Encode(pair.Value));
            File.WriteAllLines(temp,lines);File.Copy(temp,path,true);File.Delete(temp);
        }
        internal static KeyValuePair<string,string> Pair(string key,object value)=>new KeyValuePair<string,string>(key,Convert.ToString(value,CultureInfo.InvariantCulture)??"");
        internal static string Read(IDictionary<string,string> values,string key){string value;return values.TryGetValue(key,out value)?value:"";}
        internal static int ReadInt(IDictionary<string,string> values,string key){int value;return int.TryParse(Read(values,key),NumberStyles.Integer,CultureInfo.InvariantCulture,out value)?value:0;}
        internal static long ReadLong(IDictionary<string,string> values,string key){long value;return long.TryParse(Read(values,key),NumberStyles.Integer,CultureInfo.InvariantCulture,out value)?value:0;}
        internal static bool ReadBool(IDictionary<string,string> values,string key){bool value;return bool.TryParse(Read(values,key),out value)&&value;}
        internal static float ReadFloat(IDictionary<string,string> values,string key){float value;return float.TryParse(Read(values,key),NumberStyles.Float,CultureInfo.InvariantCulture,out value)?value:0f;}
        private static string Encode(string value)=>Convert.ToBase64String(Encoding.UTF8.GetBytes(value??""));
        private static string Decode(string value){try{return Encoding.UTF8.GetString(Convert.FromBase64String(value??""));}catch{return "";}}
    }

    public static class AdvancedK9ApiHost
    {
        public static void Publish(K9ApiSnapshot snapshot)
        {
            if(snapshot==null)return;snapshot.Protocol=AdvancedK9Api.ProtocolVersion;snapshot.UpdatedUtcTicks=DateTime.UtcNow.Ticks;
            AdvancedK9Api.WriteValues(AdvancedK9Api.SnapshotPath,new[]{
                AdvancedK9Api.Pair("Protocol",snapshot.Protocol),AdvancedK9Api.Pair("UpdatedUtcTicks",snapshot.UpdatedUtcTicks),
                AdvancedK9Api.Pair("OnDuty",snapshot.OnDuty),AdvancedK9Api.Pair("Deployed",snapshot.Deployed),
                AdvancedK9Api.Pair("K9Name",snapshot.K9Name),AdvancedK9Api.Pair("State",snapshot.State),
                AdvancedK9Api.Pair("DogHandle",snapshot.DogHandle),AdvancedK9Api.Pair("HandlerHandle",snapshot.HandlerHandle),
                AdvancedK9Api.Pair("Health",snapshot.Health),AdvancedK9Api.Pair("Stamina",snapshot.Stamina),
                AdvancedK9Api.Pair("Trust",snapshot.Trust),AdvancedK9Api.Pair("TrainingLevel",snapshot.TrainingLevel),
                AdvancedK9Api.Pair("Certifications",snapshot.Certifications),AdvancedK9Api.Pair("ActiveContextId",snapshot.ActiveContextId)
            });
        }

        public static bool TryReadCommand(out K9ApiCommandRequest request)
        {
            request=null;
            try
            {
                if(!File.Exists(AdvancedK9Api.RequestPath))return false;
                var values=AdvancedK9Api.ReadValues(AdvancedK9Api.RequestPath);
                request=new K9ApiCommandRequest{
                    RequestId=AdvancedK9Api.Read(values,"RequestId"),CreatedUtcTicks=AdvancedK9Api.ReadLong(values,"CreatedUtcTicks"),
                    Command=AdvancedK9Api.Read(values,"Command"),ContextId=AdvancedK9Api.Read(values,"ContextId"),
                    TargetHandle=AdvancedK9Api.ReadInt(values,"TargetHandle"),TargetType=AdvancedK9Api.Read(values,"TargetType"),
                    X=AdvancedK9Api.ReadFloat(values,"X"),Y=AdvancedK9Api.ReadFloat(values,"Y"),Z=AdvancedK9Api.ReadFloat(values,"Z"),
                    TargetX=AdvancedK9Api.ReadFloat(values,"TargetX"),TargetY=AdvancedK9Api.ReadFloat(values,"TargetY"),TargetZ=AdvancedK9Api.ReadFloat(values,"TargetZ"),
                    Details=AdvancedK9Api.Read(values,"Details")
                };
                File.Delete(AdvancedK9Api.RequestPath);
                return !string.IsNullOrWhiteSpace(request.RequestId)&&DateTime.UtcNow.Ticks-request.CreatedUtcTicks<TimeSpan.FromSeconds(15).Ticks;
            }
            catch{return false;}
        }

        public static void PublishResult(string requestId,bool accepted,string detail)
        {
            AdvancedK9Api.WriteValues(AdvancedK9Api.ResultPath,new[]{
                AdvancedK9Api.Pair("Protocol",AdvancedK9Api.ProtocolVersion),AdvancedK9Api.Pair("RequestId",requestId),
                AdvancedK9Api.Pair("CompletedUtcTicks",DateTime.UtcNow.Ticks),AdvancedK9Api.Pair("Accepted",accepted),
                AdvancedK9Api.Pair("Detail",detail??"")
            });
        }

        public static void PublishTrackingTick(string contextId,int dogHandle,int targetHandle,float dogX,float dogY,float dogZ)
        {
            AdvancedK9TrackingEvents.PublishTrackingTick(new K9TrackingEvent{ContextId=contextId??"",DogHandle=dogHandle,TargetHandle=targetHandle,DogX=dogX,DogY=dogY,DogZ=dogZ});
        }

        public static void PublishTrackingCompleted(string contextId,int dogHandle,int targetHandle,float dogX,float dogY,float dogZ)
        {
            AdvancedK9TrackingEvents.PublishTrackingCompleted(new K9TrackingEvent{ContextId=contextId??"",DogHandle=dogHandle,TargetHandle=targetHandle,DogX=dogX,DogY=dogY,DogZ=dogZ});
        }
    }
}
