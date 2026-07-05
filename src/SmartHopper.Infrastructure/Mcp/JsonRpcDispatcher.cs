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
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SmartHopper.Infrastructure.Mcp
{
    /// <summary>
    /// Translates JSON-RPC 2.0 envelopes into MCP method invocations against the
    /// <see cref="AIToolMcpAdapter"/>. Concurrency is serialised by a
    /// <see cref="SemaphoreSlim"/> so multiple HTTP clients cannot race on the
    /// Grasshopper document (see <c>docs/Architecture/mcp-server.md</c> §6).
    /// </summary>
    public sealed class JsonRpcDispatcher : IDisposable
    {
        private const string ProtocolVersion = "2025-03-26";
        private const string JsonRpcVersion = "2.0";

        // JSON-RPC error codes per https://www.jsonrpc.org/specification.
        private const int ParseError = -32700;
        private const int InvalidRequest = -32600;
        private const int MethodNotFound = -32601;
        private const int InvalidParams = -32602;
        private const int InternalError = -32603;

        private readonly McpServerOptions options;
        private readonly AIToolMcpAdapter adapter;
        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonRpcDispatcher"/> class.
        /// </summary>
        public JsonRpcDispatcher(McpServerOptions options, AIToolMcpAdapter adapter)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            this.adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        }

        /// <summary>
        /// Parses a raw JSON-RPC request body and produces a serialized response body.
        /// Returns <c>null</c> for notifications (no <c>id</c> in the request).
        /// </summary>
        /// <param name="requestBody">Raw request payload.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        public async Task<string?> DispatchAsync(string requestBody, CancellationToken cancellationToken = default)
        {
            JObject? request;
            try
            {
                request = JObject.Parse(requestBody ?? string.Empty);
            }
            catch (JsonReaderException ex)
            {
                return SerializeError(null, ParseError, $"Parse error: {ex.Message}");
            }

            var id = request["id"];
            var method = request.Value<string?>("method");
            var paramsToken = request["params"] as JObject;

            if (string.IsNullOrWhiteSpace(method))
            {
                return SerializeError(id, InvalidRequest, "Missing 'method'");
            }

            await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                JToken result;
                switch (method)
                {
                    case "initialize":
                        result = this.HandleInitialize();
                        break;
                    case "notifications/initialized":
                    case "initialized":
                        // Notification — no response.
                        return null;
                    case "ping":
                        result = new JObject();
                        break;
                    case "tools/list":
                        result = this.HandleToolsList();
                        break;
                    case "tools/call":
                        result = await this.HandleToolsCallAsync(paramsToken).ConfigureAwait(false);
                        break;
                    default:
                        return SerializeError(id, MethodNotFound, $"Method not found: {method}");
                }

                if (id == null)
                {
                    // Request without id is a notification per JSON-RPC; do not reply.
                    return null;
                }

                var response = new JObject
                {
                    ["jsonrpc"] = JsonRpcVersion,
                    ["id"] = id,
                    ["result"] = result,
                };
                return response.ToString(Formatting.None);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return SerializeError(id, InternalError, $"Internal error: {ex.Message}");
            }
            finally
            {
                this.gate.Release();
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.gate.Dispose();
        }

        private JObject HandleInitialize()
        {
            return new JObject
            {
                ["protocolVersion"] = ProtocolVersion,
                ["capabilities"] = new JObject
                {
                    ["tools"] = new JObject { ["listChanged"] = false },
                },
                ["serverInfo"] = new JObject
                {
                    ["name"] = this.options.ServerName,
                    ["version"] = this.options.ServerVersion ?? GetAssemblyVersion(),
                },
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

        private async Task<JToken> HandleToolsCallAsync(JObject? paramsToken)
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

            var result = await this.adapter.ExecuteAsync(toolName!, arguments).ConfigureAwait(false);
            return BuildToolCallEnvelope(result);
        }

        private static JObject BuildToolCallEnvelope(McpToolCallResult result)
        {
            var content = new JArray
            {
                new JObject
                {
                    ["type"] = "text",
                    ["text"] = result.Payload.ToString(Formatting.None),
                },
            };

            return new JObject
            {
                ["content"] = content,
                ["isError"] = result.IsError,
            };
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
