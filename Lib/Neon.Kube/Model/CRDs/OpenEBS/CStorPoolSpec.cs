﻿//-----------------------------------------------------------------------------
// FILE:	    V1CStorPoolSpec.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
using System.Text;
using Couchbase.Configuration.Client;
using k8s;
using k8s.Models;

using Newtonsoft.Json;
using Microsoft.Rest;

namespace Neon.Kube
{
    /// <summary>
    /// OpenEBS cStore pool specification.
    /// </summary>
    public class V1CStorPoolSpec
    {
        /// <summary>
        /// Initializes a new instance of the V1CStorPoolSpec class.
        /// </summary>
        public V1CStorPoolSpec()
        {
        }

        /// <summary>
        /// 
        /// </summary>
        [JsonProperty(PropertyName = "nodeSelector", Required = Required.Always)]
        public Dictionary<string, string> NodeSelector { get; set; }

        /// <summary>
        /// 
        /// </summary>
        [JsonProperty(PropertyName = "dataRaidGroups", Required = Required.Always)]
        public List<V1CStorDataRaidGroup> DataRaidGroups { get; set; }

        /// <summary>
        /// 
        /// </summary>
        [JsonProperty(PropertyName = "poolConfig", Required = Required.Always)]
        public V1CStorPoolConfig PoolConfig { get; set; }

    }
}
