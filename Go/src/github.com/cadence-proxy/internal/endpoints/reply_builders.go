//-----------------------------------------------------------------------------
// FILE:		reply_builders.go
// CONTRIBUTOR: John C Burnes
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

package endpoints

import (
	"fmt"
	"reflect"
	"time"

	"go.uber.org/cadence/activity"

	cadenceshared "go.uber.org/cadence/.gen/go/shared"
	"go.uber.org/cadence/workflow"
	"go.uber.org/zap"

	domain "github.com/cadence-proxy/internal/cadence/cadencedomains"
	"github.com/cadence-proxy/internal/cadence/cadenceerrors"
	"github.com/cadence-proxy/internal/messages"
	messagetypes "github.com/cadence-proxy/internal/messages/types"
)

func buildReply(reply messages.IProxyReply, cadenceError *cadenceerrors.CadenceError, result ...interface{}) {

	// check if there is anything in result
	var value interface{}
	if len(result) > 0 {
		value = result[0]
	}

	// handle the messages individually based on their message type
	switch reply.GetType() {

	// -------------------------------------------------------------------------
	// client message types

	// InitializeReply
	case messagetypes.InitializeReply:
		if v, ok := reply.(*messages.InitializeReply); ok {
			buildInitializeReply(v, cadenceError)
		}

	// HeartbeatReply
	case messagetypes.HeartbeatReply:
		if v, ok := reply.(*messages.HeartbeatReply); ok {
			buildHeartbeatReply(v, cadenceError)
		}

	// CancelReply
	case messagetypes.CancelReply:
		if v, ok := reply.(*messages.CancelReply); ok {
			buildCancelReply(v, cadenceError, value)
		}

	// ConnectReply
	case messagetypes.ConnectReply:
		if v, ok := reply.(*messages.ConnectReply); ok {
			buildConnectReply(v, cadenceError)
		}

	// DomainDescribeReply
	case messagetypes.DomainDescribeReply:
		if v, ok := reply.(*messages.DomainDescribeReply); ok {
			buildDomainDescribeReply(v, cadenceError, value)
		}

	// DomainRegisterReply
	case messagetypes.DomainRegisterReply:
		if v, ok := reply.(*messages.DomainRegisterReply); ok {
			buildDomainRegisterReply(v, cadenceError)
		}

	// DomainUpdateReply
	case messagetypes.DomainUpdateReply:
		if v, ok := reply.(*messages.DomainUpdateReply); ok {
			buildDomainUpdateReply(v, cadenceError)
		}

	// TerminateReply
	case messagetypes.TerminateReply:
		if v, ok := reply.(*messages.TerminateReply); ok {
			buildTerminateReply(v, cadenceError)
		}

	// NewWorkerReply
	case messagetypes.NewWorkerReply:
		if v, ok := reply.(*messages.NewWorkerReply); ok {
			buildNewWorkerReply(v, cadenceError, value)
		}

	// StopWorkerReply
	case messagetypes.StopWorkerReply:
		if v, ok := reply.(*messages.StopWorkerReply); ok {
			buildStopWorkerReply(v, cadenceError)
		}

	// PingReply
	case messagetypes.PingReply:
		if v, ok := reply.(*messages.PingReply); ok {
			buildPingReply(v, cadenceError)
		}

	// -------------------------------------------------------------------------
	// workflow message types

	// WorkflowExecuteReply
	case messagetypes.WorkflowExecuteReply:
		if v, ok := reply.(*messages.WorkflowExecuteReply); ok {
			buildWorkflowExecuteReply(v, cadenceError, value)
		}

	// WorkflowInvokeReply
	case messagetypes.WorkflowInvokeReply:
		if v, ok := reply.(*messages.WorkflowInvokeReply); ok {
			buildWorkflowInvokeReply(v, cadenceError, value)
		}

	// WorkflowRegisterReply
	case messagetypes.WorkflowRegisterReply:
		if v, ok := reply.(*messages.WorkflowRegisterReply); ok {
			buildWorkflowRegisterReply(v, cadenceError)
		}

	// WorkflowCancelReply
	case messagetypes.WorkflowCancelReply:
		if v, ok := reply.(*messages.WorkflowCancelReply); ok {
			buildWorkflowCancelReply(v, cadenceError)
		}

	// WorkflowSignalReply
	case messagetypes.WorkflowSignalReply:
		if v, ok := reply.(*messages.WorkflowSignalReply); ok {
			buildWorkflowSignalReply(v, cadenceError)
		}

	// WorkflowSignalWithStartReply
	case messagetypes.WorkflowSignalWithStartReply:
		if v, ok := reply.(*messages.WorkflowSignalWithStartReply); ok {
			buildWorkflowSignalWithStartReply(v, cadenceError, value)
		}

	// WorkflowQueryReply
	case messagetypes.WorkflowQueryReply:
		if v, ok := reply.(*messages.WorkflowQueryReply); ok {
			buildWorkflowQueryReply(v, cadenceError, value)
		}

	// WorkflowSetCacheSizeReply
	case messagetypes.WorkflowSetCacheSizeReply:
		if v, ok := reply.(*messages.WorkflowSetCacheSizeReply); ok {
			buildWorkflowSetCacheSizeReply(v, cadenceError)
		}

	// WorkflowMutableReply
	case messagetypes.WorkflowMutableReply:
		if v, ok := reply.(*messages.WorkflowMutableReply); ok {
			buildWorkflowMutableReply(v, cadenceError, value)
		}

	// WorkflowMutableInvokeReply
	case messagetypes.WorkflowMutableInvokeReply:
		if v, ok := reply.(*messages.WorkflowMutableInvokeReply); ok {
			buildWorkflowMutableInvokeReply(v, cadenceError, value)
		}

	// WorkflowTerminateReply
	case messagetypes.WorkflowTerminateReply:
		if v, ok := reply.(*messages.WorkflowTerminateReply); ok {
			buildWorkflowTerminateReply(v, cadenceError)
		}

	// WorkflowDescribeExecutionReply
	case messagetypes.WorkflowDescribeExecutionReply:
		if v, ok := reply.(*messages.WorkflowDescribeExecutionReply); ok {
			buildWorkflowDescribeExecutionReply(v, cadenceError, value)
		}

	// WorkflowGetResultReply
	case messagetypes.WorkflowGetResultReply:
		if v, ok := reply.(*messages.WorkflowGetResultReply); ok {
			buildWorkflowGetResultReply(v, cadenceError, value)
		}

	// WorkflowSignalSubscribeReply
	case messagetypes.WorkflowSignalSubscribeReply:
		if v, ok := reply.(*messages.WorkflowSignalSubscribeReply); ok {
			buildWorkflowSignalSubscribeReply(v, cadenceError)
		}

	// WorkflowHasLastResultReply
	case messagetypes.WorkflowHasLastResultReply:
		if v, ok := reply.(*messages.WorkflowHasLastResultReply); ok {
			buildWorkflowHasLastResultReply(v, cadenceError, value)
		}

	// WorkflowGetLastResultReply
	case messagetypes.WorkflowGetLastResultReply:
		if v, ok := reply.(*messages.WorkflowGetLastResultReply); ok {
			buildWorkflowGetLastResultReply(v, cadenceError, value)
		}

	// WorkflowDisconnectContextReply
	case messagetypes.WorkflowDisconnectContextReply:
		if v, ok := reply.(*messages.WorkflowDisconnectContextReply); ok {
			buildWorkflowDisconnectContextReply(v, cadenceError)
		}

	// WorkflowGetTimeReply
	case messagetypes.WorkflowGetTimeReply:
		if v, ok := reply.(*messages.WorkflowGetTimeReply); ok {
			buildWorkflowGetTimeReply(v, cadenceError, value)
		}

	// WorkflowSleepReply
	case messagetypes.WorkflowSleepReply:
		if v, ok := reply.(*messages.WorkflowSleepReply); ok {
			buildWorkflowSleepReply(v, cadenceError)
		}

	// -------------------------------------------------------------------------
	// activity message types

	// ActivityRegisterReply
	case messagetypes.ActivityRegisterReply:
		if v, ok := reply.(*messages.ActivityRegisterReply); ok {
			buildActivityRegisterReply(v, cadenceError)
		}

	// ActivityExecuteReply
	case messagetypes.ActivityExecuteReply:
		if v, ok := reply.(*messages.ActivityExecuteReply); ok {
			buildActivityExecuteReply(v, cadenceError, value)
		}

	// ActivityInvokeReply
	case messagetypes.ActivityInvokeReply:
		if v, ok := reply.(*messages.ActivityInvokeReply); ok {
			buildActivityInvokeReply(v, cadenceError, value)
		}

	// ActivityHasHeartbeatDetailsReply
	case messagetypes.ActivityHasHeartbeatDetailsReply:
		if v, ok := reply.(*messages.ActivityHasHeartbeatDetailsReply); ok {
			buildActivityHasHeartbeatDetailsReply(v, cadenceError, value)
		}

	// ActivityGetHeartbeatDetailsReply
	case messagetypes.ActivityGetHeartbeatDetailsReply:
		if v, ok := reply.(*messages.ActivityGetHeartbeatDetailsReply); ok {
			buildActivityGetHeartbeatDetailsReply(v, cadenceError, value)
		}

	// ActivityRecordHeartbeatReply
	case messagetypes.ActivityRecordHeartbeatReply:
		if v, ok := reply.(*messages.ActivityRecordHeartbeatReply); ok {
			buildActivityRecordHeartbeatReply(v, cadenceError, value)
		}

	// ActivityGetInfoReply
	case messagetypes.ActivityGetInfoReply:
		if v, ok := reply.(*messages.ActivityGetInfoReply); ok {
			buildActivityGetInfoReply(v, cadenceError, value)
		}

	// ActivityCompleteReply
	case messagetypes.ActivityCompleteReply:
		if v, ok := reply.(*messages.ActivityCompleteReply); ok {
			buildActivityCompleteReply(v, cadenceError)
		}

	// Undefined message type
	// This should never happen.
	default:

		// $debug(jack.burns): DELETE THIS!
		err := fmt.Errorf("unhandled message type. could not complete type assertion for type %d", reply.GetType())
		logger.Debug("Unhandled message type. Could not complete type assertion", zap.Error(err))
		panic(err)
	}
}

