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
using System.Threading.Tasks;
using SmartHopper.Infrastructure.AITools;

namespace SmartHopper.Infrastructure.AICall
{
    /// <summary>
    /// Represents a tool call made by an AI model.
    /// </summary>
    public class AIToolCall : AIRequestBase
    {
        /// <summary>
        /// Gets a value indicating whether the tool call is valid.
        /// </summary>
        /// <returns>A tuple containing a boolean indicating whether the tool call is valid and a list of error messages.</returns>
        public override (bool IsValid, List<string> Errors) IsValid()
        {
            var messages = new List<string>();
            bool hasErrors = false;

            var (baseValid, baseErrors) = base.IsValid();
            if (!baseValid)
            {
                messages.AddRange(baseErrors);
                hasErrors = true;
            }

            if (this.Body.Interactions.Count == 0)
            {
                messages.Add("Body cannot be empty");
                hasErrors = true;
            }

            if (this.Body.PendingToolCallsCount() != 1)
            {
                messages.Add("Body must have exactly one pending tool call");
                hasErrors = true;
            }

            foreach (var toolCall in this.Body.PendingToolCallsList())
            {
                if (string.IsNullOrEmpty(toolCall.Name))
                {
                    messages.Add($"Tool name is required for tool call {toolCall.Id}");
                    hasErrors = true;
                }

                if (toolCall.Arguments == null)
                {
                    messages.Add($"(Info) Tool arguments are not set for tool call {toolCall.Id}");
                    hasErrors = false;
                }
            }

            return (!hasErrors, messages);
        }

        /// <inheritdoc/>
        public override async Task<AIReturn> Exec()
        {
            var result = await AIToolManager.ExecuteTool(this).ConfigureAwait(false);
            return result;
        }

        /// <summary>
        /// Initializes the tool call with the first tool call pending to run from an AI interaction tool call.
        /// </summary>
        public void FromToolCallInteraction(AIInteractionToolCall toolCall, string provider = null, string model = null)
        {
            this.Body.AddLastInteraction(toolCall);
            if (provider != null)
            {
                this.Provider = provider;
            }
            if (model != null)
            {
                this.Model = model;
            }
        }

        /// <summary>
        /// Gets the tool call from body.
        /// </summary>
        public AIInteractionToolCall GetToolCall()
        {
            return this.Body.PendingToolCallsList().First();
        }
    }
}
