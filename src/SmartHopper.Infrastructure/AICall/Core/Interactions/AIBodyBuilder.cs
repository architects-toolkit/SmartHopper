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
    /// Fluent builder for <see cref="AIBody"/>. Produces immutable instances without
    /// implicit context injection or side effects.
    /// </summary>
    public sealed class AIBodyBuilder
    {
        private readonly List<IAIInteraction> interactions = new List<IAIInteraction>();
        private readonly List<int> interactionsNew = new List<int>();

        private string toolFilter = "-*";
        private string contextFilter = "-*";
        private string jsonOutputSchema = null;

        public AIBodyBuilder() { }

        public static AIBodyBuilder Create() => new AIBodyBuilder();

        /// <summary>
        /// Creates a builder initialized from an existing immutable body.
        /// </summary>
        public static AIBodyBuilder FromImmutable(AIBody body)
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
                this.interactionsNew.Add(this.interactions.Count - 1);
            }
            return this;
        }

        public AIBodyBuilder AddRange(IEnumerable<IAIInteraction> items)
        {
            if (items != null)
            {
                foreach (var i in items)
                {
                    this.Add(i);
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

        public AIBodyBuilder AddError(string content, AIMetrics metrics = null)
        {
            var m = metrics ?? new AIMetrics();
            var it = new AIInteractionError
            {
                Content = content,
                Metrics = m,
            };
            return Add(it);
        }

        public AIBodyBuilder ReplaceLast(IAIInteraction interaction)
        {
            int idx = this.interactions.Count - 1;
            this.interactions[idx] = interaction;
            this.interactionsNew.Add(idx);
            return this;
        }

        public AIBodyBuilder ReplaceLastRange(List<IAIInteraction> interactionList)
        {
            // Delegate to IReadOnlyList overload to centralize logic
            return ReplaceLastRange((IReadOnlyList<IAIInteraction>)interactionList);
        }

        public AIBodyBuilder ReplaceLastRange(IReadOnlyList<IAIInteraction> interactionList)
        {
            // Overload accepting IReadOnlyList; mirrors List<IAIInteraction> behavior
            if (interactionList == null || interactionList.Count == 0)
            {
                return this;
            }

            // Normalize to non-null items (consistent with AddRange behavior)
            var items = interactionList.Where(i => i != null).ToList();
            if (items.Count == 0)
            {
                return this;
            }

            int removeCount = items.Count;
            int existingCount = this.interactions.Count;

            if (removeCount >= existingCount)
            {
                // If replacing equal or more than existing, reset and add the new ones
                this.interactions.Clear();
                // All positions from 0 to items.Count-1 are considered new
                for (int i = 0; i < items.Count; i++)
                {
                    this.interactionsNew.Add(i);
                }
            }
            else
            {
                // Remove the last 'removeCount' items
                int startIndex = existingCount - removeCount;
                this.interactions.RemoveRange(startIndex, removeCount);
                // Mark the indices that will be replaced as new (they will occupy the same positions)
                for (int i = 0; i < items.Count; i++)
                {
                    this.interactionsNew.Add(startIndex + i);
                }
            }

            this.interactions.AddRange(items);
            return this;
        }

        public AIBodyBuilder SetCompletionTime(double completionTime)
        {
            this.interactions.Last().Metrics.CompletionTime = completionTime;
            return this;
        }

        public AIBody Build()
        {
            var snapshot = this.interactions?.ToArray() ?? Array.Empty<IAIInteraction>();
            // Create a copy of indices to ensure immutability of AIBody
            var newIndices = new List<int>(this.interactionsNew);
            return new AIBody(snapshot, this.toolFilter, this.contextFilter, this.jsonOutputSchema, newIndices);
        }
    }
}
