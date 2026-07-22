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

namespace SmartHopper.Infrastructure.AICall.Fallback
{
    /// <summary>
    /// Controls whether and how modality fallback is applied when a provider/model
    /// does not support a required input capability.
    /// </summary>
    public enum ModalityFallbackMode
    {
        /// <summary>No fallback; unsupported modalities produce a hard error (default).</summary>
        Disabled = 0,

        /// <summary>Convert the unsupported modality using the component's configured provider/model.
        /// If that provider also lacks the conversion capability, falls back to a hard error.</summary>
        ConfiguredProvider = 1,

        /// <summary>Convert using whichever configured provider can perform the conversion.
        /// Token costs and the provider used are reported per-branch in the Metrics output.
        /// If no configured provider can handle the conversion, falls back to a hard error.</summary>
        AnyProvider = 2,
    }
}
