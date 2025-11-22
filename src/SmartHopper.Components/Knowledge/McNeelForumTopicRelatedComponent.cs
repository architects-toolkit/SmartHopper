/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;
using SmartHopper.Components.Properties;
using SmartHopper.Core.ComponentBase;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;

namespace SmartHopper.Components.Knowledge
{
    public class McNeelForumTopicRelatedComponent : StatefulAsyncComponentBase
    {
        public McNeelForumTopicRelatedComponent()
            : base(
                  "McNeel Forum Topic Related",
                  "McNeelTopicRelated",
                  "Retrieve suggested related topics for a McNeel Discourse forum topic by ID.",
                  "SmartHopper",
                  "Knowledge")
        {
            this.RunOnlyOnInputChanges = false;
        }

        public override Guid ComponentGuid => new Guid("8C3F8A61-07D4-4D8A-AB36-2AD3DC912345");

        protected override Bitmap Icon => Resources.mcneeltopicrelated;

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override void RegisterAdditionalInputParams(GH_InputParamManager pManager)
        {
            pManager.AddIntegerParameter(
                "Topic Id",
                "T",
                "REQUIRED ID of the forum topic whose related topics will be retrieved.",
                GH_ParamAccess.item);
        }

        protected override void RegisterAdditionalOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter(
                "Related Topics",
                "T",
                "List of JSON objects, one per related topic (filtered fields).",
                GH_ParamAccess.list);
        }

        protected override AsyncWorkerBase CreateWorker(Action<string> progressReporter)
        {
            return new McNeelForumTopicRelatedWorker(this, this.AddRuntimeMessage);
        }

        private sealed class McNeelForumTopicRelatedWorker : AsyncWorkerBase
        {
            private readonly McNeelForumTopicRelatedComponent parent;
            private int topicId;
            private bool hasWork;

            private readonly List<GH_String> resultTopics = new List<GH_String>();

            public McNeelForumTopicRelatedWorker(
                McNeelForumTopicRelatedComponent parent,
                Action<GH_RuntimeMessageLevel, string> addRuntimeMessage)
                : base(parent, addRuntimeMessage)
            {
                this.parent = parent;
            }

            public override void GatherInput(IGH_DataAccess DA, out int dataCount)
            {
                int localTopicId = 0;
                DA.GetData(0, ref localTopicId);
                this.topicId = localTopicId;

                this.hasWork = this.topicId > 0;
                if (!this.hasWork)
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "A valid Topic Id (> 0) is required.");
                }

                Debug.WriteLine($"[McNeelForumTopicRelatedWorker] GatherInput - TopicId={this.topicId}, HasWork={this.hasWork}");

                dataCount = this.hasWork ? 1 : 0;
            }

            public override async Task DoWorkAsync(CancellationToken token)
            {
                if (!this.hasWork)
                {
                    Debug.WriteLine("[McNeelForumTopicRelatedWorker] DoWorkAsync called with hasWork=false, exiting.");
                    return;
                }

                try
                {
                    Debug.WriteLine($"[McNeelForumTopicRelatedWorker] DoWorkAsync starting. TopicId={this.topicId}");

                    var parameters = new JObject
                    {
                        ["topic_id"] = this.topicId,
                    };

                    var toolCallInteraction = new AIInteractionToolCall
                    {
                        Name = "mcneel_forum_topic_related",
                        Arguments = parameters,
                        Agent = AIAgent.Assistant,
                    };

                    var toolCall = new AIToolCall
                    {
                        Endpoint = "mcneel_forum_topic_related",
                    };

                    toolCall.FromToolCallInteraction(toolCallInteraction);

                    AIReturn aiResult = await toolCall.Exec().ConfigureAwait(false);
                    Debug.WriteLine($"[McNeelForumTopicRelatedWorker] Tool call completed. Success={aiResult?.Success}, Status={aiResult?.Status}, HasBody={aiResult?.Body != null}");

                    if (aiResult != null)
                    {
                        var messages = aiResult.Messages;
                        Debug.WriteLine($"[McNeelForumTopicRelatedWorker] AIReturn messages count={messages?.Count ?? 0}");

                        if (messages != null)
                        {
                            foreach (var m in messages)
                            {
                                if (m == null)
                                {
                                    continue;
                                }

                                Debug.WriteLine($"[McNeelForumTopicRelatedWorker] AIReturn message: Severity={m.Severity}, Origin={m.Origin}, Text='{m.Message}'");
                            }
                        }

                        if (!aiResult.Success && messages != null)
                        {
                            foreach (var m in messages)
                            {
                                if (m == null || string.IsNullOrWhiteSpace(m.Message))
                                {
                                    continue;
                                }

                                GH_RuntimeMessageLevel level;
                                switch (m.Severity)
                                {
                                    case AIRuntimeMessageSeverity.Error:
                                        level = GH_RuntimeMessageLevel.Error;
                                        break;
                                    case AIRuntimeMessageSeverity.Warning:
                                        level = GH_RuntimeMessageLevel.Warning;
                                        break;
                                    default:
                                        level = GH_RuntimeMessageLevel.Remark;
                                        break;
                                }

                                this.AddRuntimeMessage(level, m.Message);
                            }
                        }
                    }

                    var toolResultInteraction = aiResult.Body?.GetLastInteraction(AIAgent.ToolResult) as AIInteractionToolResult;
                    var toolResult = toolResultInteraction?.Result;

                    if (toolResult == null)
                    {
                        this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Tool 'mcneel_forum_topic_related' returned no result.");
                        return;
                    }

                    var resultsArray = toolResult["related_topics"] as JArray ?? new JArray();
                    Debug.WriteLine($"[McNeelForumTopicRelatedWorker] Parsed related_topics array. Count={resultsArray.Count}");

                    this.resultTopics.Clear();
                    foreach (var topic in resultsArray)
                    {
                        this.resultTopics.Add(new GH_String(topic?.ToString() ?? string.Empty));
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[McNeelForumTopicRelatedWorker] Error: {ex}");
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                }
            }

            public override void SetOutput(IGH_DataAccess DA, out string message)
            {
                Debug.WriteLine($"[McNeelForumTopicRelatedWorker] SetOutput - resultTopics.Count={this.resultTopics.Count}");
                this.parent.SetPersistentOutput("Related Topics", this.resultTopics, DA);
                message = this.resultTopics.Count == 0 ? "No related topics found" : "Related topics retrieved";
            }
        }
    }
}
