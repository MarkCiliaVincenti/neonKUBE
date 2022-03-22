﻿//-----------------------------------------------------------------------------
// FILE:	    KubeMinioBuckets.cs
// CONTRIBUTOR: Jeff Lill
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
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.SSH;

using Renci.SshNet;

namespace Neon.Kube
{
    /// <summary>
    /// Defines the Minio bucket names used by neonKUBE applications.
    /// </summary>
    public static class KubeMinioBuckets
    {
        /// <summary>
        /// AlertManager bucket name.
        /// </summary>
        public const string AlertManager = "neon-harbor";

        /// <summary>
        /// Cortex bucket name.
        /// </summary>
        public const string Cortex = "neon-cortex";

        /// <summary>
        /// Cortex-ruler bucket name.
        /// </summary>
        public const string CortexRuler = "neon-cortex-ruler";

        /// <summary>
        /// Harbor bucket name.
        /// </summary>
        public const string Harbor = "neon-harbor";

        /// <summary>
        /// Loki bucket name.
        /// </summary>
        public const string Loki = "neon-loki";

        /// <summary>
        /// Tempo bucket name.
        /// </summary>
        public const string Tempo = "neon-temp";

        /// <summary>
        /// Returns the list of all internal neonKUBE Minio bucket names.
        /// </summary>
        public static readonly IReadOnlyList<string> All =
            new List<string>()
            {
                AlertManager,
                Cortex,
                CortexRuler,
                Harbor,
                Loki,
                Tempo
            }
            .AsReadOnly();
    }
}
