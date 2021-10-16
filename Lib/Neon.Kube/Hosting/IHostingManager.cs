﻿//-----------------------------------------------------------------------------
// FILE:	    IHostingManager.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Collections;
using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Net;
using Neon.Retry;
using Neon.SSH;
using Neon.Time;

using Renci.SshNet;
using Renci.SshNet.Common;

namespace Neon.Kube
{
    /// <summary>
    /// Interface describing neonKUBE hosting manager implementions for different environments..
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="IHostingManager"/> implementations are used to provision the infrastructure required
    /// to deploy a neonKUBE cluster to various environments including on-premise via XenServer or
    /// Hyper-V hypervisors or to public clouds like AWS, Azure, and Google.  This infrastructure
    /// includes creating or initializing the servers as well as configuring networking in cloud
    /// environments.
    /// </para>
    /// <para>
    /// This interface also defines the mechanism for deprovisioning a cluster.
    /// </para>
    /// </remarks>
    public interface IHostingManager : IDisposable
    {
        /// <summary>
        /// Returns the hosting environment implemented by the manager.
        /// </summary>
        HostingEnvironment HostingEnvironment { get; }

        /// <summary>
        /// Verifies that a cluster is valid for the hosting manager, customizing 
        /// properties as required.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if any problems were detected.</exception>
        void Validate(ClusterDefinition clusterDefinition);

        /// <summary>
        /// Returns <c>true</c> if provisoning requires that the user have
        /// administrator privileges.
        /// </summary>
        bool RequiresAdminPrivileges { get; }

        /// <summary>
        /// Returns <c>true</c> if the hosting manager requires that the LAN be scanned
        /// for devices assigned IP addresses that may conflict with node addresses.  This
        /// is typically required only for clusters deployed on-premise because cloud
        /// clusters are typically provisioned to their own isolated network.
        /// </summary>
        bool RequiresNodeAddressCheck { get; }

        /// <summary>
        /// Specifies whether a cryptographically random node password should be generated.
        /// </summary>
        bool GenerateSecurePassword { get; }

        /// <summary>
        /// The maximum number of nodes that will execute provisioning steps in parallel.  This
        /// defaults to <b>5</b>.
        /// </summary>
        int MaxParallel { get; set; }

        /// <summary>
        /// Number of seconds to delay after specific operations (e.g. to allow services to stablize).
        /// This defaults to <b>0.0</b>.
        /// </summary>
        double WaitSeconds { get; set; }

        /// <summary>
        /// Adds the steps required to the setup controller passed that creates and initializes the
        /// cluster resources such as the virtual machines, networks, load balancers, network security groups, 
        /// public IP addresses.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        void AddProvisioningSteps(SetupController<NodeDefinition> controller);

        /// <summary>
        /// Adds any steps to be performed after the node has been otherwise prepared.
        /// </summary>
        /// <param name="controller">The target setup controller.</param>
        void AddPostProvisioningSteps(SetupController<NodeDefinition> controller);

        /// <summary>
        /// Adds the steps required to deprovision a cluster.
        /// </summary>
        /// <param name="controller"></param>
        void AddDeprovisoningSteps(SetupController<NodeDefinition> controller);

        /// <summary>
        /// Returns <c>true</c> if the hosting manage is capable of updating the upstream
        /// network router or load balancer.  Cloud based managers will return <c>true</c>
        /// whereas on-premise managers will return <c>false</c> because we don't have
        /// the ability to manage physical routers yet.
        /// </summary>
        bool CanManageRouter { get; }

        /// <summary>
        /// <para>
        /// Updates the cluster's load balancer or router to use the current set of
        /// ingress rules defined by <see cref="NetworkOptions.IngressRules"/> and the
        /// egress rules defined by <see cref="NetworkOptions.EgressAddressRules"/>.
        /// </para>
        /// <note>
        /// This currently supported only by cloud hosting managers like for Azure,
        /// AWS, and Google.  This will do nothing for the on-premise hosting managers
        /// because we don't have the ability to manage physical routers yet.
        /// </note>
        /// </summary>
        Task UpdateInternetRoutingAsync();

        /// <summary>
        /// <para>
        /// Enables public SSH access for every node in the cluster, honoring source
        /// address limitations specified by <see cref="NetworkOptions.ManagementAddressRules"/>
        /// in the cluster definition.
        /// </para>
        /// <para>
        /// Each node will be assigned a public port that has a NAT rule directing SSH
        /// traffic to that specific node.  These ports will be in the range of
        /// <see cref="NetworkOptions.FirstExternalSshPort"/> to <see cref="NetworkOptions.LastExternalSshPort"/>.
        /// <see cref="GetSshEndpoint(string)"/> will return the external endpoint
        /// for nodes when external SSH is enabled. 
        /// </para>
        /// <note>
        /// This currently supported only by cloud hosting managers like: Azure,
        /// AWS, and Google.  This will do nothing for the on-premise hosting managers
        /// because we don't have the ability to manage physical routers yet.
        /// </note>
        /// </summary>
        Task EnableInternetSshAsync();

        /// <summary>
        /// <para>
        /// Disables public SSH access for every node in the cluster, honoring source
        /// address limitations specified by <see cref="NetworkOptions.ManagementAddressRules"/>
        /// in the cluster definition.
        /// </para>
        /// <note>
        /// This currently supported only by cloud hosting managers like: Azure,
        /// AWS, and Google.  This will do nothing for the on-premise hosting managers
        /// because we don't have the ability to manage physical routers yet.
        /// </note>
        /// </summary>
        Task DisableInternetSshAsync();

