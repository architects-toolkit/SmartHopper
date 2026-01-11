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
 * along with this library; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA 02110-1301, USA.
 */

using System;
using System.Collections.Generic;
using System.Linq;

namespace SmartHopper.Core.Grasshopper.Utils.Serialization.PropertyHandlers
{
    /// <summary>
    /// Registry for property handlers that manages handler discovery and selection.
    /// Provides a centralized way to register and retrieve appropriate handlers
    /// for different property types and objects.
    /// </summary>
    public class PropertyHandlerRegistry
    {
        private readonly List<IPropertyHandler> _handlers;
        private static readonly Lazy<PropertyHandlerRegistry> _instance = new(() => new PropertyHandlerRegistry());

        /// <summary>
        /// Gets the singleton instance of the property handler registry.
        /// </summary>
        public static PropertyHandlerRegistry Instance => _instance.Value;

        private PropertyHandlerRegistry()
        {
            _handlers = new List<IPropertyHandler>();
            RegisterDefaultHandlers();
        }

        /// <summary>
        /// Registers a property handler with the registry.
        /// </summary>
        /// <param name="handler">The handler to register.</param>
        public void RegisterHandler(IPropertyHandler handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            _handlers.Add(handler);

            // Keep handlers sorted by priority (highest first)
            _handlers.Sort((h1, h2) => h2.Priority.CompareTo(h1.Priority));
        }

        /// <summary>
        /// Gets the most appropriate handler for the given object and property.
        /// </summary>
        /// <param name="sourceObject">The object containing the property.</param>
        /// <param name="propertyName">The name of the property.</param>
        /// <returns>The best matching handler, or null if no handler can process the property.</returns>
        public IPropertyHandler GetHandler(object sourceObject, string propertyName)
        {
            if (sourceObject == null || string.IsNullOrEmpty(propertyName))
                return null;

            return _handlers.FirstOrDefault(handler => handler.CanHandle(sourceObject, propertyName));
        }

        /// <summary>
        /// Gets all handlers that can process the given object and property.
        /// Useful for scenarios where multiple handlers might be applicable.
        /// </summary>
        /// <param name="sourceObject">The object containing the property.</param>
        /// <param name="propertyName">The name of the property.</param>
        /// <returns>All matching handlers, ordered by priority.</returns>
        public IEnumerable<IPropertyHandler> GetAllHandlers(object sourceObject, string propertyName)
        {
            if (sourceObject == null || string.IsNullOrEmpty(propertyName))
                return Enumerable.Empty<IPropertyHandler>();

            return _handlers.Where(handler => handler.CanHandle(sourceObject, propertyName));
        }

        /// <summary>
        /// Extracts a property value using the most appropriate handler.
        /// </summary>
        /// <param name="sourceObject">The object to extract from.</param>
        /// <param name="propertyName">The property to extract.</param>
        /// <returns>The extracted value, or null if extraction fails.</returns>
        public object ExtractProperty(object sourceObject, string propertyName)
        {
            var handler = GetHandler(sourceObject, propertyName);
            return handler?.ExtractProperty(sourceObject, propertyName);
        }

