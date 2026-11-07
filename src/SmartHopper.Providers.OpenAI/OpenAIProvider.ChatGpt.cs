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
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SmartHopper.ProviderSdk.AICall.Core.Requests;
using SmartHopper.ProviderSdk.Diagnostics;
using SmartHopper.ProviderSdk.Hosting;

namespace SmartHopper.Providers.OpenAI
{
    /// <summary>
    /// "Continue with ChatGPT" sign-in lifecycle for the OpenAI provider.
    /// When connected, the OAuth access token becomes the primary credential and the
    /// direct API key serves as fallback (and vice versa when only one is configured).
    /// </summary>
    public partial class OpenAIProvider
    {
        /// <summary>
        /// Value written to <see cref="AIRequestCall.ResolvedCredentialTag"/> when the
        /// request's credential is the ChatGPT sign-in access token.
        /// </summary>
        internal const string ChatGptCredentialTag = "chatgpt";

        /// <summary>Refresh skew: access tokens are rotated once inside this window of expiry.</summary>
        private static readonly TimeSpan TokenRefreshSkew = TimeSpan.FromSeconds(60);

        private readonly SemaphoreSlim loginGate = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim refreshGate = new SemaphoreSlim(1, 1);
        private readonly object connectionStateLock = new object();
        private CancellationTokenSource? connectionCancellation;
        private int actionInFlight;

