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
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using SmartHopper.Infrastructure.AICall.Logging;
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
using SmartHopper.ProviderSdk.Logging;
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
                    Description = tool.RichDescription,
                    Enabled = tool.Enabled,
                    Surfaces = tool.Surfaces,
                    Category = tool.Category,
                    ParametersSchema = tool.ParametersSchema,
                    ActivityLabel = tool.ActivityLabel,
                    ResultLabel = tool.ResultLabel,
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

            Logger.For<SmartHopperProviderDiagnosticsHost>().Trace($"[{providerName}] {message.Message}");
            if (message.Severity != SHRuntimeMessageSeverity.Error)
            {
                return;
            }

            // Provider diagnostics also arrive from background workers: Eto/WPF
            // windows must be created on the UI (STA) thread, otherwise their
            // constructor throws and the original report is lost. Marshal the
            // dialog instead of showing it inline.
            global::Rhino.RhinoApp.InvokeOnUiThread(new Action(() =>
                StyledMessageDialog.ShowError(message.Message, providerName ?? "SmartHopper")));
        }
    }

    /// <summary>
    /// Host-side logger that routes SDK log entries to a set of <see cref="ILogSink"/> sinks.
    /// Uses a shared <see cref="ILogFormatter"/> so live and file output are formatted identically.
    /// Caches per-sink levels and refreshes them whenever <see cref="SmartHopperSettings"/> is saved.
    /// </summary>
    public sealed class SmartHopperLogger : ILogger, IDisposable
    {
        private readonly ILogFormatter _formatter;
        private readonly List<ILogSink> _sinks;
        private SmartHopperLogRoutingState _routingState;

        /// <summary>
        /// Initializes a new instance of the <see cref="SmartHopperLogger"/> class
        /// and subscribes to settings changes.
        /// </summary>
        public SmartHopperLogger()
        {
            this._formatter = new SmartHopperLogFormatter();
            this._sinks = new List<ILogSink>
            {
                new SmartHopperLiveSink(this._formatter),
                new SmartHopperLogFileSink(this._formatter),
            };

            this._routingState = this.BuildRoutingState();

            try
            {
                SmartHopperSettings.Instance.SettingsSaved += this.OnSettingsSaved;
            }
            catch
            {
                // Settings may not be available during some test setups.
            }
        }

        /// <inheritdoc />
        public bool IsEnabled(ProviderLogLevel level)
        {
            var state = this._routingState;
            foreach (var sink in state.Sinks)
            {
                if (sink.IsEnabled && level >= sink.MinLevel)
                {
                    return true;
                }
            }

            return false;
        }

        /// <inheritdoc />
        public IDisposable BeginScope(IReadOnlyDictionary<string, object> state)
            => LogScopeContext.BeginScope(state);

        /// <inheritdoc />
        public void Log(ProviderLogLevel level, string category, string message)
        {
            var state = this._routingState;

            var anySink = false;
            foreach (var sink in state.Sinks)
            {
                if (sink.IsEnabled && level >= sink.MinLevel)
                {
                    anySink = true;
                    break;
                }
            }

            if (!anySink)
            {
                return;
            }

            var entry = new SmartHopperLogEntry(
                DateTimeOffset.UtcNow,
                level,
                category,
                message,
                LogScopeContext.Current?.Flatten());

            foreach (var sink in state.Sinks)
            {
                if (!sink.IsEnabled || level < sink.MinLevel)
                {
                    continue;
                }

                try
                {
                    sink.Sink.Write(entry);
                }
                catch
                {
                    // A failing sink must not break the other sinks.
                }
            }
        }

        /// <inheritdoc />
        public void LogException(string category, Exception exception, string? context = null)
        {
            if (exception == null)
            {
                return;
            }

            if (!this.IsEnabled(ProviderLogLevel.Error))
            {
                return;
            }

            var message = SmartHopperLogExceptionFormatter.FormatException(exception, context);

            // Attach the exception details to the entry scope so sinks that emit
            // structured output can carry error.type / exception.stacktrace
            // alongside the message. The stack is stripped of file paths before
            // it can reach disk.
            var exceptionScope = new Dictionary<string, object>
            {
                ["exception.type"] = exception.GetType().Name,
                ["exception.stacktrace"] = SmartHopperLogExceptionFormatter.NormalizeStackTrace(exception.StackTrace) ?? string.Empty,
            };

            using (LogScopeContext.BeginScope(exceptionScope))
            {
                this.Log(ProviderLogLevel.Error, category, message);
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            try
            {
                SmartHopperSettings.Instance.SettingsSaved -= this.OnSettingsSaved;
            }
            catch
            {
                // Best-effort unsubscribe.
            }

            foreach (var sink in this._sinks)
            {
                try
                {
                    sink.Flush();
                    sink.Dispose();
                }
                catch
                {
                    // Best-effort cleanup.
                }
            }
        }

        private void OnSettingsSaved(object? sender, EventArgs e)
        {
            this._routingState = this.BuildRoutingState();
        }

        private SmartHopperLogRoutingState BuildRoutingState()
        {
            var settings = this.GetSettings();

            var states = new List<SmartHopperLogSinkState>(this._sinks.Count);
            foreach (var sink in this._sinks)
            {
                var (enabled, minLevel) = sink.Name switch
                {
                    "Live" => (settings?.LiveOutputEnabled ?? true, settings?.LiveOutputLevel ?? ProviderLogLevel.Info),
                    "File" => (settings?.LogFileEnabled ?? true, settings?.LogFileLevel ?? ProviderLogLevel.Trace),
                    _ => (true, ProviderLogLevel.Trace),
                };

                states.Add(new SmartHopperLogSinkState(sink, enabled, minLevel));
            }

            return new SmartHopperLogRoutingState(states);
        }

        private SmartHopperSettings? GetSettings()
        {
            try
            {
                return SmartHopperSettings.Instance;
            }
            catch
            {
                return null;
            }
        }

        private sealed class SmartHopperLogRoutingState
        {
            public SmartHopperLogRoutingState(IReadOnlyList<SmartHopperLogSinkState> sinks)
            {
                this.Sinks = sinks;
            }

            public IReadOnlyList<SmartHopperLogSinkState> Sinks { get; }
        }

        private sealed class SmartHopperLogSinkState
        {
            public SmartHopperLogSinkState(ILogSink sink, bool isEnabled, ProviderLogLevel minLevel)
            {
                this.Sink = sink;
                this.IsEnabled = isEnabled;
                this.MinLevel = minLevel;
            }

            public ILogSink Sink { get; }

            public bool IsEnabled { get; }

            public ProviderLogLevel MinLevel { get; }
        }
    }

    /// <summary>
    /// Host-side <see cref="HttpClient"/> factory that reuses one <see cref="HttpMessageHandler"/>
    /// per provider while returning a fresh <see cref="HttpClient"/> on each call.
    /// Callers own the returned client and should dispose it; the shared handler is pooled.
    /// </summary>
    public sealed class SmartHopperProviderHttpClientFactory : IProviderHttpClientFactory
    {
        private static readonly ConcurrentDictionary<string, HttpMessageHandler> Handlers = new ConcurrentDictionary<string, HttpMessageHandler>();

        /// <inheritdoc />
        public HttpClient CreateClient(string providerName, TimeSpan timeout)
        {
            var name = string.IsNullOrEmpty(providerName) ? "default" : providerName;
            var handler = Handlers.GetOrAdd(name, _ => new HttpClientHandler());
            var client = new HttpClient(handler, disposeHandler: false);

            if (timeout > TimeSpan.Zero)
            {
                client.Timeout = timeout;
            }

            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", $"SmartHopper/{name}");

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
