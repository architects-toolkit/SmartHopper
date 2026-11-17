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
using System.Threading;
using System.Threading.Tasks;
using Eto.Drawing;
using Eto.Forms;
using Rhino;
using Rhino.UI;
using SmartHopper.Infrastructure.Networking;

namespace SmartHopper.Infrastructure.Dialogs
{
    /// <summary>Shows native source-consent controls; model text cannot supply or preselect the decision.</summary>
    internal static class ExternalSourcePrompt
    {
        /// <summary>Maximum time the consent prompt waits for a human answer before refusing.</summary>
        private static readonly TimeSpan PromptTimeout = TimeSpan.FromSeconds(120);

        /// <summary>Asks the desktop user before a new source is contacted; dismissal/cancellation denies.</summary>
        internal static async Task<ExternalSourceDecision> ShowAsync(string origin, CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource<ExternalSourceDecision>(TaskCreationOptions.RunContinuationsAsynchronously);
            Dialog? active = null;
            using var promptTimeout = new CancellationTokenSource(PromptTimeout);
            using var registration = cancellationToken.Register(() =>
            {
                completion.TrySetCanceled(cancellationToken);
                RhinoApp.InvokeOnUiThread(() => active?.Close());
            });
            using var timeoutRegistration = promptTimeout.Token.Register(() =>
            {
                // An unanswered prompt is a refusal, not a grant — and must not surface
                // as a caller-side timeout while the dialog is still waiting.
                completion.TrySetResult(ExternalSourceDecision.NoResponse);
                RhinoApp.InvokeOnUiThread(() => active?.Close());
            });
            RhinoApp.InvokeOnUiThread(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                try
                {
                    using var dialog = new Dialog
                    {
                        Title = "Allow external data retrieval?",
                        Padding = 20,
                        Resizable = true,
                        MinimumSize = new Size(520, 260),
                    };
                    active = dialog;
                    var decision = ExternalSourceDecision.Deny;
                    var once = new Button { Text = "Allow once" };
                    var always = new Button { Text = "Always allow this origin" };
                    var deny = new Button { Text = "Deny" };
                    once.Click += (_, _) => { decision = ExternalSourceDecision.AllowOnce; dialog.Close(); };
                    always.Click += (_, _) => { decision = ExternalSourceDecision.AlwaysAllow; dialog.Close(); };
                    deny.Click += (_, _) => dialog.Close();
                    var layout = new DynamicLayout { Spacing = new Size(10, 12) };
                    layout.Add(new Label
                    {
                        Text = "SmartHopper is trying to retrieve data from:",
                        Font = new Font(SystemFont.Bold, 12),
                    });
                    layout.Add(new Label { Text = origin, Wrap = WrapMode.Word });
                    layout.Add(new Label
                    {
                        Text = "Do you allow? Always allow applies to this exact scheme, host and port. " +
                            "Private addresses require their own approval. You can revoke saved permissions in Settings > Security.",
                        Wrap = WrapMode.Word,
                    });
                    if (origin.StartsWith("http:", StringComparison.Ordinal))
                    {
                        layout.Add(new Label { Text = "This source uses unencrypted HTTP.", TextColor = Colors.DarkRed });
                    }

                    layout.Add(new Panel(), yscale: true);
                    layout.Add(new StackLayout
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 10,
                        Items = { once, always, deny },
                    });
                    dialog.Content = layout;
                    dialog.DefaultButton = deny;
                    dialog.AbortButton = deny;
                    dialog.ShowModalInFront(RhinoEtoApp.MainWindow);
                    completion.TrySetResult(decision);
                }
                catch (Exception ex)
                {
                    completion.TrySetException(ex);
                }
                finally
                {
                    active = null;
                }
            });
            return await completion.Task.ConfigureAwait(false);
        }
    }
}
