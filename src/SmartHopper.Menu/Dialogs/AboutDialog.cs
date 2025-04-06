/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using Eto.Forms;
using Eto.Drawing;
using System;
using System.Diagnostics;
using System.IO;
using SmartHopper.Config.Properties;

namespace SmartHopper.Menu.Dialogs
{
    /// <summary>
    /// Dialog to display information about SmartHopper, including version, copyright, and support information
    /// </summary>
    internal class AboutDialog : Dialog
    {
        private const string GitHubUrl = "https://github.com/architects-toolkit/SmartHopper";
        private const string RktkUrl = "https://rktk.tools";

        /// <summary>
        /// Initializes a new instance of the AboutDialog
        /// </summary>
        /// <param name="version">The version of SmartHopper to display</param>
        public AboutDialog(string version)
        {
            Title = "About SmartHopper";
            Resizable = true;
            Size = new Size(800, 550);
            MinimumSize = new Size(600, 500);
            Padding = new Padding(20);

            var mainLayout = new TableLayout
            {
                Spacing = new Size(20, 0),
                Rows = { new TableRow { Cells = { CreateLogoPanel(), CreateContentPanel(version) } } }
            };

            Content = mainLayout;

            // Center the dialog on screen
            Location = new Point(
                (int)((Screen.PrimaryScreen.Bounds.Width - Size.Width) / 2),
                (int)((Screen.PrimaryScreen.Bounds.Height - Size.Height) / 2)
            );
        }

        private static Control CreateLogoPanel()
        {
            // Create an ImageView with the SmartHopper logo from the resources
            var imageView = new ImageView();
            
            // Convert the byte array to an Eto.Drawing.Image
            using (var ms = new MemoryStream(providersResources.smarthopper_256))
            {
                imageView.Image = new Bitmap(ms);
            }
            
            // Set size to match the previous placeholder
            imageView.Size = new Size(200, 200);

            return new StackLayout
            {
                Spacing = 0,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Top,
                Items = { new StackLayoutItem(imageView, false) }
            };
        }

        private Control CreateContentPanel(string version)
        {
            var titleLabel = new Label
            {
                Text = "SmartHopper",
                Font = new Font(SystemFont.Bold, 22),
                TextColor = SystemColors.ControlText
            };

            var subtitleLabel = new Label
            {
                Text = "An AI-powered assistant for Grasshopper3D",
                Font = new Font(SystemFont.Default, 12),
                TextColor = SystemColors.ControlText,
                Wrap = WrapMode.Word
            };

            var versionLabel = new Label
            {
                Text = $"Version {version}",
                Font = new Font(SystemFont.Default, 10),
                TextColor = SystemColors.DisabledText,
                Wrap = WrapMode.Word
            };

            var copyrightLabel = new Label
            {
                Text = "Copyright (c) 2024 Marc Roca Musach",
                Font = new Font(SystemFont.Default, 10),
                Wrap = WrapMode.Word
            };

            var licenseLabel = new Label
            {
                Text = "Licensed under GNU Lesser General Public License v3.0",
                Font = new Font(SystemFont.Default, 10),
                Wrap = WrapMode.Word
            };

            var supportLabel = new Label
            {
                Text = "Supported by:",
                Font = new Font(SystemFont.Default, 10),
                Wrap = WrapMode.Word
            };

            var supportLinkLabel = CreateLinkButton("Architect's Toolkit (RKTK.tools)", RktkUrl);
            
            var communityLabel = new Label
            { 
                Text = "and the SmartHopper Community",
                Font = new Font(SystemFont.Default, 10),
                Wrap = WrapMode.Word
            };

            var descriptionLabel = new Label
            {
                Text = "SmartHopper is an open-source project that implements third-party AI APIs to provide advanced features for Grasshopper.\nIt currently supports MistralAI and OpenAI.",
                Font = new Font(SystemFont.Default, 10),
                Wrap = WrapMode.Word
            };

            var warningLabel = new Label
            {
                Text = "KEEP IN MIND THAT SMARTHOPPER IS STILL IN ITS EARLY STAGES OF DEVELOPMENT AND MAY HAVE BUGS OR LIMITATIONS.",
                Font = new Font(SystemFont.Default, 10),
                Wrap = WrapMode.Word,
                TextColor = Colors.DarkRed
            };

            var aiDisclaimerLabel = new Label
            {
                Text = "AI Disclaimer: AI-generated content may contain errors. Users are responsible for reviewing, validating, and verifying all AI-generated outputs. " +
                       "SmartHopper serves as an interface between Grasshopper and user-provided 3rd-party AI services. " +
                       "The AI APIs are configured and provided by the user, and SmartHopper does not control or guarantee the accuracy of these services. " +
                       "SmartHopper and any of its authors, contributors, or affiliates are not liable for any errors or omissions in the content generated by these AI services.",
                Font = new Font(SystemFont.Default, 10),
                Wrap = WrapMode.Word,
                TextColor = Colors.DarkGray
            };

            var openSourceThanksLabel = new Label
            {
                Text = "Special Thanks to all the Open Source Projects that inspired or were used in this project:",
                Font = new Font(SystemFont.Default, 10),
                Wrap = WrapMode.Word,
                TextColor = Colors.DarkBlue
            };

            var speckleLink = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5,
                Items = 
                {
                    new Label { Text = "- Speckle Systems: ", Font = new Font(SystemFont.Default, 10) },
                    CreateLinkButton("GrasshopperAsyncComponent", "https://github.com/specklesystems/GrasshopperAsyncComponent"),
                    new Label { Text = " (Apache License 2.0)", Font = new Font(SystemFont.Default, 10) }
                }
            };

