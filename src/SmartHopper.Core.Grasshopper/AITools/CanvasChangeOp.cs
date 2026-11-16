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
using Newtonsoft.Json.Linq;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// One parsed operation inside a <see cref="CanvasChangePlan"/>.
    /// </summary>
    public sealed class CanvasChangeOp
    {
        /// <summary>Client-supplied unique identifier echoed in per-op results.</summary>
        required public string OpId { get; init; }

        /// <summary>Operation kind.</summary>
        required public CanvasChangeOpKind Kind { get; init; }

        /// <summary>put: raw GhJSON string.</summary>
        public string? GhJson { get; init; }

        /// <summary>put: optional edit mode; when true the AutoOffset default is off.</summary>
        public bool? EditMode { get; init; }

        /// <summary>put: optional AutoOffset override.</summary>
        public bool? AutoOffset { get; init; }

        /// <summary>set_value/set_provider target; connect/disconnect source endpoint.</summary>
        public CanvasChangeTarget? Target { get; init; }

        /// <summary>set_value: value to write (absent when only panelMode changes).</summary>
        public JToken? Value { get; init; }

        /// <summary>set_value: optional component input-parameter selector.</summary>
        public string? Param { get; init; }

        /// <summary>set_value: optional panel emission mode.</summary>
        public string? PanelMode { get; init; }

        /// <summary>set_provider: provider name to select.</summary>
        public string? Provider { get; init; }

        /// <summary>set_provider: model name to write onto a new Settings panel.</summary>
        public string? Model { get; init; }

        /// <summary>connect/disconnect: source (output) endpoint.</summary>
        public CanvasChangeTarget? From { get; init; }

        /// <summary>connect/disconnect: target (input) endpoint.</summary>
        public CanvasChangeTarget? To { get; init; }

        /// <summary>
        /// Disconnect entries carrying the same endpoint signature are exact
        /// duplicates: the first applies, later ones are reported as no-ops.
        /// </summary>
        public bool IsDuplicateNoop { get; set; }
    }
}
