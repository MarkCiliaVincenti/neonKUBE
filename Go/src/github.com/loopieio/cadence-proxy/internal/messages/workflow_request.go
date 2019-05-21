package messages

import (
	"time"

	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowRequest is base type for all workflow requests
	// All workflow requests will inherit from WorkflowRequest and
	// a WorkflowRequest contains a WorkflowContextID, which is a int64 property
	//
	// A WorkflowRequest contains a reference to a
	// ProxyReply struct in memory
	WorkflowRequest struct {
		*ProxyRequest
	}

	// IWorkflowRequest is the interface that all workflow message requests
	// implement.  It allows access to a WorkflowRequest's WorkflowContextID, a property
	// that all WorkflowRequests share
	IWorkflowRequest interface {
		GetWorkflowContextID() int64
		SetWorkflowContextID(value int64)
	}
)

// NewWorkflowRequest is the default constructor for a WorkflowRequest
//
// returns *WorkflowRequest -> a pointer to a newly initialized WorkflowRequest
// in memory
func NewWorkflowRequest() *WorkflowRequest {
	request := new(WorkflowRequest)
	request.ProxyRequest = NewProxyRequest()
	request.Type = messagetypes.Unspecified
	request.SetReplyType(messagetypes.Unspecified)

	return request
}

// -------------------------------------------------------------------------
// IWorkflowRequest interface methods for implementing the IWorkflowRequest interface

// GetWorkflowContextID gets the ContextId from a WorkflowRequest's properties
// map.
//
// returns int64 -> the long representing a WorkflowRequest's ContextId
func (request *WorkflowRequest) GetWorkflowContextID() int64 {
	return request.GetLongProperty("WorkflowContextId")
}

// SetWorkflowContextID sets the ContextId in a WorkflowRequest's properties map
//
// param value int64 -> int64 value to set as the WorkflowRequest's ContextId
// in its properties map
func (request *WorkflowRequest) SetWorkflowContextID(value int64) {
	request.SetLongProperty("WorkflowContextId", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (request *WorkflowRequest) Clone() IProxyMessage {
	workflowContextRequest := NewWorkflowRequest()
	var messageClone IProxyMessage = workflowContextRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (request *WorkflowRequest) CopyTo(target IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
	if v, ok := target.(IWorkflowRequest); ok {
		v.SetWorkflowContextID(request.GetWorkflowContextID())
	}
}

// SetProxyMessage inherits docs from ProxyMessage.SetProxyMessage()
func (request *WorkflowRequest) SetProxyMessage(value *ProxyMessage) {
	request.ProxyRequest.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ProxyMessage.GetProxyMessage()
func (request *WorkflowRequest) GetProxyMessage() *ProxyMessage {
	return request.ProxyRequest.GetProxyMessage()
}

// GetRequestID inherits docs from ProxyMessage.GetRequestID()
func (request *WorkflowRequest) GetRequestID() int64 {
	return request.ProxyRequest.GetRequestID()
}

// SetRequestID inherits docs from ProxyMessage.SetRequestID()
func (request *WorkflowRequest) SetRequestID(value int64) {
	request.ProxyRequest.SetRequestID(value)
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetReplyType inherits docs from ProxyRequest.GetReplyType()
func (request *WorkflowRequest) GetReplyType() messagetypes.MessageType {
	return request.ProxyRequest.GetReplyType()
}

// SetReplyType inherits docs from ProxyRequest.SetReplyType()
func (request *WorkflowRequest) SetReplyType(value messagetypes.MessageType) {
	request.ProxyRequest.SetReplyType(value)
}

// GetTimeout inherits docs from ProxyRequest.GetTimeout()
func (request *WorkflowRequest) GetTimeout() time.Duration {
	return request.ProxyRequest.GetTimeout()
}

// SetTimeout inherits docs from ProxyRequest.SetTimeout()
func (request *WorkflowRequest) SetTimeout(value time.Duration) {
	request.ProxyRequest.SetTimeout(value)
}
