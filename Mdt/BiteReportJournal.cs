using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace AdvancedK9.Mdt
{
    internal sealed class BiteReportEvent
    {
        internal string Episode,Stage,K9,PedHandle,Model,Detail,Position;
        internal DateTime Utc;
        internal string Path;
    }

    internal sealed class BiteReportJournal
    {
        internal static readonly string[] Stages={"Bite","Release","EMSRequested","TreatmentStarted","TreatmentCompleted","TreatmentObserved","Transported","RetainedForArrest","EMSFailed","Cuffed","HospitalRequired"};
        private readonly string _directory;
        internal BiteReportJournal(string directory){_directory=directory;}
        internal static bool ValidReport(string number)=>number!=null&&Regex.IsMatch(number,@"\A[A-Za-z0-9][A-Za-z0-9._/-]{0,79}\z");
        private string EpisodePath(string id,string extension)
        {
            Guid parsed;if(!Guid.TryParseExact(id,"N",out parsed))throw new ArgumentException("Invalid episode id");
            return System.IO.Path.Combine(_directory,id+extension);
        }
        private static void Atomic(string path,IEnumerable<string> lines)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            string temp=path+".tmp-"+Guid.NewGuid().ToString("N");
            try{File.WriteAllLines(temp,lines,Encoding.UTF8);File.Move(temp,path);}
            catch(IOException){if(!File.Exists(path))throw;}
            finally{if(File.Exists(temp))File.Delete(temp);}
        }
        private static string Enc(string value)=>Convert.ToBase64String(Encoding.UTF8.GetBytes(value??""));
        internal bool Add(BiteReportEvent e)
        {
            int order=Array.IndexOf(Stages,e.Stage);if(order<0)throw new ArgumentException("Unknown report event");
            string path=EpisodePath(e.Episode,"-"+order.ToString("00")+"-"+e.Stage+".event");
            if(File.Exists(path))return false;
            Atomic(path,new[]{"Episode="+Enc(e.Episode),"Stage="+Enc(e.Stage),"Utc="+Enc(e.Utc.ToString("O")),"K9="+Enc(e.K9),"PedHandle="+Enc(e.PedHandle),"Model="+Enc(e.Model),"Detail="+Enc(e.Detail),"Position="+Enc(e.Position)});
            return true;
        }
        internal bool Enabled=>!File.Exists(System.IO.Path.Combine(_directory,"disabled.flag"));
        internal void SetEnabled(bool enabled)
        {
            Directory.CreateDirectory(_directory);string path=System.IO.Path.Combine(_directory,"disabled.flag");
            if(enabled){if(File.Exists(path))File.Delete(path);}
            else File.WriteAllText(path,"MDT sharing disabled by AdvancedK9 configuration.");
        }
        internal IEnumerable<BiteReportEvent> Events(bool pendingOnly=false)
        {
            if(!Directory.Exists(_directory))yield break;
            foreach(string path in Directory.GetFiles(_directory,"*.event").OrderBy(p=>p,StringComparer.Ordinal))
            {
                if(pendingOnly&&File.Exists(path+".sent"))continue;
                BiteReportEvent e=null;
                try
                {
                    var map=File.ReadAllLines(path).Where(l=>l.Contains("=")).Select(l=>new{Key=l.Substring(0,l.IndexOf('=')),Value=Encoding.UTF8.GetString(Convert.FromBase64String(l.Substring(l.IndexOf('=')+1)))}).ToDictionary(x=>x.Key,x=>x.Value);
                    Guid id;DateTime utc;
                    if(Guid.TryParseExact(map["Episode"],"N",out id)&&Stages.Contains(map["Stage"])&&DateTime.TryParseExact(map["Utc"],"O",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.RoundtripKind,out utc))
                        e=new BiteReportEvent{Episode=map["Episode"],Stage=map["Stage"],Utc=utc.ToUniversalTime(),K9=map["K9"],PedHandle=map["PedHandle"],Model=map["Model"],Detail=map["Detail"],Position=map["Position"],Path=path};
                }catch{}
                if(e!=null)yield return e;
            }
        }
        internal string Report(string id){string p=EpisodePath(id,".report");return File.Exists(p)?File.ReadAllText(p).Trim():"";}
        internal bool Bind(string id,string report)
        {
            if(!ValidReport(report))return false;
            Atomic(EpisodePath(id,".report"),new[]{report});return Report(id)==report;
        }
        internal bool Seen(string id)=>File.Exists(EpisodePath(id,".seen"));
        internal void MarkSeen(string id)=>Atomic(EpisodePath(id,".seen"),new[]{"Automatic binding checked once at episode arrival."});
        internal string Subject(string id)=>ReadText(EpisodePath(id,".subject"));
        internal void SaveSubject(string id,string subject)=>Atomic(EpisodePath(id,".subject"),new[]{subject});
        internal static bool Accepted(BiteReportEvent e)=>File.Exists(e.Path+".sent");
        internal static bool Uncertain(BiteReportEvent e)=>File.Exists(e.Path+".sending");
        internal static bool BeginDelivery(BiteReportEvent e)
        {
            if(Accepted(e)||Uncertain(e))return false;
            Atomic(e.Path+".sending",new[]{DateTime.UtcNow.ToString("O")});return true;
        }
        internal static void Accept(BiteReportEvent e,string report)
        {Atomic(e.Path+".sent",new[]{report});if(File.Exists(e.Path+".sending"))File.Delete(e.Path+".sending");}
        internal static void Reject(BiteReportEvent e){if(File.Exists(e.Path+".sending"))File.Delete(e.Path+".sending");}
        internal void Status(string id,string status)=>File.WriteAllText(EpisodePath(id,".status"),status);
        internal string Status(string id)=>ReadText(EpisodePath(id,".status"));
        private static string ReadText(string path)=>File.Exists(path)?File.ReadAllText(path).Trim():"";
        internal void RetryUncertain(string id)
        {foreach(var e in Events().Where(e=>e.Episode==id&&!Accepted(e)))Reject(e);}
    }
}

