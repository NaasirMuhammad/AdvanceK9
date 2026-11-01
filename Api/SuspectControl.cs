using System;
using System.Globalization;
using System.IO;

namespace AdvancedK9.API
{
    public enum SuspectControlPhase { Chasing, Bite, MedicalHold, Treatment, AwaitingArrest, HospitalLoading, HospitalTransport, ProviderInteraction, Arresting, Cuffed, Released, Unavailable }

    // One captured entity lifetime. Once an external provider takes control,
    // stale bite/medical fibers cannot reclaim it by refreshing an older phase.
    public sealed class SuspectControlState
    {
        public SuspectControlPhase Phase { get; private set; } = SuspectControlPhase.Chasing;
        public bool ProviderOwns => Phase==SuspectControlPhase.ProviderInteraction||Phase==SuspectControlPhase.Arresting||Phase==SuspectControlPhase.Cuffed;
        public bool CanWritePose => Phase==SuspectControlPhase.Bite||Phase==SuspectControlPhase.MedicalHold;
        public bool Set(SuspectControlPhase next)
        {
            if(Phase==SuspectControlPhase.Unavailable||(Phase==SuspectControlPhase.Released&&next!=SuspectControlPhase.Unavailable))return false;
            if(ProviderOwns && next!=SuspectControlPhase.Unavailable &&
                !(next==SuspectControlPhase.Cuffed) && !(next==SuspectControlPhase.Arresting&&Phase==SuspectControlPhase.ProviderInteraction))return false;
            if(Phase==next)return false;Phase=next;return true;
        }
    }

    public sealed class SuspectControlLease
    {
        public int Handle;
        public uint Model;
        public long Address,UpdatedUtcTicks;
        public string Episode;
        public SuspectControlPhase Phase;
        public bool BlocksCalloutTasks => Phase!=SuspectControlPhase.Released&&Phase!=SuspectControlPhase.Unavailable;
        public bool MedicalOwner => Phase==SuspectControlPhase.MedicalHold||Phase==SuspectControlPhase.Treatment||Phase==SuspectControlPhase.AwaitingArrest||Phase==SuspectControlPhase.HospitalLoading||Phase==SuspectControlPhase.HospitalTransport;
    }

    // Optional integration contract for cooperating callouts. Only the core
    // writes leases; readers match handle, model AND captured entity address.
    public static class SuspectControlApi
    {
        private static readonly string Root=Path.Combine("Plugins","LSPDFR","AdvancedK9","SuspectControl");
        private static string PathFor(int handle)=>Path.Combine(Root,handle.ToString(CultureInfo.InvariantCulture)+".state");
        public static void Publish(SuspectControlLease lease)
        {
            if(lease==null||lease.Handle<=0||lease.Address==0)throw new ArgumentException("Invalid suspect identity");
            Directory.CreateDirectory(Root);string path=PathFor(lease.Handle),temp=path+".tmp";
            File.WriteAllLines(temp,new[]{lease.Handle.ToString(CultureInfo.InvariantCulture),lease.Model.ToString(CultureInfo.InvariantCulture),lease.Address.ToString(CultureInfo.InvariantCulture),DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture),lease.Phase.ToString(),lease.Episode??""});
            if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
        }
        public static bool TryRead(int handle,uint model,long address,out SuspectControlLease lease)
        {
            lease=null;
            try
            {
                string[] lines=File.ReadAllLines(PathFor(handle));if(lines.Length<6)return false;
                int h;uint m;long a,t;SuspectControlPhase phase;
                if(!int.TryParse(lines[0],NumberStyles.Integer,CultureInfo.InvariantCulture,out h)||!uint.TryParse(lines[1],NumberStyles.Integer,CultureInfo.InvariantCulture,out m)||
                   !long.TryParse(lines[2],NumberStyles.Integer,CultureInfo.InvariantCulture,out a)||!long.TryParse(lines[3],NumberStyles.Integer,CultureInfo.InvariantCulture,out t)||!Enum.TryParse(lines[4],out phase)||!Enum.IsDefined(typeof(SuspectControlPhase),phase))return false;
                long age=DateTime.UtcNow.Ticks-t;
                if(h!=handle||m!=model||a!=address||a==0||age<0||age>TimeSpan.FromSeconds(5).Ticks)return false;
                lease=new SuspectControlLease{Handle=h,Model=m,Address=a,UpdatedUtcTicks=t,Phase=phase,Episode=lines[5]};return true;
            }
            catch{return false;}
        }
    }
}
