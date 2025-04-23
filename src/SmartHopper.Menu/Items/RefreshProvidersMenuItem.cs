using System.Windows.Forms;
using SmartHopper.Config.Managers;

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
                ProviderManager.Instance.RefreshProviders();
                MessageBox.Show(
                    "AI provider discovery has been triggered.",
                    "SmartHopper",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            };
            return item;
        }
    }
}
