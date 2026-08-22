using System;
using System.IO;
using Rage;

namespace AdvancedK9
{
    internal static class K9IncidentLog
    {
        private static readonly string PathName=Path.Combine("Plugins","LSPDFR","AdvancedK9","K9IncidentLog.csv");
        public static void Write(string dog,string action,string result,Vector3 location)
        {
            try
            {
                string directory=Path.GetDirectoryName(PathName);if(!Directory.Exists(directory))Directory.CreateDirectory(directory);
                if(!File.Exists(PathName))File.AppendAllText(PathName,"Timestamp,Dog,Action,Result,X,Y,Z"+Environment.NewLine);
                Func<string,string> q=s=>"\""+(s??"").Replace("\"","\"\"")+"\"";
                File.AppendAllText(PathName,string.Join(",",q(DateTime.Now.ToString("s")),q(dog),q(action),q(result),location.X.ToString("0.0"),location.Y.ToString("0.0"),location.Z.ToString("0.0"))+Environment.NewLine);
            }
            catch(Exception ex){Game.LogTrivial("AdvancedK9 incident log: "+ex.Message);}
        }
    }
}
