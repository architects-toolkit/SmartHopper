/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2024 Marc Roca Musach
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 * 
 * Portions of this code adapted from:
 * https://github.com/specklesystems/GrasshopperAsyncComponent
 * Apache License 2.0
 * Copyright (c) 2021 Speckle Systems
 */

/*
 * Base class for all asynchronous Grasshopper components.
 * This class provides the fundamental structure for components that need to perform
 * asynchronous operations while maintaining Grasshopper's component lifecycle.
 */

using Grasshopper.Kernel;
using System;
using System.Threading.Tasks;

namespace SmartHopper.Core.ComponentBase
{
    /// <summary>
    /// Base class for asynchronous Grasshopper components. Inherit from this class
    /// when you need to create a component that performs long-running operations.
    /// </summary>
    public abstract class AsyncComponentBase : GH_Component
    {
        /// <summary>
        /// Constructor for AsyncComponentBase.
        /// </summary>
        /// <param name="name">The display name of the component</param>
        /// <param name="nickname">The shortened display name</param>
        /// <param name="description">Description of the component's function</param>
        /// <param name="category">The tab category where the component appears</param>
        /// <param name="subCategory">The sub-category within the tab</param>
        protected AsyncComponentBase(string name, string nickname, string description, string category, string subCategory)
            : base(name, nickname, description, category, subCategory)
        {
            // TODO: Initialize any required services or managers here
        }
    }
}
