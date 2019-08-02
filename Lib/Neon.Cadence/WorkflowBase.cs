﻿//-----------------------------------------------------------------------------
// FILE:	    WorkflowBase.cs
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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;
using Neon.Retry;
using Neon.Time;
using Neon.Diagnostics;
using Neon.Tasks;

namespace Neon.Cadence
{
    /// <summary>
    /// Base class that can be used for Cadence workflow implementations.
    /// </summary>
    public class WorkflowBase
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Used to map a Cadence client ID and workflow context ID into a
        /// key that can be used to dereference <see cref="idToWorkflow"/>.
        /// </summary>
        private struct WorkflowInstanceKey
        {
            private long clientId;
            private long contextId;

            public WorkflowInstanceKey(CadenceClient client, long contextId)
            {
                this.clientId  = client.ClientId;
                this.contextId = contextId;
            }

            public override int GetHashCode()
            {
                return clientId.GetHashCode() ^ contextId.GetHashCode();
            }

            public override bool Equals(object obj)
            {
                if (obj == null || !(obj is WorkflowInstanceKey))
                {
                    return false;
                }

                var other = (WorkflowInstanceKey)obj;

                return this.clientId == other.clientId && 
                       this.contextId == other.contextId;
            }

            public override string ToString()
            {
                return $"clientID={clientId}, contextId={contextId}";
            }
        }

        /// <summary>
        /// Describes the workflow implementation type, entry point method, and 
        /// signal/query methods for registered workflow.
        /// </summary>
        private class WorkflowRegistration
        {
            /// <summary>
            /// The workflow implemention type.
            /// </summary>
            public Type WorkflowType { get; set; }

            /// <summary>
            /// The workflow entry point method.
            /// </summary>
            public MethodInfo WorkflowMethod { get; set; }

            /// <summary>
            /// The workflow entry point parameter types.
            /// </summary>
            public Type[] WorkflowMethodParameterTypes { get; set; }

            /// <summary>
            /// Maps workflow signal and query names to the corresponding
            /// method implementations.
            /// </summary>
            public WorkflowMethodMap MethodMap { get; set; }
        }

        //---------------------------------------------------------------------
        // Static members

        private static object                                           syncLock               = new object();
        private static INeonLogger                                      log                    = LogManager.Default.GetLogger<WorkflowBase>();
        private static Dictionary<WorkflowInstanceKey, WorkflowBase>    idToWorkflow           = new Dictionary<WorkflowInstanceKey, WorkflowBase>();
        private static Dictionary<string, WorkflowRegistration>         typeNameToRegistration = new Dictionary<string, WorkflowRegistration>();
        private static byte[]                                           emptyBytes             = new byte[0];

        // This dictionary is used to map workflow type names to the target workflow
        // registration.  Note that these mappings are scoped to specific cadence client
        // instances by prefixing the type name with:
        //
        //      CLIENT-ID::
        //
        // where CLIENT-ID is the locally unique ID of the client.  This is important,
        // because we'll need to remove entries the for clients when they're disposed.
        //
        // Workflow type names may also include a second "::" separator with the
        // workflow entry point name appended afterwards to handle workflow interfaces
        // that have multiple workflow entry points.  So, valid workflow registrations
        // may looks like:
        // 
        //      1::my-workflow                  -- clientId = 1, workflow type name = my-workflow
        //      1::my-workflow::my-entrypoint   -- clientId = 1, workflow type name = my-workflow, entrypoint = my-entrypoint

        private static Dictionary<string, WorkflowRegistration> nameToRegistration = new Dictionary<string, WorkflowRegistration>();

