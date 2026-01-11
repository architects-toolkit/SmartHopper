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

#nullable enable

using System;

namespace SmartHopper.Infrastructure.AICall.Metrics
{
    /// <summary>
    /// Correlation keys used to associate metrics across session/turn/request/tool boundaries.
    /// </summary>
    public readonly record struct MetricsCorrelation(
        string? SessionId,
        int? TurnIndex,
        string? RequestId,
        string? ProviderRequestId,
        string? BodyFingerprint,
        string? ToolInvocationId
    );

    /// <summary>
    /// Types of metrics events emitted by orchestrators/providers/tools.
    /// </summary>
    public enum AIMetricsEventType
    {
        StartCall,
        StreamDelta,
        EndCall,
        ToolStart,
        ToolEnd,
        CacheEval,
        ValidationRun,
        Cancelled,
        Failed,
        Completed,
    }

    /// <summary>
    /// Basic filter for subscribing to specific subsets of metrics.
    /// </summary>
    public sealed record MetricsFilter(
        string? SessionId = null,
        string? RequestId = null,
        string? ToolInvocationId = null,
        AIMetricsEventType? EventType = null
    );

    /// <summary>
    /// Envelope for all metrics events.
    /// The <see cref="Payload"/> carries one of the domain metric records below.
    /// </summary>
    public sealed record AIMetricsEvent(
        DateTimeOffset Timestamp,
        AIMetricsEventType EventType,
        MetricsCorrelation Correlation,
        object Payload);

    /// <summary>
    /// Metrics for a single provider call (per-turn).
    /// </summary>
    public sealed record CallMetrics(
        DateTimeOffset? StartTime = null,
        DateTimeOffset? EndTime = null,
        TimeSpan? Duration = null,
        int? HttpStatusCode = null,
        int Retries = 0,
        long? BytesSent = null,
        long? BytesReceived = null,
        int? PromptTokens = null,
        int? CompletionTokens = null,
        string? Model = null,
        string? Provider = null,
        string? FinishReason = null);

    /// <summary>
    /// Metrics aggregated at the logical "turn" boundary.
    /// </summary>
    public sealed record TurnMetrics(
        TimeSpan? EncodingTime = null,
        TimeSpan? SchemaWrapTime = null,
        TimeSpan? ProviderCallTime = null,
        TimeSpan? ValidationTime = null);

    /// <summary>
    /// Streaming-related metrics for live responses.
    /// </summary>
    public sealed record StreamMetrics(
        TimeSpan? TimeToFirstToken = null,
        int DeltaCount = 0,
        double? TokensPerSecond = null,
        double? BytesPerSecond = null,
        int? LastDeltaSize = null);

    /// <summary>
    /// Metrics for a single tool invocation.
    /// </summary>
    public sealed record ToolMetrics(
        string? ToolName = null,
        TimeSpan? QueueTime = null,
        TimeSpan? DispatchTime = null,
        TimeSpan? ExecutionTime = null,
        long? ResultBytes = null,
        bool? IsError = null);

    /// <summary>
    /// Cache evaluation metrics (local and provider-side prompt caching hints).
    /// </summary>
    public sealed record CacheMetrics(
        bool? LocalHit = null,
        bool? ProviderCacheUsed = null,
        string? ETag = null,
        string? Strategy = null);

    /// <summary>
    /// Output of validators (counts only; do not include PII content).
    /// </summary>
    public sealed record ValidationMetrics(
        int Errors = 0,
        int Warnings = 0,
        int Infos = 0);
}
