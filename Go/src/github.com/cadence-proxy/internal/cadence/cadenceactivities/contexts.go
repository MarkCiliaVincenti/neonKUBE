//-----------------------------------------------------------------------------
// FILE:		contexts.go
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

package cadenceactivities

import (
	"context"
	"sync"

	"go.uber.org/cadence/workflow"
)

var (
	mu sync.RWMutex

	// contextID is incremented (protected by a mutex) every time
	// a new cadence context.Context is created
	contextID int64
)

type (

	// ActivityContextsMap holds a thread-safe map[interface{}]interface{} of
	// cadence ActivityContextsMap with their contextID's
	ActivityContextsMap struct {
		sync.Map
	}

	// ActivityContext holds a Cadence activity
	// context, the registered activity function, and a context cancel function.
	// This struct is used as an intermediate for storing worklfow information
	// and state while registering and executing cadence activitys
	ActivityContext struct {
		ctx          context.Context
		workflowCtx  workflow.Context
		activityName *string
		cancelFunc   func()
	}
)

//----------------------------------------------------------------------------
// contextID methods

// NextContextID increments the global variable
// contextID by 1 and is protected by a mutex lock
func NextContextID() int64 {
	mu.Lock()
	contextID = contextID + 1
	defer mu.Unlock()

	return contextID
}

// GetContextID gets the value of the global variable
// contextID and is protected by a mutex Read lock
func GetContextID() int64 {
	mu.RLock()
	defer mu.RUnlock()

	return contextID
}

//----------------------------------------------------------------------------
// ActivityContext instance methods

// NewActivityContext is the default constructor
// for a ActivityContext struct
//
// returns *ActivityContext -> pointer to a newly initialized
// activity ExecutionContext in memory
func NewActivityContext(ctx ...interface{}) *ActivityContext {
	actx := new(ActivityContext)

	if len(ctx) > 0 {
		c := ctx[0]

		if v, ok := c.(workflow.Context); ok {
			actx.SetWorkflowContext(v)
			return actx
		}

		if v, ok := c.(context.Context); ok {
			actx.SetContext(v)
			return actx
		}
	}

	return actx
}

// GetContext gets a ActivityContext's context.Context
//
// returns context.Context -> a cadence context context
func (actx *ActivityContext) GetContext() context.Context {
	return actx.ctx
}

// SetContext sets a ActivityContext's context.Context
//
// param value context.Context -> a cadence activity context to be
// set as a ActivityContext's cadence context.Context
func (actx *ActivityContext) SetContext(value context.Context) {
	actx.ctx = value
}

// GetWorkflowContext gets a ActivityContext's workflow.Context
//
// returns workflow.Context -> a cadence context context
func (actx *ActivityContext) GetWorkflowContext() workflow.Context {
	return actx.workflowCtx
}

// SetWorkflowContext sets a ActivityContext's workflow.Context
//
// param value workflow.Context -> a cadence activity context to be
// set as a ActivityContext's cadence workflow.Context
func (actx *ActivityContext) SetWorkflowContext(value workflow.Context) {
	actx.workflowCtx = value
}

// GetActivityName gets a ActivityContext's activity function name
//
// returns *string -> a cadence activity function name
func (actx *ActivityContext) GetActivityName() *string {
	return actx.activityName
}

// SetActivityName sets a ActivityContext's activity function name
//
// param value *string -> a cadence activity function name
func (actx *ActivityContext) SetActivityName(value *string) {
	actx.activityName = value
}

// GetCancelFunction gets a ActivityContext's context cancel function
//
// returns func() -> a cadence activity context cancel function
func (actx *ActivityContext) GetCancelFunction() func() {
	return actx.cancelFunc
}

// SetCancelFunction sets a ActivityContext's cancel function
//
// param value func() -> a cadence activity context cancel function
func (actx *ActivityContext) SetCancelFunction(value func()) {
	actx.cancelFunc = value
}

//----------------------------------------------------------------------------
// ActivityContextsMap instance methods

// Add adds a new cadence context and its corresponding ContextId into
// the ActivityContextsMap map.  This method is thread-safe.
//
// param id int64 -> the long id passed to Cadence
// activity functions.  This will be the mapped key
//
// param actx *ActivityContext -> pointer to the new ActivityContex used to
// execute activity functions. This will be the mapped value
//
// returns int64 -> long id of the new cadence ActivityContext added to the map
func (actxs *ActivityContextsMap) Add(id int64, actx *ActivityContext) int64 {
	actxs.Store(id, actx)
	return id
}

// Remove removes key/value entry from the ActivityContextsMap map at the specified
// ContextId.  This is a thread-safe method.
//
// param id int64 -> the long id passed to Cadence
// activity functions.  This will be the mapped key
//
// returns int64 -> long id of the ActivityContext removed from the map
func (actxs *ActivityContextsMap) Remove(id int64) int64 {
	actxs.Delete(id)
	return id
}

// Get gets a ActivityContext from the ActivityContextsMap at the specified
// ContextID.  This method is thread-safe.
//
// param id int64 -> the long id passed to Cadence
// activity functions.  This will be the mapped key
//
// returns *ActivityContext -> pointer to ActivityContext with the specified id
func (actxs *ActivityContextsMap) Get(id int64) *ActivityContext {
	if v, ok := actxs.Load(id); ok {
		if _v, _ok := v.(*ActivityContext); _ok {
			return _v
		}
	}

	return nil
}
