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

namespace SmartHopper.Core.Tests.DataTree
{
    using System;
    using SmartHopper.Core.DataTree;
    using Xunit;

    public class DataTreeProcessorMixedTypeTests
    {
        [Fact(DisplayName = "ProcessingTopology_EnumValues_AllPresent")]
        public void ProcessingTopology_EnumValues_AllPresent()
        {
            Assert.Equal(ProcessingTopology.ItemToItem, ProcessingTopology.ItemToItem);
            Assert.Equal(ProcessingTopology.ItemGraft, ProcessingTopology.ItemGraft);
            Assert.Equal(ProcessingTopology.BranchFlatten, ProcessingTopology.BranchFlatten);
            Assert.Equal(ProcessingTopology.BranchToBranch, ProcessingTopology.BranchToBranch);
        }

        [Fact(DisplayName = "ProcessingOptions_DefaultValues_MatchExpected")]
        public void ProcessingOptions_DefaultValues_MatchExpected()
        {
            var options = new ProcessingOptions();
            Assert.Equal(ProcessingTopology.ItemToItem, options.Topology);
            Assert.False(options.OnlyMatchingPaths);
            Assert.False(options.GroupIdenticalBranches);
        }

        [Fact(DisplayName = "ProcessingOptions_CanSetTopology")]
        public void ProcessingOptions_CanSetTopology()
        {
            var options = new ProcessingOptions { Topology = ProcessingTopology.ItemGraft };
            Assert.Equal(ProcessingTopology.ItemGraft, options.Topology);
        }

        [Fact(DisplayName = "ProcessingOptions_CanSetOnlyMatchingPaths")]
        public void ProcessingOptions_CanSetOnlyMatchingPaths()
        {
            var options = new ProcessingOptions { OnlyMatchingPaths = true };
            Assert.True(options.OnlyMatchingPaths);
        }

        [Fact(DisplayName = "ProcessingOptions_CanSetGroupIdenticalBranches")]
        public void ProcessingOptions_CanSetGroupIdenticalBranches()
        {
            var options = new ProcessingOptions { GroupIdenticalBranches = true };
            Assert.True(options.GroupIdenticalBranches);
        }
    }
}
