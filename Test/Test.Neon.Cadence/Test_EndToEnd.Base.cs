﻿//-----------------------------------------------------------------------------
// FILE:        Test_EndToEnd.Activity.cs
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

// Comment this to enable slow tests.
#define SKIP_SLOW_TESTS

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;
using Neon.Cryptography;
using Neon.Data;
using Neon.IO;
using Neon.Xunit;
using Neon.Xunit.Cadence;

using Newtonsoft.Json;
using Xunit;

namespace TestCadence
{
    public partial class Test_EndToEnd
    {
#if SKIP_SLOW_TESTS
        [Fact(Skip = "Slow: Enable for full tests")]
#else
        [Fact]
#endif
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Ping()
        {
            // Verify that Ping works and optionally measure simple transaction throughput.

            await client.PingAsync();

            var stopwatch  = new Stopwatch();
            var iterations = 5000;

            stopwatch.Start();

            for (int i = 0; i < iterations; i++)
            {
                await client.PingAsync();
            }

            stopwatch.Stop();

            var tps = iterations * (1.0 / stopwatch.Elapsed.TotalSeconds);

            Console.WriteLine($"Transactions/sec: {tps}");
        }

#if SKIP_SLOW_TESTS
        [Fact(Skip = "Slow: Enable for full tests")]
#else
        [Fact]
#endif
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void PingAttack()
        {
            // Measure througput with 4 threads hammering the proxy with pings.

            var syncLock   = new object();
            var totalTps   = 0.0;
            var threads    = new Thread[4];
            var iterations = 5000;

            for (int i = 0; i < threads.Length; i++)
            {
                threads[i] = new Thread(
                    new ThreadStart(
                        () =>
                        {
                            var stopwatch = new Stopwatch();

                            stopwatch.Start();

                            for (int j = 0; j < iterations; j++)
                            {
                                client.PingAsync().Wait();
                            }

                            stopwatch.Stop();

                            var tps = iterations * (1.0 / stopwatch.Elapsed.TotalSeconds);

                            lock (syncLock)
                            {
                                totalTps += tps;
                            }
                        }));

                threads[i].Start();
            }

            foreach (var thread in threads)
            {
                thread.Join();
            }

            Console.WriteLine($"Transactions/sec: {totalTps}");
            Console.WriteLine($"Latency (average): {1.0 / totalTps}");
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Domain()
        {
            // Exercise the Cadence domain operations.

            //-----------------------------------------------------------------
            // RegisterDomain:

            await Assert.ThrowsAsync<ArgumentNullException>(async () => await client.RegisterDomainAsync(name: null));
            await Assert.ThrowsAsync<ArgumentException>(async () => await client.RegisterDomainAsync(name: "domain-0", retentionDays: -1));

            await client.RegisterDomainAsync("domain-0", "this is domain-0", "jeff@lilltek.com", retentionDays: 14);
            await Assert.ThrowsAsync<CadenceDomainAlreadyExistsException>(async () => await client.RegisterDomainAsync(name: "domain-0"));

            //-----------------------------------------------------------------
            // DescribeDomain:

            var domainDescribeReply = await client.DescribeDomainAsync("domain-0");

            Assert.False(domainDescribeReply.Configuration.EmitMetrics);
            Assert.Equal(14, domainDescribeReply.Configuration.RetentionDays);
            Assert.Equal("domain-0", domainDescribeReply.DomainInfo.Name);
            Assert.Equal("this is domain-0", domainDescribeReply.DomainInfo.Description);
            Assert.Equal("jeff@lilltek.com", domainDescribeReply.DomainInfo.OwnerEmail);
            Assert.Equal(DomainStatus.Registered, domainDescribeReply.DomainInfo.Status);

            await Assert.ThrowsAsync<CadenceEntityNotExistsException>(async () => await client.DescribeDomainAsync("does-not-exist"));

            //-----------------------------------------------------------------
            // UpdateDomain:

            var updateDomainRequest = new UpdateDomainRequest();

            updateDomainRequest.Options.EmitMetrics    = true;
            updateDomainRequest.Options.RetentionDays  = 77;
            updateDomainRequest.DomainInfo.OwnerEmail  = "foo@bar.com";
            updateDomainRequest.DomainInfo.Description = "new description";

            await client.UpdateDomainAsync("domain-0", updateDomainRequest);

            domainDescribeReply = await client.DescribeDomainAsync("domain-0");

            Assert.True(domainDescribeReply.Configuration.EmitMetrics);
            Assert.Equal(77, domainDescribeReply.Configuration.RetentionDays);
            Assert.Equal("domain-0", domainDescribeReply.DomainInfo.Name);
            Assert.Equal("new description", domainDescribeReply.DomainInfo.Description);
            Assert.Equal("foo@bar.com", domainDescribeReply.DomainInfo.OwnerEmail);
            Assert.Equal(DomainStatus.Registered, domainDescribeReply.DomainInfo.Status);

            await Assert.ThrowsAsync<CadenceEntityNotExistsException>(async () => await client.UpdateDomainAsync("does-not-exist", updateDomainRequest));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Worker()
        {
            await client.RegisterDomainAsync("test-domain", ignoreDuplicates: true);

            // Verify that creating workers with the same attributes actually
            // return the pre-existing instance with an incremented reference
            // count.

            var activityWorker1 = await client.StartWorkerAsync("tasks1", new WorkerOptions() { DisableWorkflowWorker = true });

            Assert.Equal(1, activityWorker1.RefCount);

            var activityWorker2 = await client.StartWorkerAsync("tasks1", new WorkerOptions() { DisableWorkflowWorker = true });

            Assert.Same(activityWorker1, activityWorker2);
            Assert.Equal(2, activityWorker2.RefCount);

            var workflowWorker1 = await client.StartWorkerAsync("tasks1", new WorkerOptions() { DisableActivityWorker = true });

            Assert.Equal(1, workflowWorker1.RefCount);

            var workflowWorker2 = await client.StartWorkerAsync("tasks1", new WorkerOptions() { DisableActivityWorker = true });

            Assert.Same(workflowWorker1, workflowWorker2);
            Assert.Equal(2, workflowWorker2.RefCount);

            // Verify the dispose/refcount behavior.

            activityWorker2.Dispose();
            Assert.False(activityWorker2.IsDisposed);
            Assert.Equal(1, activityWorker2.RefCount);

            activityWorker2.Dispose();
            Assert.True(activityWorker2.IsDisposed);
            Assert.Equal(0, activityWorker2.RefCount);

            workflowWorker2.Dispose();
            Assert.False(workflowWorker2.IsDisposed);
            Assert.Equal(1, workflowWorker2.RefCount);

            workflowWorker2.Dispose();
            Assert.True(workflowWorker2.IsDisposed);
            Assert.Equal(0, workflowWorker2.RefCount);

            // Verify that we're not allowed to restart workers.

            await Assert.ThrowsAsync<InvalidOperationException>(async () => await client.StartWorkerAsync("tasks1", new WorkerOptions() { DisableWorkflowWorker = true }));
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await client.StartWorkerAsync("tasks1", new WorkerOptions() { DisableActivityWorker = true }));
        }
    }
}
