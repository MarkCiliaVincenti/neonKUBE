﻿//-----------------------------------------------------------------------------
// FILE:	    Workflow.cs
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
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;
using Neon.Diagnostics;

namespace Neon.Cadence
{
    /// <summary>
    /// Provides useful information and functionality for workflow implementations.
    /// This will be available via the <see cref="IWorkflowBase.Workflow"/> property.
    /// </summary>
    public class Workflow
    {
        /// <summary>
        /// The default workflow version returned by <see cref="GetVersionAsync(string, int, int)"/> 
        /// when a version has not been set yet.
        /// </summary>
        public const int DefaultVersion = -1;

        private object                  syncLock = new object();
        private WorkflowBase            parentInstance;
        private long                    contextId;
        private int                     pendingOperationCount;
        private long                    nextLocalActivityTypeId;
        private bool                    isDisconnected;
        private Random                  random;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="parentInstance">The parent workflow instance.</param>
        /// <param name="client">The associated client.</param>
        /// <param name="contextId">The workflow's context ID.</param>
        /// <param name="workflowTypeName">The workflow type name.</param>
        /// <param name="domain">The hosting domain.</param>
        /// <param name="taskList">The hosting task list.</param>
        /// <param name="workflowId">The workflow ID.</param>
        /// <param name="runId">The current workflow run ID.</param>
        /// <param name="isReplaying">Indicates whether the workflow is currently replaying from histor.</param>
        /// <param name="methodMap">Maps the workflow signal and query methods.</param>
        internal Workflow(
            WorkflowBase        parentInstance,
            CadenceClient       client, 
            long                contextId, 
            string              workflowTypeName, 
            string              domain, 
            string              taskList,
            string              workflowId, 
            string              runId, 
            bool                isReplaying, 
            WorkflowMethodMap   methodMap)
        {
            Covenant.Requires<ArgumentNullException>(client != null);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowTypeName));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(domain));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(taskList));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowId));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(runId));

            this.parentInstance          = parentInstance;
            this.contextId               = contextId;
            this.pendingOperationCount   = 0;
            this.nextLocalActivityTypeId = 0;
            this.isDisconnected          = false;
            this.IdToLocalActivityType   = new Dictionary<long, Type>();
            this.MethodMap               = methodMap;
            this.Client                  = client;
            this.IsReplaying             = isReplaying;

            // Initialize the random number generator with a fairly unique
            // seed for the workflow without consuming entropy to obtain
            // a cryptographically random number.
            //
            // Note that we can use a new seed every time the workflow is
            // invoked because the actually random numbers returned below
            // will be recorded and replayed from history.

            var seed = Environment.TickCount;

            seed ^= (int)DateTime.Now.Ticks;
            seed ^= (int)contextId;

            this.random = new Random(seed);

            // Initialize the workflow information.

            this.WorkflowInfo = new WorkflowInfo()
            {
                WorkflowType = workflowTypeName,
                Domain       = domain,
                TaskList     = taskList,
                WorkflowId   = workflowId,
                RunId        = runId,

                // $todo(jeff.lill): We need to initialize these from somewhere.
                //
                // ExecutionStartToCloseTimeout
                // ChildPolicy 
            };
        }

        /// <summary>
        /// Returns the <see cref="CadenceClient"/> managing this workflow.
        /// </summary>
        public CadenceClient Client { get; set; }

        /// <summary>
        /// Returns information about the running workflow.
        /// </summary>
        public WorkflowInfo WorkflowInfo { get; set; }

        /// <summary>
        /// Returns the workflow types method map.
        /// </summary>
        internal WorkflowMethodMap MethodMap { get; private set; }

        /// <summary>
        /// Returns the dictionary mapping the IDs to local activity types.
        /// </summary>
        internal Dictionary<long, Type> IdToLocalActivityType { get; private set; }

        /// <summary>
        /// <para>
        /// Indicates whether the workflow code is being replayed.
        /// </para>
        /// <note>
        /// <b>WARNING:</b> Never have workflow logic depend on this flag as doing so will
        /// break determinism.  The only reasonable uses for this flag are for managing
        /// external things like logging or metric reporting.
        /// </note>
        /// </summary>
        public bool IsReplaying { get; internal set; }

        /// <summary>
        /// Returns the execution information for the current workflow.
        /// </summary>
        public WorkflowExecution Execution { get; internal set; }

        /// <summary>
        /// Executes a workflow Cadence related operation, attempting to detect
        /// when an attempt is made to perform more than one operation in 
        /// parallel, which will likely break workflow determinism.
        /// </summary>
        /// <typeparam name="TResult">The operation result type.</typeparam>
        /// <param name="actionAsync">The workflow action function.</param>
        /// <returns>The action result.</returns>
        private async Task<TResult> ExecuteNoParallel<TResult>(Func<Task<TResult>> actionAsync)
        {
            try
            {
                if (Interlocked.Increment(ref pendingOperationCount) > 0)
                {
                    throw new WorkflowParallelOperationException();
                }

                return await actionAsync();
            }
            finally
            {
                Interlocked.Decrement(ref pendingOperationCount);
            }
        }

        /// <summary>
        /// Updates the workflow's <see cref="IsReplaying"/> state to match the
        /// state specified in the reply from cadence-proxy.
        /// </summary>
        /// <typeparam name="TReply">The reply message type.</typeparam>
        /// <param name="reply">The reply message.</param>
        private void UpdateReplay<TReply>(TReply reply)
            where TReply : WorkflowReply
        {
            switch (reply.ReplayStatus)
            {
                case InternalReplayStatus.NotReplaying:

                    IsReplaying = false;
                    break;

                case InternalReplayStatus.Replaying:

                    IsReplaying = true;
                    break;
            }
        }

        /// <summary>
        /// <para>
        /// Returns the current workflow time (UTC).
        /// </para>
        /// <note>
        /// This must used instead of calling <see cref="DateTime.UtcNow"/> or any other
        /// time method to guarantee determinism when a workflow is replayed.
        /// </note>
        /// </summary>
        public async Task<DateTime> UtcNowAsync()
        {
            var reply = await ExecuteNoParallel(
                async () =>
                {
                    return (WorkflowGetTimeReply)await Client.CallProxyAsync(
                        new WorkflowGetTimeRequest()
                        {
                            ContextId = contextId
                        });
                });

            reply.ThrowOnError();
            UpdateReplay(reply);

            return reply.Time;
        }

        /// <summary>
        /// Continues the current workflow as a new run using the same workflow options.
        /// </summary>
        /// <param name="args">The new run arguments.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task ContinueAsNewAsync(params object[] args)
        {
            // This method doesn't currently do any async operations but I'd
            // like to keep the method signature async just in case this changes
            // in the future.

            await Task.CompletedTask;

            // We're going to throw a [CadenceWorkflowRestartException] with the
            // parameters.  This exception will be caught and handled by the 
            // [WorkflowInvoke()] method which will configure the reply such
            // that the cadence-proxy will be able to signal Cadence to continue
            // the workflow with a clean history.

            throw new CadenceWorkflowRestartException(
                args:       Client.DataConverter.ToData(args),
                domain:     WorkflowInfo.Domain,
                taskList:   WorkflowInfo.TaskList);
        }

        /// <summary>
        /// Continues the current workflow as a new run allowing the specification of
        /// new workflow options.
        /// </summary>
        /// <param name="options">The continuation options.</param>
        /// <param name="args">The new run arguments.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task ContinueAsNewAsync(ContinueAsNewOptions options, params object[] args)
        {
            // This method doesn't currently do any async operations but I'd
            // like to keep the method signature async just in case this changes
            // in the future.

            await Task.CompletedTask;

            // We're going to throw a [CadenceWorkflowRestartException] with the
            // parameters.  This exception will be caught and handled by the 
            // [WorkflowInvoke()] method which will configure the reply such
            // that the cadence-proxy will be able to signal Cadence to continue
            // the workflow with a clean history.

            throw new CadenceWorkflowRestartException(
                args:                       Client.DataConverter.ToData(args),
                domain:                     WorkflowInfo.Domain,
                taskList:                   WorkflowInfo.TaskList,
                executionToStartTimeout:    options.ExecutionStartToCloseTimeout,
                scheduleToCloseTimeout:     options.ScheduleToCloseTimeout,
                scheduleToStartTimeout:     options.ScheduleToStartTimeout,
                taskStartToCloseTimeout:    options.TaskStartToCloseTimeout,
                retryPolicy:                options.RetryOptions);
        }

        /// <summary>
        /// Used to implement backwards compatible changes to a workflow implementation.
        /// </summary>
        /// <param name="changeId">Identifies the change.</param>
        /// <param name="minSupported">
        /// Specifies the minimum supported version.  You may pass <see cref="Workflow.DefaultVersion"/> <b>(-1)</b>
        /// which will be set as the version for workflows that haven't been versioned yet.
        /// </param>
        /// <param name="maxSupported">Specifies the maximum supported version.</param>
        /// <returns>The workflow implementation version.</returns>
        /// <remarks>
        /// <para>
        /// It is possible to upgrade workflow implementation with workflows in flight using
        /// the <see cref="GetVersionAsync(string, int, int)"/> method.  The essential requirement
        /// is that the new implementation must execute the same logic for the decision steps
        /// that have already been executed and recorded to the history fo a previous workflow 
        /// to maintain workflow determinism.  Subsequent unexecuted steps, are free to implement
        /// different logic.
        /// </para>
        /// <note>
        /// Cadence attempts to detect when replaying workflow performs actions that are different
        /// from those recorded as history and will fail the workflow when this occurs.
        /// </note>
        /// <para>
        /// Upgraded workflows will use <see cref="GetVersionAsync(string, int, int)"/> to indicate
        /// where upgraded logic has been inserted into the workflow.  You'll pass a <b>changeId</b>
        /// string that identifies the change being made.  This can be anything you wish as long as
        /// it's not empty and is unique for each change made to the workflow.  You'll also pass
        /// <b>minSupported</b> and <b>maxSupported</b> integers.  <b>minSupported</b> specifies the 
        /// minimum version of the workflow implementation that will be allowed to continue to
        /// run.  Workflows start out with their version set to <see cref="Workflow.DefaultVersion"/>
        /// or <b>(-1)</b> and this will often be passed as <b>minSupported</b> such that upgraded
        /// workflow implementations will be able to take over newly scheduled workflows.  
        /// <b>maxSupported</b> essentially specifies the current (latest) version of the workflow 
        /// implementation. 
        /// </para>
        /// <para>
        /// When <see cref="GetVersionAsync(string, int, int)"/> called and is not being replayed
        /// from the workflow history, the method will record the <b>changeId</b> and <b>maxSupported</b>
        /// values to the workflow history.  When this is being replayed, the method will simply
        /// return the <b>maxSupported</b> value from the history.  Let's go through an example demonstrating
        /// how this can be used.  Let's say we start out with a simple two step workflow that 
        /// first calls <b>ActivityA</b> and then calls <b>ActivityB</b>:
        /// </para>
        /// <code lang="C#">
        /// public class MyWorkflow : WorkflowBase
        /// {
        ///     public async Task DoSomething()
        ///     {
        ///         var activities = Workflow.NewActivityStub&lt;MyActivities&gt;();
        /// 
        ///         await activities.ActivityAAsync();  
        ///         await activities.ActivityBAsync();  
        ///     }
        /// }
        /// </code>
        /// <para>
        /// Now, let's assume that we need to replace the call to <b>ActivityA</b> with a call to
        /// <b>ActivityC</b>.  If there is no chance of any instances of <B>MyWorkflow</B> still
        /// being in flight, you could simply redepoy the recoded workflow:
        /// </para>
        /// <code lang="C#">
        /// public class MyWorkflow : WorkflowBase
        /// {
        ///     public async Task&lt;byte[]&gt; RunAsync(byte[] args)
        ///     {
        ///         var activities = Workflow.NewActivityStub&lt;MyActivities&gt;();
        /// 
        ///         await activities.ActivityCAsync();  
        ///         await activities.ActivityBAsync();
        ///     }
        /// }
        /// </code>
        /// <para>
        /// But, if instances of this workflow may be in flight you'll need to deploy a backwards
        /// compatible workflow implementation that handles workflows that have already executed 
        /// <b>ActivityA</b> but haven't yet executed <b>ActivityB</b>.  You can accomplish this
        /// via:
        /// </para>
        /// <code lang="C#">
        /// public class MyWorkflow : WorkflowBase
        /// {
        ///     public async Task&lt;byte[]&gt; RunAsync(byte[] args)
        ///     {
        ///         var activities = Workflow.NewActivityStub&lt;MyActivities&gt;();
        ///         var version    = await GetVersionAsync("Replace ActivityA", DefaultVersion, 1);    
        /// 
        ///         switch (version)
        ///         {
        ///             case DefaultVersion:
        ///             
        ///                 await activities.ActivityAAsync();  
        ///                 break;
        ///                 
        ///             case 1:
        ///             
        ///                 await activities.ActivityCAsync();  // &lt;-- change
        ///                 break;
        ///         }
        ///         
        ///         await activities.ActivityBAsync();  
        ///     }
        /// }
        /// </code>
        /// <para>
        /// This upgraded workflow calls <see cref="GetVersionAsync(string, int, int)"/> passing
        /// <b>minSupported=DefaultVersion</b> and <b>maxSupported=1</b>  For workflow instances
        /// that have already executed <b>ActivityA</b>, <see cref="GetVersionAsync(string, int, int)"/>
        /// will return <see cref="Workflow.DefaultVersion"/> and we'll call <b>ActivityA</b>, which will match
        /// what was recorded in the history.  For workflows that have not yet executed <b>ActivityA</b>,
        /// <see cref="GetVersionAsync(string, int, int)"/> will return <b>1</b>, which we'll use as
        /// the indication that we can call <b>ActivityC</b>.
        /// </para>
        /// <para>
        /// Now, lets say we need to upgrade the workflow again and change the call for <b>ActivityB</b>
        /// to <b>ActivityD</b>, but only for workflows that have also executed <b>ActivityC</b>.  This 
        /// would look something like:
        /// </para>
        /// <code lang="C#">
        /// public class MyWorkflow : WorkflowBase
        /// {
        ///     public async Task&lt;byte[]&gt; RunAsync(byte[] args)
        ///     {
        ///         var activities = Workflow.NewActivityStub&lt;MyActivities&gt;();
        ///         var version    = await GetVersionAsync("Replace ActivityA", DefaultVersion, 1);    
        /// 
        ///         switch (version)
        ///         {
        ///             case DefaultVersion:
        ///             
        ///                 await activities.ActivityAAsync();  
        ///                 break;
        ///                 
        ///             case 1:
        ///             
        ///                 await activities.ActivityCAsync();  // &lt;-- change
        ///                 break;
        ///         }
        ///         
        ///         version = await GetVersionAsync("Replace ActivityB", 1, 2);    
        /// 
        ///         switch (version)
        ///         {
        ///             case DefaultVersion:
        ///             case 1:
        ///             
        ///                 await activities.ActivityBAsync();
        ///                 break;
        ///                 
        ///             case 2:
        ///             
        ///                 await activities.ActivityDAsync();  // &lt;-- change
        ///                 break;
        ///         }
        ///     }
        /// }
        /// </code>
        /// <para>
        /// Notice that the second <see cref="GetVersionAsync(string, int, int)"/> call passed a different
        /// change ID and also that the version range is now <b>1..2</b>.  The version returned will be
        /// <see cref="Workflow.DefaultVersion"/> or <b>1</b> if <b>ActivityA</b> and <b>ActivityB</b> were 
        /// recorded in the history or <b>2</b> if <b>ActivityC</b> was called.
        /// </para>
        /// </remarks>
        public async Task<int> GetVersionAsync(string changeId, int minSupported, int maxSupported)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(changeId));
            Covenant.Requires<ArgumentException>(minSupported <= maxSupported);

            var reply = await ExecuteNoParallel(
                async () =>
                {
                    return (WorkflowGetVersionReply)await Client.CallProxyAsync(
                        new WorkflowGetVersionRequest()
                        {
                            ContextId    = this.contextId,
                            ChangeId     = changeId,
                            MinSupported = minSupported,
                            MaxSupported = maxSupported
                        });
                });

            reply.ThrowOnError();
            UpdateReplay(reply);

            return reply.Version;
        }

        /// <summary>
        /// Returns the <see cref="WorkflowExecution"/> for a child workflow created via
        /// <see cref="NewChildWorkflowStub{TWorkflowInterface}(ChildWorkflowOptions)"/>
        /// or <see cref="NewExternalWorkflowStub{TWorkflowInterface}(string, string)"/>.
        /// </summary>
        /// <param name="stub">The child workflow stub.</param>
        /// <returns>The <see cref="WorkflowExecution"/>.</returns>
        public async Task<WorkflowExecution> GetWorkflowExecutionAsync(object stub)
        {
            // $todo(jeff.lill):
            //
            // Come back to this one after we've implemented the stubs.  This information
            // comes back to the .NET side in [WorkflowExecuteChildReply].

            Covenant.Requires<ArgumentNullException>(stub != null);

            await Task.CompletedTask;
            throw new NotImplementedException();
        }

        /// <summary>
        /// Calls the specified function and then searches the workflow history
        /// to see if a value was already recorded with the specified <paramref name="id"/>.
        /// If no value has been recorded for the <paramref name="id"/> or the
        /// value returned by the function will be recorded, replacing any existing
        /// value.  If the function value is the same as the history value, then
        /// nothing will be recorded.
        /// </summary>
        /// <typeparam name="T">Specifies the result type.</typeparam>
        /// <param name="id">Identifies the value in the workflow history.</param>
        /// <param name="function">The side effect function.</param>
        /// <returns>The latest value persisted to the workflow history.</returns>
        /// <remarks>
        /// <para>
        /// This is similar to what you could do with a local activity but is
        /// a bit easier since you don't need to declare the activity and create
        /// a stub to call it and it's also more efficient because it avoids
        /// recording the same value multiple times in the history.
        /// </para>
        /// <note>
        /// The function must return within the configured decision task timeout 
        /// and should avoid throwing exceptions.
        /// </note>
        /// <note>
        /// The function passed should avoid throwing exceptions.  When an exception
        /// is thrown, this method will catch it and simply return the default 
        /// value for <typeparamref name="T"/>.
        /// </note>
        /// <note>
        /// <para>
        /// The .NET version of this method currently works a bit differently than
        /// the Java and GOLANG clients which will only call the function once.
        /// The .NET implementation calls the function every time 
        /// <see cref="MutableSideEffectAsync{T}(string, Func{T})"/>
        /// is called but it will ignore the all but the first call's result.
        /// </para>
        /// <para>
        /// This is an artifact of how the .NET client is currently implemented
        /// and may change in the future.  You should take care not to code your
        /// application to depend on this behavior (one way or the other).
        /// </para>
        /// </note>
        /// </remarks>
        public async Task<T> MutableSideEffectAsync<T>(string id, Func<T> function)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(id));

            T value;

            try
            {
                value = function();
            }
            catch
            {
                value = default(T);
            }

            var reply = await ExecuteNoParallel(
                async () =>
                {
                    return (WorkflowMutableReply)await Client.CallProxyAsync(
                        new WorkflowMutableRequest()
                        {
                            ContextId = this.contextId,
                            MutableId = id,
                            Result    = Client.DataConverter.ToData(value)
                        });
                });

            reply.ThrowOnError();
            UpdateReplay(reply);

            return Client.DataConverter.FromData<T>(reply.Result);
        }

        /// <summary>
        /// <para>
        /// Calls the specified function and then searches the workflow history
        /// to see if a value was already recorded with the specified <paramref name="id"/>.
        /// If no value has been recorded for the <paramref name="id"/> or the
        /// value returned by the function will be recorded, replacing any existing
        /// value.  If the function value is the same as the history value, then
        /// nothing will be recorded.
        /// </para>
        /// <para>
        /// This version of the method uses a parameter to specify the expected
        /// result type.
        /// </para>
        /// </summary>
        /// <param name="id">Identifies the value in the workflow history.</param>
        /// <param name="resultType">Specifies the result type.</param>
        /// <param name="function">The side effect function.</param>
        /// <returns>The latest value persisted to the workflow history.</returns>
        /// <remarks>
        /// <para>
        /// This is similar to what you could do with a local activity but is
        /// a bit easier since you don't need to declare the activity and create
        /// a stub to call it and it's also more efficient because it avoids
        /// recording the same value multiple times in the history.
        /// </para>
        /// <note>
        /// The function must return within the configured decision task timeout 
        /// and should avoid throwing exceptions.
        /// </note>
        /// <note>
        /// The function passed should avoid throwing exceptions.  When an exception
        /// is thrown, this method will catch it and simply return <c>null</c>.
        /// </note>
        /// <note>
        /// <para>
        /// The .NET version of this method currently works a bit differently than
        /// the Java and GOLANG clients which will only call the function once.
        /// The .NET implementation calls the function every time 
        /// <see cref="MutableSideEffectAsync(string, Type, Func{object})"/>
        /// is called but it will ignore the all but the first call's result.
        /// </para>
        /// <para>
        /// This is an artifact of how the .NET client is currently implemented
        /// and may change in the future.  You should take care not to code your
        /// application to depend on this behavior (one way or the other).
        /// </para>
        /// </note>
        /// </remarks>
        public async Task<object> MutableSideEffectAsync(string id, Type resultType, Func<dynamic> function)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(id));
            Covenant.Requires<ArgumentNullException>(resultType != null);
            Covenant.Requires<ArgumentNullException>(function != null);

            object value;

            try
            {
                value = function();
            }
            catch
            {
                value = default(object);
            }

            var reply = await ExecuteNoParallel(
                async () =>
                {
                    return (WorkflowMutableReply)await Client.CallProxyAsync(
                        new WorkflowMutableRequest()
                        {
                            ContextId = this.contextId,
                            MutableId = id,
                            Result    = Client.DataConverter.ToData(value)
                        });
                });

            reply.ThrowOnError();
            UpdateReplay(reply);

            return Client.DataConverter.FromData(resultType, reply.Result);
        }

        /// <summary>
        /// <para>
        /// Returns a replay safe <see cref="Guid"/>.
        /// </para>
        /// <note>
        /// This must be used instead of calling <see cref="Guid.NewGuid"/>
        /// to guarantee determinism when a workflow is replayed.
        /// </note>
        /// </summary>
        /// <returns>The new <see cref="Guid"/>.</returns>
        public async Task<Guid> NewGuidAsync()
        {
            return await SideEffectAsync(() => Guid.NewGuid());
        }

        /// <summary>
        /// <para>
        /// Returns a replay safe random non-negative integer greater than or equal to a minimum value
        /// less than a maximum value that is greater than or equal to 0.0 and less than 1.0.
        /// </para>
        /// <note>
        /// This must be used instead of something like <see cref="Random"/> to guarantee 
        /// determinism when a workflow is replayed.
        /// </note>
        /// </summary>
        /// <returns>The next random double between: <c>0  &lt;= value &lt; 1.0</c></returns>
        /// <remarks>
        /// <note>
        /// The internal random number generator is seeded such that workflow instances
        /// will generally see different sequences of random numbers.
        /// </note>
        /// </remarks>
        public async Task<double> NextRandomDouble()
        {
            return await SideEffectAsync(() => random.NextDouble());
        }

        /// <summary>
        /// <para>
        /// Returns a replay safe random non-negative random integer.
        /// </para>
        /// <note>
        /// This must be used instead of something like <see cref="Random"/> to guarantee 
        /// determinism when a workflow is replayed.
        /// </note>
        /// </summary>
        /// <returns>The next random integer greater than or equal to 0</returns>
        /// <remarks>
        /// <note>
        /// The internal random number generator is seeded such that workflow instances
        /// will generally see different sequences of random numbers.
        /// </note>
        /// </remarks>
        public async Task<int> NextRandomAsync()
        {
            return await SideEffectAsync(() => random.Next());
        }

        /// <summary>
        /// <para>
        /// Returns a replay safe random non-negative integer less than a maximum value.
        /// </para>
        /// <note>
        /// This must be used instead of something like <see cref="Random"/> to guarantee 
        /// determinism when a workflow is replayed.
        /// </note>
        /// </summary>
        /// <param name="maxValue">The exclusive upper limit of the value returned.  This cannot be negative.</param>
        /// <returns>The next random integer between: <c>0  &lt;= value &lt; maxValue</c></returns>
        /// <remarks>
        /// <note>
        /// The internal random number generator is seeded such that workflow instances
        /// will generally see different sequences of random numbers.
        /// </note>
        /// </remarks>
        public async Task<int> NextRandomAsync(int maxValue)
        {
            Covenant.Requires<ArgumentNullException>(maxValue > 0);

            return await SideEffectAsync(() => random.Next(maxValue));
        }

        /// <summary>
        /// <para>
        /// Returns a replay safe random non-negative integer greater than or equal to a minimum value
        /// less than a maximum value.
        /// </para>
        /// <note>
        /// This must be used instead of something like <see cref="Random"/> to guarantee 
        /// determinism when a workflow is replayed.
        /// </note>
        /// </summary>
        /// <param name="minValue">The inclusive lower limit of the value returned (may be negative).</param>
        /// <param name="maxValue">The exclusive upper limit of the value returned (may be negative).</param>
        /// <returns>The next random integer between: <c>0  &lt;= value &lt; maxValue</c>.</returns>
        /// <remarks>
        /// <note>
        /// The internal random number generator is seeded such that workflow instances
        /// will generally see different sequences of random numbers.
        /// </note>
        /// </remarks>
        public async Task<int> NextRandomAsync(int minValue, int maxValue)
        {
            Covenant.Requires<ArgumentNullException>(minValue < maxValue);

            return await SideEffectAsync(() => random.Next(minValue, maxValue));
        }

        /// <summary>
        /// <para>
        /// Returns a replay safe byte array filled with random values.
        /// </para>
        /// <note>
        /// This must be used instead of something like <see cref="Random"/> to guarantee 
        /// determinism when a workflow is replayed.
        /// </note>
        /// </summary>
        /// <param name="size">The size of the byte array returned (must be positive)..</param>
        /// <returns>The random bytes.</returns>
        /// <remarks>
        /// <note>
        /// The internal random number generator is seeded such that workflow instances
        /// will generally see different sequences of random numbers.
        /// </note>
        /// </remarks>
        public async Task<byte[]> NextRandomBytesAsync(int size)
        {
            Covenant.Requires<ArgumentNullException>(size > 0);

            return await SideEffectAsync(
                () =>
                {
                    var bytes = new byte[size];

                    random.NextBytes(bytes);

                    return bytes;
                });
        }

        /// <summary>
        /// Calls the specified function and records the value returned in the workflow
        /// history such that subsequent calls will return the same value.
        /// </summary>
        /// <typeparam name="T">Specifies the result type.</typeparam>
        /// <param name="function">The side effect function.</param>
        /// <returns>The value returned by the first function call.</returns>
        /// <remarks>
        /// <para>
        /// This is similar to what you could do with a local activity but is
        /// a bit easier since you don't need to declare the activity and create
        /// a stub to call it.
        /// </para>
        /// <note>
        /// The function must return within the configured decision task timeout 
        /// and should avoid throwing exceptions.
        /// </note>
        /// <note>
        /// The function passed should avoid throwing exceptions.  When an exception
        /// is thrown, this method will catch it and simply return the default 
        /// value for <typeparamref name="T"/>.
        /// </note>
        /// <note>
        /// <para>
        /// The .NET version of this method currently works a bit differently than
        /// the Java and GOLANG clients which will only call the function once.
        /// The .NET implementation calls the function every time <see cref="SideEffectAsync{T}(Func{T})"/>
        /// is called but it will ignore the all but the first call's result.
        /// </para>
        /// <para>
        /// This is an artifact of how the .NET client is currently implemented
        /// and may change in the future.  You should take care not to code your
        /// application to depend on this behavior (one way or the other).
        /// </para>
        /// </note>
        /// </remarks>
        public async Task<T> SideEffectAsync<T>(Func<T> function)
        {
            Covenant.Requires<ArgumentNullException>(function != null);

            T value;

            try
            {
                value = function();
            }
            catch
            {
                value = default(T);
            }

            var reply = await ExecuteNoParallel(
                async () =>
                {
                    return (WorkflowMutableReply)await Client.CallProxyAsync(
                        new WorkflowMutableRequest()
                        {
                            ContextId = this.contextId,
                            MutableId = null,
                            Result    = Client.DataConverter.ToData(value)
                        });
                });

            reply.ThrowOnError();
            UpdateReplay(reply);

            return Client.DataConverter.FromData<T>(reply.Result);
        }

        /// <summary>
        /// Calls the specified function and records the value returned in the workflow
        /// history such that subsequent calls will return the same value.  This version
        /// specifies the expected result type as a parameter.
        /// </summary>
        /// <param name="resultType">Specifies the result type.</param>
        /// <param name="function">The side effect function.</param>
        /// <returns>The value returned by the first function call.</returns>
        /// <remarks>
        /// <para>
        /// This is similar to what you could do with a local activity but is
        /// a bit easier since you don't need to declare the activity and create
        /// a stub to call it.
        /// </para>
        /// <note>
        /// The function must return within the configured decision task timeout 
        /// and should avoid throwing exceptions.
        /// </note>
        /// <note>
        /// The function passed should avoid throwing exceptions.  When an exception
        /// is thrown, this method will catch it and simply return <c>null</c>.
        /// </note>
        /// <note>
        /// <para>
        /// The .NET version of this method currently works a bit differently than
        /// the Java and GOLANG clients which will only call the function once.
        /// The .NET implementation calls the function every time <see cref="SideEffectAsync(Type, Func{object})"/>
        /// is called but it will ignore the all but the first call's result.
        /// </para>
        /// <para>
        /// This is an artifact of how the .NET client is currently implemented
        /// and may change in the future.  You should take care not to code your
        /// application to depend on this behavior (one way or the other).
        /// </para>
        /// </note>
        /// </remarks>
        public async Task<object> SideEffectAsync(Type resultType, Func<object> function)
        {
            Covenant.Requires<ArgumentNullException>(resultType != null);
            Covenant.Requires<ArgumentNullException>(function != null);

            object value;

            try
            {
                value = function();
            }
            catch
            {
                value = default(object);
            }

            var reply = await ExecuteNoParallel(
                async () =>
                {
                    return (WorkflowMutableReply)await Client.CallProxyAsync(
                        new WorkflowMutableRequest()
                        {
                            ContextId = this.contextId,
                            MutableId = null,
                            Result    = Client.DataConverter.ToData(value)
                        });
                });

            reply.ThrowOnError();
            UpdateReplay(reply);

            return Client.DataConverter.FromData(resultType, reply.Result);
        }

        /// <summary>
        /// Pauses the workflow for at least the specified interval.
        /// </summary>
        /// <param name="duration">The duration to pause.</param>
        /// <returns>The tracking <see cref="Task"/></returns>
        /// <remarks>
        /// <note>
        /// This must be used instead of calling <see cref="Task.Delay(TimeSpan)"/> or <see cref="Thread.Sleep(TimeSpan)"/>
        /// to guarantee determinism when a workflow is replayed.
        /// </note>
        /// <note>
        /// Cadence time interval resolution is limited to whole seconds and
        /// the duration will be rounded up to the nearest second and the 
        /// workflow may resumed sometime after the requested interval 
        /// depending on how busy the registered workers are and how long
        /// it takes to actually wake the workflow.
        /// </note>
        /// </remarks>
        public async Task SleepAsync(TimeSpan duration)
        {
            var reply = await ExecuteNoParallel(
                async () =>
                {
                    return (WorkflowSleepReply)await Client.CallProxyAsync(
                        new WorkflowSleepRequest()
                        {
                            ContextId = contextId,
                            Duration  = duration
                        });
                });

            reply.ThrowOnError();
            UpdateReplay(reply);
        }

        /// <summary>
        /// Pauses the workflow until at least the specified time (UTC).
        /// </summary>
        /// <param name="time">The wake time.</param>
        /// <returns>The tracking <see cref="Task"/></returns>
        public async Task SleepUntilUtcAsync(DateTime time)
        {
            var utcNow = await UtcNowAsync();

            if (time > utcNow)
            {
                await SleepAsync(time - utcNow);
            }
        }

        //---------------------------------------------------------------------
        // Stub creation methods

        /// <summary>
        /// Creates a client stub that can be used to launch one or more activity instances
        /// via the type-safe interface methods.
        /// </summary>
        /// <typeparam name="TActivityInterface">The activity interface.</typeparam>
        /// <param name="options">Optionally specifies the activity options.</param>
        /// <returns>The new <see cref="IActivityStub"/>.</returns>
        /// <remarks>
        /// <note>
        /// Unlike workflow stubs, a single activity stub instance can be used to
        /// launch multiple activities.
        /// </note>
        /// <para>
        /// Activities launched by the returned stub will be scheduled normally
        /// by Cadence to executed on one of the worker nodes.  Use <see cref="NewLocalActivityStub{TActivityInterface}(ActivityOptions)"/>
        /// to execute short-lived activities locally within the current process.
        /// </para>
        /// </remarks>
        public TActivityInterface NewActivityStub<TActivityInterface>(ActivityOptions options = null) 
            where TActivityInterface : IActivityBase
        {
            CadenceHelper.ValidateActivityInterface(typeof(TActivityInterface));

            throw new NotImplementedException();
        }

        /// <summary>
        /// Creates a workflow client stub that can be used to launch, signal, and query child
        /// workflows via the type-safe workflow interface methods.
        /// </summary>
        /// <typeparam name="TWorkflowInterface">The workflow interface.</typeparam>
        /// <param name="options"></param>
        /// <returns>The child workflow stub.</returns>
        /// <remarks>
        /// Unlike activity stubs, a workflow stub may only be used to launch a single
        /// workflow.  You'll need to create a new stub for each workflow you wish to
        /// invoke and then the first method called on a workflow stub must be
        /// the one of the methods tagged by <see cref="WorkflowMethodAttribute"/>.
        /// </remarks>
        public TWorkflowInterface NewChildWorkflowStub<TWorkflowInterface>(ChildWorkflowOptions options = null) 
            where TWorkflowInterface : IWorkflowBase
        {
            CadenceHelper.ValidateWorkflowInterface(typeof(TWorkflowInterface));

            throw new NotImplementedException();
        }

        /// <summary>
        /// Creates a typed-safe client stub that can be used to continue the workflow as a new run.
        /// </summary>
        /// <typeparam name="TWorkflowInterface">The workflow interface.</typeparam>
        /// <param name="options">Optionally specifies the new options to use when continuing the workflow.</param>
        /// <returns>The type-safe stub.</returns>
        /// <remarks>
        /// The workflow stub returned is intended just for continuing the workflow by
        /// calling one of the workflow entry point methods tagged by <see cref="WorkflowMethodAttribute"/>.
        /// Any signal or query methods defined by <typeparamref name="TWorkflowInterface"/> will 
        /// throw a <see cref="InvalidOperationException"/> when called.
        /// </remarks>
        public Task<TWorkflowInterface> NewContinueAsNewStub<TWorkflowInterface>(ContinueAsNewOptions options = null) 
            where TWorkflowInterface : IWorkflowBase
        {
            CadenceHelper.ValidateWorkflowInterface(typeof(TWorkflowInterface));

            throw new NotImplementedException();
        }

        /// <summary>
        /// Creates a workflow client stub that can be used communicate with an
        /// existing workflow identified by <see cref="WorkflowExecution"/>.
        /// </summary>
        /// <typeparam name="TWorkflowInterface">The workflow interface.</typeparam>
        /// <param name="execution">Identifies the workflow execution.</param>
        /// <param name="domain">Optionally specifies the domain.  This defaults to the domain of the parent workflow.</param>
        /// <returns>The workflow stub.</returns>
        public TWorkflowInterface NewExternalWorkflowStub<TWorkflowInterface>(WorkflowExecution execution, string domain = null)
            where TWorkflowInterface : IWorkflowBase
        {
            CadenceHelper.ValidateWorkflowInterface(typeof(TWorkflowInterface));

            throw new NotImplementedException();
        }

        /// <summary>
        /// Creates a workflow client stub that can be used communicate with an
        /// existing workflow identified by workflow ID.
        /// </summary>
        /// <typeparam name="TWorkflowInterface">The workflow interface.</typeparam>
        /// <param name="workflowId">Identifies the workflow.</param>
        /// <param name="domain">Optionally specifies the domain.  This defaults to the domain of the parent workflow.</param>
        /// <returns>The workflow stub.</returns>
        public TWorkflowInterface NewExternalWorkflowStub<TWorkflowInterface>(string workflowId, string domain = null)
            where TWorkflowInterface : IWorkflowBase
        {
            CadenceHelper.ValidateWorkflowInterface(typeof(TWorkflowInterface));

            throw new NotImplementedException();
        }

        /// <summary>
        /// Creates a client stub that can be used to launch one or more local activity 
        /// instances via the type-safe interface methods.
        /// </summary>
        /// <typeparam name="TActivityInterface">The activity interface.</typeparam>
        /// <param name="options">Optionally specifies activity options.</param>
        /// <returns>The new <see cref="IActivityStub"/>.</returns>
        /// <remarks>
        /// <note>
        /// Unlike workflow stubs, a single activity stub instance can be used to
        /// launch multiple activities.
        /// </note>
        /// <para>
        /// Activities launched by the returned stub will be executed in the current
        /// process.  This is intended to easily and efficiently execute activities
        /// that will complete very quickly (usually within a few seconds).  Local
        /// activities are similar to normal activities with these differences:
        /// </para>
        /// <list type="bullet">
        ///     <item>
        ///     Local activities are always scheduled to executed within the current process.
        ///     </item>
        ///     <item>
        ///     Local activity types do not need to be registered and local activities.
        ///     </item>
        ///     <item>
        ///     Local activities must complete within the <see cref="WorkflowOptions.DecisionTaskStartToCloseTimeout"/>.
        ///     This defaults to 10 seconds and can be set to a maximum of 60 seconds.
        ///     </item>
        ///     <item>
        ///     Local activities cannot heartbeat.
        ///     </item>
        /// </list>
        /// </remarks>
        public TActivityInterface NewLocalActivityStub<TActivityInterface>(ActivityOptions options = null) 
            where TActivityInterface : IActivityBase
        {
            CadenceHelper.ValidateActivityInterface(typeof(TActivityInterface));

            throw new NotImplementedException();
        }

        /// <summary>
        /// Creates a new untyped activity client stub that can be used to launch activities.
        /// </summary>
        /// <param name="options">Optionally specifies the activity options.</param>
        /// <returns>The new <see cref="IActivityStub"/>.</returns>
        /// <remarks>
        /// <note>
        /// Unlike workflow stubs, a single activity stub instance can be used to
        /// launch multiple activities.
        /// </note>
        /// <para>
        /// Activities launched by the returned stub will be scheduled normally
        /// by Cadence to executed on one of the worker nodes.  Use <see cref="NewLocalActivityStub{TActivityInterface}(ActivityOptions)"/>
        /// to execute short-lived activities locally within the current process.
        /// </para>
        /// </remarks>
        public IActivityStub NewUntypedActivityStub(ActivityOptions options = null)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Creates an untyped child workflow stub that can be used to start, signal, and query
        /// child workflows.
        /// </summary>
        /// <param name="workflowTypeName">The workflow type name (see the remarks).</param>
        /// <param name="options">Optionally specifies the child workflow options.</param>
        /// <returns></returns>
        /// <remarks>
        /// <para>
        /// Unlike activity stubs, a workflow stub may only be used to launch a single
        /// workflow.  You'll need to create a new stub for each workflow you wish to
        /// invoke and then the first method called on a workflow stub must be
        /// the one of the methods tagged by <see cref="WorkflowMethodAttribute"/>.
        /// </para>
        /// <para>
        /// <paramref name="workflowTypeName"/> specifies the target workflow implementation type name and optionally,
        /// the specific workflow method to be called for workflow interfaces that have multiple methods.  For
        /// workflow methods tagged by <c>[WorkflowMethod]</c> with specifying a name, the workflow type name will default
        /// to the fully qualified interface type name or the custom type name specified by <see cref="WorkflowAttribute.TypeName"/>.
        /// </para>
        /// <para>
        /// For workflow methods with <see cref="WorkflowMethodAttribute.Name"/> specified, the workflow type will
        /// look like:
        /// </para>
        /// <code>
        /// WORKFLOW-TYPE-NAME::METHOD-NAME
        /// </code>
        /// <para>
        /// You'll need to use this format when calling workflows using external untyped stubs or 
        /// from other languages.  The Java Cadence client works the same way.
        /// </para>
        /// </remarks>
        public IChildWorkflowStub NewUntypedChildWorkflowStub(string workflowTypeName, ChildWorkflowOptions options = null)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Creates an untyped stub that can be used to signal or cancel a child
        /// workflow identified by its <see cref="WorkflowExecution"/>.
        /// </summary>
        /// <param name="execution">The target <see cref="WorkflowExecution"/>.</param>
        /// <param name="domain">Optionally specifies the target domain.  This defaults to the parent workflow's domain.</param>
        /// <returns>The <see cref="IExternalWorkflowStub"/>.</returns>
        public IExternalWorkflowStub NewUntypedExternalWorkflowStub(WorkflowExecution execution, string domain = null)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Creates an untyped stub that can be used to signal or cancel a child
        /// workflow identified by its workflow ID.
        /// </summary>
        /// <param name="workflowId">The target workflow ID.</param>
        /// <param name="domain">Optionally specifies the target domain.  This defaults to the parent workflow's domain.</param>
        /// <returns>The <see cref="IExternalWorkflowStub"/>.</returns>
        public IExternalWorkflowStub NewUntypedExternalWorkflowStub(string workflowId, string domain = null)
        {
            throw new NotImplementedException();
        }

        //---------------------------------------------------------------------
        // Internal activity related methods used by dynamically generated activity stubs.

        /// <summary>
        /// Executes an activity with a specific activity type name and waits for it to complete.
        /// </summary>
        /// <param name="activityTypeName">Identifies the activity.</param>
        /// <param name="args">Optionally specifies the activity arguments.</param>
        /// <param name="options">Optionally specifies the activity options.</param>
        /// <returns>The activity result encoded as a byte array.</returns>
        /// <exception cref="CadenceException">
        /// An exception derived from <see cref="CadenceException"/> will be be thrown 
        /// if the child workflow did not complete successfully.
        /// </exception>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the named domain does not exist.</exception>
        /// <exception cref="CadenceBadRequestException">Thrown when the request is invalid.</exception>
        /// <exception cref="CadenceInternalServiceException">Thrown for internal Cadence cluster problems.</exception>
        /// <exception cref="CadenceServiceBusyException">Thrown when Cadence is too busy.</exception>
        protected async Task<byte[]> ExecuteActivityAsync(string activityTypeName, byte[] args = null, ActivityOptions options = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(activityTypeName));

            options = options ?? new ActivityOptions();

            var reply = (ActivityExecuteReply)await Client.CallProxyAsync(
                new ActivityExecuteRequest()
                {
                    ContextId = contextId,
                    Activity  = activityTypeName,
                    Args      = args,
                    Options   = options.ToInternal()
                });

            reply.ThrowOnError();
            UpdateReplay(reply);

            return reply.Result;
        }

        /// <summary>
        /// Executes a local activity and waits for it to complete.
        /// </summary>
        /// <param name="activityType">The activity type.</param>
        /// <param name="method">The target local activity method.</param>
        /// <param name="args">Optionally specifies the activity arguments.</param>
        /// <param name="options">Optionally specifies any local activity options.</param>
        /// <returns>The activity result encoded as a byte array.</returns>
        /// <exception cref="CadenceException">
        /// An exception derived from <see cref="CadenceException"/> will be be thrown 
        /// if the child workflow did not complete successfully.
        /// </exception>
        /// <remarks>
        /// This method can be used to optimize activities that will complete quickly
        /// (within seconds).  Rather than scheduling the activity on any worker that
        /// has registered an implementation for the activity, this method will simply
        /// instantiate an instance of <paramref name="activityType"/> and call its
        /// <paramref name="method"/> method.
        /// </remarks>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the named domain does not exist.</exception>
        /// <exception cref="CadenceBadRequestException">Thrown when the request is invalid.</exception>
        /// <exception cref="CadenceInternalServiceException">Thrown for internal Cadence cluster problems.</exception>
        /// <exception cref="CadenceServiceBusyException">Thrown when Cadence is too busy.</exception>
        protected async Task<byte[]> ExecuteLocalActivityAsync(Type activityType, MethodInfo method, byte[] args = null, LocalActivityOptions options = null)
        {
            Covenant.Requires<ArgumentNullException>(activityType != null);
            Covenant.Requires<ArgumentException>(activityType.Implements<IActivityBase>());
            Covenant.Requires<ArgumentNullException>(method != null);

            options = options ?? new LocalActivityOptions();

            // We need to register the local activity type with a workflow local ID
            // that we can sent to [cadence-proxy] in the [ActivityExecuteLocalRequest]
            // such that the proxy can send it back to us in the [ActivityInvokeLocalRequest]
            // so we'll know which activity type to instantate and run.

            var activityTypeId = Interlocked.Increment(ref nextLocalActivityTypeId);

            lock (syncLock)
            {
                IdToLocalActivityType.Add(activityTypeId, activityType);
            }

            try
            {
                var reply = (ActivityExecuteLocalReply)await Client.CallProxyAsync(
                    new ActivityExecuteLocalRequest()
                    {
                        ContextId      = contextId,
                        ActivityTypeId = activityTypeId,
                        Args           = args,
                        Options        = options.ToInternal()
                    });

                reply.ThrowOnError();
                UpdateReplay(reply);

                return reply.Result;
            }
            finally
            {
                // Remove the activity type mapping to prevent memory leaks.

                lock (syncLock)
                {
                    IdToLocalActivityType.Remove(activityTypeId);
                }
            }
        }
    }
}
