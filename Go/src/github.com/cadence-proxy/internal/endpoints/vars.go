//-----------------------------------------------------------------------------
// FILE:		vars.go
// CONTRIBUTOR: John C Burns
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
	"net/http"
	"sync"
	"time"

	"go.uber.org/zap"

	"github.com/cadence-proxy/internal/cadence/cadenceactivities"
	"github.com/cadence-proxy/internal/cadence/cadenceclient"
	"github.com/cadence-proxy/internal/cadence/cadenceworkers"
	"github.com/cadence-proxy/internal/cadence/cadenceworkflows"
	"github.com/cadence-proxy/internal/server"
)

var (
	mu sync.RWMutex

	// requestID is incremented (protected by a mutex) every time
	// a new request message is sent
	requestID int64

	// logger for all endpoints to utilize
	logger *zap.Logger

	// Instance is a pointer to the server instance of the current server that the
	// cadence-proxy is listening on.  This gets set in main.go
	Instance *server.Instance

	// httpClient is the HTTP client used to send requests
	// to the Neon.Cadence client
	httpClient = http.Client{}

	// replyAddress specifies the address that the Neon.Cadence library
	// will be listening on for replies from the cadence proxy
	replyAddress string

	// terminate is a boolean that will be set after handling an incoming
	// TerminateRequest.  A true value will indicate that the server instance
	// needs to gracefully shut down after handling the request, and a false value
	// indicates the server continues to run
	terminate bool

	// cadenceClientTimeout specifies the amount of time in seconds a reply has to be sent after
	// a request has been received by the cadence-proxy
	cadenceClientTimeout time.Duration = time.Minute

	// ClientHelper is a global variable that holds this cadence-proxy's instance
	// of the ClientHelper that will be used to create domain and workflow clients
	// that communicate with the cadence server
	clientHelper = cadenceclient.NewClientHelper()

	// ActivityContexts maps a int64 ContextId to the cadence
	// Activity Context passed to the cadence Activity functions.
	// The cadence-client will use contextIds to refer to specific
	// activity contexts when perfoming activity actions
	ActivityContexts = new(cadenceactivities.ActivityContextsMap)

	// Workers maps a int64 WorkerId to the cadence
	// Worker returned by the Cadence NewWorker() function.
	// This will be used to stop a worker via the
	// StopWorkerRequest.
	Workers = new(cadenceworkers.WorkersMap)

	// WorkflowContexts maps a int64 ContextId to the cadence
	// Workflow Context passed to the cadence Workflow functions.
	// The cadence-client will use contextIds to refer to specific
	// workflow ocntexts when perfoming workflow actions
	WorkflowContexts = new(cadenceworkflows.WorkflowContextsMap)

	// Operations is a map of operations used to track pending
	// cadence-client operations
	Operations = new(operationsMap)

	// Clients is a map of ClientHelpers to ClientID used to
	// store ClientHelpers to support multiple clients
	Clients = new(cadenceclient.ClientsMap)
)

//----------------------------------------------------------------------------
// RequestID thread-safe methods

// NextRequestID increments the package variable
// requestID by 1 and is protected by a mutex lock
func NextRequestID() int64 {
	mu.Lock()
	requestID = requestID + 1
	defer mu.Unlock()
	return requestID
}

// GetRequestID gets the value of the global variable
// requestID and is protected by a mutex Read lock
func GetRequestID() int64 {
	mu.RLock()
	defer mu.RUnlock()
	return requestID
}
