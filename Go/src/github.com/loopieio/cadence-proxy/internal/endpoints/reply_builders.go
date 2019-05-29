package endpoints

import (
	"fmt"
	"reflect"

	"go.uber.org/cadence/workflow"

	domain "github.com/loopieio/cadence-proxy/internal/cadence/cadencedomains"
	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceerrors"
	"github.com/loopieio/cadence-proxy/internal/messages"
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"

	cadenceshared "go.uber.org/cadence/.gen/go/shared"
	"go.uber.org/zap"
)

func buildReply(reply messages.IProxyReply, cadenceError *cadenceerrors.CadenceError, result ...interface{}) {

	// check if there is anything in result
	var value interface{}
	if len(result) > 0 {
		value = result[0]
	}

	// handle the messages individually based on their message type
	switch reply.GetType() {

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

	// PingReply
	case messagetypes.PingReply:
		if v, ok := reply.(*messages.PingReply); ok {
			buildPingReply(v, cadenceError)
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
// ProxyReply builders

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
			// TODO: JACK -- IMPLEMENT THIS
			fmt.Printf("%v", v)
		}
	}
}
