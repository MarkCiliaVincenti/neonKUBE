﻿//-----------------------------------------------------------------------------
// FILE:	    DomainRegisterRequest.cs
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

// $todo(jeff.lill):
//
// There are several more parameters we could specify but these
// don't seem critical at this point.

namespace Neon.Cadence.Internal
{
    /// <summary>
    /// <b>library --> proxy:</b> Requests that the proxy register a Cadence domain.
    /// </summary>
    [ProxyMessage(MessageTypes.DomainRegisterRequest)]
    internal class DomainRegisterRequest : ProxyRequest
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public DomainRegisterRequest()
        {
            Type = MessageTypes.DomainRegisterRequest;
        }

        /// <inheritdoc/>
        public override MessageTypes ReplyType => MessageTypes.DomainRegisterReply;

        /// <summary>
        /// Name for the new domain.
        /// </summary>
        public string Name
        {
            get => GetStringProperty("Name");
            set => SetStringProperty("Name", value);
        }

        /// <summary>
        /// Human readable description for the domain.
        /// </summary>
        public string Description
        {
            get => GetStringProperty("Description");
            set => SetStringProperty("Description", value);
        }

        /// <summary>
        /// Owner email address.
        /// </summary>
        public string OwnerEmail
        {
            get => GetStringProperty("OwnerEmail");
            set => SetStringProperty("OwnerEmail", value);
        }

        /// <summary>
        /// Enable metrics.
        /// </summary>
        public bool EmitMetrics
        {
            get => GetBoolProperty("EmitMetrics");
            set => SetBoolProperty("EmitMetrics", value);
        }

        /// <summary>
        /// The complete workflow history retention period in days.
        /// </summary>
        public int RetentionDays
        {
            get => GetIntProperty("RetentionDays");
            set => SetIntProperty("RetentionDays", value);
        }

        /// <inheritdoc/>
        internal override ProxyMessage Clone()
        {
            var clone = new DomainRegisterRequest();

            CopyTo(clone);

            return clone;
        }

        /// <inheritdoc/>
        protected override void CopyTo(ProxyMessage target)
        {
            base.CopyTo(target);

            var typedTarget = (DomainRegisterRequest)target;

            typedTarget.Name          = this.Name;
            typedTarget.Description   = this.Description;
            typedTarget.OwnerEmail    = this.OwnerEmail;
            typedTarget.EmitMetrics   = this.EmitMetrics;
            typedTarget.RetentionDays = this.RetentionDays;
        }
    }
}
