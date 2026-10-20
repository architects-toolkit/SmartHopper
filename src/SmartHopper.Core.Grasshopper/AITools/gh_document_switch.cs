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
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Utils.Canvas;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.ProviderSdk.AICall.Core.Returns;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Tool provider for switching the active Grasshopper document (which document the canvas shows).
    /// </summary>
    public class gh_document_switch : IAIToolProvider
    {
        /// <summary>
        /// Returns the gh_document_switch tool.
        /// </summary>
        /// <returns>Collection of AI tools.</returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: "gh_document_switch",
                description: "Show another open Grasshopper document in the canvas, making it the active document for all canvas tools. Get documentId values from gh_document_list.",
                category: "ViewControl",
                parametersSchema: @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""documentId"": { ""type"": ""string"", ""description"": ""documentId of an open document (from gh_document_list)."" }
                    },
                    ""required"": [""documentId""]
                }",
                execute: SwitchAsync,
                mutatesCanvas: false,
                tags: new[] { "document", "switch", "view" },
                outputSchema: GhDocumentTools.DocumentOutputSchema,
                annotations: new AIToolAnnotations(readOnlyHint: false),
                surfaces: GhDocumentTools.InteractiveSurfaces);
        }

        private static Task<AIReturn> SwitchAsync(AIToolCall toolCall)
        {
            return GhDocumentTools.Run(toolCall, args =>
            {
                var doc = GhDocumentTools.RequireDocument(args["documentId"]?.ToString());

                if (!GhDocumentAccess.Activate(doc))
                {
                    throw new InvalidOperationException("Could not activate the document: no Grasshopper canvas is available.");
                }

                return GhDocumentAccess.Describe(doc, doc);
            });
        }
    }
}
