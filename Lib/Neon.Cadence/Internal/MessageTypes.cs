﻿//-----------------------------------------------------------------------------
// FILE:	    MessageTypes.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License"),
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
using System.Linq;
using System.Text;

using Newtonsoft.Json;
using YamlDotNet.Serialization;

using Neon.Common;

namespace Neon.Cadence.Internal
{
    /// <summary>
    /// Enumerates the possible message types.
    /// </summary>
    internal enum MessageTypes
    {
        /// <summary>
        /// Indicates a message with an unspecified type.  This normally indicates an error.
        /// </summary>
        Unspecified = 0,

        //---------------------------------------------------------------------
        // Client messages

        /// <summary>
        /// <b>client --> proxy:</b> Informs the proxy of the network endpoint where the
        /// library is listening for proxy messages.  The proxy should respond with an
        /// <see cref="InitializeReply"/> when it's ready to begin receiving inbound
        /// proxy messages.
        /// </summary>
        InitializeRequest = 1,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="InitializeRequest"/> message
        /// to indicate that the proxy ready to begin receiving inbound proxy messages.
        /// </summary>
        InitializeReply = 2,

        /// <summary>
        /// client --> proxy: Requests that the proxy establish a connection to a Cadence
        /// cluster.  This maps to a <c>NewClient()</c> in the proxy.
        /// </summary>
        ConnectRequest = 3,

        /// <summary>
        /// proxy --> client: Sent in response to a <see cref="ConnectRequest"/> message.
        /// </summary>
        ConnectReply = 4,

        /// <summary>
        /// <b>client --> proxy:</b> Signals the proxy that it should terminate gracefully.  The
        /// proxy should send a <see cref="TerminateReply"/> back to the library and
        /// then exit, terminating the process.
        /// </summary>
        TerminateRequest = 5,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="TerminateRequest"/> message.
        /// </summary>
        TerminateReply = 6,

        /// <summary>
        /// <b>client --> proxy:</b> Requests that the proxy register a Cadence domain.
        /// </summary>
        DomainRegisterRequest = 7,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="DomainRegisterRequest"/> message.
        /// </summary>
        DomainRegisterReply = 8,

        /// <summary>
        /// <b>client --> proxy:</b> Requests that the proxy return the details for a Cadence domain.
        /// </summary>
        DomainDescribeRequest = 9,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="DomainDescribeRequest"/> message.
        /// </summary>
        DomainDescribeReply = 10,

        /// <summary>
        /// <b>client --> proxy:</b> Requests that the proxy update a Cadence domain.
        /// </summary>
        DomainUpdateRequest = 11,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="DomainUpdateRequest"/> message.
        /// </summary>
        DomainUpdateReply = 12,

        /// <summary>
        /// <b>client --> proxy:</b> Sent periodically (every second) by the library to the
        /// proxy to verify that it is still healthy.
        /// </summary>
        HeartbeatRequest = 13,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="HeartbeatRequest"/> message.
        /// </summary>
        HeartbeatReply = 14,

        /// <summary>
        /// <b>client --> proxy:</b> Sent to request that a pending operation be cancelled.
        /// </summary>
        CancelRequest = 15,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="CancelRequest"/> message,
        /// indicating that the operation was canceled or that it already completed or no longer
        /// exists.
        /// </summary>
        CancelReply = 16,

        /// <summary>
        /// <b>client --> proxy:</b> Indicates that the application is capable of handling workflows
        /// and activities within a specific Cadence domain and task lisk.
        /// </summary>
        NewWorkerRequest = 17,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="NewWorkerRequest"/> message.
        /// </summary>
        NewWorkerReply = 18,

        /// <summary>
        /// <b>client --> proxy:</b> Stops a Cadence worker.
        /// </summary>
        StopWorkerRequest = 19,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="StopWorkerRequest"/> message,
        /// </summary>
        StopWorkerReply = 20,

        /// <summary>
        /// Sent from either the library or proxy mainly for measuring the raw throughput of 
        /// client/proxy transactions.  The receiver simply responds immediately with a
        /// <see cref="PingReply"/>.
        /// </summary>
        PingRequest = 21,

        /// <summary>
        /// Sent by either side in response to a <see cref="PingRequest"/>.
        /// </summary>
        PingReply = 22,

        //---------------------------------------------------------------------
        // Workflow messages
        //
        // Note that all workflow client request messages will include [WorkflowClientId] property
        // identifying the target workflow client.

        /// <summary>
        /// <b>client --> proxy:</b> Registers a workflow handler.
        /// </summary>
        WorkflowRegisterRequest = 100,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowRegisterRequest"/> message.
        /// </summary>
        WorkflowRegisterReply = 101,

        /// <summary>
        /// <b>client --> proxy:</b> Starts a workflow.
        /// </summary>
        WorkflowExecuteRequest = 102,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowExecuteRequest"/> message.
        /// </summary>
        WorkflowExecuteReply = 103,

