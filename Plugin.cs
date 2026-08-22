using System;
using Rage;

namespace AdvancedK9
{
    public static class EntryPoint
    {
        private static K9Controller _controller;
        private static bool _running;

        public static void Main()
        {
            _running = true;
            Game.LogTrivial("AdvancedK9: initialized in RPH mode; starting controller.");
            try
            {
                _controller = new K9Controller(ModConfig.Load());
                _controller.Run();
            }
            catch (Exception ex) { Game.LogTrivial("AdvancedK9 controller failure: " + ex); }
            finally { _running = false; }
        }

        public static void Finally()
        {
            _running = false;
            _controller?.Dispose();
            _controller = null;
            Game.LogTrivial("AdvancedK9: unloaded.");
        }
    }
}
