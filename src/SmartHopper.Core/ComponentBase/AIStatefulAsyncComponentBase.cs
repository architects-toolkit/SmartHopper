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

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using SmartHopper.Core.Utils;
using SmartHopper.Core.Async.Workers;
using SmartHopper.Core.Async.Core;
using SmartHopper.Core.Async.Core.StateManagement;
using SmartHopper.Config.Models;
using SmartHopper.Config.Providers;
using SmartHopper.Config.Configuration;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Diagnostics;
using Newtonsoft.Json.Linq;

namespace SmartHopper.Core.ComponentBase
{
    public abstract class AIAsyncStatefulComponentBase : AsyncComponentBase 
    {
        private readonly IStateManager _stateManager;
        private readonly IParallelProcessor _parallelProcessor;
        private readonly IMessagingService _messagingService;
        private readonly IAIService _aiService;
        private readonly ICancellationManager _cancellationManager;
        private readonly IErrorHandler _errorHandler;

        protected AIAsyncStatefulComponentBase(
            string name,
            string nickname,
            string description,
            string category,
            string subCategory,
            IStateManager stateManager,
            IParallelProcessor parallelProcessor,
            IMessagingService messagingService,
            IAIService aiService,
            ICancellationManager cancellationManager,
            IErrorHandler errorHandler)
            : base(name, nickname, description, category, subCategory)
        {
            _stateManager = stateManager;
            _parallelProcessor = parallelProcessor;
            _messagingService = messagingService;
            _aiService = aiService;
            _cancellationManager = cancellationManager;
            _errorHandler = errorHandler;
        }
    }
}