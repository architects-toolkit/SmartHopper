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

/*
 * StyledMessageDialog.cs
 * Provides styled Eto.Forms dialogs with SmartHopper branding for error, warning, info messages, and confirmations.
 * Uses DynamicLayout for responsive text wrapping and dynamic content adaptation.
 */

using System;
using System.IO;
using System.Reflection;
using Eto.Drawing;
using Eto.Forms;
using Rhino.UI;
using SmartHopper.Infrastructure.Properties;

namespace SmartHopper.Infrastructure.Dialogs
{
    /// <summary>
    /// Provides styled Eto.Forms dialogs for displaying info, warning, error messages, and confirmations with the SmartHopper logo.
    /// Uses DynamicLayout for responsive content that properly wraps and adapts to dialog size.
    /// </summary>
    internal class StyledMessageDialog : Dialog
    {
        private bool _result;
        private readonly Guid _linkedInstanceGuid = Guid.Empty;
        private readonly System.Drawing.Color? _linkLineColor;

        /// <summary>
        /// Callback to register a dialog-component canvas link. Set by SmartHopper.Core at initialization.
        /// Signature: (Window dialog, Guid instanceGuid, Color? lineColor) => void
        /// </summary>
        public static Action<Window, Guid, System.Drawing.Color?>? RegisterCanvasLinkCallback { get; set; }

        private static readonly Assembly ConfigAssembly = typeof(providersResources).Assembly;
        private const string IconResourceName = "SmartHopper.Infrastructure.Resources.smarthopper.ico";

        /// <summary>
        /// Gets the SmartHopper logo image from embedded resources.
        /// </summary>
        private static Image Logo
        {
            get
            {
                using (var ms = new MemoryStream(providersResources.smarthopper_256))
                {
                    return new Bitmap(ms);
                }
            }
        }

        private StyledMessageDialog(string title, string message, DialogType dialogType, bool isConfirmation, Guid linkedInstanceGuid = default, System.Drawing.Color? linkLineColor = null)
        {
            this._linkedInstanceGuid = linkedInstanceGuid;
            this._linkLineColor = linkLineColor;
            this.Title = title;
            this.Resizable = false;
            this.Padding = new Padding(20);

            // Set window icon from embedded resource
            using (var stream = ConfigAssembly.GetManifestResourceStream(IconResourceName))
            {
                if (stream != null)
                {
                    this.Icon = new Eto.Drawing.Icon(stream);
                }
            }

            // Dialog sizing - dynamic based on content
            const int dialogWidth = 500;
            const int minDialogHeight = 250;
            const int maxDialogHeight = 600;
            const int maxContentHeight = 400;

            this.ClientSize = new Size(dialogWidth, this.CalculateDialogHeight(message, dialogWidth, minDialogHeight, maxDialogHeight));
            this.MinimumSize = new Size(dialogWidth, 200);

            // Create smaller logo
            var logoView = new ImageView
            {
                Image = Logo,
                Size = new Size(32, 32),
            };

            // Create title label
            var titleLabel = new Label
            {
                Text = "SmartHopper says...",
                Font = new Font(SystemFont.Bold, 16),
                TextAlignment = TextAlignment.Center,
            };

            // Build prefix and body labels, coloring only the prefix
            string prefixText;
            Color prefixColor;
            switch (dialogType)
            {
                case DialogType.Error:
                    prefixText = "Error: "; prefixColor = Colors.DarkRed;
                    break;
                case DialogType.Warning:
                    prefixText = "Warning: "; prefixColor = Colors.DarkOrange;
                    break;
                default:
                    prefixText = string.Empty; prefixColor = Colors.Black;
                    break;
            }

            titleLabel.Text = prefixText + titleLabel.Text;
            titleLabel.TextColor = prefixColor;

            // Header with logo and title - centered using StackLayout (compatible with Rhino Eto.Forms)
            var headerLayout = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Spacing = 10,
                Items = { logoView, new StackLayoutItem(titleLabel, VerticalAlignment.Center) }
            };

            // Body label with word wrapping - explicit width required for wrapping inside Scrollable
            var bodyLabel = new Label
            {
                Text = message,
                Wrap = WrapMode.Word,
                Font = new Font(SystemFont.Default, 12),
                TextColor = Colors.Black,
                TextAlignment = TextAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Width = dialogWidth - 60 - 20 // Dialog width minus padding (20*2 + 10*2)
            };

            // Wrap message in scrollable for long content
            var scrollableMessage = new Scrollable
            {
                Content = bodyLabel,
                Border = BorderType.None,
                ExpandContentWidth = false,
                ExpandContentHeight = false,
            };

            // Message container
            var messageContainer = new TableLayout
            {
                Spacing = new Size(0, 0),
                Rows =
                {
                    new TableRow(new TableCell(scrollableMessage, true)) { ScaleHeight = true },
                },
            };

            // Button layout with right alignment
            var buttonLayout = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                HorizontalContentAlignment = HorizontalAlignment.Right,
            };

            if (isConfirmation)
            {
                var yesButton = new Button { Text = "Yes", MinimumSize = new Size(80, 30) };
                var noButton = new Button { Text = "No", MinimumSize = new Size(80, 30) };

                yesButton.Click += (sender, e) =>
                {
                    this._result = true;
                    this.Close();
                };
                noButton.Click += (sender, e) =>
                {
                    this._result = false;
                    this.Close();
                };

                buttonLayout.Items.Add(yesButton);
                buttonLayout.Items.Add(noButton);
                this.DefaultButton = yesButton;
            }
            else
            {
                var okButton = new Button { Text = "OK", MinimumSize = new Size(80, 30) };
                okButton.Click += (sender, e) =>
                {
                    this.Close();
                };

                buttonLayout.Items.Add(okButton);
                this.DefaultButton = okButton;
            }

