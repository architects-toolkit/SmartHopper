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

namespace SmartHopper.ProviderSdk.AIModels
{
    /// <summary>
    /// The billing unit a <see cref="AIPriceEntry.Amount"/> is denominated in.
    /// Every price entry declares its unit explicitly — the usage-driven unit
    /// inference of the flat-field era is gone (<c>smarthopper-models/2</c>).
    /// Wire spellings are the lowercase names used by the catalogue protocol.
    /// </summary>
    public enum AIPriceUnit
    {
        /// <summary>Per single token.</summary>
        Token,

        /// <summary>Per second of media or compute time.</summary>
        Second,

        /// <summary>Per minute of media or compute time.</summary>
        Minute,

        /// <summary>Per single character (e.g. text-to-speech input).</summary>
        Character,

        /// <summary>Per generated item (image, song, video file).</summary>
        Item,

        /// <summary>Per megapixel of generated imagery.</summary>
        Megapixel,

        /// <summary>Per second of GPU/compute time.</summary>
        ComputeSecond,

        /// <summary>Per provider credit (e.g. Stability AI).</summary>
        Credit,

        /// <summary>Per invocation (requests, tool calls, searches).</summary>
        Invocation,
    }

    /// <summary>
    /// One priced billing bucket: <see cref="Kind"/> identifies what is billed
    /// (canonical kinds live in <c>AIQuantityKey</c>, open-ended
    /// <c>tool:&lt;name&gt;</c> kinds cover tool invocations),
    /// <see cref="Unit"/> declares the billing unit, and <see cref="Amount"/>
    /// is the price per single unit in the owning
    /// <see cref="AIModelPricing.Currency"/>.
    /// </summary>
    /// <param name="Kind">Billing kind, e.g. <c>text_input</c>, <c>audio_input</c>, <c>tool:web_search</c>.</param>
    /// <param name="Unit">Unit the amount is denominated in.</param>
    /// <param name="Amount">Price per single unit.</param>
    public sealed record AIPriceEntry(string Kind, AIPriceUnit Unit, decimal Amount)
    {
        /// <summary>Wire unit spellings, indexed by <see cref="AIPriceUnit"/>.</summary>
        private static readonly string[] WireUnits =
        {
            "token", "second", "minute", "character", "item",
            "megapixel", "compute_second", "credit", "invocation",
        };

        private static readonly Dictionary<string, AIPriceUnit> FromWireMap = BuildMap();

        private static Dictionary<string, AIPriceUnit> BuildMap()
        {
            var map = new Dictionary<string, AIPriceUnit>(StringComparer.Ordinal);
            var values = (AIPriceUnit[])Enum.GetValues(typeof(AIPriceUnit));
            for (var i = 0; i < WireUnits.Length && i < values.Length; i++)
            {
                map[WireUnits[i]] = values[i];
            }

            return map;
        }

        /// <summary>Converts a unit to its catalogue wire spelling.</summary>
        public static string ToWire(AIPriceUnit unit) => WireUnits[(int)unit];

        /// <summary>Parses a catalogue wire unit spelling.</summary>
        /// <returns>The unit, or <c>null</c> when the spelling is unknown.</returns>
        public static AIPriceUnit? ParseUnit(string wire)
        {
            if (wire != null && FromWireMap.TryGetValue(wire, out var unit))
            {
                return unit;
            }

            return null;
        }
    }
}
