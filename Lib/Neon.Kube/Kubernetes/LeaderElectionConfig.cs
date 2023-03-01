﻿//-----------------------------------------------------------------------------
// FILE:	    LeaderElectionConfig.cs
// CONTRIBUTOR: Auto-generated by [prebuilder] tool during pre-build event
// COPYRIGHT:	Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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

// WARNING: This file is automatically generated during the build.
//          Do not edit this manually.

#pragma warning disable CS1591  // Missing XML comment

using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using k8s;
using Prometheus;

using Neon.Common;
using Neon.Retry;

namespace Neon.Kube
{
    /// <summary>
    /// Configuration information for the <see cref="LeaderElector"/> class.
    /// </summary>
    public sealed class LeaderElectionConfig
    {
        private static readonly string[] LabelNames = { "namespace", "lease_name", "lease_duration_seconds" };

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="namespace">Identifies the namespace where the lease will be hosted.</param>
        /// <param name="leaseName">
        /// Specifies the lease name used to manage elections.  Note that this must be a valid
        /// Kubernetes resource name.
        /// </param>
        /// <param name="identity">
        /// <para>
        /// Specifies the unique identity of the entity using <see cref="LeaderElector"/> to run for
        /// the leadership role.  This will typically be passed as the host pod name.
        /// </para>
        /// <note>
        /// It's very important that the identifiers used by different leader candidates be unique.
        /// As mentioned above, the host pod name is a great option for most situations but this
        /// could also be a UUID or some other identity scheme which guarentees uniqueness.
        /// </note>
        /// </param>
        /// <param name="leaseDuration">
        /// Optionally specifies the interval a follower must wait before attempting to become
        /// the leader.  This defaults to <b>30 seconds</b>.
        /// </param>
        /// <param name="renewDeadline">
        /// Optionally specifies the interval when the leader will attempt to renew the lease before
        /// abandonding leadership.  This defaults to <b>15 seconds</b>.
        /// </param>
        /// <param name="retryPeriod">
        /// Optionally specifies the interval that <see cref="LeaderElector"/> instances should 
        /// wait before retrying any actions.  This defaults to <b>2 seconds</b>.
        /// </param>
        public LeaderElectionConfig(
            string          @namespace,
            string          leaseName,
            string          identity,
            TimeSpan        leaseDuration    = default,
            TimeSpan        renewDeadline    = default,
            TimeSpan        retryPeriod      = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(@namespace), nameof(@namespace));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(leaseName), nameof(leaseName));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(identity), nameof(identity));

            if (leaseDuration <= TimeSpan.Zero)
            {
                leaseDuration = TimeSpan.FromSeconds(30);
            }

            if (renewDeadline <= TimeSpan.Zero)
            {
                renewDeadline = TimeSpan.FromSeconds(15);
            }

            if (retryPeriod <= TimeSpan.Zero)
            {
                retryPeriod = TimeSpan.FromSeconds(2);
            }

            if (leaseDuration <= renewDeadline)
            {
                throw new ArgumentException($"[{nameof(leaseDuration)}={leaseDuration}] is not greater than [{nameof(renewDeadline)}={renewDeadline}].");
            }

            // Initialize the properties.

            this.Namespace     = @namespace;
            this.LeaseName     = leaseName;
            this.LeaseRef      = $"{@namespace}/{leaseName}";
            this.Identity      = identity;
            this.LeaseDuration = leaseDuration;
            this.RenewDeadline = renewDeadline;
            this.RetryPeriod   = retryPeriod;
            
            var labelValues = new string[] { @namespace, leaseName, leaseDuration.TotalSeconds.ToString() };

            this.PromotionCounter = Metrics
                .CreateCounter(
                    name: $"leader_election_promoted_total",
                    help: "Leader promotions",
                    labelNames: LabelNames)
                .WithLabels(labelValues);

            this.DemotionCounter = Metrics
                .CreateCounter(
                    name: $"leader_election_demoted_total", 
                    help: "Leader demotions", 
                    labelNames: LabelNames)
                .WithLabels(labelValues);

            this.NewLeaderCounter = Metrics
                .CreateCounter(
                    name: $"leader_election_new_leader_total", 
                    help: "Leadership changes", 
                    labelNames: LabelNames)
                .WithLabels(labelValues);
        }

        /// <summary>
        /// Returns the <see cref="IKubernetes"/> client to be used to communicate with the cluster.
        /// </summary>
        public IKubernetes K8s { get; private set; }

        /// <summary>
        /// Returns the Kubernetes namespace where the lease will reside.
        /// </summary>
        public string Namespace { get; private set; }

        /// <summary>
        /// Returns the lease name.
        /// </summary>
        public string LeaseName { get; private set; }

        /// <summary>
        /// Returns the lease reference formatted as: NAMESPACE/LEASE-aNAME.
        /// </summary>
        internal string LeaseRef { get; private set; }

        /// <summary>
        /// Returns the unique identity of the entity using the elector to running for the leadership
        /// role.  This is typically the hosting pod name.
        /// </summary>
        public string Identity { get; private set; }

        /// <summary>
        /// Returns the interval a follower must wait before attempting to become the leader.
        /// </summary>
        public TimeSpan LeaseDuration { get; private set; }

        /// <summary>
        /// Returns <see cref="LeaseDuration"/> rounded up to the nearest second
        /// and limited to the range supported by a 32-bit integer.
        /// </summary>
        internal int LeaseDurationSeconds => (int)Math.Min((long)Math.Ceiling(LeaseDuration.TotalSeconds), int.MaxValue);

        /// <summary>
        /// Returns the interval durning the leader will attempt to renew the lease before 
        /// abandonding leadership upon failures.
        /// </summary>
        public TimeSpan RenewDeadline { get; private set; }

        /// <summary>
        /// The interval that <see cref="LeaderElector"/> instances should wait before
        /// retrying any actions.
        /// </summary>
        public TimeSpan RetryPeriod { get; private set; }

        /// <summary>
        /// Returns the metrics counter to be incremented when the instance is 
        /// promoted to leader.  This may be <c>null</c>.
        /// </summary>
        internal Counter.Child PromotionCounter { get; private set; }

        /// <summary>
        /// Returns the metrics counter to be incremented when the instance is 
        /// demoted from leader.  This may be <c>null</c>.
        /// </summary>
        internal Counter.Child DemotionCounter { get; private set; }

        /// <summary>
        /// Returns the metrics counter to be incremented when a leadership
        /// change is detected.  This may be <c>null</c>.
        /// </summary>
        internal Counter.Child NewLeaderCounter { get; private set; }

        /// <summary>
        /// Returns the label values to be used when incrementing any of the
        /// metrics counters.  This may be empty or <c>null</c>.
        /// </summary>
        internal string[] CounterLabels { get; private set; }
    }
}
