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
using System.Threading;
using System.Threading.Tasks;
using SmartHopper.ProviderSdk.Hosting;

namespace SmartHopper.Infrastructure.Consent
{
    /// <summary>
    /// Identifies the caller that initiated a consent-controlled operation.
    /// </summary>
    public enum MutationInvocationSource
    {
        /// <summary>The source is unknown.</summary>
        Unknown,

        /// <summary>The operation originated in WebChat.</summary>
        WebChat,

        /// <summary>The operation originated in a Grasshopper component.</summary>
        GrasshopperComponent,

        /// <summary>The operation originated through MCP.</summary>
        Mcp,

        /// <summary>The operation was called directly as an AI tool.</summary>
        DirectTool,

        /// <summary>The operation is internal.</summary>
        Internal,
    }

    /// <summary>
    /// Identifies the terminal outcome of a consent request.
    /// </summary>
    public enum ConsentDecisionStatus
    {
        /// <summary>All proposed changes were approved.</summary>
        Approved,

        /// <summary>Only part of the proposal was approved.</summary>
        PartiallyApproved,

        /// <summary>The proposal was rejected.</summary>
        Rejected,

        /// <summary>The request was cancelled.</summary>
        Cancelled,

        /// <summary>No presenter was available.</summary>
        Unavailable,
    }

    /// <summary>
    /// Describes one immutable proposal that requires user consent.
    /// </summary>
    public interface IConsentProposal
    {
        /// <summary>Gets the unique proposal identifier.</summary>
        string Id { get; }

        /// <summary>Gets the proposal kind used to select a presenter.</summary>
        string Kind { get; }

        /// <summary>Gets the user-facing proposal title.</summary>
        string Title { get; }

        /// <summary>Gets all independently selectable item keys.</summary>
        IReadOnlyList<string> ItemKeys { get; }
    }

    /// <summary>
    /// Presents a consent request to the user without owning its lifecycle.
    /// </summary>
    public interface IConsentPresenter
    {
        /// <summary>Returns whether this presenter can display the proposal.</summary>
        bool CanPresent(IConsentProposal proposal, MutationInvocationContext context);

        /// <summary>Presents the proposal and returns one terminal decision.</summary>
        Task<ConsentDecision> PresentAsync(
            IConsentProposal proposal,
            MutationInvocationContext context,
            CancellationToken cancellationToken);
    }

    /// <summary>
    /// Carries caller identity and cancellation through a consent-controlled invocation.
    /// </summary>
    public sealed class MutationInvocationContext
    {
        /// <summary>Gets or sets the unique invocation identifier.</summary>
        public string InvocationId { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>Gets or sets the invocation source.</summary>
        public MutationInvocationSource Source { get; set; } = MutationInvocationSource.DirectTool;

        /// <summary>Gets or sets the optional owner identifier.</summary>
        public string? OwnerId { get; set; }

        /// <summary>Gets or sets the optional tool-call identifier.</summary>
        public string? ToolCallId { get; set; }

        /// <summary>Gets or sets the optional tool name.</summary>
        public string? ToolName { get; set; }

        /// <summary>Gets or sets the execution surface.</summary>
        public AIToolSurface Surface { get; set; } = AIToolSurface.Direct;

        /// <summary>Gets or sets an invocation-specific presenter.</summary>
        public IConsentPresenter? Presenter { get; set; }

        /// <summary>Creates a defensive copy for one tool call.</summary>
        public MutationInvocationContext ForTool(string? toolCallId, string? toolName)
        {
            return new MutationInvocationContext
            {
                InvocationId = Guid.NewGuid().ToString("N"),
                Source = this.Source,
                OwnerId = this.OwnerId,
                ToolCallId = toolCallId,
                ToolName = toolName,
                Surface = this.Surface,
                Presenter = this.Presenter,
            };
        }
    }

    /// <summary>
    /// Represents one terminal, single-use consent decision.
    /// </summary>
    public sealed class ConsentDecision
    {
        /// <summary>Gets or sets the terminal decision status.</summary>
        public ConsentDecisionStatus Status { get; set; }

        /// <summary>Gets or sets the accepted proposal item keys.</summary>
        public IReadOnlyList<string> AcceptedItemKeys { get; set; } = Array.Empty<string>();

        /// <summary>Gets or sets an optional user-facing reason.</summary>
        public string? Reason { get; set; }

        /// <summary>Gets or sets when the decision was made.</summary>
        public DateTime DecidedAtUtc { get; set; } = DateTime.UtcNow;

        /// <summary>Gets whether at least one change was approved.</summary>
        public bool IsApproved =>
            this.Status == ConsentDecisionStatus.Approved ||
            this.Status == ConsentDecisionStatus.PartiallyApproved;

        /// <summary>Creates a cancelled decision.</summary>
        public static ConsentDecision Cancelled(string? reason = null)
        {
            return new ConsentDecision
            {
                Status = ConsentDecisionStatus.Cancelled,
                Reason = reason,
            };
        }
    }
}