        /// <summary>How long the interactive sign-in waits for the browser flow.</summary>
        internal TimeSpan LoginTimeout { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>The in-flight sign-in task, when one is running.</summary>
        internal Task? ConnectionTask { get; private set; }

        /// <summary>True while a sign-in or refresh action is running.</summary>
        internal bool IsChatGptActionRunning => Volatile.Read(ref this.actionInFlight) != 0;

        /// <summary>Gets or sets the OAuth sign-in implementation used by this provider.</summary>
        internal Func<string, string?, string?, string?, CancellationToken, Task<ChatGptGrant>> OAuthLoginAsync { get; set; } = ChatGptOAuth.SignInAsync;

        /// <summary>Gets or sets the OAuth token refresh implementation used by this provider.</summary>
        internal Func<string, string, CancellationToken, Task<ChatGptGrant>> OAuthRefreshAsync { get; set; } = ChatGptOAuth.RefreshAsync;

        /// <summary>Gets or sets the OAuth token revocation implementation used by this provider.</summary>
        internal Func<string, string, CancellationToken, Task> OAuthRevokeAsync { get; set; } = ChatGptOAuth.TryRevokeAsync;

        /// <summary>True when a stored OAuth grant exists.</summary>
        internal bool IsChatGptConnected
            => !string.IsNullOrWhiteSpace(this.GetSetting<string>("ChatGptAccessToken"))
                || !string.IsNullOrWhiteSpace(this.GetSetting<string>("ChatGptRefreshToken"));

        /// <summary>True when the grant includes permission to use the user's ChatGPT plan.</summary>
        internal bool HasChatGptPlanUsage =>
            (this.GetSetting<string>("ChatGptScopes") ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Contains(ChatGptOAuth.PlanUsageScope, StringComparer.Ordinal);

        /// <summary>
        /// Falls back to the direct API key when the OAuth access token was the primary
        /// credential and got rejected (401/403). Returns null when the API key itself
        /// was primary or none is configured.
        /// </summary>
        protected override string? GetAuthFallbackCredential(AIRequestCall request)
        {
            if (!string.Equals(request?.ResolvedCredentialTag, ChatGptCredentialTag, StringComparison.Ordinal))
            {
                return null;
            }

            var apiKey = this.GetSetting<string>("ApiKey");
            return string.IsNullOrWhiteSpace(apiKey) ? null : apiKey;
        }

        /// <inheritdoc/>
        public override string? AuthenticationErrorMessage =>
            "OpenAI rejected the credentials. Reconnect via 'Continue with ChatGPT' in SmartHopper settings, or check the API key.";

        /// <summary>
        /// Prepares ChatGPT sign-in authentication for the request. When the stored grant
        /// is the active credential this ensures a fresh access token (refreshing when
        /// close to expiry), declares the streaming transport the plan-usage route
        /// requires, and flags the request so <see cref="GetApiKey"/> returns the token.
        /// Requests that carry audio keep the direct API key path, because the
        /// sign-in route does not accept audio input.
        /// </summary>
        internal async Task<AIRequestCall> PrepareChatGptAuthAsync(AIRequestCall request, CancellationToken cancellationToken)
        {
            request.ResolvedCredential = null;
            request.ResolvedCredentialTag = null;
            if (!this.IsChatGptConnected)
            {
                return request;
            }

            // Only /responses (inference) and /models (account catalog) are served by the
            // sign-in route; legacy endpoints (images, audio, speech) keep the API key.
            var endpoint = request.Endpoint ?? string.Empty;
            var isResponses = endpoint.Contains("/responses", StringComparison.OrdinalIgnoreCase);
            var isModels = endpoint.Contains("/models", StringComparison.OrdinalIgnoreCase);
            if (!isResponses && !isModels)
            {
                return request;
            }

            await this.EnsureFreshAccessTokenAsync(cancellationToken).ConfigureAwait(false);
            var accessToken = this.GetSetting<string>("ChatGptAccessToken");
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                // The grant was cleared after a rejected refresh; the API key takes over.
                return request;
            }

            // The request carries the resolved credential so every downstream stage
            // (authentication, body encoding, fallback retry) can see it.
            request.ResolvedCredential = accessToken;
            request.ResolvedCredentialTag = ChatGptCredentialTag;

            // ChatGPT plan usage is only served through the streaming Responses endpoint.
            if (isResponses)
            {
                request.WantsStreaming = true;
            }

            return request;
        }

        /// <summary>
        /// Rotates the stored ChatGPT access token when it is expired or inside the refresh
        /// skew window. Permanent failures (revoked grant) clear the stored credentials so
        /// the API key path takes over; transient network errors keep the stale token.
        /// </summary>
        internal async Task EnsureFreshAccessTokenAsync(CancellationToken cancellationToken = default)
        {
            var refreshToken = this.GetSetting<string>("ChatGptRefreshToken");
            var accessToken = this.GetSetting<string>("ChatGptAccessToken");
            var accessTokenMissing = string.IsNullOrWhiteSpace(accessToken);

            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                return;
            }

            if (!accessTokenMissing && DateTimeOffset.UtcNow < this.GetAccessTokenExpiry() - TokenRefreshSkew)
            {
                return;
            }

            await this.refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Re-check inside the gate: another call may have refreshed already.
                accessTokenMissing = string.IsNullOrWhiteSpace(this.GetSetting<string>("ChatGptAccessToken"));
                if (!accessTokenMissing
                    && this.GetAccessTokenExpiry() - TokenRefreshSkew > DateTimeOffset.UtcNow)
                {
                    return;
                }

                refreshToken = this.GetSetting<string>("ChatGptRefreshToken");
                if (string.IsNullOrWhiteSpace(refreshToken))
                {
                    return;
                }

                var clientId = this.GetSetting<string>("ChatGptClientId") ?? string.Empty;
                try
                {
                    var refreshed = await this.OAuthRefreshAsync(clientId, refreshToken, cancellationToken).ConfigureAwait(false);
                    if (refreshed != null && !string.IsNullOrWhiteSpace(refreshed.AccessToken))
                    {
                        this.StoreGrant(refreshed);
                        Debug.WriteLine("[OpenAI] ChatGPT access token refreshed.");
                        return;
                    }

                    this.ClearGrantTokens();
                }
                catch (InvalidOperationException ex)
                {
                    // Permanent grant failure (revoked/expired refresh token): force re-sign-in.
                    Debug.WriteLine($"[OpenAI] ChatGPT refresh rejected, clearing stored grant: {ex.Message}");
                    this.ClearGrantTokens();
                    this.SetConnectionStatus("Sign-in expired — sign in again");
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // Transient failure: keep the stale token so the call can still be attempted.
                    Debug.WriteLine($"[OpenAI] ChatGPT token refresh failed (transient): {ex.Message}");
                }
            }
            finally
            {
                this.refreshGate.Release();
            }
        }

        /// <summary>Returns the persisted access-token expiry, or the minimum value when unset/unparsable.</summary>
        private DateTimeOffset GetAccessTokenExpiry()
        {
            var raw = this.GetSetting<string>("ChatGptAccessTokenExpiresAt");
            return DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
                ? parsed
                : DateTimeOffset.MinValue;
        }