            var ghptLink = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5,
                Items = 
                {
                    new Label { Text = "- enmerk4r: ", Font = new Font(SystemFont.Default, 10) },
                    CreateLinkButton("GHPT", "https://github.com/enmerk4r/GHPT"),
                    new Label { Text = " (MIT License)", Font = new Font(SystemFont.Default, 10) }
                }
            };

            var materialIconsLink = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5,
                Items = 
                {
                    new Label { Text = "- Google: ", Font = new Font(SystemFont.Default, 10) },
                    CreateLinkButton("Material Design Icons", "https://github.com/google/material-design-icons"),
                    new Label { Text = " (Apache 2.0 License)", Font = new Font(SystemFont.Default, 10) }
                }
            };

            var githubLinkLabel = CreateLinkButton("Open an issue on GitHub", GitHubUrl);

            var okButton = new Button
            {
                Text = "OK",
                Command = new Command((sender, e) => Close())
            };

            return new Scrollable
            {
                Content = new StackLayout
                {
                    Spacing = 10,
                    Items =
                    {
                        titleLabel,
                        subtitleLabel,
                        null, // spacing
                        versionLabel,
                        copyrightLabel,
                        licenseLabel,
                        null, // spacing
                        new StackLayout
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 5,
                            Items = { supportLabel, supportLinkLabel, communityLabel }
                        },
                        null, // spacing
                        descriptionLabel,
                        null, // spacing
                        warningLabel,
                        null, // spacing
                        openSourceThanksLabel,
                        speckleLink,
                        ghptLink,
                        materialIconsLink,
                        null, // spacing
                        new StackLayout
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 5,
                            Items = 
                            { 
                                new Label { Text = "Need help or found a bug?", Font = new Font(SystemFont.Default, 10) },
                                githubLinkLabel 
                            }
                        },
                        null, // spacing
                        new StackLayoutItem(null, true), // Push everything up
                        aiDisclaimerLabel,
                        null, // spacing
                        new StackLayoutItem(
                            new StackLayout
                            {
                                Orientation = Orientation.Horizontal,
                                Items = { new StackLayoutItem(null, true), okButton }
                            }
                        )
                    }
                }
            };
        }

        private static LinkButton CreateLinkButton(string text, string url)
        {
            var link = new LinkButton
            {
                Text = text,
                Font = new Font(SystemFont.Default, 10),
                TextColor = Colors.Blue
            };

            link.Click += (sender, e) => OpenUrl(url);
            return link;
        }

        private static void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open URL: {ex.Message}", "Error", MessageBoxType.Error);
            }
        }
    }
}
