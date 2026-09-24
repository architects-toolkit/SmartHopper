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
using Newtonsoft.Json;
using SmartHopper.Infrastructure.AICall.Sessions.SpecialTurns.BuiltIn;
using SmartHopper.Infrastructure.Settings;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AIModels;

namespace SmartHopper.Core.UI.Chat
{
    internal partial class WebChatDialog
    {
        /// <summary>
        /// Generates suggested follow-up prompts after a completed turn and renders them as
        /// chips above the input bar. Skipped silently when the setting is off, when the
        /// session model lacks <see cref="AICapability.Text2Json"/>, or when the model returns
        /// nothing parseable. The special turn is ephemeral: nothing enters history.
        /// </summary>
        private async Task GenerateSuggestedPromptsAsync()
        {
            try
            {
                var session = this._currentSession;
                var assistant = SmartHopperSettings.Instance?.SmartHopperAssistant;
                if (session == null || assistant?.EnableSuggestedPrompts != true)
                {
                    return;
                }

                // JSON output is required; non-JSON models skip generation entirely so the
                // feature never triggers modality fallback or a surprise model swap.
                var capabilities = AIModelCapabilityRegistry.Instance.GetCapabilities(session.Request.Provider, session.Request.Model);
                if (capabilities?.HasCapability(AICapability.Text2Json) != true)
                {
                    DebugLog("[WebChatDialog] Suggested prompts skipped: model lacks Text2Json capability");
                    return;
                }

                var config = SuggestedPromptsSpecialTurn.Create(session.GetHistoryInteractionList());
                var result = await session.ExecuteSpecialTurnAsync(config, preferStreaming: false).ConfigureAwait(false);

                var raw = result?.Body?.Interactions?
                    .OfType<AIInteractionText>()
                    .LastOrDefault(i => i.Agent == AIAgent.Assistant)?
                    .Content;

                var suggestions = SuggestedPromptsParser.Parse(raw);
                if (suggestions.Count == 0)
                {
                    return;
                }

                this.RunWhenWebViewReady(() =>
                {
                    this.ExecuteScript($"showSuggestedPrompts({JsonConvert.SerializeObject(suggestions)});");
                });
            }
            catch (Exception ex)
            {
                // Cosmetic feature: failures degrade to "no chips", never to a user-facing error.
                DebugLog($"[WebChatDialog] Suggested prompts generation failed: {ex.Message}");
            }
        }
    }
}
