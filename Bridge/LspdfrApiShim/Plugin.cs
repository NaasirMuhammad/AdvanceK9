using System;
using Rage;

namespace LSPD_First_Response.Mod.API
{
    // Compile-time surface only. The real LSPDFR assembly supplies these types at runtime.
    public abstract class Plugin
    {
        public abstract void Initialize();
        public abstract void Finally();
    }

    public static class Functions
    {
        public static void RegisterCallout(Type calloutType){}
        public static void PlayScannerAudioUsingPosition(string audio,Vector3 position){}
    }
}

namespace LSPD_First_Response.Mod.Callouts
{
    public enum CalloutProbability { Never,VeryLow,Low,Medium,High,VeryHigh }

    [AttributeUsage(AttributeTargets.Class,AllowMultiple=false)]
    public sealed class CalloutInfoAttribute : Attribute
    {
        public CalloutInfoAttribute(string name,CalloutProbability probability){}
    }

    public abstract class Callout
    {
        public string CalloutMessage { get; set; }
        public Vector3 CalloutPosition { get; set; }
        public virtual bool OnBeforeCalloutDisplayed(){return true;}
        public virtual bool OnCalloutAccepted(){return true;}
        public virtual void OnCalloutNotAccepted(){}
        public virtual void Process(){}
        public virtual void End(){}
        protected void ShowCalloutAreaBlipBeforeAccepting(Vector3 position,float radius){}
        protected void AddMinimumDistanceCheck(float distance,Vector3 position){}
    }
}
