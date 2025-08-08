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
using SmartHopper.Infrastructure.AIContext;

namespace SmartHopper.Infrastructure.AICall
{
    public class AIRequestBody : IAIRequestBody
    {
        /// <summary>
        /// Private storage for the list of interactions.
        /// </summary>
        private List<IAIInteraction> _interactions;

        /// <inheritdoc/>
        public List<IAIInteraction> Interactions
        {
            get
            {
                var result = new List<IAIInteraction>(_interactions ?? new List<IAIInteraction>());
                
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
                            
                            var contextInteraction = new AIInteraction<string>
                            {
                                Agent = AIAgent.Context,
                                Body = contextMessage,
                            };
                            
                            result.Insert(0, contextInteraction);
                        }
                    }
                }
                
                // Return the modified list without modifying the original _interactions list
                return result; 
            }
            set => _interactions = value;
        }
        
        /// <inheritdoc/>
        public string ToolFilter { get; set; }

        /// <inheritdoc/>
        public string ContextFilter { get; set; }

        /// <inheritdoc/>
        public string JsonOutputSchema { get; set; }

        /// <inheritdoc/>
        public (bool IsValid, List<string> Errors) IsValid()
        {
            var errors = new List<string>();

            if (this.InteractionsCount() == 0)
            {
                errors.Add("At least one interaction is required");
            }

            return (errors.Count == 0, errors);
        }

        /// <inheritdoc/>
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
            _interactions ??= new List<IAIInteraction>();
            _interactions.Insert(0, interaction);
        }

        /// <summary>
        /// Adds an interaction to the start of the interaction history.
        /// </summary>
        /// <param name="interaction">The interaction to add.</param>
        public void AddFirstInteraction(List<IAIInteraction> interactions)
        {
            _interactions ??= new List<IAIInteraction>();
            _interactions.InsertRange(0, interactions);
        }

        /// <summary>
        /// Adds an interaction to the start of the interaction history from a key value pair.
        /// </summary>
        /// <param name="interaction">The interaction to add.</param>
        public void AddFirstInteraction(string agent, string body)
        {
            this.AddFirstInteraction(CreateInteraction(agent, body));
        }

        /// <summary>
        /// Adds an interaction to the end of the interaction history.
        /// </summary>
        /// <param name="interaction">The interaction to add.</param>
        public void AddLastInteraction(IAIInteraction interaction)
        {
            _interactions ??= new List<IAIInteraction>();
            _interactions.Add(interaction);
        }

        /// <summary>
        /// Adds an interaction to the end of the interaction history.
        /// </summary>
        /// <param name="interaction">The interaction to add.</param>
        public void AddLastInteraction(List<IAIInteraction> interactions)
        {
            _interactions ??= new List<IAIInteraction>();
            _interactions.AddRange(interactions);
        }

        /// <summary>
        /// Adds an interaction to the end of the interaction history from a key value pair.
        /// </summary>
        /// <param name="interaction">The interaction to add.</param>
        public void AddLastInteraction(string agent, string body)
        {
            this.AddLastInteraction(CreateInteraction(agent, body));
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
        /// Adds an interaction to the end of the interaction history.
        /// </summary>
        /// <param name="interaction">The interaction to add.</param>
        public void AddInteraction(List<IAIInteraction> interactions)
        {
            this.AddLastInteraction(interactions);
        }

        /// <summary>
        /// Adds an interaction to the start of the interaction history from a key value pair.
        /// </summary>
        /// <param name="agent"></param>
        /// <param name="body"></param>
        public void AddInteraction(string agent, string body)
        {
            this.AddLastInteraction(CreateInteraction(agent, body));
        }

        /// <summary>
        /// Gets the first interaction.
        /// </summary>
        /// <returns></returns>
        public IAIInteraction GetFirstInteraction()
        {
            return this.Interactions.FirstOrDefault();
        }

        /// <summary>
        /// Gets the first interaction of type AIAgent.
        /// </summary>
        /// /// <param name="agent"></param>
        public IAIInteraction GetFirstInteraction(AIAgent agent)
        {
            return this.Interactions.FirstOrDefault(i => i.Agent == agent);
        }

        /// <summary>
        /// Gets the first interaction of type AIAgent.
        /// </summary>
        public IAIInteraction GetFirstInteraction(string agent)
        {
            return this.Interactions.FirstOrDefault(i => i.Agent.ToString() == agent);
        }

        /// <summary>
        /// Gets the last interaction.
        /// </summary>
        public IAIInteraction GetLastInteraction()
        {
            return this.Interactions.LastOrDefault();
        }

        /// <summary>
        /// Gets the last interaction of type AIAgent.
        /// </summary>
        public IAIInteraction GetLastInteraction(AIAgent agent)
        {
            return this.Interactions.LastOrDefault(i => i.Agent == agent);
        }

        /// <summary>
        /// Gets the last interaction of type AIAgent.
        /// </summary>
        public IAIInteraction GetLastInteraction(string agent)
        {
            return this.Interactions.LastOrDefault(i => i.Agent.ToString() == agent);
        }

        /// <summary>
        /// Gets the number of interactions.
        /// </summary>
        public int InteractionsCount()
        {
            return (this._interactions?.Count ?? 0) + (HasContextData() ? 1 : 0);
        }

        /// <summary>
        /// Creates a new AIInteraction<string> from a key value pair.
        /// </summary>
        /// <param name="agent">The key of the interaction.</param>
        /// <param name="body">The value of the interaction.</param>
        private static AIInteraction<string> CreateInteraction(string agent, string body)
        {
            var interaction = new AIInteraction<string>
            {
                Agent = AIAgentExtensions.FromString(agent),
                Body = body,
            };
            return interaction;
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
