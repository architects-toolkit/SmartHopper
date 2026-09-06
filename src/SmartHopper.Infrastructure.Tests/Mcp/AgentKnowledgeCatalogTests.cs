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

namespace SmartHopper.Infrastructure.Tests.Mcp
{
    using System;
    using System.Linq;
    using SmartHopper.Infrastructure.Mcp;
    using Xunit;

    /// <summary>
    /// Tests the shared embedded knowledge used by WebChat, instruction tools, and MCP.
    /// </summary>
    public class AgentKnowledgeCatalogTests
    {
        [Fact]
        public void ListDocuments_AllRegisteredResourcesCanBeRead()
        {
            var documents = AgentKnowledgeCatalog.ListDocuments(includeInternal: true);

            Assert.NotEmpty(documents);
            foreach (var document in documents)
            {
                Assert.False(string.IsNullOrWhiteSpace(AgentKnowledgeCatalog.GetDocumentText(document.Id)));
            }
        }

        [Fact]
        public void GetInstructions_AliasesResolveFocusedDocuments()
        {
            var treeInstructions = AgentKnowledgeCatalog.GetInstructions("trees");
            var errorInstructions = AgentKnowledgeCatalog.GetInstructions("errors");

            Assert.Contains("data tree", treeInstructions, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Diagnostic order", errorInstructions, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ComposeGrasshopperSystemPrompt_PrependsMandatoryCore()
        {
            const string customInstructions = "Answer as a façade-design specialist.";

            var prompt = AgentKnowledgeCatalog.ComposeGrasshopperSystemPrompt(customInstructions);

            Assert.Contains("Rhino 8", prompt, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(customInstructions, prompt, StringComparison.Ordinal);
            Assert.True(prompt.IndexOf("Rhino 8", StringComparison.OrdinalIgnoreCase) < prompt.IndexOf(customInstructions, StringComparison.Ordinal));
        }

        [Fact]
        public void ListDocuments_HidesInternalCoreFromMcpResources()
        {
            Assert.DoesNotContain(AgentKnowledgeCatalog.ListDocuments(), document => document.Id == "assistant-core");
            Assert.Contains(AgentKnowledgeCatalog.ListDocuments(includeInternal: true), document => document.Id == "assistant-core");
        }

        [Fact]
        public void GetWorkflows_ParsesAndFiltersCanonicalMarkdown()
        {
            var all = AgentKnowledgeCatalog.GetWorkflows();
            var filtered = AgentKnowledgeCatalog.GetWorkflows("diagnose_data_tree");

            Assert.Contains(all, workflow => workflow.Name == "validate_change");
            Assert.Single(filtered);
            Assert.NotEmpty(filtered.Single().Steps);
            Assert.Contains(filtered.Single().Steps, step => step.Contains("paths", StringComparison.OrdinalIgnoreCase));
        }
    }
}
