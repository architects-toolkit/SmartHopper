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

namespace SmartHopper.Core.ComponentBase.Contracts
{
    /// <summary>
    /// Centralized registry of Grasshopper <c>GH_IReader</c> / <c>GH_IWriter</c> keys used
    /// by the <c>ComponentBase</c> hierarchy to persist component state in saved files.
    /// </summary>
    /// <remarks>
    /// Renaming any of these constants is a breaking change for existing <c>.gh</c>
    /// documents. Legacy keys are kept as <c>Legacy*</c> constants to support
    /// backwards-compatible reads.
    /// </remarks>
    internal static class PersistenceKeys
    {
        // ----- StatefulComponentBase -----

        /// <summary>Prefix for committed input hash entries, suffixed with the parameter name.</summary>
        public const string InputHashPrefix = "InputHash_";

        /// <summary>Prefix for committed input branch-count entries, suffixed with the parameter name.</summary>
        public const string InputBranchCountPrefix = "InputBranchCount_";

        // ----- ProviderSelectionCore -----

        /// <summary>Key used by provider bases to persist the selected provider name.</summary>
        public const string SelectedProvider = "AIProvider";

        // ----- SelectingComponentCore -----

        /// <summary>Number of persisted selected-object GUIDs.</summary>
        public const string SelectedObjectsCount = "SelectedObjectsCount";

        /// <summary>Prefix for individual persisted selected-object GUID entries, suffixed with the index.</summary>
        public const string SelectedObjectPrefix = "SelectedObject_";

        // ----- AIStatefulAsyncComponentBase batch state -----

        /// <summary>Provider-assigned batch identifier.</summary>
        public const string BatchId = "BatchId";

        /// <summary>Name of the provider that owns the batch.</summary>
        public const string BatchProvider = "BatchProvider";

        /// <summary>Serialized request payload used to submit the batch.</summary>
        public const string BatchRequest = "BatchRequest";

        /// <summary>ISO-8601 timestamp of batch submission.</summary>
        public const string BatchSubmittedAt = "BatchSubmittedAt";

        /// <summary>JSON array of custom IDs included in the batch.</summary>
        public const string BatchCustomIds = "BatchCustomIds";

        /// <summary>Legacy single-custom-ID entry (pre-multi-item batch support).</summary>
        public const string LegacyBatchCustomId = "BatchCustomId";

        /// <summary>JSON array of sentinel IDs for result reconstruction.</summary>
        public const string BatchSentinelIds = "BatchSentinelIds";

        /// <summary>JSON object mapping output name → serialized sentinel tree.</summary>
        public const string BatchSentinelTrees = "BatchSentinelTrees";
    }
}
