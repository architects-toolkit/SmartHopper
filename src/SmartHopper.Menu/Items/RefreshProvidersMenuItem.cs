using System.Windows.Forms;
using SmartHopper.Config.Initialization;

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
                
                MessageBox.Show(
                    "AI provider discovery and settings refresh has been triggered.",
                    "SmartHopper",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            };
            return item;
        }
    }
}
