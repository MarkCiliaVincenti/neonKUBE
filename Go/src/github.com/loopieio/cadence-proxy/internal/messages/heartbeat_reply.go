package messages

import (
	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceerrors"
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// HeartbeatReply is a ProxyReply of MessageType
	// HeartbeatReply It holds a reference to a
	// ProxyReply in memory
	HeartbeatReply struct {
		*ProxyReply
	}
)

// NewHeartbeatReply is the default constructor for
// a HeartbeatReply
//
// returns *HeartbeatReply -> pointer to a newly initialized
// HeartbeatReply in memory
func NewHeartbeatReply() *HeartbeatReply {
	reply := new(HeartbeatReply)
	reply.ProxyReply = NewProxyReply()
	reply.SetType(messagetypes.HeartbeatReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyReply.Clone()
func (reply *HeartbeatReply) Clone() IProxyMessage {
	heartbeatReply := NewHeartbeatReply()
	var messageClone IProxyMessage = heartbeatReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyReply.CopyTo()
func (reply *HeartbeatReply) CopyTo(target IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
}

// SetProxyMessage inherits docs from ProxyReply.SetProxyMessage()
func (reply *HeartbeatReply) SetProxyMessage(value *ProxyMessage) {
	reply.ProxyReply.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ProxyReply.GetProxyMessage()
func (reply *HeartbeatReply) GetProxyMessage() *ProxyMessage {
	return reply.ProxyReply.GetProxyMessage()
}

// GetRequestID inherits docs from ProxyReply.GetRequestID()
func (reply *HeartbeatReply) GetRequestID() int64 {
	return reply.ProxyReply.GetRequestID()
}

// SetRequestID inherits docs from ProxyReply.SetRequestID()
func (reply *HeartbeatReply) SetRequestID(value int64) {
	reply.ProxyReply.SetRequestID(value)
}

// GetType inherits docs from ProxyReply.GetType()
func (reply *HeartbeatReply) GetType() messagetypes.MessageType {
	return reply.ProxyReply.GetType()
}

// SetType inherits docs from ProxyReply.SetType()
func (reply *HeartbeatReply) SetType(value messagetypes.MessageType) {
	reply.ProxyReply.SetType(value)
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError inherits docs from ProxyReply.GetError()
func (reply *HeartbeatReply) GetError() *cadenceerrors.CadenceError {
	return reply.ProxyReply.GetError()
}

// SetError inherits docs from ProxyReply.SetError()
func (reply *HeartbeatReply) SetError(value *cadenceerrors.CadenceError) {
	reply.ProxyReply.SetError(value)
}
