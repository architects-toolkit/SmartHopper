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

        /// <summary>
        /// Finds paths that have identical branch data for a given set of trees and current branches
        /// </summary>
        /// <param name="trees">The complete dictionary of trees</param>
        /// <param name="currentBranches">The current branches being processed</param>
        /// <returns>List of paths that have identical branch data</returns>
        private static List<GH_Path> FindIdenticalBranches<T>(
            Dictionary<string, GH_Structure<T>> trees,
            Dictionary<string, List<T>> currentBranches,
            GH_Path currentPath,
            bool onlyMatchingPaths = false) where T : IGH_Goo
        {
            var result = new List<GH_Path>();
            var currentKey = GetBranchesKey(currentBranches);
            var allPaths = GetProcessingPaths(trees, onlyMatchingPaths).uniquePaths;

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
        private static string GetBranchesKey<T>(Dictionary<string, List<T>> branches) where T : IGH_Goo
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

        #region TREES

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
        /// Gets the amount of items in each tree, indexed by position
        /// </summary>
        private static Dictionary<int, int> TreesLength<T>(IEnumerable<GH_Structure<T>> trees) where T : IGH_Goo
        {
            var treeLengths = new Dictionary<int, int>();
            int index = 0;
            foreach (var tree in trees)
            {
                treeLengths.Add(index, tree.DataCount);
                index++;
            }
            return treeLengths;
        }

        /// <summary>
        /// Gets paths from trees based on the onlyMatchingPaths parameter and groups identical branches if requested
        /// </summary>
        /// <returns>A tuple containing the list of unique processing paths and a dictionary mapping paths to their identical branches</returns>
        private static (List<GH_Path> uniquePaths, Dictionary<GH_Path, List<GH_Path>> pathsToApplyMap) GetProcessingPaths<T>(
            Dictionary<string, GH_Structure<T>> trees, 
            bool onlyMatchingPaths = false,
            bool groupIdenticalBranches = false) where T : IGH_Goo
        {
            var allPaths = new List<GH_Path>();
            var pathsToApplyMap = new Dictionary<GH_Path, List<GH_Path>>();
            
            if (!onlyMatchingPaths)
            {
                // Get the amount of items in each tree
                var treeLengths = TreesLength(trees.Values);

                allPaths = GetAllUniquePaths(trees.Values);

                var firstTree = trees.Values.First();

                Debug.WriteLine($"[DataTreeProcessor] First tree with paths {string.Join(", ", firstTree.Paths)}");

                // If a tree only has one path, remove it from allPaths because it will be applied to all the other paths later, except when there is only one path (allPaths.Count > 1)
                if (allPaths.Count > 1)
                {
                    var singlePathTrees = new List<int>();

                    // Are there more than one tree with a single value?
                    if (treeLengths.Count(t => t.Value == 1) > 1)
                    {
                        singlePathTrees = treeLengths.Where(t => t.Value == 1 && t.Key != 0 /*Omit the first tree*/).Select(t => t.Key).ToList();
                    }
                    else
                    {
                        singlePathTrees = treeLengths.Where(t => t.Value == 1 /*Do not omit the first tree*/).Select(t => t.Key).ToList();
                    }

                    Debug.WriteLine($"[DataTreeProcessor] Single path trees: {string.Join(", ", singlePathTrees)}");

                    if (singlePathTrees.Any())
                    {
                        var singlePathTreePaths = singlePathTrees.Select(t => trees.Values.ElementAt(t).Paths.First()).ToList();
                        allPaths = allPaths.Where(p => !singlePathTreePaths.Contains(p)).ToList();
                    }
                }
            }

            var processingPaths = onlyMatchingPaths ? GetMatchingPaths(trees.Values) : allPaths;

            // If groupIdenticalBranches is true, find and group identical branches
            if (groupIdenticalBranches && 
                (typeof(T) == typeof(GH_String) ||
                 typeof(T) == typeof(GH_Number) ||
                 typeof(T) == typeof(GH_Integer) ||
                 typeof(T) == typeof(GH_Boolean)))
            {
                Debug.WriteLine($"[DataTreeProcessor] Starting identical branch grouping for {processingPaths.Count} paths");
                Debug.WriteLine($"[DataTreeProcessor] Initial paths: {string.Join(", ", processingPaths)}");
                
                // Initialize the dictionary with each path mapping to itself
                foreach (var path in processingPaths)
                {
                    pathsToApplyMap[path] = new List<GH_Path> { path };
                }

                // Track processed paths to avoid redundant processing
                var processedPaths = new HashSet<GH_Path>();
                // Track paths that should be removed from processing (they'll be handled by another path)
                var pathsToRemove = new HashSet<GH_Path>();

                // For each path, find identical branches and group them
                for (int i = 0; i < processingPaths.Count; i++)
                {
                    var currentPath = processingPaths[i];
                    
                    // Skip paths that are already processed as part of another group
                    if (pathsToRemove.Contains(currentPath))
                    {
                        Debug.WriteLine($"[DataTreeProcessor] Skipping path {currentPath} as it's already assigned to another group");
                        continue;
                    }
                        
                    // Get branches for current path from all trees
                    var currentBranches = trees.ToDictionary(
                        kvp => kvp.Key,
                        kvp => GetBranchFromTree(kvp.Value, currentPath, preserveStructure: true)
                    );
                    
                    var currentKey = GetBranchesKey(currentBranches);
                    Debug.WriteLine($"[DataTreeProcessor] Checking path {currentPath} with key: {currentKey}");
                    
                    // Look for other paths with identical branch data
                    for (int j = i + 1; j < processingPaths.Count; j++)
                    {
                        var siblingPath = processingPaths[j];
                        
                        // Skip paths that are already processed
                        if (pathsToRemove.Contains(siblingPath))
                            continue;
                        
                        // Get branches for this path from all trees
                        var siblingBranches = trees.ToDictionary(
                            kvp => kvp.Key,
                            kvp => GetBranchFromTree(kvp.Value, siblingPath, preserveStructure: true)
                        );
                        
                        var siblingKey = GetBranchesKey(siblingBranches);
                        
                        // If branches are identical, add to the group and mark as processed
                        if (siblingKey == currentKey)
                        {
                            Debug.WriteLine($"[DataTreeProcessor] Path {siblingPath} is identical to {currentPath}");
                            pathsToApplyMap[currentPath].Add(siblingPath);
                            pathsToRemove.Add(siblingPath);
                            
                            // Remove the sibling path from the pathsToApplyMap as a key
                            // since it will be processed as part of the current path's group
                            if (pathsToApplyMap.ContainsKey(siblingPath))
                            {
                                Debug.WriteLine($"[DataTreeProcessor] Removing {siblingPath} from pathsToApplyMap keys");
                                pathsToApplyMap.Remove(siblingPath);
                            }
                        }
                        else
                        {
                            Debug.WriteLine($"[DataTreeProcessor] Path {siblingPath} is NOT identical to {currentPath}");
                            Debug.WriteLine($"[DataTreeProcessor] - Key for {siblingPath}: {siblingKey}");
                        }
                    }
                    
                    processedPaths.Add(currentPath);
                }
                
                // Remove paths that are processed as part of another path's group
                foreach (var pathToRemove in pathsToRemove)
                {
                    if (processingPaths.Contains(pathToRemove))
                    {
                        Debug.WriteLine($"[DataTreeProcessor] Removing {pathToRemove} from processing paths");
                        processingPaths.Remove(pathToRemove);
                    }
                }
                
                Debug.WriteLine($"[DataTreeProcessor] After grouping, path map:");

                foreach (var kvp in pathsToApplyMap)
                {
                    Debug.WriteLine($"[DataTreeProcessor] {kvp.Key} -> {string.Join(", ", kvp.Value)}");
                }
            }
            else
            {
                // If not grouping, each path just maps to itself
                foreach (var path in processingPaths)
                {
                    pathsToApplyMap[path] = new List<GH_Path> { path };
                }
            }

            return (processingPaths, pathsToApplyMap);
        }

        #endregion

        #region EXEC

        public static async Task<Dictionary<string, GH_Structure<U>>> RunFunctionAsync<T, U>(
            Dictionary<string, GH_Structure<T>> trees,
            Func<Dictionary<string, List<T>>, Task<Dictionary<string, List<U>>>> function,
            bool onlyMatchingPaths = false,
            bool groupIdenticalBranches = false,
            CancellationToken token = default)
            where T : IGH_Goo
            where U : IGH_Goo
        {
            Dictionary<string, GH_Structure<U>> result = new Dictionary<string, GH_Structure<U>>();

            // Get the amount of items in each tree
            var treeLengths = TreesLength<T>(trees.Values);

            Debug.WriteLine($"[DataTreeProcessor] Tree lengths: {string.Join(", ", treeLengths.Select(x => $"{x.Key}: {x.Value}"))}");

            foreach (var kvp in trees)
            {
                Debug.WriteLine($"[DataTreeProcessor] Tree key: {kvp.Key}, Paths: {string.Join(", ", kvp.Value.Paths)}");
            }

            var (allPaths, pathsToApplyMap) = GetProcessingPaths(trees, onlyMatchingPaths, groupIdenticalBranches);

            foreach (var path in allPaths)
            {
                Debug.WriteLine($"[DataTreeProcessor] GENERATING RESULTS FOR PATH: {path}");

                // Check for cancellation
                token.ThrowIfCancellationRequested();
                
                // For each tree, get the branch corresponding to the path, preserving the original dictionary keys
                var branches = trees
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => GetBranchFromTree(kvp.Value, path, preserveStructure: true)
                    );
                
                // Check for empty branches
                var emptyBranches = branches.Where(kvp => !kvp.Value.Any()).ToList();

                // If there are any empty branches...
                if (emptyBranches.Any())
                {
                    // If the branch is empty because that tree only has one branch and one item, copy that branch to the current path
                    foreach (var emptyBranch in emptyBranches)
                    {
                        if (treeLengths[trees.Keys.ToList().IndexOf(emptyBranch.Key)] == 1)
                        {
                            branches[emptyBranch.Key] = GetBranchFromTree(trees[emptyBranch.Key], trees[emptyBranch.Key].Paths.First(), preserveStructure: true);
                        }
                    }
                }
                
                try 
                {
                    // Apply the function to the current branch and await its completion
                    var branchResult = await function(branches);
                    
                    // Get the paths to apply the result to (could be multiple if they have identical branch data)
                    var pathsToApply = pathsToApplyMap[path];

                    // For each path in pathsToApply, convert the branch result to a GH_Structure<T> with the appropriate paths
                    foreach (var applyPath in pathsToApply)
                    {
                        foreach (var kvp in branchResult)
                        {
                            if (!result.ContainsKey(kvp.Key))
                            {
                                result[kvp.Key] = new GH_Structure<U>();
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
                catch (Exception ex)
                {
                    Debug.WriteLine($"[DataTreeProcessor] Error processing path {path}: {ex.Message}");
                    throw;
                }
            }

            Debug.WriteLine($"[DataTreeProcessor] Finished processing all paths. Result keys: {string.Join(", ", result.Keys)}");
            return result;
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
