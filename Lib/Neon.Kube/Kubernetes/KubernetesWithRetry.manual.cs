﻿//-----------------------------------------------------------------------------
// FILE:	    KubernetesWithRetry.manual.cs
// CONTRIBUTOR: Auto-generated by [prebuilder] tool during pre-build event
// COPYRIGHT:	Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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

// WARNING: This file is automatically generated during the build.
//          Do not edit this manually.

#pragma warning disable CS1591  // Missing XML comment

using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using k8s;
using k8s.Autorest;
using k8s.Models;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Retry;
using Neon.Tasks;
using Neon.Collections;

namespace Neon.Kube
{
    public sealed partial class KubernetesWithRetry
    {
        //---------------------------------------------------------------------
        // $note(jefflill):
        //
        // The prebuilder tool is not able to generate the Kubernetes client related extensions
        // defined in [Neon.Kube.KubernetesExtensions] due to a chicken-and-egg situation, so
        // we're just going to implement these here manually.

        /// <summary>
        /// Adds a new Kubernetes secret or updates an existing secret.
        /// </summary>
        /// <param name="secret">The secret.</param>
        /// <param name="namespace">Optionally overrides the default namespace.</param>
        /// <returns>The updated secret.</returns>
        public async Task<V1Secret> UpsertSecretAsync(V1Secret secret, string @namespace = null)
        {
            await SyncContext.Clear;

            return await NormalizedRetryPolicy.InvokeAsync(
                async () =>
                {
                    return await k8s.UpsertSecretAsync(secret, @namespace);
                });
        }

        /// <summary>
        /// Executes a program within a pod container.
        /// </summary>
        /// <param name="namespace">Specifies the namespace hosting the pod.</param>
        /// <param name="name">Specifies the target pod name.</param>
        /// <param name="container">Identifies the target container within the pod.</param>
        /// <param name="command">Specifies the program and arguments to be executed.</param>
        /// <param name="noSuccessCheck">Optionally disables the <see cref="ExecuteResponse.EnsureSuccess"/> check.</param>
        /// <param name="cancellationToken">Optionally specifies a cancellation token.</param>
        /// <returns>An <see cref="ExecuteResponse"/> with the command exit code and output and error text.</returns>
        /// <exception cref="ExecuteException">Thrown if the exit code isn't zero and <paramref name="noSuccessCheck"/><c>=false</c>.</exception>
        public async Task<ExecuteResponse> NamespacedPodExecAsync(
            string @namespace,
            string name,
            string container,
            string[] command,
            bool noSuccessCheck = false,
            CancellationToken cancellationToken = default)
        {
            await SyncContext.Clear;

            return await NormalizedRetryPolicy.InvokeAsync(
                async () =>
                {
                    return await k8s.NamespacedPodExecAsync(@namespace, name, container, command, noSuccessCheck, cancellationToken);
                });
        }

        //---------------------------------------------------------------------
        // The methods below don't support the client retry policy and just call
        // the extension methods directly.

        /// <summary>
        /// Waits for a service deployment to complete.
        /// </summary>
        /// <param name="namespace">The namespace.</param>
        /// <param name="name">The deployment name.</param>
        /// <param name="labelSelector">The optional label selector.</param>
        /// <param name="fieldSelector">The optional field selector.</param>
        /// <param name="pollInterval">Optionally specifies the polling interval.  This defaults to 1 second.</param>
        /// <param name="timeout">Optopnally specifies the operation timeout.  This defaults to 30m seconds.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <remarks>
        /// One of <paramref name="name"/>, <paramref name="labelSelector"/>, or <paramref name="fieldSelector"/>
        /// must be specified.
        /// </remarks>
        public async Task WaitForDeploymentAsync(
            string @namespace,
            string name = null,
            string labelSelector = null,
            string fieldSelector = null,
            TimeSpan pollInterval = default,
            TimeSpan timeout = default)
        {
            await SyncContext.Clear;
            await k8s.WaitForDeploymentAsync(@namespace, name, labelSelector, fieldSelector, pollInterval, timeout);
        }

        /// <summary>
        /// Waits for a stateful set deployment to complete.
        /// </summary>
        /// <param name="namespace">The namespace.</param>
        /// <param name="name">The deployment name.</param>
        /// <param name="labelSelector">The optional label selector.</param>
        /// <param name="fieldSelector">The optional field selector.</param>
        /// <param name="pollInterval">Optionally specifies the polling interval.  This defaults to 1 second.</param>
        /// <param name="timeout">Optopnally specifies the operation timeout.  This defaults to 30m seconds.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <remarks>
        /// One of <paramref name="name"/>, <paramref name="labelSelector"/>, or <paramref name="fieldSelector"/>
        /// must be specified.
        /// </remarks>
        public async Task WaitForStatefulSetAsync(
            string @namespace,
            string name = null,
            string labelSelector = null,
            string fieldSelector = null,
            TimeSpan pollInterval = default,
            TimeSpan timeout = default)
        {
            await SyncContext.Clear;
            await k8s.WaitForStatefulSetAsync(@namespace, name, labelSelector, fieldSelector, pollInterval, timeout);
        }