        /// <summary>
        /// <b>client --> proxy:</b> Signals a running workflow.
        /// </summary>
        WorkflowSignalRequest = 104,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowSignalRequest"/> message.
        /// </summary>
        WorkflowSignalReply = 105,

        /// <summary>
        ///<b>client --> proxy:</b> Signals a workflow, starting it first if necessary.
        /// </summary>
        WorkflowSignalWithStartRequest = 106,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowSignalWithStartRequest"/> message.
        /// </summary>
        WorkflowSignalWithStartReply = 107,

        /// <summary>
        /// <b>client --> proxy:</b> Cancels a workflow.
        /// </summary>
        WorkflowCancelRequest = 108,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowCancelRequest"/> message.
        /// </summary>
        WorkflowCancelReply = 109,

        /// <summary>
        /// <b>client --> proxy:</b> Terminates a workflow.
        /// </summary>
        WorkflowTerminateRequest = 110,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowTerminateRequest"/> message.
        /// </summary>
        WorkflowTerminateReply = 111,

        /// <summary>
        /// <b>client --> proxy:</b> Requests the a workflow's history.
        /// </summary>
        WorkflowGetHistoryRequest = 112,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowGetHistoryRequest"/> message.
        /// </summary>
        WorkflowGetWorkflowHistoryReply = 113,

        /// <summary>
        /// <b>client --> proxy:</b> Indicates that an activity has completed.
        /// </summary>
        WorkflowCompleteActivityRequest = 114,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowCompleteActivityRequest"/> message.
        /// </summary>
        WorkflowCompleteActivityReply = 115,

        /// <summary>
        /// <b>client --> proxy:</b> Indicates that the activity with a specified ID as completed has completed.
        /// </summary>
        WorkflowCompleteActivityByIdRequest = 116,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowCompleteActivityByIdRequest"/> message.
        /// </summary>
        WorkflowCompleteActivityByIdReply = 117,

        /// <summary>
        /// <b>client --> proxy:</b> Records an activity heartbeat.
        /// </summary>
        WorkflowRecordActivityHeartbeatRequest = 118,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowRecordActivityHeartbeatRequest"/> message.
        /// </summary>
        WorkflowRecordActivityHeartbeatReply = 119,

        /// <summary>
        /// <b>client --> proxy:</b> Records a heartbeat for an activity specified by ID.
        /// </summary>
        WorkflowRecordActivityHeartbeatByIdRequest = 120,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowRecordActivityHeartbeatByIdRequest"/> message.
        /// </summary>
        WorkflowRecordActivityHeartbeatByIdReply = 121,

        /// <summary>
        /// <b>client --> proxy:</b> Requests the list of closed workflows.
        /// </summary>
        WorkflowListClosedRequest = 122,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowListClosedRequest"/> message.
        /// </summary>
        WorkflowListClosedReply = 123,

        /// <summary>
        /// <b>client --> proxy:</b> Requests the list of open workflows.
        /// </summary>
        WorkflowListOpenExecutionsRequest = 124,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowListOpenExecutionsRequest"/> message.
        /// </summary>
        WorkflowListOpenExecutionsReply = 125,

        /// <summary>
        /// <b>client --> proxy:</b> Queries a workflow's last execution.
        /// </summary>
        WorkflowQueryRequest = 126,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowQueryRequest"/> message.
        /// </summary>
        WorkflowQueryReply = 127,

        /// <summary>
        /// <b>client --> proxy:</b> Returns information about a worflow execution.
        /// </summary>
        WorkflowDescribeWorkflowExecutionRequest = 128,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowDescribeWorkflowExecutionRequest"/> message.
        /// </summary>
        WorkflowDescribexecutionReply = 129,

        /// <summary>
        /// <b>RESERVED:</b> This is not currently implemented.
        /// </summary>
        [Obsolete("RESERVED but not implemented.")]
        WorkflowDescribeTaskListRequest = 130,

        /// <summary>
        /// <b>RESERVED:</b> This is not currently implemented.
        /// </summary>
        [Obsolete("RESERVED but not implemented.")]
        WorkflowDescribeTaskListReply = 131,

        /// <summary>
        /// <b>proxy --> client:</b> Commands the client library and associated .NET application
        /// to process a workflow instance.
        /// </summary>
        WorkflowInvokeRequest = 132,

        /// <summary>
        /// <b>client --> proxy:</b> Sent in response to a <see cref="WorkflowInvokeRequest"/> message.
        /// </summary>
        WorkflowInvokeReply = 133,

        /// <summary>
        /// <b>proxy --> client:</b> Initiates execution of a child workflow.
        /// </summary>
        WorkflowExecuteChildRequest = 134,

        /// <summary>
        /// <b>client --> proxy:</b> Sent in response to a <see cref="WorkflowInvokeRequest"/> message.
        /// </summary>
        WorkflowExecuteChildReply = 135,