func createReplyMessage(request messages.IProxyRequest) messages.IProxyReply {

	// get the correct reply type and initialize a new
	// reply corresponding to the request message type
	reply := messages.CreateNewTypedMessage(request.GetReplyType())
	if reflect.ValueOf(reply).IsNil() {
		return nil
	}

	reply.SetRequestID(request.GetRequestID())
	if v, ok := reply.(messages.IProxyReply); ok {
		return v
	}

	return nil
}

// -------------------------------------------------------------------------
// Client message builders

func buildCancelReply(reply *messages.CancelReply, cadenceError *cadenceerrors.CadenceError, wasCancelled ...interface{}) {
	reply.SetError(cadenceError)

	if len(wasCancelled) > 0 {
		if v, ok := wasCancelled[0].(bool); ok {
			reply.SetWasCancelled(v)
		}
	}
}

func buildConnectReply(reply *messages.ConnectReply, cadenceError *cadenceerrors.CadenceError) {
	reply.SetError(cadenceError)
}

func buildDomainDescribeReply(reply *messages.DomainDescribeReply, cadenceError *cadenceerrors.CadenceError, describeDomainResponse ...interface{}) {
	reply.SetError(cadenceError)

	if len(describeDomainResponse) > 0 {
		if v, ok := describeDomainResponse[0].(*cadenceshared.DescribeDomainResponse); ok {
			reply.SetDomainInfoName(v.DomainInfo.Name)
			reply.SetDomainInfoDescription(v.DomainInfo.Description)

			domainStatus := domain.DomainStatus(int(*v.DomainInfo.Status))
			reply.SetDomainInfoStatus(&domainStatus)
			reply.SetConfigurationEmitMetrics(*v.Configuration.EmitMetric)
			reply.SetConfigurationRetentionDays(*v.Configuration.WorkflowExecutionRetentionPeriodInDays)
			reply.SetDomainInfoOwnerEmail(v.DomainInfo.OwnerEmail)
		}
	}
}

