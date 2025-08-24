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
using System.Linq;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Metrics;

namespace SmartHopper.Infrastructure.AICall.Core.Interactions
{
    /// <summary>
    /// Fluent builder for <see cref="AIBodyImmutable"/>. Produces immutable instances without
    /// implicit context injection or side effects.
    /// </summary>
    public sealed class AIBodyBuilder
    {
        private readonly List<IAIInteraction> interactions = new List<IAIInteraction>();

        private string toolFilter = "-*";
        private string contextFilter = "-*";
        private string jsonOutputSchema = null;

        public AIBodyBuilder() { }

        public static AIBodyBuilder Create() => new AIBodyBuilder();

        /// <summary>
        /// Creates a builder initialized from an existing immutable body.
        /// </summary>
        public static AIBodyBuilder FromImmutable(AIBodyImmutable body)
        {
            var b = new AIBodyBuilder();
            if (body != null)
            {
                if (body.Interactions != null)
                {
                    b.interactions.AddRange(body.Interactions);
                }
                b.toolFilter = body.ToolFilter ?? b.toolFilter;
                b.contextFilter = body.ContextFilter ?? b.contextFilter;
                b.jsonOutputSchema = body.JsonOutputSchema ?? b.jsonOutputSchema;
            }
            return b;
        }

        /// <summary>
        /// Creates a builder initialized from the legacy mutable body.
        /// Context interactions (Agent == Context) are filtered out to avoid mixing implicit enrichment.
        /// </summary>
        public static AIBodyBuilder FromMutable(AIBody legacy)
        {
            var b = new AIBodyBuilder();
            if (legacy != null)
            {
                var raw = legacy.Interactions ?? new List<IAIInteraction>();
                b.interactions.AddRange(raw.Where(i => i != null && i.Agent != AIAgent.Context));
                b.toolFilter = legacy.ToolFilter ?? b.toolFilter;
                b.contextFilter = legacy.ContextFilter ?? b.contextFilter;
                b.jsonOutputSchema = legacy.JsonOutputSchema ?? b.jsonOutputSchema;
            }
            return b;
        }

        public AIBodyBuilder WithToolFilter(string filter)
        {
            this.toolFilter = filter ?? this.toolFilter;
            return this;
        }

        public AIBodyBuilder WithContextFilter(string filter)
        {
            this.contextFilter = filter ?? this.contextFilter;
            return this;
        }

        public AIBodyBuilder WithJsonOutputSchema(string jsonSchema)
        {
            this.jsonOutputSchema = jsonSchema ?? this.jsonOutputSchema;
            return this;
        }

        public AIBodyBuilder Add(IAIInteraction interaction)
        {
            if (interaction != null)
            {
                this.interactions.Add(interaction);
            }
            return this;
        }

        public AIBodyBuilder AddRange(IEnumerable<IAIInteraction> items)
        {
            if (items != null)
            {
                foreach (var i in items)
                {
                    if (i != null) this.interactions.Add(i);
                }
            }
            return this;
        }

        public AIBodyBuilder AddText(AIAgent agent, string content, AIMetrics metrics = null, string reasoning = null)
        {
            var m = metrics ?? new AIMetrics();
            var it = new AIInteractionText
            {
                Agent = agent,
                Content = content,
                Reasoning = reasoning,
                Metrics = m,
            };
            return Add(it);
        }

        public AIBodyBuilder AddUser(string content) => AddText(AIAgent.User, content);
        public AIBodyBuilder AddAssistant(string content) => AddText(AIAgent.Assistant, content);
        public AIBodyBuilder AddSystem(string content) => AddText(AIAgent.System, content);

        public AIBodyBuilder AddImageRequest(string prompt, string size = null, string quality = null, string style = null)
        {
            var img = new AIInteractionImage { Agent = AIAgent.User };
            img.CreateRequest(prompt, size, quality, style);
            return Add(img);
        }

        public AIBodyBuilder AddToolCall(string id, string name, JObject args, AIMetrics metrics = null)
        {
            var tc = new AIInteractionToolCall
            {
                Id = id,
                Name = name,
                Arguments = args,
                Metrics = metrics ?? new AIMetrics(),
            };
            return Add(tc);
        }

        public AIBodyBuilder AddToolResult(JObject result, string id = null, string name = null, AIMetrics metrics = null, List<AIRuntimeMessage> messages = null)
        {
            var tr = new AIInteractionToolResult
            {
                Id = id,
                Name = name,
                Result = result,
                Metrics = metrics ?? new AIMetrics(),
                Messages = messages ?? new List<AIRuntimeMessage>(),
            };
            return Add(tr);
        }

        public AIBodyImmutable Build()
        {
            var snapshot = this.interactions?.ToArray() ?? Array.Empty<IAIInteraction>();
            return new AIBodyImmutable(snapshot, this.toolFilter, this.contextFilter, this.jsonOutputSchema);
        }
    }
}
