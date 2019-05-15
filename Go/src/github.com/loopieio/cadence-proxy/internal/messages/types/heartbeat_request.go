package types

import (
	"github.com/loopieio/cadence-proxy/internal/messages"
)

type (

	// HeartbeatRequest is ProxyRequest of MessageType
	// HeartbeatRequest.
	//
	// A HeartbeatRequest contains a RequestId and a reference to a
	// ProxyReply struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ProxyRequest
	HeartbeatRequest struct {
		*ProxyRequest
	}
)

// NewHeartbeatRequest is the default constructor for
// HeartbeatRequest
//
// returns *HeartbeatRequest -> pointer to a newly initialized
// HeartbeatReqeuest in memory
func NewHeartbeatRequest() *HeartbeatRequest {
	request := new(HeartbeatRequest)
	request.ProxyRequest = NewProxyRequest()
	request.Type = messages.HeartbeatRequest
	request.SetReplyType(messages.HeartbeatReply)

	return request
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (request *HeartbeatRequest) Clone() IProxyMessage {
	heartbeatRequest := NewHeartbeatRequest()
	var messageClone IProxyMessage = heartbeatRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (request *HeartbeatRequest) CopyTo(target IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
}

// SetProxyMessage inherits docs from ProxyMessage.SetProxyMessage()
func (request *HeartbeatRequest) SetProxyMessage(value *ProxyMessage) {
	request.ProxyMessage.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ProxyMessage.GetProxyMessage()
func (request *HeartbeatRequest) GetProxyMessage() *ProxyMessage {
	return request.ProxyMessage.GetProxyMessage()
}

// GetRequestID inherits docs from ProxyMessage.GetRequestID()
func (request *HeartbeatRequest) GetRequestID() int64 {
	return request.ProxyMessage.GetRequestID()
}

// SetRequestID inherits docs from ProxyMessage.SetRequestID()
func (request *HeartbeatRequest) SetRequestID(value int64) {
	request.ProxyMessage.SetRequestID(value)
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetReplyType inherits docs from ProxyRequest.GetReplyType()
func (request *HeartbeatRequest) GetReplyType() messages.MessageType {
	return request.ProxyRequest.GetReplyType()
}

// SetReplyType inherits docs from ProxyRequest.SetReplyType()
func (request *HeartbeatRequest) SetReplyType(value messages.MessageType) {
	request.ProxyRequest.SetReplyType(value)
}
