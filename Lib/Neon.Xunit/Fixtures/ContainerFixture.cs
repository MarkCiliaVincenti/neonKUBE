﻿//-----------------------------------------------------------------------------
// FILE:	    ContainerFixture.cs
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
using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Retry;

namespace Neon.Xunit
{
    /// <summary>
    /// Used to run a Docker container on the current machine as a test 
    /// fixture while tests are being performed and then deletes the
    /// container when the fixture is disposed.
    /// </summary>
    /// <remarks>
    /// <note>
    /// <para>
    /// <b>IMPORTANT:</b> The Neon <see cref="TestFixture"/> implementation <b>DOES NOT</b>
    /// support parallel test execution because fixtures may impact global machine state
    /// like starting a Couchbase Docker container, modifying the local DNS <b>hosts</b>
    /// file or managing a Docker Swarm or cluster.
    /// </para>
    /// <para>
    /// You should explicitly disable parallel execution in all test assemblies that
    /// rely on test fixtures by adding a C# file called <c>AssemblyInfo.cs</c> with:
    /// </para>
    /// <code language="csharp">
    /// [assembly: CollectionBehavior(DisableTestParallelization = true, MaxParallelThreads = 1)]
    /// </code>
    /// </note>
    /// </remarks>
    /// <threadsafety instance="true"/>
    public class ContainerFixture : TestFixture
    {
        // Arguments required to restart the container.

        private string                  name;
        private string                  image;
        private string[]                dockerArgs;
        private IEnumerable<string>     containerArgs;
        private IEnumerable<string>     env;
        private bool                    noRemove;
        private bool                    keepOpen;

        /// <summary>
        /// Constructor.
        /// </summary>
        public ContainerFixture()
        {
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~ContainerFixture()
        {
            Dispose(false);
        }

        /// <summary>
        /// Returns the running container's name <c>null</c> if the container
        /// has not been started.
        /// </summary>
        public string ContainerName { get; private set; }

        /// <summary>
        /// Returns the running container's short ID or <c>null</c> if the container
        /// has not been started.
        /// </summary>
        public string ContainerId { get; private set; }

        /// <summary>
        /// <para>
        /// Starts the container.
        /// </para>
        /// <note>
        /// You'll need to call <see cref="StartAsComposed(string, string, string[], IEnumerable{string}, IEnumerable{string}, bool, bool)"/>
        /// instead when this fixture is being added to a <see cref="ComposedFixture"/>.
        /// </note>
        /// </summary>
        /// <param name="name">Specifies the container name.</param>
        /// <param name="image">Specifies the container Docker image.</param>
        /// <param name="dockerArgs">Optional arguments to be passed to the <b>docker run ...</b> command.</param>
        /// <param name="containerArgs">Optional arguments to be passed to the container.</param>
        /// <param name="env">Optional environment variables to be passed to the Couchbase container, formatted as <b>NAME=VALUE</b> or just <b>NAME</b>.</param>
        /// <param name="noRemove">Optionally indicates that the <b>--rm</b> option should not be included when creating the container.</param>
        /// <param name="keepOpen">
        /// Optionally indicates that the container should continue to run after the fixture is disposed.  
        /// This implies <see cref="noRemove"/><c>=true</c> and defaults to <c>true</c>.
        /// </param>
        /// <returns>
        /// <see cref="TestFixtureStatus.Started"/> if the fixture wasn't previously started and
        /// this method call started it or <see cref="TestFixtureStatus.AlreadyRunning"/> if the 
        /// fixture was already running.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this is not called from  within the <see cref="Action"/> method 
        /// passed <see cref="ITestFixture.Start(Action)"/>
        /// </exception>
        /// <remarks>
        /// <note>
        /// You must specify a valid container <paramref name="name"/>so that the fixure
        /// can remove any existing container with the same name before starting the new container.
        /// This is very useful during test debugging when the test might be interrupted during 
        /// debugging before ensuring that the container is stopped.
        /// </note>
        /// </remarks>
        public TestFixtureStatus Start(string name, string image, string[] dockerArgs = null, IEnumerable<string> containerArgs = null, IEnumerable<string> env = null, bool noRemove = false, bool keepOpen = false)
        {
            return base.Start(
                () =>
                {
                    StartAsComposed(name, image, dockerArgs, containerArgs, env, noRemove, keepOpen);
                });
        }

        /// <summary>
        /// Used to start the fixture within a <see cref="ComposedFixture"/>.
        /// </summary>
        /// <param name="name">Specifies the container name.</param>
        /// <param name="image">Specifies the container Docker image.</param>
        /// <param name="dockerArgs">Optional arguments to be passed to the <b>docker run ...</b> command.</param>
        /// <param name="containerArgs">Optional arguments to be passed to the container.</param>
        /// <param name="env">Optional environment variables to be passed to the Couchbase container, formatted as <b>NAME=VALUE</b> or just <b>NAME</b>.</param>
        /// <param name="noRemove">Optionally indicates that the <b>--rm</b> option should not be included when creating the container.</param>
        /// <param name="keepOpen">
        /// Optionally indicates that the container should continue to run after the fixture is disposed.  
        /// This implies <see cref="noRemove"/><c>=true</c> and defaults to <c>true</c>.
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if this is not called from  within the <see cref="Action"/> method 
        /// passed <see cref="ITestFixture.Start(Action)"/>
        /// </exception>
        /// <remarks>
        /// <note>
        /// You must specify a valid container <paramref name="name"/>so that the fixure
        /// can remove any existing container with the same name before starting the new container.
        /// This is very useful during test debugging when the test might be interrupted during 
        /// debugging before ensuring that the container is stopped.
        /// </note>
        /// </remarks>
        public void StartAsComposed(string name, string image, string[] dockerArgs = null, IEnumerable<string> containerArgs = null, IEnumerable<string> env = null, bool noRemove = false, bool keepOpen = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(image));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));

