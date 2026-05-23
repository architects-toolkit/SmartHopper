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
using System.Drawing;
using Grasshopper.Kernel;
using Newtonsoft.Json.Linq;
using SmartHopper.Components.Properties;

namespace SmartHopper.Components.Knowledge
{
    /// <summary>
    /// Deconstructs Discourse forum post JSON objects into individual Grasshopper-friendly fields.
    /// Works with any Discourse instance (McNeel, Ladybug Tools, etc.).
    /// </summary>
    public class DiscoursePostDeconstructComponent : GH_Component
    {
        public DiscoursePostDeconstructComponent()
            : base(
                  "Deconstruct Discourse Post",
                  "DeconstructDiscoursePost",
                  "Deconstruct a Discourse forum post into its individual properties (id, username, topic id, title, date, etc.). Works with any Discourse instance, including McNeel and Ladybug forum posts.",
                  "SmartHopper",
                  "Knowledge")
        {
        }

        public override Guid ComponentGuid => new Guid("E56CBDDA-F00D-48AE-9713-3935117B94DC");

        /// <summary>
        /// Gets the component's icon.
        /// </summary>
        protected override Bitmap Icon => Resources.discoursepostdeconstruct;

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter(
                "Discourse Post",
                "P",
                "JSON representation of Discourse forum posts as returned by Discourse Search Component.",
                GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddIntegerParameter("Id", "I", "Numeric ID of the forum post.", GH_ParamAccess.list);
            pManager.AddTextParameter("Username", "U", "Author username of the forum post.", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Topic Id", "T", "Topic ID that the post belongs to.", GH_ParamAccess.list);
            pManager.AddTextParameter("Topic Title", "Ti", "Title of the topic that the post belongs to.", GH_ParamAccess.list);
            pManager.AddTextParameter("Post URL", "Url", "Full URL to the forum post.", GH_ParamAccess.list);
            pManager.AddTextParameter("Date", "D", "Post creation date as a string.", GH_ParamAccess.list);
            pManager.AddTextParameter("Content", "C", "Raw content of the post, in Markdown.", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Reads", "R", "Number of reads for this post, if available.", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Likes", "L", "Number of likes for this post, if available.", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var jsonPosts = new List<string>();
            if (!DA.GetDataList(0, jsonPosts) || jsonPosts.Count == 0)
            {
                // Clear outputs when no input is provided
                DA.SetDataList(0, new List<int>());
                DA.SetDataList(1, new List<string>());
                DA.SetDataList(2, new List<int>());
                DA.SetDataList(3, new List<string>());
                DA.SetDataList(4, new List<string>());
                DA.SetDataList(5, new List<string>());
                DA.SetDataList(6, new List<string>());
                DA.SetDataList(7, new List<int>());
                DA.SetDataList(8, new List<int>());
                return;
            }

            var ids = new List<int>();
            var usernames = new List<string>();
            var topicIds = new List<int>();
            var titles = new List<string>();
            var postUrls = new List<string>();
            var dates = new List<string>();
            var rawList = new List<string>();
            var readsList = new List<int>();
            var likesList = new List<int>();

            for (int index = 0; index < jsonPosts.Count; index++)
            {
                string json = jsonPosts[index];
                if (string.IsNullOrWhiteSpace(json))
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, $"Empty JSON at index {index}.");
                    ids.Add(0);
                    usernames.Add(string.Empty);
                    topicIds.Add(0);
                    titles.Add(string.Empty);
                    postUrls.Add(string.Empty);
                    dates.Add(string.Empty);
                    rawList.Add(string.Empty);
                    readsList.Add(0);
                    likesList.Add(0);
                    continue;
                }

                try
                {
                    var obj = JObject.Parse(json);

                    int id = obj["id"]?.Value<int?>() ?? 0;
                    string username = obj["username"]?.Value<string>() ?? string.Empty;
                    int topicId = obj["topic_id"]?.Value<int?>() ?? 0;
                    string topicTitle = obj["topic_title"]?.Value<string>() ?? string.Empty;
                    string postUrl = obj["url"]?.Value<string>() ?? string.Empty;

                    string date = obj["date"]?.Value<string>() ?? obj["created_at"]?.Value<string>() ?? string.Empty;
                    string rawContent = obj["raw"]?.Value<string>() ?? string.Empty;

                    int reads = obj["reads"]?.Value<int?>() ?? 0;

                    // Discourse API uses 'like_count' in search results, 'likes' in some contexts
                    int likes = obj["likes"]?.Value<int?>() ?? obj["like_count"]?.Value<int?>() ?? 0;

                    ids.Add(id);
                    usernames.Add(username);
                    topicIds.Add(topicId);
                    titles.Add(topicTitle);
                    postUrls.Add(postUrl);
                    dates.Add(date);
                    rawList.Add(rawContent);
                    readsList.Add(reads);
                    likesList.Add(likes);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[DiscoursePostDeconstruct] Failed to parse JSON at index {index}: {ex.Message}");
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Failed to parse JSON at index {index}: {ex.Message}");

                    ids.Add(0);
                    usernames.Add(string.Empty);
                    topicIds.Add(0);
                    titles.Add(string.Empty);
                    postUrls.Add(string.Empty);
                    dates.Add(string.Empty);
                    rawList.Add(string.Empty);
                    readsList.Add(0);
                    likesList.Add(0);
                }
            }

            DA.SetDataList(0, ids);
            DA.SetDataList(1, usernames);
            DA.SetDataList(2, topicIds);
            DA.SetDataList(3, titles);
            DA.SetDataList(4, postUrls);
            DA.SetDataList(5, dates);
            DA.SetDataList(6, rawList);
            DA.SetDataList(7, readsList);
            DA.SetDataList(8, likesList);
        }
    }
}
