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

namespace SmartHopper.ProviderSdk.Tests.AIProviders
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Newtonsoft.Json.Linq;
    using SmartHopper.ProviderSdk.Hosting;
    using SmartHopper.ProviderSdk.Tests.TestHelpers;
    using Xunit;

    /// <summary>Exercises default tool selection through the provider's real formatting path.</summary>
    [Collection("ProviderSdk")]
    public sealed class ProgressiveToolLoadingTests : IDisposable
    {
        private readonly IToolRegistryHost previousRegistry = ProviderSdkHost.ToolRegistry;
        private readonly FakeAIProvider provider = new FakeAIProvider();
        private readonly ToolRegistry registry = new ToolRegistry();

        /// <summary>Installs an isolated catalogue with core, research and advanced tools.</summary>
        public ProgressiveToolLoadingTests()
        {
            this.registry.Add("search_tools", "Assistant.Meta");
            this.registry.Add("gh_get", "Canvas.Query");
            this.registry.Add("gh_get_selected", "Canvas.Query");
            this.registry.Add("future_canvas_query", "Canvas.Query");
            this.registry.Add("canvas_point", "Canvas.ViewControl");
            this.registry.Add("gh_document_switch", "Canvas.ViewControl");
            this.registry.Add("gh_group_selected", "Canvas.Modifications");
            this.registry.Add("gh_connect", "Canvas.Modifications");
            this.registry.Add("ask_user", "AgentPlanning", AIToolSurface.Chat);
            this.registry.Add("spawn_subagent", "AgentPlanning", AIToolSurface.Chat);
            this.registry.Add("smarthopper_readme", "Knowledge.SmartHopper");
            this.registry.Add("smarthopper_workflows", "Knowledge.SmartHopper");
            this.registry.Add("smarthopper_tool_help", "Knowledge.SmartHopper");
            this.registry.Add("mcneel_forum_search", "Knowledge.McNeelForum");
            this.registry.Add("mcneel_forum_post_get", "Knowledge.McNeelForum");
            this.registry.Add("mcneel_forum_topic_summarize", "Knowledge.McNeelForum");
            this.registry.Add("script_edit", "Scripting.Code");
            for (var index = 0; index < 4; index++)
            {
                this.registry.Add($"data_extra_{index}", "Data.Text");
            }

            ProviderSdkHost.ToolRegistry = this.registry;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            ProviderSdkHost.ToolRegistry = this.previousRegistry;
        }

        /// <summary>Knowledge is usable without surfacing whole categories or duplicate wrappers.</summary>
        [Fact]
        public void LargeCatalogue_OffersKnowledgeAndInteractionButNotAdvancedTools()
        {
            var names = this.GetNames("*");

            Assert.Contains("gh_get", names);
            Assert.Contains("ask_user", names);
            Assert.Contains("canvas_point", names);
            Assert.Contains("gh_group_selected", names);
            Assert.Contains("smarthopper_readme", names);
            Assert.Contains("smarthopper_workflows", names);
            Assert.Contains("mcneel_forum_search", names);
            Assert.Contains("mcneel_forum_post_get", names);
            Assert.DoesNotContain("spawn_subagent", names);
            Assert.DoesNotContain("script_edit", names);
            Assert.DoesNotContain("gh_connect", names);
            Assert.DoesNotContain("gh_get_selected", names);
            Assert.DoesNotContain("future_canvas_query", names);
            Assert.DoesNotContain("gh_document_switch", names);
            Assert.DoesNotContain("mcneel_forum_topic_summarize", names);
            Assert.Single(names.Where(name => name == "search_tools"));
        }

        /// <summary>Default presentation never widens category scope; discovery remains the exception.</summary>
        [Fact]
        public void LargeScopedCatalogue_DoesNotOfferOutOfScopeDefaults()
        {
            for (var index = 0; index < 13; index++)
            {
                this.registry.Add($"canvas_extra_{index}", "Canvas.Modifications");
            }

            var names = this.GetNames("Canvas");

            Assert.Contains("gh_get", names);
            Assert.Contains("canvas_point", names);
            Assert.Contains("search_tools", names);
            Assert.DoesNotContain("ask_user", names);
            Assert.DoesNotContain("smarthopper_readme", names);
            Assert.DoesNotContain("mcneel_forum_search", names);
        }

        /// <summary>Named defaults still honor disabled registrations and execution surfaces.</summary>
        [Fact]
        public void LargeCatalogue_RespectsEnabledStatusSurfaceAndExclusions()
        {
            this.registry.Tools["smarthopper_readme"].Enabled = false;

            var names = this.GetNames("*,-Knowledge.McNeelForum", AIToolSurface.Direct);

            Assert.Contains("smarthopper_workflows", names);
            Assert.DoesNotContain("smarthopper_readme", names);
            Assert.DoesNotContain("ask_user", names);
            Assert.DoesNotContain("mcneel_forum_search", names);
            Assert.DoesNotContain("mcneel_forum_post_get", names);
            Assert.DoesNotContain("gh_connect", names);
        }

        /// <summary>Small scoped catalogues retain direct schemas without requiring discovery.</summary>
        [Fact]
        public void SmallCatalogue_PreservesEligibleToolsAndNoToolsFilter()
        {
            var names = this.GetNames("Scripting");

            Assert.Single(names);
            Assert.Contains("script_edit", names);
            Assert.Empty(this.GetNames("-*"));
        }

        /// <summary>A missing discovery tool must not make eligible capabilities unreachable.</summary>
        [Fact]
        public void MissingDiscoveryTool_DoesNotCollapseCatalogue()
        {
            this.registry.Tools.Remove("search_tools");

            var names = this.GetNames("*");

            Assert.Contains("spawn_subagent", names);
            Assert.Contains("script_edit", names);
            Assert.Contains("gh_get_selected", names);
        }

        private string[] GetNames(string filter, AIToolSurface surface = AIToolSurface.Chat)
        {
            return this.provider.FormatTools(filter, surface)
                .Select(tool => tool["function"]!["name"]!.Value<string>()!)
                .ToArray();
        }

        private sealed class ToolRegistry : IToolRegistryHost
        {
            public Dictionary<string, ProviderToolDefinition> Tools { get; } = new Dictionary<string, ProviderToolDefinition>();

            public void Add(string name, string category, AIToolSurface surface = AIToolSurface.All)
            {
                this.Tools.Add(name, new ProviderToolDefinition
                {
                    Name = name,
                    Category = category,
                    Description = name,
                    ParametersSchema = "{\"type\":\"object\",\"properties\":{}}",
                    Surfaces = surface,
                });
            }

            public void DiscoverTools()
            {
            }

            public IReadOnlyDictionary<string, ProviderToolDefinition> GetTools() => this.Tools;
        }
    }
}
