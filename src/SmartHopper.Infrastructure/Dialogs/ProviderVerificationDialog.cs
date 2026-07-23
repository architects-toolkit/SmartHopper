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
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Eto.Drawing;
using Eto.Forms;
using Rhino.UI;
using SmartHopper.ProviderSdk.AIProviders;
using SmartHopper.Infrastructure.Properties;
using SmartHopper.Infrastructure.AIProviders;

namespace SmartHopper.Infrastructure.Dialogs
{
    /// <summary>
    /// Specialized dialog for displaying provider hash verification results with Eto.Forms styling.
    /// </summary>
    internal class ProviderVerificationDialog : Dialog
    {
        private const int ContentWidth = 420;
        private const int DialogWidth = ContentWidth + 240; // allows padding, status column, margins

        private static readonly Assembly ConfigAssembly = typeof(providersResources).Assembly;
        private const string IconResourceName = "SmartHopper.Infrastructure.Resources.smarthopper.ico";

        private readonly Dictionary<string, ProviderVerificationResult> _results;
        private readonly string _version;
        private readonly string _platform;

        public ProviderVerificationDialog(Dictionary<string, ProviderVerificationResult> results, string version, string platform)
        {
            this._results = results;
            this._version = version;
            this._platform = platform;

            this.Title = "Provider Verification - SmartHopper";
            this.Resizable = false;
            this.Size = new Size(DialogWidth, 650);
            this.MinimumSize = new Size(ContentWidth + 100, 450);
            this.Padding = new Padding(20);

            // Set window icon from embedded resource
            using (var stream = ConfigAssembly.GetManifestResourceStream(IconResourceName))
            {
                if (stream != null)
                {
                    this.Icon = new Eto.Drawing.Icon(stream);
                }
            }

            this.Content = this.BuildContent();
        }

        private Control BuildContent()
        {
            var contentStack = new StackLayout
            {
                Spacing = 15,
                Items =
                {
                    this.BuildHeaderPanel(),
                    this.BuildDescriptionPanel(),
                    this.BuildResultsPanel(),
                    this.BuildSummaryPanel(),
                }
            };

            var scrollable = new Scrollable
            {
                Border = BorderType.None,
                ExpandContentWidth = true,
                ExpandContentHeight = false,
                Content = contentStack
            };

            return new TableLayout
            {
                Spacing = new Size(0, 10),
                Rows =
                {
                    new TableRow(scrollable) { ScaleHeight = true },
                    new TableRow(this.BuildButtonPanel())
                }
            };
        }

        private Control BuildHeaderPanel()
        {
            var titleLabel = new Label
            {
                Text = "Provider Hash Verification Results",
                Font = new Font(SystemFont.Bold, 14),
                TextColor = SystemColors.ControlText,
            };

            var infoLabel = new Label
            {
                Text = $"Version: {this._version}  |  Platform: {this._platform}",
                Font = new Font(SystemFont.Default, 10),
                TextColor = SystemColors.DisabledText,
            };

            return new StackLayout
            {
                Spacing = 5,
                Items = { titleLabel, infoLabel }
            };
        }

        private Control BuildDescriptionPanel()
        {
            var descriptionLabel = new Label
            {
                Text = "SmartHopper verifies that AI providers match the official published hashes.\nMismatches may indicate file corruption or tampering.",
                Font = new Font(SystemFont.Default, 10),
                TextColor = SystemColors.DisabledText,
                Wrap = WrapMode.Word,
            };

            // Clickable link to GitHub documentation
            var helpLink = new Label
            {
                Text = "Read more about the integrity check failure risks in SmartHopper documentation",
                Font = new Font(SystemFont.Default, 10),
                TextColor = Colors.DodgerBlue,
                Cursor = Cursors.Pointer,
                Wrap = WrapMode.Word,
            };

            helpLink.MouseDown += (sender, e) => this.ShowHelpPage();

            return new StackLayout
            {
                Spacing = 8,
                Items = { descriptionLabel, helpLink }
            };
        }

        private Control BuildResultsPanel()
        {
            var resultsList = new StackLayout
            {
                Spacing = 10,
                Items = { }
            };

            foreach (var result in this._results)
            {
                resultsList.Items.Add(this.BuildProviderResultItem(result.Key, result.Value));
            }

            var scrollable = new Scrollable
            {
                Border = BorderType.None,
                ExpandContentHeight = false,
                ExpandContentWidth = true,
                Content = resultsList
            };

            return scrollable;
        }

