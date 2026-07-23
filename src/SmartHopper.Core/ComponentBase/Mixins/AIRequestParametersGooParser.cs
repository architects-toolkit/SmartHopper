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

using Grasshopper.Kernel.Types;
using SmartHopper.ProviderSdk.AICall.Core;

namespace SmartHopper.Core.ComponentBase.Mixins
{
    /// <summary>
    /// Single source of truth for converting a Grasshopper goo wire into an
    /// <see cref="AIRequestParameters"/> instance.
    /// </summary>
    /// <remarks>
    /// Lives in <c>SmartHopper.Core</c> rather than alongside
    /// <see cref="AIRequestParameters"/> itself because <c>SmartHopper.Infrastructure</c>
    /// has no Grasshopper dependency. The behaviour matches the previous inline
    /// type-sniffing in <c>AIStatefulAsyncComponentBase.SolveInstance</c>:
    /// <list type="number">
    ///   <item>If the goo unwraps to an <see cref="AIRequestParameters"/>, return it as-is.</item>
    ///   <item>If it unwraps to a non-empty <see cref="string"/>, treat it as a model name.</item>
    ///   <item>Otherwise fall back to <see cref="object.ToString"/> and treat that as a model name.</item>
    ///   <item>If the goo is null/invalid, yield <see cref="AIRequestParameters.Empty"/>.</item>
    /// </list>
    /// </remarks>
    public static class AIRequestParametersGooParser
    {
        /// <summary>
        /// Attempts to materialise an <see cref="AIRequestParameters"/> from a
        /// Grasshopper goo wire.
        /// </summary>
        /// <param name="goo">The goo to parse. May be null.</param>
        /// <param name="parameters">
        /// Receives the parsed parameters. Always non-null on return — falls
        /// back to <see cref="AIRequestParameters.Empty"/> when nothing usable
        /// can be extracted.
        /// </param>
        /// <returns>
        /// <c>true</c> when the goo carried explicit parameter or model data;
        /// <c>false</c> when <paramref name="goo"/> was null/invalid and
        /// <paramref name="parameters"/> is the empty fallback.
        /// </returns>
        public static bool TryFromGoo(IGH_Goo goo, out AIRequestParameters parameters)
        {
            if (goo == null)
            {
                parameters = AIRequestParameters.Empty;
                return false;
            }

            var scriptVar = goo.ScriptVariable();
            if (scriptVar is AIRequestParameters typed)
            {
                parameters = typed;
                return true;
            }

            if (scriptVar is string s && !string.IsNullOrWhiteSpace(s))
            {
                parameters = AIRequestParameters.FromModel(s.Trim());
                return true;
            }

            // Last-ditch: many goo wrappers (e.g. GH_String) need ToString() to surface text.
            var fallback = goo.ToString();
            if (!string.IsNullOrWhiteSpace(fallback))
            {
                parameters = AIRequestParameters.FromModel(fallback.Trim());
                return true;
            }

            parameters = AIRequestParameters.Empty;
            return false;
        }
    }
}
