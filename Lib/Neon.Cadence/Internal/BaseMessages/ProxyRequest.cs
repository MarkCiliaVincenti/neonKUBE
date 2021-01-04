﻿//-----------------------------------------------------------------------------
// FILE:	    ProxyRequest.cs
// CONTRIBUTOR: Jeff Lill
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
using System.ComponentModel;

using Neon.Cadence;
using Neon.Common;

namespace Neon.Cadence.Internal
{
    /// <summary>
    /// Base class for all proxy requests.
    /// </summary>
    [InternalProxyMessage(InternalMessageTypes.Unspecified)]
    internal class ProxyRequest : ProxyMessage
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public ProxyRequest()
        {
        }

        /// <summary>
        /// Uniquely identifies this request.
        /// </summary>
        public long RequestId
        {
            get => GetLongProperty(PropertyNames.RequestId);
            set => SetLongProperty(PropertyNames.RequestId, value);
        }

        /// <summary>
        /// Optionally indicates that the operation may be cancelled by the 
        /// workflow application.
        /// </summary>
        public bool IsCancellable
        {
            get => GetBoolProperty(PropertyNames.IsCancellable);
            set => SetBoolProperty(PropertyNames.IsCancellable, value);
        }

        /// <summary>
        /// Derived request types must return the type of the expected
        /// <see cref="ProxyReply"/> message.
        /// </summary>
        public virtual InternalMessageTypes ReplyType => InternalMessageTypes.Unspecified;

        /// <inheritdoc/>
        internal override ProxyMessage Clone()
        {
            var clone = new ProxyRequest();

            CopyTo(clone);

            return clone;
        }

        /// <inheritdoc/>
        protected override void CopyTo(ProxyMessage target)
        {
            base.CopyTo(target);

            var typedTarget = (ProxyRequest)target;

            typedTarget.RequestId     = this.RequestId;
            typedTarget.IsCancellable = this.IsCancellable;
        }
    }
}
