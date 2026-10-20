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
using System.Globalization;
using System.Linq;
using System.Text;
using GhJSON.Core;
using GhJSON.Core.DiffOperations;
using GhJSON.Core.PatchModels;
using GhJSON.Core.SchemaModels;
using GhJSON.Core.Serialization;

namespace SmartHopper.Core.Grasshopper.Utils.Canvas
{
    /// <summary>
    /// Builds the deterministic "canvas changed since the last AI response" text from two GhJSON snapshots.
    /// Pure GhJSON.Core logic (no Rhino/Grasshopper runtime) so it can be unit tested.
    /// </summary>
    public static class CanvasChangeSummary
    {
        /// <summary>
        /// Default maximum length (in characters) of a compact GhPatch that is inlined in the summary.
        /// </summary>
        public const int DefaultMaxInlinePatchLength = 4000;

        /// <summary>
        /// Maximum number of component GUIDs listed when the patch is too large to inline.
        /// </summary>
        public const int MaxListedGuids = 50;

        /// <summary>
        /// Creates the diff options used for chat tracking: pivots, runtime messages and metadata are noise.
        /// </summary>
        /// <returns>The diff options.</returns>
        public static DiffOptions CreateDiffOptions()
        {
            return new DiffOptions
            {
                IgnoreRuntimeMessages = true,
                IgnoreMetadataCounters = true,
                IgnoreMetadataTimestamps = true,
                IgnorePivots = true,
                IncludeBaseChecksum = false,
            };
        }