            base.CheckWithinAction();

            if (IsRunning)
            {
                return;
            }

            if (keepOpen)
            {
                noRemove = true;
            }

            this.name          = name;
            this.image         = image;
            this.dockerArgs    = dockerArgs;
            this.containerArgs = containerArgs;
            this.env           = env;
            this.noRemove      = noRemove;
            this.keepOpen      = keepOpen;

            StartContainer();
        }

        /// <summary>
        /// Releases all associated resources.
        /// </summary>
        /// <param name="disposing">Pass <c>true</c> if we're disposing, <c>false</c> if we're finalizing.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!base.IsDisposed)
                {
                    Reset();
                }

                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// Starts the container using the instance fields.
        /// </summary>
        private void StartContainer()
        {
            // Handle the special case where an earlier run of this container was
            // not stopped because the developer was debugging and interrupted the
            // the unit tests before the fixture was disposed or a container with
            // the same name is already running for some other reason.
            //
            // We're going to look for a existing container with the same name
            // and remove it if its ID doesn't match the current container.

            var args   = new string[] { "ps", "-a", "--filter", $"name={name}", "--format", "{{.ID}}" };
            var result = NeonHelper.ExecuteCapture($"docker", args);

            if (result.ExitCode == 0)
            {
                var existingId = result.OutputText.Trim();

                if (!string.IsNullOrEmpty(existingId))
                {
                    NeonHelper.Execute("docker", new object[] { "rm", "--force", "-v", existingId });
                }
            }

            // Pull and then start the container.  Note that we're going to 
            // retry the pull a few times to handle transitent issues. 

            var argsString = NeonHelper.NormalizeExecArgs("pull", image);
            var pullRetry  = new LinearRetryPolicy(TransientDetector.Always, maxAttempts: 5, retryInterval: TimeSpan.FromSeconds(1));

            pullRetry.InvokeAsync(
                async () =>
                {
                    result = NeonHelper.ExecuteCapture($"docker", argsString);

                    if (result.ExitCode != 0)
                    {
                        throw new Exception($"Cannot pull container [{image}] - [exitcode={result.ExitCode}]: {result.ErrorText}");
                    }

                    await Task.CompletedTask;

                }).Wait();

            var dockerArgs = new List<string>();

            if (this.dockerArgs != null)
            {
                foreach (var arg in this.dockerArgs)
                {
                    dockerArgs.Add(arg);
                }
            }

            dockerArgs.Add("--detach");
#if TODO
            // This requires Docker experimental features.

            dockerArgs.Add("--platform");
            dockerArgs.Add("linux");
#endif
            if (!string.IsNullOrEmpty(name))
            {
                dockerArgs.Add("--name");
                dockerArgs.Add(name);
            }

            if (env != null)
            {
                foreach (var variable in env)
                {
                    dockerArgs.Add("--env");
                    dockerArgs.Add(variable);
                }
            }

            if (!noRemove)
            {
                dockerArgs.Add("--rm");
            }

            argsString = NeonHelper.NormalizeExecArgs("run", dockerArgs, image, null);
            result     = NeonHelper.ExecuteCapture($"docker", argsString);

            if (result.ExitCode != 0)
            {
                throw new Exception($"Cannot launch container [{image}] - [exitcode={result.ExitCode}]: {result.ErrorText}");
            }
            else
            {
                ContainerName = name;
                ContainerId   = result.OutputText.Trim().Substring(0, 12);
            }
        }

        /// <inheritdoc/>
        public override void Reset()
        {
            // Remove the container if it's running and [keepOpen] is disabled.

            if (ContainerId != null && !keepOpen)
            {
                try
                {
                    var args   = new string[] { "rm", "--force", "-v", ContainerId };
                    var result = NeonHelper.ExecuteCapture($"docker", args);

                    if (result.ExitCode != 0)
                    {
                        throw new Exception($"Cannot remove container [{ContainerId}].");
                    }
                }
                finally
                {
                    ContainerId = null;
                }
            }
        }

        /// <summary>
        /// Restarts the container.  This is a handy way to deploy a fresh container with the
        /// same properties while running unit tests.
        /// </summary>
        public void Restart()
        {
            Reset();
            StartContainer();
        }
    }
}
