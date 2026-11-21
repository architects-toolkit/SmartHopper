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

namespace SmartHopper.Infrastructure.FileSystem
{
    /// <summary>The kind of access a tool requests on a local path.</summary>
    [Flags]
    public enum LocalFileCapability
    {
        /// <summary>No capability.</summary>
        None = 0,

        /// <summary>Read file contents.</summary>
        Read = 1,

        /// <summary>Create, modify, or overwrite file contents.</summary>
        Write = 2,

        /// <summary>Open a document whose contents can execute logic (e.g. Grasshopper files that may run scripts).</summary>
        Execute = 4,
    }

    /// <summary>A file access decision, independent of canvas mutation approvals.</summary>
    public enum LocalFileAccessDecision
    {
        /// <summary>Do not touch this path.</summary>
        Deny,

        /// <summary>Allow this path and capability for the current tool invocation only.</summary>
        AllowOnce,

        /// <summary>Persist approval for this exact path and capability.</summary>
        AlwaysAllow,

        /// <summary>Persist approval for the parent directory subtree and capability.</summary>
        AlwaysAllowFolder,

        /// <summary>The prompt elapsed without an answer; treated as a refusal, never a grant.</summary>
        NoResponse,
    }

    /// <summary>Whether a persisted grant covers a single file or a directory subtree.</summary>
    public enum LocalFileGrantScope
    {
        /// <summary>The grant covers exactly the stored path.</summary>
        File,

        /// <summary>The grant covers the stored directory and everything under it.</summary>
        Directory,
    }

    /// <summary>One persisted user approval: a path (or folder) plus the capabilities granted on it.</summary>
    public sealed class LocalFileGrant
    {
        /// <summary>Gets or sets the grant scope.</summary>
        public LocalFileGrantScope Scope { get; set; }

        /// <summary>Gets or sets the canonical file or directory path.</summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>Gets or sets the capabilities approved for <see cref="Path"/>.</summary>
        public LocalFileCapability Capabilities { get; set; }

        /// <summary>Gets or sets when the grant was first stored.</summary>
        public DateTimeOffset GrantedUtc { get; set; }
    }

    /// <summary>A resolved file access request handed to the interactive prompt.</summary>
    public sealed class LocalFileAccessRequest
    {
        /// <summary>Gets or sets the canonical absolute path the tool wants to touch.</summary>
        public string CanonicalPath { get; set; } = string.Empty;

        /// <summary>Gets or sets the capability being requested.</summary>
        public LocalFileCapability Capability { get; set; }

        /// <summary>Gets or sets the AI tool asking for access, for display purposes.</summary>
        public string? ToolName { get; set; }

        /// <summary>Gets or sets a value indicating whether the target exists and would be overwritten.</summary>
        public bool WillOverwrite { get; set; }
    }
}
