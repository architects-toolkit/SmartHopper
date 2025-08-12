/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AIContext;

namespace SmartHopper.Infrastructure.AICall
{
    /// <summary>
    /// Encapsulates the request body sent to AI providers, including the interaction history,
    /// optional tool and context filters, and an optional JSON output schema.
    /// The <see cref="Interactions"/> getter injects dynamic context messages at the beginning
    /// when <see cref="ContextFilter"/> is set and matching context is available.
    /// </summary>
    public class AIBody
    {
        /// <summary>
        /// Private storage for the list of interactions.
        /// </summary>
        private List<IAIInteraction> _interactions;

        /// <summary>
        /// Gets or sets the interaction list. When getting, a copy of the internal list is returned;
        /// if <see cref="ContextFilter"/> is set and context has content, a synthesized context interaction is
        /// inserted at index 0 of the returned list without mutating the internal storage.
        /// </summary>
        public List<IAIInteraction> Interactions
        {
            get
            {
                var result = new List<IAIInteraction>(this._interactions ?? new List<IAIInteraction>());

                // Remove context interaction if present
                result = result.Where(i => i.Agent != AIAgent.Context).ToList();

                // Inject dynamic context at the beginning if ContextFilter is set
                if (!string.IsNullOrEmpty(ContextFilter))
                {
                    var contextData = AIContextManager.GetCurrentContext(ContextFilter);
                    if (contextData.Count > 0)
                    {
                        var contextMessages = contextData
                            .Where(kv => !string.IsNullOrEmpty(kv.Value))
                            .Select(kv => $"- {kv.Key}: {kv.Value}");

                        if (contextMessages.Any())
                        {
                            var contextMessage = "Conversation context:\n\n" +
                                                 string.Join("\n", contextMessages);

                            var contextInteraction = new AIInteractionText
                            {
                                Agent = AIAgent.Context,
                                Content = contextMessage,
                            };

                            result.Insert(0, contextInteraction);
                        }
                    }
                }

                // Return the modified list without modifying the original this._interactions list
                return result; 
            }
            set => this._interactions = value;
        }

        /// <summary>
        /// Gets or sets the tool filter.
        /// Defaults to no tools.
        /// </summary>
        public string ToolFilter { get; set; } = "-*";

        /// <summary>
        /// Gets or sets the context filter.
        /// Defaults to no context.
        /// </summary>
        public string ContextFilter { get; set; } = "-*";

        /// <summary>
        /// Gets or sets the output JSON schema.
        /// </summary>
        public string JsonOutputSchema { get; set; }

        /// <summary>
        /// Gets the aggregated metrics for all interactions.
        /// </summary>
        public AIMetrics Metrics {
            get
            {
                var metrics = new AIMetrics();
                foreach (var interaction in this._interactions)
                {
                    metrics.Combine(interaction.Metrics);
                }
                return metrics;
            }
        }

        /// <summary>
        /// Validates the body.
        /// </summary>
        public (bool IsValid, List<string> Errors) IsValid()
        {
            var errors = new List<string>();

            if (this.InteractionsCount() == 0)
            {
                errors.Add("At least one interaction is required");
            }

            return (errors.Count == 0, errors);
        }

        /// <summary>
        /// Checks if the body requires JSON output.
        /// </summary>
        public bool RequiresJsonOutput()
        {
            return !string.IsNullOrEmpty(this.JsonOutputSchema);
        }

        /// <summary>
        /// Adds an interaction to the start of the interaction history.
        /// </summary>
        /// <param name="interaction">The interaction to add.</param>
        public void AddFirstInteraction(IAIInteraction interaction)
        {
            this._interactions ??= new List<IAIInteraction>();
            this._interactions.Insert(0, interaction);
        }

        /// <summary>
        /// Adds interactions to the start of the interaction history.
        /// </summary>
        /// <param name="interactions">The interactions to add.</param>
        public void AddFirstInteraction(List<IAIInteraction> interactions)
        {
            this._interactions ??= new List<IAIInteraction>();
            this._interactions.InsertRange(0, interactions);
        }

