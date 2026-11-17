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

namespace SmartHopper.Core.UI.Chat
{
    /// <summary>
    /// Decides which WebView navigations and bridge events may touch the trusted chat document.
    /// Pure logic with no Rhino/Eto dependencies so it can be unit-tested on every target.
    /// </summary>
    internal static class WebChatNavigationPolicy
    {
        /// <summary>Hostname used as the embedded chat document origin on Windows.</summary>
        internal const string TrustedHost = "smarthopper.local";

        /// <summary>Query parameter carrying the per-document bridge session token.</summary>
        internal const string DocumentTokenParameter = "doc";

        /// <summary>Returns whether the scheme belongs to the JS-to-host bridge (never a real navigation).</summary>
        internal static bool IsBridgeScheme(Uri uri)
        {
            if (uri == null || !uri.IsAbsoluteUri)
            {
                return false;
            }

            return string.Equals(uri.Scheme, "sh", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(uri.Scheme, "clipboard", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Returns whether the URI is an external web link that must open in the system browser.</summary>
        internal static bool IsExternalLink(Uri uri)
        {
            if (uri == null || !uri.IsAbsoluteUri)
            {
                return false;
            }

            return string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns whether the loaded URI is allowed to become the privileged chat document.
        /// Fails closed for schemes that could carry a foreign origin (http(s) to a host
        /// other than the embedded base or the loopback document shim, bridge schemes, or
        /// anything else unexpected); inline documents (null, about:blank, file:, data:)
        /// produced by LoadHtml are accepted.
        /// </summary>
        internal static bool IsTrustedDocumentUri(Uri uri)
        {
            // Platform handlers report varying URIs for LoadHtml content; a missing URI
            // cannot be a foreign origin (foreign navigation is cancelled in DocumentLoading).
            if (uri == null)
            {
                return true;
            }

            if (!uri.IsAbsoluteUri)
            {
                return false;
            }

            if (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(uri.Host, TrustedHost, StringComparison.OrdinalIgnoreCase);
            }

            // Eto's Windows WebView2 handler never navigates to the requested https base
            // URI: it serves LoadHtml content through an embedded HttpListener bound to an
            // ephemeral loopback port (http://localhost:<port>/). That shim is the embedded
            // document itself, so plain http is trusted only for loopback hosts.
            if (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
            {
                return IsLoopbackHost(uri.Host);
            }

            // Inline/local documents produced by LoadHtml depending on the Eto handler.
            if (string.Equals(uri.AbsoluteUri, "about:blank", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(uri.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(uri.Scheme, "data", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        /// <summary>Returns whether the host is a loopback address used by the embedded document shim.</summary>
        private static bool IsLoopbackHost(string host)
        {
            return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(host, "127.0.0.1", StringComparison.Ordinal) ||
                string.Equals(host, "[::1]", StringComparison.Ordinal) ||
                string.Equals(host, "::1", StringComparison.Ordinal);
        }

        /// <summary>
        /// Returns whether a bridge event carried the token minted for the current document.
        /// Fails closed: an empty expected token never authorizes an event.
        /// </summary>
        internal static bool BridgeTokenMatches(string expectedToken, string actualToken)
        {
            return !string.IsNullOrEmpty(expectedToken) &&
                string.Equals(expectedToken, actualToken ?? string.Empty, StringComparison.Ordinal);
        }
    }
}
