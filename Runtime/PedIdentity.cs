using System;
using System.Globalization;
using Rage;

namespace AdvancedK9.Runtime
{
    internal sealed class PedIdentity
    {
        internal int Handle;
        internal uint Model;
        internal long Address;
        internal static bool TryCapture(Ped ped,out PedIdentity identity)
        {
            identity=null;
            try
            {
                if(ped==null||!ped.Exists()||ped.MemoryAddress==IntPtr.Zero)return false;
                string raw=ped.Handle.ToString();int handle;
                if(!int.TryParse(raw,out handle)&&!int.TryParse(raw.StartsWith("0x",StringComparison.OrdinalIgnoreCase)?raw.Substring(2):raw,NumberStyles.HexNumber,CultureInfo.InvariantCulture,out handle))return false;
                if(handle<=0)return false;
                identity=new PedIdentity{Handle=handle,Model=ped.Model.Hash,Address=ped.MemoryAddress.ToInt64()};return identity.Address!=0;
            }
            catch{return false;}
        }
        internal bool Matches(Ped ped)
        {
            PedIdentity current;return TryCapture(ped,out current)&&Handle==current.Handle&&Model==current.Model&&Address==current.Address;
        }
    }
}