func buildDomainRegisterReply(reply *messages.DomainRegisterReply, cadenceError *cadenceerrors.CadenceError) {
	reply.SetError(cadenceError)
}

func buildDomainUpdateReply(reply *messages.DomainUpdateReply, cadenceError *cadenceerrors.CadenceError) {
	reply.SetError(cadenceError)
}

func buildHeartbeatReply(reply *messages.HeartbeatReply, cadenceError *cadenceerrors.CadenceError) {
	reply.SetError(cadenceError)
}

func buildInitializeReply(reply *messages.InitializeReply, cadenceError *cadenceerrors.CadenceError) {
	reply.SetError(cadenceError)
}

func buildTerminateReply(reply *messages.TerminateReply, cadenceError *cadenceerrors.CadenceError) {
	reply.SetError(cadenceError)
}

func buildNewWorkerReply(reply *messages.NewWorkerReply, cadenceError *cadenceerrors.CadenceError, workerID ...interface{}) {
	reply.SetError(cadenceError)

	if len(workerID) > 0 {
		if v, ok := workerID[0].(int64); ok {
			reply.SetWorkerID(v)
		}
	}
}

func buildStopWorkerReply(reply *messages.StopWorkerReply, cadenceError *cadenceerrors.CadenceError) {
	reply.SetError(cadenceError)
}

