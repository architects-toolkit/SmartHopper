using System.Runtime.CompilerServices;

namespace SmartHopper.Core.UI
{
    internal static class CanvasButtonBootstrap
    {
        /// <summary>
        /// Module initializer to auto-run CanvasButton.EnsureInitialized at assembly load.
        /// </summary>
        [ModuleInitializer]
        public static void Init()
        {
            CanvasButton.EnsureInitialized();
        }
    }
}
