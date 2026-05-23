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

using System.Net.Http;

namespace SmartHopper.Infrastructure.AIProviders
{
    /// <summary>
    /// Exception thrown by streaming adapters when the provider returns a non-success HTTP status,
    /// carrying the classification (network-like vs provider error) so callers can build a structured
    /// <c>AIReturn</c> with the appropriate severity.
    /// Inherits <see cref="HttpRequestException"/> for backwards compatibility with existing
    /// <c>catch (HttpRequestException)</c> blocks.
    /// </summary>
    public sealed class ProviderHttpStatusException : HttpRequestException
    {
        /// <summary>
        /// Gets the HTTP status code returned by the provider.
        /// </summary>
        public int StatusCode { get; }

        /// <summary>
        /// Gets a value indicating whether the error should be classified as a network-like
        /// (transient/connectivity) error rather than a provider error (client-side misuse).
        /// </summary>
        public bool IsNetworkLike { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ProviderHttpStatusException"/> class.
        /// </summary>
        /// <param name="statusCode">HTTP status code.</param>
        /// <param name="isNetworkLike">Whether the error is network-like.</param>
        /// <param name="message">Pre-formatted, user-facing error message.</param>
        public ProviderHttpStatusException(int statusCode, bool isNetworkLike, string message)
            : base(message)
        {
            this.StatusCode = statusCode;
            this.IsNetworkLike = isNetworkLike;
        }
    }
}
