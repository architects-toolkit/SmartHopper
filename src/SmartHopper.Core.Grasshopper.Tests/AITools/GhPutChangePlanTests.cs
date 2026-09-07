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
using GhJSON.Core.SchemaModels;
using SmartHopper.Core.Grasshopper.AITools;
using SmartHopper.Core.Grasshopper.Utils.Canvas;
using Xunit;

namespace SmartHopper.Core.Grasshopper.Tests.AITools
{
    /// <summary>
    /// Tests document-only planning and selection without requiring a Rhino runtime.
    /// </summary>
    public sealed class GhPutChangePlanTests
    {
        /// <summary>
        /// Verifies that unchanged, modified, and added components are classified correctly.
        /// </summary>
        [Fact]
        public void Create_ClassifiesComponentChanges()
        {
            var modifiedGuid = Guid.NewGuid();
            var unchangedGuid = Guid.NewGuid();
            var existing = CreateDocument(
                new[]
                {
                    CreateComponent(1, modifiedGuid, "Slider", "Old"),
                    CreateComponent(2, unchangedGuid, "Panel", "Same"),
                });
            var proposed = CreateDocument(
                new[]
                {
                    CreateComponent(1, modifiedGuid, "Slider", "New"),
                    CreateComponent(2, unchangedGuid, "Panel", "Same"),
                    CreateComponent(3, null, "Addition", "Add"),
                });

            var plan = GhPutChangePlan.Create(proposed, existing);

            Assert.Equal(2, plan.Session.Items.Count);
            Assert.Contains(plan.Session.Items, item => item.Kind == CanvasChangeKind.ComponentModified && item.ComponentId == 1);
            Assert.Contains(plan.Session.Items, item => item.Kind == CanvasChangeKind.ComponentAdded && item.ComponentId == 3);
        }

        /// <summary>
        /// Verifies that dependent wires and groups are omitted when a required component is rejected.
        /// </summary>
        [Fact]
        public void BuildAcceptedDocument_RemovesRejectedDependencies()
        {
            var modifiedGuid = Guid.NewGuid();
            var existing = CreateDocument(new[] { CreateComponent(1, modifiedGuid, "Slider", "Old") });
            var components = new[]
            {
                CreateComponent(1, modifiedGuid, "Slider", "New"),
                CreateComponent(2, null, "Addition", "Add"),
            };
            var connection = new GhJsonConnection
            {
                From = new GhJsonConnectionEndpoint { Id = 1, ParamName = "R" },
                To = new GhJsonConnectionEndpoint { Id = 2, ParamName = "A" },
            };
            var group = new GhJsonGroup
            {
                Id = 3,
                Name = "Generated",
                Members = new List<int> { 1, 2 },
            };
            var proposed = CreateDocument(components, new[] { connection }, new[] { group });
            var plan = GhPutChangePlan.Create(proposed, existing);
            var added = plan.Session.Items.Single(item => item.Kind == CanvasChangeKind.ComponentAdded);

            plan.Session.SetAccepted(added.Key, false);
            var accepted = plan.BuildAcceptedDocument();

            Assert.Single(accepted.Components);
            Assert.Equal(1, accepted.Components[0].Id);
            Assert.Null(accepted.Connections);
            Assert.Single(accepted.Groups!);
            Assert.Equal(new[] { 1 }, accepted.Groups![0].Members);
            Assert.Equal(2, plan.Session.AcceptedCount);
        }

        /// <summary>
        /// Verifies that accepted wires to unchanged live components are retained for post-placement connection.
        /// </summary>
        [Fact]
        public void GetAcceptedExternalConnections_ReturnsConnectionsToUnchangedComponents()
        {
            var existingGuid = Guid.NewGuid();
            var unchanged = CreateComponent(1, existingGuid, "Slider", "Same");
            var added = CreateComponent(2, null, "Addition", "Add");
            var connection = new GhJsonConnection
            {
                From = new GhJsonConnectionEndpoint { Id = 1, ParamName = "R" },
                To = new GhJsonConnectionEndpoint { Id = 2, ParamName = "A" },
            };
            var plan = GhPutChangePlan.Create(
                CreateDocument(new[] { unchanged, added }, new[] { connection }),
                CreateDocument(new[] { unchanged }));

            var accepted = plan.BuildAcceptedDocument();
            var externalConnections = plan.GetAcceptedExternalConnections(accepted);

            Assert.Single(accepted.Components);
            Assert.Single(externalConnections);
            Assert.Equal(existingGuid, plan.GetExistingInstanceGuid(1));
        }

        private static GhJsonDocument CreateDocument(
            IEnumerable<GhJsonComponent> components,
            IEnumerable<GhJsonConnection>? connections = null,
            IEnumerable<GhJsonGroup>? groups = null)
        {
            return new GhJsonDocument("1.0", null, components, connections, groups);
        }

        private static GhJsonComponent CreateComponent(
            int id,
            Guid? instanceGuid,
            string name,
            string nickName)
        {
            return new GhJsonComponent
            {
                Id = id,
                InstanceGuid = instanceGuid,
                ComponentGuid = Guid.Parse("3e8f5f24-8e8c-4f11-a538-4dcd67e3590f"),
                Name = name,
                NickName = nickName,
                Pivot = new GhJsonPivot(id * 100, 100),
            };
        }
    }
}
