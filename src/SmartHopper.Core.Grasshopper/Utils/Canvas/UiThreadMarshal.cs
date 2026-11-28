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
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using GhJSON.Core.Hosting;
using GhJSON.Grasshopper;

namespace SmartHopper.Core.Grasshopper.Utils.Canvas
{
    /// <summary>
    /// Marshals canvas mutations onto the Rhino UI thread. Centralizes the
    /// blocking-invoke contract — including its late-execution guard — so each
    /// manipulation helper does not grow its own copy.
    /// </summary>
    internal static class UiThreadMarshal
    {
        /// <summary>Maximum time a posted call may sit unclaimed in the UI queue.</summary>
        private static readonly TimeSpan QueuedStartBudget = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Queues <paramref name="func"/> on the Rhino UI thread through the shared
        /// bounded-post contract and awaits the real outcome. Cancellation and the
        /// queued-start budget bound only the time the call may sit unclaimed: an
        /// expired or cancelled callback never runs; a started callback owns its
        /// outcome and the caller waits for it. The function's own exception is
        /// re-thrown unchanged.
        /// </summary>
        /// <typeparam name="T">The result type.</typeparam>
        /// <param name="func">The work to run on the UI thread.</param>
        /// <param name="cancellationToken">Cancels the post while still queued.</param>
        /// <returns>The function result.</returns>
        internal static async Task<T> RunOnUiThreadAsync<T>(Func<T> func, CancellationToken cancellationToken)
        {
            var post = await GhJsonGrasshopper
                .PostToUiThreadAsync(func, QueuedStartBudget, cancellationToken)
                .ConfigureAwait(false);

            switch (post.State)
            {
                case UiPostState.Completed:
                    return post.Value!;
                case UiPostState.Faulted:
                    ExceptionDispatchInfo.Capture(post.Error!).Throw();
                    throw post.Error!;
                case UiPostState.CancelledNotStarted:
                    throw new OperationCanceledException(cancellationToken);
                case UiPostState.BusyNotStarted:
                    throw new TimeoutException(
                        $"Grasshopper UI thread did not admit the call within {QueuedStartBudget.TotalSeconds:n0} s.");
                case UiPostState.NotPosted:
                    throw new InvalidOperationException(
                        "The Grasshopper UI thread is unavailable; the call was not queued.", post.Error);
                default:
                    throw new InvalidOperationException(
                        "Operation on the Grasshopper UI thread failed.", post.Error);
            }
        }

        /// <summary>
        /// Queues <paramref name="action"/> on the Rhino UI thread and blocks until it
        /// returns (or 30 seconds elapse). If we are already on the UI thread the action runs inline.
        /// </summary>
        internal static void RunAndWait(Action action)
        {
            if (global::Rhino.RhinoApp.InvokeRequired == false)
            {
                action();
                return;
            }

            Exception? captured = null;
            using var done = new ManualResetEventSlim(false);

            // Ownership flag: 0 = unclaimed. The queued callback claims it to start
            // running; the waiter claims it when the timeout elapses, so a late UI
            // pass can never apply the mutation after the caller already reported
            // a failure.
            int started = 0;

            global::Rhino.RhinoApp.InvokeOnUiThread(new Action(() =>
            {
                if (Interlocked.CompareExchange(ref started, 1, 0) != 0)
                {
                    // The caller already timed out — never run the mutation after a
                    // reported failure.
                    return;
                }

                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    captured = ex;
                }
                finally
                {
                    done.Set();
                }
            }));

            if (!done.Wait(TimeSpan.FromSeconds(30)))
            {
                if (Interlocked.CompareExchange(ref started, 1, 0) == 0)
                {
                    // We claimed the action before it started — the queued callback is
                    // now a no-op, so it is safe to report a timeout.
                    throw new TimeoutException(
                        "Grasshopper UI thread did not process the call within 30 s.");
                }

                // The action already started on the UI thread — it owns completion now.
                // Wait for it so the caller does not proceed while a mutation is still
                // being applied, then report its real outcome.
                done.Wait();
            }

            if (captured != null)
            {
                throw new InvalidOperationException(
                    "Operation on the Grasshopper UI thread failed.", captured);
            }
        }
    }
}
