/* DEPRECATED */

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
    /// Provides processing capabilities for operations involving multiple data trees
    /// </summary>
    public class MultiTreeProcessor
    {
        /// <summary>
        /// Represents a group of paths that contain identical data across all trees
        /// </summary>
        public class MultiTreeBranchGroup<T> where T : IGH_Goo
        {
            public List<GH_Path> Paths { get; set; }
            public Dictionary<GH_Structure<T>, List<T>> Branches { get; set; }
            public string Key { get; set; }

            public MultiTreeBranchGroup(List<GH_Path> paths, Dictionary<GH_Structure<T>, List<T>> branches, string key)
            {
                Paths = paths;
                Branches = branches;
                Key = key;
            }
        }

        /// <summary>
        /// Gets paths from trees based on the onlyMatchingPaths parameter
        /// </summary>
        private static List<GH_Path> GetProcessingPaths<T>(IEnumerable<GH_Structure<T>> trees, bool onlyMatchingPaths) where T : IGH_Goo
        {
            return onlyMatchingPaths ?
                DataTreeProcessor.GetMatchingPaths(trees) :
                DataTreeProcessor.GetAllUniquePaths(trees);
        }

        /// <summary>
        /// Groups paths that contain identical data across all trees
        /// </summary>
        public static Dictionary<string, MultiTreeBranchGroup<T>> GroupIdenticalBranches<T>(
            IEnumerable<GH_Structure<T>> trees,
            bool onlyMatchingPaths = true) where T : IGH_Goo
        {
            var treesList = trees.ToList(); // Cache the enumerable
            var paths = GetProcessingPaths(treesList, onlyMatchingPaths);

            var branchGroups = new Dictionary<string, MultiTreeBranchGroup<T>>();

            foreach (var path in paths)
            {
                // Get branches from all trees for this path
                var branchesForPath = new Dictionary<GH_Structure<T>, List<T>>();
                foreach (var tree in treesList)
                {
                    // Always add an entry for each tree, even if path doesn't exist
                    branchesForPath[tree] = tree.PathExists(path) ?
                        tree.get_Branch(path).Cast<T>().ToList() :
                        new List<T>();
                }

                // Generate a composite key for all branches
                var compositeKey = GetMultiTreeBranchKey(branchesForPath);

                if (!branchGroups.ContainsKey(compositeKey))
                {
                    branchGroups[compositeKey] = new MultiTreeBranchGroup<T>(
                        new List<GH_Path> { path },
                        branchesForPath,
                        compositeKey
                    );
                }
                else if (!branchGroups[compositeKey].Paths.Any(p => p.ToString() == path.ToString()))
                {
                    branchGroups[compositeKey].Paths.Add(path);
                }
            }

            return branchGroups;
        }

        /// <summary>
        /// Processes multiple trees in parallel using the provided processing function
        /// </summary>
        public static async Task<GH_Structure<T>> ProcessTreesInParallel<T>(
            IEnumerable<GH_Structure<GH_String>> trees,
            Func<Dictionary<GH_Structure<GH_String>, List<GH_String>>, GH_Path, CancellationToken, Task<List<T>>> processBranchesFunc,
            bool onlyMatchingPaths = true,
            bool groupIdenticalBranches = true,
            CancellationToken cancellationToken = default) where T : IGH_Goo
        {
            if (trees == null || !trees.Any())
                return new GH_Structure<T>();

            var treesList = trees.ToList(); // Cache the enumerable

            Debug.WriteLine($"[ProcessTreesInParallel] Processing {treesList.Count} trees");
            foreach (var tree in treesList)
            {
                Debug.WriteLine($"Tree structure: {tree.PathCount} paths, {tree.DataCount} total items");
            }

            // Handle single-item trees first
            treesList = ReplicateSingleItemTree(treesList);
            var result = new GH_Structure<T>();

            try
            {
                if (groupIdenticalBranches)
                {
                    var branchGroups = GroupIdenticalBranches(treesList, onlyMatchingPaths);
                    Debug.WriteLine($"[ProcessTreesInParallel] Grouped into {branchGroups.Count} branch groups");

                    // Process non-empty groups in parallel
                    var nonEmptyGroups = branchGroups.Values
                        .Where(g => g.Branches.Any(b => b.Value != null && b.Value.Count > 0))
                        .ToList();

                    Debug.WriteLine($"[ProcessTreesInParallel] Found {nonEmptyGroups.Count} non-empty groups");
                    foreach (var group in nonEmptyGroups)
                    {
                        Debug.WriteLine($"  Group with {group.Paths.Count} paths:");
                        foreach (var path in group.Paths)
                        {
                            Debug.WriteLine($"    - Path: {path}");
                            foreach (var kvp in group.Branches)
                            {
                                Debug.WriteLine($"      * Branch items: {kvp.Value?.Count ?? 0}");
                            }
                        }
                    }

                    var tasks = nonEmptyGroups.Select(async group =>
                    {
                        // Normalize branch lengths and process items in parallel
                        var normalizedBranches = BranchProcessor.NormalizeBranchLengths(group.Branches);

                        // Process the branches
                        var processedBranch = await processBranchesFunc(normalizedBranches, group.Paths[0], cancellationToken);

                        return (group.Paths, processedBranch);
                    });

                    var processedGroups = await Task.WhenAll(tasks);

                    foreach (var (paths, processedBranch) in processedGroups)
                    {
                        foreach (var path in paths)
                        {
                            result.AppendRange(processedBranch ?? new List<T>(), path);
                        }
                    }
                }
                else
                {
                    var paths = GetProcessingPaths(treesList, onlyMatchingPaths);

                    var tasks = paths.Select(async path =>
                    {
                        var branchesForPath = new Dictionary<GH_Structure<GH_String>, List<GH_String>>();
                        foreach (var tree in treesList)
                        {
                            // Always add an entry for each tree, even if path doesn't exist
                            branchesForPath[tree] = tree.PathExists(path) ?
                                tree.get_Branch(path).Cast<GH_String>().ToList() :
                                new List<GH_String>();
                        }

                        // Normalize branch lengths and process items in parallel
                        var normalizedBranches = BranchProcessor.NormalizeBranchLengths(branchesForPath);
                        var processedBranch = await processBranchesFunc(normalizedBranches, path, cancellationToken);
                        return (path, processedBranch);
                    });

                    var processedBranches = await Task.WhenAll(tasks);

                    foreach (var (path, processedBranch) in processedBranches)
                    {
                        result.AppendRange(processedBranch ?? new List<T>(), path);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MultiTreeProcessor] Error in parallel processing: {ex.GetType().Name} - {ex.Message}");
                throw;
            }

            return result;
        }

        private static string GetMultiTreeBranchKey<T>(Dictionary<GH_Structure<T>, List<T>> branches) where T : IGH_Goo
        {
            if (branches == null || !branches.Any())
                return "empty";

            return string.Join("|", branches.Select(kvp =>
            {
                var treeKey = kvp.Key.GetHashCode().ToString();
                var branchKey = string.Join(",", kvp.Value.Select(item =>
                {
                    if (item == null) return "null";

                    // Only use direct value comparison for simple types
                    if (item is GH_String str) return str.Value;
                    if (item is GH_Integer intVal) return intVal.Value.ToString();
                    if (item is GH_Number numVal) return numVal.Value.ToString();
                    if (item is GH_Boolean boolVal) return boolVal.Value.ToString();

                    // For other types, treat each instance as unique
                    return item.GetHashCode().ToString();
                }));
                return $"{treeKey}:{branchKey}";
            }));
        }

        /// <summary>
        /// Checks if a tree has exactly one item in one branch
        /// </summary>
        private static bool HasSingleItem<T>(GH_Structure<T> tree) where T : IGH_Goo
        {
            return tree.PathCount == 1 && tree.DataCount == 1;
        }

        /// <summary>
        /// Gets the single item from a tree that we know has exactly one item
        /// </summary>
        private static T GetSingleItem<T>(GH_Structure<T> tree) where T : IGH_Goo
        {
            // We know there's exactly one item, find which branch has it
            foreach (var path in tree.Paths)
            {
                var branch = tree.get_Branch(path);
                if (branch != null && branch.Count > 0)
                {
                    return branch.Cast<T>().First();
                }
            }
            throw new InvalidOperationException("Tree was expected to have exactly one item but none was found");
        }

        /// <summary>
        /// If any trees have a single item, replicates them to all paths from other trees
        /// </summary>
        private static List<GH_Structure<T>> ReplicateSingleItemTree<T>(
            List<GH_Structure<T>> trees) where T : IGH_Goo
        {
            // Find all trees with single items but preserve original list for order
            var singleItemTrees = trees.Where(HasSingleItem).ToList();
            var multiItemTrees = trees.Where(t => !HasSingleItem(t)).ToList();

            if (!singleItemTrees.Any())
            {
                // No single item trees found, return original trees in original order
                return trees;
            }

            // Get all unique paths from multi-item trees
            var allPaths = multiItemTrees.Any()
                ? DataTreeProcessor.GetAllUniquePaths(multiItemTrees)
                : new List<GH_Path> { trees[0].Paths.FirstOrDefault() ?? new GH_Path(0) }; // Default path if all trees are single-item, branch from first tree or {0}

            // Create replicated trees dictionary for lookup
            var replicatedTrees = new Dictionary<GH_Structure<T>, GH_Structure<T>>();
            foreach (var singleItemTree in singleItemTrees)
            {
                var singleItem = GetSingleItem(singleItemTree);
                var replicatedTree = new GH_Structure<T>();
                foreach (var path in allPaths)
                {
                    replicatedTree.Append(singleItem, path);
                }
                replicatedTrees[singleItemTree] = replicatedTree;
            }

            // Return trees in original order, replacing single item trees with their replicated versions
            return trees.Select(tree => HasSingleItem(tree) ? replicatedTrees[tree] : tree).ToList();
        }
    }
}
