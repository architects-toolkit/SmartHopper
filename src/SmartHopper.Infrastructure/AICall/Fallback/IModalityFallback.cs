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

using System.Threading;
using System.Threading.Tasks;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AIModels;

namespace SmartHopper.Infrastructure.AICall.Fallback
{
    /// <summary>
    /// Defines a single modality conversion step that can replace an unsupported
    /// input modality with a supported one via an intermediate AI call or tool.
    /// </summary>
    public interface IModalityFallback
    {
        /// <summary>Stable machine name used as settings dictionary key (e.g. "ImageToText").</summary>
        string Name { get; }

        /// <summary>Modality this fallback can eliminate from a request (e.g. ImageInput).</summary>
        AICapability Handles { get; }

        /// <summary>Capability required to perform the conversion (e.g. vision for ImageToText).</summary>
        AICapability RequiresCapability { get; }

        /// <summary>Capability produced after transformation (e.g. TextInput).</summary>
        AICapability ResultsIn { get; }

        /// <summary>Human-readable description for warnings, e.g. "audio transcribed via STT".</summary>
        string Description { get; }

        /// <summary>True if this fallback can run with the given provider.</summary>
        bool IsAvailable(string providerName);

        /// <summary>
        /// Transforms the body, replacing unsupported interactions with supported ones.
        /// May perform extra AI calls (token cost).
        /// </summary>
        Task<ModalityFallbackResult> ApplyAsync(AIBody body, string providerName, string modelName, CancellationToken ct);
    }
}
