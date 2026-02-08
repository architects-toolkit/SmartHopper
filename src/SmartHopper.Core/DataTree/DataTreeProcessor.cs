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
        /// Determines if a flat {0} tree should broadcast to a requested path based on topology rules.
        /// See docs/Components/ComponentBase/FlatTreeBroadcasting.md for complete rules.
        /// </summary>
        /// <param name="flatTree">The single-path tree to evaluate for broadcasting.</param>
        /// <param name="requestedPath">The path being requested.</param>
        /// <param name="otherTreePaths">All unique paths from the other input trees (excluding the flat tree itself).</param>
        /// <returns>True if the flat tree should broadcast to the requested path.</returns>
        private static bool ShouldBroadcastFlatTree<T>(GH_Structure<T> flatTree, GH_Path requestedPath, List<GH_Path> otherTreePaths) where T : IGH_Goo
        {
            if (flatTree == null || flatTree.PathCount != 1 || requestedPath == null)
            {
                return false;
            }

            // If there is no topology context from other trees, only allow non-{0} scalars to broadcast
            if (otherTreePaths == null || otherTreePaths.Count == 0)
            {
                return flatTree.DataCount == 1
                    && flatTree.PathCount == 1
                    && flatTree.Paths[0].ToString() != "{0}";
            }

            var flatPath = flatTree.Paths[0];
            var flatPathStr = flatPath.ToString();
            var requestedPathStr = requestedPath.ToString();

            // Scalar trees:
            // - Non-{0} scalars (e.g. B:{1} in DIFF-1 / DIFF-3-1) always broadcast.
            // - {0} scalars broadcast EXCEPT when Rule 4 applies (direct {0} match + deeper {0;...}).
            if (flatTree.DataCount == 1)
            {
                if (flatTree.PathCount == 1 && flatPathStr != "{0}")
                {
                    return true;
                }

                // {0} scalar: broadcast to all EXCEPT deeper {0;...} when Rule 4 applies
                if (flatTree.PathCount == 1 && flatPathStr == "{0}")
                {
                    // Requested is {0}? No broadcasting, direct match
                    if (requestedPathStr == "{0}")
                    {
                        return false;
                    }

                    // Check Rule 4: if other trees have {0} and requested is {0;...}, don't broadcast
                    if (requestedPathStr.StartsWith("{0;"))
                    {
                        bool otherTreeHasZero = otherTreePaths.Any(p => p.ToString() == "{0}");
                        if (otherTreeHasZero)
                        {
                            // Rule 4: direct {0} match takes precedence, don't broadcast to {0;...}
                            return false;
                        }
                    }

                    // Otherwise, scalar broadcasts (e.g. to {1}, {2}, or {1;0} if no {0} elsewhere)
                    return true;
                }
            }

            // Only {0} flat trees have special broadcasting rules
            if (flatPathStr != "{0}")
            {
                return false;
            }

            // Direct path match - no broadcasting needed
            if (requestedPathStr == "{0}")
            {
                return false;
            }

            // Analyze topology of other trees' paths
            int requestedDepth = requestedPath.Length;
            bool hasDeeperPaths = false;
            bool hasMatchingTopLevel = false;

            foreach (var p in otherTreePaths)
            {
                if (p.ToString() == "{0}")
                {
                    hasMatchingTopLevel = true;
                    continue;
                }

                int pathDepth = p.Length;

                if (pathDepth > 1)
                {
                    // Found a deeper path (contains ;)
                    hasDeeperPaths = true;
                }
            }

            // Rule 4: Direct match takes precedence - if another tree has {0} AND requested is deeper {0;...}
            // Only broadcast if there are also multiple top-level paths (Rule 2 override).
            if (hasMatchingTopLevel && requestedPathStr.StartsWith("{0;"))
            {
                // Check if there are multiple top-level paths (not just {0}) among other trees
                var topLevelCount = otherTreePaths.Count(p => p.Length == 1);
                if (topLevelCount == 1)
                {
                    // Only {0} at top level, don't broadcast to deeper {0;...}
                    return false;
                }
            }

            // Rule 1 & 2: Same-depth paths at depth 1
            if (requestedDepth == 1)
            {
                // Count all top-level paths from other trees (including {0} if present)
                var topLevelCount = otherTreePaths.Count(p => p.Length == 1);

                // If only one top-level path exists and there's no depth complexity, no broadcasting (Rule 1)
                if (topLevelCount == 1 && !hasDeeperPaths)
                {
                    return false;
                }

                // Multiple top-level paths or mixed depths → broadcast (Rule 2 or Rule 3)
                return true;
            }

            // Rule 3: Different topology depth → broadcasting
            // If requested path is deeper (has ;), broadcast
            if (requestedDepth > 1)
            {
                return true;
            }

            // Default: no broadcasting
            return false;
        }

        /// <summary>
        /// Gets a branch from a data tree with context-aware flat tree broadcasting.
        /// </summary>
        public static List<T> GetBranchFromTree<T>(GH_Structure<T> tree, GH_Path path, IEnumerable<GH_Structure<T>> allTrees, bool preserveStructure = true) where T : IGH_Goo
        {
            if (tree == null)
            {
                return preserveStructure ? new List<T>() : null;
            }

            if (tree.DataCount == 0)
            {
                return preserveStructure ? new List<T>() : null;
            }

            // Try to get the branch at the specified path
            var branch = tree.get_Branch(path);
            if (branch != null && branch.Count > 0)
            {
                return branch.Cast<T>().ToList();
            }

            // Check for flat tree broadcasting
            if (tree.PathCount == 1)
            {
                var flatBranch = tree.get_Branch(tree.Paths[0]);
                if (flatBranch != null && flatBranch.Count > 0)
                {
                    // Collect topology only from other trees (exclude this tree)
                    List<GH_Path> otherPaths = new List<GH_Path>();
                    if (allTrees != null)
                    {
                        var otherTrees = allTrees.Where(t => !ReferenceEquals(t, tree));
                        otherPaths = GetAllUniquePaths(otherTrees);
                    }

                    if (ShouldBroadcastFlatTree(tree, path, otherPaths))
                    {
                        return flatBranch.Cast<T>().ToList();
                    }
                }
            }

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
                allPaths = GetAllUniquePaths(trees.Values);

                Debug.WriteLine($"[DataTreeProcessor] All paths: {string.Join(", ", allPaths)}");

                // Remove paths from flat trees that are truly broadcast-only
                if (allPaths.Count > 1)
                {
                    var treeList = trees.Values.ToList();
                    var pathsToRemove = new List<GH_Path>();

                    for (int i = 0; i < treeList.Count; i++)
                    {
                        var tree = treeList[i];

                        // Only consider single-path trees for broadcast-only removal
                        if (tree.PathCount != 1)
                        {
                            continue;
                        }

                        var treePath = tree.Paths[0];

                        // Get paths from other trees
                        var otherTrees = treeList.Where((t, idx) => idx != i);
                        var otherPaths = GetAllUniquePaths(otherTrees);

                        // If there are no other paths, keep this tree's path
                        if (otherPaths.Count == 0)
                        {
                            continue;
                        }

                        // Check if this tree broadcasts to ALL other paths
                        bool broadcastsToAll = true;
                        foreach (var otherPath in otherPaths)
                        {
                            // Skip if this is the tree's own path in another tree
                            if (otherPath.ToString() == treePath.ToString())
                            {
                                continue;
                            }

                            if (!ShouldBroadcastFlatTree(tree, otherPath, otherPaths))
                            {
                                broadcastsToAll = false;
                                break;
                            }
                        }

                        // If this tree broadcasts to all other paths and has no direct match elsewhere,
                        // mark its path for removal (it's broadcast-only)
                        if (broadcastsToAll && !otherPaths.Any(p => p.ToString() == treePath.ToString()))
                        {
                            Debug.WriteLine($"[DataTreeProcessor] Tree with path {treePath} is broadcast-only candidate");
                            pathsToRemove.Add(treePath);
                        }
                    }

                    // Only remove paths if it won't leave us with zero processing paths
                    // When multiple scalars all broadcast to each other, keep the first tree's path
                    if (pathsToRemove.Count < allPaths.Count)
                    {
                        foreach (var pathToRemove in pathsToRemove)
                        {
                            allPaths.RemoveAll(p => p.ToString() == pathToRemove.ToString());
                            Debug.WriteLine($"[DataTreeProcessor] Removed broadcast-only path {pathToRemove}");
                        }
                    }
                    else
                    {
                        // All paths would be removed - keep the first tree's path
                        var firstTreePath = treeList[0].Paths[0];
                        allPaths.RemoveAll(p => pathsToRemove.Contains(p) && p.ToString() != firstTreePath.ToString());
                        Debug.WriteLine($"[DataTreeProcessor] All paths broadcast to each other, keeping first tree's path {firstTreePath}");
                    }

                    Debug.WriteLine($"[DataTreeProcessor] Final processing paths: {string.Join(", ", allPaths)}");
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
                        kvp => GetBranchFromTree(kvp.Value, currentPath, trees.Values, preserveStructure: true)
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
                            kvp => GetBranchFromTree(kvp.Value, siblingPath, trees.Values, preserveStructure: true));

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
        /// Computes the maximum branch length for a given primary path, excluding flat trees that broadcast.
        /// Flat trees are treated as broadcast inputs and do not affect per-item iteration counts.
        /// Uses centralized ShouldBroadcastFlatTree logic for consistent behavior.
        /// </summary>
        private static int GetMaxBranchLengthExcludingFlatTrees<T>(Dictionary<string, GH_Structure<T>> trees, GH_Path primaryPath)
            where T : IGH_Goo
        {
            int maxLength = 0;

            // Materialize list once so we can derive "other" trees per candidate flat tree
            var treeList = trees.Values.ToList();

            foreach (var tree in treeList)
            {
                // Skip flat trees that truly broadcast to this path (they don't contribute to item count)
                if (tree.PathCount == 1 && !tree.PathExists(primaryPath))
                {
                    // Build topology context from the other trees only
                    var otherTrees = treeList.Where(t => !ReferenceEquals(t, tree));
                    var otherPaths = GetAllUniquePaths(otherTrees);

                    if (ShouldBroadcastFlatTree(tree, primaryPath, otherPaths))
                    {
                        continue; // Skip broadcast-only trees
                    }
                }

                var branch = GetBranchFromTree(tree, primaryPath, treeList, preserveStructure: true);
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
        /// Returns the item at the requested index, broadcasting the last available value when the branch is shorter.
        /// </summary>
        private static T GetItemWithBroadcastFallback<T>(List<T> branch, int index)
            where T : IGH_Goo
        {
            if (branch == null || branch.Count == 0)
            {
                return default(T);
            }

            if (index < branch.Count)
            {
                return branch[index];
            }

            return branch[branch.Count - 1];
        }

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
                        var branch = GetBranchFromTree(kvp.Value, unit.InputPath, inputTrees.Values, preserveStructure: true);

                        if (unit.ItemIndex.HasValue)
                        {
                            // Item mode: single-element list with scalar broadcasting support
                            var item = GetItemWithBroadcastFallback(branch, unit.ItemIndex.Value);
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