        /// <summary>
        /// Applies a property value using the most appropriate handler.
        /// </summary>
        /// <param name="targetObject">The object to apply the property to.</param>
        /// <param name="propertyName">The name of the property to set.</param>
        /// <param name="value">The value to set.</param>
        /// <returns>True if the property was successfully applied.</returns>
        public bool ApplyProperty(object targetObject, string propertyName, object value)
        {
            try
            {
                var handler = GetHandler(targetObject, propertyName);
                return handler?.ApplyProperty(targetObject, propertyName, value) ?? false;
            }
            catch (Exception ex)
            {
                // Log the error but don't fail the entire operation
                System.Diagnostics.Debug.WriteLine($"[PropertyHandlerRegistry] Failed to apply property '{propertyName}' to {targetObject?.GetType().Name ?? "null"}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets related properties that should be extracted along with the primary property.
        /// </summary>
        /// <param name="sourceObject">The source object.</param>
        /// <param name="propertyName">The primary property name.</param>
        /// <returns>Additional property names to extract.</returns>
        public IEnumerable<string> GetRelatedProperties(object sourceObject, string propertyName)
        {
            var handler = GetHandler(sourceObject, propertyName);
            return handler?.GetRelatedProperties(sourceObject, propertyName) ?? Enumerable.Empty<string>();
        }

        /// <summary>
        /// Extracts multiple properties from an object, including related properties.
        /// </summary>
        /// <param name="sourceObject">The object to extract from.</param>
        /// <param name="propertyNames">The properties to extract.</param>
        /// <returns>Dictionary of property names and their extracted values.</returns>
        public Dictionary<string, object> ExtractProperties(object sourceObject, IEnumerable<string> propertyNames)
        {
            var result = new Dictionary<string, object>();
            var processedProperties = new HashSet<string>();

            foreach (var propertyName in propertyNames)
            {
                if (processedProperties.Contains(propertyName))
                    continue;

                var value = ExtractProperty(sourceObject, propertyName);
                if (value != null)
                {
                    result[propertyName] = value;
                }

                processedProperties.Add(propertyName);

                // Extract related properties
                var relatedProperties = GetRelatedProperties(sourceObject, propertyName);
                foreach (var relatedProperty in relatedProperties)
                {
                    if (!processedProperties.Contains(relatedProperty))
                    {
                        var relatedValue = ExtractProperty(sourceObject, relatedProperty);
                        if (relatedValue != null)
                        {
                            result[relatedProperty] = relatedValue;
                        }

                        processedProperties.Add(relatedProperty);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Applies multiple properties to an object.
        /// </summary>
        /// <param name="targetObject">The object to apply properties to.</param>
        /// <param name="properties">Dictionary of property names and values.</param>
        /// <returns>Dictionary indicating success/failure for each property.</returns>
        public Dictionary<string, bool> ApplyProperties(object targetObject, Dictionary<string, object> properties)
        {
            var results = new Dictionary<string, bool>();

            foreach (var kvp in properties)
            {
                results[kvp.Key] = ApplyProperty(targetObject, kvp.Key, kvp.Value);
            }

            return results;
        }

        /// <summary>
        /// Registers the default set of property handlers.
        /// </summary>
        private void RegisterDefaultHandlers()
        {
            // Register specialized handlers first (higher priority)
            RegisterHandler(new PersistentDataPropertyHandler());
            RegisterHandler(new PanelPropertyHandler());
            RegisterHandler(new ValueListItemsPropertyHandler());
            RegisterHandler(new ValueListModePropertyHandler());
            RegisterHandler(new SliderCurrentValuePropertyHandler());
            RegisterHandler(new SliderRoundingPropertyHandler());
            RegisterHandler(new ExpressionPropertyHandler());
            RegisterHandler(new ColorPropertyHandler());
            RegisterHandler(new FontPropertyHandler());
            RegisterHandler(new DataMappingPropertyHandler());

            // Register default handler
            RegisterHandler(new DefaultPropertyHandler());
        }

        /// <summary>
        /// Clears all registered handlers and re-registers defaults.
        /// Useful for testing or reconfiguration scenarios.
        /// </summary>
        public void Reset()
        {
            _handlers.Clear();
            RegisterDefaultHandlers();
        }

        /// <summary>
        /// Gets information about all registered handlers for debugging purposes.
        /// </summary>
        /// <returns>List of handler information.</returns>
        public List<HandlerInfo> GetHandlerInfo()
        {
            return _handlers.Select(h => new HandlerInfo
            {
                Name = h.GetType().Name,
                Priority = h.Priority,
                Type = h.GetType().FullName
            }).ToList();
        }
    }

    /// <summary>
    /// Information about a registered handler for debugging purposes.
    /// </summary>
    public class HandlerInfo
    {
        public string Name { get; set; }
        public int Priority { get; set; }
        public string Type { get; set; }
    }
}