        /// <summary>
        /// Adds an interaction to the start of the interaction history using an agent name and a body string.
        /// </summary>
        /// <param name="agent">The agent name (e.g., "User", "Assistant", "System").</param>
        /// <param name="body">The textual content of the interaction.</param>
        /// <param name="metrics">The metrics associated with the interaction.</param>
        public void AddFirstInteraction(string agent, string body, AIMetrics metrics)
        {
            this.AddFirstInteraction(CreateInteractionText(agent, body, metrics));
        }

        /// <summary>
        /// Adds an interaction to the end of the interaction history.
        /// </summary>
        /// <param name="interaction">The interaction to add.</param>
        public void AddLastInteraction(IAIInteraction interaction)
        {
            this._interactions ??= new List<IAIInteraction>();
            this._interactions.Add(interaction);
        }

        /// <summary>
        /// Adds interactions to the end of the interaction history.
        /// </summary>
        /// <param name="interactions">The interactions to add.</param>
        public void AddLastInteraction(List<IAIInteraction> interactions)
        {
            this._interactions ??= new List<IAIInteraction>();
            this._interactions.AddRange(interactions);
        }

        /// <summary>
        /// Adds an interaction to the end of the interaction history using an agent name and a body string.
        /// </summary>
        /// <param name="agent">The agent name (e.g., "User", "Assistant", "System").</param>
        /// <param name="body">The textual content of the interaction.</param>
        /// <param name="metrics">The metrics associated with the interaction.</param>
        public void AddLastInteraction(string agent, string body, AIMetrics metrics)
        {
            this.AddLastInteraction(CreateInteractionText(agent, body, metrics));
        }

        /// <summary>
        /// Adds an interaction to the end of the interaction history.
        /// </summary>
        /// <param name="interaction">The interaction to add.</param>
        public void AddInteraction(IAIInteraction interaction)
        {
            this.AddLastInteraction(interaction);
        }

        /// <summary>
        /// Adds interactions to the end of the interaction history.
        /// </summary>
        /// <param name="interactions">The interactions to add.</param>
        public void AddInteraction(List<IAIInteraction> interactions)
        {
            this.AddLastInteraction(interactions);
        }

        /// <summary>
        /// Adds an interaction to the end of the interaction history using an agent name and a body string.
        /// </summary>
        /// <param name="agent">The agent name (e.g., "User", "Assistant", "System").</param>
        /// <param name="body">The textual content of the interaction.</param>
        /// <param name="metrics">The metrics associated with the interaction.</param>>
        public void AddInteraction(string agent, string body, AIMetrics metrics)
        {
            this.AddLastInteraction(CreateInteractionText(agent, body, metrics));
        }

        /// <summary>
        /// Adds an interaction to the end of the interaction history using an agent name and a body string.
        /// </summary>
        /// <param name="body">The textual content of the interaction.</param>
        public void AddInteractionToolResult(JObject body, AIMetrics metrics)
        {
            this.AddLastInteraction(CreateInteractionToolResult(body, metrics));
        }

        /// <summary>
        /// Gets the first interaction.
        /// </summary>
        /// <returns>The first interaction if present; otherwise, null.</returns>
        public IAIInteraction GetFirstInteraction()
        {
            return this.Interactions.FirstOrDefault();
        }

        /// <summary>
        /// Gets the first interaction by the specified agent.
        /// </summary>
        /// <param name="agent">The agent to match.</param>
        /// <returns>The first matching interaction if present; otherwise, null.</returns>
        public IAIInteraction GetFirstInteraction(AIAgent agent)
        {
            return this.Interactions.FirstOrDefault(i => i.Agent == agent);
        }

        /// <summary>
        /// Gets the first interaction whose agent name matches the provided string.
        /// </summary>
        /// <param name="agent">Agent name to match.</param>
        /// <returns>The first matching interaction if present; otherwise, null.</returns>
        public IAIInteraction GetFirstInteraction(string agent)
        {
            return this.Interactions.FirstOrDefault(i => i.Agent.ToString() == agent);
        }

        /// <summary>
        /// Gets the last interaction.
        /// </summary>
        /// <returns>The last interaction if present; otherwise, null.</returns>
        public IAIInteraction GetLastInteraction()
        {
            return this.Interactions.LastOrDefault();
        }

