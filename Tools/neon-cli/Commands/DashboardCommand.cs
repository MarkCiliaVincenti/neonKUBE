﻿//-----------------------------------------------------------------------------
// FILE:	    DashboardCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Consul;
using Newtonsoft.Json;

using Neon.Cluster;
using Neon.Common;
using Neon.Net;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>dashboard</b> command.
    /// </summary>
    public class DashboardCommand : CommandBase
    {
        private const string usage = @"
Manages cluster dashboards.

USAGE:

    neon dashboard NAME                     - Show named dashboard
    neon dashboard get NAME                 - Prints a dashboard URL
    neon dashboard ls|list                  - Lists the dashboards
    neon dashboard rm|remove NAME           - Removes a dashboard
    neon dashboard set NAME URL [OPTIONS]   - Saves a dashboard

OPTIONS:

    --title=TITLE               - Optional dashboard title
    --folder=FOLDER             - Optional dashboard folder
    --description=DESCRIPTION   - Optional dashboard description

REMARKS:

Many dashboards will require proxy routes.  These will need to be 
registered elsewhere using [proxy] commands.  Note that the following
dashboard names are reserved for use as commands:

    get, list, ls, rm, remove, set
";
        private ClusterLogin    clusterLogin;
        private ClusterProxy    cluster;
        private HashSet<string> reserved;

        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "dashboard" }; }
        }

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override void Run(CommandLine commandLine)
        {
            clusterLogin = Program.ConnectCluster();
            cluster      = new ClusterProxy(clusterLogin);
            reserved     = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase)
            {
                "get",
                "list",
                "ls",
                "rm",
                "remove",
                "set"
            };

            var command = commandLine.Arguments.ElementAtOrDefault(0);

            switch (command)
            {
                case "get":

                    Get(commandLine);
                    break;

                case "ls":
                case "list":

                    List(commandLine);
                    break;

                case "rm":
                case "remove":

                    Remove(commandLine);
                    break;

                case "set":

                    Set(commandLine);
                    break;

                default:

                    Show(commandLine);
                    break;
            }
        }

        /// <inheritdoc/>
        public override DockerShimInfo Shim(DockerShim shim)
        {
            return new DockerShimInfo(isShimmed: false, ensureConnection: true);
        }

        /// <summary>
        /// Gets a dashboard.
        /// </summary>
        /// <param name="commandLine">The command line.</param>
        private void Get(CommandLine commandLine)
        {
            var name = commandLine.Arguments.ElementAtOrDefault(1);

            if (string.IsNullOrEmpty(name))
            {
                Console.Error.WriteLine("*** ERROR: Expected a NAME argument.");
                Program.Exit(1);
            }

            if (!ClusterDefinition.IsValidName(name) || reserved.Contains(name))
            {
                Console.Error.WriteLine($"*** ERROR: [{name}] is not a valid dashboard name.");
                Program.Exit(1);
            }

            name = name.ToLowerInvariant();

            var key       = GetDashboardConsulKey(name);
            var dashboard = cluster.Consul.KV.GetObjectOrDefault<ClusterDashboard>(key).Result;

            if (dashboard == null)
            {
                Console.Error.WriteLine($"*** ERROR: Dashboard [{name}] does not exist.");
                Program.Exit(1);
            }

            Console.Write(dashboard.Url);
        }

        /// <summary>
        /// Lists the dashboards.
        /// </summary>
        /// <param name="commandLine">The command line.</param>
        private void List(CommandLine commandLine)
        {
            var result = cluster.Consul.KV.ListOrDefault<ClusterDashboard>(NeonClusterConst.ConsulDashboardsKey).Result;

            Console.WriteLine();

            if (result == null)
            {
                Console.WriteLine("[0] dashboards");
                return;
            }

            var dashboards   = result.ToList();
            var maxNameWidth = dashboards.Max(d => d.Name.Length);

            Console.WriteLine($"[{dashboards.Count}] dashboards");
            Console.WriteLine();

            foreach (var dashboard in dashboards.OrderBy(d => d.Name))
            {
                var namePart = dashboard.Name + ":" + new string(' ', maxNameWidth - dashboard.Name.Length);

                Console.WriteLine($"{namePart} {dashboard.Url}");
            }
        }

        /// <summary>
        /// Returns the Consul key for a dashboard based on its name.
        /// </summary>
        /// <param name="name">The dashboard name.</param>
        /// <returns>The Consul key path.</returns>
        private string GetDashboardConsulKey(string name)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));

            return $"{NeonClusterConst.ConsulDashboardsKey}/{name}";
        }

        /// <summary>
        /// Removes a dashboard.
        /// </summary>
        /// <param name="commandLine">The command line.</param>
        private void Remove(CommandLine commandLine)
        {
            var name = commandLine.Arguments.ElementAtOrDefault(1);

            if (string.IsNullOrEmpty(name))
            {
                Console.Error.WriteLine("*** ERROR: Expected a NAME argument.");
                Program.Exit(1);
            }

            if (!ClusterDefinition.IsValidName(name) || reserved.Contains(name))
            {
                Console.Error.WriteLine($"*** ERROR: [{name}] is not a valid dashboard name.");
                Program.Exit(1);
            }

            name = name.ToLowerInvariant();

            var result = cluster.Consul.KV.ListOrDefault<ClusterDashboard>(NeonClusterConst.ConsulDashboardsKey).Result;

            if (result == null || result.Count(d => d.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase)) == 0)
            {
                Console.Error.WriteLine($"*** ERROR: Dashboard [{name}] does not exist.");
                Program.Exit(1);
            }

            cluster.Consul.KV.Delete(GetDashboardConsulKey(name)).Wait();
            Console.WriteLine($"Removed [{name}] dashboard.");
        }

        /// <summary>
        /// Sets a dashboard.
        /// </summary>
        /// <param name="commandLine">The command line.</param>
        private void Set(CommandLine commandLine)
        {
            var name = commandLine.Arguments.ElementAtOrDefault(1);
            var url  = commandLine.Arguments.ElementAtOrDefault(2);

            if (string.IsNullOrEmpty(name))
            {
                Console.Error.WriteLine("*** ERROR: Expected a NAME argument.");
                Program.Exit(1);
            }

            if (!ClusterDefinition.IsValidName(name) || reserved.Contains(name))
            {
                Console.Error.WriteLine($"*** ERROR: [{name}] is not a valid dashboard name.");
                Program.Exit(1);
            }

            name = name.ToLowerInvariant();

            if (string.IsNullOrEmpty(url))
            {
                Console.Error.WriteLine("*** ERROR: Expected a URL argument.");
                Program.Exit(1);
            }

            var title       = commandLine.GetOption("--title");
            var folder      = commandLine.GetOption("--folder");
            var description = commandLine.GetOption("--description");

            var key       = GetDashboardConsulKey(name);
            var dashboard = new ClusterDashboard()
            {
                Name        = name,
                Title       = title,
                Folder      = folder,
                Url         = url,
                Description = description
            };

            var errors = dashboard.Validate(cluster.Definition);

            if (errors.Count > 0)
            {
                foreach (var error in errors)
                {
                    Console.Error.WriteLine($"*** ERROR: {error}");
                }

                Program.Exit(1);
            }

            cluster.Consul.KV.PutObject(key, dashboard, Formatting.Indented).Wait();

            Console.WriteLine();
            Console.WriteLine($"Saved [{name}] dashboard.");
        }

        /// <summary>
        /// Opens the requested dashboard in a browser.
        /// </summary>
        /// <param name="commandLine">The command line.</param>
        private void Show(CommandLine commandLine)
        {
            var name = commandLine.Arguments.ElementAtOrDefault(0);

            name = name ?? "cluster";   // Default to the neonCLUSTER dashboard

            var dashboard = cluster.Consul.KV.GetObjectOrDefault<ClusterDashboard>($"{NeonClusterConst.ConsulDashboardsKey}/{name}").Result;

            if (dashboard == null)
            {
                Console.Error.WriteLine($"*** ERROR: Dashboard [{name}] does not exist.");
                Program.Exit(1);
            }

            if (!Uri.TryCreate(dashboard.Url, UriKind.Absolute, out var url))
            {
                Console.Error.WriteLine($"*** ERROR: Invalid dashboard [{nameof(dashboard.Url)}={dashboard.Url}].");
                Program.Exit(1);
            }

            if (url.Host.Equals("healthy-manager", StringComparison.InvariantCultureIgnoreCase))
            {
                // Special case the [health-manager] hostname by replacing it with
                // the IP address of a healthy cluster manager node.

                url = new Uri($"{url.Scheme}://{cluster.GetHealthyManager().PrivateAddress}:{url.Port}{url.PathAndQuery}");
            }

            NeonHelper.OpenBrowser(url.ToString());
        }
    }
}