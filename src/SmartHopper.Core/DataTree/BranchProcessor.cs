/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using Grasshopper.Kernel.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SmartHopper.Core.DataTree
{
    /// <summary>
    /// Provides utility methods for processing branches from multiple trees
    /// </summary>
    public static class BranchProcessor
    {
        /// <summary>
        /// Normalizes branch lengths by extending shorter branches with their last item
        /// </summary>
        public static Dictionary<TKey, List<T>> NormalizeBranchLengths<TKey, T>(Dictionary<TKey, List<T>> branches) where T : IGH_Goo
        {
            if (branches == null || !branches.Any())
                return new Dictionary<TKey, List<T>>();

            // Find the maximum length among all branches
            int maxLength = branches.Values.Max(branch => branch?.Count ?? 0);

            var result = new Dictionary<TKey, List<T>>();
            foreach (var kvp in branches)
            {
                var branch = kvp.Value ?? new List<T>();
                var normalizedBranch = new List<T>(branch);

                // If branch is empty, use null or default value for all positions
                if (branch.Count == 0)
                {
                    for (int i = 0; i < maxLength; i++)
                    {
                        normalizedBranch.Add(default(T));
                    }
                }
                else
                {
                    // Extend branch by repeating last item
                    var lastItem = branch[branch.Count - 1];
                    while (normalizedBranch.Count < maxLength)
                    {
                        normalizedBranch.Add(lastItem);
                    }
                }

                result[kvp.Key] = normalizedBranch;
            }

            return result;
        }

        /// <summary>
        /// Processes branches item by item using the provided function
        /// </summary>
        public static List<TResult> ProcessItemsInParallel<TKey, T, TResult>(
            Dictionary<TKey, List<T>> branches,
            Func<Dictionary<TKey, T>, TResult> processItemFunc) where T : IGH_Goo
        {
            if (branches == null || !branches.Any())
                return new List<TResult>();

            // First normalize branch lengths
            var normalizedBranches = NormalizeBranchLengths(branches);

            // Get the maximum length of all branches
            int branchLength = normalizedBranches.Values.Max(branch => branch.Count);

            var results = new List<TResult>();
            for (int i = 0; i < branchLength; i++)
            {
                // Create a dictionary of items at index i from each branch
                var items = normalizedBranches.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value[i]
                );

                // Process items using the provided function
                var result = processItemFunc(items);
                results.Add(result);
            }

            return results;
        }

        /// <summary>
        /// Processes branches item by item using the provided async function
        /// </summary>
        public static async Task<List<TResult>> ProcessItemsInParallelAsync<TKey, T, TResult>(
            Dictionary<TKey, List<T>> branches,
            Func<Dictionary<TKey, T>, Task<TResult>> processItemFunc,
            CancellationToken ct = default) where T : IGH_Goo
        {
            if (branches == null || !branches.Any())
                return new List<TResult>();

            if (ct.IsCancellationRequested)
                return new List<TResult>();

            // First normalize branch lengths
            var normalizedBranches = NormalizeBranchLengths(branches);

            // Get the length of branches (they should all be the same now)
            int branchLength = normalizedBranches.Values.Max(branch => branch.Count);

            var results = new List<TResult>();
            for (int i = 0; i < branchLength; i++)
            {
                if (ct.IsCancellationRequested)
                    break;

                // Create a dictionary of items at index i from each branch
                var items = normalizedBranches.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value[i]
                );

                // Process items using the provided function
                var result = await processItemFunc(items);
                results.Add(result);
            }

            return results;
        }
    }
}
