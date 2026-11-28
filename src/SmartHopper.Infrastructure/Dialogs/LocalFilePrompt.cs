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
using SmartHopper.Infrastructure.FileSystem;

namespace SmartHopper.Infrastructure.Dialogs
{
    /// <summary>Shows native file-consent controls; model text cannot supply or preselect the decision.</summary>
    internal static class LocalFilePrompt
    {
        /// <summary>Maximum time the consent prompt waits for a human answer before refusing.</summary>
        private static readonly TimeSpan PromptTimeout = TimeSpan.FromSeconds(120);

        /// <summary>Asks the desktop user before a tool touches a local path; dismissal/cancellation denies.</summary>
        internal static async Task<LocalFileAccessDecision> ShowAsync(LocalFileAccessRequest request, CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource<LocalFileAccessDecision>(TaskCreationOptions.RunContinuationsAsynchronously);
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
                completion.TrySetResult(LocalFileAccessDecision.NoResponse);
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
                        Title = "Allow file access?",
                        Padding = 20,
                        Resizable = true,
                        MinimumSize = new Size(560, 280),
                    };
                    active = dialog;
                    var decision = LocalFileAccessDecision.Deny;
                    var once = new Button { Text = "Allow once" };
                    var alwaysFile = new Button { Text = "Always allow this file" };
                    var alwaysFolder = new Button { Text = "Always allow this folder" };
                    var deny = new Button { Text = "Deny" };
                    once.Click += (_, _) => { decision = LocalFileAccessDecision.AllowOnce; dialog.Close(); };
                    alwaysFile.Click += (_, _) => { decision = LocalFileAccessDecision.AlwaysAllow; dialog.Close(); };
                    alwaysFolder.Click += (_, _) => { decision = LocalFileAccessDecision.AlwaysAllowFolder; dialog.Close(); };
                    deny.Click += (_, _) => dialog.Close();

                    var layout = new DynamicLayout { Spacing = new Size(10, 12) };
                    layout.Add(new Label
                    {
                        Text = $"SmartHopper wants to {DescribeVerb(request.Capability)} this file on your computer:",
                        Font = new Font(SystemFont.Bold, 12),
                    });
                    layout.Add(new Label { Text = request.CanonicalPath, Wrap = WrapMode.Word });

                    var detail = "Always allow applies to this exact file, or to the whole folder when you pick the folder option. " +
                        "You can revoke saved permissions in Settings > Security.";
                    if (request.WillOverwrite)
                    {
                        detail = "The file already exists and would be overwritten. " + detail;
                    }

                    layout.Add(new Label { Text = detail, Wrap = WrapMode.Word });

                    if (request.Capability == LocalFileCapability.Execute)
                    {
                        layout.Add(new Label
                        {
                            Text = "Opening this file may run embedded scripts in Grasshopper.",
                            TextColor = Colors.DarkRed,
                        });
                    }
                    else if (request.Capability == LocalFileCapability.Write)
                    {
                        layout.Add(new Label
                        {
                            Text = "The assistant can replace or create files at this location.",
                            TextColor = Colors.DarkRed,
                        });
                    }

                    layout.Add(new Panel(), yscale: true);
                    layout.Add(new StackLayout
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 10,
                        Items = { once, alwaysFile, alwaysFolder, deny },
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

        private static string DescribeVerb(LocalFileCapability capability) => capability switch
        {
            LocalFileCapability.Read => "read",
            LocalFileCapability.Write => "write to",
            LocalFileCapability.Execute => "open and run",
            _ => "access",
        };
    }
}
