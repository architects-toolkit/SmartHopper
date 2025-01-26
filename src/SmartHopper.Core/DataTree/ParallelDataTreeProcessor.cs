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
    /// Provides advanced data tree processing capabilities including duplicate detection and parallel processing
    /// </summary>
    public class ParallelDataTreeProcessor
    {
        /// <summary>
        /// Represents a group of branches that contain identical data
        /// </summary>
        public class BranchGroup
        {
            public List<GH_Path> Paths { get; set; }
            public List<IGH_Goo> Branch { get; set; }
            public string Key { get; set; }

            public BranchGroup(List<GH_Path> paths, List<IGH_Goo> branch, string key)
            {
                Paths = paths;
                Branch = branch;
                Key = key;
            }
        }

        /// Groups identical branches in a data tree to avoid duplicate processing.
        /// Only groups by content for simple types (string, int, float, bool).
        /// Other types are treated as unique instances.
        /// </summary>
        public static Dictionary<string, BranchGroup> GroupIdenticalBranches(GH_Structure<IGH_Goo> inputTree)
        {
            var branchGroups = new Dictionary<string, BranchGroup>();

            foreach (var path in inputTree.Paths)
            {
                var branch = inputTree.get_Branch(path).Cast<IGH_Goo>().ToList();
                var branchKey = GetBranchKey(branch);

                if (!branchGroups.ContainsKey(branchKey))
                {
                    branchGroups[branchKey] = new BranchGroup(
                        new List<GH_Path> { path },
                        branch,
                        branchKey
                    );
                }
                else
                {
                    branchGroups[branchKey].Paths.Add(path);
                }
            }

            return branchGroups;
        }

        /// <summary>
        /// Processes branch groups in parallel using the provided processing function
        /// </summary>
        /// <param name="inputTree">The input data tree to process</param>
        /// <param name="processBranchFunc">Function to process each branch</param>
        /// <param name="groupIdenticalBranches">If true, identical branches will be processed once and results copied to all paths. If false, each branch is processed independently.</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public static async Task<GH_Structure<IGH_Goo>> ProcessBranchesInParallel(
            GH_Structure<IGH_Goo> inputTree,
            Func<List<IGH_Goo>, GH_Path, CancellationToken, Task<List<IGH_Goo>>> processBranchFunc,
            bool groupIdenticalBranches = true,
            CancellationToken cancellationToken = default)
        {
            Debug.WriteLine("[ParallelDataTreeProcessor] ProcessBranchesInParallel - Start");
            Debug.WriteLine($"[ParallelDataTreeProcessor] Input tree null? {inputTree == null}");
            if (inputTree == null) return new GH_Structure<IGH_Goo>();

            Debug.WriteLine($"[ParallelDataTreeProcessor] Paths: {inputTree.Paths?.Count}, DataCount: {inputTree.DataCount}");
            var result = new GH_Structure<IGH_Goo>();

            try
            {
                if (groupIdenticalBranches)
                {
                    Debug.WriteLine("[ParallelDataTreeProcessor] Using group identical branches mode");
                    var branchGroups = GroupIdenticalBranches(inputTree);
                    Debug.WriteLine($"[ParallelDataTreeProcessor] Branch groups count: {branchGroups?.Count}");

                    // Handle empty branches immediately without processing
                    foreach (var group in branchGroups.Values.Where(g => g.Branch == null || g.Branch.Count == 0))
                    {
                        foreach (var path in group.Paths)
                        {
                            result.AppendRange(new List<IGH_Goo>(), path);
                        }
                    }

                    // Process non-empty branches in parallel
                    var nonEmptyGroups = branchGroups.Values.Where(g => g.Branch != null && g.Branch.Count > 0).ToList();
                    Debug.WriteLine($"[ParallelDataTreeProcessor] Non-empty groups count: {nonEmptyGroups.Count}");

                    var tasks = nonEmptyGroups.Select(async group =>
                    {
                        try
                        {
                            Debug.WriteLine($"[ParallelDataTreeProcessor] Processing group with key: {group.Key}");
                            // Process with the first path in the group
                            var processedBranch = await processBranchFunc(group.Branch, group.Paths[0], cancellationToken);
                            Debug.WriteLine($"[ParallelDataTreeProcessor] Group processed successfully: {group.Key}");
                            return (group.Paths, processedBranch);
                        }
                        catch (OperationCanceledException)
                        {
                            Debug.WriteLine("[ParallelDataTreeProcessor] Operation cancelled");
                            throw;
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[ParallelDataTreeProcessor] Error processing branch group: {ex.GetType().Name} - {ex.Message}");
                            throw;
                        }
                    }).ToList();

                    Debug.WriteLine("[ParallelDataTreeProcessor] Waiting for all tasks to complete");
                    var processedGroups = await Task.WhenAll(tasks);
                    Debug.WriteLine("[ParallelDataTreeProcessor] All tasks completed");

                    foreach (var (paths, processedBranch) in processedGroups)
                    {
                        foreach (var path in paths)
                        {
                            result.AppendRange(processedBranch, path);
                        }
                    }
                }
                else
                {
                    Debug.WriteLine("[ParallelDataTreeProcessor] Using individual branch processing mode");
                    // Process each branch independently
                    var tasks = inputTree.Paths.Select(async path =>
                    {
                        try
                        {
                            Debug.WriteLine($"[ParallelDataTreeProcessor] Processing path: {path}");
                            var branch = inputTree.get_Branch(path).Cast<IGH_Goo>().ToList();
                            var processedBranch = await processBranchFunc(branch, path, cancellationToken);
                            Debug.WriteLine($"[ParallelDataTreeProcessor] Path processed successfully: {path}");
                            return (path, processedBranch);
                        }
                        catch (OperationCanceledException)
                        {
                            Debug.WriteLine("[ParallelDataTreeProcessor] Operation cancelled");
                            throw;
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[ParallelDataTreeProcessor] Error processing branch: {ex.GetType().Name} - {ex.Message}");
                            throw;
                        }
                    }).ToList();

                    Debug.WriteLine("[ParallelDataTreeProcessor] Waiting for all tasks to complete");
                    var processedBranches = await Task.WhenAll(tasks);
                    Debug.WriteLine("[ParallelDataTreeProcessor] All tasks completed");

                    foreach (var (path, processedBranch) in processedBranches)
                    {
                        result.AppendRange(processedBranch, path);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ParallelDataTreeProcessor] Error in parallel processing: {ex.GetType().Name} - {ex.Message}");
                throw;
            }

            Debug.WriteLine("[ParallelDataTreeProcessor] ProcessBranchesInParallel - Complete");
            return result;
        }

        /// <summary>
        /// Processes branch groups in parallel using a simple transformation function (item level)
        /// </summary>
        //public static async Task<GH_Structure<IGH_Goo>> ProcessBranchesInParallel(
        //    GH_Structure<IGH_Goo> inputTree,
        //    Func<IGH_Goo, CancellationToken, Task<IGH_Goo>> processItemFunc,
        //    bool groupIdenticalBranches = true,
        //    CancellationToken cancellationToken = default)
        //{
        //    async Task<List<IGH_Goo>> ProcessBranch(List<IGH_Goo> branch, GH_Path path, CancellationToken token)
        //    {
        //        var tasks = branch.Select(item => processItemFunc(item, token));
        //        var results = await Task.WhenAll(tasks);
        //        return results.Where(r => r != null).ToList();
        //    }

        //    return await ProcessBranchesInParallel(inputTree, ProcessBranch, groupIdenticalBranches, cancellationToken);
        //}

        /// <summary>
        /// Processes string data tree in parallel
        /// </summary>
        //public static async Task<GH_Structure<GH_String>> ProcessBranchesInParallel(
        //    GH_Structure<GH_String> inputTree,
        //    Func<List<GH_String>, GH_Path, CancellationToken, Task<List<GH_String>>> processBranchFunc,
        //    bool groupIdenticalBranches = true,
        //    CancellationToken cancellationToken = default)
        //{
        //    Debug.WriteLine("[ProcessBranchesInParallel] Starting string parallel processing");

        //    // Convert input tree to IGH_Goo tree
        //    var gooTree = new GH_Structure<IGH_Goo>();
        //    foreach (var path in inputTree.Paths)
        //    {
        //        var branch = inputTree.get_Branch(path);
        //        gooTree.AppendRange(branch.Cast<IGH_Goo>(), path);
        //    }

        //    // Process using the generic function
        //    var result = await ProcessBranchesInParallel(
        //        gooTree,
        //        async (branch, path, ct) =>
        //        {
        //            var stringBranch = branch.Cast<GH_String>().ToList();
        //            return (await processBranchFunc(stringBranch, path, ct)).Cast<IGH_Goo>().ToList();
        //        },
        //        groupIdenticalBranches,
        //        cancellationToken);

        //    // Convert result back to GH_String tree
        //    var typedResult = new GH_Structure<GH_String>();
        //    foreach (var path in result.Paths)
        //    {
        //        var branch = result.get_Branch(path);
        //        typedResult.AppendRange(branch.Cast<GH_String>(), path);
        //    }

        //    Debug.WriteLine("[ProcessBranchesInParallel] String parallel processing complete");
        //    return typedResult;
        //}

        private static string GetBranchKey(List<IGH_Goo> branch)
        {
            if (branch == null || branch.Count == 0)
                return "empty";

            return string.Join(",", branch.Select(item =>
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
        }
    }
}
