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
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SmartHopper.Infrastructure.Networking;

namespace SmartHopper.Core.UI.Chat
{
    internal partial class WebChatDialog
    {
        private readonly ConcurrentDictionary<string, byte> pendingExternalImages = new ConcurrentDictionary<string, byte>();

        /// <summary>Resolves an image only after source approval and returns a bounded data URI to the chat.</summary>
        private async Task ResolveExternalImageAsync(string id, string url)
        {
            if (!Guid.TryParse(id, out _) || this.pendingExternalImages.Count >= 8 ||
                !this.pendingExternalImages.TryAdd(id, 0))
            {
                return;
            }

            string? data = null;
            try
            {
                using var client = ExternalDataHttpClient.Create();
                using var response = await client.GetAsync(new Uri(url, UriKind.Absolute)).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var mime = response.Content.Headers.ContentType?.MediaType;
                if (mime != null && mime.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    data = $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
                }
            }
            catch
            {
                // Neither token-bearing URLs nor response contents belong in logs or UI errors.
            }
            finally
            {
                this.pendingExternalImages.TryRemove(id, out _);
            }

            if (!this._dialogClosed)
            {
                this.RunWhenWebViewReady(() => this.ExecuteScript(
                    $"completeExternalImage({JsonConvert.SerializeObject(id)}, {JsonConvert.SerializeObject(data)});"));
            }
        }
    }
}
