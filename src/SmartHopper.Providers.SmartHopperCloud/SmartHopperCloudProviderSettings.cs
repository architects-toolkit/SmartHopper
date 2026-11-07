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
using System.Linq;
using SmartHopper.ProviderSdk.AIProviders;
using SmartHopper.ProviderSdk.Diagnostics;
using SmartHopper.ProviderSdk.Hosting;
using SmartHopper.ProviderSdk.Logging;
using SmartHopper.ProviderSdk.Settings;

namespace SmartHopper.Providers.SmartHopperCloud
{
    /// <summary>
    /// Settings implementation for the SmartHopper Cloud provider.
    /// The local settings manage server/environment/callback configuration, OAuth connection
    /// actions, the team picker, and a read-only connection status.
    /// The default model is resolved by the cloud from the user's cloud settings.
    /// </summary>
    public class SmartHopperCloudProviderSettings : AIProviderSettings
    {
        private readonly SmartHopperCloudProvider provider;

        /// <summary>
        /// Initializes a new instance of the <see cref="SmartHopperCloudProviderSettings"/> class.
        /// </summary>
        /// <param name="provider">The provider associated with these settings.</param>
        public SmartHopperCloudProviderSettings(SmartHopperCloudProvider provider)
            : base(provider)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        /// <inheritdoc/>
        public override IEnumerable<SettingDescriptor> GetSettingDescriptors()
        {
            var teams = this.provider.GetCloudTeams();
            var teamDescription = "Default team whose wallet pays for SmartHopper Cloud calls. A per-call Team in AI Extra Settings overrides it. Use Refresh teams after connecting.";

            return new[]
            {
                new SettingDescriptor
                {
                    Name = "ServerUrl",
                    Type = typeof(string),
                    DefaultValue = "https://api.smarthopper.xyz/v1/",
                    DisplayName = "Server URL",
                    Description = "The SmartHopper Cloud API base URL, ending in /v1/.",
                },
                new SettingDescriptor
                {
                    Name = "Environment",
                    Type = typeof(string),
                    DefaultValue = string.Empty,
                    DisplayName = "Environment",
                    Description = "Optional environment identifier forwarded to the cloud API.",
                },
                new SettingDescriptor
                {
                    Name = "OAuthCallbackPort",
                    Type = typeof(int),
                    DefaultValue = 62123,
                    DisplayName = "OAuth Callback Port",
                    Description = "Local port for the OAuth redirect. If it is unavailable, the provider tries the fallback range (62123-62130).",
                    ControlParams = new NumericSettingDescriptorControl
                    {
                        UseSlider = false,
                        Min = 1024,
                        Max = 65535,
                        Step = 1,
                    },
                },
                new SettingDescriptor
                {
                    Name = "EnableStreaming",
                    Type = typeof(bool),
                    DefaultValue = true,
                    DisplayName = "Enable Streaming",
                    Description = "Allow streaming responses for this provider.",
                },
                new SettingDescriptor
                {
                    Name = "AccessToken",
                    Type = typeof(string),
                    DefaultValue = string.Empty,
                    DisplayName = "OAuth access token",
                    Description = "Managed automatically by the cloud connection.",
                    IsSecret = true,
                    IsHidden = true,
                },
                new SettingDescriptor
                {
                    Name = "RefreshToken",
                    Type = typeof(string),
                    DefaultValue = string.Empty,
                    DisplayName = "OAuth refresh token",
                    Description = "Managed automatically by the cloud connection.",
                    IsSecret = true,
                    IsHidden = true,
                },
                new SettingDescriptor
                {
                    Name = "AccessTokenExpiresAt",
                    Type = typeof(string),
                    DefaultValue = string.Empty,
                    DisplayName = "OAuth access token expiry",
                    Description = "Managed automatically by the cloud connection.",
                    IsHidden = true,
                },
                new SettingDescriptor
                {
                    Name = "TeamId",
                    Type = typeof(string),
                    DefaultValue = string.Empty,
                    DisplayName = "Account Team",
                    Description = teamDescription,
                    AllowedValues = teams.Select(team => (object)new SettingOption(team.Id, team.Label)).ToList(),
                },
                new SettingDescriptor
                {
                    Name = "Login",
                    Type = typeof(bool),
                    DefaultValue = false,
                    DisplayName = "Connect",
                    Description = "Click the button to open your browser and log in to SmartHopper Cloud.",
                    ControlParams = new ButtonSettingDescriptorControl(),
                },
                new SettingDescriptor
                {
                    Name = "Disconnect",
                    Type = typeof(bool),
                    DefaultValue = false,
                    DisplayName = "Disconnect",
                    Description = "Sign out of SmartHopper Cloud on this computer and remove the stored tokens.",
                    ControlParams = new ButtonSettingDescriptorControl(),
                },
                new SettingDescriptor
                {
                    Name = "RefreshTeams",
                    Type = typeof(bool),
                    DefaultValue = false,
                    DisplayName = "Refresh teams",
                    Description = "Discover teams for the current connection. Does not select a team automatically.",
                    ControlParams = new ButtonSettingDescriptorControl(),
                },
                new SettingDescriptor
                {
                    Name = "ConnectionStatus",
                    Type = typeof(string),
                    DefaultValue = "Not connected",
                    DisplayName = "Connection status",
                    Description = "Current connection state. Updated automatically after connection actions.",
                    Enabled = false,
                },
            };
        }