func buildPingReply(reply *messages.PingReply, cadenceError *cadenceerrors.CadenceError) {
	reply.SetError(cadenceError)
}

// -------------------------------------------------------------------------
// Workflow message builders

func buildWorkflowRegisterReply(reply *messages.WorkflowRegisterReply, cadenceError *cadenceerrors.CadenceError) {
	reply.SetError(cadenceError)
}

func buildWorkflowExecuteReply(reply *messages.WorkflowExecuteReply, cadenceError *cadenceerrors.CadenceError, execution ...interface{}) {
	reply.SetError(cadenceError)

	if len(execution) > 0 {
		if v, ok := execution[0].(*workflow.Execution); ok {
			reply.SetExecution(v)
		}
	}
}

func buildWorkflowInvokeReply(reply *messages.WorkflowInvokeReply, cadenceError *cadenceerrors.CadenceError, result ...interface{}) {
	reply.SetError(cadenceError)

	if len(result) > 0 {
		if v, ok := result[0].([]byte); ok {
			reply.SetResult(v)
		}
	}
}

func buildWorkflowCancelReply(reply *messages.WorkflowCancelReply, cadenceError *cadenceerrors.CadenceError) {
	reply.SetError(cadenceError)
}

func buildWorkflowTerminateReply(reply *messages.WorkflowTerminateReply, cadenceError *cadenceerrors.CadenceError) {
	reply.SetError(cadenceError)
}

func buildWorkflowSignalReply(reply *messages.WorkflowSignalReply, cadenceError *cadenceerrors.CadenceError) {
	reply.SetError(cadenceError)
}

func buildWorkflowSignalWithStartReply(reply *messages.WorkflowSignalWithStartReply, cadenceError *cadenceerrors.CadenceError, execution ...interface{}) {
	reply.SetError(cadenceError)

	if len(execution) > 0 {
		if v, ok := execution[0].(*workflow.Execution); ok {
			reply.SetExecution(v)
		}
	}
}

func buildWorkflowSetCacheSizeReply(reply *messages.WorkflowSetCacheSizeReply, cadenceError *cadenceerrors.CadenceError) {
	reply.SetError(cadenceError)
}

func buildWorkflowQueryReply(reply *messages.WorkflowQueryReply, cadenceError *cadenceerrors.CadenceError, result ...interface{}) {
	reply.SetError(cadenceError)

	if len(result) > 0 {
		if v, ok := result[0].([]byte); ok {
			reply.SetResult(v)
		}
	}
}

func buildWorkflowMutableReply(reply *messages.WorkflowMutableReply, cadenceError *cadenceerrors.CadenceError, result ...interface{}) {
	reply.SetError(cadenceError)

	if len(result) > 0 {
		if v, ok := result[0].([]byte); ok {
			reply.SetResult(v)
		}
	}
}

func buildWorkflowMutableInvokeReply(reply *messages.WorkflowMutableInvokeReply, cadenceError *cadenceerrors.CadenceError, result ...interface{}) {
	reply.SetError(cadenceError)

	if len(result) > 0 {
		if v, ok := result[0].([]byte); ok {
			reply.SetResult(v)
		}
	}
}

func buildWorkflowDescribeExecutionReply(reply *messages.WorkflowDescribeExecutionReply, cadenceError *cadenceerrors.CadenceError, description ...interface{}) {
	reply.SetError(cadenceError)

	if len(description) > 0 {
		if v, ok := description[0].(*cadenceshared.DescribeWorkflowExecutionResponse); ok {
			reply.SetDetails(v)
		}
	}
}

