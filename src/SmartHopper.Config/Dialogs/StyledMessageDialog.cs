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
using Eto.Forms;
using Eto.Drawing;
using SmartHopper.Config.Properties;
using Rhino.UI;
using System.Reflection;

namespace SmartHopper.Config.Dialogs
{
    /// <summary>
    /// Provides styled Eto.Forms dialogs for displaying info, warning, error messages, and confirmations with the SmartHopper logo.
    /// </summary>
    public class StyledMessageDialog : Dialog
    {
        private bool _result;

        private static readonly Assembly ConfigAssembly = typeof(providersResources).Assembly;
        private const string IconResourceName = "SmartHopper.Config.Resources.smarthopper.ico";

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

        private StyledMessageDialog(string title, string message, DialogType dialogType, bool isConfirmation)
        {
            Title = title;
            Resizable = true;
            Padding = new Padding(20);
            Size = new Size(400, 300);
            MinimumSize = new Size(400, 300);

            // Set window icon from embedded resource
            using (var stream = ConfigAssembly.GetManifestResourceStream(IconResourceName))
            {
                if (stream != null)
                    Icon = new Eto.Drawing.Icon(stream);
            }

            // Create smaller logo
            var logoView = new ImageView
            {
                Image = Logo,
                Size = new Size(32, 32)
            };

            // Create title label
            var titleLabel = new Label
            {
                Text = "SmartHopper says...",
                Font = new Font(SystemFont.Bold, 16),
                TextAlignment = TextAlignment.Center
            };

            // Header with logo and title
            var headerLayout = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Spacing = 10,
                Items = { logoView, new StackLayoutItem(titleLabel, VerticalAlignment.Center) }
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

            var bodyLabel = new Label
            {
                Text = message,
                Wrap = WrapMode.Word,
                Font = new Font(SystemFont.Default, 12),
                TextColor = Colors.Black,
                TextAlignment = TextAlignment.Left,
            };

            // Message container with only the body
            var messageContainer = new TableLayout
            {
                Spacing = new Size(5, 5),
                Rows =
                {
                    new TableRow(new TableCell(bodyLabel, true))
                }
            };

            // Button layout with right alignment
            var buttonLayout = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10,
                HorizontalContentAlignment = HorizontalAlignment.Right
            };

            if (isConfirmation)
            {
                var yesButton = new Button { Text = "Yes", MinimumSize = new Size(80, 30) };
                var noButton = new Button { Text = "No", MinimumSize = new Size(80, 30) };
                
                yesButton.Click += (sender, e) => { _result = true; Close(); };
                noButton.Click += (sender, e) => { _result = false; Close(); };
                
                buttonLayout.Items.Add(yesButton);
                buttonLayout.Items.Add(noButton);
                DefaultButton = yesButton;
            }
            else
            {
                var okButton = new Button { Text = "OK", MinimumSize = new Size(80, 30) };
                okButton.Click += (sender, e) => { Close(); };
                buttonLayout.Items.Add(okButton);
                DefaultButton = okButton;
            }

            // Main content layout
            Content = new TableLayout
            {
                Padding = new Padding(10),
                Spacing = new Size(25, 20),
                Rows =
                {
                    // Header row centered
                    new TableRow(
                        new TableCell(
                            new StackLayout 
                            { 
                                HorizontalContentAlignment = HorizontalAlignment.Center,
                                Items = { headerLayout }
                            }
                        )
                    ),
                    
                    // Message row left-aligned (default)
                    new TableRow(messageContainer),
                    
                    // Button row right-aligned with extra top padding
                    new TableRow(
                        new TableCell(
                            new StackLayout
                            {
                                Padding = new Padding(0, 20, 0, 0),
                                HorizontalContentAlignment = HorizontalAlignment.Right,
                                Items = { buttonLayout }
                            }
                        )
                    )
                }
            };
        }

        /// <summary>
        /// Dialog type enumeration for styling purposes.
        /// </summary>
        private enum DialogType
        {
            Info,
            Warning,
            Error
        }

        /// <summary>
        /// Shows an information dialog.
        /// </summary>
        public static void ShowInfo(string message, string title = "SmartHopper")
        {
            var dlg = new StyledMessageDialog(title, message, DialogType.Info, false);
            dlg.ShowModal(RhinoEtoApp.MainWindow);
        }

        /// <summary>
        /// Shows a warning dialog.
        /// </summary>
        public static void ShowWarning(string message, string title = "SmartHopper")
        {
            var dlg = new StyledMessageDialog(title, message, DialogType.Warning, false);
            dlg.ShowModal(RhinoEtoApp.MainWindow);
        }

        /// <summary>
        /// Shows an error dialog.
        /// </summary>
        public static void ShowError(string message, string title = "SmartHopper")
        {
            var dlg = new StyledMessageDialog(title, message, DialogType.Error, false);
            dlg.ShowModal(RhinoEtoApp.MainWindow);
        }

        /// <summary>
        /// Shows a confirmation dialog with Yes and No options.
        /// </summary>
        /// <returns>True if Yes was clicked; otherwise, false.</returns>
        public static bool ShowConfirmation(string message, string title = "SmartHopper")
        {
            var dlg = new StyledMessageDialog(title, message, DialogType.Info, true);
            dlg.ShowModal(RhinoEtoApp.MainWindow);
            return dlg._result;
        }
    }
}
