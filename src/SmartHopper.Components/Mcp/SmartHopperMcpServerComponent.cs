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
using System.Diagnostics;
using Grasshopper.Kernel;
using SmartHopper.Core.Grasshopper.Contracts;
using SmartHopper.Infrastructure.Mcp;

namespace SmartHopper.Components.Mcp
{
    /// <summary>
    /// Grasshopper component that starts an MCP HTTP server exposing SmartHopper's
    /// registered AI tools to external MCP clients (Claude Desktop, Cursor,
    /// VS Code, Claude Code, etc.).
    /// </summary>
    /// <remarks>
    /// The server lifecycle is owned by <see cref="McpServerLifecycle"/>. Multiple
    /// instances of this component on the same port share a single server. The
    /// server stops automatically when the last component is disabled or removed.
    /// See <c>docs/Architecture/mcp-server.md</c> for the full design.
    /// </remarks>
    public sealed class SmartHopperMcpServerComponent : GH_Component, ICanvasProtectedComponent
    {
        private int currentPort = McpServerOptions.DefaultPort;
        private bool acquired;
        private bool lastEnable;
        private string? lastToken;
        private bool lastExposeMutating;
        private string? lastStatus;

        /// <summary>
        /// Gets a value indicating whether this MCP server component currently has
        /// its <c>Enable</c> input set to true. When true, AI tools must not modify
        /// this component (or anything directly wired to it) so the client connection
        /// stays alive.
        /// </summary>
        public bool IsProtected => this.lastEnable;

        /// <summary>
        /// Initializes a new instance of the <see cref="SmartHopperMcpServerComponent"/> class.
        /// </summary>
        public SmartHopperMcpServerComponent()
            : base(
                "SmartHopper MCP Server",
                "MCP",
                "Exposes SmartHopper's AI tools to external Model Context Protocol clients (Claude Desktop, Cursor, VS Code, Claude Code) over a loopback HTTP server. Mutating tools are disabled by default; enable them explicitly via the input.",
                "SmartHopper",
                "MCP")
        {
        }

        /// <inheritdoc/>
        public override Guid ComponentGuid => new Guid("a3c4f1d0-7e2b-4c5a-9d1b-7f5e8c0a2b4d");

        /// <inheritdoc/>
        public override GH_Exposure Exposure => GH_Exposure.primary;

        /// <inheritdoc/>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter(
                "Enable",
                "E",
                "When true, starts a loopback MCP HTTP server. Set back to false to stop.",
                GH_ParamAccess.item,
                false);
            pManager.AddIntegerParameter(
                "Port",
                "P",
                "Loopback TCP port. Defaults to 26929.",
                GH_ParamAccess.item,
                McpServerOptions.DefaultPort);
            pManager[1].Optional = true;
            pManager.AddTextParameter(
                "Bearer Token",
                "T",
                "Optional bearer token. When set, requests without an 'Authorization: Bearer <token>' header are rejected with HTTP 401.",
                GH_ParamAccess.item,
                string.Empty);
            pManager[2].Optional = true;
            pManager.AddBooleanParameter(
                "Expose Mutating Tools",
                "M",
                "When true, tools that mutate the canvas/scripts (gh_put, gh_move, gh_group, script_edit, ...) are exposed. Defaults to false.",
                GH_ParamAccess.item,
                false);
            pManager[3].Optional = true;
        }

        /// <inheritdoc/>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Url", "U", "MCP endpoint URL once the server is running.", GH_ParamAccess.item);
            pManager.AddTextParameter("Status", "S", "Server status (Stopped | Running on ... | Error: ...).", GH_ParamAccess.item);
        }

        /// <inheritdoc/>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            bool enable = false;
            int port = McpServerOptions.DefaultPort;
            string token = string.Empty;
            bool exposeMutating = false;

            DA.GetData(0, ref enable);
            this.lastEnable = enable;
            DA.GetData(1, ref port);
            DA.GetData(2, ref token);
            DA.GetData(3, ref exposeMutating);

            try
            {
                this.ApplyToggle(enable, port, token, exposeMutating);
            }
            catch (Exception ex)
            {
                this.lastStatus = $"Error: {ex.Message}";
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, this.lastStatus);
                Debug.WriteLine($"[Mcp] Component error: {ex.Message}");
            }

            var server = this.acquired ? McpServerLifecycle.Find(this.currentPort) : null;
            DA.SetData(0, server != null ? server.Url : string.Empty);
            DA.SetData(1, this.lastStatus ?? (server != null ? $"Running on {server.Url}" : "Stopped"));
        }

        /// <inheritdoc/>
        public override void RemovedFromDocument(GH_Document document)
        {
            this.ReleaseIfHeld();
            base.RemovedFromDocument(document);
        }

        /// <inheritdoc/>
        public override void DocumentContextChanged(GH_Document document, GH_DocumentContext context)
        {
            if (context == GH_DocumentContext.Close || context == GH_DocumentContext.Unloaded)
            {
                this.ReleaseIfHeld();
            }

            base.DocumentContextChanged(document, context);
        }

        private void ApplyToggle(bool enable, int port, string token, bool exposeMutating)
        {
            string? normalizedToken = string.IsNullOrWhiteSpace(token) ? null : token;

            if (!enable)
            {
                this.ReleaseIfHeld();
                this.lastStatus = "Stopped";
                return;
            }

            if (this.acquired &&
                this.currentPort == port &&
                this.lastToken == normalizedToken &&
                this.lastExposeMutating == exposeMutating)
            {
                this.lastStatus = $"Running on {McpServerLifecycle.Find(port)?.Url}";
                return;
            }

            // Port or input values changed: release any previous holder before re-acquiring
            // so the server starts with the updated configuration.
            this.ReleaseIfHeld();

            var options = new McpServerOptions
            {
                Port = port,
                BearerToken = normalizedToken,
                ExposeMutatingTools = exposeMutating,
            };
            var server = McpServerLifecycle.Acquire(this, options);
            this.acquired = true;
            this.currentPort = port;
            this.lastToken = normalizedToken;
            this.lastExposeMutating = exposeMutating;
            this.lastStatus = $"Running on {server.Url}";
        }

        private void ReleaseIfHeld()
        {
            if (!this.acquired)
            {
                return;
            }

            McpServerLifecycle.Release(this, this.currentPort);
            this.acquired = false;
        }
    }
}
