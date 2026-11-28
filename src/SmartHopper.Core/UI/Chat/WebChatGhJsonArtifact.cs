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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SmartHopper.Infrastructure.Interaction;

namespace SmartHopper.Core.UI.Chat
{
    internal partial class WebChatDialog
    {
        /// <summary>
        /// GhJSON artifacts shown in this dialog, keyed by artifact id, so a later
        /// place-on-canvas click (sh://event?type=ghjson_place) can resolve the
        /// stored document without carrying it through the URL.
        /// </summary>
        private readonly ConcurrentDictionary<string, GhJsonArtifact> _ghJsonArtifacts =
            new ConcurrentDictionary<string, GhJsonArtifact>();

        /// <summary>
        /// Queues a GhJSON artifact card for rendering in the WebView and stores the
        /// artifact so the place-on-canvas action can resolve it by id later.
        /// </summary>
        /// <param name="artifact">The artifact to render.</param>
        /// <param name="cancellationToken">Cancellation for the presentation request.</param>
        private Task ShowGhJsonArtifactAsync(GhJsonArtifact artifact, CancellationToken cancellationToken)
        {
            this._ghJsonArtifacts[artifact.Id] = artifact;

            var payload = new
            {
                id = artifact.Id,
                title = artifact.Title,
                ghjson = artifact.GhJson,
                valid = artifact.Valid,
                components = artifact.Components,
                connections = artifact.Connections,
                errors = artifact.Errors?.Select(e => new { path = e.Path, message = e.Message }).ToArray()
                    ?? Array.Empty<object>(),
            };

            this.RunWhenWebViewReady(() =>
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    this.ExecuteScript($"showGhJsonArtifact({JsonConvert.SerializeObject(payload)});");
                }
            });

            return Task.CompletedTask;
        }

        /// <summary>
        /// Places a stored GhJSON artifact on the active Grasshopper canvas through
        /// <see cref="GhJsonCanvasBridge"/>, then resolves the card with the outcome.
        /// Only valid artifacts may be placed; invalid ones carry no action.
        /// </summary>
        /// <param name="artifactId">The artifact identifier carried by the card.</param>
        private void PlaceGhJsonArtifact(string artifactId)
        {
            if (string.IsNullOrWhiteSpace(artifactId) ||
                !this._ghJsonArtifacts.TryGetValue(artifactId, out var artifact) ||
                artifact == null ||
                !artifact.Valid)
            {
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    var handler = GhJsonCanvasBridge.PlaceHandler;
                    if (handler == null)
                    {
                        this.ExecuteScript($"resolveGhJsonArtifact({JsonConvert.SerializeObject(artifactId)}, false, 'No Grasshopper placer is active');");
                        return;
                    }

                    var outcome = await handler(artifact.GhJson, CancellationToken.None).ConfigureAwait(false);
                    if (outcome != null && outcome.Success)
                    {
                        var label = outcome.PlacedCount > 0 ? $"Placed {outcome.PlacedCount} object(s)" : "Placed";
                        this.ExecuteScript($"resolveGhJsonArtifact({JsonConvert.SerializeObject(artifactId)}, true, {JsonConvert.SerializeObject(label)});");
                    }
                    else
                    {
                        var error = outcome?.Error ?? "Placement failed";
                        this.ExecuteScript($"resolveGhJsonArtifact({JsonConvert.SerializeObject(artifactId)}, false, {JsonConvert.SerializeObject(error)});");
                    }
                }
                catch (Exception ex)
                {
                    DebugLog($"[WebChatDialog] GhJSON placement failed: {ex.Message}");
                    this.ExecuteScript($"resolveGhJsonArtifact({JsonConvert.SerializeObject(artifactId)}, false, {JsonConvert.SerializeObject(ex.Message)});");
                }
            });
        }

        private sealed class WebChatGhJsonArtifactPresenter : IGhJsonArtifactPresenter
        {
            private readonly WebChatDialog dialog;

            public WebChatGhJsonArtifactPresenter(WebChatDialog dialog)
            {
                this.dialog = dialog;
            }

            public Task ShowAsync(GhJsonArtifact artifact, CancellationToken cancellationToken)
            {
                return this.dialog.ShowGhJsonArtifactAsync(artifact, cancellationToken);
            }
        }
    }
}
