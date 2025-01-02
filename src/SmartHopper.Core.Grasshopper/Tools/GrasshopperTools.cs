/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using Grasshopper.Kernel;
using Newtonsoft.Json;
using SmartHopper.Config.Models;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SmartHopper.Core.Grasshopper.Tools
{
    public class GrasshopperTools
    {
        public static List<TextChatModel> CallTool(string apiKey, string model, string toolName, string args = "", string toolCallId = "")
        {
            Debug.WriteLine("Calling tool: " + toolName);
            Debug.WriteLine("with args: " + args);

            switch (toolName)
            {
                case "gh_get_selected":
                    return GetComponents(apiKey, model, "selected", toolCallId);
                case "gh_get_errors":
                    return GetComponents(apiKey, model, "errors", toolCallId);
                case "gh_get_entire_file":
                    return GetComponents(apiKey, model, "", toolCallId);
                case "gh_get_feedback":
                    return GetFeedback(apiKey, model, toolCallId);
                default:
                    Debug.WriteLine("Invalid tool name: " + toolName);
                    return null;
            }
        }

        private static List<TextChatModel> GetComponents(string apiKey, string model, string filter = "", string toolCallId = "")
        {
            var objects = GHCanvasUtils.GetCurrentObjects();
            string toolName = "gh_get_entire_file";

            switch (filter)
            {
                case "selected":
                    objects = objects.Where(obj => obj.Attributes.Selected).ToList();
                    toolName = "gh_get_selected";
                    break;
                case "errors":
                    objects = objects.Where(obj =>
                        obj.RuntimeMessages(GH_RuntimeMessageLevel.Error).Count > 0 ||
                        obj.RuntimeMessages(GH_RuntimeMessageLevel.Warning).Count > 0
                    ).ToList();
                    toolName = "gh_get_errors";
                    break;
            }

            var document = GHDocumentUtils.GetObjectsDetails(objects);
            string jsonOutput = JsonConvert.SerializeObject(document, Formatting.None);

            return new List<TextChatModel>
            {
                new TextChatModel()
                {
                    Author = "tool",
                    Body = jsonOutput,
                    ToolName = toolName,
                    ToolCallId = toolCallId
                }
            };
        }

        private static List<TextChatModel> GetFeedback(string apiKey, string model, string toolCallId = "")
        {
            return null;
        }
    }
}
