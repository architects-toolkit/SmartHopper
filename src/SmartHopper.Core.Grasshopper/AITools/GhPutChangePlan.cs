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
using GhJSON.Core.SchemaModels;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Utils.Canvas;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Builds a selectable, non-mutating review plan for a <c>gh_put</c> proposal.
    /// </summary>
    public sealed class GhPutChangePlan
    {
        private GhPutChangePlan(
            GhJsonDocument proposedDocument,
            CanvasChangeReviewSession session)
        {
            this.ProposedDocument = proposedDocument;
            this.Session = session;
        }

        /// <summary>Gets the original proposed document.</summary>
        public GhJsonDocument ProposedDocument { get; }

        /// <summary>Gets the user-review session.</summary>
        public CanvasChangeReviewSession Session { get; }

        /// <summary>
        /// Creates a review plan by comparing proposed components with their live serialized state.
        /// </summary>
        /// <param name="proposedDocument">Incoming GhJSON proposal.</param>
        /// <param name="existingDocument">Serialized live components matched by instance GUID.</param>
        /// <returns>The review plan.</returns>
        public static GhPutChangePlan Create(GhJsonDocument proposedDocument, GhJsonDocument existingDocument)
        {
            if (proposedDocument == null)
            {
                throw new ArgumentNullException(nameof(proposedDocument));
            }

            var existingByGuid = (existingDocument?.Components ?? Array.Empty<GhJsonComponent>())
                .Where(component => component.InstanceGuid.HasValue)
                .ToDictionary(component => component.InstanceGuid!.Value);
            var items = new List<CanvasChangeReviewItem>();
            var componentKeysById = new Dictionary<int, string>();
            var unchangedIds = new HashSet<int>();

            foreach (var component in proposedDocument.Components)
            {
                var key = $"component:{component.Id?.ToString() ?? component.InstanceGuid?.ToString() ?? items.Count.ToString()}";
                if (component.Id.HasValue)
                {
                    componentKeysById[component.Id.Value] = key;
                }

                if (component.InstanceGuid.HasValue &&
                    existingByGuid.TryGetValue(component.InstanceGuid.Value, out var existing))
                {
                    if (ComponentsAreEqual(component, existing))
                    {
                        if (component.Id.HasValue)
                        {
                            unchangedIds.Add(component.Id.Value);
                        }

                        continue;
                    }

                    items.Add(new CanvasChangeReviewItem(
                        key,
                        CanvasChangeKind.ComponentModified,
                        DisplayName(component),
                        DescribeChangedFields(component, existing))
                    {
                        ComponentId = component.Id,
                        ExistingInstanceGuid = component.InstanceGuid,
                    });
                    continue;
                }

                items.Add(new CanvasChangeReviewItem(
                    key,
                    CanvasChangeKind.ComponentAdded,
                    DisplayName(component),
                    component.Library ?? "New component")
                {
                    ComponentId = component.Id,
                });
            }

            if (proposedDocument.Connections != null)
            {
                for (var index = 0; index < proposedDocument.Connections.Count; index++)
                {
                    var connection = proposedDocument.Connections[index];
                    var item = new CanvasChangeReviewItem(
                        $"connection:{index}",
                        CanvasChangeKind.ConnectionAdded,
                        DescribeConnection(connection, proposedDocument),
                        DescribeConnectionParameters(connection))
                    {
                        ConnectionIndex = index,
                    };
                    AddDependency(item, connection.From.Id, componentKeysById, unchangedIds);
                    AddDependency(item, connection.To.Id, componentKeysById, unchangedIds);
                    items.Add(item);
                }
            }

            if (proposedDocument.Groups != null)
            {
                for (var index = 0; index < proposedDocument.Groups.Count; index++)
                {
                    var group = proposedDocument.Groups[index];
                    var item = new CanvasChangeReviewItem(
                        $"group:{index}",
                        CanvasChangeKind.GroupAdded,
                        string.IsNullOrWhiteSpace(group.Name) ? "Group" : group.Name!,
                        $"{group.Members.Count} member(s)")
                    {
                        GroupIndex = index,
                    };
                    items.Add(item);
                }
            }

            var previewDocument = proposedDocument.Components.Any(component => component.Pivot != null)
                ? proposedDocument
                : GhJson.ReorganizePivots(proposedDocument);
            var session = new CanvasChangeReviewSession(
                "Review AI canvas changes",
                "gh_put",
                previewDocument,
                items);
            return new GhPutChangePlan(proposedDocument, session);
        }

        /// <summary>
        /// Gets accepted connections that cannot be created by placing the filtered fragment because at least one endpoint is an unchanged live component.
        /// </summary>
        /// <param name="acceptedDocument">Filtered document returned by <see cref="BuildAcceptedDocument"/>.</param>
        /// <returns>Accepted connections requiring post-placement connection.</returns>
        public IReadOnlyList<GhJsonConnection> GetAcceptedExternalConnections(GhJsonDocument acceptedDocument)
        {
            var placedIds = acceptedDocument.Components
                .Where(component => component.Id.HasValue)
                .Select(component => component.Id!.Value)
                .ToHashSet();
            var acceptedIndexes = this.Session.Items
                .Where(item => item.ConnectionIndex.HasValue && this.Session.IsEffectivelyAccepted(item))
                .Select(item => item.ConnectionIndex!.Value)
                .ToHashSet();
            return this.ProposedDocument.Connections?
                .Select((connection, index) => new { Connection = connection, Index = index })
                .Where(entry =>
                    acceptedIndexes.Contains(entry.Index) &&
                    (!placedIds.Contains(entry.Connection.From.Id) || !placedIds.Contains(entry.Connection.To.Id)))
                .Select(entry => entry.Connection)
                .ToList() ?? new List<GhJsonConnection>();
        }

        /// <summary>
        /// Resolves a proposed component ID to its instance GUID when the proposal references a live component.
        /// </summary>
        /// <param name="componentId">Proposed component ID.</param>
        /// <returns>The live instance GUID, or <c>null</c>.</returns>
        public Guid? GetExistingInstanceGuid(int componentId)
        {
            return this.ProposedDocument.Components.FirstOrDefault(component => component.Id == componentId)?.InstanceGuid;
        }

        /// <summary>
        /// Builds the GhJSON fragment containing only effectively accepted changes.
        /// </summary>
        /// <returns>A filtered GhJSON document.</returns>
        public GhJsonDocument BuildAcceptedDocument()
        {
            var acceptedComponents = this.Session.Items
                .Where(item => item.ComponentId.HasValue && this.Session.IsEffectivelyAccepted(item))
                .Select(item => item.ComponentId!.Value)
                .ToHashSet();
            var acceptedConnections = this.Session.Items
                .Where(item => item.ConnectionIndex.HasValue && this.Session.IsEffectivelyAccepted(item))
                .Select(item => item.ConnectionIndex!.Value)
                .ToHashSet();
            var acceptedGroups = this.Session.Items
                .Where(item => item.GroupIndex.HasValue && this.Session.IsEffectivelyAccepted(item))
                .Select(item => item.GroupIndex!.Value)
                .ToHashSet();

            var components = this.ProposedDocument.Components
                .Where(component => component.Id.HasValue && acceptedComponents.Contains(component.Id.Value))
                .ToList();
            var retainedIds = components
                .Where(component => component.Id.HasValue)
                .Select(component => component.Id!.Value)
                .ToHashSet();
            var connections = this.ProposedDocument.Connections?
                .Select((connection, index) => new { Connection = connection, Index = index })
                .Where(entry =>
                    acceptedConnections.Contains(entry.Index) &&
                    retainedIds.Contains(entry.Connection.From.Id) &&
                    retainedIds.Contains(entry.Connection.To.Id))
                .Select(entry => entry.Connection)
                .ToList();
            var groups = this.ProposedDocument.Groups?
                .Select((group, index) => new { Group = group, Index = index })
                .Where(entry => acceptedGroups.Contains(entry.Index))
                .Select(entry => CopyGroup(entry.Group, retainedIds))
                .Where(group => group.Members.Count > 0)
                .ToList();

            return new GhJsonDocument(
                this.ProposedDocument.Schema,
                this.ProposedDocument.Metadata,
                components,
                connections,
                groups);
        }

        private static void AddDependency(
            CanvasChangeReviewItem item,
            int componentId,
            IReadOnlyDictionary<int, string> componentKeysById,
            ISet<int> unchangedIds)
        {
            if (!unchangedIds.Contains(componentId) &&
                componentKeysById.TryGetValue(componentId, out var key) &&
                !item.RequiredItemKeys.Contains(key))
            {
                item.RequiredItemKeys.Add(key);
            }
        }

        private static GhJsonGroup CopyGroup(GhJsonGroup group, ISet<int> retainedIds)
        {
            return new GhJsonGroup
            {
                Id = group.Id,
                InstanceGuid = group.InstanceGuid,
                Name = group.Name,
                Color = group.Color,
                Members = group.Members.Where(retainedIds.Contains).ToList(),
            };
        }

        private static string DescribeChangedFields(GhJsonComponent proposed, GhJsonComponent existing)
        {
            var proposedObject = Normalize(proposed);
            var existingObject = Normalize(existing);
            var changed = proposedObject.Properties()
                .Select(property => property.Name)
                .Union(existingObject.Properties().Select(property => property.Name))
                .Where(name => !JToken.DeepEquals(proposedObject[name], existingObject[name]))
                .ToList();
            return changed.Count == 0 ? "Component state will change" : $"Changed: {string.Join(", ", changed)}";
        }

        private static string DescribeConnection(GhJsonConnection connection, GhJsonDocument document)
        {
            var from = document.Components.FirstOrDefault(component => component.Id == connection.From.Id);
            var to = document.Components.FirstOrDefault(component => component.Id == connection.To.Id);
            return $"{DisplayName(from)} → {DisplayName(to)}";
        }

        private static string DescribeConnectionParameters(GhJsonConnection connection)
        {
            var from = connection.From.ParamName ?? connection.From.ParamIndex?.ToString() ?? "output";
            var to = connection.To.ParamName ?? connection.To.ParamIndex?.ToString() ?? "input";
            return $"{from} → {to}";
        }

        private static string DisplayName(GhJsonComponent? component)
        {
            if (component == null)
            {
                return "Unknown component";
            }

            return !string.IsNullOrWhiteSpace(component.NickName) ? component.NickName! : component.Name ?? "Component";
        }

        private static bool ComponentsAreEqual(GhJsonComponent proposed, GhJsonComponent existing)
        {
            return JToken.DeepEquals(Normalize(proposed), Normalize(existing));
        }

        private static JObject Normalize(GhJsonComponent component)
        {
            var value = JObject.FromObject(component);
            value.Remove("id");
            value.Remove("errors");
            value.Remove("warnings");
            value.Remove("remarks");

            if (value["componentState"] is JObject state)
            {
                state.Remove("selected");
            }

            foreach (var settingsKey in new[] { "inputSettings", "outputSettings" })
            {
                if (value[settingsKey] is not JArray settings)
                {
                    continue;
                }

                foreach (var setting in settings.OfType<JObject>())
                {
                    setting.Remove("runtimeData");
                }
            }

            return value;
        }
    }
}
