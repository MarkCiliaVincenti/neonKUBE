﻿//-----------------------------------------------------------------------------
// FILE:	    HiveConst.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Net;

namespace Neon.Hive
{
    /// <summary>
    /// Important neonHIVE constants.
    /// </summary>
    /// <remarks>
    /// <note>
    /// <b>IMPORTANT:</b> These definitions must match those in the <b>$\Stack\Docker\Images\neonhive.sh</b>
    /// file.  You must manually update that file and then rebuild and push the containers
    /// as well as redeploy all hives from scratch.
    /// </note>
    /// </remarks>
    public static class HiveConst
    {
        /// <summary>
        /// Name for the built-in hive user that has the ability to manage other users.
        /// </summary>
        public const string RootUser = "root";

        /// <summary>
        /// Name of the standard hive <b>public</b> overlay network.
        /// </summary>
        public const string PublicNetwork = "neon-public";

        /// <summary>
        /// Name of the standard hive <b>private</b> overlay network.
        /// </summary>
        public const string PrivateNetwork = "neon-private";

        /// <summary>
        /// IP endpoint of the Docker embedded DNS server.
        /// </summary>
        public const string DockerDnsEndpoint = "127.0.0.11:53";

        /// <summary>
        /// Hostname of the Docker public registry.
        /// </summary>
        public const string DockerPublicRegistry = "docker.io";

        /// <summary>
        /// The default Vault transit key.
        /// </summary>
        public const string VaultTransitKey = "neon-transit";

        /// <summary>
        /// The root Vault Docker registry key.
        /// </summary>
        public const string VaultRegistryKey = "neon-secret/registry";

        /// <summary>
        /// The Vault Docker registry credentials key.
        /// </summary>
        public static readonly string VaultRegistryCredentialsKey = $"{VaultRegistryKey}/credentials";

        /// <summary>
        /// The port exposed by the <b>neon-proxy-public</b> and <b>neon-proxy-private</b>
        /// HAProxy service that server the proxy statistics pages.
        /// </summary>
        public const int HAProxyStatsPort = 1936;

        /// <summary>
        /// The relative URI for the HAProxy statistics pages.
        /// </summary>
        public const string HaProxyStatsUri = "/_stats?no-cache";

        /// <summary>
        /// The HAProxy unique ID generating format used for generating activity IDs.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The generated ID parts are:
        /// </para>
        /// <list type="table">
        /// <item>
        ///     <term><b>%ci</b></term>
        ///     <descrption>
        ///     Client IP address.
        ///     </descrption>
        /// </item>
        /// <item>
        ///     <term><b>%cp</b></term>
        ///     <descrption>
        ///     Client port number.
        ///     </descrption>
        /// </item>
        /// <item>
        ///     <term><b>%fi</b></term>
        ///     <descrption>
        ///     Proxy frontend IP address.
        ///     </descrption>
        /// </item>
        /// <item>
        ///     <term><b>%fp</b></term>
        ///     <descrption>
        ///     Proxy frontend port number.
        ///     </descrption>
        /// </item>
        /// <item>
        ///     <term><b>%Ts</b></term>
        ///     <descrption>
        ///     Timestamp.
        ///     </descrption>
        /// </item>
        /// <item>
        ///     <term><b>%rt</b></term>
        ///     <descrption>
        ///     Proxy request count.
        ///     </descrption>
        /// </item>
        /// </list>
        /// </remarks>
        public const string HAProxyUidFormat = "%{+X}o%ci:%cp_%fi:%fp_%Ts_%rt";

        /// <summary>
        /// The maximum number of manager nodes allowed in a neonHIVE.
        /// </summary>
        public const int MaxManagers = 5;

        /// <summary>
        /// Identifies the Git production branch.
        /// </summary>
        public const string GitProdBranch = "prod";

        /// <summary>
        /// Consul root key for hive globals variables and settings. 
        /// </summary>
        public static readonly string GlobalKey = "neon/global";

        /// <summary>
        /// Consul root key for the Local DNS service related values.
        /// </summary>
        public static readonly string ConsulDnsRootKey = "neon/dns";

        /// <summary>
        /// Consul root key for the Local DNS entry definitions.
        /// </summary>
        public static readonly string ConsulDnsEntriesKey = $"{ConsulDnsRootKey}/entries";

        /// <summary>
        /// Consul key for the Local DNS answer <b>hosts.txt</b> file.
        /// </summary>
        public static readonly string ConsulDnsHostsKey = $"{ConsulDnsRootKey}/answers/hosts.txt";

        /// <summary>
        /// Consul key for the Local DNS answer <b>hosts.md5</b> file.
        /// </summary>
        public static readonly string ConsulDnsHostsMd5Key = $"{ConsulDnsRootKey}/answers/hosts.md5";

        /// <summary>
        /// Consul key where hive dashboards are registered.
        /// </summary>
        public const string ConsulDashboardsKey = "neon/dashboards";

        /// <summary>
        /// Consul root key for the <b>neon-registry</b> service.
        /// </summary>
        public const string ConsulRegistryRootKey = "neon/service/neon-registry";

        /// <summary>
        /// Identifies the dashboard folder where built-in hive dashboards will reside.
        /// </summary>
        public const string DashboardSystemFolder = "system";

        /// <summary>
        /// Enumerates the Docker containers that may be deployed to a neonHIVE by name.
        /// </summary>
        public static IEnumerable<string> DockerContainers =>
            new List<string>()
            {
                "neon-log-host",
                "neon-log-metricbeat",
                "neon-registry-cache",
                "rabbitmq"
            };

        /// <summary>
        /// Enumerates the Docker services that may be deployed to a neonHIVE by name.
        /// </summary>
        public static IEnumerable<string> DockerServices =>
            new List<string>()
            {
                "neon-hive-manager",
                "neon-dns",
                "neon-dns-mon",
                "neon-log-collector",
                "neon-log-kibana",
                "neon-proxy-manager",
                "neon-proxy-private",
                "neon-proxy-public",
                "neon-proxy-vault"
            };

        /// <summary>
        /// Identifies the neonHIVE public Docker registry.
        /// </summary>
        public const string NeonPublicRegistry = "nhive";

        /// <summary>
        /// The folder where Docker writes secrets provisioned to a container.
        /// </summary>
        public const string ContainerSecretsFolder = "/var/run/secrets";

        /// <summary>
        /// Returns the hive host ports to be published by the <b>neon-proxy-public</b> service.
        /// </summary>
        public static readonly HiveProxyPorts PublicProxyPorts =
            new HiveProxyPorts(
                range: new HiveProxyPortRange(HiveHostPorts.ProxyPublicFirst, HiveHostPorts.ProxyPublicLast),
                ports: new List<int>()
                {
                    80,
                    443
                });

        /// <summary>
        /// Returns the hive host ports to be published by the <b>neon-proxy-private</b> service.
        /// </summary>
        public static readonly HiveProxyPorts PrivateProxyPorts =
            new HiveProxyPorts(
                range: new HiveProxyPortRange(HiveHostPorts.ProxyPrivateFirst, HiveHostPorts.ProxyPrivateLast),
                ports: null);

        /// <summary>
        /// The default username for component dashboards and management tools (like Ceph and RabbitMQ).
        /// </summary>
        public const string DefaultUsername = "sysadmin";

        /// <summary>
        /// The default password for component dashboards and management tools (like Ceph and RabbitMQ).
        /// </summary>
        public const string DefaultPassword = "password";
    }
}