        /// <summary>
        /// Diffs <paramref name="baseline"/> against <paramref name="current"/> and builds the summary text.
        /// </summary>
        /// <param name="baseline">Snapshot taken at the last AI response.</param>
        /// <param name="current">Current snapshot.</param>
        /// <param name="maxInlinePatchLength">Maximum compact patch length to inline; larger patches are replaced by a <c>gh_get</c> hint.</param>
        /// <returns>The Markdown summary, or null when there are no structural changes.</returns>
        public static string? Build(GhJsonDocument baseline, GhJsonDocument current, int maxInlinePatchLength = DefaultMaxInlinePatchLength)
        {
            if (baseline == null || current == null)
            {
                return null;
            }

            // GhJson.Diff strips instance GUIDs from added components in the right-hand document,
            // so diff a copy and keep the caller's snapshot intact for GUID lookups.
            var currentCopy = GhJson.FromJson(GhJson.ToJson(current, new WriteOptions { Indented = false }));
            var diff = GhJson.Diff(baseline, currentCopy, CreateDiffOptions());
            var counts = CanvasChangeCounts.FromPatch(diff.Patch);
            if (!counts.HasChanges)
            {
                return null;
            }

            // Metadata is noise for chat tracking; never ship it in the patch.
            diff.Patch.Patch.Metadata = null;

            var sb = new StringBuilder();
            sb.Append(Describe(counts));

            var patchJson = GhJson.PatchToJson(diff.Patch, new WriteOptions { Indented = false });
            if (patchJson.Length <= maxInlinePatchLength)
            {
                sb.Append("\n\n```json\n").Append(patchJson).Append("\n```");
            }
            else
            {
                sb.Append("\n\n")
                  .Append(string.Format(CultureInfo.InvariantCulture, "The GhPatch is too large to include ({0} characters). ", patchJson.Length))
                  .Append(BuildRetrievalHint(diff.Patch, baseline, current));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Builds the deterministic sentence describing the change counts.
        /// </summary>
        /// <param name="counts">The change counts.</param>
        /// <returns>The sentence.</returns>
        public static string Describe(CanvasChangeCounts counts)
        {
            var parts = new List<string>
            {
                Count(counts.ComponentsAdded, "component", "components") + " added",
                counts.ComponentsRemoved.ToString(CultureInfo.InvariantCulture) + " removed",
                counts.ComponentsModified.ToString(CultureInfo.InvariantCulture) + " modified",
                Count(counts.InputModifiersChanged, "input modifier", "input modifiers") + " changed",
                Count(counts.OutputModifiersChanged, "output modifier", "output modifiers") + " changed",
            };

            var sentence = new StringBuilder("Canvas changed since the last AI response: ");
            sentence.Append(string.Join(", ", parts));
            sentence.Append("; ")
                .Append(Count(counts.ConnectionsAdded, "connection", "connections")).Append(" added, ")
                .Append(counts.ConnectionsRemoved.ToString(CultureInfo.InvariantCulture)).Append(" removed");

            var groupChanges = counts.GroupsAdded + counts.GroupsRemoved + counts.GroupsModified;
            if (groupChanges > 0)
            {
                sentence.Append("; ")
                    .Append(Count(counts.GroupsAdded, "group", "groups")).Append(" added, ")
                    .Append(counts.GroupsRemoved.ToString(CultureInfo.InvariantCulture)).Append(" removed, ")
                    .Append(counts.GroupsModified.ToString(CultureInfo.InvariantCulture)).Append(" modified");
            }

            sentence.Append('.');
            return sentence.ToString();
        }

        private static string BuildRetrievalHint(GhPatchDocument patch, GhJsonDocument baseline, GhJsonDocument current)
        {
            var body = patch.Patch;
            var currentById = IndexById(current);
            var guids = new List<Guid>();

            void AddGuid(Guid? guid)
            {
                if (guid.HasValue && guid.Value != Guid.Empty && !guids.Contains(guid.Value))
                {
                    guids.Add(guid.Value);
                }
            }

            foreach (var added in body.Components?.Add ?? Enumerable.Empty<GhJsonComponent>())
            {
                // Patches omit instance GUIDs of added components; resolve them from the current snapshot.
                var guid = added.InstanceGuid;
                if (!guid.HasValue && added.Id.HasValue && currentById.TryGetValue(added.Id.Value, out var addedComponent))
                {
                    guid = addedComponent.InstanceGuid;
                }

                AddGuid(guid);
            }

            foreach (var modified in body.Components?.Modify ?? Enumerable.Empty<GhPatchComponentModify>())
            {
                AddGuid(modified.Match?.InstanceGuid);
            }

            var connections = (body.Connections?.Add ?? Enumerable.Empty<GhJsonConnection>())
                .Concat(body.Connections?.Remove ?? Enumerable.Empty<GhJsonConnection>());
            foreach (var connection in connections)
            {
                foreach (var endpointId in new[] { connection.From.Id, connection.To.Id })
                {
                    if (currentById.TryGetValue(endpointId, out var component))
                    {
                        AddGuid(component.InstanceGuid);
                    }
                }
            }

            var sb = new StringBuilder();
            if (guids.Count > 0)
            {
                sb.Append("Call `gh_get` with `instanceGuids` set to the affected components to retrieve the latest changes: ");
                sb.Append(string.Join(", ", guids.Take(MaxListedGuids).Select(g => "`" + g.ToString("D") + "`")));
                if (guids.Count > MaxListedGuids)
                {
                    sb.Append(string.Format(CultureInfo.InvariantCulture, " (+{0} more; call `gh_get` without filters to retrieve the whole canvas)", guids.Count - MaxListedGuids));
                }

                sb.Append('.');
            }
            else
            {
                sb.Append("Call `gh_get` to retrieve the latest changes.");
            }

            var removed = (body.Components?.Remove ?? Enumerable.Empty<GhPatchComponentMatch>()).ToList();
            if (removed.Count > 0)
            {
                var baselineByGuid = baseline.Components
                    .Where(c => c.InstanceGuid.HasValue)
                    .GroupBy(c => c.InstanceGuid!.Value)
                    .ToDictionary(g => g.Key, g => g.First());

                var labels = removed.Take(MaxListedGuids).Select(match =>
                {
                    var name = match.Name;
                    if (string.IsNullOrWhiteSpace(name) && match.InstanceGuid.HasValue && baselineByGuid.TryGetValue(match.InstanceGuid.Value, out var original))
                    {
                        name = original.Name;
                    }

                    var guid = match.InstanceGuid.HasValue ? match.InstanceGuid.Value.ToString("D") : "unknown";
                    return string.IsNullOrWhiteSpace(name) ? "`" + guid + "`" : name + " (`" + guid + "`)";
                });

                sb.Append(" Removed components: ").Append(string.Join(", ", labels));
                if (removed.Count > MaxListedGuids)
                {
                    sb.Append(string.Format(CultureInfo.InvariantCulture, " (+{0} more)", removed.Count - MaxListedGuids));
                }

                sb.Append('.');
            }

            return sb.ToString();
        }

        private static Dictionary<int, GhJsonComponent> IndexById(GhJsonDocument document)
        {
            var result = new Dictionary<int, GhJsonComponent>();
            foreach (var component in document.Components)
            {
                if (component.Id.HasValue && !result.ContainsKey(component.Id.Value))
                {
                    result[component.Id.Value] = component;
                }
            }

            return result;
        }

        private static string Count(int value, string singular, string plural)
        {
            return value.ToString(CultureInfo.InvariantCulture) + " " + (value == 1 ? singular : plural);
        }
    }

    /// <summary>
    /// Deterministic change counts extracted from a GhPatch.
    /// </summary>
    public sealed class CanvasChangeCounts
    {
        /// <summary>Gets the number of added components.</summary>
        public int ComponentsAdded { get; private set; }

        /// <summary>Gets the number of removed components.</summary>
        public int ComponentsRemoved { get; private set; }

        /// <summary>Gets the number of modified components.</summary>
        public int ComponentsModified { get; private set; }

        /// <summary>Gets the number of input parameters whose settings (modifiers, data mapping, expressions, internalized data) changed.</summary>
        public int InputModifiersChanged { get; private set; }

        /// <summary>Gets the number of output parameters whose settings changed.</summary>
        public int OutputModifiersChanged { get; private set; }

        /// <summary>Gets the number of added connections.</summary>
        public int ConnectionsAdded { get; private set; }

        /// <summary>Gets the number of removed connections. Rewired connections appear as one removal plus one addition.</summary>
        public int ConnectionsRemoved { get; private set; }

        /// <summary>Gets the number of added groups.</summary>
        public int GroupsAdded { get; private set; }

        /// <summary>Gets the number of removed groups.</summary>
        public int GroupsRemoved { get; private set; }

        /// <summary>Gets the number of modified groups.</summary>
        public int GroupsModified { get; private set; }

        /// <summary>
        /// Gets a value indicating whether any structural change was found (metadata is ignored).
        /// </summary>
        public bool HasChanges =>
            this.ComponentsAdded + this.ComponentsRemoved + this.ComponentsModified +
            this.ConnectionsAdded + this.ConnectionsRemoved +
            this.GroupsAdded + this.GroupsRemoved + this.GroupsModified > 0;

        /// <summary>
        /// Extracts counts from a GhPatch document.
        /// </summary>
        /// <param name="patch">The patch.</param>
        /// <returns>The counts.</returns>
        public static CanvasChangeCounts FromPatch(GhPatchDocument? patch)
        {
            var body = patch?.Patch;
            var modify = body?.Components?.Modify ?? new List<GhPatchComponentModify>();
            return new CanvasChangeCounts
            {
                ComponentsAdded = body?.Components?.Add?.Count ?? 0,
                ComponentsRemoved = body?.Components?.Remove?.Count ?? 0,
                ComponentsModified = modify.Count,
                InputModifiersChanged = modify.Sum(m => m.InputSettings?.ByParameterName?.Count ?? 0),
                OutputModifiersChanged = modify.Sum(m => m.OutputSettings?.ByParameterName?.Count ?? 0),
                ConnectionsAdded = body?.Connections?.Add?.Count ?? 0,
                ConnectionsRemoved = body?.Connections?.Remove?.Count ?? 0,
                GroupsAdded = body?.Groups?.Add?.Count ?? 0,
                GroupsRemoved = body?.Groups?.Remove?.Count ?? 0,
                GroupsModified = body?.Groups?.Modify?.Count ?? 0,
            };
        }
    }
}
