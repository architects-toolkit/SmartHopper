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
using System.Threading;
using GhJSON.Core.SchemaModels;
using GhJSON.Grasshopper;
using GhJSON.Grasshopper.ConnectionOperations;
using GhJSON.Grasshopper.PutOperations;
using Grasshopper;
using Grasshopper.Kernel;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.ComponentBase.Contracts;
using SmartHopper.Core.ComponentBase.Cores;
using SmartHopper.Core.Grasshopper.Utils.Canvas;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.ProviderSdk.Logging;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Outcome of a <see cref="CanvasChangeApply.ApplyOnUiThread"/> commit.
    /// </summary>
    public sealed class CanvasChangeApplyResult
    {
        /// <summary>Terminal status: applied, failed, failed_compensated, failed_partial, busy or rejected.</summary>
        public string Status { get; set; } = "failed";

        /// <summary>True when every accepted operation applied.</summary>
        public bool Success { get; set; }

        /// <summary>Top-level failure reason, when any.</summary>
        public string? Error { get; set; }

        /// <summary>Per-operation results keyed by opId.</summary>
        public JObject OpResults { get; } = new JObject();

        /// <summary>put: proposed component id → placed instance GUID.</summary>
        public Dictionary<int, Guid> IdToGuidMapping { get; } = new Dictionary<int, Guid>();

        /// <summary>True when a downstream solution was scheduled at commit end.</summary>
        public bool SolutionScheduled { get; set; }

        /// <summary>Count of operations that applied (including the put).</summary>
        public int AppliedCount { get; set; }

        /// <summary>Non-fatal warnings collected during the commit.</summary>
        public IList<string> Warnings { get; } = new List<string>();
    }
}
