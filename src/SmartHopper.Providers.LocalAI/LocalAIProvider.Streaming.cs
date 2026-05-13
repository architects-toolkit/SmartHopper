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
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SmartHopper.Infrastructure.AICall.Core;
using SmartHopper.Infrastructure.AICall.Core.Base;
using SmartHopper.Infrastructure.AICall.Core.Interactions;
using SmartHopper.Infrastructure.AICall.Core.Requests;
using SmartHopper.Infrastructure.AICall.Core.Returns;
using SmartHopper.Infrastructure.AICall.Metrics;
using SmartHopper.Infrastructure.AIProviders;
using SmartHopper.Infrastructure.Streaming;

namespace SmartHopper.Providers.LocalAI
{
    /// <summary>
    /// Streaming half of <see cref="LocalAIProvider"/>.
    /// </summary>
    public sealed partial class LocalAIProvider
    {
        /// <inheritdoc/>
        protected override IStreamingAdapter CreateStreamingAdapter()
        {
            return new LocalAIStreamingAdapter(this);
        }

        /// <summary>
        /// Provider-scoped streaming adapter for LocalAI Chat Completions SSE.
        /// LocalAI follows the OpenAI streaming protocol (data: {...}\n\n + [DONE]).
        /// </summary>
        private sealed class LocalAIStreamingAdapter : AIProviderStreamingAdapter, IStreamingAdapter
        {
            public LocalAIStreamingAdapter(LocalAIProvider provider)
                : base(provider)
            {
            }