func buildWorkflowGetResultReply(reply *messages.WorkflowGetResultReply, cadenceError *cadenceerrors.CadenceError, result ...interface{}) {
	reply.SetError(cadenceError)

	if len(result) > 0 {
		if v, ok := result[0].([]byte); ok {
			reply.SetResult(v)
		}
	}
}

func buildWorkflowSignalSubscribeReply(reply *messages.WorkflowSignalSubscribeReply, cadenceError *cadenceerrors.CadenceError) {
	reply.SetError(cadenceError)
}

func buildWorkflowHasLastResultReply(reply *messages.WorkflowHasLastResultReply, cadenceError *cadenceerrors.CadenceError, hasResult ...interface{}) {
	reply.SetError(cadenceError)

	if len(hasResult) > 0 {
		if v, ok := hasResult[0].(bool); ok {
			reply.SetHasResult(v)
		}
	}
}

func buildWorkflowGetLastResultReply(reply *messages.WorkflowGetLastResultReply, cadenceError *cadenceerrors.CadenceError, result ...interface{}) {
	reply.SetError(cadenceError)

	if len(result) > 0 {
		if v, ok := result[0].([]byte); ok {
			reply.SetResult(v)
		}
	}
}

func buildWorkflowDisconnectContextReply(reply *messages.WorkflowDisconnectContextReply, cadenceError *cadenceerrors.CadenceError) {
	reply.SetError(cadenceError)
}

func buildWorkflowGetTimeReply(reply *messages.WorkflowGetTimeReply, cadenceError *cadenceerrors.CadenceError, t ...interface{}) {
	reply.SetError(cadenceError)

	if len(t) > 0 {
		if v, ok := t[0].(time.Time); ok {
			reply.SetTime(v)
		}
	}
}

func buildWorkflowSleepReply(reply *messages.WorkflowSleepReply, cadenceError *cadenceerrors.CadenceError) {
	reply.SetError(cadenceError)
}

// -------------------------------------------------------------------------
// Activity message builders

func buildActivityRegisterReply(reply *messages.ActivityRegisterReply, cadenceError *cadenceerrors.CadenceError) {
	reply.SetError(cadenceError)
}

func buildActivityExecuteReply(reply *messages.ActivityExecuteReply, cadenceError *cadenceerrors.CadenceError, result ...interface{}) {
	reply.SetError(cadenceError)

	if len(result) > 0 {
		if v, ok := result[0].([]byte); ok {
			reply.SetResult(v)
		}
	}
}

func buildActivityInvokeReply(reply *messages.ActivityInvokeReply, cadenceError *cadenceerrors.CadenceError, result ...interface{}) {
	reply.SetError(cadenceError)

	if len(result) > 0 {
		if v, ok := result[0].([]byte); ok {
			reply.SetResult(v)
		}
	}
}

func buildActivityHasHeartbeatDetailsReply(reply *messages.ActivityHasHeartbeatDetailsReply, cadenceError *cadenceerrors.CadenceError, hasDetails ...interface{}) {
	reply.SetError(cadenceError)

	if len(hasDetails) > 0 {
		if v, ok := hasDetails[0].(bool); ok {
			reply.SetHasDetails(v)
		}
	}
}

func buildActivityGetHeartbeatDetailsReply(reply *messages.ActivityGetHeartbeatDetailsReply, cadenceError *cadenceerrors.CadenceError, details ...interface{}) {
	reply.SetError(cadenceError)

	if len(details) > 0 {
		if v, ok := details[0].([]byte); ok {
			reply.SetDetails(v)
		}
	}
}

func buildActivityRecordHeartbeatReply(reply *messages.ActivityRecordHeartbeatReply, cadenceError *cadenceerrors.CadenceError, details ...interface{}) {
	reply.SetError(cadenceError)

	if len(details) > 0 {
		if v, ok := details[0].([]byte); ok {
			reply.SetDetails(v)
		}
	}
}

func buildActivityGetInfoReply(reply *messages.ActivityGetInfoReply, cadenceError *cadenceerrors.CadenceError, info ...interface{}) {
	reply.SetError(cadenceError)

	if len(info) > 0 {
		if v, ok := info[0].(activity.Info); ok {
			reply.SetInfo(&v)
		}
	}
}

func buildActivityCompleteReply(reply *messages.ActivityCompleteReply, cadenceError *cadenceerrors.CadenceError) {
	reply.SetError(cadenceError)
}
