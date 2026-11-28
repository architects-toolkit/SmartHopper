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
using GhJSON.Core;
using GhJSON.Core.FixOperations;
using GhJSON.Core.SchemaModels;

namespace SmartHopper.Core.Grasshopper.Utils.Parsing
{
    /// <summary>
    /// Keeps AI-generated definitions independent of canvas coordinates and canvas
    /// identities. Imported documents must not pass through this generation-only
    /// policy — imports preserve pivots, component GUIDs and instance GUIDs.
    /// </summary>
    public static class GhJsonGenerationPolicy
    {
        /// <summary>
        /// Shared placement instruction for definition-generating prompts.
        /// </summary>
        public const string Instructions = "NEVER emit pivot fields in AI-generated GhJSON. Omit component positions entirely so placement uses the compact automatic layout. Do not invent coordinates or copy example pivots.";

        /// <summary>
        /// The generation-safe fixer option set: assign missing integer ids, fix
        /// metadata counts and normalize enum casing — but never generate instance
        /// GUIDs, never drop connections/group members and never renumber supplied
        /// ids. Dangling references are reported by normalization, not silently
        /// removed.
        /// </summary>
        private static readonly FixOptions GenerationFixOptions = new ()
        {
            AssignMissingIds = true,
            ReassignIds = false,
            GenerateMissingInstanceGuids = false,
            RegenerateInstanceGuids = false,
            RemoveInvalidConnections = false,
            RemoveInvalidGroupMembers = false,
            FixMetadata = true,
            NormalizeEnumCasing = true,
        };

        /// <summary>
        /// Removes model-supplied positions from a newly generated definition.
        /// </summary>
        /// <param name="document">Generated document, never an imported or existing definition.</param>
        public static void RemovePivots(GhJsonDocument document)
        {
            ArgumentNullException.ThrowIfNull(document);
            foreach (var component in document.Components)
            {
                component.Pivot = null;
            }
        }

        /// <summary>
        /// Outcome of <see cref="NormalizeGenerated"/>: the normalized document plus
        /// the precise validation errors found while normalizing. When
        /// <see cref="Success"/> is false the errors feed the bounded generation
        /// retry loop instead of dropping or repairing content silently.
        /// </summary>
        public sealed class NormalizationResult
        {
            internal NormalizationResult(GhJsonDocument document, List<string> errors)
            {
                this.Document = document;
                this.Errors = errors;
            }

            /// <summary>Gets the normalized document (the input document when errors exist).</summary>
            public GhJsonDocument Document { get; }

            /// <summary>Gets the validation errors collected during normalization.</summary>
            public IReadOnlyList<string> Errors { get; }

            /// <summary>Gets a value indicating whether normalization produced a valid generated document.</summary>
            public bool Success => this.Errors.Count == 0;
        }

