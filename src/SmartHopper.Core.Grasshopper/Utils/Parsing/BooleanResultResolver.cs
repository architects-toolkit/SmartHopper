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
using System.Linq;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;

namespace SmartHopper.Core.Grasshopper.Utils.Parsing
{
    /// <summary>
    /// Centralized resolver for the lenient-parse-then-fallback pipeline shared between
    /// the <c>text2boolean</c> / <c>textlist2boolean</c> AI tools (non-batch path) and
    /// their corresponding components' <c>OnBatchCompleted</c> overrides (batch path).
    /// </summary>
    /// <remarks>
    /// Single source of truth for the contract:
    /// <list type="bullet">
    /// <item>Parse the AI response with <see cref="AIResponseParser.ParseBooleanFromResponse"/> (lenient).</item>
    /// <item>If parsing succeeds, return that value with <c>usedFallback = false</c>.</item>
    /// <item>If parsing fails, return the user fallback (or <c>null</c> if none) with <c>usedFallback = true</c>.</item>
    /// </list>
    /// </remarks>
    public static class BooleanResultResolver
    {
        /// <summary>
        /// Resolves a boolean result from raw AI response text and an optional user fallback.
        /// </summary>
        /// <param name="aiResponseText">The raw assistant response text.</param>
        /// <param name="fallback">Optional fallback to apply when the response cannot be parsed.</param>
        /// <returns>A tuple containing the resolved boolean (or <c>null</c>) and whether the fallback was applied.</returns>
        public static (bool? Value, bool UsedFallback) Resolve(string aiResponseText, bool? fallback)
        {
            var parsed = AIResponseParser.ParseBooleanFromResponse(aiResponseText);
            if (parsed.HasValue)
            {
                return (parsed.Value, false);
            }

            return (fallback, true);
        }

        /// <summary>
        /// Resolves a boolean result by decoding a provider response body and extracting
        /// the last assistant text. Used by the batch path where the tool's execute body
        /// never runs.
        /// </summary>
        /// <param name="resultBody">The raw provider response payload.</param>
        /// <param name="decode">The provider's <c>Decode</c> delegate.</param>
        /// <param name="fallback">Optional fallback to apply when the response cannot be parsed.</param>
        /// <returns>A tuple containing the resolved boolean (or <c>null</c>) and whether the fallback was applied.</returns>
        public static (bool? Value, bool UsedFallback) ResolveFromBody(
            JObject resultBody,
            Func<JObject, List<IAIInteraction>> decode,
            bool? fallback)
        {
            if (resultBody == null || decode == null)
            {
                return (fallback, true);
            }

            var interactions = decode(resultBody);
            var lastText = interactions
                ?.OfType<AIInteractionText>()
                .LastOrDefault(i => i.Agent == AIAgent.Assistant);

            if (lastText == null)
            {
                return (fallback, true);
            }

            return Resolve(lastText.Content, fallback);
        }

        /// <summary>
        /// Builds the standard <c>{ "result", "usedFallback" }</c> JObject emitted by the
        /// <c>text2boolean</c> / <c>textlist2boolean</c> tools.
        /// </summary>
        /// <param name="aiResponseText">The raw assistant response text.</param>
        /// <param name="fallback">Optional fallback to apply when the response cannot be parsed.</param>
        /// <returns>JObject with <c>result</c> (bool or null) and <c>usedFallback</c> (bool).</returns>
        public static JObject BuildToolResult(string aiResponseText, bool? fallback)
        {
            var (value, usedFallback) = Resolve(aiResponseText, fallback);
            var toolResult = new JObject();
            if (value.HasValue)
            {
                toolResult.Add("result", value.Value);
            }
            else
            {
                toolResult.Add("result", null);
            }

            toolResult.Add("usedFallback", usedFallback);
            return toolResult;
        }
    }
}
