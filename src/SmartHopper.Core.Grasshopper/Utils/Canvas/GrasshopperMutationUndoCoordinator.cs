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
using Grasshopper.Kernel;
using Rhino;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.Diagnostics;

namespace SmartHopper.Core.Grasshopper.Utils.Canvas
{
    /// <summary>
    /// Verifies that mutating tools create Grasshopper undo records and coalesces multi-record calls.
    /// </summary>
    public sealed class GrasshopperMutationUndoCoordinator : IMutationUndoCoordinator
    {
        private static readonly SemaphoreSlim _mutationLock = new SemaphoreSlim(1, 1);

        /// <inheritdoc/>
        public async Task<IMutationUndoScope> BeginAsync(string toolName, CancellationToken cancellationToken)
        {
            await _mutationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var completion = new TaskCompletionSource<IMutationUndoScope>(TaskCreationOptions.RunContinuationsAsynchronously);
                RhinoApp.InvokeOnUiThread(() =>
                {
                    var document = CanvasAccess.GetCurrentCanvas();
                    completion.TrySetResult(new Scope(toolName, document, document?.UndoServer.UndoCount ?? 0));
                });
                using var registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
                return await completion.Task.ConfigureAwait(false);
            }
            catch
            {
                _mutationLock.Release();
                throw;
            }
        }

        private sealed class Scope : IMutationUndoScope
        {
            private readonly GH_Document? document;
            private readonly int initialUndoCount;
            private readonly string toolName;
            private int completed;

            public Scope(string toolName, GH_Document? document, int initialUndoCount)
            {
                this.toolName = toolName;
                this.document = document;
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
                    _mutationLock.Release();
                }
            }
        }
    }
}
