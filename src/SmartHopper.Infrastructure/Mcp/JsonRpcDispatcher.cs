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

/*
 * JSON-RPC dispatch shape and MCP method names structurally adapted from Cordyceps
 * (https://github.com/brookstalley/cordyceps, McpServer.cs).
 * Copyright (c) 2026 Brooks Talley. Licensed under the MIT License.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SmartHopper.ProviderSdk.AICall.Core.Interactions;
using SmartHopper.ProviderSdk.Logging;
namespace SmartHopper.Infrastructure.Mcp
{
    /// <summary>
    /// Translates JSON-RPC 2.0 envelopes into MCP method invocations against the
    /// <see cref="AIToolMcpAdapter"/>. Control-plane methods (initialize, ping,
    /// notifications, immutable tools/list) are never gated; long-running tool and
    /// dynamic provider methods are admitted through a bounded lane so transport
    /// capacity stays responsive under saturation. Document-commit serialization
    /// is owned by the shared mutation coordinator, not this dispatcher
    /// (see <c>docs/Architecture/mcp-server.md</c> §6).
    /// </summary>
    public sealed class JsonRpcDispatcher : IDisposable
    {
        /// <summary>The HTTP header carrying the logical MCP session id.</summary>
        internal const string SessionHeader = "Mcp-Session-Id";

        private const string ProtocolVersion = "2025-03-26";
        private const string JsonRpcVersion = "2.0";

        // JSON-RPC error codes per https://www.jsonrpc.org/specification.
        private const int ParseError = -32700;
        private const int InvalidRequest = -32600;
        private const int MethodNotFound = -32601;
        private const int InvalidParams = -32602;
        private const int InternalError = -32603;

        // Bounded admission lane for long-running tool/provider work. Transport
        // accepts up to 16 concurrent requests; keeping admitted work below that
        // leaves headroom so control methods never starve behind tool execution.
        private const int MaxAdmittedWork = 12;
        private static readonly TimeSpan AdmissionBudget = TimeSpan.FromSeconds(1);

        private readonly McpServerOptions options;
        private readonly AIToolMcpAdapter adapter;
        private readonly IMcpResourceProvider? resourceProvider;
        private readonly IMcpPromptProvider? promptProvider;
        private readonly SemaphoreSlim admittedWork = new SemaphoreSlim(MaxAdmittedWork, MaxAdmittedWork);
        private readonly McpSessionRegistry sessions = new McpSessionRegistry();

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonRpcDispatcher"/> class.
        /// </summary>
        public JsonRpcDispatcher(McpServerOptions options, AIToolMcpAdapter adapter)
            : this(options, adapter, null, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonRpcDispatcher"/> class
        /// with optional MCP resource and prompt providers.
        /// </summary>
        public JsonRpcDispatcher(
            McpServerOptions options,
            AIToolMcpAdapter adapter,
            IMcpResourceProvider? resourceProvider,
            IMcpPromptProvider? promptProvider)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            this.resourceProvider = resourceProvider;
            this.promptProvider = promptProvider;
        }

        /// <summary>
        /// Parses a raw JSON-RPC request body and produces a serialized response body.
        /// Returns <c>null</c> for notifications (no <c>id</c> in the request).
        /// Stateless variant kept for compatibility — no session correlation.
        /// </summary>
        /// <param name="requestBody">Raw request payload.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task<string?> DispatchAsync(string requestBody, CancellationToken cancellationToken = default)
        {
            var result = await this.DispatchRequestAsync(requestBody, null, cancellationToken).ConfigureAwait(false);
            return result.Body;
        }

        /// <summary>
        /// Parses a raw JSON-RPC request body and produces the transport-facing
        /// dispatch outcome. <paramref name="sessionId"/> is the value of the
        /// <c>Mcp-Session-Id</c> request header; an <c>initialize</c> call allocates
        /// a fresh session returned on the result for the response header.
        /// </summary>
        /// <param name="requestBody">Raw request payload.</param>
        /// <param name="sessionId">Presented <c>Mcp-Session-Id</c> header value, or null.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task<McpDispatchResult> DispatchRequestAsync(
            string requestBody,
            string? sessionId,
            CancellationToken cancellationToken = default)
        {
            JObject? request;
            try
            {
                request = JObject.Parse(requestBody ?? string.Empty);
            }
            catch (JsonReaderException ex)
            {
                Logger.Warning(typeof(JsonRpcDispatcher).FullName, $"[JsonRpcDispatcher] Rejected malformed JSON-RPC payload: {ex.Message}");
                return new McpDispatchResult { Body = SerializeError(null, ParseError, $"Parse error: {ex.Message}") };
            }

            var id = request["id"];
            var method = request.Value<string?>("method");
            var paramsToken = request["params"] as JObject;

            if (string.IsNullOrWhiteSpace(method))
            {
                return new McpDispatchResult { Body = SerializeError(id, InvalidRequest, "Missing 'method'") };
            }

            // Resolve the presented session. An unknown id (expired or foreign) is
            // rejected at the transport level per the MCP streamable-HTTP contract —
            // silently degrading to stateless would break the client's expectation
            // that its requestId/cancellation scope still exists.
            McpSession? session = null;
            if (!string.IsNullOrWhiteSpace(sessionId))
            {
                session = this.sessions.TryGet(sessionId);
                if (session == null)
                {
                    return new McpDispatchResult
                    {
                        StatusHint = 404,
                        Body = SerializeError(id, InvalidRequest, "Unknown or expired Mcp-Session-Id; re-initialize"),
                    };
                }
            }

            // notifications/cancelled finds only the matching request inside the
            // caller's own session; stateless callers cannot cancel by guessing ids.
            if (string.Equals(method, "notifications/cancelled", StringComparison.Ordinal))
            {
                var cancelId = paramsToken?["id"];
                if (session != null && cancelId != null)
                {
                    session.Requests.Cancel(cancelId);
                }

                return new McpDispatchResult { StatusHint = 204 };
            }

            // Requests carrying an id register in the session so cancellation can
            // reach them; concurrent duplicate ids are rejected rather than
            // overwriting each other's cancellation slot.
            McpRequestScope? scope = null;
            if (session != null && id != null)
            {
                if (!session.Requests.TryBegin(id, cancellationToken, out scope))
                {
                    return new McpDispatchResult
                    {
                        Body = SerializeError(id, InvalidRequest, "Duplicate request id: a request with this id is already in flight in this session"),
                    };
                }
            }

            var executionToken = scope?.Token ?? cancellationToken;

            // Control-plane methods (initialize, ping, notifications, tools/list)
            // skip admission entirely. Dynamic tool/resource/prompt execution is
            // admitted through the bounded lane; excess work is rejected promptly
            // rather than queued unboundedly behind long-running calls.
            var requiresAdmission = RequiresAdmission(method);
            var admitted = false;
            if (requiresAdmission)
            {
                try
                {
                    admitted = await this.admittedWork.WaitAsync(AdmissionBudget, executionToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    scope?.Dispose();
                    throw;
                }

                if (!admitted)
                {
                    scope?.Dispose();
                    return new McpDispatchResult
                    {
                        Body = SerializeError(id, InternalError, "server_busy: admitted work capacity saturated; retry later"),
                    };
                }
            }

            try
            {
                JToken result;
                string? newSessionId = null;
                switch (method)
                {
                    case "initialize":
                        result = this.HandleInitialize();
                        newSessionId = this.sessions.TryCreate()?.Id;
                        if (newSessionId == null)
                        {
                            return new McpDispatchResult
                            {
                                Body = SerializeError(id, InternalError, "Session capacity reached; retry later"),
                            };
                        }

                        break;
                    case "notifications/initialized":
                    case "initialized":
                        // Notification — no response.
                        return new McpDispatchResult { StatusHint = 204 };
                    case "ping":
                        result = new JObject();
                        break;
                    case "tools/list":
                        result = this.HandleToolsList();
                        break;
                    case "tools/call":
                        result = await this.HandleToolsCallAsync(paramsToken, session, executionToken).ConfigureAwait(false);
                        break;
                    case "resources/list":
                        result = await this.HandleResourcesListAsync().ConfigureAwait(false);
                        break;
                    case "resources/read":
                        result = await this.HandleResourcesReadAsync(paramsToken).ConfigureAwait(false);
                        break;
                    case "prompts/list":
                        result = await this.HandlePromptsListAsync().ConfigureAwait(false);
                        break;
                    case "prompts/get":
                        result = await this.HandlePromptsGetAsync(paramsToken).ConfigureAwait(false);
                        break;
                    default:
                        return new McpDispatchResult { Body = SerializeError(id, MethodNotFound, $"Method not found: {method}") };
                }

                if (id == null)
                {
                    // Request without id is a notification per JSON-RPC; do not reply.
                    return new McpDispatchResult { StatusHint = 204 };
                }

                var response = new JObject
                {
                    ["jsonrpc"] = JsonRpcVersion,
                    ["id"] = id,
                    ["result"] = result,
                };
                return new McpDispatchResult
                {
                    Body = response.ToString(Formatting.None),
                    SessionId = newSessionId,
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.Exception(typeof(JsonRpcDispatcher).FullName, ex, $"[JsonRpcDispatcher] Unhandled error dispatching '{method}': {ex.Message}");
                return new McpDispatchResult { Body = SerializeError(id, InternalError, $"Internal error: {ex.Message}") };
            }
            finally
            {
                if (admitted)
                {
                    this.admittedWork.Release();
                }

                scope?.Dispose();
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            // 'admittedWork' is intentionally not disposed: an in-flight DispatchAsync
            // still releases it from its finally block, so disposing here turns that
            // release into an ObjectDisposedException whenever the server is stopped
            // mid-request (e.g. the hosting component's document is unloaded or the
            // component is disabled while a call is running). SemaphoreSlim only
            // allocates unmanaged resources when AvailableWaitHandle is accessed,
            // which never happens here.
        }

        private static bool RequiresAdmission(string method)
        {
            switch (method)
            {
                case "tools/call":
                case "resources/list":
                case "resources/read":
                case "prompts/list":
                case "prompts/get":
                    return true;
                default:
                    return false;
            }
        }

        private JObject HandleInitialize()
        {
            return new JObject
            {
                ["protocolVersion"] = ProtocolVersion,
                ["capabilities"] = new JObject
                {
                    ["tools"] = new JObject { ["listChanged"] = false },
                    ["resources"] = new JObject { ["listChanged"] = false },
                    ["prompts"] = new JObject { ["listChanged"] = false },
                },
                ["serverInfo"] = new JObject
                {
                    ["name"] = this.options.ServerName,
                    ["version"] = this.options.ServerVersion ?? GetAssemblyVersion(),
                },
                ["instructions"] =
                    "This server is hosted by a SmartHopper MCP Server component inside a Grasshopper document. " +
                    "The session lives only while that document is shown in the Grasshopper canvas: activating " +
                    "a different document (gh_document_new, gh_document_switch, gh_document_open, or manually " +
                    "in the editor) unloads the host document, stops this server, and drops the connection " +
                    "mid-call. Prefer documentId-scoped tools such as gh_get and gh_document_list instead of " +
                    "switching the active document; gh_document_list reports which document hosts the server " +
                    "via hostsMcpServer.",
            };
        }

        private JObject HandleToolsList()
        {
            var descriptors = this.adapter.BuildDescriptors();
            var list = new JArray();
            foreach (var d in descriptors)
            {
                list.Add(d.ToMcpJson());
            }

            return new JObject { ["tools"] = list };
        }

        private async Task<JToken> HandleToolsCallAsync(JObject? paramsToken, McpSession? session, CancellationToken cancellationToken)
        {
            if (paramsToken == null)
            {
                return BuildToolError("Missing 'params'");
            }

            var toolName = paramsToken.Value<string?>("name");
            if (string.IsNullOrWhiteSpace(toolName))
            {
                return BuildToolError("Missing 'name'");
            }

            var argumentsToken = paramsToken["arguments"];
            JObject? arguments;
            if (argumentsToken is JObject obj)
            {
                arguments = obj;
            }
            else if (argumentsToken == null || argumentsToken.Type == JTokenType.Null)
            {
                arguments = new JObject();
            }
            else
            {
                return BuildToolError("'arguments' must be a JSON object");
            }

            var result = await this.adapter.ExecuteAsync(toolName!, arguments, session, cancellationToken).ConfigureAwait(false);
            return BuildToolCallEnvelope(result);
        }

        private async Task<JToken> HandleResourcesListAsync()
        {
            if (this.resourceProvider == null)
            {
                return new JObject { ["resources"] = new JArray() };
            }

            var resources = await this.resourceProvider.ListResourcesAsync().ConfigureAwait(false);
            var list = new JArray();
            foreach (var resource in resources)
            {
                list.Add(new JObject
                {
                    ["uri"] = resource.Uri.ToString(),
                    ["name"] = resource.Name,
                    ["mimeType"] = resource.MimeType,
                    ["description"] = resource.Description ?? string.Empty,
                });
            }

            return new JObject { ["resources"] = list };
        }

        private async Task<JToken> HandleResourcesReadAsync(JObject? paramsToken)
        {
            if (this.resourceProvider == null)
            {
                return new JObject { ["contents"] = new JArray() };
            }

            var uriString = paramsToken?["uri"]?.ToString();
            if (string.IsNullOrWhiteSpace(uriString) || !Uri.TryCreate(uriString, UriKind.RelativeOrAbsolute, out var uri))
            {
                return BuildResourceError("Missing or invalid 'uri'");
            }

            var resource = await this.resourceProvider.GetResourceAsync(uri).ConfigureAwait(false);
            if (resource == null)
            {
                return BuildResourceError($"Resource not found: {uri}");
            }

            var text = await resource.GetTextAsync().ConfigureAwait(false);
            var contents = new JArray
            {
                new JObject
                {
                    ["uri"] = resource.Uri.ToString(),
                    ["mimeType"] = resource.MimeType,
                    ["text"] = text ?? string.Empty,
                },
            };

            return new JObject { ["contents"] = contents };
        }

        private async Task<JToken> HandlePromptsListAsync()
        {
            if (this.promptProvider == null)
            {
                return new JObject { ["prompts"] = new JArray() };
            }

            var prompts = await this.promptProvider.ListPromptsAsync().ConfigureAwait(false);
            var list = new JArray();
            foreach (var prompt in prompts)
            {
                list.Add(new JObject
                {
                    ["name"] = prompt.Name,
                    ["description"] = prompt.Description,
                });
            }

            return new JObject { ["prompts"] = list };
        }

        private async Task<JToken> HandlePromptsGetAsync(JObject? paramsToken)
        {
            if (this.promptProvider == null)
            {
                return BuildResourceError("Prompts are not available");
            }

            var name = paramsToken?["name"]?.ToString();
            if (string.IsNullOrWhiteSpace(name))
            {
                return BuildResourceError("Missing 'name'");
            }

            if (!Uri.TryCreate($"prompts:///{name}", UriKind.Absolute, out var uri))
            {
                return BuildResourceError($"Invalid prompt name: {name}");
            }

            var prompt = await this.promptProvider.GetPromptAsync(uri).ConfigureAwait(false);
            if (prompt == null)
            {
                return BuildResourceError($"Prompt not found: {name}");
            }

            var messages = await prompt.GetMessagesAsync().ConfigureAwait(false);
            var messageArray = new JArray();
            foreach (var message in messages)
            {
                messageArray.Add(new JObject
                {
                    ["role"] = message.Role,
                    ["content"] = new JObject
                    {
                        ["type"] = message.ContentType,
                        ["text"] = message.Text,
                    },
                });
            }

            return new JObject
            {
                ["description"] = prompt.Description,
                ["messages"] = messageArray,
            };
        }

        private static JObject BuildResourceError(string message)
        {
            return new JObject
            {
                ["error"] = message ?? string.Empty,
            };
        }

        internal static JObject BuildToolCallEnvelope(McpToolCallResult result)
        {
            var content = new JArray();

            // Typed tool-result images: one native MCP image block per model-visible
            // image, ordered as produced; page/id descriptors stay in the text payload —
            // no base64 is ever mined from or embedded into JSON.
            var modelVisible = result.Images?.Where(i =>
                i?.SendToModel == true
                && !string.IsNullOrEmpty(i.ImageData)
                && i.MimeType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true)
                .ToList() ?? new List<ToolResultImage>();

            // The structured-content source is the payload itself on the typed path and
            // the binary-stripped metadata on the legacy screenshot path.
            JToken structuredSource = result.Payload;

            if (!result.IsError && modelVisible.Count > 0)
            {
                foreach (var image in modelVisible)
                {
                    content.Add(new JObject
                    {
                        ["type"] = "image",
                        ["data"] = image.ImageData,
                        ["mimeType"] = image.MimeType,
                    });
                }

                content.Add(new JObject
                {
                    ["type"] = "text",
                    ["text"] = result.Payload.ToString(Formatting.None),
                });
            }
            // Legacy single-screenshot payload mapper retained for old producers.
            else if (!result.IsError && TryBuildImageContent(result.Payload, out var imageBlock, out var metadataPayload))
            {
                structuredSource = metadataPayload!;
                content.Add(imageBlock);
                content.Add(new JObject
                {
                    ["type"] = "text",
                    ["text"] = metadataPayload!.ToString(Formatting.None),
                });
            }
            else
            {
                content.Add(new JObject
                {
                    ["type"] = "text",
                    ["text"] = result.Payload.ToString(Formatting.None),
                });
            }

            // Compact structured metadata — always an object; scalar/array payloads
            // wrap under "result" without changing the text payload. No image bytes
            // are carried on any path.
            var structured = structuredSource is JObject structuredObj
                ? (JObject)structuredObj.DeepClone()
                : new JObject { ["result"] = structuredSource };
            structured.Remove("imageBase64");
            structured.Remove("base64Data");
            if (modelVisible.Count > 0)
            {
                structured["nativeImageOrder"] = new JArray(
                    modelVisible.Select(i => i.SourceReference?.AssetId ?? string.Empty));
            }

            // Forwarded tool diagnostics reach the model in two places: a typed
            // _meta projection for machines and an extra text block for clients
            // that only surface content. Metadata alone is not model-visible.
            if (result.Diagnostics != null && result.Diagnostics.Count > 0)
            {
                var diagnosticsJson = new JArray(result.Diagnostics.Select(d => d.ToJson()));
                content.Add(new JObject
                {
                    ["type"] = "text",
                    ["text"] = new JObject { ["diagnostics"] = diagnosticsJson }.ToString(Formatting.None),
                });
            }

            var envelope = new JObject
            {
                ["content"] = content,
                ["isError"] = result.IsError,
                ["structuredContent"] = structured,
            };

            // Helper/usage metrics and diagnostics forwarded verbatim under _meta —
            // survives error results and stays out of text/structuredContent.
            JObject? meta = null;
            if (result.ExtraMetrics != null && result.ExtraMetrics.Count > 0)
            {
                meta ??= new JObject();
                meta["smarthopper/helperUsage"] = JArray.FromObject(result.ExtraMetrics);
            }

            if (result.Diagnostics != null && result.Diagnostics.Count > 0)
            {
                meta ??= new JObject();
                meta["smarthopper/diagnostics"] = new JArray(result.Diagnostics.Select(d => d.ToJson()));
            }

            if (meta != null)
            {
                envelope["_meta"] = meta;
            }

            return envelope;
        }

        /// <summary>
        /// Detects a tool payload carrying a base64 image and splits it into an MCP
        /// <c>image</c> content block plus a text block with the remaining fields.
        /// </summary>
        private static bool TryBuildImageContent(JToken? payload, out JObject? imageBlock, out JToken? metadataPayload)
        {
            imageBlock = null;
            metadataPayload = null;
            if (payload is not JObject obj)
            {
                return false;
            }

            var data = obj["imageBase64"]?.ToString();
            var mimeType = obj["mimeType"]?.ToString();
            if (string.IsNullOrWhiteSpace(data)
                || string.IsNullOrWhiteSpace(mimeType)
                || !mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            imageBlock = new JObject
            {
                ["type"] = "image",
                ["data"] = data,
                ["mimeType"] = mimeType,
            };

            var metadata = (JObject)obj.DeepClone();
            metadata.Remove("imageBase64");
            metadataPayload = metadata;
            return true;
        }

        private static JObject BuildToolError(string message)
        {
            var content = new JArray
            {
                new JObject
                {
                    ["type"] = "text",
                    ["text"] = message ?? string.Empty,
                },
            };
            return new JObject
            {
                ["content"] = content,
                ["isError"] = true,
            };
        }

        private static string SerializeError(JToken? id, int code, string message)
        {
            var response = new JObject
            {
                ["jsonrpc"] = JsonRpcVersion,
                ["id"] = id ?? JValue.CreateNull(),
                ["error"] = new JObject
                {
                    ["code"] = code,
                    ["message"] = message ?? string.Empty,
                },
            };
            return response.ToString(Formatting.None);
        }

        private static string GetAssemblyVersion()
        {
            var asm = typeof(JsonRpcDispatcher).Assembly;
            var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(info))
            {
                return info!;
            }

            return asm.GetName().Version?.ToString() ?? "0.0.0";
        }
    }
}
