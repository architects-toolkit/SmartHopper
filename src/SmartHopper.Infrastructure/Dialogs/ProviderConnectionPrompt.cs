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
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Eto.Drawing;
using Eto.Forms;
using Rhino.UI;
using SmartHopper.Infrastructure.Properties;
using SmartHopper.ProviderSdk.AIProviders;
using SmartHopper.ProviderSdk.Hosting;

namespace SmartHopper.Infrastructure.Dialogs
{
    /// <summary>
    /// Interactive <see cref="IProviderConnectionPrompt"/> used by the desktop host:
    /// shows a branded modal when a provider call arrives without usable credentials,
    /// offering Connect (open the provider sign-in flow), Retry (call anyway), and
    /// Cancel (fail the call). Registered once at plugin load; headless surfaces
    /// never see a prompt.
    /// </summary>
    internal sealed class ProviderConnectionPrompt : IProviderConnectionPrompt
    {
        private static readonly Assembly ConfigAssembly = typeof(providersResources).Assembly;
        private const string IconResourceName = "SmartHopper.Infrastructure.Resources.smarthopper.ico";

        /// <inheritdoc/>
        public Task<ProviderConnectionPromptResult> PromptAsync(IAIProvider provider, CancellationToken cancellationToken)
        {
            return PromptOnUiThread(provider?.Name ?? "Provider", cancellationToken);
        }

        /// <inheritdoc/>
        public Task<ProviderConnectionPromptResult> PromptAsync(string displayName, CancellationToken cancellationToken)
        {
            return PromptOnUiThread(displayName ?? "Provider", cancellationToken);
        }

        private static Task<ProviderConnectionPromptResult> PromptOnUiThread(string displayName, CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource<ProviderConnectionPromptResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
            global::Rhino.RhinoApp.InvokeOnUiThread(() =>
            {
                try
                {
                    completion.TrySetResult(ShowDialog(displayName));
                }
                catch (Exception ex)
                {
                    completion.TrySetException(ex);
                }
            });
            return completion.Task;
        }

        private static ProviderConnectionPromptResult ShowDialog(string displayName)
        {
            var result = ProviderConnectionPromptResult.Cancel;
            using var dialog = new Dialog
            {
                Title = $"{displayName} connection required",
                Resizable = false,
                Padding = new Padding(20),
                ClientSize = new Size(480, 210),
                MinimumSize = new Size(480, 210),
            };

            using (var stream = ConfigAssembly.GetManifestResourceStream(IconResourceName))
            {
                if (stream != null)
                {
                    dialog.Icon = new Icon(stream);
                }
            }

            var logoView = new ImageView
            {
                Size = new Size(32, 32),
            };
            using (var logoStream = new MemoryStream(providersResources.smarthopper_256))
            {
                logoView.Image = new Bitmap(logoStream);
            }

            var titleLabel = new Label
            {
                Text = $"{displayName} is not connected",
                Font = new Font(SystemFont.Bold, 14),
                VerticalAlignment = VerticalAlignment.Center,
            };
            var bodyLabel = new Label
            {
                Text = "This action requires a connection to the provider's cloud service. "
                    + "Use Connect to sign in with your account, Retry to run the call anyway, "
                    + "or Cancel to stop the call and fail the component.",
                Wrap = WrapMode.Word,
                TextAlignment = TextAlignment.Left,
                Width = 420,
            };

            var connectButton = new Button { Text = "Connect", MinimumSize = new Size(90, 30) };
            connectButton.Click += (_, _) =>
            {
                result = ProviderConnectionPromptResult.Connect;
                dialog.Close();
            };
            var retryButton = new Button { Text = "Retry", MinimumSize = new Size(90, 30) };
            retryButton.Click += (_, _) =>
            {
                result = ProviderConnectionPromptResult.Retry;
                dialog.Close();
            };
            var cancelButton = new Button { Text = "Cancel", MinimumSize = new Size(90, 30) };
            cancelButton.Click += (_, _) => dialog.Close();

            dialog.DefaultButton = connectButton;
            dialog.AbortButton = cancelButton;

            dialog.Content = new TableLayout
            {
                Spacing = new Size(10, 15),
                Rows =
                {
                    new TableRow(
                        new TableCell(
                            new StackLayout
                            {
                                Orientation = Orientation.Horizontal,
                                Spacing = 12,
                                Items =
                                {
                                    new StackLayoutItem(logoView, VerticalAlignment.Center),
                                    new StackLayoutItem(titleLabel, VerticalAlignment.Center),
                                },
                            })),
                    new TableRow(new TableCell(bodyLabel, true)) { ScaleHeight = true },
                    new TableRow(new TableCell(
                        new StackLayout
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalContentAlignment = HorizontalAlignment.Right,
                            Spacing = 10,
                            Items = { connectButton, retryButton, cancelButton },
                        })),
                },
            };

            dialog.ShowModalInFront(RhinoEtoApp.MainWindow);
            return result;
        }
    }
}
