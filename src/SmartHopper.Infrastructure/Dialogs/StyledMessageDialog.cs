/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

/*
 * StyledMessageDialog.cs
 * Provides styled Eto.Forms dialogs with SmartHopper branding for error, warning, info messages, and confirmations.
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
            this.Resizable = true;
            this.Padding = new Padding(20);

            // Set window icon from embedded resource
            using (var stream = ConfigAssembly.GetManifestResourceStream(IconResourceName))
            {
                if (stream != null)
                {
                    this.Icon = new Eto.Drawing.Icon(stream);
                }
            }

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

            // Header with logo and title
            var headerLayout = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Spacing = 10,
                Items = { logoView, new StackLayoutItem(titleLabel, VerticalAlignment.Center) },
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
                    prefixText = ""; prefixColor = Colors.Black;
                    break;
            }

            titleLabel.Text = prefixText + titleLabel.Text;
            titleLabel.TextColor = prefixColor;

            // Calculate required height based on message content
            const int dialogWidth = 400;
            const int textWidth = 360;
            const int lineHeight = 24; // Line height for 12pt font with spacing
            const int charsPerLine = 42; // Conservative estimate for wrapped text

            // Count actual lines (including newlines and wrapped text)
            var lines = message.Split('\n');
            var totalLines = 0;
            foreach (var line in lines)
            {
                // Empty lines count as 1 (paragraph spacing)
                if (string.IsNullOrEmpty(line))
                {
                    totalLines++;
                }
                else
                {
                    // Calculate wrapped lines with conservative estimate
                    totalLines += Math.Max(1, (int)Math.Ceiling((double)line.Length / charsPerLine));
                }
            }

            // Calculate content height:
            // - Header (logo + title): ~60px
            // - Message area: lines * lineHeight
            // - Spacing between sections: ~20px
            // - Button row: ~50px
            // - Dialog padding (top + bottom): ~40px
            // - Extra buffer for word wrapping variance: ~20px
            var messageHeight = totalLines * lineHeight;
            var contentHeight = 60 + messageHeight + 20 + 50 + 40 + 20;
            var dialogHeight = Math.Max(240, Math.Min(contentHeight, 600)); // Clamp between 240 and 600

            this.Size = new Size(dialogWidth, dialogHeight);
            this.MinimumSize = new Size(350, 200);

            var bodyLabel = new Label
            {
                Text = message,
                Wrap = WrapMode.Word,
                Font = new Font(SystemFont.Default, 12),
                TextColor = Colors.Black,
                TextAlignment = TextAlignment.Left,
                Width = textWidth,
            };

            // Message container with only the body
            var messageContainer = new TableLayout
            {
                Spacing = new Size(5, 5),
                Rows =
                {
                    new TableRow(new TableCell(bodyLabel, true)),
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

            // Main content layout - use ScaleHeight on message row to expand vertically
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

                    // Message row left-aligned - use ScaleHeight to allow vertical expansion
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
        /// Dialog type enumeration for styling purposes.
        /// </summary>
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
    }
}
