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
    /// Kind of a single <c>gh_apply_changes</c> operation. The schema is closed:
    /// unknown kinds are rejected at parse time.
    /// </summary>
    public enum CanvasChangeOpKind
    {
        /// <summary>Place a GhJSON fragment through the shared prepared-put pipeline.</summary>
        Put,

        /// <summary>Write a persistent value/panel mode on an existing object.</summary>
        SetValue,

        /// <summary>Set a provider and/or model on an <c>IProviderComponent</c>.</summary>
        SetProvider,

        /// <summary>Wire one output endpoint to one input endpoint.</summary>
        Connect,

        /// <summary>Remove the wire between two endpoints.</summary>
        Disconnect,
    }
}
