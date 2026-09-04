using System;
using System.Collections.Generic;
using System.Linq;
using Rage;
using Rage.Native;

namespace AdvancedK9
{
    internal sealed class VehicleSearchPoint
    {
        public Vector3 Position;
        public string Zone;
        public VehicleSearchPoint(Vector3 position,string zone){Position=position;Zone=zone;}
    }

    internal static class VehicleSearchPaths
    {
        private sealed class BoneSpec
        {
            public string Bone;public string Zone;public float SideX;public float ForwardY;
            public BoneSpec(string bone,string zone,float sideX,float forwardY){Bone=bone;Zone=zone;SideX=sideX;ForwardY=forwardY;}
        }

        public static List<VehicleSearchPoint> Build(Vehicle vehicle)
        {
            string model=(vehicle.Model.Name??"").ToLowerInvariant();
            bool motorcycle=model.Contains("bike")||model.Contains("bati")||model.Contains("akuma")||model.Contains("daemon")||model.Contains("faggio");
            bool longBody=model.Contains("bus")||model.Contains("coach")||model.Contains("mule")||model.Contains("pounder")||model.Contains("benson")||model.Contains("boxville")||model.Contains("stockade")||model.Contains("ambulance");
            bool utility=longBody||model.Contains("van")||model.Contains("rumpo")||model.Contains("speedo")||model.Contains("burrito")||model.Contains("granger")||model.Contains("suburban");
            if(motorcycle)return Relative(vehicle,new[]{new Vector3(-.75f,.8f,0f),new Vector3(.75f,.8f,0f),new Vector3(.75f,-.8f,0f),new Vector3(-.75f,-.8f,0f)},new[]{"front-left controls","front-right controls","rear-right storage","rear-left storage"});

            var specs=new List<BoneSpec>{
                new BoneSpec("bumper_f","front grille / engine bay",0f,.45f),
                new BoneSpec("wheel_lf","driver-front wheel arch",-.55f,0f),
                new BoneSpec("door_dside_f","driver door seam",-.55f,0f),
                new BoneSpec("door_dside_r","driver-rear door seam",-.55f,0f),
                new BoneSpec("wheel_lr","driver-rear wheel arch",-.55f,0f),
                new BoneSpec("boot","trunk / cargo seam",0f,-.5f),
                new BoneSpec("wheel_rr","passenger-rear wheel arch",.55f,0f),
                new BoneSpec("door_pside_r","passenger-rear door seam",.55f,0f),
                new BoneSpec("door_pside_f","passenger door seam",.55f,0f),
                new BoneSpec("wheel_rf","passenger-front wheel arch",.55f,0f)};
            if(!utility)specs.RemoveAll(x=>x.Bone=="wheel_lr"||x.Bone=="wheel_rr");
            var result=new List<VehicleSearchPoint>();
            foreach(var spec in specs)
            {
                Vector3 bone;
                if(!TryBone(vehicle,spec.Bone,out bone))continue;
                Vector3 outward=vehicle.GetOffsetPosition(new Vector3(spec.SideX,spec.ForwardY,0f))-vehicle.Position;
                float length=(float)Math.Sqrt(outward.X*outward.X+outward.Y*outward.Y);if(length>.01f)bone+=new Vector3(outward.X/length*.65f,outward.Y/length*.65f,0f);
                if(result.All(x=>x.Position.DistanceTo(bone)>.65f))result.Add(new VehicleSearchPoint(bone,spec.Zone));
            }
            if(result.Count>=6)return result;
            float side=utility?1.7f:1.4f,front=longBody?4.2f:utility?3.1f:2.35f;
            return Relative(vehicle,new[]{new Vector3(-side,front,0f),new Vector3(-side,0f,0f),new Vector3(-side,-front,0f),new Vector3(side,-front,0f),new Vector3(side,0f,0f),new Vector3(side,front,0f)},new[]{"driver-front","driver side","driver-rear / cargo","passenger-rear / cargo","passenger side","passenger-front"});
        }

        private static bool TryBone(Vehicle vehicle,string name,out Vector3 position)
        {
            position=new Vector3();try{int index=NativeFunction.Natives.GET_ENTITY_BONE_INDEX_BY_NAME<int>(vehicle,name);if(index<0)return false;position=NativeFunction.Natives.GET_WORLD_POSITION_OF_ENTITY_BONE<Vector3>(vehicle,index);return position.DistanceTo(vehicle.Position)<12f;}catch{return false;}
        }

        private static List<VehicleSearchPoint> Relative(Vehicle vehicle,IList<Vector3> offsets,IList<string> labels)
        {
            var result=new List<VehicleSearchPoint>();for(int i=0;i<offsets.Count;i++)result.Add(new VehicleSearchPoint(vehicle.GetOffsetPosition(offsets[i]),labels[Math.Min(i,labels.Count-1)]));return result;
        }
    }
}
