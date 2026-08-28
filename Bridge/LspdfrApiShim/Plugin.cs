namespace LSPD_First_Response.Mod.API
{
    // Compile-time surface only. The built bridge resolves this type from the real
    // LSPD First Response assembly already loaded by LSPDFR. This shim is never packaged.
    public abstract class Plugin
    {
        public abstract void Initialize();
        public abstract void Finally();
    }
}
