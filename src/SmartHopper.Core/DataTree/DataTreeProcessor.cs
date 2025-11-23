/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace SmartHopper.Core.DataTree
{
    public enum ProcessingTopology
    {
        ItemToItem,
        ItemGraft,
        BranchFlatten,
        BranchToBranch,
    }

    public sealed class ProcessingOptions
    {
        public ProcessingTopology Topology { get; set; }

        public bool OnlyMatchingPaths { get; set; }

        public bool GroupIdenticalBranches { get; set; }
    }

    /// <summary>
    /// Provides utility methods for processing Grasshopper data trees.
    /// </summary>
    public static class DataTreeProcessor
    {
        #region BRANCH

        /// <summary>
        /// Gets a branch from a data tree at the specified path.
        /// Handles flat tree broadcasting: if the tree has only path {0} and a different path is requested,
        /// returns the {0} branch (broadcasting the flat tree data to all structured paths).
        /// Returns empty list if no matching branch is found and preserveStructure is true.
        /// </summary>
        /// <typeparam name="T">Type of items contained in the data tree.</typeparam>
        /// <param name="tree">The input data tree.</param>
        /// <param name="path">The path to get the branch from.</param>
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

            // Flat tree broadcasting: if tree has only path {0}, broadcast it to any requested path
            if (tree.PathCount == 1 && tree.Paths[0].ToString() == "{0}")
            {
                var flatBranch = tree.get_Branch(tree.Paths[0]);
                if (flatBranch != null && flatBranch.Count > 0)
                    return flatBranch.Cast<T>().ToList();
            }

            // Return empty list or null based on preserveStructure flag
            return preserveStructure ? new List<T>() : null;
        }

        /// <summary>
        /// Generates a unique key for a set of branches based on their content.
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
        /// <typeparam name="T">Type of items contained in the data trees.</typeparam>
        /// <param name="trees">List of data trees to combine.</param>
        /// <returns>List of all unique paths found across all trees.</returns>
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
        /// <typeparam name="T">Type of items contained in the data trees.</typeparam>
        /// <param name="trees">List of data trees to compare.</param>
        /// <returns>List of paths that exist in all trees.</returns>
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
        /// Gets the amount of items in each tree, indexed by position.
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
        /// Gets paths from trees based on the onlyMatchingPaths parameter and groups identical branches if requested.
        /// </summary>
        /// <returns>A tuple containing the list of unique processing paths and a dictionary mapping paths to their identical branches.</returns>
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
                            kvp => GetBranchFromTree(kvp.Value, siblingPath, preserveStructure: true));

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

        #region PLAN

        /// <summary>
        /// Describes the processing plan for a set of Grasshopper data trees.
        /// Each entry represents a unique primary path and the set of target paths
        /// that should receive the computed results (taking identical branch grouping into account).
        /// </summary>
        internal sealed class ProcessingPlan<T> where T : IGH_Goo
        {
            public ProcessingPlan(IReadOnlyList<PlanEntry<T>> entries)
            {
                this.Entries = entries ?? throw new ArgumentNullException(nameof(entries));
            }

            public IReadOnlyList<PlanEntry<T>> Entries { get; }
        }

        /// <summary>
        /// Represents a single primary processing path and the target paths that should
        /// receive the computed results for that primary branch.
        /// </summary>
        internal sealed class PlanEntry<T> where T : IGH_Goo
        {
            public PlanEntry(GH_Path primaryPath, IReadOnlyList<GH_Path> targetPaths)
            {
                this.PrimaryPath = primaryPath ?? throw new ArgumentNullException(nameof(primaryPath));
                this.TargetPaths = targetPaths ?? throw new ArgumentNullException(nameof(targetPaths));
            }

            public GH_Path PrimaryPath { get; }

            public IReadOnlyList<GH_Path> TargetPaths { get; }
        }

        /// <summary>
        /// Builds a processing plan for the provided data trees, taking into account
        /// matching paths and identical branch grouping rules.
        /// </summary>
        /// <typeparam name="T">Type of items contained in the data trees.</typeparam>
        /// <param name="trees">Dictionary of input data trees keyed by a logical name.</param>
        /// <param name="onlyMatchingPaths">If true, only consider paths that exist in all trees (intersection); otherwise use union.</param>
        /// <param name="groupIdenticalBranches">If true, group identical branches to reduce redundant processing.</param>
        /// <returns>A <see cref="ProcessingPlan{T}"/> describing how branches should be processed.</returns>
        internal static ProcessingPlan<T> BuildProcessingPlan<T>(
            Dictionary<string, GH_Structure<T>> trees,
            bool onlyMatchingPaths = false,
            bool groupIdenticalBranches = false)
            where T : IGH_Goo
        {
            if (trees == null)
            {
                throw new ArgumentNullException(nameof(trees));
            }

            var (processingPaths, pathsToApplyMap) = GetProcessingPaths(
                trees,
                onlyMatchingPaths,
                groupIdenticalBranches);

            var entries = new List<PlanEntry<T>>(processingPaths.Count);

            foreach (var path in processingPaths)
            {
                if (!pathsToApplyMap.TryGetValue(path, out var targets) || targets == null || targets.Count == 0)
                {
                    targets = new List<GH_Path> { path };
                }

                entries.Add(new PlanEntry<T>(path, targets));
            }

            return new ProcessingPlan<T>(entries);
        }

        /// <summary>
        /// Calculates the number of processing units (iterations) that will be executed based on the processing plan and topology.
        /// </summary>
        /// <typeparam name="T">Type of items contained in the data trees.</typeparam>
        /// <param name="trees">Dictionary of input data trees.</param>
        /// <param name="options">Processing options specifying topology and path/grouping behavior.</param>
        /// <returns>A tuple containing (dataCount, iterationCount) where dataCount is the number of output items and iterationCount is the number of function invocations.</returns>
        public static (int dataCount, int iterationCount) CalculateProcessingMetrics<T>(
            Dictionary<string, GH_Structure<T>> trees,
            ProcessingOptions options)
            where T : IGH_Goo
        {
            if (trees == null || options == null)
            {
                return (0, 0);
            }

            var plan = BuildProcessingPlan(trees, options.OnlyMatchingPaths, options.GroupIdenticalBranches);

            bool isItemMode = options.Topology == ProcessingTopology.ItemToItem ||
                              options.Topology == ProcessingTopology.ItemGraft;

            if (isItemMode)
            {
                // Item mode: count total items across all branches
                int totalItems = 0;
                foreach (var entry in plan.Entries)
                {
                    int maxLength = GetMaxBranchLengthExcludingFlatTrees(trees, entry.PrimaryPath);
                    totalItems += maxLength;
                }

                return (totalItems, totalItems);
            }

            if (options.Topology == ProcessingTopology.BranchFlatten)
            {
                // BranchFlatten: all branches flattened to a single output
                return (1, 1);
            }

            // Branch mode: count branches and target paths
            int iterationCount = plan.Entries.Count;
            int dataCount = plan.Entries.Sum(e => e.TargetPaths.Count);
            return (dataCount, iterationCount);
        }

        /// <summary>
        /// Computes the maximum branch length for a given primary path, excluding flat trees (single-path trees at {0}).
        /// Flat trees are treated as broadcast inputs and do not affect per-item iteration counts.
        /// </summary>
        private static int GetMaxBranchLengthExcludingFlatTrees<T>(Dictionary<string, GH_Structure<T>> trees, GH_Path primaryPath)
            where T : IGH_Goo
        {
            int maxLength = 0;

            foreach (var tree in trees.Values)
            {
                // Skip flat trees (trees with only path {0}) when calculating maxLength
                // Flat trees are broadcast to all paths, not iterated per item
                if (tree.PathCount == 1 && tree.Paths[0].ToString() == "{0}")
                {
                    continue;
                }

                var branch = GetBranchFromTree(tree, primaryPath, preserveStructure: true);
                if (branch != null && branch.Count > maxLength)
                {
                    maxLength = branch.Count;
                }
            }

            return maxLength;
        }

        #endregion

        #region UNIFIED RUNNER

        /// <summary>
        /// Represents a single unit of processing work (either an item or a branch).
        /// </summary>
        private struct ProcessingUnit<T> where T : IGH_Goo
        {
            /// <summary>
            /// The input path from which to read data.
            /// </summary>
            public GH_Path InputPath { get; set; }

            /// <summary>
            /// The item index within the branch. Null for branch mode (process entire branch).
            /// </summary>
            public int? ItemIndex { get; set; }

            /// <summary>
            /// The target paths where results should be written.
            /// </summary>
            public IReadOnlyList<GH_Path> TargetPaths { get; set; }
        }

        /// <summary>
        /// Unified runner that processes data trees based on a specified topology.
        /// Handles item-to-item, item-graft, branch-flatten, and branch-to-branch processing modes.
        /// </summary>
        /// <typeparam name="T">Type of input tree items.</typeparam>
        /// <typeparam name="U">Type of output tree items.</typeparam>
        /// <param name="inputTrees">Dictionary of input data trees.</param>
        /// <param name="function">Function to run on each logical unit (item or branch). Receives Dictionary&lt;string, List&lt;T&gt;&gt; and returns Dictionary&lt;string, List&lt;U&gt;&gt;.</param>
        /// <param name="options">Processing options specifying topology and path/grouping behavior.</param>
        /// <param name="progressCallback">Optional callback to report progress (current, total).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Dictionary of output data trees keyed by the same keys as the input dictionary.</returns>
        public static async Task<Dictionary<string, GH_Structure<U>>> RunAsync<T, U>(
            Dictionary<string, GH_Structure<T>> inputTrees,
            Func<Dictionary<string, List<T>>, Task<Dictionary<string, List<U>>>> function,
            ProcessingOptions options,
            Action<int, int> progressCallback = null,
            CancellationToken token = default)
            where T : IGH_Goo
            where U : IGH_Goo
        {
            if (inputTrees == null)
            {
                throw new ArgumentNullException(nameof(inputTrees));
            }

            if (function == null)
            {
                throw new ArgumentNullException(nameof(function));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var result = new Dictionary<string, GH_Structure<U>>();
            var plan = BuildProcessingPlan(inputTrees, options.OnlyMatchingPaths, options.GroupIdenticalBranches);

            Debug.WriteLine($"[DataTreeProcessor.RunAsync] Topology: {options.Topology}, Plan entries: {plan.Entries.Count}");

            // Build unified schedule based on topology
            var schedule = new List<ProcessingUnit<T>>();
            bool isItemMode = options.Topology == ProcessingTopology.ItemToItem || options.Topology == ProcessingTopology.ItemGraft;
            bool isBranchFlatten = options.Topology == ProcessingTopology.BranchFlatten;

            if (isBranchFlatten)
            {
                // BranchFlatten: create a single ProcessingUnit with null path to indicate "all branches"
                schedule.Add(new ProcessingUnit<T>
                {
                    InputPath = null,  // null indicates flatten all branches
                    ItemIndex = null,
                    TargetPaths = new List<GH_Path> { new GH_Path(0) },  // Output to path {0}
                });
            }
            else if (isItemMode)
            {
                // Item mode: create one ProcessingUnit per item
                foreach (var entry in plan.Entries)
                {
                    int maxLength = GetMaxBranchLengthExcludingFlatTrees(inputTrees, entry.PrimaryPath);

                    // Add each item index to the schedule
                    for (int i = 0; i < maxLength; i++)
                    {
                        schedule.Add(new ProcessingUnit<T>
                        {
                            InputPath = entry.PrimaryPath,
                            ItemIndex = i,
                            TargetPaths = entry.TargetPaths,
                        });
                    }
                }
            }
            else
            {
                // Branch mode: create one ProcessingUnit per branch
                foreach (var entry in plan.Entries)
                {
                    schedule.Add(new ProcessingUnit<T>
                    {
                        InputPath = entry.PrimaryPath,
                        ItemIndex = null,  // null indicates branch mode
                        TargetPaths = entry.TargetPaths,
                    });
                }
            }

            int totalUnits = schedule.Count;
            int currentUnit = 0;

            Debug.WriteLine($"[DataTreeProcessor.RunAsync] Total units: {totalUnits}");
            progressCallback?.Invoke(currentUnit, totalUnits);

            // Process each unit in the schedule
            foreach (var unit in schedule)
            {
                token.ThrowIfCancellationRequested();

                // Build input dictionary based on mode
                var inputs = new Dictionary<string, List<T>>();

                if (options.Topology == ProcessingTopology.BranchFlatten)
                {
                    // BranchFlatten: flatten all branches from all paths
                    foreach (var kvp in inputTrees)
                    {
                        var flattenedList = new List<T>();
                        foreach (var path in kvp.Value.Paths)
                        {
                            var branch = kvp.Value.get_Branch(path);
                            if (branch != null)
                            {
                                flattenedList.AddRange(branch.Cast<T>());
                            }
                        }

                        inputs[kvp.Key] = flattenedList;
                    }
                }
                else
                {
                    // Normal path-based processing
                    foreach (var kvp in inputTrees)
                    {
                        var branch = GetBranchFromTree(kvp.Value, unit.InputPath, preserveStructure: true);

                        if (unit.ItemIndex.HasValue)
                        {
                            // Item mode: single-element list
                            var item = (branch != null && unit.ItemIndex.Value < branch.Count) ? branch[unit.ItemIndex.Value] : default(T);
                            inputs[kvp.Key] = new List<T> { item };
                        }
                        else
                        {
                            // Branch mode: full branch
                            inputs[kvp.Key] = branch ?? new List<T>();
                        }
                    }
                }

                // Invoke function
                var outputs = await function(inputs).ConfigureAwait(false);

                // Determine output paths and append results based on topology
                if (options.Topology == ProcessingTopology.BranchFlatten)
                {
                    // BranchFlatten: all branches flatten to [0]
                    var flatPath = new GH_Path(0);
                    foreach (var kvp in outputs)
                    {
                        if (!result.TryGetValue(kvp.Key, out var structure))
                        {
                            structure = new GH_Structure<U>();
                            result[kvp.Key] = structure;
                        }

                        if (kvp.Value != null)
                        {
                            structure.AppendRange(kvp.Value, flatPath);
                        }
                    }
                }
                else
                {
                    // ItemToItem, ItemGraft, or BranchToBranch: use target paths
                    foreach (var targetPath in unit.TargetPaths)
                    {
                        GH_Path outputPath;

                        if (options.Topology == ProcessingTopology.ItemGraft && unit.ItemIndex.HasValue)
                        {
                            // ItemGraft: append item index to path -> [q0,q1,q2,i]
                            outputPath = targetPath.AppendElement(unit.ItemIndex.Value);
                            Debug.WriteLine($"[DataTreeProcessor.RunAsync] ItemGraft: {targetPath} + [{unit.ItemIndex.Value}] -> {outputPath}");
                        }
                        else
                        {
                            // ItemToItem or BranchToBranch: keep same path
                            outputPath = targetPath;
                        }

                        // Append outputs to result structures
                        foreach (var kvp in outputs)
                        {
                            if (!result.TryGetValue(kvp.Key, out var structure))
                            {
                                structure = new GH_Structure<U>();
                                result[kvp.Key] = structure;
                            }

                            if (kvp.Value != null)
                            {
                                structure.AppendRange(kvp.Value, outputPath);
                            }
                        }
                    }
                }

                currentUnit++;
                progressCallback?.Invoke(currentUnit, totalUnits);
            }

            Debug.WriteLine($"[DataTreeProcessor.RunAsync] Finished. Output keys: {string.Join(", ", result.Keys)}");
            return result;
        }

        #endregion

        #region NORMALIZATION

        /// <summary>
        /// Normalizes branch lengths by extending shorter branches with their last item.
        /// </summary>
        /// <typeparam name="T">Type of items in the branches.</typeparam>
        /// <param name="branches">Collection of branches to normalize.</param>
        /// <returns>List of normalized branches with equal length.</returns>
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
