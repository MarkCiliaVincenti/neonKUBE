﻿//-----------------------------------------------------------------------------
// FILE:        Test_EndToEnd.cs
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
    public partial class Test_EndToEnd : IClassFixture<CadenceFixture>, IDisposable
    {
        const int maxWaitSeconds = 5;

        private static readonly TimeSpan allowedVariation = TimeSpan.FromMilliseconds(1000);
        private static readonly TimeSpan workflowTimeout = TimeSpan.FromSeconds(20);

        CadenceFixture  fixture;
        CadenceClient   client;
        HttpClient      proxyClient;

        public Test_EndToEnd(CadenceFixture fixture)
        {
            var settings = new CadenceSettings()
            {
                DefaultDomain   = CadenceFixture.DefaultDomain,
                DefaultTaskList = CadenceFixture.DefaultTaskList,
                CreateDomain    = true,
                Debug           = true,

                //--------------------------------
                // $debug(jeff.lill): DELETE THIS!
                Emulate                = false,
                DebugPrelaunched       = false,
                DebugDisableHandshakes = false,
                DebugDisableHeartbeats = true,
                //--------------------------------
            };

            if (fixture.Start(settings, keepConnection: true) == TestFixtureStatus.Started)
            {
                this.fixture     = fixture;
                this.client      = fixture.Connection;
                this.proxyClient = new HttpClient() { BaseAddress = client.ProxyUri };

                // Auto register the test workflow and activity implementations.

                client.RegisterAssembly(Assembly.GetExecutingAssembly()).Wait();

                // Start the worker.

                client.StartWorkerAsync().Wait();
            }
            else
            {
                this.fixture     = fixture;
                this.client      = fixture.Connection;
                this.proxyClient = new HttpClient() { BaseAddress = client.ProxyUri };
            }
        }

        public void Dispose()
        {
            if (proxyClient != null)
            {
                proxyClient.Dispose();
                proxyClient = null;
            }
        }
    }
}
