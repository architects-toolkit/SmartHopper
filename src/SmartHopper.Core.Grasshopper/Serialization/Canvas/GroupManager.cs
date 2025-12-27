/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using SmartHopper.Core.Grasshopper.Serialization.GhJson;
using SmartHopper.Core.Serialization.DataTypes;

namespace SmartHopper.Core.Grasshopper.Serialization.Canvas
{
    /// <summary>
    /// Handles recreation of Grasshopper groups from GhJSON definitions.
    /// Manages group creation, member assignment, and styling.
    /// </summary>
    public static class GroupManager
    {
        /// <summary>
        /// Creates all groups from deserialization result.
        /// </summary>
        /// <param name="result">Deserialization result with components and document</param>
        /// <returns>Number of groups created</returns>
        public static int CreateGroups(DeserializationResult result)
        {
            if (result?.Document?.Groups == null || result.Document.Groups.Count == 0)
            {
                return 0;
            }

            var document = Instances.ActiveCanvas?.Document;
            if (document == null)
            {
                Debug.WriteLine("[GroupManager] No active Grasshopper document");
                return 0;
            }

            var groupsCreated = 0;
            var idToComponent = CanvasUtilities.BuildIdMapping(result);

            foreach (var groupInfo in result.Document.Groups)
            {
                try
                {
                    if (CreateGroup(groupInfo, idToComponent, document))
                    {
                        groupsCreated++;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[GroupManager] Error creating group '{groupInfo.Name}': {ex.Message}");
                }
            }

            Debug.WriteLine($"[GroupManager] Created {groupsCreated} groups");
            return groupsCreated;
        }

        /// <summary>
        /// Creates a single group with members.
        /// </summary>
        private static bool CreateGroup(
            SmartHopper.Core.Models.Document.GroupInfo groupInfo,
            Dictionary<int, IGH_DocumentObject> idToComponent,
            GH_Document document)
        {
            // Get member components
            var members = new List<IGH_DocumentObject>();
            if (groupInfo.Members != null)
            {
                foreach (var memberId in groupInfo.Members)
                {
                    if (idToComponent.TryGetValue(memberId, out var component))
                    {
                        members.Add(component);
                    }
                }
            }

            if (members.Count == 0)
            {
                Debug.WriteLine($"[GroupManager] No valid members for group '{groupInfo.Name}'");
                return false;
            }

            // Create group
            var group = new GH_Group();
            group.NickName = groupInfo.Name ?? "Group";

            // Set color if provided
            if (!string.IsNullOrEmpty(groupInfo.Color))
            {
                try
                {
                    if (DataTypeSerializer.TryDeserializeFromPrefix(groupInfo.Color, out var colorObj) && colorObj is Color color)
                    {
                        group.Colour = color;
                        Debug.WriteLine($"[GroupManager] Set group color to {color}");
                    }
                    else
                    {
                        Debug.WriteLine($"[GroupManager] Failed to parse color '{groupInfo.Color}' - using default");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[GroupManager] Error parsing color '{groupInfo.Color}': {ex.Message}");
                }
            }
            else
            {
                Debug.WriteLine($"[GroupManager] No color specified - using default group color");
            }

            // Add members to group
            foreach (var member in members)
            {
                group.AddObject(member.InstanceGuid);
            }

            // Add group to document
            document.AddObject(group, false);

            Debug.WriteLine($"[GroupManager] Created group '{group.NickName}' with {members.Count} members");
            return true;
        }
    }
}
