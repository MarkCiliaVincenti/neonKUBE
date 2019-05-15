﻿//-----------------------------------------------------------------------------
// FILE:	    DomainDescribeRequest.cs
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
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;

using Newtonsoft.Json;
using YamlDotNet.Serialization;

using Neon.Cadence;
using Neon.Common;

namespace Neon.Cadence.Internal
{
    /// <summary>
    /// <b>library --> proxy:</b> Requests the details for a named domain.
    /// </summary>
    [ProxyMessage(MessageTypes.DomainDescribeRequest)]
    internal class DomainDescribeRequest : ProxyRequest
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public DomainDescribeRequest()
        {
            Type = MessageTypes.DomainDescribeRequest;
        }

        /// <inheritdoc/>
        public override MessageTypes ReplyType => MessageTypes.DomainDescribeReply;

        /// <summary>
        /// The target Cadence domain name.
        /// </summary>
        public string Name
        {
            get => GetStringProperty("Name");
            set => SetStringProperty("Name", value);
        }

        /// <inheritdoc/>
        internal override ProxyMessage Clone()
        {
            var clone = new DomainDescribeRequest();

            CopyTo(clone);

            return clone;
        }

        /// <inheritdoc/>
        protected override void CopyTo(ProxyMessage target)
        {
            base.CopyTo(target);

            var typedTarget = (DomainDescribeRequest)target;

            typedTarget.Name = this.Name;
        }
    }
}
