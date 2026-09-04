using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Rage;

namespace AdvancedK9
{
    internal sealed class K9RosterEntry
    {
        public string Id;
        public string Name;
        public string KennelKey;
        public string Status;
        public DateTime LastDutyUtc;

        public string Summary => Name+" — "+(string.IsNullOrWhiteSpace(KennelKey)?"Unassigned":KennelKey)+" • "+(string.IsNullOrWhiteSpace(Status)?"Available":Status);
    }

    internal sealed class K9Roster
    {
        private readonly string _path=Path.Combine("Plugins","LSPDFR","AdvancedK9","Profiles","roster.dat");
        private readonly List<K9RosterEntry> _entries=new List<K9RosterEntry>();
        public IReadOnlyList<K9RosterEntry> Entries => _entries;
        public string ActiveId { get; private set; }
        public K9RosterEntry Active => _entries.FirstOrDefault(x=>x.Id.Equals(ActiveId,StringComparison.OrdinalIgnoreCase))??_entries.First();

        public K9Roster(string defaultName)
        {
            Load();
            if(_entries.Count==0)_entries.Add(new K9RosterEntry{Id="rex",Name=string.IsNullOrWhiteSpace(defaultName)?"Rex":defaultName.Trim(),KennelKey="MissionRow",Status="Available"});
            if(string.IsNullOrWhiteSpace(ActiveId)||!_entries.Any(x=>x.Id.Equals(ActiveId,StringComparison.OrdinalIgnoreCase)))ActiveId=_entries[0].Id;
            Save();
        }

        public bool Select(string id)
        {
            var entry=_entries.FirstOrDefault(x=>x.Id.Equals(id,StringComparison.OrdinalIgnoreCase));
            if(entry==null)return false;ActiveId=entry.Id;Save();return true;
        }

        public K9RosterEntry Add(string name,string kennelKey)
        {
            if(_entries.Count>=8)return null;
            string baseId=Sanitize(name);if(string.IsNullOrWhiteSpace(baseId))baseId="k9";
            string id=baseId;int suffix=2;while(_entries.Any(x=>x.Id.Equals(id,StringComparison.OrdinalIgnoreCase)))id=baseId+(suffix++);
            var entry=new K9RosterEntry{Id=id,Name=string.IsNullOrWhiteSpace(name)?"K9 Partner":name.Trim(),KennelKey=kennelKey??"",Status="Available"};
            _entries.Add(entry);ActiveId=id;Save();return entry;
        }

        public void UpdateActive(string name,string kennelKey,string status)
        {
            var entry=Active;if(!string.IsNullOrWhiteSpace(name))entry.Name=name.Trim();if(kennelKey!=null)entry.KennelKey=kennelKey;if(status!=null)entry.Status=status;Save();
        }

        public void RecordDuty(bool deployed)
        {
            var entry=Active;entry.Status=deployed?"Deployed":entry.Status=="Rehabilitation"?entry.Status:"Available";entry.LastDutyUtc=DateTime.UtcNow;Save();
        }

        private void Load()
        {
            try
            {
                if(!File.Exists(_path))return;
                foreach(string line in File.ReadAllLines(_path))
                {
                    if(line.StartsWith("Active=",StringComparison.OrdinalIgnoreCase)){ActiveId=line.Substring(7).Trim();continue;}
                    if(!line.StartsWith("Dog=",StringComparison.OrdinalIgnoreCase))continue;
                    string[] p=line.Substring(4).Split('|');if(p.Length<4)continue;DateTime last;DateTime.TryParse(p.Length>4?p[4]:"",out last);
                    _entries.Add(new K9RosterEntry{Id=Sanitize(p[0]),Name=Unescape(p[1]),KennelKey=Unescape(p[2]),Status=Unescape(p[3]),LastDutyUtc=last});
                }
            }
            catch(Exception ex){Game.LogTrivial("AdvancedK9 roster load: "+ex.Message);_entries.Clear();}
        }

        public void Save()
        {
            try
            {
                string directory=Path.GetDirectoryName(_path);if(!Directory.Exists(directory))Directory.CreateDirectory(directory);
                var lines=new List<string>{"Version=1","Active="+(ActiveId??"")};
                lines.AddRange(_entries.Select(x=>"Dog="+x.Id+"|"+Escape(x.Name)+"|"+Escape(x.KennelKey)+"|"+Escape(x.Status)+"|"+x.LastDutyUtc.ToString("o")));
                File.WriteAllLines(_path,lines);
            }
            catch(Exception ex){Game.LogTrivial("AdvancedK9 roster save: "+ex.Message);}
        }

        private static string Sanitize(string value)=>new string((value??"").ToLowerInvariant().Where(c=>char.IsLetterOrDigit(c)||c=='-'||c=='_').ToArray());
        private static string Escape(string value)=>(value??"").Replace("|","/").Replace("\r"," ").Replace("\n"," ");
        private static string Unescape(string value)=>value??"";
    }
}