        private Control BuildProviderResultItem(string dllName, ProviderVerificationResult verification)
        {
            string statusText = string.Empty;
            Color statusColor = Colors.Black;
            string tooltip = string.Empty;

            switch (verification.Status)
            {
                case ProviderVerificationStatus.Match:
                    statusText = "✓ VERIFIED";
                    statusColor = Colors.DarkGreen;
                    tooltip = "Provider hash matches the official SmartHopper repository.\nThis provider is authentic and safe to use.";
                    break;
                case ProviderVerificationStatus.Mismatch:
                    statusText = "✗ MISMATCH";
                    statusColor = Colors.DarkRed;
                    tooltip = "Provider hash does NOT match official records.\n⚠️ SECURITY WARNING: This indicates potential tampering or corruption.\nACTION: Do not use this provider. Re-download from official sources.";
                    break;
                case ProviderVerificationStatus.Unavailable:
                    statusText = "? UNAVAILABLE";
                    statusColor = Colors.DarkOrange;
                    tooltip = "Could not retrieve hash from official repository.\nThis may be due to network issues or repository unavailability.\nACTION: Check your internet connection and try again later.";
                    break;
                case ProviderVerificationStatus.NotFound:
                    statusText = "? NOT FOUND";
                    statusColor = Colors.DarkOrange;
                    tooltip = "Provider hash not found in official repository.\nThis is a custom or third-party provider not from SmartHopper.\nACTION: Ensure you trust the source before enabling this provider.";
                    break;
            }

            var statusLabel = new Label
            {
                Text = statusText,
                Font = new Font(SystemFont.Bold, 11),
                TextColor = statusColor,
                Width = 120,
                ToolTip = tooltip
            };

            var dllLabel = new Label
            {
                Text = dllName,
                Font = new Font(SystemFont.Default, 11),
                TextColor = SystemColors.ControlText,
                Wrap = WrapMode.Word,
                Width = ContentWidth,
            };

            var detailsLayout = new StackLayout
            {
                Spacing = 3,
                Items = { }
            };

            if (!string.IsNullOrEmpty(verification.LocalHash))
            {
                if (verification.Status == ProviderVerificationStatus.Match &&
                    verification.LocalHash == verification.PublicHash)
                {
                    // For verified providers with matching hashes, show only one hash
                    var hashLabel = this.CreateClickableHashLabel($"Hash: {verification.LocalHash}", verification.LocalHash);
                    detailsLayout.Items.Add(hashLabel);
                }
                else
                {
                    // For other statuses, show local hash inline
                    var localHashLabel = this.CreateClickableHashLabel($"Local: {verification.LocalHash}", verification.LocalHash);
                    detailsLayout.Items.Add(localHashLabel);
                }
            }

            if (!string.IsNullOrEmpty(verification.PublicHash) &&
                !(verification.Status == ProviderVerificationStatus.Match &&
                  verification.LocalHash == verification.PublicHash))
            {
                // Show expected hash inline (only when different from local)
                var expectedHashLabel = this.CreateClickableHashLabel($"Expected: {verification.PublicHash}", verification.PublicHash);
                detailsLayout.Items.Add(expectedHashLabel);
            }

            var headerLayout = new TableLayout
            {
                Spacing = new Size(10, 0),
                Rows =
                {
                    new TableRow(
                        new TableCell(statusLabel, false),
                        new TableCell(dllLabel, true))
                }
            };

            var itemLayout = new StackLayout
            {
                Spacing = 5,
                BackgroundColor = Colors.White,
                Padding = new Padding(10),
                Items =
                {
                    headerLayout,
                    detailsLayout
                }
            };

            return itemLayout;
        }

