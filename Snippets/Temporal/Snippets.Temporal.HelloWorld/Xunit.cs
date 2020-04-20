﻿using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Reflection;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Temporal;
using Neon.Xunit;
using Neon.Xunit.Temporal;

using Xunit;

namespace MyTests
{
    [WorkflowInterface(TaskList = "test-tasks")]
    public interface IHelloWorkflow : IWorkflow
    {
        [WorkflowMethod]
        Task<string> HelloAsync(string name);
    }

    [Workflow(AutoRegister = true)]
    public class HelloWorkflow : WorkflowBase, IHelloWorkflow
    {
        public async Task<string> HelloAsync(string name)
        {
            return await Task.FromResult($"Hello {name}!");
        }
    }

    public class TemporalTests : IClassFixture<TemporalFixture>
    {
        private TemporalFixture     fixture;
        private TemporalClient      client;

        public TemporalTests(TemporalFixture fixture)
        {
            var settings = new TemporalSettings()
            {
                DefaulNamespace = "test-domain",
                LogLevel      = LogLevel.Info,
                CreateNamespace  = true            // <-- this ensures that the default domain exists
            };

            // This starts/restarts the [nforgeio/temporal-dev] container for the first test
            // run in this class.  Subsequent tests run from the class will use the existing
            // container instance, saving time by not having to wait for Temporal and Cassandra
            // to spin up and be ready for business.
            //
            // The [keepOpen=true] parameter tells the fixture to let the container continue running
            // after all of the tests have completed.  This is useful for examining workflow histories
            // via the Temporal UX after the tests have completed.  You can view the Temporal portal at
            //
            //      http://localhost:8088
            //
            // You can pass [keepOpen=false] to have the fixture remove the container after the
            // test run if you wish.

            if (fixture.Start(settings, keepConnection: true, keepOpen: true) == TestFixtureStatus.Started)
            {
                this.fixture = fixture;
                this.client  = fixture.Client;

                // Register the test workflow and activity implementations
                // from this assembly and start the worker.

                client.RegisterAssemblyAsync(Assembly.GetExecutingAssembly()).Wait();
                client.StartWorkerAsync("test-tasks").Wait();
            }
            else
            {
                this.fixture = fixture;
                this.client  = fixture.Client;
            }
        }

        [Fact]
        public async Task HelloWorld()
        {
            var stub   = client.NewWorkflowStub<IHelloWorkflow>();
            var result = await stub.HelloAsync("Jeff");

            Assert.Equal("Hello Jeff!", result);
        }
    }
}
