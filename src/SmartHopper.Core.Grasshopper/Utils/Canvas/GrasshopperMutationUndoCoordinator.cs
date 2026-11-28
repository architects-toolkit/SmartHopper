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
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Rhino;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.Diagnostics;

namespace SmartHopper.Core.Grasshopper.Utils.Canvas
{
    /// <summary>
    /// Verifies that mutating tools create Grasshopper undo records and coalesces multi-record calls.
    /// Commits are serialized per bound document identity — mutations on different documents may
    /// proceed concurrently, while two mutations targeting the same document never overlap.
    /// </summary>
    public sealed class GrasshopperMutationUndoCoordinator : IMutationUndoCoordinator
    {
        private static readonly ConcurrentDictionary<GH_Document, SemaphoreSlim> _documentGates = new ConcurrentDictionary<GH_Document, SemaphoreSlim>();
        private static readonly SemaphoreSlim _noDocumentGate = new SemaphoreSlim(1, 1);

        /// <inheritdoc/>
        public async Task<IMutationUndoScope> BeginAsync(string toolName, CancellationToken cancellationToken)
        {
            // Resolve the bound document on the UI thread before taking its gate, so the
            // serialization key is the authoritative document identity rather than an ambient read.
            var capture = new TaskCompletionSource<GH_Document?>(TaskCreationOptions.RunContinuationsAsynchronously);
            RhinoApp.InvokeOnUiThread(() => capture.TrySetResult(CanvasAccess.GetCurrentCanvas()));
            using var captureRegistration = cancellationToken.Register(() => capture.TrySetCanceled(cancellationToken));
            var document = await capture.Task.ConfigureAwait(false);

            var gate = document == null
                ? _noDocumentGate
                : _documentGates.GetOrAdd(document, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var completion = new TaskCompletionSource<IMutationUndoScope>(TaskCreationOptions.RunContinuationsAsynchronously);
                RhinoApp.InvokeOnUiThread(() =>
                {
                    var active = CanvasAccess.GetCurrentCanvas();
                    completion.TrySetResult(new Scope(toolName, gate, document, active, document?.UndoServer.UndoCount ?? 0));
                });
                using var registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
                return await completion.Task.ConfigureAwait(false);
            }
            catch
            {
                gate.Release();
                throw;
            }
        }

        private sealed class Scope : IMutationUndoScope
        {
            private readonly GH_Document? document;
            private readonly GH_Document? activeDocument;
            private readonly SemaphoreSlim gate;
            private readonly int initialUndoCount;
            private readonly string toolName;
            private int completed;

            public Scope(string toolName, SemaphoreSlim gate, GH_Document? document, GH_Document? activeDocument, int initialUndoCount)
            {
                this.toolName = toolName;
                this.gate = gate;
                this.document = document;
                this.activeDocument = activeDocument;
                this.initialUndoCount = initialUndoCount;
            }

            public async Task CompleteAsync(AIReturn result, CancellationToken cancellationToken)
            {
                if (Interlocked.Exchange(ref this.completed, 1) != 0)
                {
                    return;
                }

                try
                {
                    if (this.document == null)
                    {
                        return;
                    }

                    var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    RhinoApp.InvokeOnUiThread(() =>
                    {
                        try
                        {
                            if (!ReferenceEquals(this.activeDocument, this.document))
                            {
                                result.AddRuntimeMessage(
                                    SHRuntimeMessageSeverity.Warning,
                                    SHRuntimeMessageOrigin.Tool,
                                    $"Active document changed while '{this.toolName}' acquired its commit gate; undo was verified against the originally bound document.");
                            }

                            var addedRecords = Math.Max(0, this.document.UndoServer.UndoCount - this.initialUndoCount);
                            if (addedRecords > 1)
                            {
                                this.document.UndoUtil.MergeRecords(addedRecords);
                            }
                            else if (addedRecords == 0 && result.Success)
                            {
                                result.AddRuntimeMessage(
                                    SHRuntimeMessageSeverity.Warning,
                                    SHRuntimeMessageOrigin.Tool,
                                    $"Mutating tool '{this.toolName}' produced no Grasshopper undo record; the operation may have been a no-op.");
                            }

                            completion.TrySetResult(true);
                        }
                        catch (Exception ex)
                        {
                            result.AddRuntimeMessage(
                                SHRuntimeMessageSeverity.Warning,
                                SHRuntimeMessageOrigin.Tool,
                                $"Undo verification failed for '{this.toolName}': {ex.Message}");
                            completion.TrySetResult(false);
                        }
                    });
                    using var registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
                    await completion.Task.ConfigureAwait(false);
                }
                finally
                {
                    this.gate.Release();
                }
            }
        }
    }
}
