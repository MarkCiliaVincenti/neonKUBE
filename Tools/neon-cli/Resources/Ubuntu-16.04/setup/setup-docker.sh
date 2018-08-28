#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         setup-docker.sh
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
#
# NOTE: This script must be run under [sudo].
#
# NOTE: Variables formatted like $<name> will be expanded by [neon-cli]
#       using a [PreprocessReader].
#
# This script handles the installation of the Docker CLI.

# Configure Bash strict mode so that the entire script will fail if 
# any of the commands fail.
#
#       http://redsymbol.net/articles/unofficial-bash-strict-mode/

set -euo pipefail

echo
echo "**********************************************" 1>&2
echo "** SETUP-DOCKER                             **" 1>&2
echo "**********************************************" 1>&2

# Load the hive configuration and setup utilities.

. $<load-hive-conf>
. setup-utility.sh

# Ensure that setup is idempotent.

startsetup docker

#--------------------------------------------------------------------------
# Note we're going to delete the docker unique key file if present.  Docker will
# generate a unique key the first time it starts.  It's possible that a key
# might be left over if the OS image was cloned.  Docker Swarm won't schedule
# containers on nodes with duplicate keys.

rm -f /etc/docker/key.json

#--------------------------------------------------------------------------
# We want the Docker containers, volumes, and other files to be located on
# the attached data drive (if there is one) rather than the OS drive.  Node
# preparation should have configured [/mnt-data] to be a link to the data
# drive or simply be a physical directory on the OS drive so we'll link
# the root Docker storage directory [/var/lib/docker] to [/mnt-data/docker]

if [ ! -d /mnt-data/docker ] ; then
	mkdir -p /mnt-data/docker
	ln -s /mnt-data/docker /var/lib/docker
fi

#--------------------------------------------------------------------------
# Install Docker
#
#   https://docs.docker.com/engine/installation/linux/ubuntulinux/
#
# Note that NEON_DOCKER_VERSION can be set to [latest] (the default),
# [test], [experimental], [development] or a version number like [17.03.0-ce].
#
# [latest], [test], and [experimental] install the current published
# releases as described at https://github.com/docker/docker/releases
# using the standard Docker setup scripts.
#
# Specifying a straight version number like [17.03.0-ce] installs a specific
# package, as described here:
#
#   https://docs.docker.com/engine/installation/linux/docker-ce/ubuntu/

# IMPORTANT!
#
# Production hives should install Docker with a specific version number
# to ensure that you'll be able to deploy additional hosts with the
# same Docker release as the rest of the hive.  This also prevents 
# the package manager from inadvertently upgrading Docker.

docker_version=

case "${NEON_DOCKER_VERSION}" in

test)

    curl -4fsSLv ${CURL_RETRY} https://test.docker.com/ | sh 1>&2
    touch ${NEON_STATE_FOLDER}/docker
    ;;

experimental)

    curl -4fsSLv ${CURL_RETRY} https://experimental.docker.com/ | sh 1>&2
    touch ${NEON_STATE_FOLDER}/docker
    ;;

latest)

    curl -4fsSLv ${CURL_RETRY} https://get.docker.com/ | sh 1>&2
    touch ${NEON_STATE_FOLDER}/docker
    ;;

*)
    # Specific Docker version requested.  We'll perform the 
    # actual installation below.

    docker_version=${NEON_DOCKER_VERSION}
    ;;

esac

