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
    /// Tool provider for creating a new blank Grasshopper document.
    /// </summary>
    public class gh_document_new : IAIToolProvider
    {
        /// <summary>
        /// Returns the gh_document_new tool.
        /// </summary>
        /// <returns>Collection of AI tools.</returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: "gh_document_new",
                description: "Create a new blank Grasshopper document and make it the active document. Use gh_document_save with a filePath to save it to disk.",
                category: "Document",
                parametersSchema: @"{ ""type"": ""object"", ""properties"": {} }",
                execute: NewAsync,
                mutatesCanvas: false,
                tags: new[] { "document", "new", "file" },
                outputSchema: GhDocumentTools.DocumentOutputSchema,
                annotations: new AIToolAnnotations(readOnlyHint: false),
                surfaces: GhDocumentTools.InteractiveSurfaces);
        }

        private static Task<AIReturn> NewAsync(AIToolCall toolCall)
        {
            return GhDocumentTools.Run(toolCall, _ =>
            {
                var doc = GhDocumentAccess.CreateBlank()
                    ?? throw new InvalidOperationException("Could not create a document: no Grasshopper canvas is available.");
                return GhDocumentAccess.Describe(doc, doc);
            });
        }
    }
}
