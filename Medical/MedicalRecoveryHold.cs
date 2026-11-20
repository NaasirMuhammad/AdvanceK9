namespace AdvancedK9.Medical
{
    // A hold must begin while arrival recovery is available. One action per press.
    internal sealed class MedicalRecoveryHold
    {
        private bool _down,_armed,_fired;
        private uint _started;
        internal bool Update(bool down,bool eligible,uint now,int holdMilliseconds)
        {
            if(!down){_down=false;_armed=false;_fired=false;return false;}
            if(!_down){_down=true;_armed=eligible;_started=now;}
            if(!eligible)_armed=false;
            if(!_armed||_fired||unchecked(now-_started)<(uint)holdMilliseconds)return false;
            _fired=true;return true;
        }
    }
}
