package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowGetResultRequest is WorkflowRequest of MessageType
	// WorkflowGetResultRequest.
	//
	// A WorkflowGetResultRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// A WorkflowGetResultRequest will pass all of the given data
	// necessary to get the execution result of a cadence workflow instance
	WorkflowGetResultRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowGetResultRequest is the default constructor for a WorkflowGetResultRequest
//
// returns *WorkflowGetResultRequest -> a reference to a newly initialized
// WorkflowGetResultRequest in memory
func NewWorkflowGetResultRequest() *WorkflowGetResultRequest {
	request := new(WorkflowGetResultRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(messagetypes.WorkflowGetResultRequest)
	request.SetReplyType(messagetypes.WorkflowGetResultReply)

	return request
}

// GetWorkflowID gets a WorkflowGetResultRequest's WorkflowID value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowGetResultRequest's WorkflowID
func (request *WorkflowGetResultRequest) GetWorkflowID() *string {
	return request.GetStringProperty("WorkflowId")
}

// SetWorkflowID sets an WorkflowGetResultRequest's WorkflowID value
// in its properties map
//
// param value *string -> pointer to a string in memory holding the value
// of a WorkflowGetResultRequest's WorkflowID
func (request *WorkflowGetResultRequest) SetWorkflowID(value *string) {
	request.SetStringProperty("WorkflowId", value)
}

// GetRunID gets a WorkflowGetResultRequest's RunID value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowGetResultRequest's RunID
func (request *WorkflowGetResultRequest) GetRunID() *string {
	return request.GetStringProperty("RunId")
}

// SetRunID sets a WorkflowGetResultRequest's RunID value
// in its properties map.
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *WorkflowGetResultRequest) SetRunID(value *string) {
	request.SetStringProperty("RunId", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowGetResultRequest) Clone() IProxyMessage {
	workflowGetResultRequest := NewWorkflowGetResultRequest()
	var messageClone IProxyMessage = workflowGetResultRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowGetResultRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
	if v, ok := target.(*WorkflowGetResultRequest); ok {
		v.SetWorkflowID(request.GetWorkflowID())
		v.SetRunID(request.GetRunID())
	}
}
