#!/bin/sh
#------------------------------------------------------------------------------
# FILE:         docker-entrypoint.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

# Add the root directory to the PATH.

PATH=${PATH}:/

# Load the Docker host node environment.

if [ ! -f /etc/neon/env-host ] ; then
    . log-critical.sh "The [/etc/neon/env-host] file does not exist.  This file must have been generated on the Docker host by [neon-cli] during hive setup and be bound to the container."
    exit 1
fi

. /etc/neon/env-host

# Launch the service.

neon-dns