        /// <summary>
        /// <b>client --> proxy:</b> Indicates that .NET application wishes to consume signals from
        /// a named channel.  Any signals received by the proxy will be forwarded to the
        /// library via <see cref="WorkflowSignalReceivedRequest"/> messages.
        /// </summary>
        WorkflowSignalSubscribeRequest = 136,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowSignalSubscribeRequest"/> message.
        /// </summary>
        WorkflowSignalSubscribeReply = 137,

        /// <summary>
        /// <b>proxy --> client:</b> Send when a signal is received by the proxy on a subscribed channel.
        /// </summary>
        WorkflowSignalReceivedRequest = 138,

        /// <summary>
        /// <b>client --> proxy:</b> Sent in response to a <see cref="WorkflowSignalReceivedRequest"/> message.
        /// </summary>
        WorkflowSignalReceivedReply = 139,

        /// <summary>
        /// <para>
        /// <b>proxy --> client:</b> Implements the standard Cadence <i>side effect</i> behavior by 
        /// transmitting a <see cref="WorkflowSideEffectInvokeRequest"/> to the library and
        /// waiting for the <see cref="WorkflowSideEffectInvokeReply"/> reply, persisting the 
        /// answer in the workflow history and then transmitting the answer back to the .NET
        /// workflow implementation via a <see cref="WorkflowSideEffectReply"/>.
        /// </para>
        /// <para>
        /// This message includes a unique identifier that is used to ensure that a specific side effect
        /// operation results in only a single <see cref="WorkflowSideEffectInvokeRequest"/> message to
        /// the .NET workflow application per workflow instance.  Subsequent calls will simply return the
        /// value from the execution history.
        /// </para>
        /// </summary>
        WorkflowSideEffectRequest = 140,

        /// <summary>
        /// <b>client --> proxy:</b> Sent in response to a <see cref="WorkflowSignalReceivedRequest"/> message.
        /// </summary>
        WorkflowSideEffectReply = 141,

        /// <summary>
        /// <b>proxy --> client:</b> Sent by the proxy to the library the first time a side effect
        /// operation is submitted a workflow instance.  The library will response with the
        /// side effect value to be persisted in the workflow history and returned back to
        /// the the .NET workflow application.
        /// </summary>
        WorkflowSideEffectInvokeRequest = 142,

        /// <summary>
        /// <b>client --> proxy:</b> Sent in response to a <see cref="WorkflowSignalReceivedRequest"/> message.
        /// </summary>
        WorkflowSideEffectInvokeReply = 143,

        /// <summary>
        /// <b>client --> proxy:</b> Sets the maximum number of bytes the client will use
        /// to cache the history of a sticky workflow on a workflow worker as a performance
        /// optimization.  When this is exceeded for a workflow, its full history will
        /// need to be retrieved from the Cadence cluster the next time the workflow
        /// instance is assigned to a worker. 
        /// </summary>
        WorkflowSetCacheSizeRequest = 23,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="WorkflowSetCacheSizeRequest"/>.
        /// </summary>
        WorkflowSetCacheSizeReply = 24,

        //---------------------------------------------------------------------
        // Activity messages

        /// <summary>
        /// <b>proxy --> client:</b> Commands the client library and associated .NET application
        /// to process an activity instance.
        /// </summary>
        ActivityInvokeRequest = 200,

        /// <summary>
        /// <b>client --> proxy:</b> Sent in response to a <see cref="ActivityInvokeRequest"/> message.
        /// </summary>
        ActivityInvokeReply = 201,

        /// <summary>
        /// <b>client --> proxy:</b> Requests the heartbeat details from the last failed attempt.
        /// </summary>
        ActivityGetHeartbeatDetailsRequest = 202,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="ActivityGetHeartbeatDetailsRequest"/> message.
        /// </summary>
        ActivityGetHeartbeatDetailsReply = 203,

        /// <summary>
        /// <b>client --> proxy:</b> Logs a message for an activity.
        /// </summary>
        ActivityLogRequest = 204,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="ActivityLogRequest"/> message.
        /// </summary>
        ActivityLogReply = 205,

        /// <summary>
        /// <b>client --> proxy:</b> Records a heartbeat message for an activity.
        /// </summary>
        ActivityRecordHeartbeatRequest = 206,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="ActivityRecordHeartbeatRequest"/> message.
        /// </summary>
        ActivityRecordHeartbeatReply = 207,

        /// <summary>
        /// <b>client --> proxy:</b> Determines whether an activity execution has any heartbeat details.
        /// </summary>
        ActivityHasHeartbeatDetailsRequest = 208,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="ActivityHasHeartbeatDetailsRequest"/> message.
        /// </summary>
        ActivityHasHeartbeatDetailsReply = 209,

        /// <summary>
        /// <b>client --> proxy:</b> Signals that the application executing an activity is terminating,
        /// giving the the proxy a chance to gracefully inform Cadence and then terminate the activity.
        /// </summary>
        ActivityStopRequest = 210,

        /// <summary>
        /// <b>proxy --> client:</b> Sent in response to a <see cref="ActivityStopRequest"/> message.
        /// </summary>
        ActivityStopReply = 211,
    }
}
