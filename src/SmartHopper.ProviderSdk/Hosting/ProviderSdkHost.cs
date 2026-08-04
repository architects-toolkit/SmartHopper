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

namespace SmartHopper.ProviderSdk.Hosting
{
    /// <summary>
    /// Static composition root the SmartHopper host uses at startup to inject
    /// host-side services into SDK code paths. Default null/no-op implementations are
    /// provided so SDK code remains executable in unit tests and stand-alone tools.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The host registers concrete implementations exactly once during plugin
    /// initialization; SDK code reads the configured property each time it needs a
    /// service so registration order does not matter.
    /// </para>
    /// <para>
    /// This is a runtime extension point only — provider authors must not depend on
    /// specific host implementations or rely on host registrations happening before
    /// provider activation.
    /// </para>
    /// </remarks>
    public static class ProviderSdkHost
    {
        /// <summary>
        /// Per-provider trust/integrity status surface used by request validation.
        /// </summary>
        public static IProviderTrustHost ProviderTrust { get; set; } = new NullProviderTrustHost();

        /// <summary>
        /// Provider lookup surface used by DTOs and the AIProvider base class.
        /// </summary>
        public static IProviderRegistryHost ProviderRegistry { get; set; } = new NullProviderRegistryHost();

        /// <summary>
        /// Host policy pipeline invoked by SDK execution paths around request/response.
        /// </summary>
        public static IPolicyPipelineHost PolicyPipeline { get; set; } = new NullPolicyPipelineHost();

        /// <summary>
        /// Context bag provider used by SDK system-prompt assembly.
        /// </summary>
        public static IContextProviderHost ContextProvider { get; set; } = new NullContextProviderHost();

        /// <summary>
        /// Tool registry surface used when an SDK request formats tools for an LLM.
        /// </summary>
        public static IToolRegistryHost ToolRegistry { get; set; } = new NullToolRegistryHost();

        /// <summary>
        /// Diagnostics sink for user-visible runtime messages emitted from SDK code.
        /// </summary>
        public static IProviderDiagnostics Diagnostics { get; set; } = new NullProviderDiagnostics();

        /// <summary>
        /// Logger used by the AIProvider base class and SDK utilities.
        /// </summary>
        public static IProviderLogger Logger { get; set; } = new DebugProviderLogger();

        /// <summary>
        /// HTTP client factory used by the AIProvider base class. The host installs a
        /// pooled factory at startup; tests can swap a mock factory here.
        /// </summary>
        public static IProviderHttpClientFactory HttpClientFactory { get; set; } = new DefaultProviderHttpClientFactory();

        /// <summary>
        /// Factory used by the AIProvider base class to obtain a per-provider settings
        /// store scoped to a single provider id. The default returns an in-memory store
        /// so tests can exercise SDK paths without a host attached; the SmartHopper host
        /// replaces this with a real, persistence-backed factory at startup.
        /// </summary>
        public static System.Func<string, IProviderSettingsStore> SettingsStoreFactory { get; set; }
            = providerName => new InMemoryProviderSettingsStore(providerName);
    }
}
