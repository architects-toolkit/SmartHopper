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
using System.Threading;
using System.Threading.Tasks;
using Eto.Drawing;
using Eto.Forms;
using Rhino;
using Rhino.UI;
using SmartHopper.Infrastructure.Consent;
using SmartHopper.Infrastructure.Dialogs;

namespace SmartHopper.Core.Grasshopper.Utils.Canvas
{
    /// <summary>
    /// Asks the user before a tool opens a Grasshopper file from disk. Activating a
    /// document starts its solution, so script components inside the file run code
    /// on the user's machine — opening is gated on an explicit consent decision.
    /// </summary>
    internal static class OpenDocumentConsent
    {
        /// <summary>
        /// Requests consent to open <paramref name="fullPath"/>.
        /// </summary>
        /// <param name="fullPath">Normalized absolute path to the .gh/.ghx file.</param>
        /// <param name="context">The invocation context of the calling tool.</param>
        /// <param name="cancellationToken">Cancellation for the pending request.</param>
        /// <returns>The user's consent decision.</returns>
        public static async Task<ConsentDecision> RequestAsync(
            string fullPath,
            MutationInvocationContext? context,
            CancellationToken cancellationToken = default)
        {
            ConsentGate.RegisterPresenter(OpenDocumentConsentPresenter.Instance);
            return await ConsentGate.RequestAsync(
                new OpenDocumentConsentProposal(fullPath),
                context,
                cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// A consent proposal to open one Grasshopper document from disk.
    /// </summary>
    internal sealed class OpenDocumentConsentProposal : IConsentProposal
    {
        private const string OpenItemKey = "open";

        /// <summary>Initializes a new open-document proposal.</summary>
        /// <param name="filePath">The full path of the file to open.</param>
        public OpenDocumentConsentProposal(string filePath)
        {
            this.Id = Guid.NewGuid().ToString("N");
            this.FilePath = filePath;
            this.ItemKeys = new[] { OpenItemKey };
        }

        /// <inheritdoc/>
        public string Id { get; }

        /// <inheritdoc/>
        public string Kind => "document-open";

        /// <inheritdoc/>
        public string Title => "Open Grasshopper document";

        /// <inheritdoc/>
        public IReadOnlyList<string> ItemKeys { get; }

        /// <summary>Gets the full path of the file to open.</summary>
        public string FilePath { get; }
    }

    /// <summary>
    /// Presents open-document consent through a modal desktop dialog. Fails closed:
    /// cancellation, timeout and presentation errors never grant approval.
    /// </summary>
    internal sealed class OpenDocumentConsentPresenter : IConsentPresenter
    {
        /// <summary>Maximum time the prompt waits for an answer before refusing.</summary>
        private static readonly TimeSpan PromptTimeout = TimeSpan.FromSeconds(120);

        private OpenDocumentConsentPresenter()
        {
        }

        /// <summary>Gets the shared presenter instance.</summary>
        public static OpenDocumentConsentPresenter Instance { get; } = new OpenDocumentConsentPresenter();

        /// <inheritdoc/>
        public bool CanPresent(IConsentProposal proposal, MutationInvocationContext context)
        {
            return proposal is OpenDocumentConsentProposal;
        }

        /// <inheritdoc/>
        public async Task<ConsentDecision> PresentAsync(
            IConsentProposal proposal,
            MutationInvocationContext context,
            CancellationToken cancellationToken)
        {
            var openProposal = (OpenDocumentConsentProposal)proposal;
            var completion = new TaskCompletionSource<ConsentDecision>(TaskCreationOptions.RunContinuationsAsynchronously);
            Dialog? active = null;
            using var promptTimeout = new CancellationTokenSource(PromptTimeout);
            using var registration = cancellationToken.Register(() =>
            {
                completion.TrySetCanceled(cancellationToken);
                RhinoApp.InvokeOnUiThread(() => active?.Close());
            });
            using var timeoutRegistration = promptTimeout.Token.Register(() =>
            {
                // An unanswered prompt is a refusal, not a grant.
                completion.TrySetResult(new ConsentDecision
                {
                    Status = ConsentDecisionStatus.Rejected,
                    Reason = "The open request timed out waiting for an answer.",
                });
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
                        Title = "Open Grasshopper document?",
                        Padding = 20,
                        Resizable = true,
                        MinimumSize = new Size(520, 240),
                    };
                    active = dialog;
                    var approved = false;
                    var open = new Button { Text = "Open" };
                    var deny = new Button { Text = "Don't open" };
                    open.Click += (_, _) =>
                    {
                        approved = true;
                        dialog.Close();
                    };
                    deny.Click += (_, _) => dialog.Close();
                    var layout = new DynamicLayout { Spacing = new Size(10, 12) };
                    layout.Add(new Label
                    {
                        Text = "SmartHopper wants to open this Grasshopper file:",
                        Font = new Font(SystemFont.Bold, 12),
                    });
                    layout.Add(new Label { Text = openProposal.FilePath, Wrap = WrapMode.Word });
                    layout.Add(new Label
                    {
                        Text = "Opening it makes the file the active document and starts its solution — " +
                            "script components inside (C#, Python, VB) run code on this machine. " +
                            "Only open files you trust.",
                        Wrap = WrapMode.Word,
                        TextColor = Colors.DarkRed,
                    });
                    layout.Add(new Panel(), yscale: true);
                    layout.Add(new StackLayout
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 10,
                        Items = { open, deny },
                    });
                    dialog.Content = layout;
                    dialog.DefaultButton = deny;
                    dialog.AbortButton = deny;
                    dialog.ShowModalInFront(RhinoEtoApp.MainWindow);
                    completion.TrySetResult(approved
                        ? new ConsentDecision
                        {
                            Status = ConsentDecisionStatus.Approved,
                            AcceptedItemKeys = openProposal.ItemKeys,
                        }
                        : new ConsentDecision
                        {
                            Status = ConsentDecisionStatus.Rejected,
                            Reason = "The user declined to open the file.",
                        });
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
