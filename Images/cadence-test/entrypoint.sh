#!/bin/bash -x

# Original code: Copyright (c) 2017 Uber Technologies, Inc.
# Modifications: Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
#
# Permission is hereby granted, free of charge, to any person obtaining a copy
# of this software and associated documentation files (the "Software"), to deal
# in the Software without restriction, including without limitation the rights
# to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
# copies of the Software, and to permit persons to whom the Software is
# furnished to do so, subject to the following conditions:
#
# The above copyright notice and this permission notice shall be included in
# all copies or substantial portions of the Software.
#
# THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
# IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
# FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
# AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
# LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
# OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
# THE SOFTWARE.

CADENCE_HOME=$1
UI_HOME=$2
SERVICES="history,matching,frontend,worker"

start_cassandra() {
    ./cassandra -R
}

wait_for_cassandra() {
    export HOST_IP=`hostname --ip-address`
    server=`echo $HOST_IP | awk -F ',' '{print $1}'`
    until cqlsh --cqlversion=3.4.4 $server < /dev/null; do
        echo 'waiting for cassandra to start up'
        sleep 1
    done
    echo 'cassandra started'
}

# start cassandra,
# wait for it to complete startup
start_cassandra
wait_for_cassandra

# start the frontend
export CADENCE_TCHANNEL_PEERS=$HOST_IP:7933
node $UI_HOME/server.js & ./cadence-server --root $CADENCE_HOME --env docker start --services=$SERVICES
