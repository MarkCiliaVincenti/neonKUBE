package messages

import (
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// ActivityGetHeartbeatDetailsReply is a ActivityReply of MessageType
	// ActivityGetHeartbeatDetailsReply.  It holds a reference to a ActivityReply in memory
	// and is the reply type to a ActivityGetHeartbeatDetailsRequest
	ActivityGetHeartbeatDetailsReply struct {
		*ActivityReply
	}
)

// NewActivityGetHeartbeatDetailsReply is the default constructor for
// a ActivityGetHeartbeatDetailsReply
//
// returns *ActivityGetHeartbeatDetailsReply -> a pointer to a newly initialized
// ActivityGetHeartbeatDetailsReply in memory
func NewActivityGetHeartbeatDetailsReply() *ActivityGetHeartbeatDetailsReply {
	reply := new(ActivityGetHeartbeatDetailsReply)
	reply.ActivityReply = NewActivityReply()
	reply.SetType(messagetypes.ActivityGetHeartbeatDetailsReply)

	return reply
}

// GetDetails gets the Activity heartbeat Details or nil
// from a ActivityGetHeartbeatDetailsReply's properties map.
// Returns the activity heartbeat details encoded as a byte array.
//
// returns []byte -> []byte representing the encoded activity heartbeat Details
func (reply *ActivityGetHeartbeatDetailsReply) GetDetails() []byte {
	return reply.GetBytesProperty("Details")
}

// SetDetails sets the Activity heartbeat Details or nil
// in a ActivityGetHeartbeatDetailsReply's properties map.
// Returns the activity heartbeat details encoded as a byte array.
//
// param value []byte -> []byte representing the encoded activity heartbeat
// Details, to be set in the ActivityGetHeartbeatDetailsReply's properties map
func (reply *ActivityGetHeartbeatDetailsReply) SetDetails(value []byte) {
	reply.SetBytesProperty("Details", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (reply *ActivityGetHeartbeatDetailsReply) Clone() IProxyMessage {
	activityGetHeartbeatDetailsReply := NewActivityGetHeartbeatDetailsReply()
	var messageClone IProxyMessage = activityGetHeartbeatDetailsReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *ActivityGetHeartbeatDetailsReply) CopyTo(target IProxyMessage) {
	reply.ActivityReply.CopyTo(target)
	if v, ok := target.(*ActivityGetHeartbeatDetailsReply); ok {
		v.SetDetails(reply.GetDetails())
	}
}