            public async IAsyncEnumerable<AIReturn> StreamAsync(
                AIRequestCall request,
                StreamingOptions options,
                [EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                if (request == null)
                {
                    yield break;
                }

                request = this.Prepare(request);

                if (!string.Equals(request.Endpoint, "/chat/completions", StringComparison.Ordinal))
                {
                    var unsupported = new AIReturn();
                    unsupported.CreateProviderError("Streaming is only supported for /chat/completions in this adapter.", request);
                    yield return unsupported;
                    yield break;
                }

                JObject body;
                try
                {
                    body = JObject.Parse(this.Provider.Encode(request));
                }
                catch
                {
                    body = new JObject();
                }

                body["stream"] = true;

                var fullUrl = this.BuildFullUrl(request.Endpoint);

                using var httpClient = this.CreateHttpClient();
                AIReturn? authError = null;
                try
                {
                    var apiKey = ((LocalAIProvider)this.Provider).GetApiKey();
                    this.ApplyAuthentication(httpClient, request.Authentication, apiKey);
                }
                catch (Exception ex)
                {
                    authError = new AIReturn();
                    authError.CreateProviderError(ex.Message, request);
                }

                if (authError != null)
                {
                    yield return authError;
                    yield break;
                }

                using var httpRequest = this.CreateSsePost(fullUrl, body.ToString(), "application/json");

                HttpResponseMessage response;
                AIReturn? sendError = null;
                try
                {
                    response = await this.SendForStreamAsync(httpClient, httpRequest, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    sendError = new AIReturn();
                    sendError.CreateNetworkError(ex.InnerException?.Message ?? ex.Message, request);
                    response = null!;
                }

                if (sendError != null)
                {
                    yield return sendError;
                    yield break;
                }

                if (!response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    var (message, isNetworkLike) = AIProvider.ClassifyHttpError((int)response.StatusCode, response.ReasonPhrase, content, this.Provider.Name);
                    var err = new AIReturn();
                    if (isNetworkLike)
                    {
                        err.CreateNetworkError(message, request);
                    }
                    else
                    {
                        err.CreateProviderError(message, request);
                    }

                    yield return err;
                    yield break;
                }

                var buffer = new StringBuilder();
                var lastEmit = DateTime.UtcNow;
                var firstChunk = true;
                string finalFinishReason = string.Empty;
                int promptTokens = 0;
                int completionTokens = 0;

                var assistantAggregate = new AIInteractionText
                {
                    Agent = AIAgent.Assistant,
                    Content = string.Empty,
                    Metrics = new AIMetrics { Provider = this.Provider.Name, Model = request.Model },
                };

                // Tool call accumulation (index -> partial)
                var toolCalls = new Dictionary<int, (string Id, string Name, StringBuilder Args)>();

                async IAsyncEnumerable<AIReturn> EmitAsync(string text, bool streamingStatus)
                {
                    if (string.IsNullOrEmpty(text)) yield break;

                    assistantAggregate.AppendDelta(contentDelta: text);

                    var snapshot = new AIInteractionText
                    {
                        Agent = assistantAggregate.Agent,
                        Content = assistantAggregate.Content,
                        Metrics = new AIMetrics
                        {
                            Provider = assistantAggregate.Metrics.Provider,
                            Model = assistantAggregate.Metrics.Model,
                            FinishReason = assistantAggregate.Metrics.FinishReason,
                            InputTokensCached = assistantAggregate.Metrics.InputTokensCached,
                            InputTokensPrompt = assistantAggregate.Metrics.InputTokensPrompt,
                            OutputTokensReasoning = assistantAggregate.Metrics.OutputTokensReasoning,
                            OutputTokensGeneration = assistantAggregate.Metrics.OutputTokensGeneration,
                            CompletionTime = assistantAggregate.Metrics.CompletionTime,
                        },
                    };

                    var delta = new AIReturn
                    {
                        Request = request,
                        Status = streamingStatus ? AICallStatus.Streaming : AICallStatus.Processing,
                    };
                    delta.SetBody(new List<IAIInteraction> { snapshot });
                    yield return delta;
                    await Task.Yield();
                }

                async Task<List<AIReturn>> FlushAsync(bool force)
                {
                    var results = new List<AIReturn>();
                    if (buffer.Length == 0) return results;
                    var elapsed = (DateTime.UtcNow - lastEmit).TotalMilliseconds;
                    if (force || !options.CoalesceTokens || buffer.Length >= options.PreferredChunkSize || elapsed >= options.CoalesceDelayMs)
                    {
                        var text = buffer.ToString();
                        buffer.Clear();
                        lastEmit = DateTime.UtcNow;
                        await foreach (var d in EmitAsync(text, streamingStatus: true).WithCancellation(cancellationToken))
                        {
                            results.Add(d);
                        }
                    }

                    return results;
                }

                {
                    var initial = new AIReturn { Request = request, Status = AICallStatus.Processing };
                    initial.SetBody(new List<IAIInteraction>());
                    yield return initial;
                }

                var idleTimeout = TimeSpan.FromSeconds((double)(request.TimeoutSeconds > 0 ? request.TimeoutSeconds : TimeoutDefaults.DefaultTimeoutSeconds));
                await foreach (var data in this.ReadSseDataAsync(
                    response,
                    idleTimeout,
                    null,
                    cancellationToken).WithCancellation(cancellationToken))
                {
                    JObject parsed;
                    try
                    {
                        parsed = JObject.Parse(data);
                    }
                    catch
                    {
                        continue;
                    }

                    var choices = parsed["choices"] as JArray;
                    var choice = choices?.FirstOrDefault() as JObject;
                    var delta = choice?["delta"] as JObject;
                    var finishReason = choice?["finish_reason"]?.ToString();
                    bool hasFinish = !string.IsNullOrEmpty(finishReason);
                    if (hasFinish) finalFinishReason = finishReason;

                    var usage = parsed["usage"] as JObject;
                    if (usage != null)
                    {
                        var pt = usage["prompt_tokens"]?.Value<int?>();
                        var ct = usage["completion_tokens"]?.Value<int?>();
                        if (pt.HasValue) promptTokens = pt.Value;
                        if (ct.HasValue) completionTokens = ct.Value;

                        assistantAggregate.AppendDelta(metricsDelta: new AIMetrics
                        {
                            Provider = this.Provider.Name,
                            Model = request.Model,
                            InputTokensPrompt = pt ?? 0,
                            OutputTokensGeneration = ct ?? 0,
                        });
                    }

                    var contentDelta = delta?["content"]?.ToString();
                    if (!string.IsNullOrEmpty(contentDelta))
                    {
                        buffer.Append(contentDelta);
                        var emitted = await FlushAsync(force: firstChunk).ConfigureAwait(false);
                        if (firstChunk) firstChunk = false;
                        foreach (var d in emitted)
                        {
                            yield return d;
                        }
                    }

                    var tcArray = delta?["tool_calls"] as JArray;
                    if (tcArray != null)
                    {
                        foreach (var t in tcArray.OfType<JObject>())
                        {
                            var idx = t["index"]?.Value<int?>() ?? 0;
                            if (!toolCalls.TryGetValue(idx, out var entry))
                            {
                                entry = (Id: string.Empty, Name: string.Empty, Args: new StringBuilder());
                                toolCalls[idx] = entry;
                            }

                            var idVal = t["id"]?.ToString();
                            if (!string.IsNullOrEmpty(idVal)) entry.Id = idVal;

                            var func = t["function"] as JObject;
                            if (func != null)
                            {
                                var name = func["name"]?.ToString();
                                if (!string.IsNullOrEmpty(name)) entry.Name = name;
                                var args = func["arguments"]?.ToString();
                                if (!string.IsNullOrEmpty(args)) entry.Args.Append(args);
                            }

                            toolCalls[idx] = entry;
                        }

                        var emittedTc = await FlushAsync(force: true).ConfigureAwait(false);
                        foreach (var d in emittedTc)
                        {
                            yield return d;
                        }

                        var interactions = new List<IAIInteraction>();
                        foreach (var kv in toolCalls.OrderBy(k => k.Key))
                        {
                            var (id, name, argsSb) = kv.Value;
                            JObject? argsObj = null;
                            var argsStr = argsSb.ToString();
                            try
                            {
                                if (!string.IsNullOrWhiteSpace(argsStr))
                                {
                                    argsObj = JObject.Parse(argsStr);
                                }
                            }
                            catch
                            {
                                // Partial JSON, ignore
                            }

                            interactions.Add(new AIInteractionToolCall { Id = id, Name = name, Arguments = argsObj });
                        }

                        var tcDelta = new AIReturn { Request = request, Status = AICallStatus.CallingTools };
                        tcDelta.SetBody(interactions);
                        yield return tcDelta;
                    }
                }

                var finalEmitted = await FlushAsync(force: true).ConfigureAwait(false);
                foreach (var d in finalEmitted)
                {
                    yield return d;
                }

                var final = new AIReturn
                {
                    Request = request,
                    Status = AICallStatus.Finished,
                };

                assistantAggregate.AppendDelta(metricsDelta: new AIMetrics
                {
                    FinishReason = string.IsNullOrEmpty(finalFinishReason) ? "stop" : finalFinishReason,
                });

                var finalBuilder = AIBodyBuilder.Create();
                if (!string.IsNullOrEmpty(assistantAggregate.Content))
                {
                    var finalSnapshot = new AIInteractionText
                    {
                        Agent = assistantAggregate.Agent,
                        Content = assistantAggregate.Content,
                        Metrics = new AIMetrics
                        {
                            Provider = assistantAggregate.Metrics.Provider,
                            Model = assistantAggregate.Metrics.Model,
                            FinishReason = assistantAggregate.Metrics.FinishReason,
                            InputTokensCached = assistantAggregate.Metrics.InputTokensCached,
                            InputTokensPrompt = assistantAggregate.Metrics.InputTokensPrompt,
                            OutputTokensReasoning = assistantAggregate.Metrics.OutputTokensReasoning,
                            OutputTokensGeneration = assistantAggregate.Metrics.OutputTokensGeneration,
                            CompletionTime = assistantAggregate.Metrics.CompletionTime,
                        },
                    };
                    finalBuilder.Add(finalSnapshot, markAsNew: false);
                }

                foreach (var kv in toolCalls.OrderBy(k => k.Key))
                {
                    var (id, name, argsSb) = kv.Value;
                    JObject? argsObj = null;
                    var argsStr = argsSb.ToString();
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(argsStr))
                        {
                            argsObj = JObject.Parse(argsStr);
                        }
                    }
                    catch
                    {
                        // Partial JSON
                    }

                    finalBuilder.Add(new AIInteractionToolCall { Id = id, Name = name, Arguments = argsObj }, markAsNew: false);
                }

                final.SetBody(finalBuilder.Build());
                yield return final;
            }
        }
    }
}
