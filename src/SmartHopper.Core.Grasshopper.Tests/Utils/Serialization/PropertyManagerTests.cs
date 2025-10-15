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
    using SmartHopper.Core.Models.Components;
    using Xunit;

    /// <summary>
    /// Unit tests for PropertyManager utility class.
    /// Tests property whitelisting, type conversion, and property setting.
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
        [InlineData("InvalidProperty", false)]
        [InlineData("RandomName", false)]
        public void IsPropertyInWhitelist_VariousProperties_ReturnsExpectedResult(string propertyName, bool expected)
        {
            // Act
            var result = PropertyManager.IsPropertyInWhitelist(propertyName);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void GetChildProperties_PropertiesKey_ReturnsChildList()
        {
            // Act
            var result = PropertyManager.GetChildProperties("Properties");

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("Properties", result[0]);
        }

        [Fact]
        public void GetChildProperties_ValueKey_ReturnsNull()
        {
            // Act
            var result = PropertyManager.GetChildProperties("Value");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetChildProperties_NonExistentKey_ReturnsNull()
        {
            // Act
            var result = PropertyManager.GetChildProperties("NonExistent");

            // Assert
            Assert.Null(result);
        }

        #endregion

        #region Omitted Properties Tests

        [Theory]
        [InlineData("VolatileData", true)]
        [InlineData("DataType", true)]
        [InlineData("Properties", true)]
        [InlineData("NickName", false)]
        [InlineData("Value", false)]
        public void IsPropertyOmitted_VariousProperties_ReturnsExpectedResult(string propertyName, bool expected)
        {
            // Act
            var result = PropertyManager.IsPropertyOmitted(propertyName);

            // Assert
            Assert.Equal(expected, result);
        }

        #endregion
    }
}
