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

namespace SmartHopper.Infrastructure.Consent
{
    /// <summary>
    /// Represents one user-reviewable copilot plan.
    /// </summary>
    public sealed class PlanConsentProposal : IConsentProposal
    {
        /// <summary>Initializes a new plan proposal.</summary>
        public PlanConsentProposal(
            string goal,
            string summary,
            IReadOnlyList<PlanConsentStep> steps,
            IReadOnlyList<string> assumptions,
            IReadOnlyList<string> successCriteria)
        {
            this.Id = Guid.NewGuid().ToString("N");
            this.Goal = goal;
            this.Summary = summary;
            this.Steps = steps;
            this.Assumptions = assumptions;
            this.SuccessCriteria = successCriteria;
            this.ItemKeys = steps.Select(step => step.Id).ToList();
        }

        /// <inheritdoc/>
        public string Id { get; }

        /// <inheritdoc/>
        public string Kind => "copilot-plan";

        /// <inheritdoc/>
        public string Title => "Review copilot plan";

        /// <inheritdoc/>
        public IReadOnlyList<string> ItemKeys { get; }

        /// <summary>Gets the requested goal.</summary>
        public string Goal { get; }

        /// <summary>Gets the plan summary.</summary>
        public string Summary { get; }

        /// <summary>Gets the ordered plan steps.</summary>
        public IReadOnlyList<PlanConsentStep> Steps { get; }

        /// <summary>Gets the stated assumptions.</summary>
        public IReadOnlyList<string> Assumptions { get; }

        /// <summary>Gets the success criteria.</summary>
        public IReadOnlyList<string> SuccessCriteria { get; }
    }

    /// <summary>
    /// Represents one normalized step in a copilot plan.
    /// </summary>
    public sealed class PlanConsentStep
    {
        /// <summary>Gets or sets the stable step identifier.</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Gets or sets the user-facing description.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Gets or sets the optional registered tool name.</summary>
        public string? Tool { get; set; }

        /// <summary>Gets or sets whether the registered tool mutates the canvas.</summary>
        public bool MutatesCanvas { get; set; }
    }
}
