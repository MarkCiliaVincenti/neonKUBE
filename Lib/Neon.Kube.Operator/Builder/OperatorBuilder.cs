﻿//-----------------------------------------------------------------------------
// FILE:	    OperatorBuilder.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ObjectPool;

using Neon.Diagnostics;
using Neon.Kube.Operator.Finalizer;
using Neon.Kube.Operator.Cache;
using Neon.Kube.Operator.Controller;
using Neon.Kube.Operator.ResourceManager;
using Neon.Kube.Operator.Webhook;
using Neon.Kube.Operator.Webhook.Ngrok;

using k8s.Models;
using k8s;
using Microsoft.AspNetCore.Components;
using Neon.BuildInfo;
using Neon.Kube.Operator.Attributes;

namespace Neon.Kube.Operator.Builder
{
    /// <summary>
    /// <para>
    /// Used to build a kubernetes operator.
    /// </para>
    /// </summary>
    public class OperatorBuilder : IOperatorBuilder
    {
        /// <inheritdoc/>
        public IServiceCollection Services { get; }

        private ComponentRegister componentRegister { get; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="services"></param>
        public OperatorBuilder(IServiceCollection services)
        {
            Services = services;
            componentRegister = new ComponentRegister();
        }

        internal IOperatorBuilder AddOperatorBase(OperatorSettings settings)
        {
            KubeHelper.InitializeJson();

            if (!Services.Any(x => x.ServiceType == typeof(IKubernetes)))
            {
                var k8s = new Kubernetes(
                    KubernetesClientConfiguration.BuildDefaultConfig(),
                    new KubernetesRetryHandler());
                Services.AddSingleton(k8s);
            }

            Services.AddSingleton(componentRegister);
            Services.AddSingleton<IFinalizerBuilder, FinalizerBuilder>();
            Services.AddTransient(typeof(IFinalizerManager<>), typeof(FinalizerManager<>));
            Services.AddSingleton(typeof(IResourceCache<>), typeof(ResourceCache<>));
            Services.AddSingleton(typeof(ILockProvider<>), typeof(LockProvider<>));

            if (settings.AssemblyScanningEnabled)
            {
                var types = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(s => s.GetTypes())
                    .Where(
                        t => t.GetInterfaces().Any(i => i.GetCustomAttributes<OperatorComponentAttribute>().Any())
                            & t.GetCustomAttribute<OperatorBuilderIgnoreAttribute>() == null
                    );

                foreach (var type in types)
                {
                    switch (type.GetInterfaces().Where(i => i.GetCustomAttributes<OperatorComponentAttribute>().Any()).Select(i => i.GetCustomAttribute<OperatorComponentAttribute>()).FirstOrDefault().ComponentType)
                    {
                        case OperatorComponentType.Controller:

                            var regMethod = typeof(OperatorBuilderExtensions).GetMethod(nameof(OperatorBuilderExtensions.AddController));
                            var args = new object[regMethod.GetParameters().Count()];
                            args[0] = this;
                            regMethod.MakeGenericMethod(type).Invoke(null, args);

                            break;

                        case OperatorComponentType.Finalizer:

                            regMethod = typeof(OperatorBuilderExtensions).GetMethod(nameof(OperatorBuilderExtensions.AddFinalizer));
                            regMethod.MakeGenericMethod(type).Invoke(null, new object[] { this });

                            break;

                        case OperatorComponentType.MutationWebhook:

                            regMethod = typeof(OperatorBuilderExtensions).GetMethod(nameof(OperatorBuilderExtensions.AddMutatingWebhook));
                            regMethod.MakeGenericMethod(type).Invoke(null, new object[] { this });

                            break;

                        case OperatorComponentType.ValidationWebhook:

                            regMethod = typeof(OperatorBuilderExtensions).GetMethod(nameof(OperatorBuilderExtensions.AddValidatingWebhook));
                            regMethod.MakeGenericMethod(type).Invoke(null, new object[] { this });

                            break;
                    }
                }
            }

            Services.AddHostedService<ResourceControllerManager>();

            Services.AddRouting();
            return this;
        }

        /// <inheritdoc/>
        public IOperatorBuilder AddFinalizer<TImplementation, TEntity>()
            where TImplementation : class, IResourceFinalizer<TEntity>
            where TEntity : IKubernetesObject<V1ObjectMeta>, new()
        {
            Services.TryAddScoped<TImplementation>();
            componentRegister.RegisterFinalizer<TImplementation, TEntity>();

            return this;
        }

        /// <inheritdoc/>
        public IOperatorBuilder AddMutatingWebhook<TImplementation, TEntity>()
            where TImplementation : class, IMutatingWebhook<TEntity>
            where TEntity : IKubernetesObject<V1ObjectMeta>, new()
        {
            Services.TryAddScoped<TImplementation>();
            componentRegister.RegisterMutatingWebhook<TImplementation, TEntity>();

            return this;
        }

        /// <inheritdoc/>
        public IOperatorBuilder AddValidatingWebhook<TImplementation, TEntity>()
            where TImplementation : class, IValidatingWebhook<TEntity>
            where TEntity : IKubernetesObject<V1ObjectMeta>, new()
        {
            Services.TryAddScoped<TImplementation>();
            componentRegister.RegisterValidatingWebhook<TImplementation, TEntity>();

            return this;
        }

        /// <inheritdoc/>
        public IOperatorBuilder AddController<TImplementation, TEntity>(
            string @namespace = null,
            ResourceManagerOptions options = null,
            Func<TEntity, bool> filter = null,
            LeaderElectionConfig leaderConfig = null,
            bool leaderElectionDisabled = false)
            where TImplementation : class, IOperatorController<TEntity>
            where TEntity : IKubernetesObject<V1ObjectMeta>, new()
        {
            var resourceManager = new ResourceManager<TEntity, TImplementation>(
                serviceProvider: Services.BuildServiceProvider(),
                @namespace: @namespace,
                options: options,
                filter: filter,
                leaderConfig: leaderConfig,
                leaderElectionDisabled: leaderElectionDisabled);

            Services.AddSingleton(resourceManager);
            componentRegister.RegisterResourceManager<ResourceManager<TEntity, TImplementation>>();

            Services.TryAddScoped<TImplementation>();
            componentRegister.RegisterController<TImplementation, TEntity>();

            return this;
        }

        /// <inheritdoc/>
        public IOperatorBuilder AddNgrokTunnnel(
            string hostname = "localhost",
            int port = 5000,
            string ngrokDirectory = null,
            string ngrokAuthToken = null,
            bool enabled = true)
        {
            if (!enabled)
            {
                return this;
            }

            Services.AddHostedService(
                services => new NgrokWebhookTunnel(
                    services.GetRequiredService<IKubernetes>(),
                    componentRegister,
                    services,
                    ngrokDirectory,
                    ngrokAuthToken,
                    services.GetService<ILogger>())
                {
                    Host = hostname,
                    Port = port
                });

            return this;
        }
    }
}
