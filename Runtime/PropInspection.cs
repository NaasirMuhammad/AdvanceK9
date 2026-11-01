using System;
using System.Linq;
using Rage;
using Rage.Native;

namespace AdvancedK9
{
    internal sealed partial class K9Controller
    {
        private uint _nextPropAudit;
        private void AuditNearbyProps(bool manual=false)
        {
            if(!manual&&Game.GameTime<_nextPropAudit)return;
            _nextPropAudit=Game.GameTime+15000;
            try
            {
                var player=Game.LocalPlayer.Character;if(player==null||!player.Exists())return;
                int logged=0;
                foreach(var prop in World.GetAllObjects().Where(o=>o!=null&&o.Exists()&&o.DistanceTo(player)<(manual?20f:12f)).OrderBy(o=>o.DistanceTo(player)).Take(32))
                {
                    string model=prop.Model.Name??"";
                    bool woodlike=new[]{"wood","plank","board","splinter"}.Any(s=>model.IndexOf(s,StringComparison.OrdinalIgnoreCase)>=0);
                    if(!manual&&!woodlike)continue;
                    string owner=_animatedLeash==prop?"AdvancedK9 leash":_leashCollarClip==prop?"AdvancedK9 leash clasp":_stationKennels.Any(k=>k.Prop==prop)?"AdvancedK9 kennel":"not identified as an AdvancedK9 persistent prop";
                    Vector3 position=prop.Position;float ground=0f;
                    bool groundFound=NativeFunction.Natives.GET_GROUND_Z_FOR_3D_COORD<bool>(position.X,position.Y,position.Z+3f,out ground,false);
                    Game.LogTrivial("AdvancedK9 prop inspection: model="+model+", hash="+prop.Model.Hash+", handle="+prop.Handle+", position="+position+", aboveGround="+(groundFound?(position.Z-ground).ToString("0.00"):"unknown")+", attached="+NativeFunction.Natives.IS_ENTITY_ATTACHED<bool>(prop)+", owner="+owner+".");logged++;
                }
                if(manual){Game.LogTrivial("AdvancedK9 prop inspection complete: "+logged+" nearby object(s). No world props deleted.");Game.DisplayNotification("~b~Nearby props logged.~s~ Capture the floating object and send RagePluginHook.log.");}
            }
            catch(Exception ex){Game.LogTrivial("AdvancedK9 prop inspection contained: "+ex.Message);}
        }
    }
}