        /// <summary>
        /// Prepends the Cadence client ID to the workflow type name as well as the optional
        /// workflow method name to generate the key used to dereference the <see cref="typeNameToRegistration"/> 
        /// dictionary.
        /// </summary>
        /// <param name="client">The Cadence client.</param>
        /// <param name="workflowTypeName">The workflow type name.</param>
        /// <param name="workflowMethodAttribute">The workflow method attribute. </param>
        /// <returns>The workflow registration key.</returns>
        private static string GetWorkflowTypeKey(CadenceClient client, string workflowTypeName, WorkflowMethodAttribute workflowMethodAttribute)
        {
            Covenant.Requires<ArgumentNullException>(client != null);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowTypeName));
            Covenant.Requires<ArgumentNullException>(workflowMethodAttribute != null);

            if (string.IsNullOrEmpty(workflowMethodAttribute.Name))
            {
                return $"{client.ClientId}::{workflowTypeName}";
            }
            else
            {
                return $"{client.ClientId}::{workflowTypeName}::{workflowMethodAttribute.Name}";
            }
        }

        /// <summary>
        /// Returns the string used to register a workflow type and method with Cadence.
        /// </summary>
        /// <param name="workflowTypeName">The workflow type name.</param>
        /// <param name="workflowMethod">
        /// Optionally identifies the workflow (entry point) method.  
        /// <paramref name="workflowMethod"/> will be assumed to already
        /// include the method component when this is <c>null</c>.
        /// </param>
        /// <returns>The Cadence workflow registration string.</returns>
        private static string GetWorkflowRegistrationKey(string workflowTypeName, MethodInfo workflowMethod = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowTypeName));

            if (workflowMethod == null)
            {
                return workflowTypeName;
            }

            var workflowMethodAttribute = workflowMethod.GetCustomAttribute<WorkflowMethodAttribute>();

            if (string.IsNullOrEmpty(workflowMethodAttribute.Name))
            {
                return workflowTypeName;
            }
            else
            {
                return $"{workflowTypeName}::{workflowMethodAttribute.Name}";
            }
        }

        /// <summary>
        /// Registers a workflow implementation.
        /// </summary>
        /// <param name="client">The associated client.</param>
        /// <param name="workflowType">The workflow implementation type.</param>
        /// <param name="workflowTypeName">The name used to identify the implementation.</param>
        /// <param name="domain">Specifies the target domain.</param>
        /// <exception cref="InvalidOperationException">Thrown if a different workflow class has already been registered for <paramref name="workflowTypeName"/>.</exception>
        internal static async Task RegisterAsync(CadenceClient client, Type workflowType, string workflowTypeName, string domain)
        {
            Covenant.Requires<ArgumentNullException>(client != null);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(domain));
            CadenceHelper.ValidateWorkflowImplementation(workflowType);

            var methodMap = WorkflowMethodMap.Create(workflowType);

            // We need to register each workflow method that implements a workflow interface method
            // with the same signature that that was tagged by [WorkflowMethod].
            //
            // First, we'll create a dictionary that maps method signatures from any inherited
            // interfaces that are tagged by [WorkflowMethod] to the attribute.

            var methodSignatureToAttribute = new Dictionary<string, WorkflowMethodAttribute>();

            foreach (var interfaceType in workflowType.GetInterfaces())
            {
                foreach (var method in interfaceType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    var workflowMethodAttribute = method.GetCustomAttribute<WorkflowMethodAttribute>();

                    if (workflowMethodAttribute == null)
                    {
                        continue;
                    }

                    var signature = method.ToString();

                    if (methodSignatureToAttribute.ContainsKey(signature))
                    {
                        throw new NotSupportedException($"Workflow type [{workflowType.FullName}] cannot implement the [{signature}] method from two different interfaces.");
                    }

                    methodSignatureToAttribute.Add(signature, workflowMethodAttribute);
                }
            }

            // Next, we need to register the workflow methods that implement the
            // workflow interface.

            foreach (var method in workflowType.GetMethods())
            {
                if (!methodSignatureToAttribute.TryGetValue(method.ToString(), out var workflowMethodAttribute))
                {
                    continue;
                }

                var workflowTypeKey   = GetWorkflowTypeKey(client, workflowTypeName, workflowMethodAttribute);
                var alreadyRegistered = false;

                lock (syncLock)
                {
                    if (nameToRegistration.TryGetValue(workflowTypeName, out var existingRegistration))
                    {
                        if (!object.ReferenceEquals(existingRegistration.WorkflowType, workflowType))
                        {
                            throw new InvalidOperationException($"Conflicting workflow interface registration: Workflow interface [{workflowType.FullName}] is already registered for workflow type name [{workflowTypeName}].");
                        }

                        alreadyRegistered = true;
                    }
                    else
                    {
                        nameToRegistration[workflowTypeName] =
                            new WorkflowRegistration()
                            {
                                WorkflowType   = workflowType,
                                WorkflowMethod = method,
                                MethodMap      = methodMap
                            };
                    }
                }

                if (!alreadyRegistered)
                {
                    var reply = (WorkflowRegisterReply)await client.CallProxyAsync(
                        new WorkflowRegisterRequest()
                        {
                            Name   = GetWorkflowRegistrationKey(workflowTypeName, method),
                            Domain = client.ResolveDomain(domain)
                        });

                    reply.ThrowOnError();
                }
            }
        }

        /// <summary>
        /// Removes all type workflow interface registrations for a Cadence client (when it's being disposed).
        /// </summary>
        /// <param name="client">The client being disposed.</param>
        internal static void UnregisterClient(CadenceClient client)
        {
            Covenant.Requires<ArgumentNullException>(client != null);

            var prefix = $"{client.ClientId}::";

            lock (syncLock)
            {
                foreach (var key in nameToRegistration.Keys.Where(key => key.StartsWith(prefix)).ToList())
                {
                    nameToRegistration.Remove(key);
                }
            }
        }

        /// <summary>
        /// Returns the registration for the named Cadence workflow.
        /// </summary>
        /// <param name="client">The Cadence client.</param>
        /// <param name="workflowTypeName">The Cadence workflow type name.</param>
        /// <returns>The <see cref="WorkflowRegistration"/> or <c>null</c> if the type was not found.</returns>
        private static WorkflowRegistration GetWorkflowRegistration(CadenceClient client, string workflowTypeName)
        {
            Covenant.Requires<ArgumentNullException>(client != null);
            Covenant.Requires<ArgumentNullException>(workflowTypeName != null);

            lock (syncLock)
            {
                if (nameToRegistration.TryGetValue($"{client.ClientId}::{workflowTypeName}", out var registration))
                {
                    return registration;
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Called to handle a workflow related request message received from the cadence-proxy.
        /// </summary>
        /// <param name="client">The client that received the request.</param>
        /// <param name="request">The request message.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        internal static async Task OnProxyRequestAsync(CadenceClient client, ProxyRequest request)
        {
            Covenant.Requires<ArgumentNullException>(client != null);
            Covenant.Requires<ArgumentNullException>(request != null);

            ProxyReply reply;

            switch (request.Type)
            {
                case InternalMessageTypes.WorkflowInvokeRequest:

                    reply = await OnInvokeAsync(client, (WorkflowInvokeRequest)request);
                    break;

                case InternalMessageTypes.WorkflowSignalInvokeRequest:

                    reply = await OnSignalAsync(client, (WorkflowSignalInvokeRequest)request);
                    break;

                case InternalMessageTypes.WorkflowQueryInvokeRequest:

                    reply = await OnQueryAsync(client, (WorkflowQueryInvokeRequest)request);
                    break;

                case InternalMessageTypes.ActivityInvokeLocalRequest:

                    reply = await OnInvokeLocalActivity(client, (ActivityInvokeLocalRequest)request);
                    break;

                default:

                    throw new InvalidOperationException($"Unexpected message type [{request.Type}].");
            }

            await client.ProxyReplyAsync(request, reply);
        }

        /// <summary>
        /// Thread-safe method that maps a workflow ID to the corresponding workflow instance.
        /// </summary>
        /// <param name="client">The Cadence client.</param>
        /// <param name="contextId">The workflow's context ID.</param>
        /// <returns>The <see cref="WorkflowBase"/> instance or <c>null</c> if the workflow was not found.</returns>
        private static WorkflowBase GetWorkflow(CadenceClient client, long contextId)
        {
            Covenant.Requires<ArgumentNullException>(client != null);

            lock (syncLock)
            {
                if (idToWorkflow.TryGetValue(new WorkflowInstanceKey(client, contextId), out var workflow))
                {
                    return workflow;
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Handles workflow invocation.
        /// </summary>
        /// <param name="client">The associated cadence client.</param>
        /// <param name="request">The request message.</param>
        /// <returns>The reply message.</returns>
        internal static async Task<WorkflowInvokeReply> OnInvokeAsync(CadenceClient client, WorkflowInvokeRequest request)
        {
            Covenant.Requires<ArgumentNullException>(client != null);
            Covenant.Requires<ArgumentNullException>(request != null);
            Covenant.Requires<ArgumentException>(request.ReplayStatus != InternalReplayStatus.Unspecified);

            WorkflowBase            workflow;
            WorkflowRegistration    registration;

            var contextId   = request.ContextId;
            var workflowKey = new WorkflowInstanceKey(client, contextId);

            lock (syncLock)
            {
                if (request.ReplayStatus != InternalReplayStatus.Unspecified)
                {
#if TODO
                    // $todo(jeff.lill): Restore this code once Jack sets the status.

                    return new WorkflowInvokeReply()
                    {
                        Error = new CadenceError($"[{nameof(WorkflowInvokeRequest)}]: [{nameof(request.ReplayStatus)}] cannot be [{nameof(InternalReplayStatus.Unspecified)}]..")
                    };
#endif
                }

                if (idToWorkflow.TryGetValue(workflowKey, out workflow))
                {
                    return new WorkflowInvokeReply()
                    {
                        Error = new CadenceError($"A workflow with [ID={workflowKey}] is already running on this worker.")
                    };
                }

                registration = GetWorkflowRegistration(client, request.WorkflowType);

                if (registration == null)
                {
                    return new WorkflowInvokeReply()
                    {
                        Error = new CadenceError($"Workflow type name [Type={request.WorkflowType}] is not registered for this worker.")
                    };
                }
            }

            workflow = (WorkflowBase)Activator.CreateInstance(registration.WorkflowType);
            workflow.Workflow = 
                new Workflow(
                    parent:     (WorkflowBase)workflow,
                    client:             client, 
                    contextId:          contextId,
                    workflowTypeName:   request.WorkflowType,
                    domain:             request.Domain,
                    taskList:           request.TaskList,
                    workflowId:         request.WorkflowId,
                    runId:              request.RunId,
                    isReplaying:        request.ReplayStatus == InternalReplayStatus.Replaying,
                    methodMap:          registration.MethodMap);

            lock (syncLock)
            {
                idToWorkflow.Add(workflowKey, workflow);
            }

            // Register any workflow signal and/or query handlers with cadence-proxy

            foreach (var signalName in registration.MethodMap.GetSignalNames())
            {
                var reply = (WorkflowSignalSubscribeReply)await client.CallProxyAsync(
                    new WorkflowSignalSubscribeRequest()
                    {
                        ContextId  = contextId,
                        SignalName = signalName
                    });

                reply.ThrowOnError();
            }

            foreach (var queryType in registration.MethodMap.GetQueryTypes())
            {
                var reply = (WorkflowSetQueryHandlerReply)await client.CallProxyAsync(
                    new WorkflowSetQueryHandlerRequest()
                    {
                        ContextId = contextId,
                        QueryName = queryType
                    });

                reply.ThrowOnError();
            }

            // Start the workflow by calling its workflow entry point method.
            // This method will indicate that it has completed via one of these 
            // techniques:
            //
            //      1. The method returns normally with the workflow result.
            //
            //      2. The method calls [RestartAsync(result, args)] which throws an
            //         [InternalWorkflowRestartException] which will be caught and
            //         handled here.
            //
            //      3. The method throws another exception which will be caught
            //         and be used to indicate that the workflow failed.

            try
            {
                var workflowMethod   = registration.WorkflowMethod;
                var resultType       = workflowMethod.ReturnType;
                var args             = client.DataConverter.FromDataArray(request.Args, registration.WorkflowMethodParameterTypes);
                var serializedResult = emptyBytes;

                if (resultType.IsGenericType)
                {
                    // Method returns: Task<T>

                    var result = await (Task<object>)workflowMethod.Invoke(workflow, args);

                    serializedResult = client.DataConverter.ToData(result);
                }
                else
                {
                    // Method returns: Task

                    await (Task<object>)workflowMethod.Invoke(workflow, args);
                    serializedResult = emptyBytes;
                }

                return new WorkflowInvokeReply()
                {
                    Result = serializedResult
                };
            }
            catch (CadenceWorkflowRestartException e)
            {
                return new WorkflowInvokeReply()
                {
                    ContinueAsNew                             = true,
                    ContinueAsNewArgs                         = e.Args,
                    ContinueAsNewDomain                       = e.Domain,
                    ContinueAsNewTaskList                     = e.TaskList,
                    ContinueAsNewExecutionStartToCloseTimeout = CadenceHelper.ToCadence(e.ExecutionStartToCloseTimeout),
                    ContinueAsNewScheduleToCloseTimeout       = CadenceHelper.ToCadence(e.ScheduleToCloseTimeout),
                    ContinueAsNewScheduleToStartTimeout       = CadenceHelper.ToCadence(e.ScheduleToStartTimeout),
                    ContinueAsNewStartToCloseTimeout          = CadenceHelper.ToCadence(e.TaskStartToCloseTimeout),
                };
            }
            catch (CadenceException e)
            {
                return new WorkflowInvokeReply()
                {
                    Error = e.ToCadenceError()
                };
            }
            catch (Exception e)
            {
                return new WorkflowInvokeReply()
                {
                    Error = new CadenceError(e)
                };
            }
        }

        /// <summary>
        /// Handles workflow signals.
        /// </summary>
        /// <param name="client">The Cadence client.</param>
        /// <param name="request">The request message.</param>
        /// <returns>The reply message.</returns>
        internal static async Task<WorkflowSignalInvokeReply> OnSignalAsync(CadenceClient client, WorkflowSignalInvokeRequest request)
        {
            Covenant.Requires<ArgumentNullException>(client != null);
            Covenant.Requires<ArgumentNullException>(request != null);

            try
            {
                var workflow = GetWorkflow(client, request.ContextId);

                if (workflow != null)
                {
                    var method = workflow.Workflow.MethodMap.GetSignalMethod(request.SignalName);

                    if (method != null)
                    {
                        await (Task)(method.Invoke(workflow, new object[] { request.SignalArgs }));

                        return new WorkflowSignalInvokeReply()
                        {
                            RequestId = request.RequestId
                        };
                    }
                    else
                    {
                        return new WorkflowSignalInvokeReply()
                        {
                            Error = new CadenceEntityNotExistsException($"Workflow type [{workflow.GetType().FullName}] does not define a signal handler for [signalName={request.SignalName}].").ToCadenceError()
                        };
                    }
                }
                else
                {
                    // It's possible that we'll land here if the workflow has been scheduled
                    // and/or started but execution has not actually started.  Since signals
                    // are fire-and-forget, we're just going to ignore these here.

                    return new WorkflowSignalInvokeReply();
                }
            }
            catch (Exception e)
            {
                return new WorkflowSignalInvokeReply()
                {
                    Error = new CadenceError(e)
                };
            }
        }

        /// <summary>
        /// Handles workflow queries.
        /// </summary>
        /// <param name="client">The Cadence client.</param>
        /// <param name="request">The request message.</param>
        /// <returns>The reply message.</returns>
        internal static async Task<WorkflowQueryInvokeReply> OnQueryAsync(CadenceClient client, WorkflowQueryInvokeRequest request)
        {
            Covenant.Requires<ArgumentNullException>(client != null);
            Covenant.Requires<ArgumentNullException>(request != null);

            try
            {
                var workflow = GetWorkflow(client, request.ContextId);

                if (workflow != null)
                {
                    var method = workflow.Workflow.MethodMap.GetQueryMethod(request.QueryName);

                    if (method != null)
                    {
                        var result = await (Task<byte[]>)(method.Invoke(workflow, new object[] { request.QueryArgs }));

                        return new WorkflowQueryInvokeReply()
                        {
                            RequestId = request.RequestId,
                            Result    = result
                        };
                    }
                    else
                    {
                        return new WorkflowQueryInvokeReply()
                        {
                            Error = new CadenceEntityNotExistsException($"Workflow type [{workflow.GetType().FullName}] does not define a query handler for [queryType={request.QueryName}].").ToCadenceError()
                        };
                    }
                }
                else
                {
                    return new WorkflowQueryInvokeReply()
                    {
                        Error = new CadenceEntityNotExistsException($"Workflow with [contextID={request.ContextId}] does not exist.").ToCadenceError()
                    };
                }
            }
            catch (Exception e)
            {
                return new WorkflowQueryInvokeReply()
                {
                    Error = new CadenceError(e)
                };
            }
        }

        /// <summary>
        /// Handles workflow local activity invocations.
        /// </summary>
        /// <param name="client">The client the request was received from.</param>
        /// <param name="request">The request message.</param>
        /// <returns>The reply message.</returns>
        internal static async Task<ActivityInvokeLocalReply> OnInvokeLocalActivity(CadenceClient client, ActivityInvokeLocalRequest request)
        {
            Covenant.Requires<ArgumentNullException>(request != null);

            try
            {
                var workflow = GetWorkflow(client, request.ContextId);

                if (workflow != null)
                {
                    LocalActivityAction activityAction;

                    lock (syncLock)
                    {
                        if (!workflow.Workflow.IdToLocalActivityAction.TryGetValue(request.ActivityTypeId, out activityAction))
                        {
                            return new ActivityInvokeLocalReply()
                            {
                                Error = new CadenceEntityNotExistsException($"Activity type does not exist for [activityTypeId={request.ActivityTypeId}].").ToCadenceError()
                            };
                        }
                    }

                    var workerArgs = new WorkerArgs() { Client = client, ContextId = request.ActivityContextId };
                    var activity   = ActivityBase.Create(client, activityAction, null);
                    var result     = await activity.OnRunAsync(client, request.Args);

                    return new ActivityInvokeLocalReply()
                    {
                        Result = result
                    };
                }
                else
                {
                    return new ActivityInvokeLocalReply()
                    {
                        Error = new CadenceEntityNotExistsException($"Workflow with [contextID={request.ContextId}] does not exist.").ToCadenceError()
                    };
                }
            }
            catch (Exception e)
            {
                return new ActivityInvokeLocalReply()
                {
                    Error = new CadenceError(e)
                };
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <inheritdoc/>
        public Workflow Workflow { get; set; }
    }
}
