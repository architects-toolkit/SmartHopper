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
using System.IO;
using System.Reflection;
using Eto.Drawing;
using Eto.Forms;
using Rhino.UI;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Infrastructure.Properties;

namespace SmartHopper.Infrastructure.Dialogs
{
    /// <summary>
    /// Specialized dialog for displaying provider hash verification results with Eto.Forms styling.
    /// </summary>
    internal class ProviderVerificationDialog : Dialog
    {
        private const int ContentWidth = 720;
        private const int DialogWidth = ContentWidth + 260; // allows padding, status column, margins

        private static readonly Assembly ConfigAssembly = typeof(providersResources).Assembly;
        private const string IconResourceName = "SmartHopper.Infrastructure.Resources.smarthopper.ico";

        private readonly Dictionary<string, ProviderVerificationResult> _results;
        private readonly string _version;
        private readonly string _platform;

        public ProviderVerificationDialog(Dictionary<string, ProviderVerificationResult> results, string version, string platform)
        {
            _results = results;
            _version = version;
            _platform = platform;

            this.Title = "Provider Verification - SmartHopper";
            this.Resizable = true;
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

            this.Content = BuildContent();
        }

        private Control BuildContent()
        {
            var contentStack = new StackLayout
            {
                Spacing = 15,
                Items =
                {
                    BuildHeaderPanel(),
                    BuildResultsPanel(),
                    BuildSummaryPanel(),
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
                    new TableRow(BuildButtonPanel())
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
                Text = $"Version: {_version}  |  Platform: {_platform}",
                Font = new Font(SystemFont.Default, 10),
                TextColor = SystemColors.DisabledText,
            };

            return new StackLayout
            {
                Spacing = 5,
                Items = { titleLabel, infoLabel }
            };
        }

        private Control BuildResultsPanel()
        {
            var resultsList = new StackLayout
            {
                Spacing = 10,
                Items = { }
            };

            foreach (var result in _results)
            {
                resultsList.Items.Add(BuildProviderResultItem(result.Key, result.Value));
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
            string statusText = "";
            Color statusColor = Colors.Black;
            string tooltip = "";

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
                detailsLayout.Items.Add(new Label
                {
                    Text = $"Local:  {verification.LocalHash}",
                    Font = new Font(SystemFont.Default, 9),
                    TextColor = SystemColors.DisabledText,
                    Wrap = WrapMode.Word,
                    Width = ContentWidth,
                });
            }

            if (!string.IsNullOrEmpty(verification.PublicHash))
            {
                detailsLayout.Items.Add(new Label
                {
                    Text = $"Expected: {verification.PublicHash}",
                    Font = new Font(SystemFont.Default, 9),
                    TextColor = SystemColors.DisabledText,
                    Wrap = WrapMode.Word,
                    Width = ContentWidth,
                });
            }

            if (!string.IsNullOrEmpty(verification.ErrorMessage))
            {
                detailsLayout.Items.Add(new Label
                {
                    Text = $"Note: {verification.ErrorMessage}",
                    Font = new Font(SystemFont.Default, 9),
                    TextColor = Colors.DarkGray,
                    Wrap = WrapMode.Word,
                    Width = ContentWidth,
                });
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

        private Control BuildSummaryPanel()
        {
            int matchCount = 0;
            int mismatchCount = 0;
            int unavailableCount = 0;
            int notFoundCount = 0;

            foreach (var result in _results.Values)
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
                Items = { }
            };

            summaryItems.Items.Add(new Label
            {
                Text = $"Total Providers: {_results.Count}",
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
            var okButton = new Button
            {
                Text = "OK",
                MinimumSize = new Size(80, 30),
                Command = new Command((sender, e) => this.Close())
            };

            return new StackLayout
            {
                Orientation = Orientation.Horizontal,
                HorizontalContentAlignment = HorizontalAlignment.Right,
                Items = { okButton }
            };
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
