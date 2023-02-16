﻿//------------------------------------------------------------------------------
// FILE:        Service.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

using Neon.Common;
using Neon.Data;
using Neon.Diagnostics;
using Neon.Kube;
using Neon.Kube.Clients;
using Neon.Kube.Glauth;
using Neon.Kube.Operator;
using Neon.Kube.Operator.ResourceManager;
using Neon.Kube.Resources;
using Neon.Kube.Resources.CertManager;
using Neon.Net;
using Neon.Retry;
using Neon.Service;
using Neon.Tasks;

using NeonClusterOperator.Harbor;

using DnsClient;

using Grpc.Net.Client;

using k8s;
using k8s.Models;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Npgsql;

using OpenTelemetry;
using OpenTelemetry.Instrumentation.Quartz;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using Prometheus;

using Quartz;
using Quartz.Impl;
using Quartz.Logging;
using System.Net.Http;

using Task    = System.Threading.Tasks.Task;
using Metrics = Prometheus.Metrics;
using Minio;
using Neon.Kube.Operator.Rbac;

namespace NeonClusterOperator
{
    /// <summary>
    /// Implements the <b>neon-cluster-operator</b> service.
    /// </summary>
    /// <remarks>
    /// <para><b>ENVIRONMENT VARIABLES</b></para>
    /// <para>
    /// The <b>neon-node-agent</b> is configured using these environment variables:
    /// </para>
    /// <list type="table">
    /// <item>
    ///     <term><b>WATCHER_TIMEOUT_INTERVAL</b></term>
    ///     <description>
    ///     <b>timespan:</b> Specifies the maximum time the resource watcher will wait without
    ///     a response before creating a new request.  This defaults to <b>2 minutes</b>.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>WATCHER_MAX_RETRY_INTERVAL</b></term>
    ///     <description>
    ///     <b>timespan:</b> Specifies the maximum time the resource watcher will wait
    ///     after a watch failure.  This defaults to <b>15 seconds</b>.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>NODETASK_IDLE_INTERVAL</b></term>
    ///     <description>
    ///     <b>timespan:</b> Specifies the interval at which IDLE events will be raised
    ///     for <b>NodeTask</b> giving the operator the chance to delete node tasks assigned
    ///     to nodes that don't exist.  This defaults to <b>60 seconds</b>.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>NODETASK_ERROR_MIN_REQUEUE_INTERVAL</b></term>
    ///     <description>
    ///     <b>timespan:</b> Specifies the minimum requeue interval to use when an
    ///     exception is thrown when handling NodeTask events.  This
    ///     value will be doubled when subsequent events also fail until the
    ///     requeue time maxes out at <b>CONTAINERREGISTRY_ERROR_MIN_REQUEUE_INTERVAL</b>.
    ///     This defaults to <b>5 seconds</b>.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>NODETASK_ERROR_MIN_REQUEUE_INTERVAL</b></term>
    ///     <description>
    ///     <b>timespan:</b> Specifies the maximum requeue time for NodeTask
    ///     handler exceptions.  This defaults to <b>60 seconds</b>.
    ///     </description>
    /// </item>
    /// </list>
    /// </remarks>
    [RbacRule<V1ConfigMap>(Verbs = RbacVerb.All, Scope = EntityScope.Cluster)]
    [RbacRule<V1Secret>(Verbs = RbacVerb.All, Scope = EntityScope.Cluster)]
    public partial class Service : NeonService
    {
        /// <summary>
        /// Information about the cluster.
        /// </summary>
        public ClusterInfo ClusterInfo;

        /// <summary>
        /// Kubernetes client.
        /// </summary>
        public IKubernetes K8s;

        /// <summary>
        /// Kubernetes client.
        /// </summary>
        public HeadendClient HeadendClient;

        /// <summary>
        /// Harbor client.
        /// </summary>
        public HarborClient HarborClient;

        /// <summary>
        /// Dex client.
        /// </summary>
        public Dex.Dex.DexClient DexClient;

        /// <summary>
        /// The service port;
        /// </summary>
        public int Port { get; private set; } = 443;

        // private fields
        private HttpClient harborHttpClient;
        private readonly JsonSerializerOptions serializeOptions;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="name">The service name.</param>
        public Service(string name)
            : base(name, version: KubeVersions.NeonKube, new NeonServiceOptions() { MetricsPrefix = "neonclusteroperator" })
        {
            serializeOptions = new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            serializeOptions.Converters.Add(new JsonStringEnumMemberConverter());
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }

