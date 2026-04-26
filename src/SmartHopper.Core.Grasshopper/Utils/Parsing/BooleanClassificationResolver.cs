/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024-2026 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

namespace SmartHopper.Core.Grasshopper.Utils.Parsing
{
    /// <summary>
    /// Centralized resolver for one-shot boolean classification, where the AI is expected to
    /// reply with a single affirmative/negative token (e.g. "true"/"false", "yes"/"no", "1"/"0").
    /// </summary>
    /// <remarks>
    /// Differs from <see cref="BooleanResultResolver"/> in that classification has no concept of
    /// a user-supplied fallback: an unparseable response collapses to a configurable default
    /// (typically <c>false</c>) without surfacing a "usedFallback" flag.
    /// Internally delegates to <see cref="AIResponseParser.ParseBooleanFromResponse"/>.
    /// </remarks>
    public static class BooleanClassificationResolver
    {
        /// <summary>
        /// Classifies AI response text as <c>true</c>, <c>false</c>, or the supplied default
        /// when the response cannot be parsed.
        /// </summary>
        /// <param name="aiResponseText">The raw assistant response text.</param>
        /// <param name="defaultWhenUnparseable">Value to return when the response is empty or ambiguous. Defaults to <c>false</c>.</param>
        public static bool Classify(string aiResponseText, bool defaultWhenUnparseable = false)
        {
            return ClassifyWithFallback(aiResponseText, defaultWhenUnparseable).Value;
        }

        /// <summary>
        /// Classifies AI response text as <c>true</c> or <c>false</c>, additionally reporting
        /// whether the supplied fallback was applied because the response was unparseable.
        /// Mirrors the contract of <see cref="BooleanResultResolver.Resolve"/>.
        /// </summary>
        /// <param name="aiResponseText">The raw assistant response text.</param>
        /// <param name="fallback">Value to return when the response is empty or ambiguous. Defaults to <c>false</c>.</param>
        /// <returns>A tuple containing the resolved value and whether the fallback was applied.</returns>
        public static (bool Value, bool UsedFallback) ClassifyWithFallback(string aiResponseText, bool fallback = false)
        {
            var parsed = AIResponseParser.ParseBooleanFromResponse(aiResponseText);
            return parsed.HasValue ? (parsed.Value, false) : (fallback, true);
        }
    }
}
