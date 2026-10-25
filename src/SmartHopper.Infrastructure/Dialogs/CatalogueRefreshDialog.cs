/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024-2026 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this library; if not, see <https://www.gnu.org/licenses/lgpl-3.0.html>.
 */

using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Eto.Drawing;
using Eto.Forms;
using Rhino.UI;
using SmartHopper.Infrastructure.Properties;
using SmartHopper.Infrastructure.Utils;
using SmartHopper.ProviderSdk.Catalogue;

namespace SmartHopper.Infrastructure.Dialogs
{
    /// <summary>
    /// Manual refresh dialog for the signed model catalogue: streams the service's
    /// progress log, then reports the applied source (remote / verified cache /
    /// built-in seed) and any descriptive error. Failures never surface as
    /// exceptions — they appear as log lines and a red conclusion.
    /// </summary>
    internal class CatalogueRefreshDialog : Dialog
    {
        private static readonly Assembly ConfigAssembly = typeof(providersResources).Assembly;
        private const string IconResourceName = "SmartHopper.Infrastructure.Resources.smarthopper.ico";

        private readonly Label _statusLabel;
        private readonly TextArea _logArea;
        private readonly Button _runButton;
        private bool _running;

        public CatalogueRefreshDialog()
        {
            this.Title = "Model Catalogue - SmartHopper";
            this.Resizable = true;
            this.Size = new Size(620, 480);
            this.MinimumSize = new Size(460, 320);
            this.Padding = new Padding(20);

            using (var stream = ConfigAssembly.GetManifestResourceStream(IconResourceName))
            {
                if (stream != null)
                {
                    this.Icon = new Eto.Drawing.Icon(stream);
                }
            }

            this._statusLabel = new Label
            {
                Text = StatusText(),
                Font = new Font(SystemFont.Default, 10),
                TextColor = SystemColors.DisabledText,
                Wrap = WrapMode.Word,
            };

            this._logArea = new TextArea
            {
                ReadOnly = true,
                Font = new Font(FontFamilies.Monospace, 10),
            };

            this._runButton = new Button
            {
                Text = "Refresh again",
                MinimumSize = new Size(110, 30),
            };
            this._runButton.Click += (sender, e) => _ = this.RunRefreshAsync();

            var closeButton = new Button
            {
                Text = "Close",
                MinimumSize = new Size(80, 30),
                Command = new Command((sender, e) => this.Close()),
            };
            this.DefaultButton = closeButton;
            this.AbortButton = closeButton;

            this.Content = new TableLayout
            {
                Spacing = new Size(0, 10),
                Rows =
                {
                    new TableRow(this.BuildHeaderPanel()),
                    new TableRow(this._logArea) { ScaleHeight = true },
                    new TableRow(new StackLayout
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 10,
                        HorizontalContentAlignment = HorizontalAlignment.Right,
                        Items = { this._runButton, closeButton },
                    }),
                },
            };
        }

        private Control BuildHeaderPanel()
        {
            var titleLabel = new Label
            {
                Text = "Signed Model Catalogue",
                Font = new Font(SystemFont.Bold, 14),
                TextColor = SystemColors.ControlText,
            };

            string platform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "net7.0-windows" : "net7.0";
            var infoLabel = new Label
            {
                Text = $"Endpoint: {ModelCatalogueTrust.DefaultBaseUrl}  |  Version: {VersionHelper.GetDisplayVersion()}  |  Platform: {platform}",
                Font = new Font(SystemFont.Default, 10),
                TextColor = SystemColors.DisabledText,
            };

            var descriptionLabel = new Label
            {
                Text = "Model metadata (capabilities, pricing, aliases) is fetched lazily from models.smarthopper.xyz,\n" +
                       "verified against an embedded public key before it is applied, and cached locally.\n" +
                       "When the service is unreachable SmartHopper keeps working on the verified cache\n" +
                       "or the built-in model tables.",
                Font = new Font(SystemFont.Default, 10),
                TextColor = SystemColors.DisabledText,
                Wrap = WrapMode.Word,
            };

            return new StackLayout
            {
                Spacing = 5,
                Items = { titleLabel, infoLabel, descriptionLabel, this._statusLabel },
            };
        }

        private static string StatusText()
        {
            var service = ModelCatalogueService.Instance;
            var source = service.Source switch
            {
                ModelCatalogueSource.Remote => "remote service",
                ModelCatalogueSource.Cache => "verified local cache",
                _ => "built-in model tables",
            };
            var applied = service.AppliedVersion > 0
                ? $"catalogue v{service.AppliedVersion} ({source})"
                : $"no catalogue ({source})";
            return $"Applied: {applied}" + (service.LastError != null ? $"   |   Last error: {service.LastError}" : string.Empty);
        }

        private void AppendLog(string line)
        {
            this._logArea.Text += line + Environment.NewLine;
        }

        private async Task RunRefreshAsync()
        {
            if (this._running)
            {
                return;
            }

            this._running = true;
            this._runButton.Enabled = false;
            this.AppendLog($"── {DateTime.Now:HH:mm:ss} — refresh requested ──");

            var service = ModelCatalogueService.Instance;
            EventHandler<ModelCatalogueLogEventArgs> handler = (sender, e) =>
                Application.Instance?.AsyncInvoke(() => this.AppendLog(e.Message));
            service.RefreshLog += handler;
            try
            {
                await service.RefreshAsync(force: true).ConfigureAwait(false);
            }
            finally
            {
                service.RefreshLog -= handler;
            }

            Application.Instance?.AsyncInvoke(() =>
            {
                this._statusLabel.Text = StatusText();
                this.AppendLog(ConclusionLine());
                this._runButton.Enabled = true;
                this._running = false;
            });
        }

        private static string ConclusionLine()
        {
            var service = ModelCatalogueService.Instance;
            switch (service.Source)
            {
                case ModelCatalogueSource.Remote:
                    return $"✔ SUCCESS — applied catalogue v{service.AppliedVersion} from models.smarthopper.xyz.";
                case ModelCatalogueSource.Cache:
                    return $"✔ OFFLINE OK — verified local catalogue v{service.AppliedVersion} applied; the service was unreachable or had no newer release.";
                default:
                    return "✖ NO CATALOGUE — using the built-in model tables." +
                           (service.LastError != null ? $" Reason: {service.LastError}" : string.Empty);
            }
        }

        /// <summary>Shows the dialog and immediately runs a refresh.</summary>
        public static void Show()
        {
            var dlg = new CatalogueRefreshDialog();
            dlg.Shown += (sender, e) => _ = dlg.RunRefreshAsync();
            dlg.ShowModal(RhinoEtoApp.MainWindow);
        }

        /// <inheritdoc/>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            var referenceWindow = RhinoEtoApp.MainWindow ?? this.Owner;
            var targetScreen = referenceWindow != null
                ? Screen.FromRectangle(referenceWindow.Bounds)
                : Screen.PrimaryScreen;

            if (targetScreen != null)
            {
                var workArea = targetScreen.WorkingArea;
                var center = workArea.Center;
                this.Location = new Point(
                    (int)(center.X - (this.Width / 2)),
                    (int)(center.Y - (this.Height / 2)));
            }
        }
    }
}