        /// <inheritdoc/>
        protected async override Task<int> OnRunAsync()
        {
            K8s = new Kubernetes(KubernetesClientConfiguration.BuildDefaultConfig(), new KubernetesRetryHandler());
            
            LogContext.SetCurrentLogProvider(TelemetryHub.LoggerFactory);

            harborHttpClient = new HttpClient(new HttpClientHandler() { UseCookies = false });
            HarborClient = new HarborClient(harborHttpClient);
            HarborClient.BaseUrl = "http://registry-harbor-harbor-core.neon-system/api/v2.0";

            HeadendClient = HeadendClient.Create();
            HeadendClient.HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GetEnvironmentVariable("NEONCLOUD_HEADEND_TOKEN"));

            var channel = GrpcChannel.ForAddress($"http://{KubeService.Dex}:5557");
            DexClient = new Dex.Dex.DexClient(channel);

            await WatchClusterInfoAsync();
            await WatchRootUserAsync();

            // Start the web service.

            if (NeonHelper.IsDevWorkstation)
            {
                Port = 11005;
            }

            Logger.LogInformationEx(() => $"Listening on: {IPAddress.Any}:{Port}");
            
            var k8s = KubernetesOperatorHost
               .CreateDefaultBuilder()
               .ConfigureOperator(configure =>
               {
                   configure.Port                    = Port;
                   configure.AssemblyScanningEnabled = true;
                   configure.Name                    = Name;
                   configure.DeployedNamespace       = KubeNamespace.NeonSystem;
               })
               .ConfigureNeonKube()
               .AddSingleton(typeof(Service), this)
               .UseStartup<OperatorStartup>()
               .Build();

            _ = k8s.RunAsync();

            Logger.LogInformationEx(() => $"Listening on: {IPAddress.Any}:{Port}");

            // Indicate that the service is running.

            await StartedAsync();

            // Handle termination gracefully.
            await Terminator.StopEvent.WaitAsync();
            Terminator.ReadyToExit();

            return 0;
        }

        /// <inheritdoc/>
        protected override bool OnLoggerConfg(OpenTelemetryLoggerOptions options)
        {
            if (NeonHelper.IsDevWorkstation || !string.IsNullOrEmpty(GetEnvironmentVariable("DEBUG")))
            {
                options.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName: Name, serviceVersion: Version));

                options.AddConsoleTextExporter(options =>
                {
                    options.Format = (record) => $"[{record.LogLevel}][{record.CategoryName}] {record.FormattedMessage}";
                });

                return true;
            }

            return false;
        }

        /// <inheritdoc/>
        protected override bool OnTracerConfig(TracerProviderBuilder builder)
        {
            builder.AddHttpClientInstrumentation(
                options =>
                {
                    options.Filter = (httpcontext) =>
                    {
                        if (GetEnvironmentVariable("LOG_LEVEL").ToLower() == "trace")
                        {
                            return true;
                        }

                        // filter out leader election since it's really chatty
                        if (httpcontext.RequestUri.Host == "10.253.0.1"
                        & httpcontext.RequestUri.AbsolutePath.StartsWith("/apis/coordination.k8s.io"))
                        {
                            return false;
                        }

                        return true;
                    };
                });

            builder.AddAspNetCoreInstrumentation()
                .AddGrpcCoreInstrumentation()
                .AddNpgsql()
                .AddQuartzInstrumentation()
                .AddOtlpExporter(
                    options =>
                    {
                        options.ExportProcessorType         = ExportProcessorType.Batch;
                        options.BatchExportProcessorOptions = new BatchExportProcessorOptions<Activity>();
                        options.Endpoint                    = new Uri(NeonHelper.NeonKubeOtelCollectorUri);
                        options.Protocol                    = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
                    });

            return true;
        }

        private async Task WatchClusterInfoAsync()
        {
            await SyncContext.Clear;

            _ = K8s.WatchAsync<V1ConfigMap>(async (@event) =>
            {
                await SyncContext.Clear;

                ClusterInfo = TypeSafeConfigMap<ClusterInfo>.From(@event.Value).Config;

                Logger.LogInformationEx("Updated cluster info");
            },
            KubeNamespace.NeonStatus,
            fieldSelector: $"metadata.name={KubeConfigMapName.ClusterInfo}");
        }

        private async Task WatchRootUserAsync()
        {
            await SyncContext.Clear;

            _ = K8s.WatchAsync<V1Secret>(async (@event) =>
            {
                await SyncContext.Clear;

                var rootUser   = NeonHelper.YamlDeserialize<GlauthUser>(Encoding.UTF8.GetString(@event.Value.Data["root"]));
                var authString = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{rootUser.Name}:{rootUser.Password}"));

                harborHttpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authString);

                Logger.LogInformationEx("Updated Harbor Client");
            },
            KubeNamespace.NeonSystem,
            fieldSelector: $"metadata.name=glauth-users");
        }
    }
}