        private Label CreateClickableHashLabel(string displayText, string hashValue)
        {
            var label = new Label
            {
                Text = displayText,
                Font = new Font(SystemFont.Default, 9),
                TextColor = Colors.Gray,
                Wrap = WrapMode.Word,
                Width = ContentWidth,
                Cursor = Cursors.Pointer,
                ToolTip = "Click to copy hash to clipboard"
            };

            label.MouseDown += (sender, e) =>
            {
                try
                {
                    var clipboard = new Clipboard();
                    clipboard.Text = hashValue;
                    label.TextColor = Colors.DarkGreen;
                    label.ToolTip = "Copied!";

                    // Reset color after a short delay
                    Task.Run(async () =>
                    {
                        await Task.Delay(1000).ConfigureAwait(false);
                        Rhino.RhinoApp.InvokeOnUiThread(new Action(() =>
                        {
                            label.TextColor = Colors.Gray;
                            label.ToolTip = "Click to copy hash to clipboard";
                        }));
                    });
                }
                catch
                {
                    // Silently ignore clipboard errors
                }
            };

            return label;
        }

        private void ShowHelpPage()
        {
            try
            {
                string helpUrl = "https://github.com/architects-toolkit/SmartHopper/blob/main/docs/Help/integrity-check-failure.md";
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(helpUrl)
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Could not open help page: {ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxType.Error);
            }
        }

        private Control BuildSummaryPanel()
        {
            int matchCount = 0;
            int mismatchCount = 0;
            int unavailableCount = 0;
            int notFoundCount = 0;

            foreach (var result in this._results.Values)
            {
                switch (result.Status)
                {
                    case ProviderVerificationStatus.Match:
                        matchCount++;
                        break;
                    case ProviderVerificationStatus.Mismatch:
                        mismatchCount++;
                        break;
                    case ProviderVerificationStatus.Unavailable:
                        unavailableCount++;
                        break;
                    case ProviderVerificationStatus.NotFound:
                        notFoundCount++;
                        break;
                }
            }

            var summaryItems = new StackLayout
            {
                Spacing = 5,
                Items = { },
            };

            summaryItems.Items.Add(new Label
            {
                Text = $"Total Providers: {this._results.Count}",
                Font = new Font(SystemFont.Bold, 11),
            });

            summaryItems.Items.Add(new Label
            {
                Text = $"✓ Verified: {matchCount}",
                Font = new Font(SystemFont.Default, 10),
                TextColor = Colors.DarkGreen,
            });

            summaryItems.Items.Add(new Label
            {
                Text = mismatchCount > 0 ? $"✗ Mismatched: {mismatchCount} ⚠️" : "✗ Mismatched: 0",
                Font = new Font(SystemFont.Default, 10),
                TextColor = Colors.DarkRed,
            });

            if (unavailableCount > 0)
            {
                summaryItems.Items.Add(new Label
                {
                    Text = $"? Unavailable: {unavailableCount}",
                    Font = new Font(SystemFont.Default, 10),
                    TextColor = Colors.DarkOrange,
                });
            }

            if (notFoundCount > 0)
            {
                summaryItems.Items.Add(new Label
                {
                    Text = $"? Not Found: {notFoundCount}",
                    Font = new Font(SystemFont.Default, 10),
                    TextColor = Colors.DarkOrange,
                });
            }

            return new StackLayout
            {
                Spacing = 5,
                BackgroundColor = Colors.White,
                Padding = new Padding(10),
                Items = { summaryItems }
            };
        }

        private Control BuildButtonPanel()
        {
            var buttonLayout = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                HorizontalContentAlignment = HorizontalAlignment.Right,
                Items = { }
            };

            // Add "Which are the risks?" button if there are mismatches
            bool hasMismatches = false;
            foreach (var result in this._results.Values)
            {
                if (result.Status == ProviderVerificationStatus.Mismatch)
                {
                    hasMismatches = true;
                    break;
                }
            }

            if (hasMismatches)
            {
                var helpButton = new Button
                {
                    Text = "Which are the risks?",
                    MinimumSize = new Size(120, 30),
                };
                helpButton.Click += (sender, e) => this.ShowHelpPage();
                buttonLayout.Items.Add(helpButton);
            }

            var okButton = new Button
            {
                Text = "OK",
                MinimumSize = new Size(80, 30),
                Command = new Command((sender, e) => this.Close())
            };
            buttonLayout.Items.Add(okButton);

            return buttonLayout;
        }

        /// <summary>
        /// Shows the provider verification results dialog.
        /// </summary>
        public static void Show(Dictionary<string, ProviderVerificationResult> results, string version, string platform, bool hasErrors)
        {
            var dlg = new ProviderVerificationDialog(results, version, platform);
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