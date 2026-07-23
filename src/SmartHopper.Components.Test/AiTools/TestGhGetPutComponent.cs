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
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.ProviderSdk.AICall.Core.Base;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;

namespace SmartHopper.Components.Test.AiTools
{
    /// <summary>
    /// Test component that exercises the core features of gh_get and gh_put.
    /// Run=True triggers a sequence of AI tool calls and reports the results.
    /// </summary>
    public class TestGhGetPutComponent : GH_Component
    {
        /// <inheritdoc />
        public override Guid ComponentGuid => new Guid("6BBDEF7C-1616-475F-8337-7B40852B2625");

        /// <inheritdoc />
        protected override Bitmap Icon => null;

        /// <inheritdoc />
        public override GH_Exposure Exposure => GH_Exposure.hidden;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestGhGetPutComponent"/> class.
        /// </summary>
        public TestGhGetPutComponent()
            : base(
                "Test gh_get / gh_put",
                "TEST-GH-GET-PUT",
                "Tests core gh_get and gh_put AI tool features. Set Run=True to execute.",
                "SmartHopper Tests",
                "Testing AiTools")
        {
        }

        /// <inheritdoc />
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter(
                "Run?",
                "R",
                "Set to True to execute the gh_get / gh_put test sequence.",
                GH_ParamAccess.item,
                false);
        }

        /// <inheritdoc />
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter(
                "Summary",
                "S",
                "High-level pass/fail summary for each test step.",
                GH_ParamAccess.list);

            pManager.AddTextParameter(
                "gh_get Count",
                "GC",
                "Total component count reported by gh_get.",
                GH_ParamAccess.item);

            pManager.AddTextParameter(
                "Placed GUIDs",
                "PG",
                "Instance GUIDs of the components placed by gh_put.",
                GH_ParamAccess.list);