        /// <summary>
        /// Waits for a daemon set deployment to complete.
        /// </summary>
        /// <param name="namespace">The namespace.</param>
        /// <param name="name">The deployment name.</param>
        /// <param name="labelSelector">The optional label selector.</param>
        /// <param name="fieldSelector">The optional field selector.</param>
        /// <param name="pollInterval">Optionally specifies the polling interval.  This defaults to 1 second.</param>
        /// <param name="timeout">Optopnally specifies the operation timeout.  This defaults to 30m seconds.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <remarks>
        /// One of <paramref name="name"/>, <paramref name="labelSelector"/>, or <paramref name="fieldSelector"/>
        /// must be specified.
        /// </remarks>
        public async Task WaitForDaemonsetAsync(
            string @namespace,
            string name = null,
            string labelSelector = null,
            string fieldSelector = null,
            TimeSpan pollInterval = default,
            TimeSpan timeout = default)
        {
            await SyncContext.Clear;
            await k8s.WaitForDaemonsetAsync(@namespace, name, labelSelector, fieldSelector, pollInterval, timeout);
        }

        /// <summary>
        /// A generic Watcher to watch Kubernetes resources, and respond with a custom (async) callback method.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="action">The function to handle updates.</param>
        /// <param name="namespaceParameter">That target Kubernetes namespace.</param>
        /// <param name="fieldSelector">The optional field selector</param>
        /// <param name="labelSelector">The optional label selector</param>
        /// <param name="resourceVersion">The start resource version.</param>
        /// <param name="resourceVersionMatch">The optional resourceVersionMatch setting.</param>
        /// <param name="timeoutSeconds">Optional timeout override.</param>
        /// <param name="logger">An optional <see cref="ILogger"/>.</param>
        /// <returns></returns>
        public async Task WatchAsync<T>(
            Func<WatchEvent<T>, Task> action,
            string namespaceParameter = null,
            string fieldSelector = null,
            string labelSelector = null,
            string resourceVersion = null,
            string resourceVersionMatch = null,
            int? timeoutSeconds = null,
            ILogger logger = null) where T : IKubernetesObject<V1ObjectMeta>, new()
        {
            await SyncContext.Clear;
            await k8s.WatchAsync<T>(action,
                namespaceParameter,
                fieldSelector: fieldSelector,
                labelSelector: labelSelector,
                resourceVersion: resourceVersion,
                resourceVersionMatch: resourceVersionMatch,
                timeoutSeconds: timeoutSeconds,
                logger: logger);
        }

        /// <summary>
        /// Generic method to list cluster custom objects.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="allowWatchBookmarks">Whether to allow watch bookmarks.</param>
        /// <param name="continueParameter">That continue parameter.</param>
        /// <param name="fieldSelector">The optional field selector</param>
        /// <param name="labelSelector">The optional label selector</param>
        /// <param name="limit">The optional limit.</param>
        /// <param name="resourceVersion">The start resource version.</param>
        /// <param name="resourceVersionMatch">The optional resourceVersionMatch setting.</param>
        /// <param name="timeoutSeconds">Optional timeout override.</param>
        /// <param name="watch">Whether to watch the resource.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns></returns>
        public async Task<HttpOperationResponse<object>> ListClusterCustomObjectWithHttpMessagesAsync<T>(
            bool? allowWatchBookmarks = null,
            string continueParameter = null,
            string fieldSelector = null,
            string labelSelector = null,
            int? limit = null,
            string resourceVersion = null,
            string resourceVersionMatch = null,
            int? timeoutSeconds = null,
            bool? watch = null,
            CancellationToken cancellationToken = default(CancellationToken)) where T : IKubernetesObject, new()
        {
            await SyncContext.Clear;
            return await k8s.ListClusterCustomObjectWithHttpMessagesAsync<T>(
                allowWatchBookmarks: allowWatchBookmarks,
                continueParameter: continueParameter,
                fieldSelector: fieldSelector,
                labelSelector: labelSelector,
                limit: limit,
                resourceVersion: resourceVersion,
                resourceVersionMatch: resourceVersionMatch,
                timeoutSeconds: timeoutSeconds,
                watch: watch,
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Generic method to list namespaced custom objects.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="namespaceParameter">That target Kubernetes namespace.</param>
        /// <param name="allowWatchBookmarks">Whether to allow watch bookmarks.</param>
        /// <param name="continueParameter">That continue parameter.</param>
        /// <param name="fieldSelector">The optional field selector</param>
        /// <param name="labelSelector">The optional label selector</param>
        /// <param name="limit">The optional limit.</param>
        /// <param name="resourceVersion">The start resource version.</param>
        /// <param name="resourceVersionMatch">The optional resourceVersionMatch setting.</param>
        /// <param name="timeoutSeconds">Optional timeout override.</param>
        /// <param name="watch">Whether to watch the resource.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns></returns>
        public async Task<HttpOperationResponse<object>> ListNamespacedCustomObjectWithHttpMessagesAsync<T>(
            string namespaceParameter,
            bool? allowWatchBookmarks = null,
            string continueParameter = null,
            string fieldSelector = null,
            string labelSelector = null,
            int? limit = null,
            string resourceVersion = null,
            string resourceVersionMatch = null,
            int? timeoutSeconds = null,
            bool? watch = null,
            CancellationToken cancellationToken = default(CancellationToken)) where T : IKubernetesObject, new()
        {
            await SyncContext.Clear;
            return await k8s.ListNamespacedCustomObjectWithHttpMessagesAsync<T>(
                namespaceParameter: namespaceParameter,
                allowWatchBookmarks: allowWatchBookmarks,
                continueParameter: continueParameter,
                fieldSelector: fieldSelector,
                labelSelector: labelSelector,
                limit: limit,
                resourceVersion: resourceVersion,
                resourceVersionMatch: resourceVersionMatch,
                timeoutSeconds: timeoutSeconds,
                watch: watch,
                cancellationToken: cancellationToken);
        }
    }
}
