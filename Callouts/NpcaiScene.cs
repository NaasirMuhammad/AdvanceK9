using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using LSPD_First_Response.Mod.API;
using Rage;
using Rage.Native;

namespace AdvancedK9.Callouts
{
    // Late binding keeps the optional SDK out of the assembly references and installer.
    internal sealed class NpcaiScene
    {
        private readonly string _name;
        private readonly Dictionary<Ped,string> _stories=new Dictionary<Ped,string>();
        private Type _context,_briefing,_lines;
        private bool _probed,_closed,_greeted,_warned;
        private string _lastBriefing;
        private object _greeting;

        internal NpcaiScene(string name){_name=name;}
        internal bool Available
        {
            get
            {
                if(_closed)return false;
                if(_probed)return _context!=null;
                _probed=true;
                try
                {
                    var plugins=typeof(Functions).GetMethod("GetAllUserPlugins",Type.EmptyTypes)?.Invoke(null,null) as IEnumerable;
                    if(plugins==null||!plugins.OfType<Assembly>().Any(a=>a.GetName().Name.Equals("NPCI",StringComparison.OrdinalIgnoreCase)))return false;
                    string path=Path.GetFullPath("NPCAI_Callouts_API.dll");
                    if(!File.Exists(path))return false;
                    string name=AssemblyName.GetAssemblyName(path).FullName;
                    var dll=AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a=>a.FullName==name)??Assembly.LoadFrom(path);
                    const string prefix="NPCAI.CalloutsAPI.";
                    _context=dll.GetType(prefix+"NPCAICalloutContext",true);
                    _briefing=dll.GetType(prefix+"NPCAICalloutBriefing",true);
                    _lines=dll.GetType(prefix+"NPCAIAmbientLines",true);
                    Game.LogTrivial("AdvancedK9 NPCAI: callout API bound for "+_name);
                }
                catch(Exception ex){_context=null;Warn(ex);}
                return _context!=null;
            }
        }

        private static object Call(Type type,string method,params object[] args)
        {
            var signature=args.Select(a=>a is Ped?typeof(Ped):a.GetType()).ToArray();
            var target=type.GetMethod(method,BindingFlags.Public|BindingFlags.Static,null,signature,null);
            if(target==null)throw new MissingMethodException(type.FullName,method);
            return target.Invoke(null,args);
        }
        private void Warn(Exception ex)
        {
            if(_warned)return;_warned=true;
            Game.LogTrivial("AdvancedK9 NPCAI: "+(ex.InnerException??ex).Message);
        }
        internal void Brief(string text,Vector3 position)
        {
            if(!Available||string.IsNullOrWhiteSpace(text)||text==_lastBriefing)return;
            try{Call(_briefing,"Set",_name,text.Length>1500?text.Substring(0,1500):text,position);_lastBriefing=text;}
            catch(Exception ex){Warn(ex);}
        }
        internal void Story(Ped ped,string text,string mood,bool stay=false)
        {
            if(!Available||ped==null||!ped.Exists()||ped.IsDead)return;
            string signature=text+"|"+mood+"|"+stay;
            if(_stories.TryGetValue(ped,out var old)&&old==signature)return;
            try
            {
                string identity="";
                try
                {
                    var persona=typeof(Functions).GetMethod("GetPersonaForPed",new[]{typeof(Ped)})?.Invoke(null,new object[]{ped});
                    var fullName=persona?.GetType().GetProperty("FullName")?.GetValue(persona) as string;
                    if(!string.IsNullOrWhiteSpace(fullName))identity="Your name is "+fullName+". ";
                }
                catch { }
                Call(_context,"SetCalloutScenario",ped,identity+text+" Say when you don't know. Do not invent evidence, orders, or a scene outcome.");
                _stories[ped]=signature;
                var moodType=_context.Assembly.GetType("NPCAI.CalloutsAPI.PedMood",true);
                Call(_context,"SetCalloutMood",ped,Enum.Parse(moodType,mood));
                Call(_context,"SetPedStayPut",ped,stay);
            }
            catch(Exception ex){Warn(ex);}
        }
        internal void Greet(Ped contact,string prompt)
        {
            if(_greeted||!Available||contact==null||!contact.Exists()||contact.IsDead)return;
            try
            {
                var player=Game.LocalPlayer.Character;
                if(player==null||!player.Exists()||!player.IsOnFoot||player.DistanceTo(contact)>8f||
                   !NativeFunction.Natives.HAS_ENTITY_CLEAR_LOS_TO_ENTITY<bool>(contact,player,17))return;
                _greeted=true;
                _greeting=Call(_lines,"Say",contact,"You are the designated on-scene officer briefing the K9 handler.",
                    "[Give a short greeting and relay only this confirmed briefing: "+prompt+"]",false);
            }
            catch(Exception ex){Warn(ex);}
        }
        internal void Close()
        {
            if(_closed)return;_closed=true;
            if(_context==null)return;
            try{_greeting?.GetType().GetMethod("Cancel",Type.EmptyTypes)?.Invoke(_greeting,null);}catch(Exception ex){Warn(ex);}
            foreach(var ped in _stories.Keys)
                try{Call(_context,"ClearCalloutData",ped);}catch(Exception ex){Warn(ex);}
            _stories.Clear();
            if(_lastBriefing!=null)try{Call(_briefing,"Clear",_name);}catch(Exception ex){Warn(ex);}
        }
    }
}
