﻿//-----------------------------------------------------------------------------
// FILE:	    GrpcGetWindowsCapabilitiesReply.cs
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
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Net;

using ProtoBuf.Grpc;

namespace Neon.Kube.GrpcProto.Desktop
{
    /// <summary>
    /// Describes the current Windows capabilities.
    /// </summary>
    [DataContract]
    public class GrpcGetWindowsCapabilitiesReply
    {
        /// <summary>
        /// Error constructor.
        /// </summary>
        /// <param name="e">The exception.</param>
        public GrpcGetWindowsCapabilitiesReply(Exception e)
        {
            this.Error = new GrpcError(e);
        }

        /// <summary>
        /// Reply constructor.
        /// </summary>
        /// <param name="capabilities"></param>
        public GrpcGetWindowsCapabilitiesReply(Dictionary<string, GrpcWindowsFeatureStatus> capabilities)
        {
            this.Capabilities = capabilities;
        }

        /// <summary>
        /// Set to a non-null value when the request failed.
        /// </summary>
        [DataMember(Order = 1)]
        public GrpcError? Error { get; set; }

        /// <summary>
        /// A dictionary mapping Windows feature names to their current status.
        /// </summary>
        [DataMember(Order = 2)]
        public Dictionary<string, GrpcWindowsFeatureStatus>? Capabilities { get; set; }
    }

}
