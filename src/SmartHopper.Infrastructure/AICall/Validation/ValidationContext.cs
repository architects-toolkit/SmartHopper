/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Collections.Generic;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AIModels;

namespace SmartHopper.Infrastructure.AICall.Validation
{
    /// <summary>
    /// Strictness levels for gating validation results.
    /// </summary>
    public enum ValidationStrictness
    {
        InfoOrAbove = 0,
        WarningOrAbove = 1,
        ErrorOnly = 2,
    }

    /// <summary>
    /// Ambient context for validators, carrying request/response, provider/model, flags and fingerprints.
    /// </summary>
    public sealed class ValidationContext
    {
        /// <summary>
        /// Optional originating policy context for advanced scenarios.
        /// </summary>
        public Policies.PolicyContext PolicyContext { get; init; }

        /// <summary>
        /// Request under validation (when available).
        /// </summary>
        public AIRequestCall Request { get; init; }

        /// <summary>
        /// Response under validation (when available).
        /// </summary>
        public AIReturn Response { get; init; }

        /// <summary>
        /// Provider id for convenience.
        /// </summary>
        public string Provider => this.Request?.Provider ?? string.Empty;

        /// <summary>
        /// Model id for convenience.
        /// </summary>
        public string Model => this.Request?.Model ?? string.Empty;

        /// <summary>
        /// Effective capability for the call.
        /// </summary>
        public AICapability Capability => this.Request?.Capability ?? AICapability.None;

        /// <summary>
        /// Optional body fingerprint for cache/memoization correlation.
        /// </summary>
        public string BodyFingerprint { get; init; }

        /// <summary>
        /// Feature flags to enable/disable behaviors.
        /// </summary>
        public HashSet<string> FeatureFlags { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Strictness mode to use for gating (default: ErrorOnly).
        /// </summary>
        public ValidationStrictness Strictness { get; init; } = ValidationStrictness.ErrorOnly;

        public static ValidationContext FromPolicyContext(Policies.PolicyContext pc)
        {
            return new ValidationContext
            {
                PolicyContext = pc,
                Request = pc?.Request,
                Response = pc?.Response,
            };
        }
    }
}
