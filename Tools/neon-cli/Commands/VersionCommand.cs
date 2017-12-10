﻿//-----------------------------------------------------------------------------
// FILE:	    VersionCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;

using Neon.Cluster;
using Neon.Common;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>version</b> command.
    /// </summary>
    public class VersionCommand : CommandBase
    {
        private const string usage = @"
Prints the program version.

USAGE:

    neon version [-n] [--get]

OPTIONS:

    -n      - Don't write a newline after version.
    --git   - Include the Git branch/commit information.
";
        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "version" }; }
        }

        /// <inheritdoc/>
        public override string[] ExtendedOptions
        {
            get { return new string[] { "-n", "--git" }; }
        }

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override void Run(CommandLine commandLine)
        {
            if (commandLine.HasHelpOption)
            {
                Console.WriteLine(usage);
                Program.Exit(0);
            }

            if (commandLine.HasOption("--git"))
            {
                Console.Write($"{Program.Version}/{Program.GitVersion}");
            }
            else
            {
                Console.Write(Program.Version);
            }

            if (!commandLine.HasOption("-n"))
            {
                Console.WriteLine();
            }
        }

        /// <inheritdoc/>
        public override ShimInfo Shim(DockerShim shim)
        {
            return new ShimInfo(isShimmed: true);
        }
    }
}
