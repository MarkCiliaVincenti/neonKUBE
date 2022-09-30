﻿//-----------------------------------------------------------------------------
// FILE:	    EventQueue.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Net;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Neon.Common;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Kube;
using Neon.Tasks;

using KubeOps.Operator;
using KubeOps.Operator.Builder;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Entities;

using k8s;
using k8s.Autorest;
using k8s.Models;

using Prometheus;
using System.Reactive.Subjects;
using KubeOps.Operator.Kubernetes;

namespace Neon.Kube.Operator
{
    internal class EventQueue<TEntity>
        where TEntity : class, IKubernetesObject<V1ObjectMeta>
    {
        private readonly IKubernetes k8s;
        private readonly ILogger logger;
        private readonly ResourceManagerOptions options;
        private readonly Dictionary<string, CancellationTokenSource> queue;
        private readonly Func<WatchEvent<TEntity>, Task> eventHandler;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="k8s"></param>
        /// <param name="options"></param>
        /// <param name="eventHandler"></param>
        public EventQueue(
            IKubernetes k8s,
            ResourceManagerOptions options,
            Func<WatchEvent<TEntity>, Task> eventHandler)
        {
            this.k8s     = k8s;
            this.options = options; 
            this.eventHandler = eventHandler;
            this.logger  = TelemetryHub.CreateLogger($"Neon.Kube.Operator.EventQueue({typeof(TEntity).Name})");
            this.queue   = new Dictionary<string, CancellationTokenSource>();
        }

        /// <summary>
        /// Requeue an event.
        /// </summary>
        /// <param name="event"></param>
        /// <param name="delay"></param>
        /// <param name="watchEventType"></param>
        /// <returns></returns>
        public async Task EnqueueAsync(
            WatchEvent<TEntity> @event, 
            TimeSpan? delay = null, 
            WatchEventType watchEventType = WatchEventType.Modified)
        {
            await SyncContext.Clear;

            if (queue.ContainsKey(@event.Value.Uid()))
            {
                return;
            }

            if (delay == null)
            {
                delay = GetDelay(@event.Attempt);
            }

            @event.Type = watchEventType;

            var cts = new CancellationTokenSource();

            queue.Add(@event.Value.Uid(), cts);

            _ = QueueAsync(@event, delay.Value, cts.Token);
        }

        /// <summary>
        /// Dequeue an event.
        /// </summary>
        /// <param name="event"></param>
        /// <returns></returns>
        public async Task DequeueAsync(WatchEvent<TEntity> @event)
        {
            await SyncContext.Clear;

            if (queue.TryGetValue(@event.Value.Uid(), out var cts))
            {
                cts.Cancel();
            }
        }

        private async Task QueueAsync(WatchEvent<TEntity> @event, TimeSpan delay, CancellationToken cancellationToken)
        {
            await SyncContext.Clear;

            try
            {
                await Task.Delay(delay, cancellationToken);

                await eventHandler?.Invoke(@event);
            }
            catch (TaskCanceledException)
            {
                if (queue.ContainsKey(@event.Value.Uid()))
                {
                    queue.Remove(@event.Value.Uid());
                }
            }
        }

        private TimeSpan GetDelay(int attempts)
        {
            var delay = Math.Min(options.ErrorMinRequeueInterval.TotalMilliseconds * (attempts),
                    options.ErrorMaxRequeueInterval.TotalMilliseconds);

            return TimeSpan.FromMilliseconds(delay);
        }
    }
}