            pManager.AddTextParameter(
                "Edit Verified",
                "EV",
                "Result of verifying the edit-mode panel text change.",
                GH_ParamAccess.item);
        }

        /// <inheritdoc />
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            bool run = false;
            DA.GetData(0, ref run);
            if (!run)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "Set Run to True to execute the test sequence.");
                return;
            }

            var summary = new List<string>();
            string getCount = "N/A";
            var placedGuids = new List<string>();
            string editVerified = "N/A";

            try
            {
                // Step 1: gh_get with default filters to retrieve the whole document.
                var getResult = this.ExecuteTool("gh_get", new JObject());
                if (getResult == null)
                {
                    summary.Add("FAIL: gh_get returned no result");
                    this.SetOutputs(DA, summary, getCount, placedGuids, editVerified);
                    return;
                }

                var getJson = this.ParseToolResult(getResult);
                getCount = getJson?["pagination"]?.Value<int>("totalComponents").ToString() ?? "unknown";
                summary.Add($"gh_get: totalComponents={getCount}");

                // Step 2: gh_put a small connected document (Panel -> Web To Markdown).
                const string panelText = "https://en.wikipedia.org/wiki/Markdown";
                var putDocument = new JObject
                {
                    ["schema"] = "1.0",
                    ["metadata"] = new JObject(),
                    ["components"] = new JArray
                    {
                        new JObject
                        {
                            ["name"] = "Panel",
                            ["library"] = "Params",
                            ["componentGuid"] = "59e0b89a-e487-49f8-bab8-b5bab16be14c",
                            ["id"] = 1,
                            ["pivot"] = "1500,300",
                            ["inputSettings"] = new JArray(),
                            ["outputSettings"] = new JArray
                            {
                                new JObject { ["parameterName"] = "Panel", ["nickName"] = "" },
                            },
                            ["componentState"] = new JObject
                            {
                                ["extensions"] = new JObject
                                {
                                    ["gh.panel"] = new JObject
                                    {
                                        ["text"] = panelText,
                                        ["multiline"] = true,
                                        ["wrap"] = true,
                                        ["drawIndices"] = true,
                                        ["drawPaths"] = true,
                                        ["color"] = "argb:255,255,250,90",
                                        ["bounds"] = "255x47",
                                    },
                                },
                            },
                        },
                        new JObject
                        {
                            ["name"] = "Web To Markdown",
                            ["library"] = "SmartHopper",
                            ["componentGuid"] = "4053ad8d-10df-47d3-ac0c-ca24e8bb638d",
                            ["id"] = 2,
                            ["pivot"] = "1800,300",
                            ["inputSettings"] = new JArray
                            {
                                new JObject { ["parameterName"] = "URL", ["nickName"] = "U" },
                                new JObject { ["parameterName"] = "Run?", ["nickName"] = "R" },
                            },
                            ["outputSettings"] = new JArray
                            {
                                new JObject { ["parameterName"] = "Markdown", ["nickName"] = "Md" },
                                new JObject { ["parameterName"] = "Format", ["nickName"] = "Fmt" },
                            },
                            ["componentState"] = new JObject
                            {
                                ["extensions"] = new JObject { ["smarthopper.state"] = new JObject() },
                            },
                        },
                    },
                    ["connections"] = new JArray
                    {
                        new JObject
                        {
                            ["from"] = new JObject { ["id"] = 1, ["paramName"] = "Panel", ["paramIndex"] = 0 },
                            ["to"] = new JObject { ["id"] = 2, ["paramName"] = "URL", ["paramIndex"] = 0 },
                        },
                    },
                    ["groups"] = new JArray(),
                };

                var putResult = this.ExecuteTool(
                    "gh_put",
                    new JObject
                    {
                        ["ghjson"] = putDocument.ToString(Newtonsoft.Json.Formatting.None),
                        ["autoOffset"] = false,
                    });

                var putJson = this.ParseToolResult(putResult);
                var putGuids = (putJson?["instanceGuids"] as JArray)?.Select(t => t.ToString()).ToList()
                    ?? new List<string>();
                placedGuids.AddRange(putGuids);
                summary.Add($"gh_put (add): placed {putGuids.Count} component(s)");

                if (putGuids.Count < 2)
                {
                    summary.Add("FAIL: gh_put did not place both components");
                    this.SetOutputs(DA, summary, getCount, placedGuids, editVerified);
                    return;
                }

                // Step 3: gh_get_by_guid to verify the placed objects and their connection.
                var verifyResult = this.ExecuteTool(
                    "gh_get_by_guid",
                    new JObject { ["guidFilter"] = new JArray(putGuids), ["connectionDepth"] = 1 });

                var verifyJson = this.ParseToolResult(verifyResult);
                var verifyCount = verifyJson?["pagination"]?.Value<int>("returnedComponents") ?? 0;
                var verifyConns = verifyJson?["ghjson"]?.ToObject<JObject>()?["metadata"]?["connectionCount"]?.Value<int>() ?? 0;
                summary.Add($"gh_get_by_guid: returned {verifyCount} component(s), {verifyConns} connection(s)");

                if (verifyCount < 2 || verifyConns < 1)
                {
                    summary.Add("FAIL: gh_get_by_guid did not verify the connection");
                }
                else
                {
                    summary.Add("PASS: connection preserved after gh_put");
                }

                // Step 4: gh_put edit mode to change the panel text.
                const string editedText = "https://en.wikipedia.org/wiki/Grasshopper_3D";
                var panelGuid = putGuids[0];
                var editDocument = new JObject
                {
                    ["schema"] = "1.0",
                    ["metadata"] = new JObject(),
                    ["components"] = new JArray
                    {
                        new JObject
                        {
                            ["name"] = "Panel",
                            ["library"] = "Params",
                            ["componentGuid"] = "59e0b89a-e487-49f8-bab8-b5bab16be14c",
                            ["instanceGuid"] = panelGuid,
                            ["id"] = 1,
                            ["pivot"] = "1500,300",
                            ["inputSettings"] = new JArray(),
                            ["outputSettings"] = new JArray
                            {
                                new JObject { ["parameterName"] = "Panel", ["nickName"] = "" },
                            },
                            ["componentState"] = new JObject
                            {
                                ["extensions"] = new JObject
                                {
                                    ["gh.panel"] = new JObject
                                    {
                                        ["text"] = editedText,
                                        ["multiline"] = true,
                                        ["wrap"] = true,
                                        ["drawIndices"] = true,
                                        ["drawPaths"] = true,
                                        ["color"] = "argb:255,255,250,90",
                                        ["bounds"] = "255x47",
                                    },
                                },
                            },
                        },
                    },
                    ["connections"] = new JArray(),
                    ["groups"] = new JArray(),
                };

                var editResult = this.ExecuteTool(
                    "gh_put",
                    new JObject
                    {
                        ["ghjson"] = editDocument.ToString(Newtonsoft.Json.Formatting.None),
                        ["editMode"] = true,
                        ["autoOffset"] = false,
                    });

                var editJson = this.ParseToolResult(editResult);
                var editGuids = (editJson?["instanceGuids"] as JArray)?.Select(t => t.ToString()).ToList()
                    ?? new List<string>();
                summary.Add($"gh_put (edit): updated {editGuids.Count} component(s)");

                // Step 5: Verify the edited panel text.
                var verifyEditResult = this.ExecuteTool(
                    "gh_get_by_guid",
                    new JObject { ["guidFilter"] = new JArray(panelGuid) });

                var verifyEditJson = this.ParseToolResult(verifyEditResult);
                var actualText = verifyEditJson?["ghjson"]
                    ?.ToObject<JObject>()?["components"]?[0]
                    ?["componentState"]?["extensions"]?["gh.panel"]?["text"]
                    ?.ToString();

                if (actualText == editedText)
                {
                    editVerified = "PASS";
                    summary.Add("PASS: edit-mode panel text change verified");
                }
                else
                {
                    editVerified = $"FAIL: expected '{editedText}', got '{actualText}'";
                    summary.Add(editVerified);
                }
            }
            catch (Exception ex)
            {
                summary.Add($"ERROR: {ex.Message}");
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }

            this.SetOutputs(DA, summary, getCount, placedGuids, editVerified);
        }

        private void SetOutputs(
            IGH_DataAccess DA,
            List<string> summary,
            string getCount,
            List<string> placedGuids,
            string editVerified)
        {
            DA.SetDataList(0, summary);
            DA.SetData(1, getCount);
            DA.SetDataList(2, placedGuids);
            DA.SetData(3, editVerified);
        }

        private JObject ParseToolResult(AIReturn result)
        {
            if (result?.Body?.Interactions == null)
            {
                return null;
            }

            return result.Body.Interactions
                .OfType<AIInteractionToolResult>()
                .FirstOrDefault()
                ?.Result;
        }

        private AIReturn ExecuteTool(string name, JObject arguments)
        {
            var toolCallInteraction = new AIInteractionToolCall
            {
                Name = name,
                Arguments = arguments,
                Agent = AIAgent.Assistant,
            };

            var toolCall = new AIToolCall();
            toolCall.Endpoint = name;
            toolCall.FromToolCallInteraction(toolCallInteraction);
            toolCall.SkipMetricsValidation = true;

            return toolCall.Exec().GetAwaiter().GetResult();
        }
    }
}