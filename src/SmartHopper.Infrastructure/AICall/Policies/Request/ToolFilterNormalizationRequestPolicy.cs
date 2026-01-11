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
 * along with this library; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.Utils;

namespace SmartHopper.Infrastructure.AICall.Policies.Request
{
    /// <summary>
    /// Normalizes and canonicalizes the ToolFilter string in the request body prior to provider encoding.
    /// Non-intrusive: preserves semantics and does not inject interactions/messages.
    /// Canonical forms:
    ///  - "-*" for exclude-all
    ///  - "*" when include-all and no excludes
    ///  - "* -a -b" when include-all with explicit excludes (sorted, case-insensitive)
    ///  - "a,b -c -d" when explicit includes with optional excludes (both sorted, includes comma-separated)
    /// </summary>
    public sealed class ToolFilterNormalizationRequestPolicy : IRequestPolicy
    {
        public Task ApplyAsync(PolicyContext context)
        {
            var rq = context?.Request;
            if (rq?.Body == null)
            {
                return Task.CompletedTask;
            }

            var raw = rq.Body.ToolFilter;
            var normalized = Canonicalize(raw);

            if (!string.Equals(raw, normalized, StringComparison.Ordinal))
            {
                var builder = AIBodyBuilder.FromImmutable(rq.Body)
                    .WithToolFilter(normalized);

                // Attach a light diagnostic to the request body for traceability
                // Use AIInteractionError so it can be surfaced in UI but skipped by providers during encoding
                if (string.IsNullOrWhiteSpace(raw))
                {
                    builder = builder.AddError($"Tool filter was empty; interpreted as '{normalized}'.");
                }
                else
                {
                    builder = builder.AddError($"Tool filter normalized from '{raw}' to '{normalized}'.");
                }

                rq.Body = builder.Build();
            }

            return Task.CompletedTask;
        }

        private static string Canonicalize(string raw)
        {
            var f = Filtering.Parse(raw);

            // Exclude all
            if (f.ExcludeAll)
            {
                return "-*";
            }

            var excludes = (f.ExcludeSet ?? new HashSet<string>())
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var includes = (f.IncludeSet ?? new HashSet<string>())
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Include all
            if (f.IncludeAll)
            {
                if (excludes.Count == 0)
                {
                    return "*";
                }

                var ex = string.Join(" ", excludes.Select(x => $"-{x}"));
                return $"* {ex}";
            }

            // Explicit includes
            var inc = string.Join(",", includes);
            if (string.IsNullOrEmpty(inc))
            {
                // Safety fallback: treat as include-all
                if (excludes.Count == 0)
                {
                    return "*";
                }

                var ex = string.Join(" ", excludes.Select(x => $"-{x}"));
                return $"* {ex}";
            }

            if (excludes.Count == 0)
            {
                return inc;
            }

            return inc + " " + string.Join(" ", excludes.Select(x => $"-{x}"));
        }
    }
}