        /// <inheritdoc/>
        public override bool ValidateSettings(Dictionary<string, object> settings)
        {
            Logger.For<SmartHopperCloudProviderSettings>().Trace($"[SmartHopperCloud] ValidateSettings called. Settings null? {settings == null}");
            if (settings == null)
            {
                return false;
            }

            settings.Remove("ConnectionStatus");
            settings.Remove("ApiKey");
            settings.Remove("AccessToken");
            settings.Remove("RefreshToken");
            settings.Remove("AccessTokenExpiresAt");

            var disconnect = settings.TryGetValue("Disconnect", out var disconnectObj)
                && bool.TryParse(disconnectObj?.ToString(), out var disconnectValue)
                && disconnectValue;
            if (disconnect)
            {
                settings["Disconnect"] = false;
                this.provider.Disconnect();
                settings.Remove("AccessToken");
                settings.Remove("RefreshToken");
                settings.Remove("AccessTokenExpiresAt");
                return true;
            }

            if (settings.TryGetValue("ServerUrl", out var serverUrlObj) && serverUrlObj != null)
            {
                var serverUrl = serverUrlObj.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(serverUrl))
                {
                    if (!SmartHopperCloudProvider.TryParseServerUrl(serverUrl, out _))
                    {
                        ProviderSdkHost.Diagnostics.Report(this.GetType().Name, new SHRuntimeMessage(SHRuntimeMessageSeverity.Error, SHRuntimeMessageOrigin.Validation, SHMessageCode.InputInvalid, SmartHopperCloudProvider.InvalidServerUrlMessage));
                        return false;
                    }
                }
            }

            if (settings.TryGetValue("OAuthCallbackPort", out var portObj) && portObj != null)
            {
                if (int.TryParse(portObj.ToString(), out var port))
                {
                    if (port < 1 || port > 65535)
                    {
                        ProviderSdkHost.Diagnostics.Report(this.GetType().Name, new SHRuntimeMessage(SHRuntimeMessageSeverity.Error, SHRuntimeMessageOrigin.Validation, SHMessageCode.InputInvalid, "OAuth callback port must be between 1 and 65535."));
                        return false;
                    }
                }
            }

            var login = settings.TryGetValue("Login", out var loginObj) && bool.TryParse(loginObj?.ToString(), out var loginValue) && loginValue;
            var refreshTeams = settings.TryGetValue("RefreshTeams", out var refreshObj) && bool.TryParse(refreshObj?.ToString(), out var refreshValue) && refreshValue;
            if (login || refreshTeams)
            {
                // Connection/discovery actions bootstrap selection and do not authorize generation.
                settings["Login"] = false;
                settings["RefreshTeams"] = false;
                try
                {
                    if (login)
                    {
                        var snapshot = this.provider.SnapshotCloudConnectionSettings(settings);
                        this.provider.StartCloudConnection(snapshot.ServerUrl, snapshot.CallbackPorts);
                    }
                    else
                    {
                        this.provider.SnapshotCloudConnectionSettings(settings);
                        this.provider.RefreshCachedSettings(settings);
                        this.provider.StartCloudTeamRefresh();
                    }
                }
                catch
                {
                    ProviderSdkHost.Diagnostics.Report(this.GetType().Name, new SHRuntimeMessage(
                        SHRuntimeMessageSeverity.Error,
                        SHRuntimeMessageOrigin.Provider,
                        SHMessageCode.Unknown,
                        "Connection or team discovery failed - retry"));
                }
                finally
                {
                    if (login)
                    {
                        // Login writes managed credentials through protected storage.
                        // Do not let pre-login form values overwrite a newly stored grant,
                        // even if subsequent discovery fails.
                        settings.Remove("AccessToken");
                        settings.Remove("RefreshToken");
                        settings.Remove("AccessTokenExpiresAt");
                    }
                }

                return true;
            }

            if (!settings.TryGetValue("TeamId", out var teamObj) || string.IsNullOrWhiteSpace(teamObj?.ToString()))
            {
                settings["TeamId"] = string.Empty;
                return true;
            }

            if (!SmartHopperCloudProvider.TryNormalizeTeamId(teamObj?.ToString(), out var teamId))
            {
                ProviderSdkHost.Diagnostics.Report(this.GetType().Name, new SHRuntimeMessage(SHRuntimeMessageSeverity.Error, SHRuntimeMessageOrigin.Validation, SHMessageCode.InputInvalid, "TeamId must be a non-empty UUID when a default team is selected."));
                return false;
            }

            settings["TeamId"] = teamId;
            return true;
        }
    }
}
