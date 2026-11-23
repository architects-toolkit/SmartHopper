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
using System.Linq;
using System.Threading.Tasks;
using Rhino;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Infrastructure.Dialogs;
using SmartHopper.Infrastructure.Settings;
using SmartHopper.ProviderSdk.AIModels;
using SmartHopper.ProviderSdk.Logging;
using SmartHopper.ProviderSdk.Settings;

namespace SmartHopper.Core.UI.Chat
{
    /// <summary>
    /// Utility functions for the AI Chat component.
    /// </summary>
    public static partial class WebChatUtils
    {
        /// <summary>
        /// Predefined system prompt for the SmartHopper canvas assistant.
        /// </summary>
        private const string AssistantSystemPrompt = """
            Act as SmartHopper's general-purpose canvas assistant. Help with parametric design, algorithmic problem-solving, definition quality, debugging, scripting, and performance. Explain complex concepts clearly and retrieve focused instruction topics before specialized work.
            """;

        /// <summary>
        /// Opens the assistant web chat dialog. When <paramref name="dialogId"/> already
        /// identifies an open dialog it is reused (brought to front); otherwise a new
        /// independent dialog is created, allowing multiple parallel conversations.
        /// </summary>
        /// <param name="dialogId">
        /// Dialog identity. Pass a stable GUID to reuse a single dialog across invocations,
        /// or <see cref="Guid.NewGuid"/> to always open a new independent chat.
        /// </param>
        /// <param name="origin">
        /// Semantic origin label stored on the request (e.g. "canvas-chat"); it labels
        /// the call in logs and provider attribution.
        /// </param>
        /// <returns>A task that completes when the chat dialog closes.</returns>
        public static async Task OpenAssistantChatDialogAsync(Guid dialogId, string origin = "canvas-chat")
        {
            try
            {
                DebugLog($"[WebChatUtils] Opening assistant chat dialog '{dialogId}' (origin '{origin}')");

                // Resolve provider and model from SmartHopperAssistant settings with fallbacks
                var providerManager = ProviderManager.Instance;
                var assistant = SmartHopperSettings.Instance?.SmartHopperAssistant;

                var requestedProviderName = assistant?.AssistantProvider;

                // Treat "(Default)" as request to use configured default provider
                var isDefaultProviderSelected = string.Equals(requestedProviderName, "(Default)", StringComparison.Ordinal);
                var providerNameToUse = (!string.IsNullOrWhiteSpace(requestedProviderName) && !isDefaultProviderSelected)
                    ? requestedProviderName
                    : providerManager.GetDefaultAIProvider();

                var provider = providerManager.GetProvider(providerNameToUse);

                // If a requested provider is not available, fall back to the default provider
                if (provider == null)
                {
                    var fallbackProviderName = providerManager.GetDefaultAIProvider();
                    if (!string.Equals(fallbackProviderName, providerNameToUse, StringComparison.OrdinalIgnoreCase))
                    {
                        provider = providerManager.GetProvider(fallbackProviderName);
                    }
                }

                if (provider == null)
                {
                    DebugLog("[WebChatUtils] No AI provider available");
                    ReportAssistantNoProvider(providerManager);
                    return;
                }

                var providerName = provider.Name;
                var requestedModel = assistant?.AssistantModel;

                // If user selected (Default) provider, always use provider default model
                var model = isDefaultProviderSelected
                    ? provider.GetDefaultModel(AICapability.Text2Text)
                    : (!string.IsNullOrWhiteSpace(requestedModel)
                        ? requestedModel
                        : provider.GetDefaultModel(AICapability.Text2Text));

                DebugLog($"[WebChatUtils] Using provider: {providerName}, model: {model}");

                // Create and process the web chat worker
                var greetingEnabled = assistant?.EnableAIGreeting == true;
                var chatWorker = CreateWebChatWorker(
                    providerName,
                    model,
                    origin: origin,
                    systemPrompt: AssistantSystemPrompt,
                    toolFilter: "Canvas,GhJSON,Knowledge,AgentPlanning,Scripting,AIProviders",
                    componentId: dialogId,
                    progressReporter: null,
                    onUpdate: null,
                    generateGreeting: greetingEnabled);

                await chatWorker.ProcessChatAsync(default).ConfigureAwait(false);

                DebugLog($"[WebChatUtils] Assistant chat dialog '{dialogId}' completed");
            }
            catch (Exception ex)
            {
                Logger.Exception(typeof(WebChatUtils).FullName, ex, $"[WebChatUtils] Error opening assistant chat dialog: {ex.Message}");
            }
        }

        /// <summary>
        /// Surfaces why no provider could be resolved: providers blocked by the
        /// integrity check get an actionable path to Settings (Security tab);
        /// other causes get the generic configuration error. The styled dialogs
        /// self-marshal to Rhino's UI thread, and the settings deep-link is posted
        /// so the whole path is safe from this background task.
        /// </summary>
        /// <param name="providerManager">The provider manager holding the last integrity findings.</param>
        private static void ReportAssistantNoProvider(ProviderManager providerManager)
        {
            var findings = providerManager.LastIntegrityFindings;
            var blocked = (findings?.Count(f => f.Severity == ProviderIntegrityIssueSeverity.Error) ?? 0)
                + providerManager.CountDeniedByIntegrityMode();

            if (blocked > 0 && SettingsLauncher.IsAvailable)
            {
                var openSettings = StyledMessageDialog.ShowConfirmation(
                    $"No usable AI provider was found — {blocked} provider{(blocked == 1 ? " is" : "s are")} blocked by the integrity check ({SmartHopperSettings.Instance?.ProviderIntegrityCheckMode} mode). " +
                    "Blocked providers are not loaded for your security. Open SmartHopper Settings on the Security tab to review trust and integrity options?",
                    "SmartHopper Assistant");
                if (openSettings)
                {
                    RhinoApp.InvokeOnUiThread(() => SettingsLauncher.Open(SettingsLauncher.SecurityTab));
                }
            }
            else
            {
                StyledMessageDialog.ShowError(
                    "No available AI provider was found. Please check the SmartHopper settings to ensure that at least one AI provider is both configured and enabled.",
                    "SmartHopper Assistant");
            }
        }
    }
}
