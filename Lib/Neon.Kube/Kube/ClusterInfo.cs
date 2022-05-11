﻿//-----------------------------------------------------------------------------
// FILE:	    ClusterInfo.cs
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
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Runtime.Serialization;

using Newtonsoft.Json;

using Neon.Common;

namespace Neon.Kube
{
    /// <summary>
    /// Holds details about a cluster's state.
    /// </summary>
    public class ClusterInfo
    {
        /// <summary>
        /// Default constructor used for deserializion.
        /// </summary>
        public ClusterInfo()
        {
        }

        /// <summary>
        /// Used to construct an instance, picking up common properties from a
        /// cluster definition.
        /// </summary>
        /// <param name="clusterDefinition">Specifies the cluster definition.</param>
        public ClusterInfo(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            Name            = clusterDefinition.Name;
            Description     = clusterDefinition.Description;
            Datacenter      = clusterDefinition.Datacenter;
            Domain          = clusterDefinition.Domain;
            PublicAddresses = clusterDefinition.PublicAddresses;
        }

        /// <summary>
        /// The neonKUBE version of the cluster.  This is formatted as a <see cref="SemanticVersion"/>.
        /// </summary>
        [JsonProperty(PropertyName = "ClusterVersion", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(KubeVersions.NeonKube)]
        public string ClusterVersion { get; set; } = KubeVersions.NeonKube;

        /// <summary>
        /// Indicates whether the cluster is currently locked, unlocked, or whether the
        /// lock state is currentlt unknown (when <c>null</c>.
        /// </summary>
        [JsonProperty(PropertyName = "IsLocked", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public bool? IsLocked { get; set; } = null;

        /// <summary>
        /// Describes the overall state of a cluster.
        /// </summary>
        [JsonProperty(PropertyName = "State", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(ClusterState.Unknown)]
        public ClusterState State { get; set; } = ClusterState.Unknown;

        /// <summary>
        /// Identifies the cluster by name as specified by <see cref="ClusterDefinition.Name"/> in the cluster definition.
        /// </summary>
        [JsonProperty(PropertyName = "Name", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue("")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Optionally describes the cluster for humans.
        /// </summary>
        [JsonProperty(PropertyName = "Description", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Description { get; set; } = null;

        /// <summary>
        /// Identifies the cloud or other hosting platform.
        /// definition. 
        /// </summary>
        [JsonProperty(PropertyName = "HostingEnvironment", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(HostingEnvironment.Unknown)]
        public HostingEnvironment HostingEnvironment { get; set; } = HostingEnvironment.Unknown;

        /// <summary>
        /// Indicates how the cluster is being used as specified by <see cref="ClusterDefinition.Environment"/>.
        /// definition. 
        /// </summary>
        [JsonProperty(PropertyName = "Environment", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue("")]
        public string Environment { get; set; } = string.Empty;

        /// <summary>
        /// Identifies where the cluster is hosted as specified by <see cref="ClusterDefinition.Datacenter"/> in the cluster
        /// definition.  That property defaults to the empty string for on-premise clusters and the the region for cloud
        /// based clusters.
        /// </summary>
        [JsonProperty(PropertyName = "Datacenter", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue("")]
        public string Datacenter { get; set; } = string.Empty;

        /// <summary>
        /// Identifies the DNS domain assigned to the cluster when it was provisioned.
        /// </summary>
        [JsonProperty(PropertyName = "Domain", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue("")]
        public string Domain { get; set; } = string.Empty;

        /// <summary>
        /// <para>
        /// Lists the IP addresses that can be used to communicate with the cluster.
        /// </para>
        /// <para>
        /// For cloud deployed clusters, this will be configured by default with the public IP
        /// address assigned to the cluster load balancer.  For on-premis clusters, this will
        /// be set to the IP addresses of the master nodes by default.
        /// </para>
        /// <para>
        /// Users may also customize this by setting IP addresses in the cluster definition.
        /// This is often done for clusters behind a router mapping the public IP address
        /// to the LAN address for cluster nodes.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "PublicAddresses", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<string> PublicAddresses { get; set; } = null;

        /// <summary>
        /// Maps node names to their provisioning states.
        /// </summary>
        [JsonProperty(PropertyName = "Nodes", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public Dictionary<string, ClusterNodeState> Nodes { get; set; } = new Dictionary<string, ClusterNodeState>(StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Human readable string that summarizes the cluster state.
        /// </summary>
        [JsonProperty(PropertyName = "Summary", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Summary { get; set; } = null;

        /// <summary>
        /// Describes which optional components have been deployed to the cluster.
        /// </summary>
        [JsonProperty(PropertyName = "OptionalComponents", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public ClusterOptionalComponents OptionalComponents { get; set; } = new ClusterOptionalComponents();
    }
}
