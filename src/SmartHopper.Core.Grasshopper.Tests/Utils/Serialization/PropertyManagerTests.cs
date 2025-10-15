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

        #region Type Conversion Tests - Basic Types

        [Fact]
        public void SetProperty_StringValue_SetsCorrectly()
        {
            // Arrange
            var testObj = new TestClass();
            var value = "TestValue";

            // Act
            PropertyManager.SetProperty(testObj, "StringProperty", value);

            // Assert
            Assert.Equal("TestValue", testObj.StringProperty);
        }

        [Fact]
        public void SetProperty_IntValue_SetsCorrectly()
        {
            // Arrange
            var testObj = new TestClass();
            var value = 42;

            // Act
            PropertyManager.SetProperty(testObj, "IntProperty", value);

            // Assert
            Assert.Equal(42, testObj.IntProperty);
        }

        [Fact]
        public void SetProperty_DoubleValue_SetsCorrectly()
        {
            // Arrange
            var testObj = new TestClass();
            var value = 3.14;

            // Act
            PropertyManager.SetProperty(testObj, "DoubleProperty", value);

            // Assert
            Assert.Equal(3.14, testObj.DoubleProperty);
        }

        [Fact]
        public void SetProperty_BoolValue_SetsCorrectly()
        {
            // Arrange
            var testObj = new TestClass();
            var value = true;

            // Act
            PropertyManager.SetProperty(testObj, "BoolProperty", value);

            // Assert
            Assert.True(testObj.BoolProperty);
        }

        [Fact]
        public void SetProperty_FloatValue_SetsCorrectly()
        {
            // Arrange
            var testObj = new TestClass();
            var value = 2.5f;

            // Act
            PropertyManager.SetProperty(testObj, "FloatProperty", value);

            // Assert
            Assert.Equal(2.5f, testObj.FloatProperty);
        }

        #endregion

        #region Type Conversion Tests - JValue

        [Fact]
        public void SetProperty_JValueString_ConvertsAndSets()
        {
            // Arrange
            var testObj = new TestClass();
            var jValue = new JValue("TestValue");

            // Act
            PropertyManager.SetProperty(testObj, "StringProperty", jValue);

            // Assert
            Assert.Equal("TestValue", testObj.StringProperty);
        }

        [Fact]
        public void SetProperty_JValueInt_ConvertsAndSets()
        {
            // Arrange
            var testObj = new TestClass();
            var jValue = new JValue(42);

            // Act
            PropertyManager.SetProperty(testObj, "IntProperty", jValue);

            // Assert
            Assert.Equal(42, testObj.IntProperty);
        }

        [Fact]
        public void SetProperty_JValueLongToInt_ConvertsCorrectly()
        {
            // Arrange
            var testObj = new TestClass();
            var jValue = new JValue(42L); // Long value

            // Act
            PropertyManager.SetProperty(testObj, "IntProperty", jValue);

            // Assert
            Assert.Equal(42, testObj.IntProperty);
        }

        [Fact]
        public void SetProperty_JValueNull_SetsNull()
        {
            // Arrange
            var testObj = new TestClass { StringProperty = "Initial" };
            var jValue = JValue.CreateNull();

            // Act
            PropertyManager.SetProperty(testObj, "StringProperty", jValue);

            // Assert
            Assert.Null(testObj.StringProperty);
        }

        #endregion

        #region Type Conversion Tests - Color

        [Fact]
        public void SetProperty_ColorFromRgbString_SetsCorrectly()
        {
            // Arrange
            var testObj = new TestClass();
            var colorString = "255,128,64"; // RGB format

            // Act
            PropertyManager.SetProperty(testObj, "ColorProperty", colorString);

            // Assert
            Assert.Equal(Color.FromArgb(255, 128, 64), testObj.ColorProperty);
        }

        [Fact]
        public void SetProperty_ColorFromNamedColor_SetsCorrectly()
        {
            // Arrange
            var testObj = new TestClass();
            var colorString = "Red";

            // Act
            PropertyManager.SetProperty(testObj, "ColorProperty", colorString);

            // Assert
            Assert.Equal(Color.Red, testObj.ColorProperty);
        }

        [Fact]
        public void SetProperty_ColorFromHexString_SetsCorrectly()
        {
            // Arrange
            var testObj = new TestClass();
            var colorString = "#FF8040";

            // Act
            PropertyManager.SetProperty(testObj, "ColorProperty", colorString);

            // Assert
            Assert.Equal(Color.FromArgb(255, 128, 64), testObj.ColorProperty);
        }

        #endregion

        #region Type Conversion Tests - Enum

        [Fact]
        public void SetProperty_EnumFromString_SetsCorrectly()
        {
            // Arrange
            var testObj = new TestClass();
            var enumValue = "Value2";

            // Act
            PropertyManager.SetProperty(testObj, "EnumProperty", enumValue);

            // Assert
            Assert.Equal(TestEnum.Value2, testObj.EnumProperty);
        }

        [Fact]
        public void SetProperty_EnumFromInt_SetsCorrectly()
        {
            // Arrange
            var testObj = new TestClass();
            var enumValue = 1;

            // Act
            PropertyManager.SetProperty(testObj, "EnumProperty", enumValue);

            // Assert
            Assert.Equal(TestEnum.Value2, testObj.EnumProperty);
        }

        #endregion

        #region Type Conversion Tests - Nested Properties

        [Fact]
        public void SetProperty_NestedProperty_SetsCorrectly()
        {
            // Arrange
            var testObj = new TestClass { NestedObject = new NestedClass() };
            var value = "NestedValue";

            // Act
            PropertyManager.SetProperty(testObj, "NestedObject.NestedProperty", value);

            // Assert
            Assert.Equal("NestedValue", testObj.NestedObject.NestedProperty);
        }

        [Fact]
        public void SetProperty_NestedPropertyNull_HandlesGracefully()
        {
            // Arrange
            var testObj = new TestClass { NestedObject = null };
            var value = "NestedValue";

            // Act & Assert - Should not throw
            PropertyManager.SetProperty(testObj, "NestedObject.NestedProperty", value);

            // Property should remain null since parent is null
            Assert.Null(testObj.NestedObject);
        }

        #endregion

        #region Type Conversion Tests - ComponentProperty Wrapper

        [Fact]
        public void SetProperty_ComponentPropertyWrapper_UnwrapsAndSets()
        {
            // Arrange
            var testObj = new TestClass();
            var wrapper = new JObject
            {
                ["value"] = "WrappedValue"
            };

            // Act
            PropertyManager.SetProperty(testObj, "StringProperty", wrapper);

            // Assert
            Assert.Equal("WrappedValue", testObj.StringProperty);
        }

        #endregion

        #region SetProperties Tests

        [Fact]
        public void SetProperties_MultipleProperties_SetsAllCorrectly()
        {
            // Arrange
            var testObj = new TestClass();
            var properties = new Dictionary<string, ComponentProperty>
            {
                { "StringProperty", new ComponentProperty { Value = "Test" } },
                { "IntProperty", new ComponentProperty { Value = 42 } },
                { "BoolProperty", new ComponentProperty { Value = true } }
            };

            // Act
            PropertyManager.SetProperties(testObj, properties);

            // Assert
            Assert.Equal("Test", testObj.StringProperty);
            Assert.Equal(42, testObj.IntProperty);
            Assert.True(testObj.BoolProperty);
        }

        [Fact]
        public void SetProperties_NullValue_SkipsProperty()
        {
            // Arrange
            var testObj = new TestClass { StringProperty = "Initial" };
            var properties = new Dictionary<string, ComponentProperty>
            {
                { "StringProperty", null },
            };

            // Act
            PropertyManager.SetProperties(testObj, properties);

            // Assert - Should remain unchanged
            Assert.Equal("Initial", testObj.StringProperty);
        }

        [Fact]
        public void SetProperties_InvalidProperty_ContinuesWithOthers()
        {
            // Arrange
            var testObj = new TestClass();
            var properties = new Dictionary<string, ComponentProperty>
            {
                { "InvalidProperty", new ComponentProperty { Value = "Test" } },
                { "StringProperty", new ComponentProperty { Value = "Valid" } },
            };

            // Act
            PropertyManager.SetProperties(testObj, properties);

            // Assert - Valid property should be set
            Assert.Equal("Valid", testObj.StringProperty);
        }

        #endregion

        #region Test Helper Classes

        public class TestClass
        {
            public string StringProperty { get; set; }

            public int IntProperty { get; set; }

            public double DoubleProperty { get; set; }

            public bool BoolProperty { get; set; }

            public float FloatProperty { get; set; }

            public Color ColorProperty { get; set; }

            public TestEnum EnumProperty { get; set; }

            public NestedClass NestedObject { get; set; }
        }

        public class NestedClass
        {
            public string NestedProperty { get; set; }
        }

        public enum TestEnum
        {
            Value1 = 0,
            Value2 = 1,
            Value3 = 2
        }

        #endregion
    }
}
