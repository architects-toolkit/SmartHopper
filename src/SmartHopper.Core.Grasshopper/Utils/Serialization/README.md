# Property Management System V2

This document describes the new, maintainable property management system that replaces the old hardcoded PropertyManager approach.

## Overview

The new system provides a clean, flexible way to specify which properties should be included or excluded during Grasshopper object serialization. It separates concerns into distinct, maintainable components:

- **Property Filtering**: Determines which properties to include/exclude
- **Property Handling**: Manages extraction and application of specific property types
- **Property Management**: Orchestrates the entire process

## Key Benefits

### ✅ **Maintainable**
- Clear separation of concerns
- Easy to add new component types
- No more hardcoded property lists scattered throughout code

### ✅ **Flexible**
- Multiple serialization contexts (AI-optimized, full, compact, etc.)
- Fluent builder API for custom configurations
- Component category-based filtering

### ✅ **Extensible**
- Plugin architecture for property handlers
- Easy to add new property types or special handling
- Support for custom filtering rules

### ✅ **Type-Safe**
- Strongly typed configuration
- Compile-time checking of property rules
- Clear interfaces and contracts

## Architecture

```
┌─────────────────────┐    ┌──────────────────────┐    ┌─────────────────────┐
│   PropertyFilter    │    │  PropertyHandler     │    │  PropertyManager    │
│                     │    │                      │    │                     │
│ • Filtering Rules   │    │ • Extraction Logic   │    │ • Orchestration     │
│ • Context-based     │    │ • Type Conversion    │    │ • High-level API    │
│ • Category Support  │    │ • Special Handling   │    │ • Factory Methods   │
└─────────────────────┘    └──────────────────────┘    └─────────────────────┘
```

## Quick Start

### Basic Usage

```csharp
// Use predefined contexts
var aiManager = PropertyManagerFactory.CreateForAI();
var fullManager = PropertyManagerFactory.CreateForFullSerialization();
var compactManager = PropertyManagerFactory.CreateForCompactSerialization();

// Extract properties from any Grasshopper object
var slider = new GH_NumberSlider();
var properties = aiManager.ExtractProperties(slider);

// Apply properties to another object
var targetSlider = new GH_NumberSlider();
var results = aiManager.ApplyProperties(targetSlider, properties);
```

### Custom Filtering

```csharp
// Build custom filters with fluent API
var customManager = PropertyFilterBuilder
    .Create()
    .WithCore(true)                    // Include core properties
    .WithParameters(true)              // Include parameter properties
    .WithCategories(ComponentCategory.Essential)  // Only essential components
    .Include("CustomProperty")         // Always include specific properties
    .Exclude("LegacyProperty")        // Always exclude specific properties
    .ExcludeDataType(true)            // Exclude DataType property
    .BuildManager();
```

### Document Extraction

```csharp
// Extract entire documents with different contexts
var objects = GetGrasshopperObjects();

// AI-optimized extraction
var aiDocument = DocumentIntrospectionV2.ExtractionFactory.ForAI(objects);

// Full fidelity extraction
var fullDocument = DocumentIntrospectionV2.ExtractionFactory.ForFullSerialization(objects);

// Custom extraction
var customRule = PropertyFilterBuilder.Create().ForSliders().Build();
var customDocument = DocumentIntrospectionV2.ExtractionFactory.WithCustomFilter(objects, customRule);
```

## Serialization Contexts

The system provides several predefined contexts for common scenarios:

### `SerializationContext.AIOptimized`
- **Purpose**: Clean, predictable structure for AI processing
- **Includes**: Core + Parameters + Essential UI components
- **Excludes**: Runtime data, redundant properties
- **Use Case**: AI tools, automated processing

### `SerializationContext.FullSerialization`
- **Purpose**: Maximum fidelity preservation
- **Includes**: All properties and categories
- **Excludes**: Only globally blacklisted properties
- **Use Case**: Complete backup, migration

### `SerializationContext.CompactSerialization`
- **Purpose**: Minimal data for storage efficiency
- **Includes**: Core properties only
- **Excludes**: UI properties, metadata
- **Use Case**: Network transfer, storage optimization

### `SerializationContext.ParametersOnly`
- **Purpose**: Parameter-focused extraction
- **Includes**: Core + Parameter properties only
- **Excludes**: Component-specific properties
- **Use Case**: Parameter analysis, data flow tracking

## Component Categories

Properties are organized by component categories for easy management:

```csharp
[Flags]
public enum ComponentCategory
{
    None = 0,
    Panel = 1 << 0,           // GH_Panel properties
    Scribble = 1 << 1,        // GH_Scribble properties
    Slider = 1 << 2,          // GH_NumberSlider properties
    ValueList = 1 << 4,       // GH_ValueList properties
    Script = 1 << 6,          // Script component properties
    // ... more categories

    // Convenience combinations
    Essential = Panel | Scribble | Slider | ValueList | Script,
    UI = Panel | Scribble | Button | ColorWheel,
    All = ~None
}
```

### Using Categories

```csharp
// Include only UI components
var uiManager = PropertyManagerFactory.CreateWithCategories(ComponentCategory.UI);

// Include essential + data components
var dataManager = PropertyManagerFactory.CreateWithCategories(
    ComponentCategory.Essential | ComponentCategory.Data);

// Include everything except advanced components
var basicManager = PropertyFilterBuilder
    .Create()
    .Maximum()
    .RemoveCategories(ComponentCategory.Advanced)
    .BuildManager();
```

## Property Handlers

The system uses specialized handlers for different property types:

### Built-in Handlers

