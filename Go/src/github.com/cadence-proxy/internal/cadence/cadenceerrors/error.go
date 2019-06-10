//-----------------------------------------------------------------------------
// FILE:		error.go
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

package cadenceerrors

import (
	"fmt"
)

type (

	// CadenceError is a struct used to pass errors
	// generated by calls to the cadence server from the
	// cadence-proxy to the Neon.Cadence Library.
	CadenceError struct {
		String *string `json:"String"`
		Type   *string `json:"Type"`
	}
)

// NewCadenceErrorEmpty is the default constructor for a CadenceError
//
// returns *CadenceError -> pointer to a newly initialized CadenceError
// in memory
func NewCadenceErrorEmpty() *CadenceError {
	return new(CadenceError)
}

// NewCadenceError is the constructor for a CadenceError
// when supplied parameters
//
// param errStr string -> pointer to the error string
//
// param errorType ...interface{} -> the cadence error type
func NewCadenceError(errStr string, errType ...CadenceErrorType) *CadenceError {
	cadenceError := NewCadenceErrorEmpty()
	cadenceError.String = &errStr

	if len(errType) > 0 {
		cadenceError.SetType(errType[0])
	} else {
		cadenceError.SetType(Custom)
	}

	return cadenceError
}

// GetType gets the CadenceErrorType from a CadenceError
// instance
//
// returns CadenceErrorType -> the corresponding error type to the
// string representing the error type in a CadenceError instance
func (c *CadenceError) GetType() CadenceErrorType {
	if c.Type == nil {
		err := fmt.Errorf("no error type set")
		panic(err)
	}

	switch *c.Type {
	case "cancelled":
		return Cancelled
	case "custom":
		return Custom
	case "generic":
		return Generic
	case "panic":
		return Panic
	case "terminated":
		return Terminated
	case "timeout":
		return Timeout
	default:
		err := fmt.Errorf("unrecognized error type %v", *c.Type)
		panic(err)
	}
}

// SetType sets the *string to the corresponding CadenceErrorType
// in a CadenceError instance
//
// param errorType CadenceErrorType -> the CadenceErrorType to set as a string
// in a CadenceError instance
func (c *CadenceError) SetType(errorType CadenceErrorType) {
	var typeString string
	switch errorType {
	case Cancelled:
		typeString = "cancelled"
	case Custom:
		typeString = "custom"
	case Generic:
		typeString = "generic"
	case Panic:
		typeString = "panic"
	case Terminated:
		typeString = "terminated"
	case Timeout:
		typeString = "timeout"
	default:
		err := fmt.Errorf("unrecognized error type %s", errorType)
		panic(err)
	}

	c.Type = &typeString
}

// ToString returns the string representation of a CadenceError
//
// returns string -> a CadenceError as a string (CadenceError.String field)
func (c *CadenceError) ToString() string {
	return *c.String
}
