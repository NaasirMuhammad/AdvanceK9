using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using Rage;

namespace AdvancedK9
{
    internal sealed class SearchEvidenceMarkers : IDisposable
    {
        private readonly string _path=Path.Combine("Plugins","LSPDFR","AdvancedK9","SearchEvidenceMarkers.csv");
        private sealed class Marker{public Blip Blip;public uint Expires;}
        private readonly List<Marker> _sessionMarkers=new List<Marker>();

        public void Add(string dog,string target,string specialty,string zone,Vector3 position)
        {
            try
            {
                var blip=new Blip(position){Color=Color.Orange,Name="K9 evidence — "+specialty+" ("+zone+")"};
                _sessionMarkers.Add(new Marker{Blip=blip,Expires=Game.GameTime+600000});
                string directory=Path.GetDirectoryName(_path);if(!Directory.Exists(directory))Directory.CreateDirectory(directory);
                if(!File.Exists(_path))File.AppendAllText(_path,"Timestamp,Dog,Target,Specialty,AlertZone,X,Y,Z"+Environment.NewLine);
                Func<string,string> q=s=>"\""+(s??"").Replace("\"","\"\"")+"\"";
                File.AppendAllText(_path,string.Join(",",q(DateTime.Now.ToString("s")),q(dog),q(target),q(specialty),q(zone),position.X.ToString("0.000",CultureInfo.InvariantCulture),position.Y.ToString("0.000",CultureInfo.InvariantCulture),position.Z.ToString("0.000",CultureInfo.InvariantCulture))+Environment.NewLine);
                Game.DisplayNotification("~o~K9 evidence marker saved.~s~~n~"+zone+" • "+position.X.ToString("0.0")+", "+position.Y.ToString("0.0")+", "+position.Z.ToString("0.0"));
            }
            catch(Exception ex){Game.LogTrivial("AdvancedK9 evidence marker: "+ex.Message);}
        }

        public void Tick()
        {
            for(int i=_sessionMarkers.Count-1;i>=0;i--)if(Game.GameTime>=_sessionMarkers[i].Expires){try{if(_sessionMarkers[i].Blip!=null&&_sessionMarkers[i].Blip.Exists())_sessionMarkers[i].Blip.Delete();}catch{}_sessionMarkers.RemoveAt(i);}
        }

        public void ClearSession()
        {
            foreach(var marker in _sessionMarkers)try{if(marker!=null&&marker.Blip!=null&&marker.Blip.Exists())marker.Blip.Delete();}catch{}
            _sessionMarkers.Clear();
        }

        public void Dispose(){ClearSession();}
    }
}
