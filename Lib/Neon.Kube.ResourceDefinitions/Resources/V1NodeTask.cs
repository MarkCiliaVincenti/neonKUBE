﻿//-----------------------------------------------------------------------------
// FILE:	    V1NodeTask.cs
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
using System.Text;

using k8s;
using k8s.Models;

#if KUBEOPS
using DotnetKubernetesClient.Entities;
using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;
#endif

#if KUBEOPS
namespace Neon.Kube.ResourceDefinitions
#else
namespace Neon.Kube.Resources
#endif
{
    /// <summary>
    /// <para>
    /// Describes a task to be executed as a Bash script on a node by the <b>neon-node-agent</b> pod
    /// running on the target cluster node.
    /// </para>
    /// <note>
    /// The node agent currently executes one node task at a time in no guaranteed order.
    /// </note>
    /// </summary>
    /// <remarks>
    /// <para>
    /// neonKUBE clusters deploy the <b>neon-node-agent</b> as a daemonset such that this is running on
    /// every node in the cluster.  This runs as a privileged pod and has full access to the host node's
    /// file system, network, and processes and is typically used for low-level node maintainance activities.
    /// </para>
    /// <para><b>NODETASK SCRIPTS</b></para>
    /// <para>
    /// Node tasks are simply Bash scripts executed on the node by the <b>neon-node-agent</b> daemon running
    /// on the node.  These scripts will be written to the node's file system like:
    /// </para>
    /// <para><b>/var/run/neonkube/node-agent/nodetasks/GUID/script.sh</b></para>
    /// <para>
    /// where GUID is a base-36 encoded GUID generated and assigned to the task by the agent.
    /// </para>
    /// <para>
    /// <b>neon-node-agent</b> adds some variable to the beginning of the deployed script before executing it:
    /// </para>
    /// <list type="table">
    /// <item>
    ///     <term><b>$NODE_ROOT></b></term>
    ///     <description>
    ///     Identifies where the host node's file system is mounted to the <b>neon-node-agent</b> container.
    ///     Since the script is executing in the context of the container, your script will need to use this
    ///     to reference files and directories on the host node.  This currently returns <b>/mnt/host</b> but
    ///     you should always use this variable instead of hardcoding the path.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>$SCRIPT_DIR</b></term>
    ///     <description>
    ///     Set to the directory where the script is executing (like <b>/var/run/neonkube/node-agent/nodetasks/GUID</b>.
    ///     Your scripts should generally store any temporary files here so they will be removed automaticaly by the
    ///     node agent.
    ///     </description>
    /// </item>
    /// </list>
    /// <para>
    /// </para>
    /// <para><b>LIFECYCLE</b></para>
    /// <para>
    /// Here is the description of a NodeTask lifecycle:
    /// </para>
    /// <list type="number">
    /// <item>
    /// <b>neon-cluster-operator</b> or other entity determines that a script needs to be run on a
    /// specific node and creates a <see cref="V1NodeTask"/> specifiying the name of the target node
    /// as well as the Bash script to be executed.
    /// </item>
    /// <item>
    /// <b>neon-node-agent</b> is running as a daemonset on all cluster nodes and each instance is
    /// watching for node tasks assigned to its node.
    /// </item>
    /// <item>
    /// When a <b>neon-node-agent</b> sees a pending <see cref="V1NodeTask"/> assigned to the
    /// node it's managing, the agent will assign its unique ID to the task status, set the
    /// <see cref="V1NodeTaskStatus.StartedUtc"/> to the current time and change the state to
    /// <see cref="NodeTaskPhase.Running"/>.
    /// </item>
    /// <item>
    /// The agent will assign a new UUID to the task and save this in the node task status.  This UUID will
    /// be used to name the script file persisted to the host and will also be used to identify the 
    /// The agent will then execute the script on the node, persisting the process ID to the node task 
    /// status along with the command line used to execute the script.  When the script finishes, the
    /// agent will capture its exit code and standard output and error streams as text.  The command 
    /// execution time will be limited by <see cref="V1NodeTaskSpec.TimeoutSeconds"/>.
    /// </item>
    /// <note>
    /// <para>
    /// <b>WARNING!</b> You need to recognize that secrets included in a node task command line will
    /// can be observed by examining the <b>NodeTask</b> custom resource.  These are persisted at the
    /// cluster level.  The script itself will be executed in a host folder where only <b>root</b> has
    /// permissions.
    /// </para>
    /// <para>
    /// Node tasks are intended to run local node tasks that probably won't need secrets.  We recommend 
    /// that you avoid running node tasks that need secrets and perform those operations using normal
    /// Kubernetes pods that obtain secrets from Kubernetes the usual way.
    /// </para>
    /// </note>
    /// <item>
    /// When the command completes without timing out, the agent will set its state to <see cref="NodeTaskPhase.Finished"/>,
    /// set <see cref="V1NodeTaskStatus.FinishedUtc"/> to the current time and <see cref="V1NodeTaskStatus.ExitCode"/>,
    /// <see cref="V1NodeTaskStatus.Output"/> and <see cref="V1NodeTaskStatus.Error"/> to the command results.
    /// </item>
    /// <note>
    /// The <see cref="V1NodeTask.V1NodeTaskSpec.CaptureOutput"/> property controls whether the standard
    /// output and error streams are captured.  This defaults to <c>true</c>.  <see cref="V1NodeTask"/>
    /// supports only text output encoded as UTF-8 or ASCII.  Binary output is not supported.  You should
    /// set <see cref="V1NodeTask.V1NodeTaskSpec.CaptureOutput"/><c>=false</c> in these cases or when
    /// the output may include secrets.
    /// </note>
    /// <item>
    /// When the command execution timesout, the agent will kill the process and set the node task state to
    /// <see cref="NodeTaskPhase.Timeout"/> and set <see cref="V1NodeTaskStatus.FinishedUtc"/> to the
    /// current time.
    /// </item>
    /// <item>
    /// <b>neon-node-agents</b> also look for running tasks that are assigned to its node but include a 
    /// <see cref="V1NodeTaskStatus.AgentId"/> that doesn't match the current agent's ID.  This can
    /// happen when the previous agent pod started executing the command and then was terminated before the
    /// command completed.  The agent will attempt to locate the running pod by its command line and
    /// process ID and terminate when it exists and then set the state to <see cref="NodeTaskPhase.Orphaned"/>
    /// and <see cref="V1NodeTaskStatus.FinishedUtc"/> to the current time.
    /// </item>
    /// <item>
    /// Finally, <b>neon-node-agent</b> periodically looks for Bash scripts that don't have corresponding node
    /// tasks and will delete these so they don't accumulate.  This means the a task's script will typically
    /// be deleted shortly after the task retention period has been exceeded.
    /// </item>
    /// <item>
    /// <b>neon-cluster-operator</b> also monitors these tasks.  It will remove tasks assigned to nodes
    /// that don't exist.
    /// </item>
    /// </list>
    /// </remarks>
    [KubernetesEntity(Group = Helper.NeonKubeResourceGroup, ApiVersion = "v1alpha1", Kind = "NodeTask", PluralName = "nodetasks")]
#if KUBEOPS
    [KubernetesEntityShortNames]
    [EntityScope(EntityScope.Cluster)]
    [Description("Describes a neonKUBE task to be executed on a specific cluster node.")]
#endif
    public class V1NodeTask : CustomKubernetesEntity<V1NodeTask.V1NodeTaskSpec, V1NodeTask.V1NodeTaskStatus>
    {
        //---------------------------------------------------------------------
        // Local types

        /// <summary>
        /// Enumerates the possible status of a <see cref="V1NodeTask"/>.
        /// </summary>
        public enum NodeTaskPhase
        {
            /// <summary>
            /// The task has been newly submitted.  <b>neon-node-agent</b> will set this
            /// to <see cref="Pending"/> when it sees the task for the first time.
            /// </summary>
            New = 0,

            /// <summary>
            /// The task is waiting to be executed by the <b>neon-node-agent</b>.
            /// </summary>
            Pending,

            /// <summary>
            /// The task is currently running.
            /// </summary>
            Running,

            /// <summary>
            /// The task timed out while executing.
            /// </summary>
            Timeout,

            /// <summary>
            /// The task started executing on one <b>neon-node-agent</b> pod which
            /// crashed or was otherwise terminated and a newly scheduled pod detected
            /// this sutuation.
            /// </summary>
            Orphaned,

            /// <summary>
            /// The task failed with a non-zero exit code.
            /// </summary>
            Failed,

            /// <summary>
            /// The task finished executing.
            /// </summary>
            Finished
        }

        //---------------------------------------------------------------------
        // Implementation

        /// <summary>
        /// Default constructor.
        /// </summary>
        public V1NodeTask()
        {
            ((IKubernetesObject)this).SetMetadata();
        }

        /// <summary>
        /// The node execute task specification.
        /// </summary>
        public class V1NodeTaskSpec
        {
            /// <summary>
            /// Identifies the target node where the command will be executed.
            /// </summary>
#if KUBEOPS
            [Required]
#endif
            public string Node { get; set; }

            /// <summary>
            /// Specifies the Bash script to be executed on the target node.
            /// </summary>
#if KUBEOPS
            [Required]
#endif
            public string BashScript { get; set; }

            /// <summary>
            /// Specifies the maximum time in seconds the command will be allowed to execute.
            /// This defaults to 300 seconds (5 minutes).
            /// </summary>
#if KUBEOPS
            [Required]
#endif
            public int TimeoutSeconds { get; set; } = 300;

            /// <summary>
            /// Specifies the maximum time to retain the task after it has been
            /// ended, for any reason.  <b>neon-cluster-operator</b> will add
            /// this to <see cref="V1NodeTaskStatus.FinishedUtc"/> to determine
            /// when it should delete the task.  This defaults to 600 seconds
            /// (10 minutes).
            /// </summary>
#if KUBEOPS
            [Required]
#endif
            public int RetainSeconds { get; set; } = 600;

            /// <summary>
            /// <para>
            /// Controls whether the command output is to be captured.  This defaults to <c>true</c>.
            /// </para>
            /// <note>
            /// <see cref="V1NodeTask"/> is designed to capture command output as UTF-8 or
            /// ASCII text.  Binary output or other text encodings are not supported.  You
            /// should set this to <c>false</c> for commands with unsupported output or
            /// when the command output may include secrets.
            /// </note>
            /// </summary>
#if KUBEOPS
            [Required]
#endif
            public bool CaptureOutput { get; set; } = true;

            /// <summary>
            /// Verifies that the specification properties are valid.
            /// </summary>
            /// <exception cref="CustomResourceException">Thrown when the resource is not valid.</exception>
            public void Validate()
            {
                var specPrefix = $"{nameof(V1NodeTask)}.Spec";

                if (string.IsNullOrEmpty(BashScript))
                {
                    throw new CustomResourceException($"[{specPrefix}.{nameof(BashScript)}]: cannot be NULL or empty.");
                }

                if (TimeoutSeconds <= 0)
                {
                    throw new CustomResourceException($"[{specPrefix}.{nameof(TimeoutSeconds)}={TimeoutSeconds}]: Must be greater than zero.");
                }

                if (RetainSeconds < 0)
                {
                    throw new CustomResourceException($"[{specPrefix}.{nameof(TimeoutSeconds)}={TimeoutSeconds}]: Cannot be negative.");
                }
            }
        }

        /// <summary>
        /// The node execute task status.
        /// </summary>
        public class V1NodeTaskStatus
        {
            /// <summary>
            /// The globally unique ID of the <b>neon-node-agent</b> instance that executed
            /// the command.  This is used to detect tasks that started executing but didn't
            /// finish before node agent crashed or was otherwise terminated, providing a way
            /// for the next node-agent to clean things up.
            /// </summary>
            public string AgentId { get; set; }

            /// <summary>
            /// Indicates the current task phase.  This defaults to <see cref="NodeTaskPhase.New"/>
            /// when the task is constructed which can be used to detect whether the status is
            /// actually persisted to Kubernetes.
            /// </summary>
            public NodeTaskPhase Phase { get; set; } = NodeTaskPhase.New;

            /// <summary>
            /// Indicates when the task started executing. 
            /// </summary>
            public DateTime? StartedUtc { get; set; }

            /// <summary>
            /// Indicates when the task finished executing.
            /// </summary>
            public DateTime? FinishedUtc { get; set;}

            /// <summary>
            /// Set to the task execution time serialized to a string.
            /// </summary>
            public string ExecutionTime { get; set; }

            /// <summary>
            /// The command line invoked for the task.  This is used for detecting orphaned tasks.
            /// </summary>
            public string CommandLine { get; set; }

            /// <summary>
            /// Set to a UUID identifying the execution.  This will be used to name the Bash
            /// script when persisted to the host node as well as to help identify the process
            /// when it's running.
            /// </summary>
            public string ExecutionId { get; set; }

            /// <summary>
            /// Set to the ID of the task process while its running.
            /// </summary>
            public int? ProcessId { get; set; }

            /// <summary>
            /// The exit code returned by the command.
            /// </summary>
            public int ExitCode { get; set; }

            /// <summary>
            /// The text written to standard output by the command.
            /// </summary>
            public string Output { get; set; }

            /// <summary>
            /// The text written to standard error by the command.
            /// </summary>
            public string Error { get; set; }

            /// <summary>
            /// Verifies that the status properties are valid.
            /// </summary>
            /// <exception cref="CustomResourceException">Thrown when the resource is not valid.</exception>
            public void Validate()
            {
            }
        }

        /// <summary>
        /// Verifies that the resource properties are valid.
        /// </summary>
        /// <exception cref="CustomResourceException">Thrown when the resource is not valid.</exception>
        public void Validate()
        {
            Spec?.Validate();
            Status?.Validate();
        }
    }
}
