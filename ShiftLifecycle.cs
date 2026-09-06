using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Rage;

namespace AdvancedK9
{
    internal sealed class ShiftLifecycle
    {
        private readonly string _directory=Path.Combine("Plugins","LSPDFR","AdvancedK9","Reports");
        private readonly string _hoursPath=Path.Combine("Plugins","LSPDFR","AdvancedK9","DutyHours.dat");
        private DateTime _startedUtc;
        private uint _startedGameTime;
        private string _k9Name="";
        private bool _active;
        private int _commands,_searches,_tracks,_apprehensions,_alerts,_medical;

        public void Begin(string k9Name)
        {
            _active=true;_k9Name=k9Name??"K9";_startedUtc=DateTime.UtcNow;_startedGameTime=Game.GameTime;
            _commands=_searches=_tracks=_apprehensions=_alerts=_medical=0;
            Game.DisplayNotification("~b~K9 PRE-SHIFT CHECK~s~~n~1. Inspect partner and equipment~n~2. Confirm water/rest status~n~3. Deploy from a station kennel when ready.");
            Game.LogTrivial("AdvancedK9 shift lifecycle: pre-shift checklist started for "+_k9Name+".");
        }

        public void Record(K9Command command)
        {
            if(!_active)return;_commands++;
            if(command==K9Command.SearchArea||command==K9Command.SearchBuilding||command==K9Command.SearchVehicle||command==K9Command.SearchNarcotics||command==K9Command.SearchExplosives||command==K9Command.SearchWeapons)_searches++;
            else if(command==K9Command.Track||command==K9Command.FindTrail)_tracks++;
            else if(command==K9Command.Apprehend)_apprehensions++;
            else if(command==K9Command.Bark||command==K9Command.K9Warning)_alerts++;
            else if(command==K9Command.FirstAid||command==K9Command.EmergencyLoadK9||command==K9Command.VeterinaryTransport||command==K9Command.VeterinaryCare)_medical++;
        }

        public void End(int health,int stamina,int food,int water,bool returnedToKennel)
        {
            if(!_active)return;_active=false;
            int minutes=Math.Max(1,(int)((Game.GameTime-_startedGameTime)/60000));double totalHours=ReadHours()+minutes/60.0;WriteHours(totalHours);
            try
            {
                if(!Directory.Exists(_directory))Directory.CreateDirectory(_directory);
                string stamp=DateTime.UtcNow.ToString("yyyyMMdd-HHmmss",CultureInfo.InvariantCulture);
                string path=Path.Combine(_directory,"Shift-"+stamp+".txt");
                File.WriteAllLines(path,new[]{
                    "AdvancedK9 Shift Deployment Summary",
                    "K9: "+_k9Name,
                    "Started UTC: "+_startedUtc.ToString("u"),
                    "Ended UTC: "+DateTime.UtcNow.ToString("u"),
                    "Duty minutes: "+minutes,
                    "Saved total duty hours: "+totalHours.ToString("0.00",CultureInfo.InvariantCulture),
                    "Commands: "+_commands,
                    "Searches: "+_searches,
                    "Tracks: "+_tracks,
                    "Apprehensions: "+_apprehensions,
                    "Warnings / alerts: "+_alerts,
                    "Medical actions: "+_medical,
                    "End vitals: health "+health+"%, stamina "+stamina+"%, food "+food+"%, water "+water+"%",
                    "Returned to kennel: "+returnedToKennel,
                    "End-of-shift checklist: kennel return, inspect, feed/water if needed, review deployment reports."
                });
                Game.DisplayNotification("~b~K9 END-OF-SHIFT SUMMARY~s~~n~"+minutes+" min • "+_commands+" commands • "+_searches+" searches • "+_tracks+" tracks~n~Report saved. Feed/water "+_k9Name+" if below 50%.");
                Game.LogTrivial("AdvancedK9 shift lifecycle: report saved to "+path+".");
            }
            catch(Exception ex){Game.LogTrivial("AdvancedK9 shift report failed: "+ex.Message);}
        }

        private double ReadHours(){try{double value;if(File.Exists(_hoursPath)&&double.TryParse(File.ReadAllText(_hoursPath),NumberStyles.Float,CultureInfo.InvariantCulture,out value))return value;}catch{}return 0;}
        private void WriteHours(double value){try{string directory=Path.GetDirectoryName(_hoursPath);if(!Directory.Exists(directory))Directory.CreateDirectory(directory);File.WriteAllText(_hoursPath,value.ToString("0.0000",CultureInfo.InvariantCulture));}catch(Exception ex){Game.LogTrivial("AdvancedK9 duty hours save failed: "+ex.Message);}}
    }
}
