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
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using SmartHopper.Core.DataTree;
using Xunit;

namespace SmartHopper.Core.Tests.DataTree
{
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

        [Fact(DisplayName = "GH_Structure_WithStrings_CanBeCreated")]
        public void GH_Structure_WithStrings_CanBeCreated()
        {
            var tree = new GH_Structure<GH_String>();
            tree.Append(new GH_String("test"), new GH_Path(0));
            Assert.Single(tree.Paths);
            Assert.Equal(1, tree.DataCount);
        }

        [Fact(DisplayName = "GH_Structure_WithBooleans_CanBeCreated")]
        public void GH_Structure_WithBooleans_CanBeCreated()
        {
            var tree = new GH_Structure<GH_Boolean>();
            tree.Append(new GH_Boolean(true), new GH_Path(0));
            Assert.Single(tree.Paths);
            Assert.Equal(1, tree.DataCount);
        }

        [Fact(DisplayName = "GH_Structure_WithIntegers_CanBeCreated")]
        public void GH_Structure_WithIntegers_CanBeCreated()
        {
            var tree = new GH_Structure<GH_Integer>();
            tree.Append(new GH_Integer(42), new GH_Path(0));
            Assert.Single(tree.Paths);
            Assert.Equal(1, tree.DataCount);
        }

        [Fact(DisplayName = "GH_Structure_WithNumbers_CanBeCreated")]
        public void GH_Structure_WithNumbers_CanBeCreated()
        {
            var tree = new GH_Structure<GH_Number>();
            tree.Append(new GH_Number(3.14), new GH_Path(0));
            Assert.Single(tree.Paths);
            Assert.Equal(1, tree.DataCount);
        }

        [Fact(DisplayName = "GH_Path_ToString_MatchesExpected")]
        public void GH_Path_ToString_MatchesExpected()
        {
            var path1 = new GH_Path(0);
            var path2 = new GH_Path(1, 2);
            var path3 = new GH_Path(0, 0, 0);
            Assert.Equal("{0}", path1.ToString());
            Assert.Equal("{1;2}", path2.ToString());
            Assert.Equal("{0;0;0}", path3.ToString());
        }

        [Fact(DisplayName = "GH_Path_Equality_Works")]
        public void GH_Path_Equality_Works()
        {
            var path1 = new GH_Path(0);
            var path2 = new GH_Path(0);
            var path3 = new GH_Path(1);
            Assert.Equal(path1, path2);
            Assert.NotEqual(path1, path3);
        }

        [Fact(DisplayName = "GH_Structure_MultipleItems_SamePath")]
        public void GH_Structure_MultipleItems_SamePath()
        {
            var tree = new GH_Structure<GH_String>();
            tree.Append(new GH_String("a"), new GH_Path(0));
            tree.Append(new GH_String("b"), new GH_Path(0));
            tree.Append(new GH_String("c"), new GH_Path(0));
            Assert.Single(tree.Paths);
            Assert.Equal(3, tree.DataCount);
        }

        [Fact(DisplayName = "GH_Structure_MultipleItems_DifferentPaths")]
        public void GH_Structure_MultipleItems_DifferentPaths()
        {
            var tree = new GH_Structure<GH_String>();
            tree.Append(new GH_String("a"), new GH_Path(0));
            tree.Append(new GH_String("b"), new GH_Path(1));
            tree.Append(new GH_String("c"), new GH_Path(2));
            Assert.Equal(3, tree.Paths.Count);
            Assert.Equal(3, tree.DataCount);
        }

        [Fact(DisplayName = "GH_Structure_GetBranch_ReturnsCorrectBranch")]
        public void GH_Structure_GetBranch_ReturnsCorrectBranch()
        {
            var tree = new GH_Structure<GH_String>();
            tree.Append(new GH_String("a"), new GH_Path(0));
            tree.Append(new GH_String("b"), new GH_Path(0));
            tree.Append(new GH_String("c"), new GH_Path(1));
            var branch = tree.get_Branch(new GH_Path(0));
            Assert.Equal(2, branch.Count);
        }

        [Fact(DisplayName = "GH_Structure_PathCount_ReturnsCorrectCount")]
        public void GH_Structure_PathCount_ReturnsCorrectCount()
        {
            var tree = new GH_Structure<GH_String>();
            tree.Append(new GH_String("a"), new GH_Path(0));
            tree.Append(new GH_String("b"), new GH_Path(1));
            tree.Append(new GH_String("c"), new GH_Path(2));
            Assert.Equal(3, tree.PathCount);
        }

        [Fact(DisplayName = "GH_Structure_DataCount_ReturnsCorrectCount")]
        public void GH_Structure_DataCount_ReturnsCorrectCount()
        {
            var tree = new GH_Structure<GH_String>();
            tree.Append(new GH_String("a"), new GH_Path(0));
            tree.Append(new GH_String("b"), new GH_Path(0));
            tree.Append(new GH_String("c"), new GH_Path(1));
            Assert.Equal(3, tree.DataCount);
        }

        [Fact(DisplayName = "GH_Structure_Flatten_ReducesToSinglePath")]
        public void GH_Structure_Flatten_ReducesToSinglePath()
        {
            var tree = new GH_Structure<GH_String>();
            tree.Append(new GH_String("a"), new GH_Path(0));
            tree.Append(new GH_String("b"), new GH_Path(1));
            tree.Flatten();
            Assert.Single(tree.Paths);
            Assert.Equal(2, tree.DataCount);
        }

        [Fact(DisplayName = "GH_Structure_Graft_CreatesNestedPaths")]
        public void GH_Structure_Graft_CreatesNestedPaths()
        {
            var tree = new GH_Structure<GH_String>();
            tree.Append(new GH_String("a"), new GH_Path(0));
            tree.Append(new GH_String("b"), new GH_Path(0));
            tree.Graft(false);
            Assert.Equal(2, tree.PathCount);
        }

        [Fact(DisplayName = "GH_String_Constructor_WithValue")]
        public void GH_String_Constructor_WithValue()
        {
            var ghString = new GH_String("test value");
            Assert.Equal("test value", ghString.Value);
        }

        [Fact(DisplayName = "GH_Boolean_Constructor_WithValue")]
        public void GH_Boolean_Constructor_WithValue()
        {
            var ghBool = new GH_Boolean(true);
            Assert.True(ghBool.Value);
        }

        [Fact(DisplayName = "GH_Integer_Constructor_WithValue")]
        public void GH_Integer_Constructor_WithValue()
        {
            var ghInt = new GH_Integer(42);
            Assert.Equal(42, ghInt.Value);
        }

        [Fact(DisplayName = "GH_Number_Constructor_WithValue")]
        public void GH_Number_Constructor_WithValue()
        {
            var ghNum = new GH_Number(3.14);
            Assert.Equal(3.14, ghNum.Value);
        }
    }
}
