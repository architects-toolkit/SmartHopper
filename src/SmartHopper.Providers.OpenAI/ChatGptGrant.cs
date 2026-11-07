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

using System.Linq;

namespace SmartHopper.Providers.OpenAI
{
    /// <summary>
    /// Result of a successful "Sign in with ChatGPT" OAuth grant or refresh.
    /// </summary>
    public sealed class ChatGptGrant
    {
        /// <summary>Access token used as the bearer credential for OpenAI API calls.</summary>
        public string AccessToken { get; set; } = string.Empty;

        /// <summary>Refresh token for future token rotation.</summary>
        public string RefreshToken { get; set; } = string.Empty;

        /// <summary>OIDC ID token retained for <c>id_token_hint</c> on later sign-ins.</summary>
        public string IdToken { get; set; } = string.Empty;

        /// <summary>
        /// The issued OAuth client ID bound to the user's registration
        /// (<c>oaiapp_...</c>). Reused on refresh and later sign-ins.
        /// </summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>Token lifetime in seconds, if provided.</summary>
        public int? ExpiresIn { get; set; }

        /// <summary>Space-separated scopes granted by the user.</summary>
        public string Scopes { get; set; } = string.Empty;

        /// <summary>Validated account subject (<c>sub</c> claim of the ID token).</summary>
        public string Subject { get; set; } = string.Empty;

        /// <summary>Validated account email (<c>email</c> claim of the ID token), when present.</summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Gets a value indicating whether the grant includes permission to use the
        /// user's ChatGPT plan for Responses API requests.
        /// </summary>
        public bool HasPlanUsageScope
        {
            get
            {
                return !string.IsNullOrWhiteSpace(this.Scopes)
                    && this.Scopes.Split(' ').Contains("chatgpt.tokens.use.direct", System.StringComparer.Ordinal);
            }
        }
    }
}
