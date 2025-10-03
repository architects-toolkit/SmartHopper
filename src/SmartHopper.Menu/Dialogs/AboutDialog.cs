/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Eto.Drawing;
using Eto.Forms;
using SmartHopper.Infrastructure.Dialogs;
using SmartHopper.Infrastructure.Properties;

namespace SmartHopper.Menu.Dialogs
{
    /// <summary>
    /// Dialog to display information about SmartHopper, including version, copyright, and support information
    /// </summary>
    internal sealed class AboutDialog : Dialog
    {
        private const string GitHubUrl = "https://github.com/architects-toolkit/SmartHopper";
        private const string RktkUrl = "https://rktk.tools";
        private static readonly Assembly ConfigAssembly = typeof(providersResources).Assembly;
        private const string IconResourceName = "SmartHopper.Infrastructure.Resources.smarthopper.ico";

        /// <summary>
        /// Initializes a new instance of the AboutDialog
        /// </summary>
        /// <param name="version">The version of SmartHopper to display</param>
        public AboutDialog(string version)
        {
            this.Title = "About SmartHopper";

            // Set window icon from embedded resource
            using (var stream = ConfigAssembly.GetManifestResourceStream(IconResourceName))
            {
                if (stream != null)
                {
                    this.Icon = new Icon(stream);
                }
            }

            this.Resizable = true;
            this.Size = new Size(800, 550);
            this.MinimumSize = new Size(600, 500);
            this.Padding = new Padding(20);

            var mainLayout = new TableLayout
            {
                Spacing = new Size(20, 0),
                Rows = { new TableRow { Cells = { CreateLogoPanel(), this.CreateContentPanel(version) } } },
            };

            this.Content = mainLayout;

            // Center the dialog on screen
            this.Location = new Point(
                (int)((Screen.PrimaryScreen.Bounds.Width - this.Size.Width) / 2),
                (int)((Screen.PrimaryScreen.Bounds.Height - this.Size.Height) / 2));
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
                Items = { new StackLayoutItem(imageView, false) },
            };
        }

        private Control CreateContentPanel(string version)
        {
            var titleLabel = new Label
            {
                Text = "SmartHopper",
                Font = new Font(SystemFont.Bold, 22),
                TextColor = SystemColors.ControlText,
            };

            var subtitleLabel = new Label
            {
                Text = "An AI-powered assistant for Grasshopper3D",
                Font = new Font(SystemFont.Default, 12),
                TextColor = SystemColors.ControlText,
                Wrap = WrapMode.Word,
            };

            var versionLabel = new Label
            {
                Text = $"Version {version}",
                Font = new Font(SystemFont.Default, 10),
                TextColor = SystemColors.DisabledText,
                Wrap = WrapMode.Word,
            };

            var copyrightLabel = new Label
            {
                Text = "Copyright (c) 2024-2025 Marc Roca Musach",
                Font = new Font(SystemFont.Default, 10),
                Wrap = WrapMode.Word,
            };

            var licenseLabel = new Label
            {
                Text = "Licensed under GNU Lesser General Public License v3.0",
                Font = new Font(SystemFont.Default, 10),
                Wrap = WrapMode.Word,
            };

            var supportLabel = new Label
            {
                Text = "Supported by:",
                Font = new Font(SystemFont.Default, 10),
                Wrap = WrapMode.Word,
            };

            var supportLinkLabel = CreateLinkButton("Architect's Toolkit (RKTK.tools)", RktkUrl);

            var communityLabel = new Label
            {
                Text = "and the SmartHopper Community",
                Font = new Font(SystemFont.Default, 10),
                Wrap = WrapMode.Word,
            };

            var descriptionLabel = new Label
            {
                Text = "SmartHopper is an open-source project that implements third-party AI APIs to provide advanced features for Grasshopper.\n\nIt currently supports MistralAI, OpenAI, DeepSeek, Anthropic and OpenRouter (in order of implementation).",
                Font = new Font(SystemFont.Default, 10),
                Wrap = WrapMode.Word,
            };

            var warningLabel = new Label
            {
                Text = "KEEP IN MIND THAT SMARTHOPPER IS STILL IN ITS EARLY STAGES OF DEVELOPMENT AND MAY HAVE BUGS OR LIMITATIONS. FILES SAVED WITH THIS VERSION MAY NOT BE COMPATIBLE WITH FUTURE VERSIONS.",
                Font = new Font(SystemFont.Default, 10),
                Wrap = WrapMode.Word,
                TextColor = Colors.DarkRed,
            };

            var aiDisclaimerLabel = new Label
            {
                Text = "AI Disclaimer: AI-generated content may contain errors. Users are responsible for reviewing, validating, and verifying all AI-generated outputs. " +
                       "SmartHopper serves as an interface between Grasshopper and user-provided 3rd-party AI services. " +
                       "The AI APIs are configured and provided by the user, and SmartHopper does not control or guarantee the accuracy of these services. " +
                       "SmartHopper and any of its authors, contributors, or affiliates are not liable for any errors or omissions in the content generated by these AI services.",
                Font = new Font(SystemFont.Default, 10),
                Wrap = WrapMode.Word,
                TextColor = Colors.DarkGray,
            };

            var openSourceThanksLabel = new Label
            {
                Text = "Special thanks to all the Open Source Projects that inspired or were used as a base for this project:",
                Font = new Font(SystemFont.Default, 10),
                Wrap = WrapMode.Word,
            };

            var speckleLink = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5,
                Items =
                {
                    new Label { Text = "- Speckle Systems for", Font = new Font(SystemFont.Default, 10) },
                    CreateLinkButton("GrasshopperAsyncComponent", "https://github.com/specklesystems/GrasshopperAsyncComponent"),
                    new Label { Text = "(Apache 2.0 License)", Font = new Font(SystemFont.Default, 10) }
                },
            };

