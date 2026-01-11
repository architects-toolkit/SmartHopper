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

namespace SmartHopper.Infrastructure.Settings
{
    /// <summary>
    /// Wrapper for lazy evaluation of default values.
    /// </summary>
    public class LazyDefaultValue
    {
        private readonly object lockObject = new object();
        private readonly Func<object>? valueFactory;
        private readonly object? directValue;
        private readonly bool isLazy;
        private object? cachedValue;
        private bool isEvaluated;

        /// <summary>
        /// Initializes a new instance of the <see cref="LazyDefaultValue"/> class that will be computed when first accessed.
        /// </summary>
        /// <param name="valueFactory">The factory function to create the value.</param>
        public LazyDefaultValue(Func<object> valueFactory)
        {
            this.valueFactory = valueFactory ?? throw new ArgumentNullException(nameof(valueFactory));
            this.isLazy = true;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LazyDefaultValue"/> class with a direct value (non-lazy).
        /// </summary>
        /// <param name="directValue">The direct value to store.</param>
        public LazyDefaultValue(object? directValue)
        {
            this.directValue = directValue;
            this.isLazy = false;
            this.isEvaluated = true;
        }

        /// <summary>
        /// Gets the resolved default value, evaluating the lazy function if necessary.
        /// </summary>
        public object? Value
        {
            get
            {
                if (!this.isLazy)
                {
                    return this.directValue;
                }

                if (this.isEvaluated)
                {
                    return this.cachedValue;
                }

                lock (this.lockObject)
                {
                    if (this.isEvaluated)
                    {
                        return this.cachedValue;
                    }

                    this.cachedValue = this.valueFactory!();
                    this.isEvaluated = true;
                    return this.cachedValue;
                }
            }
        }

        /// <summary>
        /// Converts a function to a LazyDefaultValue.
        /// </summary>
        /// <param name="valueFactory">The factory function.</param>
        /// <returns>A new LazyDefaultValue instance.</returns>
        public static LazyDefaultValue FromFunction(Func<object> valueFactory)
        {
            return new LazyDefaultValue(valueFactory);
        }

        /// <summary>
        /// Converts a string to a LazyDefaultValue.
        /// </summary>
        /// <param name="directValue">The string value.</param>
        /// <returns>A new LazyDefaultValue instance.</returns>
        public static LazyDefaultValue FromString(string directValue)
        {
            return new LazyDefaultValue(directValue);
        }

        /// <summary>
        /// Converts an integer to a LazyDefaultValue.
        /// </summary>
        /// <param name="directValue">The integer value.</param>
        /// <returns>A new LazyDefaultValue instance.</returns>
        public static LazyDefaultValue FromInt32(int directValue)
        {
            return new LazyDefaultValue(directValue);
        }

        /// <summary>
        /// Converts a double to a LazyDefaultValue.
        /// </summary>
        /// <param name="directValue">The double value.</param>
        /// <returns>A new LazyDefaultValue instance.</returns>
        public static LazyDefaultValue FromDouble(double directValue)
        {
            return new LazyDefaultValue(directValue);
        }

        /// <summary>
        /// Implicit conversion from function to LazyDefaultValue.
        /// </summary>
        /// <param name="valueFactory">The factory function.</param>
        public static implicit operator LazyDefaultValue(Func<object> valueFactory)
        {
            return FromFunction(valueFactory);
        }

        /// <summary>
        /// Implicit conversion from string to LazyDefaultValue.
        /// </summary>
        /// <param name="directValue">The string value.</param>
        public static implicit operator LazyDefaultValue(string directValue)
        {
            return FromString(directValue);
        }

        /// <summary>
        /// Implicit conversion from integer to LazyDefaultValue.
        /// </summary>
        /// <param name="directValue">The integer value.</param>
        public static implicit operator LazyDefaultValue(int directValue)
        {
            return FromInt32(directValue);
        }

        /// <summary>
        /// Implicit conversion from double to LazyDefaultValue.
        /// </summary>
        /// <param name="directValue">The double value.</param>
        public static implicit operator LazyDefaultValue(double directValue)
        {
            return FromDouble(directValue);
        }

        /// <summary>
        /// Returns a string representation of the value.
        /// </summary>
        /// <returns>The string representation of the value.</returns>
        public override string ToString()
        {
            return this.Value?.ToString() ?? string.Empty;
        }
    }

    /// <summary>
    /// Represents a setting descriptor with metadata for UI generation and validation.
    /// </summary>
    public class SettingDescriptor
    {
        private LazyDefaultValue? defaultValue;

        /// <summary>
        /// Gets or sets the name of the setting (used as key in configuration).
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the data type of the setting.
        /// </summary>
        public Type Type { get; set; } = typeof(string);

        /// <summary>
        /// Gets or sets the default value for the setting (supports lazy evaluation).
        /// </summary>
        public object? DefaultValue
        {
            get => this.defaultValue?.Value;
            set
            {
                if (value is LazyDefaultValue lazy)
                {
                    this.defaultValue = lazy;
                }
                else if (value is Func<object> factory)
                {
                    this.defaultValue = new LazyDefaultValue(factory);
                }
                else
                {
                    this.defaultValue = new LazyDefaultValue(value);
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the setting contains sensitive data that should be masked.
        /// </summary>
        public bool IsSecret { get; set; }

        /// <summary>
        /// Gets or sets the display name shown in the UI.
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the description shown in the UI.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the list of allowed values that will be displayed as a dropdown for string settings.
        /// </summary>
        public IEnumerable<object>? AllowedValues { get; set; }

        /// <summary>
        /// Gets or sets the optional UI control parameters (e.g., slider vs stepper for numeric types).
        /// </summary>
        public SettingDescriptorControl? ControlParams { get; set; }

        /// <summary>
        /// Gets or sets whether the setting's UI control should be enabled. Default is true.
        /// When set to false, the control will render as disabled (read-only).
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Sets a lazy default value factory function.
        /// </summary>
        /// <param name="valueFactory">The factory function to create the default value.</param>
        public void SetLazyDefault(Func<object> valueFactory)
        {
            this.defaultValue = new LazyDefaultValue(valueFactory);
        }
    }

    /// <summary>
    /// Extension methods for SettingDescriptor.
    /// </summary>
    public static class SettingDescriptorExtensions
    {
        /// <summary>
        /// Applies a configuration action to a SettingDescriptor and returns it (fluent interface).
        /// </summary>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <param name="obj">The object to apply the action to.</param>
        /// <param name="action">The action to apply.</param>
        /// <returns>The original object after applying the action.</returns>
        public static T Apply<T>(this T obj, System.Action<T> action)
            where T : class
        {
            action?.Invoke(obj);
            return obj;
        }
    }
}
