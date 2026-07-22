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
using System.Linq;
using Grasshopper.Kernel;
using SmartHopper.Core.Grasshopper.Contracts;
using SmartHopper.Infrastructure.Mcp;

namespace SmartHopper.Core.Grasshopper.Utils.Canvas
{
    /// <summary>
    /// Centralized guard that prevents AI-driven canvas tools from touching components
    /// that have opted into protection (e.g. the SmartHopper MCP Server while enabled)
    /// and any components directly wired to them.
    /// </summary>
    public static class CanvasProtection
    {
        /// <summary>
        /// Gets the set of instance GUIDs that are currently protected. This includes
        /// every canvas object whose <see cref="ICanvasProtectedComponent.IsProtected"/>
        /// is <c>true</c>, plus any component connected to such an object by a single wire.
        /// </summary>
        public static IReadOnlySet<Guid> GetProtectedInstanceGuids()
        {
            var doc = CanvasAccess.GetCurrentCanvas();
            if (doc == null)
            {
                return new HashSet<Guid>();
            }

            var objects = doc.Objects.OfType<IGH_DocumentObject>().ToList();

            var protectedComponents = objects
                .OfType<ICanvasProtectedComponent>()
                .Where(c => c.IsProtected)
                .Cast<IGH_DocumentObject>()
                .ToList();

            // Add any components the user explicitly locked from the canvas context menu.
            var userLockedGuids = McpCanvasLockState.GetLockedGuids();
            protectedComponents.AddRange(objects.Where(o => userLockedGuids.Contains(o.InstanceGuid)));

            var protectedGuids = new HashSet<Guid>(protectedComponents.Select(o => o.InstanceGuid));

            foreach (var protectedComponent in protectedComponents.OfType<IGH_Component>())
            {
                foreach (var input in protectedComponent.Params.Input)
                {
                    foreach (var source in input.Sources)
                    {
                        var owner = FindOwner(source, objects);
                        if (owner != null)
                        {
                            protectedGuids.Add(owner.InstanceGuid);
                        }
                    }
                }

                foreach (var output in protectedComponent.Params.Output)
                {
                    foreach (var recipient in output.Recipients)
                    {
                        var owner = FindOwner(recipient, objects);
                        if (owner != null)
                        {
                            protectedGuids.Add(owner.InstanceGuid);
                        }
                    }
                }
            }

            return protectedGuids;
        }

        /// <summary>
        /// Returns whether the given instance GUID is currently protected.
        /// </summary>
        public static bool IsProtected(Guid instanceGuid)
        {
            if (instanceGuid == Guid.Empty)
            {
                return false;
            }

            return GetProtectedInstanceGuids().Contains(instanceGuid);
        }

        /// <summary>
        /// Returns whether the given document object is currently protected.
        /// </summary>
        public static bool IsProtected(IGH_DocumentObject? obj)
        {
            return obj != null && IsProtected(obj.InstanceGuid);
        }

        /// <summary>
        /// Splits a collection of instance GUIDs into allowed and protected sets.
        /// </summary>
        public static (IReadOnlyList<Guid> Allowed, IReadOnlyList<Guid> Protected) FilterProtectedGuids(
            IEnumerable<Guid> guids)
        {
            var protectedSet = GetProtectedInstanceGuids();
            var allowed = new List<Guid>();
            var protectedList = new List<Guid>();

            foreach (var guid in guids)
            {
                if (guid == Guid.Empty)
                {
                    continue;
                }

                if (protectedSet.Contains(guid))
                {
                    protectedList.Add(guid);
                }
                else
                {
                    allowed.Add(guid);
                }
            }

            return (allowed, protectedList);
        }

        /// <summary>
        /// Formats a human-readable message explaining why the given GUIDs were skipped.
        /// </summary>
        public static string FormatProtectionMessage(IEnumerable<Guid> protectedGuids)
        {
            var guids = protectedGuids.ToList();
            if (guids.Count == 0)
            {
                return string.Empty;
            }

            var guidsText = string.Join(", ", guids.Select(g => g.ToString()));
            return $"Skipped protected component(s) ({guidsText}). Protected components and any component directly wired to them cannot be altered by AI tools.";
        }

        private static IGH_DocumentObject? FindOwner(IGH_Param param, IEnumerable<IGH_DocumentObject> objects)
        {
            if (param == null)
            {
                return null;
            }

            var owner = objects.FirstOrDefault(o => ReferenceEquals(o, param));
            owner ??= objects
                .OfType<IGH_Component>()
                .FirstOrDefault(comp =>
                    comp.Params.Input.Any(p => ReferenceEquals(p, param)) ||
                    comp.Params.Output.Any(p => ReferenceEquals(p, param)));

            return owner;
        }
    }
}
