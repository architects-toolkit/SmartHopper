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
using Newtonsoft.Json;
using SmartHopper.Infrastructure.Interaction;

namespace SmartHopper.Core.UI.Chat
{
    internal partial class WebChatDialog
    {
        /// <summary>
        /// Queues a canvas pointer card for rendering in the WebView.
        /// The card carries the pointer id so the replay button can re-trigger the
        /// pan/zoom/highlight through <see cref="CanvasPointerBridge"/>.
        /// </summary>
        /// <param name="request">The pointer request to render.</param>
        /// <param name="cancellationToken">Cancellation for the presentation request.</param>
        private Task ShowCanvasPointerAsync(CanvasPointerRequest request, CancellationToken cancellationToken)
        {
            var payload = new
            {
                id = request.Id,
                message = request.Message,
            };

            this.RunWhenWebViewReady(() =>
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    this.ExecuteScript($"showCanvasPointer({JsonConvert.SerializeObject(payload)});");
                }
            });

            return Task.CompletedTask;
        }

        /// <summary>
        /// Replays a stored canvas pointer request: re-frames the viewport and re-arms
        /// the highlight. Resolved through <see cref="CanvasPointerBridge"/> so this dialog
        /// never touches Grasshopper APIs directly.
        /// </summary>
        /// <param name="pointerId">The pointer identifier carried by the card.</param>
        private void ReplayCanvasPointer(string pointerId)
        {
            if (string.IsNullOrWhiteSpace(pointerId))
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    var handler = CanvasPointerBridge.ReplayHandler;
                    var replayed = handler != null &&
                        await handler(pointerId, CancellationToken.None).ConfigureAwait(false);
                    if (!replayed)
                    {
                        this.ExecuteScript("showToast('Target no longer available');");
                    }
                }
                catch (Exception ex)
                {
                    DebugLog($"[WebChatDialog] Canvas pointer replay failed: {ex.Message}");
                    this.ExecuteScript("showToast('Could not show the target on canvas');");
                }
            });
        }

        private sealed class WebChatCanvasPointerPresenter : ICanvasPointerPresenter
        {
            private readonly WebChatDialog dialog;

            public WebChatCanvasPointerPresenter(WebChatDialog dialog)
            {
                this.dialog = dialog;
            }

            public Task ShowAsync(CanvasPointerRequest request, CancellationToken cancellationToken)
            {
                return this.dialog.ShowCanvasPointerAsync(request, cancellationToken);
            }
        }
    }
}
