﻿//-----------------------------------------------------------------------------
// FILE:	    AsyncExternalWorkflowFuture.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;
using Neon.Tasks;

namespace Neon.Cadence.Internal
{
    /// <summary>
    /// Implements an external workflow future that returns <c>void</c>.
    /// </summary>
    internal class AsyncExternalWorkflowFuture : IAsyncFuture
    {
        private bool                completed = false;
        private CadenceClient       client;
        private WorkflowExecution   execution;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="client">The associated client.</param>
        /// <param name="execution">The workflow execution.</param>
        public AsyncExternalWorkflowFuture(CadenceClient client, WorkflowExecution execution)
        {
            this.client    = client;
            this.execution = execution;
        }

        /// <inheritdoc/>
        public async Task GetAsync()
        {
            if (completed)
            {
                throw new InvalidOperationException($"[{nameof(IAsyncFuture<object>)}.GetAsync()] may only be called once per stub instance.");
            }

            completed = true;

            await client.GetWorkflowResultAsync(execution);
        }
    }

    /// <summary>
    /// Implements an external workflow future that returns a value.
    /// </summary>
    /// <typeparam name="TResult">The workflow result type.</typeparam>
    internal class AsyncExternalWorkflowFuture<TResult> : IAsyncFuture<TResult>
    {
        private bool                completed = false;
        private CadenceClient       client;
        private WorkflowExecution   execution;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="client">The associated client.</param>
        /// <param name="execution">The workflow execution.</param>
        /// <param name="resultType">Specifies the workflow result type or <c>null</c> for <c>void</c> workflow methods.</param>
        public AsyncExternalWorkflowFuture(CadenceClient client, WorkflowExecution execution, Type resultType)
        {
            this.client    = client;
            this.execution = execution;
        }

        /// <inheritdoc/>
        public async Task<TResult> GetAsync()
        {
            if (completed)
            {
                throw new InvalidOperationException($"[{nameof(IAsyncFuture<object>)}.GetAsync()] may only be called once per stub instance.");
            }

            completed = true;

            var resultBytes = await client.GetWorkflowResultAsync(execution);

            return client.DataConverter.FromData<TResult>(resultBytes);
        }
    }
}
