﻿//-----------------------------------------------------------------------------
// FILE:	    ActivityBase.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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

using Microsoft.Extensions.DependencyInjection;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Temporal;
using Neon.Temporal.Internal;

namespace Neon.Temporal
{
    /// <summary>
    /// Base class that must be inherited by all implementations.
    /// </summary>
    public abstract class ActivityBase
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Used to map a Temporal client ID and workflow context ID into a
        /// key that can be used to dereference <see cref="idToActivity"/>.
        /// </summary>
        private struct ActivityKey
        {
            private long clientId;
            private long contextId;

            public ActivityKey(TemporalClient client, long contextId)
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
                if (obj == null || !(obj is ActivityKey))
                {
                    return false;
                }

                var other = (ActivityKey)obj;

                return this.clientId == other.clientId &&
                       this.contextId == other.contextId;
            }

            public override string ToString()
            {
                return $"clientID={clientId}, contextId={contextId}";
            }
        }

        /// <summary>
        /// Used for mapping an activity type name to its underlying type, constructor
        /// and entry point method.
        /// </summary>
        private struct ActivityRegistration
        {
            /// <summary>
            /// The activity type.
            /// </summary>
            public Type ActivityType { get; set; }

            /// <summary>
            /// The activity entry point method.
            /// </summary>
            public MethodInfo ActivityMethod { get; set; }

            /// <summary>
            /// The activity method parameter types.
            /// </summary>
            public Type[] ActivityMethodParameterTypes { get; set; }
        }

        //---------------------------------------------------------------------
        // Static members

        private static object                                   syncLock     = new object();
        private static object[]                                 noArgs       = new object[0];
        private static Dictionary<ActivityKey, ActivityBase>    idToActivity = new Dictionary<ActivityKey, ActivityBase>();

        // This dictionary is used to map activity type names to the target activity
        // type, constructor, and entry point method.  Note that these mappings are 
        // scoped to specific Temporal client instances by prefixing the type name with:
        //
        //      CLIENT-ID::
        //
        // where CLIENT-ID is the locally unique ID of the client.  This is important,
        // because we'll need to remove the entries for clients when they're disposed.
        //
        // Activity type names may also include a second "::" separator with the
        // activity method name appended afterwards to handle activity interfaces
        // that have multiple methods.  So, valid activity registrations
        // may looks like:
        // 
        //      1::my-activity                  -- clientId = 1, activity type name = my-activity
        //      1::my-activity::my-entrypoint   -- clientId = 1, activity type name = my-activity, entrypoint = my-entrypoint

        private static Dictionary<string, ActivityRegistration> nameToRegistration = new Dictionary<string, ActivityRegistration>();

        /// <summary>
        /// Restores the class to its initial state.
        /// </summary>
        internal static void Reset()
        {
            lock (syncLock)
            {
                idToActivity.Clear();
            }
        }

        /// <summary>
        /// Prepends the Temporal client ID to the activity type name and optional
        /// activity method attribute name to generate the key used to dereference the 
        /// <see cref="nameToRegistration"/> dictionary.
        /// </summary>
        /// <param name="client">The Temporal client.</param>
        /// <param name="activityTypeName">The activity type name.</param>
        /// <param name="activityMethodAttribute">Optionally specifies the activity method attribute. </param>
        /// <returns>The prepended activity registration key.</returns>
        private static string GetActivityTypeKey(TemporalClient client, string activityTypeName, ActivityMethodAttribute activityMethodAttribute = null)
        {
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(activityTypeName), nameof(activityTypeName));

            if (string.IsNullOrEmpty(activityMethodAttribute?.Name))
            {
                return $"{client.ClientId}::{activityTypeName}";
            }
            else
            {
                return $"{client.ClientId}::{activityTypeName}::{activityMethodAttribute.Name}";
            }
        }

        /// <summary>
        /// Strips the leading client ID from the activity type key passed
        /// and returns the type name actually registered with Temporal.
        /// </summary>
        /// <param name="activityTypeKey">The activity type key.</param>
        /// <returns>The Temporal workflow type name.</returns>
        private static string GetActivityTypeNameFromKey(string activityTypeKey)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(activityTypeKey), nameof(activityTypeKey));

            var separatorPos = activityTypeKey.IndexOf(TemporalHelper.ActivityTypeMethodSeparator);

            Covenant.Assert(separatorPos >= 0);

            return activityTypeKey.Substring(separatorPos + TemporalHelper.ActivityTypeMethodSeparator.Length);
        }

        /// <summary>
        /// Registers an activity type.
        /// </summary>
        /// <param name="client">The associated client.</param>
        /// <param name="activityType">The activity type.</param>
        /// <param name="activityTypeName">The name used to identify the implementation.</param>
        /// <param name="namespace">Specifies the target namespace.</param>
        /// <returns><c>true</c> if the activity was already registered.</returns>
        /// <exception cref="InvalidOperationException">Thrown if a different activity class has already been registered for <paramref name="activityTypeName"/>.</exception>
        internal async static Task RegisterAsync(TemporalClient client, Type activityType, string activityTypeName, string @namespace)
        {
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(@namespace), nameof(@namespace));
            TemporalHelper.ValidateActivityImplementation(activityType);

            // We need to register each activity method that implements an activity interface method
            // with the same signature that that was tagged by [ActivityMethod].
            //
            // First, we'll create a dictionary that maps method signatures from any inherited
            // interfaces that are tagged by [ActivityMethod] to the attribute.

            var methodSignatureToAttribute = new Dictionary<string, ActivityMethodAttribute>();

            foreach (var interfaceType in activityType.GetInterfaces())
            {
                foreach (var method in interfaceType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    var activityMethodAttribute = method.GetCustomAttribute<ActivityMethodAttribute>();

                    if (activityMethodAttribute == null)
                    {
                        continue;
                    }

                    var signature = method.ToString();

                    if (methodSignatureToAttribute.ContainsKey(signature))
                    {
                        throw new NotSupportedException($"Activity type [{activityType.FullName}] cannot implement the [{signature}] method from two different interfaces.");
                    }

                    methodSignatureToAttribute.Add(signature, activityMethodAttribute);
                }
            }

            // Next, we need to register the activity methods that implement the
            // activity interface.

            foreach (var method in activityType.GetMethods())
            {
                if (!methodSignatureToAttribute.TryGetValue(method.ToString(), out var activityMethodAttribute))
                {
                    continue;
                }

                var activityTypeKey = GetActivityTypeKey(client, activityTypeName, activityMethodAttribute);

                lock (syncLock)
                {
                    if (nameToRegistration.TryGetValue(activityTypeKey, out var registration))
                    {
                        if (!object.ReferenceEquals(registration.ActivityType, registration.ActivityType))
                        {
                            throw new InvalidOperationException($"Conflicting activity type registration: Activity type [{activityType.FullName}] is already registered for activity type name [{activityTypeKey}].");
                        }
                    }
                    else
                    {
                        nameToRegistration[activityTypeKey] =
                            new ActivityRegistration()
                            {
                                ActivityType                 = activityType,
                                ActivityMethod               = method,
                                ActivityMethodParameterTypes = method.GetParameterTypes()
                            };
                    }
                }

                var reply = (ActivityRegisterReply)await client.CallProxyAsync(
                    new ActivityRegisterRequest()
                    {
                        Name      = GetActivityTypeNameFromKey(activityTypeKey),
                        Namespace = client.ResolveNamespace(@namespace)
                    });

                // $hack(jefflill): 
                //
                // We're going to ignore any errors here to handle:
                //
                //      https://github.com/nforgeio/neonKUBE/issues/668

                // reply.ThrowOnError();
            }
        }

        /// <summary>
        /// Removes all type activity type registrations for a Temporal client (when it's being disposed).
        /// </summary>
        /// <param name="client">The client being disposed.</param>
        internal static void UnregisterClient(TemporalClient client)
        {
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));

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
        /// Returns the <see cref="ActivityRegistration"/> for any activity type and activity type name.
        /// </summary>
        /// <param name="activityType">The targetr activity type.</param>
        /// <param name="activityTypeName">The target activity type name.</param>
        /// <returns>The <see cref="ActivityRegistration"/>.</returns>
        private static ActivityRegistration GetActivityInvokeInfo(Type activityType, string activityTypeName)
        {
            Covenant.Requires<ArgumentNullException>(activityType != null, nameof(activityType));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(activityTypeName), nameof(activityTypeName));

            var info = new ActivityRegistration();

            // Locate the target method.  Note that the activity type name will be
            // formatted like:
            //
            //      CLIENT-ID::TYPE-NAME
            // or   CLIENT-ID::TYPE-NAME::METHOD-NAME

            var activityTypeNameParts = activityTypeName.Split(TemporalHelper.ActivityTypeMethodSeparator.ToCharArray(), 3);
            var activityMethodName    = (string)null;

            Covenant.Assert(activityTypeNameParts.Length >= 2);

            if (activityTypeNameParts.Length > 2)
            {
                activityMethodName = activityTypeNameParts[2];
            }

            if (string.IsNullOrEmpty(activityMethodName))
            {
                activityMethodName = null;
            }

            foreach (var method in activityType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                var activityMethodAttribute = method.GetCustomAttribute<ActivityMethodAttribute>();

                if (activityMethodAttribute == null)
                {
                    continue;
                }

                var name = activityMethodAttribute.Name;

                if (string.IsNullOrEmpty(name))
                {
                    name = null;
                }

                if (name == activityMethodName)
                {
                    info.ActivityMethod = method;
                    break;
                }
            }

            if (info.ActivityMethod == null)
            {
                throw new ArgumentException($"Activity type [{activityType.FullName}] does not have an entry point method tagged with [ActivityMethod(Name = \"{activityMethodName}\")].", nameof(activityType));
            }

            return info;
        }

        /// <summary>
        /// Constructs an activity instance suitable for executing a local activity.
        /// </summary>
        /// <param name="client">The associated client.</param>
        /// <param name="invokeInfo">The activity invocation information.</param>
        /// <param name="contextId">The activity context ID or <c>null</c> for local activities.</param>
        /// <returns>The constructed activity.</returns>
        private static ActivityBase Create(TemporalClient client, ActivityRegistration invokeInfo, long? contextId)
        {
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));

            var activity = (ActivityBase)ActivatorUtilities.CreateInstance(NeonHelper.ServiceContainer, invokeInfo.ActivityType);

            activity.Initialize(client, invokeInfo.ActivityType, invokeInfo.ActivityMethod, client.DataConverter, contextId);

            return activity;
        }

        /// <summary>
        /// Constructs an activity instance suitable for executing a normal (non-local) activity.
        /// </summary>
        /// <param name="client">The associated client.</param>
        /// <param name="activityAction">The target activity action.</param>
        /// <param name="contextId">The activity context ID or <c>null</c> for local activities.</param>
        /// <returns>The constructed activity.</returns>
        internal static ActivityBase Create(TemporalClient client, LocalActivityAction activityAction, long? contextId)
        {
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));

            var activity = (ActivityBase)(activityAction.ActivityConstructor.Invoke(noArgs));

            activity.Initialize(client, activityAction.ActivityType, activityAction.ActivityMethod, client.DataConverter, contextId);

            return activity;
        }

        /// <summary>
        /// Called to handle a workflow related request message received from the temporal-proxy.
        /// </summary>
        /// <param name="client">The client that received the request.</param>
        /// <param name="request">The request message.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        internal static async Task OnProxyRequestAsync(TemporalClient client, ProxyRequest request)
        {
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));
            Covenant.Requires<ArgumentNullException>(request != null, nameof(request));

            ProxyReply reply;

            switch (request.Type)
            {
                case InternalMessageTypes.ActivityInvokeRequest:

                    reply = await OnActivityInvokeRequest(client, (ActivityInvokeRequest)request);
                    break;

                case InternalMessageTypes.ActivityStoppingRequest:

                    reply = await ActivityStoppingRequest(client, (ActivityStoppingRequest)request);
                    break;

                default:

                    throw new InvalidOperationException($"Unexpected message type [{request.Type}].");
            }

            await client.ProxyReplyAsync(request, reply);
        }

        /// <summary>
        /// Handles received <see cref="ActivityInvokeRequest"/> messages.
        /// </summary>
        /// <param name="client">The receiving Temporal client.</param>
        /// <param name="request">The request message.</param>
        /// <returns>The reply message.</returns>
        private static async Task<ActivityInvokeReply> OnActivityInvokeRequest(TemporalClient client, ActivityInvokeRequest request)
        {
            ActivityRegistration  invokeInfo;

            lock (syncLock)
            {
                if (!nameToRegistration.TryGetValue(GetActivityTypeKey(client, request.Activity), out invokeInfo))
                {
                    throw new KeyNotFoundException($"Cannot resolve [activityTypeName = {request.Activity}] to a registered activity type and activity method.");
                }
            }

            var activity = Create(client, invokeInfo, request.ContextId);

            try
            {
                var result = await activity.OnInvokeAsync(client, request.Args);

                if (activity.CompleteExternally)
                {
                    return new ActivityInvokeReply()
                    {
                        Pending = true
                    };
                }
                else
                {
                    return new ActivityInvokeReply()
                    {
                        Result = result,
                    };
                }
            }
            catch (TemporalException e)
            {
                activity.logger.LogError(e);

                return new ActivityInvokeReply()
                {
                    Error = e.ToTemporalError()
                };
            }
            catch (TaskCanceledException e)
            {
                return new ActivityInvokeReply()
                {
                    Error = new CancelledException(e.Message).ToTemporalError()
                };
            }
            catch (Exception e)
            {
                activity.logger.LogError(e);

                return new ActivityInvokeReply()
                {
                    Error = new TemporalError(e)
                };
            }
        }

        /// <summary>
        /// Handles received <see cref="ActivityStoppingRequest"/> messages.
        /// </summary>
        /// <param name="client">The receiving Temporal client.</param>
        /// <param name="request">The request message.</param>
        /// <returns>The reply message.</returns>
        private static async Task<ActivityStoppingReply> ActivityStoppingRequest(TemporalClient client, ActivityStoppingRequest request)
        {
            lock (syncLock)
            {
                if (idToActivity.TryGetValue(new ActivityKey(client, request.ContextId), out var activity))
                {
                    activity.CancellationTokenSource.Cancel();
                }
            }

            return await Task.FromResult(new ActivityStoppingReply());
        }

        //---------------------------------------------------------------------
        // Instance members

        private Type            activityType;
        private ActivityTask    activityTask;
        private MethodInfo      activityMethod;
        private IDataConverter  dataConverter;
        private INeonLogger     logger;

        /// <summary>
        /// Default protected constructor.
        /// </summary>
        protected ActivityBase()
        {
        }

        /// <summary>
        /// Called internally to initialize the activity.
        /// </summary>
        /// <param name="client">The associated client.</param>
        /// <param name="activityType">Specifies the target activity type.</param>
        /// <param name="activityMethod">Specifies the target activity method.</param>
        /// <param name="dataConverter">Specifies the data converter to be used for parameter and result serilization.</param>
        /// <param name="contextId">The activity's context ID or <c>null</c> for local activities.</param>
        internal void Initialize(TemporalClient client, Type activityType, MethodInfo activityMethod, IDataConverter dataConverter, long? contextId)
        {
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));
            Covenant.Requires<ArgumentNullException>(activityType != null, nameof(activityType));
            Covenant.Requires<ArgumentNullException>(activityMethod != null, nameof(activityMethod));
            Covenant.Requires<ArgumentNullException>(dataConverter != null, nameof(dataConverter));
            TemporalHelper.ValidateActivityImplementation(activityType);

            this.Client                  = client;
            this.Activity                = new Activity(this);
            this.activityType            = activityType;
            this.activityMethod          = activityMethod;
            this.dataConverter           = dataConverter;
            this.ContextId               = contextId;
            this.CancellationTokenSource = new CancellationTokenSource();
            this.CancellationToken       = CancellationTokenSource.Token;
            this.logger                  = LogManager.Default.GetLogger(sourceModule: activityType.FullName);
        }

        /// <inheritdoc/>
        public Activity Activity { get; set;  }

        /// <summary>
        /// Returns the <see cref="TemporalClient"/> managing this activity invocation.
        /// </summary>
        internal TemporalClient Client { get; private set; }

        /// <summary>
        /// Returns the <see cref="CancellationTokenSource"/> for the activity invocation.
        /// </summary>
        internal CancellationTokenSource CancellationTokenSource { get; private set; }

        /// <summary>
        /// Returns the <see cref="CancellationToken"/> for thge activity invocation.
        /// </summary>
        internal CancellationToken CancellationToken { get; private set; }

        /// <summary>
        /// Returns the context ID for the activity invocation or <c>null</c> for
        /// local activities.
        /// </summary>
        internal long? ContextId { get; private set; }

        /// <summary>
        /// Indicates whether the activity was executed locally.
        /// </summary>
        internal bool IsLocal => !ContextId.HasValue;

        /// <summary>
        /// <para>
        /// Returns additional information about the activity and the workflow that executed it.
        /// </para>
        /// <note>
        /// For local activity executions, this property will return an <see cref="ActivityTask"/>
        /// instance with all properties initialized to their default values.
        /// </note>
        /// </summary>
        internal ActivityTask ActivityTask
        {
            get
            {
                if (activityTask == null)
                {
                    activityTask = new ActivityTask();
                }

                return activityTask;
            }

            set => activityTask = value;
        }

        /// <summary>
        /// Indicates that the activity will be completed externally.
        /// </summary>
        internal bool CompleteExternally { get; set; } = false;

        /// <summary>
        /// Executes the target activity method.
        /// </summary>
        /// <param name="client">The associated Temporal client.</param>
        /// <param name="argBytes">The encoded activity arguments.</param>
        /// <returns>The encoded activity results.</returns>
        private async Task<byte[]> InvokeAsync(TemporalClient client, byte[] argBytes)
        {
            var parameters     = activityMethod.GetParameters();
            var parameterTypes = new Type[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                parameterTypes[i] = parameters[i].ParameterType;
            }

            var resultType       = activityMethod.ReturnType;
            var args             = TemporalHelper.BytesToArgs(dataConverter, argBytes, parameterTypes);
            var serializedResult = Array.Empty<byte>();

            if (resultType.IsGenericType)
            {
                // Activity method returns: Task<T>

                var result = await NeonHelper.GetTaskResultAsObjectAsync((Task)activityMethod.Invoke(this, args));

                serializedResult = client.DataConverter.ToData(result);
            }
            else
            {
                // Activity method returns: Task

                await (Task)activityMethod.Invoke(this, args);
            }

            return serializedResult;
        }

        /// <summary>
        /// Called internally to execute the activity.
        /// </summary>
        /// <param name="client">The Temporal client.</param>
        /// <param name="args">The encoded activity arguments.</param>
        /// <returns>Thye activity results.</returns>
        internal async Task<byte[]> OnInvokeAsync(TemporalClient client, byte[] args)
        {
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));

            if (IsLocal)
            {
                return await InvokeAsync(client, args);
            }
            else
            {
                // Capture the activity information.

                var reply = (ActivityGetInfoReply)(await Client.CallProxyAsync(
                    new ActivityGetInfoRequest()
                    {
                        ContextId = ContextId.Value,
                    }));

                reply.ThrowOnError();

                ActivityTask = reply.Info.ToPublic();

                // Track the activity.

                var activityKey = new ActivityKey(client, ContextId.Value);

                try
                {
                    lock (syncLock)
                    {
                        idToActivity[activityKey] = this;
                    }

                    return await InvokeAsync(client, args);
                }
                catch (Exception e)
                {
                    logger.LogError(e);

                    throw;
                }
                finally
                {
                    lock (syncLock)
                    {
                        idToActivity.Remove(activityKey);
                    }
                }
            }
        }

        /// <summary>
        /// Ensures that the activity has an associated Temporal context and thus
        /// is not a local actvity.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown for local activities.</exception>
        internal void EnsureNotLocal()
        {
            if (IsLocal)
            {
                throw new InvalidOperationException("This operation is not supported for local activity executions.");
            }
        }
    }
}
