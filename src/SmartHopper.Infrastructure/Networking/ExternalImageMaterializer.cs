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
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Requests;

namespace SmartHopper.Infrastructure.Networking
{
    /// <summary>Prevents providers from fetching image URLs outside the host's source-consent boundary.</summary>
    public static class ExternalImageMaterializer
    {
        private const int MaximumImageBytes = 16 * 1024 * 1024;

        /// <summary>
        /// Resolves one approved image to bounded inline bytes, checking its actual format.
        /// Source references survive materialization; unsupported or denied resources fail explicitly.
        /// </summary>
        /// <param name="image">The image reference to materialize.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <param name="clientFactory">Optional transport seam for deterministic tests.</param>
        /// <returns>The inline image with its original provenance.</returns>
        public static Task<AIInteractionImage> MaterializeAsync(
            AIInteractionImage image,
            CancellationToken ct,
            Func<HttpClient>? clientFactory = null)
        {
            return MaterializeCoreAsync(image, ct, clientFactory, null);
        }

        private static async Task<AIInteractionImage> MaterializeCoreAsync(
            AIInteractionImage image,
            CancellationToken ct,
            Func<HttpClient>? clientFactory,
            HttpClient? sharedClient)
        {
            ArgumentNullException.ThrowIfNull(image);
            ct.ThrowIfCancellationRequested();
            if (!string.IsNullOrEmpty(image.ImageData))
            {
                return image with { ImageUrl = null };
            }

            var uri = image.ImageUrl ?? throw new InvalidDataException("An image source is required.");
            byte[] bytes;
            if (uri.Scheme.Equals("data", StringComparison.OrdinalIgnoreCase))
            {
                var value = uri.OriginalString;
                var comma = value.IndexOf(',');
                if (comma < 0 || !value[..comma].EndsWith(";base64", StringComparison.OrdinalIgnoreCase)
                    || value.Length - comma - 1 > ((MaximumImageBytes + 2L) / 3) * 4)
                {
                    throw new InvalidDataException("The inline image must contain bounded base64 data.");
                }

                bytes = Convert.FromBase64String(value[(comma + 1)..]);
            }
            else
            {
                if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                {
                    throw new InvalidDataException("Only HTTP(S) image sources are allowed.");
                }

                using var scope = ExternalSourceAccess.BeginScope();
                using var ownedClient = sharedClient == null ? (clientFactory ?? ExternalDataHttpClient.Create)() : null;
                var client = sharedClient ?? ownedClient!;
                using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                // An explicit non-image declaration fails closed before any bytes are
                // read; an absent Content-Type is still allowed but must then carry a
                // supported image signature (validated below).
                var declaredType = response.Content.Headers.ContentType?.MediaType;
                if (!string.IsNullOrEmpty(declaredType)
                    && !declaredType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"The remote resource is declared as '{declaredType}', not an image.");
                }

                if (response.Content.Headers.ContentLength > MaximumImageBytes)
                {
                    throw new InvalidDataException("The image source exceeds the 16 MiB limit.");
                }

                await using var input = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                using var output = new MemoryStream();
                var buffer = new byte[81920];
                while (true)
                {
                    var count = await input.ReadAsync(
                        buffer.AsMemory(0, Math.Min(buffer.Length, MaximumImageBytes + 1 - (int)output.Length)),
                        ct).ConfigureAwait(false);
                    if (count == 0)
                    {
                        break;
                    }

                    output.Write(buffer, 0, count);
                    if (output.Length > MaximumImageBytes)
                    {
                        throw new InvalidDataException("The image source exceeds the 16 MiB limit.");
                    }
                }

                bytes = output.ToArray();
            }

            ct.ThrowIfCancellationRequested();
            if (bytes.Length > MaximumImageBytes || !TryDetectMimeType(bytes, out var mime))
            {
                throw new InvalidDataException("The image source is not a supported PNG, JPEG, GIF or WebP image.");
            }

            return image with { ImageUrl = null, ImageData = Convert.ToBase64String(bytes), MimeType = mime };
        }

        /// <summary>Identifies supported transport images by signature rather than a remote MIME declaration.</summary>
        /// <param name="bytes">Image bytes.</param>
        /// <param name="mime">The detected MIME type, or an empty string.</param>
        /// <returns>Whether a supported image signature is present.</returns>
        public static bool TryDetectMimeType(ReadOnlySpan<byte> bytes, out string mime)
        {
            mime = string.Empty;
            if (bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            {
                mime = "image/png";
            }
            else if (bytes.Length >= 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255)
            {
                mime = "image/jpeg";
            }
            else if (bytes.Length >= 6 && bytes[0] == 'G' && bytes[1] == 'I' && bytes[2] == 'F'
                && bytes[3] == '8' && (bytes[4] == '7' || bytes[4] == '9') && bytes[5] == 'a')
            {
                mime = "image/gif";
            }
            else if (bytes.Length >= 12 && bytes[0] == 'R' && bytes[1] == 'I' && bytes[2] == 'F' && bytes[3] == 'F'
                && bytes[8] == 'W' && bytes[9] == 'E' && bytes[10] == 'B' && bytes[11] == 'P')
            {
                mime = "image/webp";
            }

            return mime.Length > 0;
        }

        /// <summary>Resolves external images to bounded inline data before handing the request to a provider.</summary>
        internal static async Task ApplyAsync(
            AIRequestCall request,
            Func<HttpClient>? clientFactory = null,
            CancellationToken cancellationToken = default)
        {
            var body = request.Body;
            if (body?.Interactions == null ||
                !body.Interactions.OfType<AIInteractionImage>().Any(image => image.ImageUrl != null))
            {
                return;
            }

            using var scope = ExternalSourceAccess.BeginScope();
            using var client = (clientFactory ?? ExternalDataHttpClient.Create)();
            var interactions = body.Interactions.ToArray();
            for (var index = 0; index < interactions.Length; index++)
            {
                if (interactions[index] is not AIInteractionImage image || image.ImageUrl == null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(image.ImageData))
                {
                    interactions[index] = image with { ImageUrl = null };
                    continue;
                }

                // Embedded data has no external destination and is already handled by provider codecs.
                if (image.ImageUrl.Scheme.Equals("data", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                interactions[index] = await MaterializeCoreAsync(image, cancellationToken, null, client).ConfigureAwait(false);
            }

            request.Body = body with { Interactions = interactions };
        }
    }
}
