using System.Windows.Forms;
using SmartHopper.Config.Initialization;
using SmartHopper.Config.Dialogs;

namespace SmartHopper.Menu.Items
{
    internal static class RefreshProvidersMenuItem
    {
        /// <summary>
        /// Creates a menu item to manually refresh AI provider discovery.
        /// </summary>
        public static ToolStripMenuItem Create()
        {
            var item = new ToolStripMenuItem("Refresh Providers");
            item.Click += (sender, e) =>
            {
                // Use the new initializer to safely refresh everything
                SmartHopperInitializer.Reinitialize();
                
                StyledMessageDialog.ShowInfo("AI provider discovery and settings refresh has been triggered.", "SmartHopper");
            };
            return item;
        }
    }
}