            // Main content layout
            this.Content = new TableLayout
            {
                Padding = new Padding(10),
                Spacing = new Size(10, 15),
                Rows =
                {
                    // Header row centered
                    new TableRow(
                        new TableCell(
                            new StackLayout
                            {
                                HorizontalContentAlignment = HorizontalAlignment.Center,
                                Items = { headerLayout }
                            })),

                    // Message row - expands vertically
                    new TableRow(messageContainer) { ScaleHeight = true },

                    // Button row right-aligned
                    new TableRow(
                        new TableCell(
                            new StackLayout
                            {
                                Padding = new Padding(0, 10, 0, 0),
                                HorizontalContentAlignment = HorizontalAlignment.Right,
                                Items = { buttonLayout }
                            }))
                },
            };
        }

        /// <summary>
        /// Calculates an appropriate dialog height based on message length.
        /// </summary>
        private int CalculateDialogHeight(string message, int dialogWidth, int minHeight, int maxHeight)
        {
            // Estimate lines based on character count and average chars per line
            const int avgCharsPerLine = 55; // Approximate for 500px width with 12pt font
            int estimatedLines = Math.Max(1, (int)Math.Ceiling(message.Length / (double)avgCharsPerLine));

            // Account for explicit line breaks
            estimatedLines += message.Split('\n').Length - 1;

            // Calculate height: header (~80px) + content lines (~20px each) + buttons (~80px) + padding (~60px)
            int contentHeight = estimatedLines * 20;
            int totalHeight = 80 + contentHeight + 80 + 60;

            // Clamp to min/max bounds
            return Math.Min(maxHeight, Math.Max(minHeight, totalHeight));
        }

        private enum DialogType
        {
            Info,
            Warning,
            Error,
        }

        /// <summary>
        /// Shows an information dialog.
        /// </summary>
        /// <param name="message">The message to display in the dialog body.</param>
        /// <param name="title">The dialog window title.</param>
        /// <param name="linkedInstanceGuid">Optional GUID of a component to visually link to on the canvas.</param>
        /// <param name="linkLineColor">Optional custom color for the link line.</param>
        public static void ShowInfo(string message, string title = "SmartHopper", Guid linkedInstanceGuid = default, System.Drawing.Color? linkLineColor = null)
        {
            var dlg = new StyledMessageDialog(title, message, DialogType.Info, false, linkedInstanceGuid, linkLineColor);
            dlg.ShowWithLink(RhinoEtoApp.MainWindow);
        }

        /// <summary>
        /// Shows a warning dialog.
        /// </summary>
        /// <param name="message">The message to display in the dialog body.</param>
        /// <param name="title">The dialog window title.</param>
        /// <param name="linkedInstanceGuid">Optional GUID of a component to visually link to on the canvas.</param>
        /// <param name="linkLineColor">Optional custom color for the link line.</param>
        public static void ShowWarning(string message, string title = "SmartHopper", Guid linkedInstanceGuid = default, System.Drawing.Color? linkLineColor = null)
        {
            var dlg = new StyledMessageDialog(title, message, DialogType.Warning, false, linkedInstanceGuid, linkLineColor);
            dlg.ShowWithLink(RhinoEtoApp.MainWindow);
        }

        /// <summary>
        /// Shows an error dialog.
        /// </summary>
        /// <param name="message">The message to display in the dialog body.</param>
        /// <param name="title">The dialog window title.</param>
        /// <param name="linkedInstanceGuid">Optional GUID of a component to visually link to on the canvas.</param>
        /// <param name="linkLineColor">Optional custom color for the link line.</param>
        public static void ShowError(string message, string title = "SmartHopper", Guid linkedInstanceGuid = default, System.Drawing.Color? linkLineColor = null)
        {
            var dlg = new StyledMessageDialog(title, message, DialogType.Error, false, linkedInstanceGuid, linkLineColor);
            dlg.ShowWithLink(RhinoEtoApp.MainWindow);
        }

        /// <summary>
        /// Shows a confirmation dialog with Yes and No options.
        /// </summary>
        /// <returns>True if Yes was clicked; otherwise, false.</returns>
        /// <param name="message">The message to display in the dialog body.</param>
        /// <param name="title">The dialog window title.</param>
        /// <param name="linkedInstanceGuid">Optional GUID of a component to visually link to on the canvas.</param>
        /// <param name="linkLineColor">Optional custom color for the link line.</param>
        public static bool ShowConfirmation(string message, string title = "SmartHopper", Guid linkedInstanceGuid = default, System.Drawing.Color? linkLineColor = null)
        {
            var dlg = new StyledMessageDialog(title, message, DialogType.Info, true, linkedInstanceGuid, linkLineColor);
            dlg.ShowWithLink(RhinoEtoApp.MainWindow);
            return dlg._result;
        }

        /// <summary>
        /// Shows the dialog with optional canvas link visualization.
        /// </summary>
        /// <param name="parent">The parent window.</param>
        private void ShowWithLink(Window parent)
        {
            // Register link if we have a valid instance GUID and the callback is set
            if (this._linkedInstanceGuid != Guid.Empty && RegisterCanvasLinkCallback != null)
            {
                RegisterCanvasLinkCallback(this, this._linkedInstanceGuid, this._linkLineColor);
            }

            // Show the dialog
            this.ShowModal(parent);

            // Link is automatically unregistered when dialog closes via DialogCanvasLink.OnDialogClosed
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