            var ghptLink = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5,
                Items =
                {
                    new Label { Text = "- enmerk4r for", Font = new Font(SystemFont.Default, 10) },
                    CreateLinkButton("GHPT", "https://github.com/enmerk4r/GHPT"),
                    new Label { Text = "(MIT License)", Font = new Font(SystemFont.Default, 10) }
                },
            };

            var materialIconsLink = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5,
                Items =
                {
                    new Label { Text = "- Google for", Font = new Font(SystemFont.Default, 10) },
                    CreateLinkButton("Material Design Icons", "https://github.com/google/material-design-icons"),
                    new Label { Text = "(Apache 2.0 License)", Font = new Font(SystemFont.Default, 10) }
                },
            };

            var lobeIconsLink = new StackLayout
            {
                Orientation = Orientation.Horizontal,
                Spacing = 5,
                Items =
                {
                    new Label { Text = "- LobeHub for", Font = new Font(SystemFont.Default, 10) },
                    CreateLinkButton("Lobe Icons", "https://github.com/lobehub/lobe-icons"),
                    new Label { Text = "(MIT License)", Font = new Font(SystemFont.Default, 10) }
                },
            };

            var logoDesignThanksLabel = new Label
            {
                Text = "Acknowledgement to Jordina Roca Musach for the SmartHopper logo design.",
                Font = new Font(SystemFont.Default, 10),
                Wrap = WrapMode.Word,
            };

            var githubLinkLabel = CreateLinkButton("Open an issue on GitHub", GitHubUrl);

            var okButton = new Button
            {
                Text = "OK",
                Command = new Command((sender, e) => this.Close()),
            };

            // Create a layout for the scrollable content and OK button
            var mainContentLayout = new Scrollable
            {
                Border = BorderType.None,
                ExpandContentHeight = false,
                ExpandContentWidth = true,
                Content = new StackLayout
                {
                    Spacing = 10,
                    Width = 200,
                    Height = -1,
                    Items =
                    {
                        titleLabel,
                        subtitleLabel,
                        null, // spacing
                        descriptionLabel,
                        null, // spacing
                        versionLabel,
                        copyrightLabel,
                        licenseLabel,
                        null, // spacing
                        new StackLayout
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 5,
                            Items = { supportLabel, supportLinkLabel, communityLabel },
                        },
                        null, // spacing
                        warningLabel,
                        null,
                        new StackLayout
                        {
                            Orientation = Orientation.Horizontal,
                            Spacing = 5,
                            Items =
                            {
                                new Label { Text = "Need help or found a bug?", Font = new Font(SystemFont.Default, 10) },
                                githubLinkLabel
                            },
                        },
                        null, // spacing
                        openSourceThanksLabel,
                        speckleLink,
                        ghptLink,
                        materialIconsLink,
                        lobeIconsLink,
                        null, // spacing
                        logoDesignThanksLabel,
                        null, // spacing
                        null, // spacing
                        new StackLayoutItem(null, true), // Push everything up
                        aiDisclaimerLabel,
                        null, // spacing
                        okButton,
                    },
                },
            };

            return mainContentLayout;
        }

        private static LinkButton CreateLinkButton(string text, string url)
        {
            var link = new LinkButton
            {
                Text = text,
                Font = new Font(SystemFont.Default, 10),
                TextColor = Colors.Blue,
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
                    UseShellExecute = true,
                });
            }
            catch (Exception ex)
            {
                StyledMessageDialog.ShowError($"Failed to open URL: {ex.Message}", "Error");
            }
        }
    }
}
