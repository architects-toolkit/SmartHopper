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
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Metrics;
using SmartHopper.Infrastructure.AICall.Utilities;

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

        // Default behavior: appended/replaced interactions are marked as 'new'
        private bool defaultMarkAsNew = true;

        // Interactions in a logical turn share the same TurnId.
        // When set, any interaction added without TurnId will inherit this TurnId. Otherwise a new GUID is generated.
        private string defaultTurnId;

        private string toolFilter = "-*";
        private string contextFilter = "-*";
        private string jsonOutputSchema;

        /// <summary>
        /// Initializes a new instance of the <see cref="AIBodyBuilder"/> class.
        /// </summary>
        public AIBodyBuilder()
        {
        }

        /// <summary>
        /// Creates a new <see cref="AIBodyBuilder"/> instance.
        /// </summary>
        /// <returns>A new builder instance.</returns>
        public static AIBodyBuilder Create() => new AIBodyBuilder();

        /// <summary>
        /// Creates a builder initialized from an existing immutable body.
        /// </summary>
        /// <param name="body">The source immutable <see cref="AIBody"/> to copy interactions and filters from. If null, an empty builder is returned.</param>
        /// <returns>A new builder pre-populated from the given immutable body.</returns>
        public static AIBodyBuilder FromImmutable(AIBody body)
        {
            var b = new AIBodyBuilder();
            if (body != null)
            {
                try
                {
                    Debug.WriteLine($"[AIBodyBuilder.FromImmutable] input: interactions={body.InteractionsCount}, new={body.InteractionsNew?.Count ?? 0} [{string.Join(",", body.InteractionsNew ?? new List<int>())}]");
                }
                catch
                {
                    /* logging only */
                }

                if (body.Interactions != null)
                {
                    b.interactions.AddRange(body.Interactions);
                }

                b.toolFilter = body.ToolFilter ?? b.toolFilter;
                b.contextFilter = body.ContextFilter ?? b.contextFilter;
                b.jsonOutputSchema = body.JsonOutputSchema ?? b.jsonOutputSchema;

                // Preserve 'new' interaction markers so downstream mutations don't clear them
                if (body.InteractionsNew != null && body.InteractionsNew.Count > 0)
                {
                    b.interactionsNew.AddRange(body.InteractionsNew);
                }

                try
                {
                    Debug.WriteLine($"[AIBodyBuilder.FromImmutable] preserved new markers: {string.Join(",", b.interactionsNew)}");
                }
                catch
                {
                    /* logging only */
                }
            }

            return b;
        }

        /// <summary>
        /// Sets the tool filter expression to be applied to the body.
        /// </summary>
        /// <param name="filter">Filter expression (e.g. "-*", "+gh_*", "-web_*"), null keeps current.</param>
        /// <returns>The same builder instance.</returns>
        public AIBodyBuilder WithToolFilter(string filter)
        {
            this.toolFilter = filter ?? this.toolFilter;
            return this;
        }

        /// <summary>
        /// Sets the context filter expression to be applied to the body.
        /// </summary>
        /// <param name="filter">Filter expression for context providers; null keeps current.</param>
        /// <returns>The same builder instance.</returns>
        public AIBodyBuilder WithContextFilter(string filter)
        {
            this.contextFilter = filter ?? this.contextFilter;
            return this;
        }

        /// <summary>
        /// Sets a JSON Schema to instruct providers to produce structured output.
        /// </summary>
        /// <param name="jsonSchema">A JSON Schema string; null keeps current.</param>
        /// <returns>The same builder instance.</returns>
        public AIBodyBuilder WithJsonOutputSchema(string jsonSchema)
        {
            this.jsonOutputSchema = jsonSchema ?? this.jsonOutputSchema;
            return this;
        }

        /// <summary>
        /// Sets the default TurnId to be applied to interactions added by this builder.
        /// If an interaction already has a TurnId, it is preserved.
        /// </summary>
        /// <param name="turnId">The TurnId to assign to interactions lacking one. If null or whitespace, the previous default is kept.</param>
        /// <returns>The same builder instance for fluent chaining.</returns>
        public AIBodyBuilder WithTurnId(string turnId)
        {
            this.defaultTurnId = string.IsNullOrWhiteSpace(turnId) ? this.defaultTurnId : turnId;
            return this;
        }

        /// <summary>
        /// Sets the default newness applied by Add/Replace operations when an explicit flag is not provided.
        /// </summary>
        /// <param name="markAsNew">When true, subsequent adds/replacements will be marked as new unless overridden.</param>
        /// <returns>The same builder instance for fluent chaining.</returns>
        public AIBodyBuilder WithDefaultNewness(bool markAsNew)
        {
            this.defaultMarkAsNew = markAsNew;
            return this;
        }

        /// <summary>
        /// Convenience: subsequent Add/Replace operations will be considered history (not new).
        /// </summary>
        /// <returns>The same builder instance for fluent chaining.</returns>
        public AIBodyBuilder AsHistory() => this.WithDefaultNewness(false);

        /// <summary>
        /// Convenience: subsequent Add/Replace operations will be considered new.
        /// </summary>
        /// <returns>The same builder instance for fluent chaining.</returns>
        public AIBodyBuilder AsNew() => this.WithDefaultNewness(true);

        /// <summary>
        /// Adds an interaction using the builder's default newness flag.
        /// </summary>
        /// <param name="interaction">The interaction to append. Null values are ignored.</param>
        /// <returns>The same builder instance.</returns>
        public AIBodyBuilder Add(IAIInteraction interaction)
        {
            return this.Add(interaction, this.defaultMarkAsNew);
        }

        /// <summary>
        /// Adds an interaction and optionally marks it as new.
        /// </summary>
        /// <param name="interaction">The interaction to add. Null values are ignored.</param>
        /// <param name="markAsNew">Whether to mark the added interaction as new.</param>
        /// <returns>The same builder instance for fluent chaining.</returns>
        public AIBodyBuilder Add(IAIInteraction interaction, bool markAsNew)
        {
            if (interaction != null)
            {
                this.EnsureTurnId(interaction);
                this.interactions.Add(interaction);
                if (markAsNew)
                {
                    this.interactionsNew.Add(this.interactions.Count - 1);
                }

                try
                {
                    Debug.WriteLine($"[AIBodyBuilder.Add] idx={this.interactions.Count - 1}, type={interaction.GetType().Name}, agent={interaction.Agent.ToString()}, content={(interaction is AIInteractionText t ? (t.Content ?? string.Empty) : string.Empty)}, new={(markAsNew ? 1 : 0)}");
                }
                catch
                {
                    /* logging only */
                }
            }

            return this;
        }

        /// <summary>
        /// Adds a range of interactions using the builder's default newness flag for each item.
        /// </summary>
        /// <param name="items">Sequence of interactions to append.</param>
        /// <returns>The same builder instance.</returns>
        public AIBodyBuilder AddRange(IEnumerable<IAIInteraction> items)
        {
            if (items != null)
            {
                foreach (var i in items)
                {
                    this.EnsureTurnId(i);
                    this.Add(i, this.defaultMarkAsNew);
                }
            }

            return this;
        }

        /// <summary>
        /// Adds a range of interactions with per-item newness control.
        /// </summary>
        /// <param name="items">A sequence of pairs where each tuple contains the interaction and a flag indicating whether it is new.</param>
        /// <returns>The same builder instance for fluent chaining.</returns>
        public AIBodyBuilder AddRange(IEnumerable<(IAIInteraction interaction, bool isNew)> items)
        {
            if (items != null)
            {
                foreach (var (interaction, isNew) in items)
                {
                    this.EnsureTurnId(interaction);
                    this.Add(interaction, isNew);
                }
            }

            return this;
        }

        /// <summary>
        /// Adds a text interaction using the builder's default newness flag.
        /// </summary>
        /// <param name="agent">The agent producing the text (system, user, assistant).</param>
        /// <param name="content">The text content.</param>
        /// <param name="metrics">Optional metrics to attach; a new instance is created if null.</param>
        /// <param name="reasoning">Optional reasoning or hidden chain-of-thought content.</param>
        /// <returns>The same builder instance.</returns>
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

            return this.Add(it, this.defaultMarkAsNew);
        }

        /// <summary>
        /// Adds a text interaction with explicit newness.
        /// </summary>
        /// <param name="agent">The agent producing the text (system, user, assistant).</param>
        /// <param name="content">The text content.</param>
        /// <param name="markAsNew">Whether to mark the interaction as new.</param>
        /// <param name="metrics">Optional metrics to attach; a new instance is created if null.</param>
        /// <param name="reasoning">Optional reasoning or hidden chain-of-thought content.</param>
        /// <returns>The same builder instance for fluent chaining.</returns>
        public AIBodyBuilder AddText(AIAgent agent, string content, bool markAsNew, AIMetrics metrics = null, string reasoning = null)
        {
            var m = metrics ?? new AIMetrics();
            var it = new AIInteractionText
            {
                Agent = agent,
                Content = content,
                Reasoning = reasoning,
                Metrics = m,
            };
            return this.Add(it, markAsNew);
        }

        /// <summary>
        /// Adds a user text interaction using the builder's default newness flag.
        /// </summary>
        /// <param name="content">The text content.</param>
        /// <returns>The same builder instance.</returns>
        public AIBodyBuilder AddUser(string content) => this.AddText(AIAgent.User, content);

        /// <summary>
        /// Adds a user text interaction with explicit newness.
        /// </summary>
        /// <param name="content">The text content.</param>
        /// <param name="markAsNew">Whether to mark the interaction as new.</param>
        /// <returns>The same builder instance for fluent chaining.</returns>
        public AIBodyBuilder AddUser(string content, bool markAsNew) => this.AddText(AIAgent.User, content, markAsNew);

        /// <summary>
        /// Adds an assistant text interaction using the builder's default newness flag.
        /// </summary>
        /// <param name="content">The text content.</param>
        /// <returns>The same builder instance.</returns>
        public AIBodyBuilder AddAssistant(string content) => this.AddText(AIAgent.Assistant, content);

        /// <summary>
        /// Adds an assistant text interaction with explicit newness.
        /// </summary>
        /// <param name="content">The text content.</param>
        /// <param name="markAsNew">Whether to mark the interaction as new.</param>
        /// <returns>The same builder instance for fluent chaining.</returns>
        public AIBodyBuilder AddAssistant(string content, bool markAsNew) => this.AddText(AIAgent.Assistant, content, markAsNew);

        /// <summary>
        /// Adds a system text interaction using the builder's default newness flag.
        /// </summary>
        /// <param name="content">The text content.</param>
        /// <returns>The same builder instance.</returns>
        public AIBodyBuilder AddSystem(string content) => this.AddText(AIAgent.System, content);

        /// <summary>
        /// Adds a system text interaction with explicit newness.
        /// </summary>
        /// <param name="content">The text content.</param>
        /// <param name="markAsNew">Whether to mark the interaction as new.</param>
        /// <returns>The same builder instance for fluent chaining.</returns>
        public AIBodyBuilder AddSystem(string content, bool markAsNew) => this.AddText(AIAgent.System, content, markAsNew);

        /// <summary>
        /// Adds an image generation request interaction.
        /// </summary>
        /// <param name="prompt">Prompt describing the desired image.</param>
        /// <param name="size">Optional size (provider-specific).</param>
        /// <param name="quality">Optional quality (provider-specific).</param>
        /// <param name="style">Optional style (provider-specific).</param>
        /// <returns>The same builder instance.</returns>
        public AIBodyBuilder AddImageRequest(string prompt, string size = null, string quality = null, string style = null)
        {
            var img = new AIInteractionImage { Agent = AIAgent.User };
            img.CreateRequest(prompt, size, quality, style);
            return this.Add(img, this.defaultMarkAsNew);
        }

        /// <summary>
        /// Adds an image generation request interaction with explicit newness.
        /// </summary>
        /// <param name="prompt">Prompt describing the desired image.</param>
        /// <param name="markAsNew">Whether to mark the interaction as new.</param>
        /// <param name="size">Optional size (provider-specific).</param>
        /// <param name="quality">Optional quality (provider-specific).</param>
        /// <param name="style">Optional style (provider-specific).</param>
        /// <returns>The same builder instance for fluent chaining.</returns>
        public AIBodyBuilder AddImageRequest(string prompt, bool markAsNew, string size = null, string quality = null, string style = null)
        {
            var img = new AIInteractionImage { Agent = AIAgent.User };
            img.CreateRequest(prompt, size, quality, style);
            return this.Add(img, markAsNew);
        }

        /// <summary>
        /// Adds a tool call interaction.
        /// </summary>
        /// <param name="id">The tool call id to correlate with results.</param>
        /// <param name="name">The tool name.</param>
        /// <param name="args">The tool arguments as JSON.</param>
        /// <param name="metrics">Optional metrics to attach; a new instance is created if null.</param>
        /// <returns>The same builder instance.</returns>
        public AIBodyBuilder AddToolCall(string id, string name, JObject args, AIMetrics metrics = null)
        {
            var tc = new AIInteractionToolCall
            {
                Id = id,
                Name = name,
                Arguments = args,
                Metrics = metrics ?? new AIMetrics(),
            };
            return this.Add(tc, this.defaultMarkAsNew);
        }

        /// <summary>
        /// Adds a tool call interaction with explicit newness.
        /// </summary>
        /// <param name="id">The tool call id to correlate with results.</param>
        /// <param name="name">The tool name.</param>
        /// <param name="args">The tool arguments as JSON.</param>
        /// <param name="markAsNew">Whether to mark the interaction as new.</param>
        /// <param name="metrics">Optional metrics to attach; a new instance is created if null.</param>
        /// <returns>The same builder instance for fluent chaining.</returns>
        public AIBodyBuilder AddToolCall(string id, string name, JObject args, bool markAsNew, AIMetrics metrics = null)
        {
            var tc = new AIInteractionToolCall
            {
                Id = id,
                Name = name,
                Arguments = args,
                Metrics = metrics ?? new AIMetrics(),
            };
            return this.Add(tc, markAsNew);
        }

        /// <summary>
        /// Adds a tool result interaction.
        /// </summary>
        /// <param name="result">The tool result payload as JSON.</param>
        /// <param name="id">Optional tool call id the result relates to.</param>
        /// <param name="name">Optional tool name.</param>
        /// <param name="metrics">Optional metrics to attach; a new instance is created if null.</param>
        /// <param name="messages">Optional runtime messages associated with the result.</param>
        /// <returns>The same builder instance.</returns>
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
            return this.Add(tr, this.defaultMarkAsNew);
        }

        /// <summary>
        /// Adds a tool result interaction with explicit newness.
        /// </summary>
        /// <param name="result">The tool result payload as JSON.</param>
        /// <param name="markAsNew">Whether to mark the interaction as new.</param>
        /// <param name="id">Optional tool call id the result relates to.</param>
        /// <param name="name">Optional tool name.</param>
        /// <param name="metrics">Optional metrics to attach; a new instance is created if null.</param>
        /// <param name="messages">Optional runtime messages associated with the result.</param>
        /// <returns>The same builder instance for fluent chaining.</returns>
        public AIBodyBuilder AddToolResult(JObject result, bool markAsNew, string id = null, string name = null, AIMetrics metrics = null, List<AIRuntimeMessage> messages = null)
        {
            var tr = new AIInteractionToolResult
            {
                Id = id,
                Name = name,
                Result = result,
                Metrics = metrics ?? new AIMetrics(),
                Messages = messages ?? new List<AIRuntimeMessage>(),
            };
            return this.Add(tr, markAsNew);
        }

        /// <summary>
        /// Adds an error interaction using the builder's default newness flag.
        /// </summary>
        /// <param name="content">The error message.</param>
        /// <param name="metrics">Optional metrics to attach; a new instance is created if null.</param>
        /// <returns>The same builder instance.</returns>
        public AIBodyBuilder AddError(string content, AIMetrics metrics = null)
        {
            var m = metrics ?? new AIMetrics();
            var it = new AIInteractionError
            {
                Content = content,
                Metrics = m,
            };
            return this.Add(it, this.defaultMarkAsNew);
        }

        /// <summary>
        /// Adds an error interaction with explicit newness.
        /// </summary>
        /// <param name="content">The error message.</param>
        /// <param name="markAsNew">Whether to mark the interaction as new.</param>
        /// <param name="metrics">Optional metrics to attach; a new instance is created if null.</param>
        /// <returns>The same builder instance for fluent chaining.</returns>
        public AIBodyBuilder AddError(string content, bool markAsNew, AIMetrics metrics = null)
        {
            var m = metrics ?? new AIMetrics();
            var it = new AIInteractionError
            {
                Content = content,
                Metrics = m,
            };
            return this.Add(it, markAsNew);
        }

        /// <summary>
        /// Replaces the last interaction using the builder's default newness flag.
        /// </summary>
        /// <param name="interaction">The replacement interaction.</param>
        /// <returns>The same builder instance.</returns>
        public AIBodyBuilder ReplaceLast(IAIInteraction interaction)
        {
            // Delegate to overload honoring the builder's default newness
            return this.ReplaceLast(interaction, this.defaultMarkAsNew);
        }

        /// <summary>
        /// Replaces the last interaction and optionally marks it as new.
        /// </summary>
        /// <param name="interaction">The replacement interaction.</param>
        /// <param name="markAsNew">Whether to mark the replaced interaction as new.</param>
        /// <returns>The same builder instance for fluent chaining.</returns>
        public AIBodyBuilder ReplaceLast(IAIInteraction interaction, bool markAsNew)
        {
            // Ergonomics: if there is no last item, treat replace as add.
            if (this.interactions.Count == 0)
            {
                this.EnsureTurnId(interaction);
                return this.Add(interaction, markAsNew);
            }

            int idx = this.interactions.Count - 1;
            this.EnsureTurnId(interaction);
            this.interactions[idx] = interaction;
            if (markAsNew)
            {
                this.interactionsNew.Add(idx);
            }

            try
            {
                Debug.WriteLine($"[AIBodyBuilder.ReplaceLast] idx={idx}, type={interaction.GetType().Name}, agent={interaction.Agent.ToString()}, content={(interaction is AIInteractionText t ? (t.Content ?? string.Empty) : string.Empty)}, new={(markAsNew ? 1 : 0)}");
            }
            catch
            {
                /* logging only */
            }

            return this;
        }

        /// <summary>
        /// Replaces the last range of interactions with the provided list, using the builder's default newness flag.
        /// </summary>
        /// <param name="interactionList">The replacement interactions for the tail slice.</param>
        /// <returns>The same builder instance.</returns>
        public AIBodyBuilder ReplaceLastRange(List<IAIInteraction> interactionList)
        {
            // Delegate to IReadOnlyList overload to centralize logic
            return this.ReplaceLastRange((IReadOnlyList<IAIInteraction>)interactionList);
        }

        /// <summary>
        /// Replaces the last range of interactions with the provided list, using the builder's default newness flag.
        /// </summary>
        /// <param name="interactionList">The replacement interactions for the tail slice.</param>
        /// <returns>The same builder instance.</returns>
        public AIBodyBuilder ReplaceLastRange(IReadOnlyList<IAIInteraction> interactionList)
        {
            // Delegate to overload honoring the builder's default newness
            return this.ReplaceLastRange(interactionList, this.defaultMarkAsNew);
        }

        /// <summary>
        /// Replaces the last range of interactions and applies a single newness to all replacements.
        /// </summary>
        /// <param name="interactionList">The replacement interactions for the tail slice.</param>
        /// <param name="markAsNew">Whether to mark all replaced interactions as new.</param>
        /// <returns>The same builder instance for fluent chaining.</returns>
        public AIBodyBuilder ReplaceLastRange(IReadOnlyList<IAIInteraction> interactionList, bool markAsNew)
        {
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
                    this.EnsureTurnId(items[i]);
                    if (markAsNew)
                    {
                        this.interactionsNew.Add(i);
                    }

                    try
                    {
                        var it = items[i];
                        Debug.WriteLine($"[AIBodyBuilder.ReplaceLastRange(reset,flag)] idx={i}, type={it.GetType().Name}, agent={it.Agent.ToString()}, content={(it is AIInteractionText t ? (t.Content ?? string.Empty) : string.Empty)}, new={(markAsNew ? 1 : 0)}");
                    }
                    catch
                    {
                        /* logging only */
                    }
                }
            }
            else
            {
                int startIndex = existingCount - removeCount;
                this.interactions.RemoveRange(startIndex, removeCount);
                for (int i = 0; i < items.Count; i++)
                {
                    this.EnsureTurnId(items[i]);
                    if (markAsNew)
                    {
                        this.interactionsNew.Add(startIndex + i);
                    }

                    try
                    {
                        var it = items[i];
                        Debug.WriteLine($"[AIBodyBuilder.ReplaceLastRange(slice,flag)] idx={startIndex + i}, type={it.GetType().Name}, agent={it.Agent.ToString()}, content={(it is AIInteractionText t ? (t.Content ?? string.Empty) : string.Empty)}, new={(markAsNew ? 1 : 0)}");
                    }
                    catch
                    {
                        /* logging only */
                    }
                }
            }

            this.interactions.AddRange(items);
            return this;
        }

        /// <summary>
        /// Replaces the last range with per-item newness control.
        /// </summary>
        /// <param name="interactionList">A sequence of pairs where each tuple contains the interaction and a flag indicating whether it is new.</param>
        /// <returns>The same builder instance for fluent chaining.</returns>
        public AIBodyBuilder ReplaceLastRange(IReadOnlyList<(IAIInteraction interaction, bool isNew)> interactionList)
        {
            if (interactionList == null || interactionList.Count == 0)
            {
                return this;
            }

            var items = interactionList.Where(i => i.interaction != null).ToList();
            if (items.Count == 0)
            {
                return this;
            }

            int removeCount = items.Count;
            int existingCount = this.interactions.Count;

            if (removeCount >= existingCount)
            {
                this.interactions.Clear();
                for (int i = 0; i < items.Count; i++)
                {
                    this.EnsureTurnId(items[i].interaction);
                    if (items[i].isNew)
                    {
                        this.interactionsNew.Add(i);
                    }

                    try
                    {
                        var it = items[i].interaction;
                        Debug.WriteLine($"[AIBodyBuilder.ReplaceLastRange(reset,mixed)] idx={i}, type={it.GetType().Name}, agent={it.Agent.ToString()}, content={(it is AIInteractionText t ? (t.Content ?? string.Empty) : string.Empty)}, new={(items[i].isNew ? 1 : 0)}");
                    }
                    catch
                    {
                        /* logging only */
                    }
                }
            }
            else
            {
                int startIndex = existingCount - removeCount;
                this.interactions.RemoveRange(startIndex, removeCount);
                for (int i = 0; i < items.Count; i++)
                {
                    this.EnsureTurnId(items[i].interaction);
                    if (items[i].isNew)
                    {
                        this.interactionsNew.Add(startIndex + i);
                    }

                    try
                    {
                        var it = items[i].interaction;
                        Debug.WriteLine($"[AIBodyBuilder.ReplaceLastRange(slice,mixed)] idx={startIndex + i}, type={it.GetType().Name}, agent={it.Agent.ToString()}, content={(it is AIInteractionText t ? (t.Content ?? string.Empty) : string.Empty)}, new={(items[i].isNew ? 1 : 0)}");
                    }
                    catch
                    {
                        /* logging only */
                    }
                }
            }

            this.interactions.AddRange(items.Select(x => x.interaction));
            return this;
        }

        /// <summary>
        /// Sets the completion time (in seconds) on the last interaction's metrics, creating metrics if needed.
        /// </summary>
        /// <param name="completionTime">Completion time in seconds.</param>
        /// <returns>The same builder instance for fluent chaining.</returns>
        public AIBodyBuilder SetCompletionTime(double completionTime)
        {
            // Guard against empty collections and ensure metrics exists
            if (this.interactions.Count == 0)
            {
                return this;
            }

            var last = this.interactions[this.interactions.Count - 1];
            if (last == null)
            {
                return this;
            }

            if (last.Metrics == null)
            {
                last.Metrics = new AIMetrics();
            }

            last.Metrics.CompletionTime = completionTime;
            try
            {
                Debug.WriteLine($"[AIBodyBuilder.SetCompletionTime] interactions={this.interactions.Count}, new={string.Join(",", this.interactionsNew)} completionTime={completionTime:F3}");
            }
            catch
            {
                /* logging only */
            }

            return this;
        }

        /// <summary>
        /// Clears any currently tracked 'new' interaction indices. Useful when cloning an existing body
        /// to perform a new mutation (e.g., appending to session history) where only the appended items
        /// should be considered new.
        /// </summary>
        /// <returns>The same builder instance for fluent chaining.</returns>
        public AIBodyBuilder ClearNewMarkers()
        {
            this.interactionsNew.Clear();
            return this;
        }

        /// <summary>
        /// Explicitly mark the provided indices as new in the current builder state.
        /// </summary>
        /// <param name="indices">Zero-based interaction indices to mark as new.</param>
        /// <returns>The same builder instance for fluent chaining.</returns>
        public AIBodyBuilder MarkIndicesAsNew(IEnumerable<int> indices)
        {
            if (indices == null) return this;
            foreach (var idx in indices)
            {
                this.interactionsNew.Add(idx);
            }

            return this;
        }

        /// <summary>
        /// Marks the last interaction as new, if any.
        /// </summary>
        /// <returns>The same builder instance for fluent chaining.</returns>
        public AIBodyBuilder MarkLastAsNew()
        {
            if (this.interactions.Count > 0)
            {
                this.interactionsNew.Add(this.interactions.Count - 1);
            }

            return this;
        }

        /// <summary>
        /// Marks the last N interactions as new, clamped to available range.
        /// </summary>
        /// <param name="n">The number of tail interactions to mark as new.</param>
        /// <returns>The same builder instance for fluent chaining.</returns>
        public AIBodyBuilder MarkLastNAsNew(int n)
        {
            if (n <= 0 || this.interactions.Count == 0) return this;
            int start = Math.Max(0, this.interactions.Count - n);
            for (int i = start; i < this.interactions.Count; i++)
            {
                this.interactionsNew.Add(i);
            }

            return this;
        }

        /// <summary>
        /// Builds an immutable <see cref="AIBody"/> snapshot from the current builder state.
        /// </summary>
        public AIBody Build()
        {
            var snapshot = this.interactions?.ToArray() ?? Array.Empty<IAIInteraction>();

            // Final pass: ensure all interactions have a TurnId
            for (int i = 0; i < snapshot.Length; i++)
            {
                this.EnsureTurnId(snapshot[i]);
            }

            // Create a copy of indices to ensure immutability of AIBody
            var newIndices = new List<int>();
            var seen = new HashSet<int>();
            for (int i = 0; i < this.interactionsNew.Count; i++)
            {
                var idx = this.interactionsNew[i];
                if (idx < 0 || idx >= snapshot.Length) continue; // clamp to valid range
                if (seen.Add(idx)) newIndices.Add(idx); // de-duplicate while preserving order
            }

            try
            {
                Debug.WriteLine($"[AIBodyBuilder.Build] building body: interactions={snapshot.Length}, new={string.Join(",", newIndices)}");
            }
            catch
            {
                /* logging only */
            }

            return new AIBody(snapshot, this.toolFilter, this.contextFilter, this.jsonOutputSchema, newIndices);
        }

        private void EnsureTurnId(IAIInteraction interaction)
        {
            if (interaction == null) return;
            if (string.IsNullOrWhiteSpace(interaction.TurnId))
            {
                interaction.TurnId = string.IsNullOrWhiteSpace(this.defaultTurnId) ? InteractionUtility.GenerateTurnId() : this.defaultTurnId;
            }
        }
    }
}