        /// <summary>Persists the token set and account metadata from a validated grant.</summary>
        private void StoreGrant(ChatGptGrant grant)
        {
            this.SetSetting("ChatGptAccessToken", grant.AccessToken);
            this.SetSetting("ChatGptRefreshToken", grant.RefreshToken);
            if (!string.IsNullOrWhiteSpace(grant.IdToken))
            {
                this.SetSetting("ChatGptIdToken", grant.IdToken);
            }

            if (!string.IsNullOrWhiteSpace(grant.ClientId))
            {
                this.SetSetting("ChatGptClientId", grant.ClientId);
            }

            if (!string.IsNullOrWhiteSpace(grant.Email))
            {
                this.SetSetting("ChatGptEmail", grant.Email);
            }

            if (!string.IsNullOrWhiteSpace(grant.Scopes))
            {
                this.SetSetting("ChatGptScopes", grant.Scopes);
            }

            var expiresAt = grant.ExpiresIn.HasValue
                ? DateTimeOffset.UtcNow.AddSeconds(grant.ExpiresIn.Value)
                : DateTimeOffset.MinValue;
            this.SetSetting("ChatGptAccessTokenExpiresAt", expiresAt.ToString("O", CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Clears the token set but keeps the client registration, host ID and account
        /// email so a later sign-in can reuse them as hints.
        /// </summary>
        private void ClearGrantTokens()
        {
            this.SetSetting("ChatGptAccessToken", string.Empty);
            this.SetSetting("ChatGptRefreshToken", string.Empty);
            this.SetSetting("ChatGptIdToken", string.Empty);
            this.SetSetting("ChatGptAccessTokenExpiresAt", string.Empty);
            this.SetSetting("ChatGptScopes", string.Empty);
        }

        /// <summary>Returns or creates the stable per-install OAuth host identifier.</summary>
        private string EnsureHostId()
        {
            var hostId = this.GetSetting<string>("ChatGptHostId");
            if (!string.IsNullOrWhiteSpace(hostId))
            {
                return hostId;
            }

            hostId = $"urn:uuid:{Guid.NewGuid():D}";
            this.SetSetting("ChatGptHostId", hostId);
            return hostId;
        }

        /// <summary>Writes the user-facing connection status and its colour level.</summary>
        private void SetConnectionStatus(string status)
        {
            this.SetSetting("ChatGptStatus", status);
            this.SetSetting("ChatGptStatusLevel", StatusLevel(status));
        }

        /// <summary>Maps a user-facing connection status to the status-label level.</summary>
        internal static string StatusLevel(string status)
        {
            if (status.Contains("fail", StringComparison.OrdinalIgnoreCase)
                || status.Contains("timed out", StringComparison.OrdinalIgnoreCase)
                || status.Contains("expired", StringComparison.OrdinalIgnoreCase)
                || status.Contains("revoked", StringComparison.OrdinalIgnoreCase))
            {
                return "error";
            }

            if (status.StartsWith("Connected", StringComparison.Ordinal))
            {
                return "ok";
            }

            if (status.StartsWith("Connecting", StringComparison.Ordinal)
                || status.StartsWith("Refreshing", StringComparison.Ordinal))
            {
                return "busy";
            }

            return "none";
        }

        /// <summary>
        /// Rewrites a stale busy status ("Connecting…") when no sign-in action is
        /// actually running, e.g. after the process was closed mid sign-in. The busy
        /// state is persisted, so without this pass the settings page shows it forever.
        /// </summary>
        internal void NormalizeConnectionStatus()
        {
            if (this.IsChatGptActionRunning)
            {
                return;
            }

            var status = this.GetSetting<string>("ChatGptStatus") ?? string.Empty;
            if (!string.Equals(StatusLevel(status), "busy", StringComparison.Ordinal))
            {
                return;
            }

            var normalized = this.IsChatGptConnected
                ? this.ConnectedStatusText()
                : "Not connected";
            Debug.WriteLine($"[OpenAI] Normalizing stale ChatGPT busy status '{status}' -> '{normalized}'");
            this.SetConnectionStatus(normalized);
        }

        /// <summary>The status text shown while connected.</summary>
        private string ConnectedStatusText()
        {
            var email = this.GetSetting<string>("ChatGptEmail");
            var account = string.IsNullOrWhiteSpace(email) ? "ChatGPT account" : email;
            return this.HasChatGptPlanUsage
                ? $"Connected as {account}"
                : $"Connected as {account} (plan usage not granted)";
        }

        /// <summary>Starts OAuth sign-in without blocking settings validation.</summary>
        internal Task StartChatGptSignIn(CancellationToken cancellationToken = default)
        {
            if (!this.loginGate.Wait(0))
            {
                Debug.WriteLine("[OpenAI] ChatGPT sign-in requested while another action is already running; ignoring.");
                return this.ConnectionTask ?? Task.CompletedTask;
            }

            var connectionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            this.connectionCancellation = connectionCancellation;
            Volatile.Write(ref this.actionInFlight, 1);
            this.ConnectionTask = Task.Run(() => this.RunChatGptSignInAsync(connectionCancellation));
            this.SetConnectionStatus("Connecting — complete sign-in in your browser…");
            Debug.WriteLine("[OpenAI] ChatGPT sign-in started.");
            return this.ConnectionTask;
        }

        /// <summary>Cancels the active sign-in action.</summary>
        internal void CancelChatGptSignIn()
        {
            var cancellation = this.connectionCancellation;
            if (cancellation == null)
            {
                return;
            }

            try
            {
                cancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private async Task RunChatGptSignInAsync(CancellationTokenSource connectionCancellation)
        {
            try
            {
                using var loginCancellation = CancellationTokenSource.CreateLinkedTokenSource(connectionCancellation.Token);
                loginCancellation.CancelAfter(this.LoginTimeout);

                ChatGptGrant grant;
                try
                {
                    grant = await this.OAuthLoginAsync(
                        this.EnsureHostId(),
                        this.GetSetting<string>("ChatGptClientId"),
                        this.GetSetting<string>("ChatGptIdToken"),
                        this.GetSetting<string>("ChatGptEmail"),
                        loginCancellation.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (connectionCancellation.IsCancellationRequested)
                {
                    throw;
                }
                catch (OperationCanceledException) when (loginCancellation.IsCancellationRequested)
                {
                    this.SetConnectionStatus("Sign-in timed out — retry");
                    return;
                }

                if (grant == null || string.IsNullOrWhiteSpace(grant.AccessToken))
                {
                    throw new InvalidOperationException("The sign-in did not return an access token.");
                }

                lock (this.connectionStateLock)
                {
                    connectionCancellation.Token.ThrowIfCancellationRequested();
                    this.StoreGrant(grant);
                    this.SetConnectionStatus(this.ConnectedStatusText());
                }

                Debug.WriteLine("[OpenAI] ChatGPT sign-in completed; grant stored.");
            }
            catch (OperationCanceledException) when (connectionCancellation.IsCancellationRequested)
            {
                if (!string.Equals(this.GetSetting<string>("ChatGptStatus"), "Not connected", StringComparison.Ordinal))
                {
                    this.SetConnectionStatus("Sign-in cancelled");
                }
            }
            catch (Exception ex)
            {
                this.ReportConnectionFailure(ex);
            }
            finally
            {
                this.CompleteBackgroundAction(connectionCancellation);
            }
        }

        private void CompleteBackgroundAction(CancellationTokenSource connectionCancellation)
        {
            if (ReferenceEquals(this.connectionCancellation, connectionCancellation))
            {
                this.connectionCancellation = null;
            }

            connectionCancellation.Dispose();
            Volatile.Write(ref this.actionInFlight, 0);
            this.loginGate.Release();
        }

        private void ReportConnectionFailure(Exception exception)
        {
            Debug.WriteLine($"[OpenAI] ChatGPT sign-in background action failed: {exception.Message}");
            ProviderSdkHost.Diagnostics.Report(this.Name, new SHRuntimeMessage(
                SHRuntimeMessageSeverity.Error,
                SHRuntimeMessageOrigin.Provider,
                SHMessageCode.Unknown,
                "ChatGPT sign-in failed — retry"));
            this.SetConnectionStatus("Sign-in failed — retry");
        }

        /// <summary>Disconnects the sign-in: revokes the session best-effort and clears tokens.</summary>
        internal void DisconnectChatGpt()
        {
            this.CancelChatGptSignIn();
            lock (this.connectionStateLock)
            {
                var refreshToken = this.GetSetting<string>("ChatGptRefreshToken");
                var clientId = this.GetSetting<string>("ChatGptClientId");
                if (!string.IsNullOrWhiteSpace(refreshToken) && !string.IsNullOrWhiteSpace(clientId))
                {
                    _ = Task.Run(() => this.OAuthRevokeAsync(refreshToken, clientId, CancellationToken.None));
                }

                this.ClearGrantTokens();
                this.SetConnectionStatus("Not connected");
            }
        }
    }
}
