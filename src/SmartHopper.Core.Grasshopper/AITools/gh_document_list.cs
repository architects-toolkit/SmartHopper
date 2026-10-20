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
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Core.Grasshopper.Utils.Canvas;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.ProviderSdk.AICall.Core.Returns;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Tool provider for listing the Grasshopper documents open in Rhino.
    /// </summary>
    public class gh_document_list : IAIToolProvider
    {
        /// <summary>
        /// Returns the gh_document_list tool.
        /// </summary>
        /// <returns>Collection of AI tools.</returns>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                name: "gh_document_list",
                description: "List the Grasshopper documents open in Rhino with their documentId, name, filePath, isActive, isModified and objectCount. Use the documentId with gh_get, gh_document_switch or gh_document_save to work with a document that is not active.",
                category: "Document",
                parametersSchema: @"{ ""type"": ""object"", ""properties"": {} }",
                execute: ListAsync,
                mutatesCanvas: false,
                tags: new[] { "document", "list" },
                outputSchema: @"{ ""type"": ""object"", ""properties"": { ""activeDocumentId"": { ""type"": [""string"", ""null""] }, ""documents"": { ""type"": ""array"" } } }",
                annotations: new AIToolAnnotations(readOnlyHint: true));
        }

        private static Task<AIReturn> ListAsync(AIToolCall toolCall)
        {
            return GhDocumentTools.Run(toolCall, _ =>
            {
                var active = GhDocumentAccess.GetActiveDocument();
                var documents = new JArray(GhDocumentAccess.GetOpenDocuments().Select(d => GhDocumentAccess.Describe(d, active)));
                return new JObject
                {
                    ["activeDocumentId"] = active?.DocumentID.ToString("D"),
                    ["documents"] = documents,
                };
            });
        }
    }
}
