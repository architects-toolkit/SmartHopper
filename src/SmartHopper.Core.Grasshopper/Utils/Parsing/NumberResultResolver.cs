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
    /// Centralized resolver for AI text → <see cref="double"/> with optional fallback.
    /// Mirrors the contract of <see cref="BooleanResultResolver"/>.
    /// </summary>
    public static class NumberResultResolver
    {
        /// <summary>
        /// Resolves a number from raw AI response text and an optional fallback.
        /// </summary>
        /// <param name="aiResponseText">The raw assistant response text.</param>
        /// <param name="fallback">Optional fallback to apply when the response cannot be parsed.</param>
        /// <returns>A tuple containing the resolved number (or <c>null</c>) and whether the fallback was applied.</returns>
        public static (double? Value, bool UsedFallback) Resolve(string aiResponseText, double? fallback)
        {
            var parsed = AIResponseParser.ParseNumberFromResponse(aiResponseText);
            if (parsed.HasValue)
            {
                return (parsed.Value, false);
            }

            return (fallback, true);
        }

        /// <summary>
        /// Decodes a provider response body, extracts the last assistant text, and resolves a number.
        /// </summary>
        public static (double? Value, bool UsedFallback) ResolveFromBody(
            JObject resultBody,
            Func<JObject, List<IAIInteraction>> decode,
            double? fallback)
        {
            if (resultBody == null || decode == null)
            {
                return (fallback, true);
            }

            var lastText = decode(resultBody)
                ?.OfType<AIInteractionText>()
                .LastOrDefault(i => i.Agent == AIAgent.Assistant);

            if (lastText == null)
            {
                return (fallback, true);
            }

            return Resolve(lastText.Content, fallback);
        }

        /// <summary>
        /// Builds a <c>{ "number", "usedFallback" }</c> tool result JObject.
        /// </summary>
        public static JObject BuildToolResult(string aiResponseText, double? fallback)
        {
            var (value, usedFallback) = Resolve(aiResponseText, fallback);
            var toolResult = new JObject();
            toolResult.Add("number", value.HasValue ? (JToken)value.Value : JValue.CreateNull());
            toolResult.Add("usedFallback", usedFallback);
            return toolResult;
        }
    }
}
