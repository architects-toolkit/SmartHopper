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
using System.Linq;
using GhJSON.Core.SchemaModels;

namespace SmartHopper.Core.Grasshopper.Utils.Canvas
{
    /// <summary>
    /// Identifies the visual category of a staged canvas change.
    /// </summary>
    public enum CanvasChangeKind
    {
        /// <summary>A component will be added.</summary>
        ComponentAdded,

        /// <summary>An existing component will be replaced with modified state.</summary>
        ComponentModified,

        /// <summary>A component will be removed.</summary>
        ComponentRemoved,

        /// <summary>A connection will be added.</summary>
        ConnectionAdded,

        /// <summary>A connection will be removed.</summary>
        ConnectionRemoved,

        /// <summary>A group will be added.</summary>
        GroupAdded,

        /// <summary>An existing group will be modified.</summary>
        GroupModified,

        /// <summary>A group will be removed.</summary>
        GroupRemoved,
    }

    /// <summary>
    /// Represents one independently selectable change in a staged canvas proposal.
    /// </summary>
    public sealed class CanvasChangeReviewItem
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CanvasChangeReviewItem"/> class.
        /// </summary>
        /// <param name="key">Stable key within the proposal.</param>
        /// <param name="kind">Change category.</param>
        /// <param name="title">Short user-facing title.</param>
        /// <param name="detail">Optional user-facing detail.</param>
        public CanvasChangeReviewItem(string key, CanvasChangeKind kind, string title, string detail = "")
        {
            this.Key = key ?? throw new ArgumentNullException(nameof(key));
            this.Kind = kind;
            this.Title = title ?? throw new ArgumentNullException(nameof(title));
            this.Detail = detail ?? string.Empty;
        }

        /// <summary>Gets the stable key within the proposal.</summary>
        public string Key { get; }

        /// <summary>Gets the visual change category.</summary>
        public CanvasChangeKind Kind { get; }

        /// <summary>Gets the short user-facing title.</summary>
        public string Title { get; }

        /// <summary>Gets the optional user-facing detail.</summary>
        public string Detail { get; }

        /// <summary>Gets or sets whether this change should be applied.</summary>
        public bool IsAccepted { get; set; } = true;

        /// <summary>Gets or sets the affected live component instance GUID.</summary>
        public Guid? ExistingInstanceGuid { get; set; }

        /// <summary>Gets or sets the proposed component ID.</summary>
        public int? ComponentId { get; set; }

        /// <summary>Gets or sets the index of the proposed connection.</summary>
        public int? ConnectionIndex { get; set; }

        /// <summary>Gets or sets the index of the proposed group.</summary>
        public int? GroupIndex { get; set; }

        /// <summary>Gets keys of component changes required by this change.</summary>
        public IList<string> RequiredItemKeys { get; } = new List<string>();
    }

    /// <summary>
    /// Holds an immutable proposal document and mutable user selections for one review operation.
    /// </summary>
    public sealed class CanvasChangeReviewSession
    {
        private readonly IReadOnlyDictionary<string, CanvasChangeReviewItem> itemsByKey;

        /// <summary>
        /// Initializes a new instance of the <see cref="CanvasChangeReviewSession"/> class.
        /// </summary>
        /// <param name="title">Review title.</param>
        /// <param name="source">Name of the tool that produced the proposal.</param>
        /// <param name="proposedDocument">Proposed GhJSON fragment.</param>
        /// <param name="items">Selectable changes.</param>
        public CanvasChangeReviewSession(
            string title,
            string source,
            GhJsonDocument proposedDocument,
            IEnumerable<CanvasChangeReviewItem> items)
        {
            this.Title = title ?? throw new ArgumentNullException(nameof(title));
            this.Source = source ?? throw new ArgumentNullException(nameof(source));
            this.ProposedDocument = proposedDocument ?? throw new ArgumentNullException(nameof(proposedDocument));
            this.Items = (items ?? throw new ArgumentNullException(nameof(items))).ToList();
            this.itemsByKey = this.Items.ToDictionary(item => item.Key, StringComparer.Ordinal);
        }

        /// <summary>Occurs when an acceptance selection changes.</summary>
        public event EventHandler? SelectionChanged;

        /// <summary>Gets the review title.</summary>
        public string Title { get; }

        /// <summary>Gets the proposal source.</summary>
        public string Source { get; }

        /// <summary>Gets the proposed GhJSON fragment.</summary>
        public GhJsonDocument ProposedDocument { get; }

        /// <summary>Gets the selectable changes.</summary>
        public IReadOnlyList<CanvasChangeReviewItem> Items { get; }

        /// <summary>Gets the number of accepted changes whose dependencies are also accepted.</summary>
        public int AcceptedCount => this.Items.Count(this.IsEffectivelyAccepted);

        /// <summary>
        /// Determines whether a change and all of its dependencies are accepted.
        /// </summary>
        /// <param name="item">Change to inspect.</param>
        /// <returns><c>true</c> when the change can be applied.</returns>
        public bool IsEffectivelyAccepted(CanvasChangeReviewItem item)
        {
            return item.IsAccepted && item.RequiredItemKeys.All(key =>
                !this.itemsByKey.TryGetValue(key, out var required) || required.IsAccepted);
        }

        /// <summary>
        /// Updates one change selection.
        /// </summary>
        /// <param name="key">Change key.</param>
        /// <param name="isAccepted">Whether the change is accepted.</param>
        public void SetAccepted(string key, bool isAccepted)
        {
            if (!this.itemsByKey.TryGetValue(key, out var item) || item.IsAccepted == isAccepted)
            {
                return;
            }

            item.IsAccepted = isAccepted;
            this.SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Updates all change selections.
        /// </summary>
        /// <param name="isAccepted">Whether all changes are accepted.</param>
        public void SetAllAccepted(bool isAccepted)
        {
            var changed = false;
            foreach (var item in this.Items)
            {
                if (item.IsAccepted == isAccepted)
                {
                    continue;
                }

                item.IsAccepted = isAccepted;
                changed = true;
            }

            if (changed)
            {
                this.SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
