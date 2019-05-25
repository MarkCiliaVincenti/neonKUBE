package messages

import (
	"time"

	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// ConnectRequest is ConnectRequest of MessageType
	// ConnectRequest.
	//
	// A ConnectRequest contains a reference to a
	// ProxyRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ProxyRequest
	ConnectRequest struct {
		*ProxyRequest
	}
)

// NewConnectRequest is the default constructor for a ConnectRequest
//
// returns *ConnectRequest -> a reference to a newly initialized
// ConnectRequest in memory
func NewConnectRequest() *ConnectRequest {
	request := new(ConnectRequest)
	request.ProxyRequest = NewProxyRequest()
	request.SetType(messagetypes.ConnectRequest)
	request.SetReplyType(messagetypes.ConnectReply)

	return request
}

// GetEndpoints gets a ConnectRequest's endpoints value from
// its nested properties map
//
// returns *string -> a pointer to a string in memory holding the value
// of a ConnectRequest's endpoints
func (request *ConnectRequest) GetEndpoints() *string {
	return request.GetStringProperty("Endpoints")
}

// SetEndpoints sets a ConnectionRequest's endpoints in
// its nested properties map
//
// param value *string -> a pointer to a string in memory
// that holds the value to be set in the properties map
func (request *ConnectRequest) SetEndpoints(value *string) {
	request.SetStringProperty("Endpoints", value)
}

// GetIdentity gets a ConnectRequest's identity value from
// its nested properties map
//
// returns *string -> a pointer to a string in memory holding the value
// of a ConnectRequest's identity
func (request *ConnectRequest) GetIdentity() *string {
	return request.GetStringProperty("Identity")
}

// SetIdentity sets a ConnectionRequest's identity in
// its nested properties map
//
// param value *string -> a pointer to a string in memory
// that holds the value to be set in the properties map
func (request *ConnectRequest) SetIdentity(value *string) {
	request.SetStringProperty("Identity", value)
}

// GetClientTimeout gets the ClientTimeout property from a ConnectRequest's properties map
// ClientTimeout is a timespan property and indicates the timeout for a cadence client request
//
// returns time.Duration -> the duration for a ConnectRequest's timeout from its properties map
func (request *ConnectRequest) GetClientTimeout() time.Duration {
	return request.GetTimeSpanProperty("ClientTimeout", time.Second*30)
}

// SetClientTimeout sets the ClientTimeout property in a ConnectRequest's properties map
// ClientTimeout is a timespan property and indicates the timeout for a cadence client request
//
// param value time.Duration -> the timeout duration to be set in a
// ConnectRequest's properties map
func (request *ConnectRequest) SetClientTimeout(value time.Duration) {
	request.SetTimeSpanProperty("ClientTimeout", value)
}

// -------------------------------------------------------------------------
// ProxyMessage interface methods for implementing the ProxyMessage interface

// Clone inherits docs from ProxyRequest.Clone()
func (request *ConnectRequest) Clone() IProxyMessage {
	connectRequest := NewConnectRequest()
	var messageClone IProxyMessage = connectRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyRequest.CopyTo()
func (request *ConnectRequest) CopyTo(target IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
	if v, ok := target.(*ConnectRequest); ok {
		v.SetEndpoints(request.GetEndpoints())
		v.SetIdentity(request.GetIdentity())
		v.SetClientTimeout(request.GetClientTimeout())
	}
}

// SetProxyMessage inherits docs from ProxyRequest.SetProxyMessage()
func (request *ConnectRequest) SetProxyMessage(value *ProxyMessage) {
	request.ProxyRequest.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ProxyRequest.GetProxyMessage()
func (request *ConnectRequest) GetProxyMessage() *ProxyMessage {
	return request.ProxyRequest.GetProxyMessage()
}

// GetRequestID inherits docs from ProxyRequest.GetRequestID()
func (request *ConnectRequest) GetRequestID() int64 {
	return request.ProxyRequest.GetRequestID()
}

// SetRequestID inherits docs from ProxyRequest.SetRequestID()
func (request *ConnectRequest) SetRequestID(value int64) {
	request.ProxyRequest.SetRequestID(value)
}

// GetType inherits docs from ProxyRequest.GetType()
func (request *ConnectRequest) GetType() messagetypes.MessageType {
	return request.ProxyRequest.GetType()
}

// SetType inherits docs from ProxyRequest.SetType()
func (request *ConnectRequest) SetType(value messagetypes.MessageType) {
	request.ProxyRequest.SetType(value)
}

// -------------------------------------------------------------------------
// ProxyRequest interface methods for implementing the ProxyRequest interface

// GetReplyType inherits docs from ProxyRequest.GetReplyType()
func (request *ConnectRequest) GetReplyType() messagetypes.MessageType {
	return request.ReplyType
}

// SetReplyType inherits docs from ProxyRequest.SetReplyType()
func (request *ConnectRequest) SetReplyType(value messagetypes.MessageType) {
	request.ProxyRequest.SetReplyType(value)
}

// GetTimeout inherits docs from ProxyRequest.GetTimeout()
func (request *ConnectRequest) GetTimeout() time.Duration {
	return request.ProxyRequest.GetTimeout()
}

// SetTimeout inherits docs from ProxyRequest.SetTimeout()
func (request *ConnectRequest) SetTimeout(value time.Duration) {
	request.ProxyRequest.SetTimeout(value)
}
