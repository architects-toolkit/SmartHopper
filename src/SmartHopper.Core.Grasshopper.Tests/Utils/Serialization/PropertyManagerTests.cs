/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

namespace SmartHopper.Core.Grasshopper.Tests.Utils.Serialization
{
    using System.Collections.Generic;
    using System.Drawing;
    using Newtonsoft.Json.Linq;
    using SmartHopper.Core.Grasshopper.Utils.Serialization;
    using SmartHopper.Core.Grasshopper.Utils.Serialization.PropertyFilters;
    using SmartHopper.Core.Models.Components;
    using Xunit;

    /// <summary>
    /// Unit tests for PropertyManagerV2 utility class.
    /// Tests property filtering, extraction, and application.
    /// </summary>
    public class PropertyManagerTests
    {
        #region Whitelist Tests

        [Theory]
        [InlineData("Value", true)]
        [InlineData("Locked", true)]
        [InlineData("NickName", true)]
        [InlineData("Expression", true)]
        [InlineData("PersistentData", true)]
        [InlineData("CurrentValue", true)]
        [InlineData("Minimum", true)]
        [InlineData("Maximum", true)]
        [InlineData("VolatileData", false)]
        [InlineData("DataType", false)]
        public void ShouldIncludeProperty_VariousProperties_ReturnsExpectedResult(string propertyName, bool expected)
        {
            // Arrange
            var propertyManager = PropertyManagerFactory.CreateForAI();
            var mockObject = new TestComponent(); // Mock object for context

            // Act
            var result = propertyManager.ShouldIncludeProperty(propertyName, mockObject);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void ExtractProperties_TestComponent_ReturnsExpectedProperties()
        {
            // Arrange
            var propertyManager = PropertyManagerFactory.CreateForAI();
            var testComponent = new TestComponent();

            // Act
            var result = propertyManager.ExtractProperties(testComponent);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Count > 0);
            Assert.True(result.ContainsKey("NickName"));
            Assert.True(result.ContainsKey("Locked"));
        }

        [Fact]
        public void CreateExtractionSummary_TestComponent_ReturnsValidSummary()
        {
            // Arrange
            var propertyManager = PropertyManagerFactory.CreateForAI();
            var testComponent = new TestComponent();

            // Act
            var summary = propertyManager.CreateExtractionSummary(testComponent);

            // Assert
            Assert.NotNull(summary);
            Assert.Equal("TestComponent", summary.ObjectType);
            Assert.True(summary.TotalProperties > 0);
            Assert.NotNull(summary.AllowedProperties);
            Assert.NotNull(summary.ExcludedProperties);
        }

        #endregion

        #region Test Helper Classes

        /// <summary>
        /// Mock component class for testing property extraction.
        /// </summary>
        private class TestComponent
        {
            public string NickName { get; set; } = "Test Component";
            public bool Locked { get; set; } = false;
            public string Value { get; set; } = "TestValue";
            public object VolatileData { get; set; } = null;
            public string DataType { get; set; } = "TestType";
            public string Expression { get; set; } = "x + y";
            public object PersistentData { get; set; } = null;
            public double CurrentValue { get; set; } = 42.0;
            public double Minimum { get; set; } = 0.0;
            public double Maximum { get; set; } = 100.0;
        }

        #endregion
    }
}