        /// <summary>
        /// Normalizes an AI-generated document to the generation contract: components
        /// are identified by <c>name</c> plus an integer <c>id</c> only. Strips
        /// component <c>componentGuid</c>/<c>instanceGuid</c>, group
        /// <c>instanceGuid</c> and pivots; validates that every connection and group
        /// reference targets a supplied integer id; then assigns missing ids,
        /// repairs metadata counts and normalizes enum casing through a fixer option
        /// set that can never inject GUIDs or drop content.
        /// </summary>
        /// <param name="document">Generated document, never an imported or existing definition.</param>
        /// <param name="resolveComponentTypeName">
        /// Optional host resolver mapping a component type GUID to its canonical
        /// type name. Used only when a component supplies a <c>componentGuid</c>
        /// without a name; the resolved name replaces the GUID. When null, a
        /// GUID-only component is reported as an error.
        /// </param>
        /// <returns>The normalized document and any errors found.</returns>
        public static NormalizationResult NormalizeGenerated(
            GhJsonDocument document,
            Func<Guid, string?>? resolveComponentTypeName = null)
        {
            ArgumentNullException.ThrowIfNull(document);
            var errors = new List<string>();
            var components = document.Components ?? new List<GhJsonComponent>();

            // 1. Type identities: strip every GUID — generated components are
            // identified by name plus integer id only. A GUID outranks the name in
            // the placement resolver, so a hallucinated GUID would silently place a
            // different component than the name intends.
            for (var i = 0; i < components.Count; i++)
            {
                var component = components[i];
                var label = $"component #{i + 1} ('{component.Name ?? component.ComponentGuid?.ToString() ?? "?"}')";
                component.InstanceGuid = null;

                if (component.ComponentGuid.HasValue)
                {
                    var guid = component.ComponentGuid.Value;
                    if (guid != Guid.Empty && string.IsNullOrWhiteSpace(component.Name))
                    {
                        var resolved = resolveComponentTypeName?.Invoke(guid);
                        if (!string.IsNullOrWhiteSpace(resolved))
                        {
                            component.Name = resolved;
                        }
                        else
                        {
                            errors.Add($"{label}: componentGuid '{guid}' does not resolve to an available component type; supply a component name instead.");
                        }
                    }

                    component.ComponentGuid = null;
                }

                if (string.IsNullOrWhiteSpace(component.Name))
                {
                    errors.Add($"{label}: missing component name; generated components are identified by name plus integer id.");
                }
            }

            // 2. Group identities.
            if (document.Groups != null)
            {
                foreach (var group in document.Groups)
                {
                    group.InstanceGuid = null;
                }
            }

            // 3. Supplied integer ids must be unique — ambiguous references must not
            // be silently renumbered.
            var suppliedIds = new HashSet<int>();
            for (var i = 0; i < components.Count; i++)
            {
                var component = components[i];
                if (component.Id.HasValue && !suppliedIds.Add(component.Id.Value))
                {
                    errors.Add($"Duplicate component id {component.Id.Value} ('{component.Name}'); generated ids must be unique.");
                }
            }

            // 4. References must target supplied ids — unresolved targets are errors,
            // never silently dropped wires or group members.
            if (document.Connections != null)
            {
                for (var i = 0; i < document.Connections.Count; i++)
                {
                    var connection = document.Connections[i];
                    var label = $"connection #{i + 1}";
                    ValidateEndpoint(connection?.From, label, "from", suppliedIds, errors);
                    ValidateEndpoint(connection?.To, label, "to", suppliedIds, errors);
                }
            }

            if (document.Groups != null)
            {
                for (var i = 0; i < document.Groups.Count; i++)
                {
                    var group = document.Groups[i];
                    foreach (var member in group?.Members ?? Enumerable.Empty<int>())
                    {
                        if (!suppliedIds.Contains(member))
                        {
                            errors.Add($"group #{i + 1} ('{group?.Name}'): member id {member} does not match any generated component id.");
                        }
                    }
                }
            }

            // 5. Bounded repair with the generation-safe option set, then the
            // generation-only coordinate policy.
            var fixedDocument = errors.Count == 0
                ? GhJson.Fix(document, GenerationFixOptions).Document
                : document;
            RemovePivots(fixedDocument);

            return new NormalizationResult(fixedDocument, errors);
        }

        private static void ValidateEndpoint(
            GhJsonConnectionEndpoint? endpoint,
            string connectionLabel,
            string direction,
            HashSet<int> suppliedIds,
            List<string> errors)
        {
            if (endpoint == null)
            {
                errors.Add($"{connectionLabel}: missing '{direction}' endpoint.");
                return;
            }

            if (!suppliedIds.Contains(endpoint.Id))
            {
                errors.Add($"{connectionLabel}: '{direction}' endpoint references component id {endpoint.Id}, which is not a generated component id.");
            }

            if (endpoint.ParamIndex.HasValue && endpoint.ParamIndex.Value < 0)
            {
                errors.Add($"{connectionLabel}: '{direction}' endpoint has negative paramIndex {endpoint.ParamIndex.Value}.");
            }
        }
    }
}
