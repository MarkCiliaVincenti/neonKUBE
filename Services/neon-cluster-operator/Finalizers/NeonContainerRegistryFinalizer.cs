﻿//-----------------------------------------------------------------------------
// FILE:	    NeonContainerRegistryFinalizer.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Neon.Diagnostics;
using Neon.Kube.Operator;
using Neon.Kube.ResourceDefinitions;
using Neon.Tasks;

using k8s.Models;

namespace NeonClusterOperator.Finalizers
{
    /// <summary>
    /// Finalizes deletion of <see cref="V1NeonContainerRegistry"/> resources.
    /// </summary>
    public class NeonContainerRegistryFinalizer : IResourceFinalizer<V1NeonContainerRegistry>
    {
        private ILogger logger;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="logger"></param>
        public NeonContainerRegistryFinalizer(ILogger logger)
        { 
            this.logger = logger;
        }

        /// <inheritdoc/>
        public async Task FinalizeAsync(V1NeonContainerRegistry entity)
        {
            await SyncContext.Clear;

            logger.LogInformationEx(() => $"Finalizing {entity.Name()}");
        }
    }
}
