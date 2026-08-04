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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using SmartHopper.Infrastructure.AICall.Policies;
using SmartHopper.Infrastructure.AIContext;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.Infrastructure.Dialogs;
using SmartHopper.Infrastructure.Settings;
using SmartHopper.ProviderSdk.AICall.Core.Requests;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.AIProviders;
using SmartHopper.ProviderSdk.Diagnostics;
using SmartHopper.ProviderSdk.Hosting;
using SmartHopper.ProviderSdk.Settings;

namespace SmartHopper.Infrastructure.Hosting
{
    /// <summary>
    /// Host-side adapter wiring <see cref="ProviderManager"/> into the SDK's
    /// <see cref="IProviderRegistryHost"/> abstraction.
    /// </summary>
    public sealed class SmartHopperProviderRegistryHost : IProviderRegistryHost
    {
        /// <inheritdoc />
        public IAIProvider GetProvider(string providerName)
            => ProviderManager.Instance.GetProvider(providerName);

        /// <inheritdoc />
        public IAIProviderSettings GetProviderSettings(string providerName)
            => ProviderManager.Instance.GetProviderSettings(providerName);
    }

    /// <summary>
    /// Host-side adapter wiring the host's <see cref="PolicyPipeline"/> into the SDK's
    /// <see cref="IPolicyPipelineHost"/> abstraction.
    /// </summary>
    public sealed class SmartHopperPolicyPipelineHost : IPolicyPipelineHost
    {
        /// <inheritdoc />
        public async Task ApplyRequestPoliciesAsync(AIRequestCall request)
        {
            if (request == null)
            {
                return;
            }

            await PolicyPipeline.Default.ApplyRequestPoliciesAsync(request).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task ApplyResponsePoliciesAsync(AIReturn response)
        {
            if (response == null)
            {
                return;
            }

            await PolicyPipeline.Default.ApplyResponsePoliciesAsync(response).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Host-side adapter wiring <see cref="AIContextManager"/> into the SDK's
    /// <see cref="IContextProviderHost"/> abstraction.
    /// </summary>
    public sealed class SmartHopperContextProviderHost : IContextProviderHost
    {
        /// <inheritdoc />
        public IDictionary<string, string> GetCurrentContext(string providerFilter = null)
            => AIContextManager.GetCurrentContext(providerFilter);
    }

    /// <summary>
    /// Host-side adapter wiring <see cref="AIToolManager"/> into the SDK's
    /// <see cref="IToolRegistryHost"/> abstraction.
    /// </summary>
    public sealed class SmartHopperToolRegistryHost : IToolRegistryHost
    {
        /// <inheritdoc />
        public void DiscoverTools()
            => AIToolManager.DiscoverTools();

        /// <inheritdoc />
        public IReadOnlyDictionary<string, ProviderToolDefinition> GetTools()
        {
            var hostTools = AIToolManager.GetTools();
            var result = new Dictionary<string, ProviderToolDefinition>(hostTools.Count);
            foreach (var kvp in hostTools)
            {
                var tool = kvp.Value;
                if (tool == null)
                {
                    continue;
                }

                result[kvp.Key] = new ProviderToolDefinition
                {
                    Name = tool.Name,
                    Description = tool.Description,
                    Enabled = tool.Enabled,
                    Category = tool.Category,
                    ParametersSchema = tool.ParametersSchema,
                };
            }

            return result;
        }
    }

    /// <summary>
    /// Host-side diagnostics sink routing SDK runtime messages to the SmartHopper UI.
    /// </summary>
    public sealed class SmartHopperProviderDiagnosticsHost : IProviderDiagnostics
    {
        /// <inheritdoc />
        public void Report(string providerName, SHRuntimeMessage message)
        {
            if (message == null)
            {
                return;
            }

            if (message.Severity == SHRuntimeMessageSeverity.Error)
            {
                StyledMessageDialog.ShowError(message.Message, providerName ?? "SmartHopper");
            }
            else
            {
                Debug.WriteLine($"[{providerName}] {message.Message}");
            }
        }
    }

    /// <summary>
    /// Host-side logger routing SDK log output to Rhino's command-line window.
    /// </summary>
    public sealed class SmartHopperProviderLogger : IProviderLogger
    {
        /// <inheritdoc />
        public void Log(ProviderLogLevel level, string providerName, string message)
        {
            var line = $"[{level}][{providerName}] {message}";
            try
            {
                Rhino.RhinoApp.WriteLine(line);
            }
            catch
            {
                // RhinoApp may not be initialized in headless/test runs.
            }

            Debug.WriteLine(line);
        }

        /// <inheritdoc />
        public void LogException(string providerName, Exception exception, string context = null)
        {
            if (exception == null)
            {
                return;
            }

            var prefix = string.IsNullOrEmpty(context) ? string.Empty : context + ": ";
            var line = $"[Error][{providerName}] {prefix}{exception.GetType().Name}: {exception.Message}";
            try
            {
                Rhino.RhinoApp.WriteLine(line);
            }
            catch
            {
                // RhinoApp may not be initialized in headless/test runs.
            }

            Debug.WriteLine(line);
        }
    }

    /// <summary>
    /// Host-side <see cref="HttpClient"/> factory that reuses one client per provider
    /// while honoring per-request timeout overrides.
    /// </summary>
    public sealed class SmartHopperProviderHttpClientFactory : IProviderHttpClientFactory
    {
        private static readonly ConcurrentDictionary<string, HttpClient> Clients = new ConcurrentDictionary<string, HttpClient>();

        /// <inheritdoc />
        public HttpClient CreateClient(string providerName, TimeSpan timeout)
        {
            var name = string.IsNullOrEmpty(providerName) ? "default" : providerName;
            var client = Clients.GetOrAdd(name, _ =>
            {
                var c = new HttpClient();
                try
                {
                    c.DefaultRequestHeaders.Add("User-Agent", $"SmartHopper/{name}");
                }
                catch
                {
                    // Header may already exist or be restricted; ignore.
                }

                return c;
            });

            if (timeout > TimeSpan.Zero)
            {
                client.Timeout = timeout;
            }

            return client;
        }
    }

    /// <summary>
    /// Per-provider settings store backed by <see cref="SmartHopperSettings"/>.
    /// </summary>
    public sealed class SmartHopperProviderSettingsStore : IProviderSettingsStore
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SmartHopperProviderSettingsStore"/> class.
        /// </summary>
        /// <param name="providerName">The provider id this store is scoped to.</param>
        public SmartHopperProviderSettingsStore(string providerName)
        {
            this.ProviderName = string.IsNullOrEmpty(providerName) ? string.Empty : providerName;
        }

        /// <inheritdoc />
        public string ProviderName { get; }

        /// <inheritdoc />
        public IDictionary<string, object> GetAll()
            => SmartHopperSettings.Instance.GetProviderSettings(this.ProviderName);

        /// <inheritdoc />
        public void SetAll(IDictionary<string, object> settings)
        {
            if (settings == null)
            {
                return;
            }

            foreach (var kvp in settings)
            {
                SmartHopperSettings.Instance.SetSetting(this.ProviderName, kvp.Key, kvp.Value);
            }
        }

        /// <inheritdoc />
        public T Get<T>(string key, T defaultValue = default)
        {
            var value = SmartHopperSettings.Instance.GetSetting(this.ProviderName, key);
            if (value == null)
            {
                return defaultValue;
            }

            if (value is T typed)
            {
                return typed;
            }

            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <inheritdoc />
        public void Set<T>(string key, T value)
            => SmartHopperSettings.Instance.SetSetting(this.ProviderName, key, value);
    }

    /// <summary>
    /// Factory that creates per-provider <see cref="SmartHopperProviderSettingsStore"/> instances
    /// backed by <see cref="SmartHopperSettings"/>.
    /// </summary>
    public static class SmartHopperProviderSettingsStoreFactory
    {
        /// <summary>
        /// Creates a settings store scoped to <paramref name="providerName"/>.
        /// </summary>
        public static IProviderSettingsStore Create(string providerName)
            => new SmartHopperProviderSettingsStore(providerName);
    }
}
