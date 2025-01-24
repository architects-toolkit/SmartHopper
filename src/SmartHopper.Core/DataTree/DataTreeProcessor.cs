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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SmartHopper.Core.DataTree
{
    /// <summary>
    /// Provides utility methods for processing Grasshopper data trees
    /// </summary>
    public static class DataTreeProcessor
    {
        #region BRANCH

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

        #endregion

        #region PATHS

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

        /// <summary>
        /// Gets paths from trees based on the onlyMatchingPaths parameter
        /// </summary>
        private static List<GH_Path> GetProcessingPaths<T>(IEnumerable<GH_Structure<T>> trees, bool onlyMatchingPaths = false) where T : IGH_Goo
        {
            return onlyMatchingPaths ?
                GetMatchingPaths(trees) :
                GetAllUniquePaths(trees);
        }

        /// <summary>
        /// Gets paths from a dictionary of trees based on the onlyMatchingPaths parameter, ignoring the dictionary keys
        /// </summary>
        private static List<GH_Path> GetProcessingPaths<T>(Dictionary<string, GH_Structure<T>> trees, bool onlyMatchingPaths = false) where T : IGH_Goo
        {
            return GetProcessingPaths(trees.Values, onlyMatchingPaths);
        }

        #endregion

        #region EXEC

        public static async Task<Dictionary<string, GH_Structure<T>>> RunFunctionAsync<T>(
            Dictionary<string, GH_Structure<T>> trees,
            Func<Dictionary<string, List<T>>, Task<Dictionary<string, List<T>>>> function,
            bool onlyMatchingPaths = false,
            bool groupIdenticalBranches = false,
            CancellationToken token = default) where T : GH_String
        {
            Dictionary<string, GH_Structure<T>> result = new Dictionary<string, GH_Structure<T>>();

            // Get the amount of items in each tree
            var treeLengths = new Dictionary<string, int>();
            foreach (var tree in trees)
            {
                treeLengths.Add(tree.Key, tree.Value.Count());
            }

            Debug.WriteLine($"[DataTreeProcessor] Tree lengths: {string.Join(", ", treeLengths.Select(x => $"{x.Key}: {x.Value}"))}");

            var allPaths = GetProcessingPaths(trees, onlyMatchingPaths);

            foreach (var path in allPaths)
            {
                Debug.WriteLine($"[DataTreeProcessor] Processing path: {path}");

                // Check for cancellation
                token.ThrowIfCancellationRequested();

                // Initialize paths to apply
                var pathsToApply = new List<GH_Path> { path };
                
                // For each tree, get the branch corresponding to the path, preserving the original dictionary keys
                var branches = trees
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => GetBranchFromTree(kvp.Value, path, preserveStructure: true)
                    );

                // Apply the function to the current branch
                var branchResult = await function(branches);
                
                // If groupIdenticalBranches is true, find identical combination of branches, return the paths
                // Allow this only for types GH_String, GH_Number, GH_Integer and GH_Boolean, which are the comparable ones
                if (groupIdenticalBranches &&
                    (typeof(T) == typeof(GH_String) ||
                     typeof(T) == typeof(GH_Number) ||
                     typeof(T) == typeof(GH_Integer) ||
                     typeof(T) == typeof(GH_Boolean)
                     )
                    )
                {
                    pathsToApply.AddRange(FindIdenticalBranches(trees, branches, path));
                }

                // For each path in pathsToApply, convert the branch result to a GH_Structure<T> with the appropriate paths
                foreach (var applyPath in pathsToApply)
                {
                    foreach (var kvp in branchResult)
                    {
                        if (!result.ContainsKey(kvp.Key))
                        {
                            result[kvp.Key] = new GH_Structure<T>();
                            Debug.WriteLine($"[DataTreeProcessor] Created new structure for key: {kvp.Key}");
                        }
                        
                        if (kvp.Value != null)
                        {
                            Debug.WriteLine($"[DataTreeProcessor] Appending {kvp.Value.Count} items to path {applyPath} for key {kvp.Key}");
                            result[kvp.Key].AppendRange(kvp.Value, applyPath);
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Finds paths that have identical branch data for a given set of trees and current branches
        /// </summary>
        /// <param name="trees">The complete dictionary of trees</param>
        /// <param name="currentBranches">The current branches being processed</param>
        /// <returns>List of paths that have identical branch data</returns>
        private static List<GH_Path> FindIdenticalBranches<T>(
            Dictionary<string, GH_Structure<T>> trees,
            Dictionary<string, List<T>> currentBranches,
            GH_Path currentPath) where T : GH_String
        {
            var result = new List<GH_Path>();
            var currentKey = GetBranchesKey(currentBranches);
            var allPaths = GetAllUniquePaths(trees.Values);

            foreach (var path in allPaths)
            {
                // Avoid comparing the current path
                if (path == currentPath)
                    continue;
                
                // Get branches for this path from all trees
                var siblingBranches = trees.ToDictionary(
                    kvp => kvp.Key,
                    kvp => GetBranchFromTree(kvp.Value, path, preserveStructure: true)
                );

                // Compare the branch data
                if (GetBranchesKey(siblingBranches) == currentKey)
                {
                    result.Add(path);
                }
            }

            return result;
        }

        /// <summary>
        /// Generates a unique key for a set of branches based on their content
        /// </summary>
        private static string GetBranchesKey<T>(Dictionary<string, List<T>> branches) where T : GH_String
        {
            var keyParts = branches
                .OrderBy(kvp => kvp.Key)
                .Select(kvp => 
                {
                    var branch = kvp.Value;
                    var branchData = branch.Select(item => item.ToString());
                    return $"{kvp.Key}:{string.Join(",", branchData)}";
                });

            return string.Join("|", keyParts);
        }

        #endregion

        #region NORMALIZATION

        /// <summary>
        /// Normalizes branch lengths by extending shorter branches with their last item
        /// </summary>
        /// <typeparam name="T">Type of items in the branches</typeparam>
        /// <param name="branches">Collection of branches to normalize</param>
        /// <returns>List of normalized branches with equal length</returns>
        public static List<List<T>> NormalizeBranchLengths<T>(IEnumerable<List<T>> branches) where T : IGH_Goo
        {
            if (branches == null || !branches.Any())
                return new List<List<T>>();

            var branchesList = branches.ToList();

            // Find the maximum length among all branches
            int maxLength = branchesList.Max(branch => branch?.Count ?? 0);

            var result = new List<List<T>>();
            foreach (var branch in branchesList)
            {
                var currentBranch = branch ?? new List<T>();
                var normalizedBranch = new List<T>(currentBranch);

                // If branch is empty, use null or default value for all positions
                if (currentBranch.Count == 0)
                {
                    for (int i = 0; i < maxLength; i++)
                    {
                        normalizedBranch.Add(default(T));
                    }
                }
                else
                {
                    // Extend branch by repeating last item
                    var lastItem = currentBranch[currentBranch.Count - 1];
                    while (normalizedBranch.Count < maxLength)
                    {
                        normalizedBranch.Add(lastItem);
                    }
                }

                result.Add(normalizedBranch);
            }

            return result;
        }

        #endregion
    }
}
