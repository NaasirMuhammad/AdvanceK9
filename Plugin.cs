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
            Game.LogTrivial("AdvancedK9: initialized; waiting for LSPDFR duty state.");
            while (_running)
            {
                try
                {
                    bool onDuty = LspdfrBridge.IsPlayerOnDuty();
                    if (onDuty && _controller == null)
                    {
                        _controller = new K9Controller(ModConfig.Load());
                        GameFiber.StartNew(_controller.Run, "AdvancedK9.Main");
                    }
                    else if (!onDuty && _controller != null)
                    {
                        _controller.Dispose();
                        _controller = null;
                    }
                }
                catch (Exception ex) { Game.LogTrivial("AdvancedK9 duty monitor: " + ex); }
                GameFiber.Wait(1000);
            }
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
