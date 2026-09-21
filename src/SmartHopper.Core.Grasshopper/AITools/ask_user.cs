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
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Tools;
using SmartHopper.Infrastructure.AITools;
using SmartHopper.Infrastructure.Interaction;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.AICall.Core.Returns;
using SmartHopper.ProviderSdk.Hosting;

namespace SmartHopper.Core.Grasshopper.AITools
{
    /// <summary>
    /// Provides the Chat-only tool that lets the model ask the user a question while
    /// working. The card renders 2 to 4 predefined options plus an always-present
    /// free-text field; the call blocks until the user answers or the run is cancelled.
    /// </summary>
    public sealed class ask_user : IAIToolProvider
    {
        private const int MaxQuestionLength = 4000;
        private const int MaxOptionLength = 200;
        private const int MinOptions = 2;
        private const int MaxOptions = 4;

        /// <inheritdoc/>
        public IEnumerable<AITool> GetTools()
        {
            yield return new AITool(
                "ask_user",
                "Ask the user a question and wait for the answer. Provide between 2 and 4 predefined answer options; the chat always adds a free-text field so the user can also type their own answer. The call blocks until the user answers or the run is cancelled. Use it for genuine decisions or missing information you cannot resolve yourself — not for confirmations you could assume.",
                "Planning",
                @"{
                    ""type"": ""object"",
                    ""properties"": {
                        ""question"": {
                            ""type"": ""string"",
                            ""description"": ""The question shown to the user.""
                        },
                        ""options"": {
                            ""type"": ""array"",
                            ""minItems"": 2,
                            ""maxItems"": 4,
                            ""items"": { ""type"": ""string"" },
                            ""description"": ""Predefined answer options (2 to 4). A free-text option is added automatically.""
                        }
                    },
                    ""required"": [""question"", ""options""]
                }",
                this.ExecuteAsync,
                mutatesCanvas: false,
                tags: new[] { "planning", "question", "user-input", "interactive" },
                outputSchema: @"{ ""type"": ""object"", ""properties"": { ""answered"": { ""type"": ""boolean"" }, ""answer"": { ""type"": ""string"" }, ""isFreeText"": { ""type"": ""boolean"" } } }",
                surfaces: AIToolSurface.Chat);
        }

        private async Task<AIReturn> ExecuteAsync(AIToolCall toolCall)
        {
            var output = new AIReturn { Request = toolCall };
            try
            {
                toolCall.SkipMetricsValidation = true;
                var args = toolCall.GetToolCall().GetArgumentsOrEmpty();

                var question = args["question"]?.ToString().Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(question))
                {
                    throw new ArgumentException("The 'question' field is required.");
                }

                if (question.Length > MaxQuestionLength)
                {
                    throw new ArgumentException($"The question exceeds {MaxQuestionLength} characters.");
                }

                var optionsToken = args["options"] as JArray
                    ?? throw new ArgumentException("The 'options' array is required.");
                if (optionsToken.Count < MinOptions || optionsToken.Count > MaxOptions)
                {
                    throw new ArgumentException($"Provide between {MinOptions} and {MaxOptions} answer options.");
                }

                var options = new List<string>();
                foreach (var token in optionsToken)
                {
                    var option = token?.ToString().Trim() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(option))
                    {
                        throw new ArgumentException("Answer options cannot be empty.");
                    }

                    if (option.Length > MaxOptionLength)
                    {
                        throw new ArgumentException($"Each answer option is limited to {MaxOptionLength} characters.");
                    }

                    options.Add(option);
                }

                var presenter = toolCall.InvocationContext?.UserQuestionPresenter
                    ?? throw new InvalidOperationException("ask_user requires an interactive chat surface; no question presenter is available in this context.");

                var request = new UserQuestionRequest
                {
                    Id = $"q-{Guid.NewGuid():N}",
                    Question = question,
                    Options = options,
                };

                var answer = await presenter.AskAsync(request, toolCall.CancellationToken).ConfigureAwait(false);

                var result = new JObject
                {
                    ["answered"] = answer.Answered,
                    ["answer"] = answer.Answered ? answer.Answer : null,
                    ["isFreeText"] = answer.IsFreeText,
                };
                if (!answer.Answered)
                {
                    result["note"] = "The user did not answer; the run was cancelled.";
                }

                output.CreateSuccess(AIBodyBuilder.Create()
                    .AddToolResult(result, id: toolCall.GetToolCall().Id, name: "ask_user")
                    .Build(), toolCall);
                return output;
            }
            catch (Exception ex)
            {
                output.CreateToolError(ex.Message, toolCall);
                return output;
            }
        }
    }
}