        /// <summary>
        /// Returns the FQDN or IP address (as a string) and the port to use
        /// to establish a SSH connection to a specific node. 
        /// </summary>
        /// <param name="nodeName">The target node's name.</param>
        /// <returns>A <b>(string Address, int Port)</b> tuple.</returns>
        /// <remarks>
        /// This will return the direct private node endpoint by default.  If
        /// <see cref="EnableInternetSshAsync"/> has been called and is supported by 
        /// the hosting manager, then this returns the public address of the
        /// cluster along with the public NAT port.
        /// </remarks>
        (string Address, int Port) GetSshEndpoint(string nodeName);

        /// <summary>
        /// Identifies the data disk device for a node.  This returns the data disk's device 
        /// name when an uninitialized data disk exists or "PRIMARY" when the  OS disk
        /// will be used for data.
        /// </summary>
        /// <returns>The disk device name or "PRIMARY".</returns>
        /// <remarks>
        /// <note>
        /// This will not work after the node's data disk has been initialized.
        /// </note>
        /// </remarks>
        string GetDataDisk(LinuxSshProxy node);

        //---------------------------------------------------------------------
        // Cluster life cycle methods

        /// <summary>
        /// <para>
        /// Starts a cluster if it's not already running.
        /// </para>
        /// <note>
        /// This operation may not be supported for all environments.
        /// </note>
        /// </summary>
        /// <param name="noWait">Optionally specifies that the method should not wait until the operation has completed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="NotSupportedException">Thrown if the hosting environment doesn't support this operation.</exception>
        Task StartClusterAsync(bool noWait = false);

        /// <summary>
        /// <para>
        /// Shuts down a cluster if it's running.
        /// </para>
        /// <note>
        /// This operation may not be supported for all environments.
        /// </note>
        /// </summary>
        /// <param name="stopMode">Optionally specifies how the cluster nodes are stopped.  This defaults to <see cref="StopMode.Graceful"/>.</param>
        /// <param name="noWait">Optionally specifies that the method should not wait until the operation has completed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="NotSupportedException">Thrown if the hosting environment doesn't support this operation.</exception>
        Task StopClusterAsync(StopMode stopMode = StopMode.Graceful, bool noWait = false);

        /// <summary>
        /// <para>
        /// Removes an existing cluster by terminating any nodes and then removing node VMs
        /// and any related resources as well as the related local cluster login by default.  
        /// The cluster does not need to be running.  This method can optionally remove clusters
        /// or VMs potentially orphaned by interrupted unit tests as identified by a resource 
        /// group or VM name prefix.
        /// </para>
        /// <note>
        /// This operation may not be supported for all environments.
        /// </note>
        /// </summary>
        /// <param name="noWait">Optionally specifies that the method should not wait until the operation has completed.</param>
        /// <param name="removeOrphansByPrefix">
        /// Optionally specifies that VMs or clusters with the same resource group prefix or VM name
        /// prefix will be removed as well.  See the remarks for more information.
        /// </param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="NotSupportedException">Thrown if the hosting environment doesn't support this operation.</exception>
        /// <remarks>
        /// <para>
        /// The <paramref name="removeOrphansByPrefix"/> parameter is typically enabled when running unit tests
        /// via the <b>KubernetesFixture</b> to ensure that clusters and VMs orphaned by previous interrupted
        /// test runs are removed in addition to removing the cluster specified by the cluster definition.
        /// </para>
        /// </remarks>
        Task RemoveClusterAsync(bool noWait = false, bool removeOrphansByPrefix = false);

        /// <summary>
        /// <para>
        /// Starts a specific cluster node when it's not already running.
        /// </para>
        /// <note>
        /// This operation may not be supported for all environments.
        /// </note>
        /// </summary>
        /// <param name="nodeName">Identifies the target node.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        Task StartNodeAsync(string nodeName);

        /// <summary>
        /// <para>
        /// Shuts a specific cluster node down when it's not already stopped or sleeping.
        /// </para>
        /// <note>
        /// This operation may not be supported for all environments.
        /// </note>
        /// </summary>
        /// <param name="nodeName">Identifies the target node.</param>
        /// <param name="stopMode">Optionally specifies how the node is stopped.  This defaults to <see cref="StopMode.Graceful"/>.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        Task StopNodeAsync(string nodeName, StopMode stopMode = StopMode.Graceful);

        /// <summary>
        /// <para>
        /// Retrieves the node image for a specified node in a cluster to a folder.  The node
        /// must already be stopped.  The node image file name will look like <b>NODE-NAME.EXTENSION</b>
        /// where <b>NODE-NAME</b> is the name of the node and <b>EXTENSION</b> will be the native
        /// extension for the hosting environment (e.g. <b>.vhdx</b> for Hyper-V, <b>.xva</b> for
        /// XenServer or <b>.tar</b> for WSL2.
        /// </para>
        /// <note>
        /// This operation may not be supported for all environments.
        /// </note>
        /// <note>
        /// This method is currently intended for internal use to support generating special ready-to-go node
        /// images and does not support nodes that have more than just an OS drive, such as cStor drives.
        /// </note>
        /// </summary>
        /// <param name="nodeName">Identifies the node being captured.</param>
        /// <param name="folder">Path to the output folder.</param>
        /// <returns>The fully qualified path to the downloaded image file.</returns>
        /// <exception cref="NotSupportedException">Thrown if the hosting environment doesn't support this operation.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the node is not stopped or the node has multiple drives.</exception>
        Task<string> GetNodeImageAsync(string nodeName, string folder);
    }
}