- **`PersistentDataPropertyHandler`**: Handles parameter data serialization
- **`SliderCurrentValuePropertyHandler`**: Special formatting for slider values
- **`ExpressionPropertyHandler`**: Reflection-based expression extraction
- **`ColorPropertyHandler`**: Color conversion with DataTypeSerializer
- **`FontPropertyHandler`**: Font property handling
- **`DataMappingPropertyHandler`**: GH_DataMapping enum handling
- **`DefaultPropertyHandler`**: Fallback for standard properties

### Custom Handlers

```csharp
public class CustomPropertyHandler : PropertyHandlerBase
{
    public override int Priority => 50;

    public override bool CanHandle(object sourceObject, string propertyName)
    {
        return propertyName == "MyCustomProperty";
    }

    public override object ExtractProperty(object sourceObject, string propertyName)
    {
        // Custom extraction logic
        return ProcessCustomProperty(sourceObject);
    }

    public override bool ApplyProperty(object targetObject, string propertyName, object value)
    {
        // Custom application logic
        return ApplyCustomProperty(targetObject, value);
    }
}

// Register the custom handler
PropertyHandlerRegistry.Instance.RegisterHandler(new CustomPropertyHandler());
```

## Advanced Scenarios

### Dynamic Filtering

```csharp
public static PropertyManagerV2 CreateDynamicManager(object grasshopperObject)
{
    return grasshopperObject switch
    {
        GH_NumberSlider => PropertyFilterBuilder.Create().ForSliders().BuildManager(),
        GH_Panel => PropertyFilterBuilder.Create().ForPanels().BuildManager(),
        _ => PropertyManagerFactory.CreateForAI()
    };
}
```

### Conditional Properties

```csharp
var conditionalManager = PropertyFilterBuilder
    .Create()
    .WithCore(true)
    .Configure(rule =>
    {
        if (Environment.GetEnvironmentVariable("DEBUG") == "1")
        {
            rule.AdditionalIncludes.Add("DebugInfo");
        }

        if (DateTime.Now.DayOfWeek == DayOfWeek.Monday)
        {
            rule.IncludeCategories |= ComponentCategory.Advanced;
        }
    })
    .BuildManager();
```

### Performance Analysis

```csharp
var manager = PropertyManagerFactory.CreateForAI();
var slider = new GH_NumberSlider();

// Get detailed analysis
var summary = manager.CreateExtractionSummary(slider);
Console.WriteLine($"Would extract {summary.AllowedProperties.Count} of {summary.TotalProperties} properties");

// Check specific properties
var shouldInclude = manager.ShouldIncludeProperty("CurrentValue", slider);
```

## Migration Guide

### From Old PropertyManager

```csharp
// OLD WAY ❌
var oldManager = new PropertyManager();
var isAllowed = oldManager.IsPropertyInWhitelist("CurrentValue");
var properties = oldManager.ExtractProperties(obj);

// NEW WAY ✅
var newManager = PropertyManagerFactory.CreateForAI();
var isAllowed = newManager.ShouldIncludeProperty("CurrentValue", obj);
var properties = newManager.ExtractProperties(obj);
```

### Updating DocumentIntrospection

```csharp
// OLD WAY ❌
var document = DocumentIntrospection.GetObjectsDetails(objects, includeMetadata, includeGroups);

// NEW WAY ✅
var document = DocumentIntrospectionV2.ExtractDocument(objects, SerializationContext.AIOptimized, includeMetadata, includeGroups);

// Or use factory methods
var document = DocumentIntrospectionV2.ExtractionFactory.ForAI(objects);
```

## Configuration Examples

### Slider-Specific Configuration

```csharp
var sliderManager = PropertyFilterBuilder
    .Create()
    .ForSliders()
    .Include("DisplayFormat", "TickCount")
    .Exclude("Minimum", "Maximum")  // Use CurrentValue format instead
    .BuildManager();
```

### Panel-Specific Configuration

```csharp
var panelManager = PropertyFilterBuilder
    .FromContext(SerializationContext.AIOptimized)
    .ForPanels()
    .Include("BackgroundColor", "BorderColor")
    .BuildManager();
```

### Debug Configuration

```csharp
var debugManager = PropertyFilterBuilder
    .FromContext(SerializationContext.AIOptimized)
    .ForDebugging()
    .Include("InstanceDescription", "ComponentGuid")
    .BuildManager();
```

## Best Practices

### ✅ **Do**
- Use predefined contexts when possible
- Create specific managers for different use cases
- Use the builder pattern for complex configurations
- Register custom handlers for special property types
- Test property extraction with `CreateExtractionSummary()`

### ❌ **Don't**
- Hardcode property lists in business logic
- Mix filtering logic with extraction logic
- Create overly complex custom rules without testing
- Ignore the component category system
- Forget to handle null values in custom handlers

## Troubleshooting

### Property Not Being Extracted

1. Check if it's in the global blacklist
2. Verify the serialization context includes the property type
3. Check if the component category is included
4. Use `CreateExtractionSummary()` to analyze what would be extracted

### Custom Handler Not Working

1. Verify the handler is registered with `PropertyHandlerRegistry`
2. Check the `Priority` value (higher priority = tried first)
3. Ensure `CanHandle()` returns true for your property
4. Test with `GetHandlerInfo()` to see registered handlers

### Performance Issues

1. Use more restrictive contexts (Compact vs Full)
2. Limit component categories to only what's needed
3. Profile different configurations with performance examples
4. Consider caching property managers for repeated use

## Future Extensions

The system is designed to be easily extensible:

- **New Component Types**: Add to `ComponentCategory` enum and `PropertyFilterConfig`
- **New Contexts**: Add to `SerializationContext` enum and `ContextRules`
- **New Handlers**: Implement `IPropertyHandler` and register
- **New Filters**: Use `PropertyFilterBuilder` or create custom `PropertyFilterRule`

This architecture ensures the property management system can grow with the project while maintaining clean, maintainable code.
