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

using System.Collections.Generic;
using System.Threading.Tasks;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AIModels;
using SmartHopper.Infrastructure.AIProviders;

namespace SmartHopper.Infrastructure.AICall.Core.Requests
{
    public interface IAIRequest
    {
        /// <summary>
        /// Gets or sets the AI provider name.
        /// </summary>
        string Provider { get; set; }

        /// <summary>
        /// Gets the AI provider instance.
        /// </summary>
        IAIProvider ProviderInstance { get; }

        /// <summary>
        /// Gets or sets the model name.
        /// </summary>
        string Model { get; set; }

        /// <summary>
        /// Gets or sets the required capabilities to process this request.
        /// </summary>
        AICapability Capability { get; set; }

        /// <summary>
        /// Gets or sets the immutable request body.
        /// </summary>
        AIBody Body { get; set; }

        /// <summary>
        /// Indicates the caller intends to stream this request. Enables validation hints for streaming support.
        /// </summary>
        bool WantsStreaming { get; set; }

        /// <summary>
        /// Distinguishes between normal generation requests and provider backoffice/metadata requests.
        /// Defaults to <see cref="AIRequestKind.Generation"/>. When set to <see cref="AIRequestKind.Backoffice"/>,
        /// providers may bypass model/body validation for metadata endpoints (e.g., "/models").
        /// </summary>
        AIRequestKind RequestKind { get; set; }

        /// <summary>
        /// Gets or sets validation messages produced during request preparation and execution.
        /// These are informational, warning, or error notes that should be surfaced by components.
        /// Expected format uses prefixes, e.g. "(Error) ...", "(Warning) ...", "(Info) ...".
        /// </summary>
        List<AIRuntimeMessage> Messages { get; set; }

        /// <summary>
        /// A value indicating whether the request is valid.
        /// </summary>
        (bool IsValid, List<AIRuntimeMessage> Errors) IsValid();

        /// <summary>
        /// Executes the request and gets the result.
        /// </summary>
        /// <returns>The result of the request in <see cref="AIReturn"/> format.</returns>
        Task<AIReturn> Exec();
    }
}
