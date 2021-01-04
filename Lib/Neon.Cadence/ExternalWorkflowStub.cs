﻿//-----------------------------------------------------------------------------
// FILE:	    ExternalWorkflowStub.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
using System.Threading.Tasks;

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;
using Neon.Tasks;

namespace Neon.Cadence
{
    /// <summary>
    /// Supports signalling and cancelling any workflow.  This is useful when an
    /// external workflow interface type is not known at compile time or to manage 
    /// workflows written in another language.
    /// </summary>
    public class ExternalWorkflowStub
    {
        //---------------------------------------------------------------------
        // Local types

        [ActivityInterface]
        private interface ILocalOperations : IActivity
        {
            /// <summary>
            /// Cancels the specified workflow.
            /// </summary>
            /// <param name="execution">The target workflow execution.</param>
            /// <returns>The tracking <see cref="Task"/>.</returns>
            Task CancelAsync(WorkflowExecution execution);

            /// <summary>
            /// Waits for the specified workflow to complete.
            /// </summary>
            /// <param name="execution">The target workflow execution.</param>
            /// <returns>The tracking <see cref="Task"/>.</returns>
            Task GetResultAsync(WorkflowExecution execution);

            /// <summary>
            /// Waits for the specified workflow to complete and then returns the
            /// workflow result.
            /// </summary>
            /// <param name="execution">The target workflow execution.</param>
            /// <returns>The workflow result.</returns>
            Task<byte[]> GetResultBytesAsync(WorkflowExecution execution);

            /// <summary>
            /// Signals the specified workflow.
            /// </summary>
            /// <param name="execution">The target workflow execution.</param>
            /// <param name="signalName">The signal name.</param>
            /// <param name="args">The signal arguments.</param>
            /// <returns>The tracking <see cref="Task"/>.</returns>
            Task SignalAsync(WorkflowExecution execution, string signalName, params object[] args);
        }

        private class LocalOperations : ActivityBase, ILocalOperations
        {
            public async Task CancelAsync(WorkflowExecution execution)
            {
                await Activity.Client.CancelWorkflowAsync(execution);
            }

            public async Task GetResultAsync(WorkflowExecution execution)
            {
                await Activity.Client.GetWorkflowResultAsync(execution);
            }

            public async Task<byte[]> GetResultBytesAsync(WorkflowExecution execution)
            {
                return await Activity.Client.GetWorkflowResultAsync(execution);
            }

            public async Task SignalAsync(WorkflowExecution execution, string signalName, params object[] args)
            {
                var dataConverter = Activity.Client.DataConverter;

                await Activity.Client.SignalWorkflowAsync(execution, signalName, CadenceHelper.ArgsToBytes(dataConverter, args));
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        private Workflow        parentWorkflow;
        private CadenceClient   client;
        private string          domain;

        /// <summary>
        /// Internal constructor for use outside of a workflow.
        /// </summary>
        /// <param name="client">Specifies the associated client.</param>
        /// <param name="execution">Specifies the target workflow execution.</param>
        /// <param name="domain">Optionally specifies the target domain (defaults to the client's default domain).</param>
        internal ExternalWorkflowStub(CadenceClient client, WorkflowExecution execution, string domain = null)
        {
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));
            Covenant.Requires<ArgumentNullException>(execution != null, nameof(execution));

            this.client    = client;
            this.domain    = client.ResolveDomain(domain);
            this.Execution = execution;
        }

        /// <summary>
        /// Internal constructor for use within a workflow.
        /// </summary>
        /// <param name="parentWorkflow">Specifies the parent workflow.</param>
        /// <param name="execution">Specifies the target workflow execution.</param>
        /// <param name="domain">Optionally specifies the target domain (defaults to the client's default domain).</param>
        internal ExternalWorkflowStub(Workflow parentWorkflow, WorkflowExecution execution, string domain = null)
        {
            Covenant.Requires<ArgumentNullException>(parentWorkflow != null, nameof(parentWorkflow));
            Covenant.Requires<ArgumentNullException>(execution != null, nameof(execution));

            this.parentWorkflow = parentWorkflow;
            this.client         = parentWorkflow.Client;
            this.domain         = client.ResolveDomain(domain);
            this.Execution      = execution;
        }

        /// <summary>
        /// Returns the workflow execution.
        /// </summary>
        public WorkflowExecution Execution { get; private set; }

        /// <summary>
        /// Cancels the workflow.
        /// </summary>
        public async Task CancelAsync()
        {
            await SyncContext.ClearAsync;

            if (parentWorkflow != null)
            {
                var stub = parentWorkflow.NewLocalActivityStub<ILocalOperations, LocalOperations>();

                await stub.CancelAsync(Execution);
            }
            else
            {
                await client.CancelWorkflowAsync(Execution, domain);
            }
        }

        /// <summary>
        /// Signals the workflow.
        /// </summary>
        /// <param name="signalName">Specifies the signal name.</param>
        /// <param name="args">Specifies the signal arguments.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task SignalAsync(string signalName, params object[] args)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(signalName), nameof(signalName));
            Covenant.Requires<ArgumentNullException>(args != null, nameof(args));

            if (parentWorkflow != null)
            {
                var stub = parentWorkflow.NewLocalActivityStub<ILocalOperations, LocalOperations>();

                await stub.SignalAsync(Execution, signalName, args);
            }
            else
            {
                await client.SignalWorkflowAsync(Execution, signalName, CadenceHelper.ArgsToBytes(client.DataConverter, args));
            }
        }

        /// <summary>
        /// Waits for the workflow complete if necessary, without returning the result.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task GetResultAsync()
        {
            await SyncContext.ClearAsync;

            if (parentWorkflow != null)
            {
                var stub = parentWorkflow.NewLocalActivityStub<ILocalOperations, LocalOperations>();

                await stub.GetResultAsync(Execution);
            }
            else
            {
                await client.GetWorkflowResultAsync(Execution, domain);
            }
        }

        /// <summary>
        /// Returns the workflow result, waiting for the workflow to complete if necessary.
        /// </summary>
        /// <typeparam name="TResult">The workflow result type.</typeparam>
        /// <returns>The workflow result.</returns>
        public async Task<TResult> GetResultAsync<TResult>()
        {
            await SyncContext.ClearAsync;

            if (parentWorkflow != null)
            {
                var stub  = parentWorkflow.NewLocalActivityStub<ILocalOperations, LocalOperations>();
                var bytes = await stub.GetResultBytesAsync(Execution);

                return client.DataConverter.FromData<TResult>(bytes);
            }
            else
            {
                return client.DataConverter.FromData<TResult>(await client.GetWorkflowResultAsync(Execution, domain));
            }
        }
    }
}