        /// <summary>
        /// Gets the last interaction by the specified agent.
        /// </summary>
        /// <param name="agent">The agent to match.</param>
        /// <returns>The last matching interaction if present; otherwise, null.</returns>
        public IAIInteraction GetLastInteraction(AIAgent agent)
        {
            return this.Interactions.LastOrDefault(i => i.Agent == agent);
        }

        /// <summary>
        /// Gets the last interaction whose agent name matches the provided string.
        /// </summary>
        /// <param name="agent">Agent name to match.</param>
        /// <returns>The last matching interaction if present; otherwise, null.</returns>
        public IAIInteraction GetLastInteraction(string agent)
        {
            return this.Interactions.LastOrDefault(i => i.Agent.ToString() == agent);
        }

        /// <summary>
        /// Gets the number of interactions, including a synthesized context interaction when applicable.
        /// </summary>
        /// <returns>The total count of interactions that would be returned by <see cref="Interactions"/>.</returns>
        public int InteractionsCount()
        {
            return (this._interactions?.Count ?? 0) + (HasContextData() ? 1 : 0);
        }

        /// <summary>
        /// Checks if there are pending tool calls by matching AIInteractionToolCall.Id with AIInteractionToolResult.Id.
        /// </summary>
        /// <returns>The count of pending tool calls.</returns>
        public int PendingToolCallsCount()
        {
            var toolCalls = this.Interactions.OfType<AIInteractionToolCall>();
            var toolResults = this.Interactions.OfType<AIInteractionToolResult>();

            return toolCalls.Count(tc => toolResults.Any(tr => tr.Id == tc.Id));
        }

        /// <summary>
        /// Gets the list of pending tool calls by matching AIInteractionToolCall.Id with AIInteractionToolResult.Id.
        /// </summary>
        /// <returns>The list of pending tool calls.</returns>
        public List<AIInteractionToolCall> PendingToolCallsList()
        {
            var toolCalls = this.Interactions.OfType<AIInteractionToolCall>();
            var toolResults = this.Interactions.OfType<AIInteractionToolResult>();

            return toolCalls.Where(tc => toolResults.Any(tr => tr.Id == tc.Id)).ToList();
        }

        /// <summary>
        /// Creates a new AIInteraction<string> from an agent name and body string.
        /// </summary>
        /// <param name="agent">The agent name.</param>
        /// <param name="body">The textual content.</param>
        /// <param name="metrics">The metrics associated with the interaction.</param>
        /// <returns>The created AIInteraction<string>.</returns>
        private static AIInteractionText CreateInteractionText(string agent, string body, AIMetrics metrics)
        {
            if (metrics is null)
            {
                metrics = new AIMetrics();
            }

            var interaction = new AIInteractionText
            {
                Agent = AIAgentExtensions.FromString(agent),
                Content = body,
                Metrics = metrics,
            };
            return interaction;
        }

        /// <summary>
        /// Creates a new AIInteraction<string> from an agent name and body string.
        /// </summary>
        /// <param name="body">The textual content.</param>
        /// <param name="metrics">The metrics associated with the interaction.</param>
        /// <returns>The created AIInteraction<string>.</returns>
        private static AIInteractionToolResult CreateInteractionToolResult(JObject result, AIMetrics metrics)
        {
            if (metrics is null)
            {
                metrics = new AIMetrics();
            }

            var interaction = new AIInteractionToolResult
            {
                Result = result,
                Metrics = metrics,
            };
            return interaction;
        }

        /// <summary>
        /// Creates a new AIInteraction<string> from an agent name and body string.
        /// </summary>
        /// <param name="agent">The agent name.</param>
        /// <param name="body">The textual content.</param>
        private void OverrideInteractions(List<IAIInteraction> interactions)
        {
            this._interactions = interactions;
        }

        /// <summary>
        /// Checks if there is context data to inject.
        /// </summary>
        private bool HasContextData()
        {
            if (string.IsNullOrEmpty(ContextFilter))
            {
                return false;
            }

            var contextData = AIContextManager.GetCurrentContext(ContextFilter);

            return contextData.Any(kv => !string.IsNullOrEmpty(kv.Value));
        }
    }
}
