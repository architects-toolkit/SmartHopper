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

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// A target endpoint inside a change plan: exactly one of
    /// <see cref="InstanceGuid"/> (live canvas object) or <see cref="Id"/>
    /// (a component inside this request's <c>put</c> fragment), plus optional
    /// port selectors used by connect/disconnect endpoints.
    /// </summary>
    public sealed class CanvasChangeTarget
    {
        /// <summary>Gets the live object identity, when present.</summary>
        public Guid? InstanceGuid { get; private set; }

        /// <summary>Gets the put-fragment component id, when present.</summary>
        public int? Id { get; private set; }

        /// <summary>Gets the optional port name selector.</summary>
        public string? ParamName { get; private set; }

        /// <summary>Gets the optional zero-based port index selector.</summary>
        public int? ParamIndex { get; private set; }

        /// <summary>Parses the closed target union; exactly one identity key is required.</summary>
        /// <param name="token">The JSON token to parse.</param>
        /// <param name="allowPortSelectors">Whether paramName/paramIndex keys are allowed.</param>
        /// <param name="target">The parsed target.</param>
        /// <param name="error">The rejection reason when parsing fails.</param>
        /// <returns>True on success.</returns>
        public static bool TryParse(JToken? token, bool allowPortSelectors, out CanvasChangeTarget? target, out string? error)
        {
            target = null;
            error = null;

            if (token is not JObject obj)
            {
                error = "target must be an object with exactly one of 'instanceGuid' or 'id'.";
                return false;
            }

            var allowed = allowPortSelectors
                ? new HashSet<string>(StringComparer.Ordinal) { "instanceGuid", "id", "paramName", "paramIndex" }
                : new HashSet<string>(StringComparer.Ordinal) { "instanceGuid", "id" };
            var unknown = obj.Properties().Select(p => p.Name).Where(name => !allowed.Contains(name)).ToList();
            if (unknown.Count > 0)
            {
                error = $"target contains unsupported field(s): {string.Join(", ", unknown)}.";
                return false;
            }

            var hasGuid = obj["instanceGuid"] != null;
            var hasId = obj["id"] != null;
            if (hasGuid == hasId)
            {
                error = "target must contain exactly one of 'instanceGuid' or 'id'.";
                return false;
            }

            var parsed = new CanvasChangeTarget();
            if (hasGuid)
            {
                if (!Guid.TryParse(obj["instanceGuid"]?.ToString(), out var guid))
                {
                    error = $"target 'instanceGuid' is not a valid GUID: '{obj["instanceGuid"]}'.";
                    return false;
                }

                parsed.InstanceGuid = guid;
            }
            else
            {
                var idToken = obj["id"];
                if (idToken?.Type != JTokenType.Integer)
                {
                    error = "target 'id' must be an integer component id from this request's put fragment.";
                    return false;
                }

                parsed.Id = idToken.Value<int>();
            }

            var paramNameToken = obj["paramName"];
            if (paramNameToken != null)
            {
                parsed.ParamName = paramNameToken.ToString();
            }

            var paramIndexToken = obj["paramIndex"];
            if (paramIndexToken != null)
            {
                if (paramIndexToken.Type != JTokenType.Integer || paramIndexToken.Value<int>() < 0)
                {
                    error = "target 'paramIndex' must be a non-negative integer.";
                    return false;
                }

                parsed.ParamIndex = paramIndexToken.Value<int>();
            }

            target = parsed;
            return true;
        }

        /// <summary>Identity signature used for conflict detection.</summary>
        /// <returns>A stable key for this target.</returns>
        public string IdentityKey()
        {
            return this.InstanceGuid.HasValue ? $"guid:{this.InstanceGuid}" : $"id:{this.Id}";
        }
    }
}