if [ "${docker_version}" != "" ] ; then

	# Install prerequisites.

	safe-apt-get install -yq apt-transport-https ca-certificates curl software-properties-common

    # Download the Docker GPG key.

    curl -fsSL https://download.docker.com/linux/ubuntu/gpg | apt-key add -

	# Configure the stable repository only.  We don't currently support specifying a 
    # specific edge or testing release.

	add-apt-repository "deb [arch=amd64] https://download.docker.com/linux/ubuntu $(lsb_release -cs) stable"
	# add-apt-repository "deb [arch=amd64] https://download.docker.com/linux/ubuntu $(lsb_release -cs) edge"
	# add-apt-repository "deb [arch=amd64] https://download.docker.com/linux/ubuntu $(lsb_release -cs) testing"
	safe-apt-get update

    # We need to use [apt-cache madison docker-ce] to determine the fully qualified name
    # for the desired package.  This command produces output like:
    #
    #   docker-ce | 18.06.1~ce~3-0~ubuntu | https://download.docker.com/linux/ubuntu xenial/stable amd64 Packages
    #   docker-ce | 18.06.0~ce~3-0~ubuntu | https://download.docker.com/linux/ubuntu xenial/stable amd64 Packages
    #   docker-ce | 18.03.1~ce-0~ubuntu | https://download.docker.com/linux/ubuntu xenial/stable amd64 Packages
    #   docker-ce | 18.03.0~ce-0~ubuntu | https://download.docker.com/linux/ubuntu xenial/stable amd64 Packages
    #   docker-ce | 17.12.1~ce-0~ubuntu | https://download.docker.com/linux/ubuntu xenial/stable amd64 Packages
    #
    # We're going to grep for the first line that matches the Docker version and then extract The
    # fully qualified package name between the first and second "|" characters.

    set +euo pipefail
    package=$(apt-cache madison docker-ce | grep --max-count 1 ${docker_version} | cut -d '|' -f 2 - | xargs)
    set -euo pipefail

    if [ "${package}" == "" ] ; then

        echo "*** ERROR: [${docker_version}] is not a known Docker stable version." > /dev/stderr
        echo "           Here are the known Docker packages:"                       > /dev/stderr
        echo                                                                        > /dev/stderr
        apt-cache madison docker-ce                                                 > /dev/stderr
        exit 1
    fi
 
	safe-apt-get install -yq docker-ce=${package}
fi

#--------------------------------------------------------------------------
# Create a drop-in Docker systemd unit file with that has our custom options.

cat <<EOF > /etc/systemd/system/docker.service
# Modified from the original installed by Docker to add custom command
# line options generated by [neon-cli] as well as explicit restart options.

[Unit]
Description=Docker Application Container Engine
Documentation=https://docs.docker.com
After=network.target
After=
Requires=
Before=

[Service]
Type=notify
# The default is not to use systemd for cgroups because the delegate issues still
# exists and systemd currently does not support the cgroup feature set required
# for containers run by docker
ExecStart=/usr/bin/dockerd --data-root /mnt-data/docker $<docker.options>
ExecReload=/bin/kill -s HUP \$MAINPID
# Rate limit Docker restarts
Restart=always
RestartSec=2s
# Having non-zero Limit*s causes performance problems due to accounting overhead
# in the kernel. We recommend using cgroups to do container-local accounting.
LimitNOFILE=infinity
LimitNPROC=infinity
LimitCORE=infinity
# Uncomment TasksMax if your systemd version supports it.
# Only systemd 226 and above support this version.
TasksMax=infinity
TimeoutStartSec=0
# Set delegate yes so that systemd does not reset the cgroups of docker containers
Delegate=yes
# Kill only the docker process, not all processes in the cgroup
KillMode=process

[Install]
WantedBy=multi-user.target
EOF

# We need to do a [daemon-reload] so systemd will be aware of the new unit drop-in.

systemctl disable docker
systemctl daemon-reload

# Configure Docker to start on boot and then restart it to pick up the new options.

systemctl enable docker
systemctl restart docker

# We relocated the Docker graph root directory to [/mnt-data/docker] so we can
# remove any of the old files at the default location.

if [ -d /var/lib/docker ] ; then
	rm -r /var/lib/docker
fi

# Add the current user to the [docker] group so SUDO won't be necessary.

addgroup $<hive.rootuser> docker

# Prevent the package manager from automatically upgrading the Docker engine.

set +e      # Don't exit if the next command fails
apt-mark hold docker

# Indicate that the script has completed.

endsetup docker
