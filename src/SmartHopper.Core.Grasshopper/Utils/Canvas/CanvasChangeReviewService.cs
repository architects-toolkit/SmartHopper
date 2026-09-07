/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024-2026 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using GhJSON.Core.SchemaModels;
using GhJSON.Grasshopper;

namespace SmartHopper.Core.Grasshopper.Utils.Canvas
{
    /// <summary>
    /// Describes a proposed connection or disconnection between two live canvas objects.
    /// </summary>
    public sealed class CanvasConnectionReviewProposal
    {
        /// <summary>Gets or sets the source object instance GUID.</summary>
        public Guid SourceGuid { get; set; }

        /// <summary>Gets or sets the target object instance GUID.</summary>
        public Guid TargetGuid { get; set; }

        /// <summary>Gets or sets the optional source parameter name.</summary>
        public string? SourceParameter { get; set; }

        /// <summary>Gets or sets the optional target parameter name.</summary>
        public string? TargetParameter { get; set; }
    }

    /// <summary>
    /// Creates and displays reusable review sessions for AI-proposed structural canvas changes.
    /// </summary>
    public static class CanvasChangeReviewService
    {
        /// <summary>
        /// Displays a review dialog on the Rhino UI thread.
        /// </summary>
        /// <param name="session">Session to review.</param>
        /// <returns><c>true</c> when the user chooses to apply selected changes.</returns>
        public static async Task<bool> ReviewAsync(CanvasChangeReviewSession session)
        {
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            global::Rhino.RhinoApp.InvokeOnUiThread(() =>
            {
                try
                {
                    completion.SetResult(CanvasChangeReviewDialog.ShowReview(session));
                }
                catch (Exception ex)
                {
                    completion.SetException(ex);
                }
            });
            return await completion.Task.ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a review session for removing live canvas objects.
        /// </summary>
        /// <param name="source">Tool that produced the proposal.</param>
        /// <param name="instanceGuids">Objects proposed for removal.</param>
        /// <returns>A session containing objects currently present on the canvas.</returns>
        public static CanvasChangeReviewSession CreateRemovalSession(string source, IEnumerable<Guid> instanceGuids)
        {
            var document = GhJsonGrasshopper.GetByGuids(instanceGuids);
            var items = document.Components
                .Where(component => component.InstanceGuid.HasValue)
                .Select(component => new CanvasChangeReviewItem(
                    $"remove:{component.InstanceGuid}",
                    CanvasChangeKind.ComponentRemoved,
                    component.NickName ?? component.Name ?? "Component",
                    component.Library ?? "Existing canvas object")
                {
                    ComponentId = component.Id,
                    ExistingInstanceGuid = component.InstanceGuid,
                })
                .ToList();
            if (document.Groups != null)
            {
                items.AddRange(document.Groups
                    .Select((group, index) => new CanvasChangeReviewItem(
                        $"remove:{group.InstanceGuid}",
                        CanvasChangeKind.GroupRemoved,
                        group.Name ?? "Group",
                        $"{group.Members.Count} member(s)")
                    {
                        ExistingInstanceGuid = group.InstanceGuid,
                        GroupIndex = index,
                    }));
            }
            return new CanvasChangeReviewSession(
                "Review AI canvas removals",
                source,
                document,
                items);
        }

        /// <summary>
        /// Creates a review session for proposed component moves.
        /// </summary>
        /// <param name="source">Tool that produced the proposal.</param>
        /// <param name="targets">Target positions or offsets by instance GUID.</param>
        /// <param name="relative">Whether targets are relative offsets.</param>
        /// <returns>A session containing proposed component pivots.</returns>
        public static CanvasChangeReviewSession CreateMoveSession(
            string source,
            IReadOnlyDictionary<Guid, PointF> targets,
            bool relative)
        {
            var document = GhJsonGrasshopper.GetByGuids(targets.Keys);
            var proposedComponents = document.Components.Select(component =>
            {
                if (!component.InstanceGuid.HasValue ||
                    !targets.TryGetValue(component.InstanceGuid.Value, out var target))
                {
                    return component;
                }

                var current = component.Pivot ?? new GhJsonPivot();
                return CopyWithPivot(component, new GhJsonPivot(
                    relative ? current.X + (int)Math.Round(target.X) : (int)Math.Round(target.X),
                    relative ? current.Y + (int)Math.Round(target.Y) : (int)Math.Round(target.Y)));
            }).ToList();
            var proposedDocument = new GhJsonDocument(
                document.Schema,
                document.Metadata,
                proposedComponents,
                document.Connections,
                document.Groups);
            var items = proposedComponents
                .Where(component => component.InstanceGuid.HasValue && targets.ContainsKey(component.InstanceGuid.Value))
                .Select(component => new CanvasChangeReviewItem(
                    $"move:{component.InstanceGuid}",
                    CanvasChangeKind.ComponentModified,
                    component.NickName ?? component.Name ?? "Component",
                    component.Pivot == null ? "Move component" : $"Move to {component.Pivot.X}, {component.Pivot.Y}")
                {
                    ComponentId = component.Id,
                    ExistingInstanceGuid = component.InstanceGuid,
                })
                .ToList();
            return new CanvasChangeReviewSession(
                "Review AI component moves",
                source,
                proposedDocument,
                items);
        }

        /// <summary>
        /// Gets accepted component instance GUIDs from a review session.
        /// </summary>
        /// <param name="session">Reviewed session.</param>
        /// <returns>Accepted component identifiers.</returns>
        public static IReadOnlySet<Guid> GetAcceptedComponentGuids(CanvasChangeReviewSession session)
        {
            return session.Items
                .Where(item => item.ExistingInstanceGuid.HasValue && session.IsEffectivelyAccepted(item))
                .Select(item => item.ExistingInstanceGuid!.Value)
                .ToHashSet();
        }

        /// <summary>
        /// Creates a review session for proposed live-object connections or disconnections.
        /// </summary>
        /// <param name="source">Tool that produced the proposal.</param>
        /// <param name="proposals">Connection proposals.</param>
        /// <param name="kind">Connection change category.</param>
        /// <returns>A session whose connection indexes align with <paramref name="proposals"/>.</returns>
        public static CanvasChangeReviewSession CreateConnectionSession(
            string source,
            IReadOnlyList<CanvasConnectionReviewProposal> proposals,
            CanvasChangeKind kind)
        {
            if (kind != CanvasChangeKind.ConnectionAdded && kind != CanvasChangeKind.ConnectionRemoved)
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            var document = GhJsonGrasshopper.GetByGuids(proposals
                .SelectMany(proposal => new[] { proposal.SourceGuid, proposal.TargetGuid })
                .Distinct());
            var idsByGuid = document.Components
                .Where(component => component.InstanceGuid.HasValue && component.Id.HasValue)
                .ToDictionary(component => component.InstanceGuid!.Value, component => component.Id!.Value);
            var connections = new List<GhJsonConnection>();
            var items = new List<CanvasChangeReviewItem>();
            for (var index = 0; index < proposals.Count; index++)
            {
                var proposal = proposals[index];
                if (!idsByGuid.TryGetValue(proposal.SourceGuid, out var sourceId) ||
                    !idsByGuid.TryGetValue(proposal.TargetGuid, out var targetId))
                {
                    continue;
                }

                var connectionIndex = connections.Count;
                connections.Add(new GhJsonConnection
                {
                    From = new GhJsonConnectionEndpoint
                    {
                        Id = sourceId,
                        ParamName = proposal.SourceParameter,
                    },
                    To = new GhJsonConnectionEndpoint
                    {
                        Id = targetId,
                        ParamName = proposal.TargetParameter,
                    },
                });
                var sourceComponent = document.Components.First(component => component.Id == sourceId);
                var targetComponent = document.Components.First(component => component.Id == targetId);
                items.Add(new CanvasChangeReviewItem(
                    $"connection:{index}",
                    kind,
                    $"{sourceComponent.NickName ?? sourceComponent.Name} → {targetComponent.NickName ?? targetComponent.Name}",
                    $"{proposal.SourceParameter ?? "first output"} → {proposal.TargetParameter ?? "first input"}")
                {
                    ComponentId = index,
                    ConnectionIndex = connectionIndex,
                });
            }

            var reviewDocument = new GhJsonDocument(
                document.Schema,
                document.Metadata,
                document.Components,
                connections,
                document.Groups);
            return new CanvasChangeReviewSession(
                kind == CanvasChangeKind.ConnectionAdded ? "Review AI connections" : "Review AI disconnections",
                source,
                reviewDocument,
                items);
        }

        /// <summary>
        /// Gets accepted proposal indexes from a connection review session.
        /// </summary>
        /// <param name="session">Reviewed connection session.</param>
        /// <returns>Accepted indexes into the caller's proposal list.</returns>
        public static IReadOnlySet<int> GetAcceptedConnectionProposalIndexes(CanvasChangeReviewSession session)
        {
            return session.Items
                .Where(item => item.ComponentId.HasValue && session.IsEffectivelyAccepted(item))
                .Select(item => item.ComponentId!.Value)
                .ToHashSet();
        }

        /// <summary>
        /// Gets accepted instance GUIDs from a component-removal session.
        /// </summary>
        /// <param name="session">Reviewed removal session.</param>
        /// <returns>Accepted live object identifiers.</returns>
        public static IReadOnlyList<Guid> GetAcceptedRemovalGuids(CanvasChangeReviewSession session)
        {
            return session.Items
                .Where(item =>
                    item.ExistingInstanceGuid.HasValue &&
                    session.IsEffectivelyAccepted(item))
                .Select(item => item.ExistingInstanceGuid!.Value)
                .ToList();
        }

        private static GhJsonComponent CopyWithPivot(GhJsonComponent component, GhJsonPivot pivot)
        {
            return new GhJsonComponent
            {
                Id = component.Id,
                InstanceGuid = component.InstanceGuid,
                ComponentGuid = component.ComponentGuid,
                Name = component.Name,
                NickName = component.NickName,
                Library = component.Library,
                Pivot = pivot,
                InputSettings = component.InputSettings,
                OutputSettings = component.OutputSettings,
                ComponentState = component.ComponentState,
                Errors = component.Errors,
                Warnings = component.Warnings,
                Remarks = component.Remarks,
            };
        }
    }
}
