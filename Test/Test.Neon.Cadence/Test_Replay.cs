﻿//-----------------------------------------------------------------------------
// FILE:        Test_Replay.cs
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
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
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
    // Implementation Notes:
    // ---------------------
    // This class implements replay tests on the essential workflow operations.
    // Maxim over at Uber told me how to cause a workflow to be replayed:
    //
    //      https://github.com/nforgeio/neonKUBE/issues/620
    //
    // This gist is that we need to disable sticky execution for the worker and
    // then the workflow will replay after sleeping.
    //
    // So we're going to implement a workflow that accepts a parameter that specifies
    // the operation to be tested and uses a static field to indicate whether the
    // workflow is being run for the first time or is replaying.  The test will
    // perform the specified operation on the first pass, trigger a replay, and
    // then ensure that the operation returned the same results on the second pass.

    public class Test_Replay : IClassFixture<CadenceFixture>, IDisposable
    {
        private const int maxWaitSeconds = 5;

        private static readonly TimeSpan allowedVariation = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan workflowTimeout  = TimeSpan.FromSeconds(20);

        private CadenceFixture  fixture;
        private CadenceClient   client;

        public Test_Replay(CadenceFixture fixture)
        {
            var settings = new CadenceSettings()
            {
                DefaultDomain   = CadenceFixture.DefaultDomain,
                DefaultTaskList = CadenceFixture.DefaultTaskList,
                CreateDomain    = true,
                Debug           = true,

                //--------------------------------
                // $debug(jeff.lill): DELETE THIS!
                DebugPrelaunched       = false,
                DebugDisableHandshakes = false,
                DebugDisableHeartbeats = true,
                //--------------------------------
            };

            if (fixture.Start(settings, keepConnection: true, keepOpen: CadenceTestHelper.KeepCadenceServerOpen) == TestFixtureStatus.Started)
            {
                this.fixture = fixture;
                this.client  = fixture.Client;

                // Auto register the test workflow and activity implementations.

                client.RegisterAssembly(Assembly.GetExecutingAssembly()).Wait();

                // Start the worker.

                client.StartWorkerAsync().Wait();
            }
            else
            {
                this.fixture = fixture;
                this.client  = fixture.Client;
            }
        }

        public void Dispose()
        {
        }

        //---------------------------------------------------------------------

        public enum ReplayTest
        {
            Nop,
            GetVersion,
            WorkflowExecution,
            MutableSideEffect,
            MutableSideEffectGeneric,
            SideEffect,
            SideEffectGeneric,
            NewGuid,
            NextRandomDouble,
            NextRandom,
            NextRandomMax,
            NextRandomMinMax,
            NextRandomBytes,
            GetLastCompletionResult,
            GetIsSetLastCompletionResult,
            ChildWorkflow,
            Activity,
            LocalActivity
        }

        public interface IWorkflowReplayHello : IWorkflow
        {
            [WorkflowMethod]
            Task<string> HelloAsync(string name);
        }

        [Workflow(AutoRegister = true)]
        public class WorkflowReplayHello : WorkflowBase, IWorkflowReplayHello
        {
            public async Task<string> HelloAsync(string name)
            {
                return await Task.FromResult($"Hello {name}!");
            }
        }

        public interface IReplayActivity : IActivity
        {
            [ActivityMethod]
            Task<string> RunAsync(string value);
        }

        [Activity(AutoRegister = true)]
        public class ReplayActivity : ActivityBase, IReplayActivity
        {
            [ActivityMethod]
            public async Task<string> RunAsync(string value)
            {
                return await Task.FromResult(value);
            }
        }

        public interface IWorkflowReplay : IWorkflow
        {
            [WorkflowMethod]
            Task<bool> RunAsync(ReplayTest test);
        }

        [Workflow(AutoRegister = true)]
        public class WorkflowReplay : WorkflowBase, IWorkflowReplay
        {
            private static bool     firstPass = true;
            private static object   originalValue;

            public new static void Reset()
            {
                firstPass = true;
            }

            /// <summary>
            /// Some workflow operations like <see cref="Workflow.SideEffectAsync{T}(Func{T})"/> don't
            /// actually indicate the end of a decision task by themselves.  We'll use this method in
            /// these cases to run a local activity which will do this.
            /// </summary>
            /// <returns>The tracking <see cref="Task"/>.</returns>
            private async Task DecisionAsync()
            {
                var stub = Workflow.NewActivityStub<IReplayActivity>();

                await stub.RunAsync("test");
            }

            public async Task<bool> RunAsync(ReplayTest test)
            {
                var success = false;

                if (test != ReplayTest.Nop)
                {
                    // This ensures that the workflow has some history so that when
                    // Cadence restarts the workflow it will be treated as a replay
                    // instead of an initial execution.

                    await DecisionAsync();
                }

                switch (test)
                {
                    case ReplayTest.Nop:

                        if (firstPass)
                        {
                            firstPass = false;

                            await Workflow.ForceReplayAsync();
                        }
                        else
                        {
                            // NOTE: 
                            //
                            // The other Cadence clients (GOLANG, Java,...) always report
                            // IsReplaying=FALSE when a workflow with no history is restarted,
                            // which is what's happening in this case.  This is a bit weird
                            // but is BY DESIGN but will probably be very rare in real life.
                            //
                            //      https://github.com/uber-go/cadence-client/issues/821

                            success = !Workflow.IsReplaying;
                        }
                        break;

                    case ReplayTest.GetVersion:

                        if (firstPass)
                        {
                            firstPass     = false;
                            originalValue = await Workflow.GetVersionAsync("change", Workflow.DefaultVersion, 1);

                            await Workflow.ForceReplayAsync();
                        }
                        else
                        {
                            success = originalValue.Equals(await Workflow.GetVersionAsync("change", Workflow.DefaultVersion, 1));
                            success = success && Workflow.IsReplaying;
                        }
                        break;

                    case ReplayTest.WorkflowExecution:

                        var helloStub = Workflow.Client.NewWorkflowStub<IWorkflowReplayHello>();

                        if (firstPass)
                        {
                            firstPass     = false;
                            originalValue = await helloStub.HelloAsync("Jeff");

                            await Workflow.ForceReplayAsync();
                        }
                        else
                        {
                            success = originalValue.Equals(await helloStub.HelloAsync("Jeff"));
                            success = success && Workflow.IsReplaying;
                        }
                        break;

                    case ReplayTest.MutableSideEffect:

                        if (firstPass)
                        {
                            firstPass     = false;
                            originalValue = await Workflow.MutableSideEffectAsync(typeof(string), "value", () => "my-value");

                            await Workflow.ForceReplayAsync();
                        }
                        else
                        {
                            success = originalValue.Equals(await Workflow.MutableSideEffectAsync(typeof(string), "value", () => "my-value"));
                            success = success && Workflow.IsReplaying;
                        }
                        break;

                    case ReplayTest.MutableSideEffectGeneric:

                        if (firstPass)
                        {
                            firstPass     = false;
                            originalValue = await Workflow.MutableSideEffectAsync<string>("value", () => "my-value");

                            await Workflow.ForceReplayAsync();
                        }
                        else
                        {
                            success = originalValue.Equals(await Workflow.MutableSideEffectAsync<string>("value", () => "my-value"));
                            success = success && Workflow.IsReplaying;
                        }
                        break;

                    case ReplayTest.SideEffect:

                        if (firstPass)
                        {
                            firstPass     = false;
                            originalValue = await Workflow.SideEffectAsync(typeof(string), () => "my-value");

                            await Workflow.ForceReplayAsync();
                        }
                        else
                        {
                            success = originalValue.Equals(await Workflow.SideEffectAsync(typeof(string), () => "my-value"));
                            success = success && Workflow.IsReplaying;
                        }
                        break;

                    case ReplayTest.SideEffectGeneric:

                        if (firstPass)
                        {
                            firstPass = false;
                            originalValue = await Workflow.SideEffectAsync<string>(() => "my-value");

                            await Workflow.ForceReplayAsync();
                        }
                        else
                        {
                            success = originalValue.Equals(await Workflow.SideEffectAsync<string>(() => "my-value"));
                            success = success && Workflow.IsReplaying;
                        }
                        break;

                    case ReplayTest.NewGuid:

                        if (firstPass)
                        {
                            firstPass     = false;
                            originalValue = await Workflow.NewGuidAsync();

                            await Workflow.ForceReplayAsync();
                        }
                        else
                        {
                            var v = await Workflow.NewGuidAsync();

                            success = originalValue.Equals(await Workflow.NewGuidAsync());
                            success = success && Workflow.IsReplaying;
                        }
                        break;

                    case ReplayTest.NextRandomDouble:
                    case ReplayTest.NextRandom:
                    case ReplayTest.NextRandomMax:
                    case ReplayTest.NextRandomMinMax:
                    case ReplayTest.NextRandomBytes:
                    case ReplayTest.GetLastCompletionResult:
                    case ReplayTest.GetIsSetLastCompletionResult:
                    case ReplayTest.ChildWorkflow:
                    case ReplayTest.Activity:
                    case ReplayTest.LocalActivity:
                    default:

                        success = false;
                        break;
                }

                return await Task.FromResult(success);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Nop()
        {
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            var result = await stub.RunAsync(ReplayTest.Nop);

            Assert.True(result);

            //Assert.True(await stub.RunAsync(ReplayTest.Nop));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task GetVersion()
        {
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            Assert.True(await stub.RunAsync(ReplayTest.GetVersion));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task WorkflowExecution()
        {
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            Assert.True(await stub.RunAsync(ReplayTest.WorkflowExecution));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task MutableSideEffect()
        {
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            Assert.True(await stub.RunAsync(ReplayTest.MutableSideEffect));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task MutableSideEffectGeneric()
        {
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            Assert.True(await stub.RunAsync(ReplayTest.MutableSideEffectGeneric));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task SideEffect()
        {
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            Assert.True(await stub.RunAsync(ReplayTest.SideEffect));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task SideEffectGeneric()
        {
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            Assert.True(await stub.RunAsync(ReplayTest.SideEffectGeneric));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task NewGuid()
        {
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            Assert.True(await stub.RunAsync(ReplayTest.NewGuid));
        }

#if TODO
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task NextRandomDouble()
        {
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            Assert.True(await stub.RunAsync(ReplayTest.NextRandomDouble));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task NextRandom()
        {
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            Assert.True(await stub.RunAsync(ReplayTest.NextRandom));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task NextRandomMax()
        {
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            Assert.True(await stub.RunAsync(ReplayTest.NextRandomMax));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task NextRandomMinMax()
        {
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            Assert.True(await stub.RunAsync(ReplayTest.NextRandomMinMax));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task NextRandomBytes()
        {
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            Assert.True(await stub.RunAsync(ReplayTest.NextRandomBytes));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task GetLastCompletionResult()
        {
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            Assert.True(await stub.RunAsync(ReplayTest.GetLastCompletionResult));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task GetIsSetLastCompletionResult()
        {
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            Assert.True(await stub.RunAsync(ReplayTest.GetIsSetLastCompletionResult));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task ChildWorkflow()
        {
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            Assert.True(await stub.RunAsync(ReplayTest.ChildWorkflow));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task Activity()
        {
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            Assert.True(await stub.RunAsync(ReplayTest.Activity));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public async Task LocalActivity()
        {
            WorkflowReplay.Reset();

            var stub = client.NewWorkflowStub<IWorkflowReplay>();

            Assert.True(await stub.RunAsync(ReplayTest.LocalActivity));
        }
#endif
    }
}
