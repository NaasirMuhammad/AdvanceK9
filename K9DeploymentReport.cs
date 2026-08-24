using System;
using System.Globalization;
using System.IO;
using System.Text;
using Rage;

namespace AdvancedK9
{
    internal static class K9DeploymentReport
    {
        private static readonly object Gate=new object();
        private static string PathName=>Path.Combine("Plugins","LSPDFR","AdvancedK9","K9DeploymentReports.csv");
        public static void Write(string handler,string dog,string deployment,string reason,string source,bool warning,float trackDistance,int trackSeconds,int biteSeconds,string suspectOutcome,string k9Injury,string disposition,Vector3 location)
        {
            try
            {
                lock(Gate)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(PathName));
                    bool header=!File.Exists(PathName);
                    using(var writer=new StreamWriter(PathName,true,new UTF8Encoding(true)))
                    {
                        if(header)writer.WriteLine("DateTime,Handler,K9,Deployment,Reason,ScentSource,WarningGiven,TrackDistanceMeters,TrackSeconds,BiteSeconds,SuspectOutcome,K9Injury,Disposition,LocationX,LocationY,LocationZ");
                        writer.WriteLine(string.Join(",",new[]{Csv(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture)),Csv(handler),Csv(dog),Csv(deployment),Csv(reason),Csv(source),warning?"true":"false",trackDistance.ToString("0.0",CultureInfo.InvariantCulture),trackSeconds.ToString(CultureInfo.InvariantCulture),biteSeconds.ToString(CultureInfo.InvariantCulture),Csv(suspectOutcome),Csv(k9Injury),Csv(disposition),location.X.ToString("0.00",CultureInfo.InvariantCulture),location.Y.ToString("0.00",CultureInfo.InvariantCulture),location.Z.ToString("0.00",CultureInfo.InvariantCulture)}));
                    }
                }
            }
            catch(Exception ex){Game.LogTrivial("AdvancedK9 deployment report: "+ex.Message);}
        }
        private static string Csv(string value)=>"\""+(value??"").Replace("\"","\"\"")+"\"";
    }
}
