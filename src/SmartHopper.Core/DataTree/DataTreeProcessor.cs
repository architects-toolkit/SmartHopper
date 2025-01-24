/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using System.Collections.Generic;
using System.Linq;

namespace SmartHopper.Core.DataTree
{
    /// <summary>
    /// Provides utility methods for processing Grasshopper data trees
    /// </summary>
    public static class DataTreeProcessor
    {
        /// <summary>
        /// Filters a data tree based on specified indices for each branch
        /// </summary>
        //public static GH_Structure<IGH_Goo> FilterByIndices(GH_Structure<IGH_Goo> inputTree, int[] indices)
        //{
        //    var result = new GH_Structure<IGH_Goo>();

        //    foreach (var path in inputTree.Paths)
        //    {
        //        var branch = inputTree.get_Branch(path);
        //        var filteredItems = new List<IGH_Goo>();

        //        foreach (var index in indices)
        //        {
        //            if (index >= 0 && index < branch.Count)
        //            {
        //                if (branch[index] is IGH_Goo gooItem)
        //                {
        //                    filteredItems.Add(gooItem);
        //                }
        //            }
        //        }

        //        // Always append the branch, even if empty
        //        result.AppendRange(filteredItems, path);
        //    }

        //    return result;
        //}

        /// <summary>
        /// Gets a branch from a data tree, handling cases where the tree is flat (single value or list) or structured.
        /// If the tree is flat, returns the first branch for any path. If structured, returns the branch at the matching path.
        /// Returns empty list if no matching branch is found and preserveStructure is true.
        /// </summary>
        /// <param name="tree">The input data tree</param>
        /// <param name="path">The path to get the branch from</param>
        /// <param name="preserveStructure">If true, returns empty list for non-existing paths. If false, returns null.</param>
        public static List<T> GetBranchFromTree<T>(GH_Structure<T> tree, GH_Path path, bool preserveStructure = true) where T : IGH_Goo
        {
            if (tree == null)
                return preserveStructure ? new List<T>() : null;

            // Handle empty tree
            if (tree.DataCount == 0)
                return preserveStructure ? new List<T>() : null;

            // Get the branch at the specified path
            var branch = tree.get_Branch(path);
            if (branch != null && branch.Count > 0)
                return branch.Cast<T>().ToList();

            // If no branch at path, check if tree is flat (single branch)
            if (tree.PathCount == 1)
            {
                var singleBranch = tree.Branches.FirstOrDefault();
                if (singleBranch != null)
                    return singleBranch.Cast<T>().ToList();
            }

            // Return empty list or null based on preserveStructure flag
            return preserveStructure ? new List<T>() : null;
        }

        ///// <summary>
        ///// Gets a value from a data tree, handling cases where the tree is flat (single value or list) or structured.
        ///// If the tree is flat, returns the first value for any path. If structured, returns the value at the matching path.
        ///// </summary>
        //public static T GetValueFromTree<T>(GH_Structure<T> tree, GH_Path path) where T : IGH_Goo
        //{
        //    if (tree == null || tree.DataCount == 0)
        //        return default;

        //    // Get the branch at the specified path
        //    var branch = tree.get_Branch(path);
        //    if (branch == null || branch.Count == 0)
        //    {
        //        // If the target branch is empty, only use default value if tree has a non-empty branch
        //        var hasNonEmptyBranch = tree.Paths.Any(p =>
        //        {
        //            var b = tree.get_Branch(p);
        //            return b != null && b.Count > 0;
        //        });

        //        if (!hasNonEmptyBranch)
        //            return default;

        //        // If tree has no paths (flat list) or only root path, use the first value for all paths
        //        if (!tree.Paths.Any() || (tree.PathCount == 1 && tree.Paths[0].ToString() == "{0}"))
        //        {
        //            return tree.AllData(true).Cast<T>().FirstOrDefault();
        //        }
        //    }

        //    return branch?.Count > 0 ? (T)branch[0] : default;
        //}

        /// <summary>
        /// Checks if a branch in a data tree should be considered empty
        /// </summary>
        //public static bool IsBranchEmpty<T>(GH_Structure<T> tree, GH_Path path) where T : IGH_Goo
        //{
        //    if (tree == null) return true;
        //    var branch = tree.get_Branch(path);
        //    return branch == null || branch.Count == 0;
        //}

        /// <summary>
        /// Returns all unique paths that exist in ANY of the provided data trees.
        /// </summary>
        /// <param name="trees">List of data trees to combine</param>
        /// <returns>List of all unique paths found across all trees</returns>
        public static List<GH_Path> GetAllUniquePaths<T>(IEnumerable<GH_Structure<T>> trees) where T : IGH_Goo
        {
            if (trees == null || !trees.Any())
                return new List<GH_Path>();

            var uniquePaths = new List<GH_Path>();

            // Collect all paths from all trees
            foreach (var tree in trees)
            {
                foreach (var path in tree.Paths)
                {
                    // Check if the path string representation is already in the list
                    if (!uniquePaths.Any(p => p.ToString() == path.ToString()))
                    {
                        uniquePaths.Add(path);
                    }
                }
            }

            return uniquePaths;
        }

        /// <summary>
        /// Returns all paths that exist in ALL provided data trees (intersection).
        /// </summary>
        /// <param name="trees">List of data trees to compare</param>
        /// <returns>List of paths that exist in all trees</returns>
        public static List<GH_Path> GetMatchingPaths<T>(IEnumerable<GH_Structure<T>> trees) where T : IGH_Goo
        {
            if (trees == null || !trees.Any())
                return new List<GH_Path>();

            // Get the first tree's paths as the starting set
            var matchingPaths = trees.First().Paths.ToList();

            // Intersect with paths from all other trees
            foreach (var tree in trees.Skip(1))
            {
                matchingPaths = matchingPaths
                    .Where(path => tree.PathExists(path))
                    .ToList();

                // Early exit if no matching paths found
                if (!matchingPaths.Any())
                    break;
            }

            return matchingPaths;
        }

    }
}
