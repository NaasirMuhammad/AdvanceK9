using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Rage;

namespace AdvancedK9
{
    internal sealed class SavedContainmentPosition
    {
        public string Name;public Vector3 Position;public float Radius;
    }

    internal sealed class ContainmentPositionStore
    {
        private readonly string _path=Path.Combine("Plugins","LSPDFR","AdvancedK9","ContainmentPositions.dat");
        private readonly List<SavedContainmentPosition> _positions=new List<SavedContainmentPosition>();
        public ContainmentPositionStore(){Load();}

        public SavedContainmentPosition Nearest(Vector3 point,float maximum)=>_positions.Where(x=>x.Position.DistanceTo(point)<=maximum).OrderBy(x=>x.Position.DistanceTo(point)).FirstOrDefault();

        public SavedContainmentPosition SaveOrUpdate(Vector3 position,float radius)
        {
            var item=Nearest(position,5f);if(item==null){item=new SavedContainmentPosition{Name="Containment "+(_positions.Count+1),Position=position,Radius=radius};_positions.Add(item);}else{item.Position=position;item.Radius=radius;}Save();return item;
        }

        private void Load(){try{if(!File.Exists(_path))return;foreach(string line in File.ReadAllLines(_path)){string[] p=line.Split('|');float x,y,z,r;if(p.Length==5&&float.TryParse(p[1],NumberStyles.Float,CultureInfo.InvariantCulture,out x)&&float.TryParse(p[2],NumberStyles.Float,CultureInfo.InvariantCulture,out y)&&float.TryParse(p[3],NumberStyles.Float,CultureInfo.InvariantCulture,out z)&&float.TryParse(p[4],NumberStyles.Float,CultureInfo.InvariantCulture,out r))_positions.Add(new SavedContainmentPosition{Name=p[0],Position=new Vector3(x,y,z),Radius=r});}}catch(Exception ex){Game.LogTrivial("AdvancedK9 containment positions load: "+ex.Message);}}
        private void Save(){try{string directory=Path.GetDirectoryName(_path);if(!Directory.Exists(directory))Directory.CreateDirectory(directory);File.WriteAllLines(_path,_positions.Take(24).Select(x=>x.Name.Replace("|","/")+"|"+x.Position.X.ToString(CultureInfo.InvariantCulture)+"|"+x.Position.Y.ToString(CultureInfo.InvariantCulture)+"|"+x.Position.Z.ToString(CultureInfo.InvariantCulture)+"|"+x.Radius.ToString(CultureInfo.InvariantCulture)));}catch(Exception ex){Game.LogTrivial("AdvancedK9 containment positions save: "+ex.Message);}}
    }
}
