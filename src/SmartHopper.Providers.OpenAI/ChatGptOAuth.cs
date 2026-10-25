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
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace SmartHopper.Providers.OpenAI
{
    /// <summary>
    /// Performs an OAuth 2.0/OIDC PKCE sign-in for "Continue with ChatGPT".
    /// Opens the user's browser, waits for the loopback redirect on a temporary
    /// <see cref="TcpListener"/>, and exchanges the authorization code for tokens.
    /// Implements OpenAI's open-source client flow: first-time registrations use
    /// the dynamic client (<c>dynamic_agent_client</c>) together with a stable
    /// per-install <c>ext_agent_host_id</c>; later sign-ins reuse the issued client ID.
    /// </summary>
    /// <remarks>
    /// A raw <see cref="TcpListener"/> is used instead of <see cref="HttpListener"/>:
    /// on Windows every <see cref="HttpListener"/> in the process shares the same
    /// http.sys request-queue handle, so stopping or disposing any other listener
    /// (including in unrelated plugins) faults pending <c>GetContext</c> calls and
    /// kills the callback. TcpListener has no shared state and needs no URL ACL.
    /// </remarks>
    public static class ChatGptOAuth
    {
        internal const string AuthorizeUrl = "https://auth.openai.com/api/accounts/authorize";
        internal const string TokenUrl = "https://auth.openai.com/api/accounts/oauth/token";
        internal const string ApiResource = "https://api.openai.com/v1";
        internal const string DynamicClientId = "dynamic_agent_client";
        internal const string AgentNameHint = "SmartHopper";
        internal const string Scopes = "openid profile email offline_access resource.invoke chatgpt.tokens.use.direct";
        internal const string PlanUsageScope = "chatgpt.tokens.use.direct";

        private const int ReadWriteTimeoutMilliseconds = 15000;
        private const int MaxRequestBytes = 16384;

        /// <summary>
        /// Creates the <see cref="HttpClient"/> used for token, discovery and JWKS calls.
        /// Replaceable in tests; production code always uses the default factory.
        /// </summary>
        internal static Func<HttpClient> HttpClientFactory { get; set; } = () => new HttpClient();

        /// <summary>
        /// Runs the interactive sign-in: binds a loopback listener on an available port,
        /// opens the browser at the OpenAI authorization endpoint, waits for the callback,
        /// exchanges the code, and validates the ID token.
        /// </summary>
        /// <param name="hostId">Stable host identifier (e.g. <c>urn:uuid:...</c>) persisted for this installation.</param>
        /// <param name="existingClientId">Issued client ID from a previous registration for the same account, or <c>null</c> for first-time registration.</param>
        /// <param name="idTokenHint">ID token retained from this account's previous sign-in; skips account selection.</param>
        /// <param name="loginHint">Saved account email used to pre-select the account.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The validated <see cref="ChatGptGrant"/>.</returns>
        public static async Task<ChatGptGrant> SignInAsync(
            string hostId,
            string? existingClientId = null,
            string? idTokenHint = null,
            string? loginHint = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(hostId))
            {
                throw new ArgumentException("A host identifier is required.", nameof(hostId));
            }

            var state = GenerateState();
            var nonce = GenerateState();
            var codeVerifier = GenerateCodeVerifier();
            var codeChallenge = GenerateCodeChallenge(codeVerifier);

            // Port 0 lets the OS pick a free port; the selected port is embedded in the
            // exact redirect URI sent with the authorization request and code exchange.
            TcpListener listener;
            try
            {
                listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
            }
            catch (SocketException ex)
            {
                throw new InvalidOperationException("Could not start a local listener for the sign-in callback.", ex);
            }

            try
            {
                var selectedPort = ((IPEndPoint)listener.LocalEndpoint).Port;
                var redirectUri = new Uri($"http://127.0.0.1:{selectedPort}/auth/callback");
                var authorizeUrl = BuildAuthorizeUrl(
                    redirectUri,
                    codeChallenge,
                    state,
                    nonce,
                    hostId,
                    existingClientId,
                    idTokenHint,
                    loginHint);

#pragma warning disable CA1031
                try
                {
                    Process.Start(new ProcessStartInfo(authorizeUrl) { UseShellExecute = true });
                    Debug.WriteLine("[ChatGptOAuth] Opened browser for ChatGPT sign-in.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ChatGptOAuth] Could not open browser: {ex.Message}");
                }
#pragma warning restore CA1031

                var query = await WaitForCallbackAsync(listener, cancellationToken).ConfigureAwait(false);
                var returnedState = GetQueryValue(query, "state");
                var code = GetQueryValue(query, "code");
                var error = GetQueryValue(query, "error");
                var errorDescription = GetQueryValue(query, "error_description");

                if (!string.IsNullOrWhiteSpace(error))
                {
                    throw new InvalidOperationException($"ChatGPT sign-in error: {error}. {errorDescription}".Trim());
                }

                if (string.IsNullOrWhiteSpace(code))
                {
                    throw new InvalidOperationException("The sign-in callback did not include an authorization code.");
                }

                if (!string.Equals(returnedState, state, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("The sign-in state does not match the authorization attempt.");
                }

                // On first-time registration the callback returns the issued client ID
                // (oaiapp_...); later sign-ins reuse the stored one.
                var issuedClientId = GetQueryValue(query, "client_id");
                var clientId = !string.IsNullOrWhiteSpace(issuedClientId) ? issuedClientId : existingClientId;
                if (string.IsNullOrWhiteSpace(clientId))
                {
                    throw new InvalidOperationException("The sign-in did not issue a client ID; the registration is incomplete.");
                }

                return await ExchangeCodeAsync(redirectUri, code, codeVerifier, nonce, clientId, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                listener.Stop();
            }
        }

        /// <summary>
        /// Exchanges a stored refresh token for a new token set (rotating refresh token).
        /// </summary>
        /// <param name="clientId">The issued client ID saved with the token set.</param>
        /// <param name="refreshToken">The stored refresh token.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The refreshed <see cref="ChatGptGrant"/>.</returns>
        /// <exception cref="InvalidOperationException">The refresh grant was rejected (4xx); a new sign-in is required.</exception>
        /// <exception cref="HttpRequestException">A transient network failure occurred.</exception>
        public static async Task<ChatGptGrant> RefreshAsync(
            string clientId,
            string refreshToken,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                throw new ArgumentException("A client ID is required.", nameof(clientId));
            }

            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                throw new ArgumentException("A refresh token is required.", nameof(refreshToken));
            }

            var fields = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = clientId,
                ["refresh_token"] = refreshToken,
                ["resource"] = ApiResource,
            };

            var tokenResponseText = await PostTokenFormAsync(fields, cancellationToken).ConfigureAwait(false);
            return ParseTokenResponse(tokenResponseText, clientId, isRefresh: true);
        }

        /// <summary>
        /// Attempts to revoke the renewable session's refresh token during sign-out.
        /// Best-effort: network failures are logged and swallowed — local sign-out still proceeds.
        /// </summary>
        /// <param name="refreshToken">The stored refresh token to revoke.</param>
        /// <param name="clientId">The issued client ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public static async Task TryRevokeAsync(
            string refreshToken,
            string clientId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(refreshToken) || string.IsNullOrWhiteSpace(clientId))
                {
                    return;
                }

                var revocationEndpoint = await ChatGptIdToken.GetRevocationEndpointAsync(cancellationToken).ConfigureAwait(false);
                if (revocationEndpoint == null)
                {
                    return;
                }

                var fields = new Dictionary<string, string>
                {
                    ["token"] = refreshToken,
                    ["token_type_hint"] = "refresh_token",
                    ["client_id"] = clientId,
                };

                using var httpClient = HttpClientFactory();
                using var content = new FormUrlEncodedContent(fields);
                using var response = await httpClient.PostAsync(revocationEndpoint, content, cancellationToken).ConfigureAwait(false);
                Debug.WriteLine($"[ChatGptOAuth] Token revocation completed with status {(int)response.StatusCode}.");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ChatGptOAuth] Token revocation failed (local sign-out continues): {ex.Message}");
            }
        }

        /// <summary>
        /// Accepts loopback connections until the sign-in callback request arrives, then
        /// writes the user-facing response and returns the parsed callback query entries.
        /// Requests for other paths (browser probes, favicons) get a 404 and the wait
        /// continues; malformed or aborted connections are skipped rather than failing
        /// the whole sign-in.
        /// </summary>
        /// <param name="listener">The started loopback listener.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <param name="timeout">Optional wait budget; defaults to 5 minutes.</param>
        /// <returns>Parsed <c>key=value</c> callback query entries.</returns>
        internal static async Task<string[]> WaitForCallbackAsync(
            TcpListener listener,
            CancellationToken cancellationToken,
            TimeSpan? timeout = null)
        {
            var timeoutTask = Task.Delay(timeout ?? TimeSpan.FromMinutes(5), cancellationToken);

            while (true)
            {
                var acceptTask = listener.AcceptTcpClientAsync(cancellationToken).AsTask();
                var completed = await Task.WhenAny(acceptTask, timeoutTask).ConfigureAwait(false);
                if (completed == timeoutTask)
                {
                    // Stopping the listener makes the pending accept fault so it can be observed.
                    listener.Stop();
#pragma warning disable CA1031
                    try
                    {
                        await acceptTask.ConfigureAwait(false);
                    }
                    catch
                    {
                        // The listener was stopped or cancelled; the task is expected to fault.
                    }
#pragma warning restore CA1031

                    cancellationToken.ThrowIfCancellationRequested();

                    throw new TimeoutException("ChatGPT sign-in timed out. The browser did not complete the sign-in within 5 minutes.");
                }

                using (var client = await acceptTask.ConfigureAwait(false))
                {
                    string? target;
                    try
                    {
                        client.ReceiveTimeout = ReadWriteTimeoutMilliseconds;
                        client.SendTimeout = ReadWriteTimeoutMilliseconds;
                        target = await ReadRequestTargetAsync(client, cancellationToken).ConfigureAwait(false);
                    }
#pragma warning disable CA1031
                    catch (Exception ex) when (!(ex is OperationCanceledException))
                    {
                        Debug.WriteLine($"[ChatGptOAuth] Dropped unreadable callback connection: {ex.GetType().Name}");
                        continue;
                    }
#pragma warning restore CA1031

                    if (!IsCallbackTarget(target))
                    {
                        await WriteResponseAsync(client, "404 Not Found", "Not found.", cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    var query = ParseQuery(new Uri($"http://127.0.0.1{target}", UriKind.Absolute).Query);
                    var error = GetQueryValue(query, "error");

                    var success = string.IsNullOrWhiteSpace(error);
                    var message = success
                        ? "Sign-in successful. You can close this tab and return to SmartHopper."
                        : $"Sign-in failed: {WebUtility.HtmlEncode(error)}. You can close this tab.";
                    await WriteResponseAsync(client, "200 OK", message, cancellationToken).ConfigureAwait(false);
                    Debug.WriteLine($"[ChatGptOAuth] Received sign-in callback request (success={success})");
                    return query;
                }
            }
        }

        /// <summary>
        /// Reads HTTP headers (bounded) and extracts the request target of the first line.
        /// </summary>
        private static async Task<string?> ReadRequestTargetAsync(TcpClient client, CancellationToken cancellationToken)
        {
            var stream = client.GetStream();
            var buffer = new byte[4096];
            var builder = new StringBuilder();

            while (builder.Length <= MaxRequestBytes)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
                if (read <= 0)
                {
                    break;
                }

                builder.Append(Encoding.ASCII.GetString(buffer, 0, read));
                if (builder.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
                {
                    break;
                }
            }

            return ExtractRequestTarget(builder.ToString());
        }

        /// <summary>Extracts the origin-form request target from an HTTP GET request.</summary>
        internal static string? ExtractRequestTarget(string requestText)
        {
            if (string.IsNullOrWhiteSpace(requestText))
            {
                return null;
            }

            var lineEnd = requestText.IndexOf('\r');
            var requestLine = (lineEnd >= 0 ? requestText.Substring(0, lineEnd) : requestText).Trim();
            var parts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !string.Equals(parts[0], "GET", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var target = parts[1];
            if (target.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                && Uri.TryCreate(target, UriKind.Absolute, out var absolute))
            {
                return absolute.PathAndQuery;
            }

            return target.StartsWith("/", StringComparison.Ordinal) ? target : null;
        }

        internal static bool IsCallbackTarget(string? target)
        {
            return target != null && target.StartsWith("/auth/callback", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Writes a minimal HTTP response; failures are logged and ignored.</summary>
        private static async Task WriteResponseAsync(TcpClient client, string status, string message, CancellationToken cancellationToken)
        {
            try
            {
                var body = Encoding.UTF8.GetBytes(
                    $"<!DOCTYPE html><html><body><p>{message}</p></body></html>");
                var header = Encoding.ASCII.GetBytes(
                    $"HTTP/1.1 {status}\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
                var stream = client.GetStream();
                await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
                await stream.WriteAsync(body, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
#pragma warning disable CA1031
            catch (Exception ex)
            {
                Debug.WriteLine($"[ChatGptOAuth] Could not write callback response: {ex.GetType().Name}");
            }
#pragma warning restore CA1031
        }

        /// <summary>
        /// Builds the OpenAI authorization URL. First-time registrations use
        /// <c>client_id=dynamic_agent_client</c> with <c>agent_name_hint</c>; re-authorizations
        /// reuse the issued client ID and may include <c>id_token_hint</c>/<c>login_hint</c>.
        /// </summary>
        internal static string BuildAuthorizeUrl(
            Uri redirectUri,
            string codeChallenge,
            string state,
            string nonce,
            string hostId,
            string? existingClientId,
            string? idTokenHint,
            string? loginHint)
        {
            var isFirstRegistration = string.IsNullOrWhiteSpace(existingClientId);
            var query = new StringBuilder();
            query.Append("client_id=");
            query.Append(Uri.EscapeDataString(isFirstRegistration ? DynamicClientId : existingClientId!));

            if (isFirstRegistration)
            {
                query.Append("&agent_name_hint=");
                query.Append(Uri.EscapeDataString(AgentNameHint));
            }

            query.Append("&ext_agent_host_id=");
            query.Append(Uri.EscapeDataString(hostId));

            if (!string.IsNullOrWhiteSpace(idTokenHint))
            {
                query.Append("&id_token_hint=");
                query.Append(Uri.EscapeDataString(idTokenHint));
            }

            if (!string.IsNullOrWhiteSpace(loginHint))
            {
                query.Append("&login_hint=");
                query.Append(Uri.EscapeDataString(loginHint));
            }

            query.Append("&response_type=code");
            query.Append("&redirect_uri=");
            query.Append(Uri.EscapeDataString(redirectUri.ToString()));
            query.Append("&scope=");
            query.Append(Uri.EscapeDataString(Scopes));
            query.Append("&resource=");
            query.Append(Uri.EscapeDataString(ApiResource));
            query.Append("&state=");
            query.Append(Uri.EscapeDataString(state));
            query.Append("&nonce=");
            query.Append(Uri.EscapeDataString(nonce));
            query.Append("&code_challenge=");
            query.Append(Uri.EscapeDataString(codeChallenge));
            query.Append("&code_challenge_method=S256");

            return string.Concat(AuthorizeUrl, "?", query.ToString());
        }

        internal static string GenerateState()
        {
            return Guid.NewGuid().ToString("N");
        }

        internal static string GenerateCodeVerifier()
        {
            var bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }

            return ToBase64Url(bytes);
        }

        internal static string GenerateCodeChallenge(string verifier)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(verifier));
            return ToBase64Url(hash);
        }

        internal static string[] ParseQuery(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Array.Empty<string>();
            }

            var trimmed = query.TrimStart('?');
            return trimmed
                .Split('&')
                .Select(part =>
                {
                    var pair = part.Split(new[] { '=' }, 2);
                    if (pair.Length != 2)
                    {
                        return null;
                    }

                    var key = Uri.UnescapeDataString(pair[0]);
                    var value = Uri.UnescapeDataString(pair[1]);
                    return $"{key}={value}";
                })
                .Where(x => x != null)
                .Select(x => x!)
                .ToArray();
        }

        internal static string? GetQueryValue(string[] parsed, string key)
        {
            foreach (var item in parsed)
            {
                if (string.IsNullOrWhiteSpace(item))
                {
                    continue;
                }

                var pair = item.Split(new[] { '=' }, 2);
                if (pair.Length == 2 && string.Equals(pair[0], key, StringComparison.OrdinalIgnoreCase))
                {
                    return pair[1];
                }
            }

            return null;
        }

        private static string ToBase64Url(byte[] data)
        {
            return Convert.ToBase64String(data)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        /// <summary>
        /// POSTs a form-encoded grant to the OpenAI token endpoint and returns the raw body.
        /// </summary>
        private static async Task<string> PostTokenFormAsync(
            IReadOnlyDictionary<string, string> fields,
            CancellationToken cancellationToken)
        {
            using var httpClient = HttpClientFactory();
            using var content = new FormUrlEncodedContent(fields);
            using var tokenResponse = await httpClient.PostAsync(TokenUrl, content, cancellationToken).ConfigureAwait(false);
            var tokenResponseText = await tokenResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!tokenResponse.IsSuccessStatusCode)
            {
                var errorCode = TryGetOAuthError(tokenResponseText);
                if ((int)tokenResponse.StatusCode >= 400 && (int)tokenResponse.StatusCode < 500)
                {
                    throw new InvalidOperationException(
                        $"OAuth grant rejected: {(int)tokenResponse.StatusCode} {tokenResponse.ReasonPhrase} {errorCode}".Trim());
                }

                throw new HttpRequestException(
                    $"OAuth token request failed: {(int)tokenResponse.StatusCode} {tokenResponse.ReasonPhrase} {errorCode}".Trim());
            }

            return tokenResponseText;
        }

        /// <summary>Reads the standard OAuth <c>error</c>/<c>error_description</c> fields, if present.</summary>
        private static string TryGetOAuthError(string responseText)
        {
            try
            {
                var json = JObject.Parse(responseText);
                var error = json["error"]?.ToString();
                var description = json["error_description"]?.ToString();
                return string.IsNullOrWhiteSpace(description) ? error ?? string.Empty : $"{error}: {description}";
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Exchanges the authorization code for tokens (form-encoded grant, no client
        /// secret: this flow is a public client), then validates the ID token.
        /// </summary>
        private static async Task<ChatGptGrant> ExchangeCodeAsync(
            Uri redirectUri,
            string code,
            string codeVerifier,
            string nonce,
            string clientId,
            CancellationToken cancellationToken)
        {
            var fields = new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = clientId,
                ["code"] = code,
                ["code_verifier"] = codeVerifier,
                ["redirect_uri"] = redirectUri.ToString(),
                ["resource"] = ApiResource,
            };

            var tokenResponseText = await PostTokenFormAsync(fields, cancellationToken).ConfigureAwait(false);
            var grant = ParseTokenResponse(tokenResponseText, clientId, isRefresh: false);

            var identity = await ChatGptIdToken.ValidateAsync(grant.IdToken, nonce, clientId, cancellationToken).ConfigureAwait(false);
            grant.Subject = identity.Subject;
            grant.Email = identity.Email;
            return grant;
        }

        /// <summary>Validates the complete token grant returned by the token endpoint.</summary>
        private static ChatGptGrant ParseTokenResponse(string tokenResponseText, string clientId, bool isRefresh)
        {
            var tokenJson = JObject.Parse(tokenResponseText);
            var responseName = isRefresh ? "OAuth refresh response" : "OAuth token response";
            var accessToken = tokenJson["access_token"]?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new InvalidOperationException($"{responseName} did not include an access_token.");
            }

            var refreshToken = tokenJson["refresh_token"];
            if (refreshToken == null
                || refreshToken.Type != JTokenType.String
                || string.IsNullOrWhiteSpace(refreshToken.Value<string>()))
            {
                throw new InvalidOperationException($"{responseName} did not include a refresh_token.");
            }

            var expiresInToken = tokenJson["expires_in"];
            var expiresIn = expiresInToken != null && int.TryParse(
                expiresInToken.ToString(Newtonsoft.Json.Formatting.None),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var parsed)
                ? parsed
                : (int?)null;

            var grant = new ChatGptGrant
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken.Value<string>()!,
                IdToken = tokenJson["id_token"]?.ToString() ?? string.Empty,
                ClientId = clientId,
                ExpiresIn = expiresIn,
                Scopes = tokenJson["scope"]?.ToString() ?? string.Empty,
            };

            if (!isRefresh && string.IsNullOrWhiteSpace(grant.IdToken))
            {
                throw new InvalidOperationException($"{responseName} did not include an id_token.");
            }

            return grant;
        }
    }
}
