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
 * along with this library; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

using System;
using System.Collections.Generic;
using System.Net.Http;

namespace SmartHopper.Infrastructure.Utils
{
    /// <summary>
    /// Shared helper for applying request-scoped HTTP headers consistently across
    /// streaming and non-streaming provider calls.
    /// </summary>
    public static class HttpHeadersHelper
    {
        /// <summary>
        /// Applies additional request-scoped headers to the HttpClient.
        /// Reserved headers are excluded and applied via authentication helpers: 'Authorization', 'x-api-key'.
        /// </summary>
        /// <param name="client">Target HttpClient.</param>
        /// <param name="headers">Headers to apply.</param>
        public static void ApplyExtraHeaders(HttpClient client, IDictionary<string, string> headers)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            if (headers == null || headers.Count == 0) return;

            foreach (var kv in headers)
            {
                if (string.Equals(kv.Key, "Authorization", StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(kv.Key, "x-api-key", StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    client.DefaultRequestHeaders.TryAddWithoutValidation(kv.Key, kv.Value ?? string.Empty);
                }
                catch
                {
                    // Ignore per-header errors to keep calls resilient.
                }
            }
        }
    }
}
