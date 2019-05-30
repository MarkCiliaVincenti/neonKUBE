package messages

import (
	"time"

	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// ProxyRequest "extends" ProxyMessage and it is
	// a type of ProxyMessage that comes into the server
	// i.e. a request
	//
	// A ProxyRequest contains a RequestId and a reference to a
	// ProxyMessage struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ProxyRequest
	ProxyRequest struct {
		*ProxyMessage
		ReplyType messagetypes.MessageType
	}

	// IProxyRequest is an interface for all ProxyRequest message types.
	// It allows any message type that implements the IProxyRequest interface
	// to use any methods defined.  The primary use of this interface is to
	// allow message types that implement it to get and set their nested ProxyRequest
	IProxyRequest interface {
		IProxyMessage
		GetReplyType() messagetypes.MessageType
		SetReplyType(value messagetypes.MessageType)
		GetTimeout() time.Duration
		SetTimeout(value time.Duration)
	}
)

// NewProxyRequest is the default constructor for a ProxyRequest
//
// returns *ProxyRequest -> a pointer to a newly initialized ProxyRequest
// in memory
func NewProxyRequest() *ProxyRequest {
	request := new(ProxyRequest)
	request.ProxyMessage = NewProxyMessage()
	request.SetType(messagetypes.Unspecified)
	request.SetReplyType(messagetypes.Unspecified)

	return request
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetReplyType gets the MessageType used to reply to a specific
// ProxyRequest
//
// returns MessageType -> the message type to reply to the
// request with
func (request *ProxyRequest) GetReplyType() messagetypes.MessageType {
	return request.ReplyType
}

// SetReplyType sets the MessageType used to reply to a specific
// ProxyRequest
//
// param value MessageType -> the message type to reply to the
// request with
func (request *ProxyRequest) SetReplyType(value messagetypes.MessageType) {
	request.ReplyType = value
}

// GetTimeout gets the Timeout property from a ProxyRequest's properties map
// Timeout is a timespan property and indicates the timeout for a specific request
//
// returns time.Duration -> the duration for a ProxyRequest's timeout from its properties map
func (request *ProxyRequest) GetTimeout() time.Duration {
	return request.GetTimeSpanProperty("Timeout")
}

// SetTimeout sets the Timeout property in a ProxyRequest's properties map
// Timeout is a timespan property and indicates the timeout for a specific request
//
// param value time.Duration -> the timeout duration to be set in a
// ProxyRequest's properties map
func (request *ProxyRequest) SetTimeout(value time.Duration) {
	request.SetTimeSpanProperty("Timeout", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (request *ProxyRequest) Clone() IProxyMessage {
	proxyRequest := NewProxyRequest()
	var messageClone IProxyMessage = proxyRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (request *ProxyRequest) CopyTo(target IProxyMessage) {
	request.ProxyMessage.CopyTo(target)
	if v, ok := target.(IProxyRequest); ok {
		v.SetTimeout(request.GetTimeout())
	}
}
