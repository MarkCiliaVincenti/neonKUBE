﻿//-----------------------------------------------------------------------------
// FILE:	    KubeHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Win32;
using Microsoft.Rest;

using Newtonsoft.Json;

using k8s;
using k8s.Models;

using Neon.Common;
using Neon.Cryptography;
using Neon.Data;
using Neon.Deployment;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Net;
using Neon.Retry;
using Neon.SSH;
using Neon.Tasks;
using Neon.Windows;

namespace Neon.Kube
{
    /// <summary>
    /// cluster related utilties.
    /// </summary>
    public static class KubeHelper
    {
        private static INeonLogger          log = LogManager.Default.GetLogger(typeof(KubeHelper));
        private static string               orgKUBECONFIG;
        private static string               userHomeFolder;
        private static string               neonkubeHomeFolder;
        private static string               automationFolder;
        private static KubeConfig           cachedConfig;
        private static KubeConfigContext    cachedContext;
        private static string               cachedKubeConfigPath;
        private static string               cachedNeonKubeUserFolder;
        private static string               cachedRunFolder;
        private static string               cachedLogFolder;
        private static string               cachedTempFolder;
        private static string               cachedLoginsFolder;
        private static string               cachedPasswordsFolder;
        private static string               cachedCacheFolder;
        private static string               cachedDesktopFolder;
        private static string               cachedDesktopWsl2Folder;
        private static string               cachedDesktopHypervFolder;
        private static KubeClientConfig     cachedClientConfig;
        private static X509Certificate2     cachedClusterCertificate;
        private static string               cachedProgramFolder;
        private static string               cachedProgramDataFolder;
        private static string               cachedPwshPath;
        private static IStaticDirectory     cachedResources;
        private static string               cachedNodeImageFolder;
        private static string               cacheDashboardStateFolder;

        /// <summary>
        /// CURL command common options.
        /// </summary>
        public const string CurlOptions = "-4fsSL --retry 10 --retry-delay 30";

        /// <summary>
        /// Static constructor.
        /// </summary>
        static KubeHelper()
        {
            // Initialize the standard home and [.neonkube] folder paths for the current user.

            if (NeonHelper.IsWindows)
            {
                userHomeFolder = Path.Combine(Environment.GetEnvironmentVariable("USERPROFILE"));
            }
            else if (NeonHelper.IsLinux || NeonHelper.IsOSX)
            {
                userHomeFolder = Path.Combine(Environment.GetEnvironmentVariable("HOME"));
            }
            else
            {
                throw new NotSupportedException("Operating system not supported.");
            }
                
            neonkubeHomeFolder = Path.Combine(userHomeFolder, ".neonkube");
        }

        /// <summary>
        /// Clears all cached items.
        /// </summary>
        private static void ClearCachedItems()
        {
            cachedConfig              = null;
            cachedContext             = null;
            cachedKubeConfigPath      = null;
            cachedNeonKubeUserFolder  = null;
            cachedRunFolder           = null;
            cachedLogFolder           = null;
            cachedTempFolder          = null;
            cachedLoginsFolder        = null;
            cachedPasswordsFolder     = null;
            cachedCacheFolder         = null;
            cachedDesktopFolder       = null;
            cachedDesktopWsl2Folder   = null;
            cachedDesktopHypervFolder = null;
            cachedClientConfig        = null;
            cachedClusterCertificate  = null;
            cachedProgramFolder       = null;
            cachedProgramDataFolder   = null;
            cachedPwshPath            = null;
            cachedResources           = null;
            cachedNodeImageFolder     = null;
            cacheDashboardStateFolder = null;
        }

        /// <summary>
        /// Explicitly sets the class <see cref="INeonLogger"/> implementation.  This defaults to
        /// a reasonable value.
        /// </summary>
        /// <param name="log">The logger.</param>
        public static void SetLogger(INeonLogger log)
        {
            Covenant.Requires<ArgumentNullException>(log != null, nameof(log));

            KubeHelper.log = log;
        }

        /// <summary>
        /// Returns the <see cref="IStaticDirectory"/> for the assembly's resources.
        /// </summary>
        public static IStaticDirectory Resources
        {
            get
            {
                if (cachedResources != null)
                {
                    return cachedResources;
                }

                return cachedResources = Assembly.GetExecutingAssembly().GetResourceFileSystem("Neon.Kube.Resources");
            }
        }

        /// <summary>
        /// Reads a file as text, retrying if the file is already open.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <returns>The file text.</returns>
        /// <remarks>
        /// It's possible for the configuration file to be temporarily opened
        /// by another process (e.g. the neonDESKTOP application or a 
        /// command line tool).  Rather than throw an exception, we're going
        /// to retry the operation a few times.
        /// </remarks>
        internal static string ReadFileTextWithRetry(string path)
        {
            var retry = new LinearRetryPolicy(typeof(IOException), maxAttempts: 10, retryInterval: TimeSpan.FromMilliseconds(200));
            var text  = string.Empty;

            retry.Invoke(
                () =>
                {
                    text = File.ReadAllText(path);
                });

            return text;
        }

        /// <summary>
        /// Writes a file as text, retrying if the file is already open.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <param name="text">The text to be written.</param>
        /// <remarks>
        /// It's possible for the configuration file to be temporarily opened
        /// by another process (e.g. the neonDESKTOP application or a 
        /// command line tool).  Rather than throw an exception, we're going
        /// to retry the operation a few times.
        /// </remarks>
        internal static string WriteFileTextWithRetry(string path, string text)
        {
            var retry = new LinearRetryPolicy(typeof(IOException), maxAttempts: 10, retryInterval: TimeSpan.FromMilliseconds(200));

            retry.Invoke(
                () =>
                {
                    File.WriteAllText(path, text);
                });

            return text;
        }

        /// <summary>
        /// Returns the path to the current user's <b>.neonkube</b> folder.  This value does not
        /// change when automation mode is set to <see cref="KubeAutomationMode.Enabled"/> or
        /// <see cref="KubeAutomationMode.EnabledWithSharedCache"/>.
        /// </summary>
        public static string StandardNeonKubeFolder
        {
            get
            {
                Directory.CreateDirectory(neonkubeHomeFolder);
                return neonkubeHomeFolder;
            }
        }

        /// <summary>
        /// Returns the path to the current user's <b>.neonkube/automation</b> folder.  This value does
        /// not change when automation mode is set to <see cref="KubeAutomationMode.Enabled"/> or
        /// <see cref="KubeAutomationMode.EnabledWithSharedCache"/>.
        /// </summary>
        public static string StandardNeonKubeAutomationFolder
        {
            get
            {
                var path = Path.Combine(StandardNeonKubeFolder, "automation");

                Directory.CreateDirectory(path);
                return path;
            }
        }

        /// <summary>
        /// Accesses the neonDESKTOP client configuration.
        /// </summary>
        public static KubeClientConfig ClientConfig
        {
            get
            {
                if (cachedClientConfig != null)
                {
                    return cachedClientConfig;
                }

                var clientStatePath = Path.Combine(KubeHelper.DesktopFolder, "config.json");

                try
                {
                    cachedClientConfig = NeonHelper.JsonDeserialize<KubeClientConfig>(ReadFileTextWithRetry(clientStatePath));

                    ClientConfig.Validate();
                }
                catch
                {
                    // The file doesn't exist yet or could not be parsed, so we'll
                    // generate a new file with default settings.

                    cachedClientConfig = new KubeClientConfig();

                    SaveClientState();
                }

                return cachedClientConfig;
            }

            set
            {
                Covenant.Requires<ArgumentNullException>(value != null, nameof(value));

                value.Validate();
                cachedClientConfig = value;
                SaveClientState();
            }
        }

        /// <summary>
        /// Loads or reloads the <see cref="ClientConfig"/>.
        /// </summary>
        /// <returns>The client configuration.</returns>
        public static KubeClientConfig LoadClientConfig()
        {
            cachedClientConfig = null;

            return ClientConfig;
        }

        /// <summary>
        /// Persists the <see cref="ClientConfig"/> to disk.
        /// </summary>
        public static void SaveClientState()
        {
            var clientStatePath = Path.Combine(KubeHelper.DesktopFolder, "config.json");

            ClientConfig.Validate();
            WriteFileTextWithRetry(clientStatePath, NeonHelper.JsonSerialize(cachedClientConfig, Formatting.Indented));
        }

        /// <summary>
        /// Returns the <see cref="KubeClientPlatform"/> for the current workstation.
        /// </summary>
        public static KubeClientPlatform HostPlatform
        {
            get
            {
                if (NeonHelper.IsLinux)
                {
                    return KubeClientPlatform.Linux;
                }
                else if (NeonHelper.IsOSX)
                {
                    return KubeClientPlatform.Osx;
                }
                else if (NeonHelper.IsWindows)
                {
                    return KubeClientPlatform.Windows;
                }
                else
                {
                    throw new NotSupportedException("The current workstation opersating system is not supported.");
                }
            }
        }

        /// <summary>
        /// Determines whether a cluster hosting environment deploys to the cloud.
        /// </summary>
        /// <param name="hostingEnvironment">The hosting environment.</param>
        /// <returns><c>true</c> for cloud environments.</returns>
        public static bool IsCloudEnvironment(HostingEnvironment hostingEnvironment)
        {
            switch (hostingEnvironment)
            {
                // On-premise environments

                case HostingEnvironment.BareMetal:
                case HostingEnvironment.HyperV:
                case HostingEnvironment.HyperVLocal:
                case HostingEnvironment.XenServer:
                case HostingEnvironment.Wsl2:

                    return false;

                // Cloud environments

                case HostingEnvironment.Aws:
                case HostingEnvironment.Azure:
                case HostingEnvironment.Google:

                    return true;

                default:

                    throw new NotImplementedException("Unexpected hosting environment.");
            }
        }

        /// <summary>
        /// Determines whether a cluster hosting environment is available only for neonFORGE
        /// enterprise (closed-source) related projects.
        /// </summary>
        /// <param name="hostingEnvironment">The hosting environment.</param>
        /// <returns><c>true</c> for enteprise/closed-source related projects.</returns>
        public static bool IsEnterpriseEnvironment(HostingEnvironment hostingEnvironment)
        {
            switch (hostingEnvironment)
            {
                case HostingEnvironment.Wsl2:

                    return true;

                default:

                    return false;
            }
        }

        /// <summary>
        /// Determines whether a cluster hosting environment deploys on-premise.
        /// </summary>
        /// <param name="hostingEnvironment">The hosting environment.</param>
        /// <returns><c>true</c> for on-premise environments.</returns>
        public static bool IsOnPremiseEnvironment(HostingEnvironment hostingEnvironment)
        {
            return !IsCloudEnvironment(hostingEnvironment);
        }

        /// <summary>
        /// Determines whether a cluster hosting environment deploys to on-premise hypervisors.
        /// </summary>
        /// <param name="hostingEnvironment">The hosting environment.</param>
        /// <returns><c>true</c> for on-premise environments.</returns>
        /// <remarks>
        /// <note>
        /// Although <see cref="HostingEnvironment.Wsl2"/> is technically hosted on the Windows
        /// Hyper-V platform, we're not going to consider it to be hosted by a hypervisor because
        /// it's a special case.
        /// </note>
        /// </remarks>
        public static bool IsOnPremiseHypervisorEnvironment(HostingEnvironment hostingEnvironment)
        {
            return hostingEnvironment == HostingEnvironment.HyperV ||
                   hostingEnvironment == HostingEnvironment.HyperVLocal ||
                   hostingEnvironment == HostingEnvironment.XenServer;
        }

        /// <summary>
        /// Returns the current <see cref="KubeAutomationMode"/>.
        /// </summary>
        public static KubeAutomationMode AutomationMode { get; private set; } = KubeAutomationMode.Disabled;

        /// <summary>
        /// Returns the path to the standard automation folder within the user's <b>.neonkube</b>
        /// directory.  This doesn't change when a non <see cref="KubeAutomationMode.Disabled"/>
        /// mode is set.
        /// </summary>
        public static string StandardAutomationFolder => Path.Combine(neonkubeHomeFolder, "automation");

        /// <summary>
        /// Sets cluster deployment automation mode by specifying the folder where 
        /// cluster related assets such as the KubeConfig file, cluster login, logs,
        /// etc. are saved.
        /// </summary>
        /// <param name="mode">
        /// Passed as one of <see cref="KubeAutomationMode.Enabled"/> or <see cref="KubeAutomationMode.EnabledWithSharedCache"/>,
        /// where <see cref="KubeAutomationMode.Enabled"/> relocates all folders from the
        /// standard <b>$(USERPROFILE)\.neonkube</b> to <paramref name="folder"/> including
        /// the node image cache.  <see cref="KubeAutomationMode.EnabledWithSharedCache"/>
        /// continues to use the shared neon image cache to avoid downloading multiple
        /// copies of node images.
        /// </param>
        /// <param name="folder">
        /// <para>
        /// Specifies the root folder where global cluster-specific files will
        /// be folders will be located as long as automation mode is set.  You may
        /// pass an fully qualified or relative folder path.  Relative paths will be
        /// rooted at <b>$(USERPROFILE)/.neonkube/automation/</b>.
        /// </para>
        /// </param>
        public static void SetAutomationMode(KubeAutomationMode mode, string folder)
        {
            Covenant.Requires<ArgumentException>(mode != KubeAutomationMode.Disabled, nameof(mode));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(folder), nameof(folder));
            Covenant.Assert(AutomationMode == KubeAutomationMode.Disabled);
            Covenant.Assert(automationFolder == null);

            ClearCachedItems();

            if (!Path.IsPathFullyQualified(folder))
            {
                folder = Path.Combine(neonkubeHomeFolder, "automation", folder);
            }

            AutomationMode   = mode;
            automationFolder = folder;
            orgKUBECONFIG    = Environment.GetEnvironmentVariable("KUBECONFIG");

            var kubeFolder = Path.Combine(automationFolder, ".kube");

            // Remove any existing automation folder that was potentially left over
            // from a previous test or other automation run.

            if (Directory.Exists(folder))
            {
                NeonHelper.DeleteFolder(folder);
            }

            // Create the new automation folder and set the environment variable that
            // references where the Kubernetes config file will be located.

            Directory.CreateDirectory(kubeFolder);
            Environment.SetEnvironmentVariable("KUBECONFIG", Path.Combine(kubeFolder, "config"));
        }

        /// <summary>
        /// Resets the automation mode to <see cref="KubeAutomationMode.Disabled"/> and deletes
        /// the current automation folder and its contents.
        /// </summary>
        public static void ResetAutomationMode()
        {
            if (AutomationMode == KubeAutomationMode.Disabled)
            {
                return;
            }

            Covenant.Assert(automationFolder != null);
            NeonHelper.DeleteFolder(automationFolder);

            Environment.SetEnvironmentVariable("KUBECONFIG", orgKUBECONFIG);

            AutomationMode   = KubeAutomationMode.Disabled;
            automationFolder = null;
            orgKUBECONFIG    = null;

            ClearCachedItems();
        }

        /// <summary>
        /// Returns a special prefix based on <paramref name="prefix"/> that can be used to distinguish
        /// between automation related assets and those belonging to production clusters.  This is used
        /// by <b>KubernetesFixture</b> and custom tools to ensure that automation related cluster and
        /// file/folder names don't conflict.
        /// </summary>
        /// <param name="prefix">The prefix string.</param>
        /// <returns>A string like: (PREFIX)</returns>
        public static string AutomationPrefix(string prefix)
        {
            return $"({prefix})";
        }

        /// <summary>
        /// Returns the path the folder holding the user specific cluster login and other files.
        /// </summary>
        /// <returns>The folder path.</returns>
        public static string NeonKubeUserFolder
        {
            get
            {
                if (cachedNeonKubeUserFolder != null)
                {
                    return cachedNeonKubeUserFolder;
                }

                switch (AutomationMode)
                {
                    case KubeAutomationMode.Disabled:

                        cachedNeonKubeUserFolder = neonkubeHomeFolder;
                        break;

                    case KubeAutomationMode.Enabled:
                    case KubeAutomationMode.EnabledWithSharedCache:

                        Covenant.Assert(automationFolder != null);
                        cachedNeonKubeUserFolder = automationFolder;
                        break;

                    default:

                        throw new NotImplementedException();
                }

                Directory.CreateDirectory(cachedNeonKubeUserFolder);

                return cachedNeonKubeUserFolder;
            }
        }

        /// <summary>
        /// Returns the directory path where the [neon run CMD ...] will copy secrets and run the command.
        /// </summary>
        /// <returns>The folder path.</returns>
        public static string RunFolder
        {
            get
            {
                if (cachedRunFolder != null)
                {
                    return cachedRunFolder;
                }

                var path = Path.Combine(NeonKubeUserFolder, "run");

                Directory.CreateDirectory(path);

                return cachedRunFolder = path;
            }
        }

        /// <summary>
        /// Returns the default directory path where neon-cli logs will be written.
        /// </summary>
        /// <returns>The folder path.</returns>
        public static string LogFolder
        {
            get
            {
                if (cachedLogFolder != null)
                {
                    return cachedLogFolder;
                }

                var path = Path.Combine(NeonKubeUserFolder, "log");

                Directory.CreateDirectory(path);

                return cachedLogFolder = path;
            }
        }

        /// <summary>
        /// Returns the path the user specific neonKUBE temporary folder, creating the folder if it doesn't already exist.
        /// </summary>
        /// <returns>The folder path.</returns>
        /// <remarks>
        /// This folder will exist on developer/operator workstations that have used the <b>neon-cli</b>
        /// to deploy and manage clusters.
        /// </remarks>
        public static string TempFolder
        {
            get
            {
                if (cachedTempFolder != null)
                {
                    return cachedTempFolder;
                }

                var path = Path.Combine(NeonKubeUserFolder, "temp");

                Directory.CreateDirectory(path);

                return cachedTempFolder = path;
            }
        }

        /// <summary>
        /// Returns the path to the Kubernetes configuration file.
        /// </summary>
        public static string KubeConfigPath
        {
            get
            {
                string kubeFolder;

                if (cachedKubeConfigPath != null)
                {
                    return cachedKubeConfigPath;
                }

                switch (AutomationMode)
                {
                    case KubeAutomationMode.Disabled:

                        kubeFolder = Path.Combine(userHomeFolder, ".kube");

                        Directory.CreateDirectory(kubeFolder);

                        return cachedKubeConfigPath = Path.Combine(kubeFolder, "config");

                    case KubeAutomationMode.Enabled:
                    case KubeAutomationMode.EnabledWithSharedCache:

                        Covenant.Assert(automationFolder != null);
                        
                        kubeFolder = Path.Combine(automationFolder, ".kube");

                        Directory.CreateDirectory(kubeFolder);

                        return cachedKubeConfigPath = Path.Combine(kubeFolder, "config");

                    default:

                        throw new NotImplementedException();
                }
            }
        }

        /// <summary>
        /// Returns the path the folder containing cluster login files, creating the folder 
        /// if it doesn't already exist.
        /// </summary>
        /// <returns>The folder path.</returns>
        /// <remarks>
        /// <para>
        /// This folder will exist on developer/operator workstations that have used the <b>neon-cli</b>
        /// to deploy and manage clusters.  Each known cluster will have a JSON file named
        /// <b><i>NAME</i>.context.json</b> holding the serialized <see cref="ClusterLogin"/> 
        /// information for the cluster, where <i>NAME</i> maps to a cluster configuration name
        /// within the <c>kubeconfig</c> file.
        /// </para>
        /// </remarks>
        public static string LoginsFolder
        {
            get
            {
                if (cachedLoginsFolder != null)
                {
                    return cachedLoginsFolder;
                }

                var path = Path.Combine(NeonKubeUserFolder, "logins");

                Directory.CreateDirectory(path);

                return cachedLoginsFolder = path;
            }
        }

        /// <summary>
        /// Returns path to the folder holding the encryption passwords.
        /// </summary>
        /// <returns>The folder path.</returns>
        public static string PasswordsFolder
        {
            get
            {
                if (cachedPasswordsFolder != null)
                {
                    return cachedPasswordsFolder;
                }

                var path = Path.Combine(NeonKubeUserFolder, "passwords");

                Directory.CreateDirectory(path);

                return cachedPasswordsFolder = path;
            }
        }

        /// <summary>
        /// Returns path to the neonDESKTOP application state folder.
        /// </summary>
        /// <returns>The folder path.</returns>
        public static string DesktopFolder
        {
            get
            {
                if (cachedDesktopFolder != null)
                {
                    return cachedDesktopFolder;
                }

                var path = Path.Combine(NeonKubeUserFolder, "desktop");

                Directory.CreateDirectory(path);

                return cachedDesktopFolder = path;
            }
        }

        /// <summary>
        /// Returns path to the neonDESKTOP WSL2 state folder.
        /// </summary>
        /// <returns>The folder path.</returns>
        public static string DesktopWsl2Folder
        {
            get
            {
                if (cachedDesktopWsl2Folder != null)
                {
                    return cachedDesktopWsl2Folder;
                }

                var path = Path.Combine(NeonKubeUserFolder, "desktop", "wsl2");

                Directory.CreateDirectory(path);

                return cachedDesktopWsl2Folder = path;
            }
        }

        /// <summary>
        /// Returns path to the neonDESKTOP WSL2 state folder.
        /// </summary>
        /// <returns>The folder path.</returns>
        public static string DesktopHypervFolder
        {
            get
            {
                if (cachedDesktopHypervFolder != null)
                {
                    return cachedDesktopHypervFolder;
                }

                var path = Path.Combine(NeonKubeUserFolder, "desktop", "hyperv");

                Directory.CreateDirectory(path);

                return cachedDesktopHypervFolder = path;
            }
        }

        /// <summary>
        /// Returns the path the folder containing cached files for various environments.
        /// </summary>
        /// <returns>The folder path.</returns>
        public static string CacheFolder
        {
            get
            {
                if (cachedCacheFolder != null)
                {
                    return cachedCacheFolder;
                }

                var path = Path.Combine(NeonKubeUserFolder, "cache");

                Directory.CreateDirectory(path);

                return cachedCacheFolder = path;
            }
        }

        /// <summary>
        /// Returns the path to the folder containing cached files for the specified platform.
        /// </summary>
        /// <param name="platform">Identifies the platform.</param>
        /// <returns>The folder path.</returns>
        public static string GetPlatformCacheFolder(KubeClientPlatform platform)
        {
            string subfolder;

            switch (platform)
            {
                case KubeClientPlatform.Linux:

                    subfolder = "linux";
                    break;

                case KubeClientPlatform.Osx:

                    subfolder = "osx";
                    break;

                case KubeClientPlatform.Windows:

                    subfolder = "windows";
                    break;

                default:

                    throw new NotImplementedException($"Platform [{platform}] is not implemented.");
            }

            var path = Path.Combine(CacheFolder, subfolder);

            Directory.CreateDirectory(path);

            return path;
        }

        /// <summary>
        /// Returns the path to the cached file for a specific named component with optional version.
        /// </summary>
        /// <param name="platform">Identifies the platform.</param>
        /// <param name="component">The component name.</param>
        /// <param name="version">The component version (or <c>null</c>).</param>
        /// <returns>The component file path.</returns>
        public static string GetCachedComponentPath(KubeClientPlatform platform, string component, string version)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(component), nameof(component));

            string path;

            if (string.IsNullOrEmpty(version))
            {
                path = Path.Combine(GetPlatformCacheFolder(platform), component);
            }
            else
            {
                path = Path.Combine(GetPlatformCacheFolder(platform), $"{component}-{version}");
            }

            return path;
        }

        /// <summary>
        /// Returns the path to the cluster login file path for a specific context
        /// by raw name.
        /// </summary>
        /// <param name="contextName">The kubecontext name.</param>
        /// <returns>The file path.</returns>
        public static string GetClusterLoginPath(KubeContextName contextName)
        {
            Covenant.Requires<ArgumentNullException>(contextName != null, nameof(contextName));

            // Kubecontext names may include a forward slash to specify a Kubernetes
            // namespace.  This won't work for a file name, so we're going to replace
            // any of these with a "~".

            var rawName = (string)contextName;

            return Path.Combine(LoginsFolder, $"{rawName.Replace("/", "~")}.login.yaml");
        }

        /// <summary>
        /// Returns the cluster login for the structured configuration name.
        /// </summary>
        /// <param name="name">The structured context name.</param>
        /// <returns>The <see cref="ClusterLogin"/> or <c>null</c>.</returns>
        public static ClusterLogin GetClusterLogin(KubeContextName name)
        {
            Covenant.Requires<ArgumentNullException>(name != null, nameof(name));

            var path = GetClusterLoginPath(name);

            if (!File.Exists(path))
            {
                return null;
            }

            var extension = NeonHelper.YamlDeserialize<ClusterLogin>(ReadFileTextWithRetry(path));

            extension.SetPath(path);
            extension.ClusterDefinition?.Validate();

            return extension;
        }

        /// <summary>
        /// Returns the path to the current user's cluster virtual machine node
        /// image cache, creating the directory if it doesn't already exist.
        /// </summary>
        /// <returns>The path to the cluster setup folder.</returns>
        public static string NodeImageFolder
        {
            get
            {
                if (cachedNodeImageFolder != null)
                {
                    return cachedNodeImageFolder;
                }

                var path = Path.Combine(NeonKubeUserFolder, "node-images");

                Directory.CreateDirectory(path);

                return cachedNodeImageFolder = path;
            }
        }

        /// <summary>
        /// Creates a new folder for holding neonDESKTOP dashboard browser state
        /// if it doesn't exist and returns its path.
        /// </summary>
        /// <returns>Path to the folder.</returns>
        public static string DashboardStateFolder
        {
            get
            {
                if (cacheDashboardStateFolder != null)
                {
                    return cacheDashboardStateFolder;
                }

                cacheDashboardStateFolder = Path.Combine(NeonKubeUserFolder, "dashboards");

                Directory.CreateDirectory(cacheDashboardStateFolder);

                return cacheDashboardStateFolder;
            }
        }

        /// <summary>
        /// Clears the contents of the dashboard state folder.
        /// </summary>
        public static void ClearDashboardStateFolder()
        {
            NeonHelper.DeleteFolderContents(cacheDashboardStateFolder);
        }

        /// <summary>
        /// Returns the path to the neon program folder.
        /// </summary>
        public static string ProgramFolder
        {
            get
            {
                if (cachedProgramFolder != null)
                {
                    return cachedProgramFolder;
                }

                cachedProgramFolder = Environment.GetEnvironmentVariable("NEONDESKTOP_PROGRAM_FOLDER");

                if (cachedProgramFolder == null)
                {
                    cachedProgramFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "neonFORGE", "neonDESKTOP");

                    // For some reason, [SpecialFolder.ProgramFiles] is returning: 
                    //
                    //      C:\Program Files (x86)
                    //
                    // We're going to strip off the " (x86)" part if present.

                    cachedProgramFolder = cachedProgramFolder.Replace(" (x86)", string.Empty);
                }

                if (!Directory.Exists(cachedProgramFolder))
                {
                    Directory.CreateDirectory(cachedProgramFolder);
                }

                return cachedProgramFolder;
            }
        }

        /// <summary>
        /// Returns the path to the neon program data folder.
        /// </summary>
        public static string ProgramDataFolder
        {
            get
            {
                if (cachedProgramDataFolder != null)
                {
                    return cachedProgramDataFolder;
                }

                cachedProgramDataFolder = Environment.GetEnvironmentVariable("NEONDESKTOP_PROGRAM_FOLDER");

                if (cachedProgramDataFolder == null)
                {
                    cachedProgramDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "neonFORGE", "neonDESKTOP");
                }

                if (!Directory.Exists(cachedProgramDataFolder))
                {
                    Directory.CreateDirectory(cachedProgramDataFolder);
                }

                return cachedProgramDataFolder;
            }
        }

        /// <summary>
        /// Returns the path to the Powershell Core executable to be used.
        /// This will first examine the <b>NEONDESKTOP_PROGRAM_FOLDER</b> environment
        /// variable to see if the installed version of Powershell Core should
        /// be used, otherwise it will simply return <b>pwsh.exe</b> so that
        /// the <b>PATH</b> will be searched.
        /// </summary>
        public static string PwshPath
        {
            get
            {
                if (cachedPwshPath != null)
                {
                    return cachedPwshPath;
                }

                if (!string.IsNullOrEmpty(ProgramFolder))
                {
                    var pwshPath = Path.Combine(ProgramFolder, "powershell", "pwsh.exe");

                    if (File.Exists(pwshPath))
                    {
                        return cachedPwshPath = pwshPath;
                    }
                }

                return cachedPwshPath = "pwsh.exe";
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the current assembly was built from the production <b>PROD</b> 
        /// source code branch.
        /// </summary>
#pragma warning disable 0436
        public static bool IsRelease => ThisAssembly.Git.Branch.StartsWith("release-", StringComparison.InvariantCultureIgnoreCase);
#pragma warning restore 0436

        /// <summary>
        /// Loads or reloads the Kubernetes configuration.
        /// </summary>
        /// <returns>The <see cref="Config"/>.</returns>
        public static KubeConfig LoadConfig()
        {
            cachedConfig = null;
            return Config;
        }

        /// <summary>
        /// Returns the user's current <see cref="Config"/>.
        /// </summary>
        public static KubeConfig Config
        {
            get
            {
                if (cachedConfig != null)
                {
                    return cachedConfig;
                }

                var configPath = KubeConfigPath;

                if (File.Exists(configPath))
                {
                    return cachedConfig = NeonHelper.YamlDeserialize<KubeConfig>(ReadFileTextWithRetry(configPath));
                }

                return cachedConfig = new KubeConfig();
            }
        }

        /// <summary>
        /// Rewrites the local kubeconfig file.
        /// </summary>
        /// <param name="config">The new configuration.</param>
        public static void SetConfig(KubeConfig config)
        {
            Covenant.Requires<ArgumentNullException>(config != null, nameof(config));

            cachedConfig = config;

            WriteFileTextWithRetry(KubeConfigPath, NeonHelper.YamlSerialize(config));
        }

        /// <summary>
        /// This is used for special situations for setting up a cluster to
        /// set an uninitialized Kubernetes config context as the current
        /// <see cref="CurrentContext"/>.
        /// </summary>
        /// <param name="context">The context being set or <c>null</c> to reset.</param>
        public static void InitContext(KubeConfigContext context = null)
        {
            cachedContext = context;
        }

        /// <summary>
        /// Sets the current Kubernetes config context.
        /// </summary>
        /// <param name="contextName">The context name of <c>null</c> to clear the current context.</param>
        /// <exception cref="ArgumentException">Thrown if the context specified doesn't exist.</exception>
        public static void SetCurrentContext(KubeContextName contextName)
        {
            if (contextName == null)
            {
                cachedContext         = null;
                Config.CurrentContext = null;
            }
            else
            {
                var newContext = Config.GetContext(contextName);

                if (newContext == null)
                {
                    throw new ArgumentException($"Kubernetes [context={contextName}] does not exist.", nameof(contextName));
                }

                if (!contextName.IsNeonKubeContext)
                {
                    throw new ArgumentException($"[{contextName}] is not a neonKUBE context.", nameof(contextName));
                }

                cachedContext         = newContext;
                Config.CurrentContext = (string)contextName;
            }

            cachedClusterCertificate = null;

            Config.Save();
        }

        /// <summary>
        /// Sets the current Kubernetes config context by string name.
        /// </summary>
        /// <param name="contextName">The context name of <c>null</c> to clear the current context.</param>
        /// <exception cref="ArgumentException">Thrown if the context specified doesn't exist.</exception>
        public static void SetCurrentContext(string contextName)
        {
            SetCurrentContext((KubeContextName)contextName);
        }

        /// <summary>
        /// Returns the <see cref="CurrentContext"/> for the connected cluster
        /// or <c>null</c> when there is no current context.
        /// </summary>
        public static KubeConfigContext CurrentContext
        {
            get
            {
                if (cachedContext != null)
                {
                    return cachedContext;
                }

                if (string.IsNullOrEmpty(Config.CurrentContext))
                {
                    return null;
                }
                else
                {
                    return Config.GetContext(Config.CurrentContext);
                }
            }
        }

        /// <summary>
        /// Returns the current context's <see cref="CurrentContextName"/> or <c>null</c>
        /// if there's no current context.
        /// </summary>
        public static KubeContextName CurrentContextName => CurrentContext == null ? null : KubeContextName.Parse(CurrentContext.Name);

        /// <summary>
        /// Returns the Kuberneties API service certificate for the current
        /// cluster context or <c>null</c> if we're not connected to a cluster.
        /// </summary>
        public static X509Certificate2 ClusterCertificate
        {
            get
            {
                if (cachedClusterCertificate != null)
                {
                    return cachedClusterCertificate;
                }

                if (CurrentContext == null)
                {
                    return null;
                }

                var cluster = KubeHelper.Config.GetCluster(KubeHelper.CurrentContext.Properties.Cluster);
                var certPem = Encoding.UTF8.GetString(Convert.FromBase64String(cluster.Properties.CertificateAuthorityData));
                var tlsCert = TlsCertificate.FromPemParts(certPem);

                return cachedClusterCertificate = tlsCert.ToX509();
            }
        }

        /// <summary>
        /// Returns the Kuberneties API client certificate for the current
        /// cluster context or <c>null</c> if we're not connected to a cluster.
        /// </summary>
        public static X509Certificate2 ClientCertificate
        {
            get
            {
                if (cachedClusterCertificate != null)
                {
                    return cachedClusterCertificate;
                }

                if (CurrentContext == null)
                {
                    return null;
                }

                var userContext = KubeHelper.Config.GetUser(KubeHelper.CurrentContext.Properties.User);
                var certPem     = Encoding.UTF8.GetString(Convert.FromBase64String(userContext.Properties.ClientCertificateData));
                var keyPem      = Encoding.UTF8.GetString(Convert.FromBase64String(userContext.Properties.ClientKeyData));
                var tlsCert     = TlsCertificate.FromPemParts(certPem, keyPem);
                var clientCert  = tlsCert.ToX509();

                return null;
            }
        }

        /// <summary>
        /// Generates a self-signed certificate for arbitrary hostnames, possibly including 
        /// hostnames with wildcards.
        /// </summary>
        /// <param name="hostname">
        /// <para>
        /// The certificate host names.
        /// </para>
        /// <note>
        /// You can use include a <b>"*"</b> to specify a wildcard
        /// certificate like: <b>*.test.com</b>.
        /// </note>
        /// </param>
        /// <param name="bitCount">The certificate key size in bits: one of <b>1024</b>, <b>2048</b>, or <b>4096</b> (defaults to <b>2048</b>).</param>
        /// <param name="validDays">
        /// The number of days the certificate will be valid.  This defaults to 365,000 days
        /// or about 1,000 years.
        /// </param>
        /// <param name="wildcard">
        /// Optionally generate a wildcard certificate for the subdomains of 
        /// <paramref name="hostname"/> or the combination of the subdomains
        /// and the hostname.  This defaults to <see cref="Wildcard.None"/>
        /// which does not generate a wildcard certificate.
        /// </param>
        /// <param name="issuedBy">Optionally specifies the issuer.</param>
        /// <param name="issuedTo">Optionally specifies who/what the certificate is issued for.</param>
        /// <param name="friendlyName">Optionally specifies the certificate's friendly name.</param>
        /// <returns>The new <see cref="TlsCertificate"/>.</returns>
        public static X509Certificate2 CreateSelfSigned(
            string      hostname,
            int         bitCount     = 2048,
            int         validDays    = 365000,
            Wildcard    wildcard     = Wildcard.None,
            string      issuedBy     = null,
            string      issuedTo     = null,
            string      friendlyName = null)
        {
            Covenant.Requires<ArgumentException>(!string.IsNullOrEmpty(hostname), nameof(hostname));
            Covenant.Requires<ArgumentException>(bitCount == 1024 || bitCount == 2048 || bitCount == 4096, nameof(bitCount));
            Covenant.Requires<ArgumentException>(validDays > 1, nameof(validDays));

            if (string.IsNullOrEmpty(issuedBy))
            {
                issuedBy = ".";
            }

            if (string.IsNullOrEmpty(issuedTo))
            {
                issuedTo = hostname;
            }

            var sanBuilder = new SubjectAlternativeNameBuilder();

            switch (wildcard)
            {
                case Wildcard.None:

                    sanBuilder.AddDnsName(hostname);
                    break;

                case Wildcard.SubdomainsOnly:

                    hostname = $"*.{hostname}";
                    sanBuilder.AddDnsName(hostname);
                    break;

                case Wildcard.RootAndSubdomains:

                    sanBuilder.AddDnsName(hostname);
                    sanBuilder.AddDnsName($"*.{hostname}");
                    break;
            }

            X500DistinguishedName distinguishedName = new X500DistinguishedName($"CN={hostname}");

            using (RSA rsa = RSA.Create(bitCount))
            {
                var request = new CertificateRequest(distinguishedName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

                request.CertificateExtensions.Add(
                    new X509KeyUsageExtension(X509KeyUsageFlags.KeyEncipherment |
                        X509KeyUsageFlags.CrlSign | X509KeyUsageFlags.KeyCertSign |
                        X509KeyUsageFlags.DigitalSignature, true));

                request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
                request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension());

                request.CertificateExtensions.Add(sanBuilder.Build());

                var certificate          = request.CreateSelfSigned(new DateTimeOffset(DateTime.UtcNow.AddDays(-1)), new DateTimeOffset(DateTime.UtcNow.AddDays(validDays)));
                certificate.FriendlyName = friendlyName;

                return certificate;
            }
        }

        /// <summary>
        /// Looks for a certificate with a friendly name.
        /// </summary>
        /// <param name="store">The certificate store.</param>
        /// <param name="friendlyName">The case insensitive friendly name.</param>
        /// <returns>The certificate or <c>null</c> if one doesn't exist by the name.</returns>
        private static X509Certificate2 FindCertificateByFriendlyName(X509Store store, string friendlyName)
        {
            Covenant.Requires<ArgumentNullException>(store != null, nameof(store));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(friendlyName), nameof(friendlyName));

            foreach (var certificate in store.Certificates)
            {
                if (friendlyName.Equals(certificate.FriendlyName, StringComparison.InvariantCultureIgnoreCase))
                {
                    return certificate;
                }
            }

            return null;
        }

        /// <summary>
        /// <para>
        /// Ensures that <b>kubectl</b> tool whose version is at least as great as the Kubernetes
        /// cluster version is installed to the <b>neonKUBE</b> programs folder by copying the
        /// tool from the cache if necessary.
        /// </para>
        /// <note>
        /// This will probably require elevated privileges.
        /// </note>
        /// <note>
        /// This assumes that <b>kubectl</b> has already been downloaded and cached and also that 
        /// more recent <b>kubectl</b> releases are backwards compatible with older deployed versions
        /// of Kubernetes.
        /// </note>
        /// </summary>
        public static void InstallKubeCtl()
        {
            var hostPlatform      = KubeHelper.HostPlatform;
            var cachedKubeCtlPath = KubeHelper.GetCachedComponentPath(hostPlatform, "kubectl", KubeVersions.KubernetesVersion);
            var targetPath        =  Path.Combine(KubeHelper.ProgramFolder);

            switch (hostPlatform)
            {
                case KubeClientPlatform.Windows:

                    targetPath = Path.Combine(targetPath, "kubectl.exe");

                    // Ensure that the KUBECONFIG environment variable exists and includes
                    // the path to the user's [.neonkube] configuration.

                    var kubeConfigVar = Environment.GetEnvironmentVariable("KUBECONFIG");

                    if (string.IsNullOrEmpty(kubeConfigVar))
                    {
                        // The [KUBECONFIG] environment variable doesn't exist so we'll set it.

#pragma warning disable CA1416
                        Registry.SetValue(@"HKEY_CURRENT_USER\Environment", "KUBECONFIG", KubeConfigPath, RegistryValueKind.ExpandString);
#pragma warning restore CA1416
                        Environment.SetEnvironmentVariable("KUBECONFIG", KubeConfigPath);
                    }
                    else
                    {
                        // The [KUBECONFIG] environment variable exists.  We need to ensure that the
                        // path to our [USER/.neonkube] config is present.  We're also going to ensure
                        // that no paths are duplicated within the variable.

                        var sb    = new StringBuilder();
                        var paths = new HashSet<string>();
                        var found = false;

                        foreach (var path in kubeConfigVar.Split(';', StringSplitOptions.RemoveEmptyEntries))
                        {
                            if (paths.Contains(path))
                            {
                                // Ignore duplicate paths.

                                continue;
                            }

                            if (path == KubeConfigPath)
                            {
                                found = true;
                            }

                            sb.AppendWithSeparator(path, ";");
                            paths.Add(path);
                        }

                        if (!found)
                        {
                            sb.AppendWithSeparator(KubeConfigPath, ";");
                        }

                        var newKubeConfigVar = sb.ToString();

                        if (newKubeConfigVar != kubeConfigVar)
                        {
#pragma warning disable CA1416
                            Registry.SetValue(@"HKEY_CURRENT_USER\Environment", "KUBECONFIG", newKubeConfigVar, RegistryValueKind.ExpandString);
#pragma warning restore CA1416
                            Environment.SetEnvironmentVariable("KUBECONFIG", newKubeConfigVar);
                        }
                    }

                    if (!File.Exists(targetPath))
                    {
                        File.Copy(cachedKubeCtlPath, targetPath);
                    }
                    else
                    {
                        // Execute the existing target to obtain its version and update it
                        // to the cached copy if the cluster installed a more recent version
                        // of Kubernetes.

                        // $hack(jefflill): Simple client version extraction

                        var pattern  = "GitVersion:\"v";
                        var response = NeonHelper.ExecuteCapture(targetPath, "version");
                        var pStart   = response.OutputText.IndexOf(pattern);
                        var error    = "Cannot identify existing [kubectl] version.";

                        if (pStart == -1)
                        {
                            throw new KubeException(error);
                        }

                        pStart += pattern.Length;

                        var pEnd = response.OutputText.IndexOf("\"", pStart);

                        if (pEnd == -1)
                        {
                            throw new KubeException(error);
                        }

                        var currentVersionString = response.OutputText.Substring(pStart, pEnd - pStart);

                        if (!Version.TryParse(currentVersionString, out var currentVersion))
                        {
                            throw new KubeException(error);
                        }

                        if (Version.Parse(KubeVersions.KubernetesVersion) > currentVersion)
                        {
                            // We need to copy the latest version.

                            NeonHelper.DeleteFile(targetPath);
                            File.Copy(cachedKubeCtlPath, targetPath);
                        }
                    }
                    break;

                case KubeClientPlatform.Linux:
                case KubeClientPlatform.Osx:
                default:

                    throw new NotImplementedException($"[{hostPlatform}] support is not implemented.");
            }
        }

        /// <summary>
        /// <para>
        /// Ensures that <b>helm</b> client installed on the workstation version is at least as
        /// great as the requested cluster version is installed to the <b>neonKUBE</b> programs 
        /// folder by copying the tool from the cache if necessary.
        /// </para>
        /// <note>
        /// This will probably require elevated privileges.
        /// </note>
        /// <note>
        /// This assumes that <b>Helm</b> has already been downloaded and cached and also that 
        /// more recent <b>Helm</b> releases are backwards compatible with older deployed versions
        /// of Tiller.
        /// </note>
        /// </summary>
        public static void InstallWorkstationHelm()
        {
            var hostPlatform   = KubeHelper.HostPlatform;
            var cachedHelmPath = KubeHelper.GetCachedComponentPath(hostPlatform, "helm", KubeVersions.HelmVersion);
            var targetPath     = Path.Combine(KubeHelper.ProgramFolder);

            switch (hostPlatform)
            {
                case KubeClientPlatform.Windows:

                    targetPath = Path.Combine(targetPath, "helm.exe");

                    if (!File.Exists(targetPath))
                    {
                        File.Copy(cachedHelmPath, targetPath);
                    }
                    else
                    {
                        // Execute the existing target to obtain its version and update it
                        // to the cached copy if the cluster installed a more recent version
                        // of Kubernetes.

                        // $hack(jefflill): Simple client version extraction

                        var pattern  = "Version:\"v";
                        var response = NeonHelper.ExecuteCapture(targetPath, "version");
                        var pStart   = response.OutputText.IndexOf(pattern);
                        var error    = "Cannot identify existing [helm] version.";

                        if (pStart == -1)
                        {
                            throw new KubeException(error);
                        }

                        pStart += pattern.Length;

                        var pEnd = response.OutputText.IndexOf("\"", pStart);

                        if (pEnd == -1)
                        {
                            throw new KubeException(error);
                        }

                        var currentVersionString = response.OutputText.Substring(pStart, pEnd - pStart);

                        if (!Version.TryParse(currentVersionString, out var currentVersion))
                        {
                            throw new KubeException(error);
                        }
                    }
                    break;

                case KubeClientPlatform.Linux:
                case KubeClientPlatform.Osx:
                default:

                    throw new NotImplementedException($"[{hostPlatform}] support is not implemented.");
            }
        }

        /// <summary>
        /// Executes a <b>kubectl</b> command on the local workstation.
        /// </summary>
        /// <param name="args">The command arguments.</param>
        /// <returns>The <see cref="ExecuteResponse"/>.</returns>
        public static ExecuteResponse Kubectl(params object[] args)
        {
            return NeonHelper.ExecuteCapture("kubectl", args);
        }

        /// <summary>
        /// Executes a <b>kubectl port-forward</b> command on the local workstation.
        /// </summary>
        /// <param name="serviceName">The service to forward.</param>
        /// <param name="remotePort">The service port.</param>
        /// <param name="localPort">The local port to forward to.</param>
        /// <param name="namespace">The Kubernetes namespace where the service is running.</param>
        /// <param name="process">The <see cref="Process"/> to use.</param>
        /// <returns>The <see cref="ExecuteResponse"/>.</returns>
        public static void PortForward(string serviceName, int remotePort, int localPort, string @namespace, Process process)
        {
            Task.Run(() => NeonHelper.ExecuteAsync("kubectl",
                args: new string[]
                {
                    "--namespace", @namespace,
                    "port-forward",
                    $"svc/{serviceName}",
                    $"{localPort}:{remotePort}"
                },
                process: process));
        }

        /// <summary>
        /// Executes a command in a k8s pod.
        /// </summary>
        /// <param name="client">The <see cref="Kubernetes"/> client to use.</param>
        /// <param name="pod">The pod where the command should run.</param>
        /// <param name="namespace">The namespace where the pod is running.</param>
        /// <param name="command">The command to run.</param>
        /// <returns>The command result.</returns>
        public static async Task<string> ExecuteInPod(IKubernetes client, V1Pod pod, string @namespace, string[] command)
        {
            var webSocket = await client.WebSocketNamespacedPodExecAsync(pod.Metadata.Name, @namespace, command, pod.Spec.Containers[0].Name);
            var demux = new StreamDemuxer(webSocket);

            demux.Start();

            var buff   = new byte[4096];
            var stream = demux.GetStream(1, 1);
            var read   = stream.Read(buff, 0, 4096);

            return Encoding.Default.GetString(buff.Where(b => b != 0).ToArray());
        }

        /// <summary>
        /// Looks up a password given its name.
        /// </summary>
        /// <param name="passwordName">The password name.</param>
        /// <returns>The password value.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the password doesn't exist.</exception>
        public static string LookupPassword(string passwordName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(passwordName), nameof(passwordName));

            var path = Path.Combine(PasswordsFolder, passwordName);

            if (!File.Exists(path))
            {
                throw new KeyNotFoundException(passwordName);
            }

            return File.ReadAllText(path).Trim();
        }

        /// <summary>
        /// <para>
        /// Packages the files within a folder into an ISO file.
        /// </para>
        /// <note>
        /// This requires Powershell to be installed and this will favor using the version of
        /// Powershell installed along with the neon-cli, if present.
        /// </note>
        /// </summary>
        /// <param name="inputFolder">Path to the input folder.</param>
        /// <param name="isoPath">Path to the output ISO file.</param>
        /// <param name="label">Optionally specifies a volume label.</param>
        /// <exception cref="ExecuteException">Thrown if the operation failed.</exception>
        public static void CreateIsoFile(string inputFolder, string isoPath, string label = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(inputFolder), nameof(inputFolder));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(isoPath), nameof(isoPath));
            Covenant.Requires<ArgumentException>(!inputFolder.Contains('"'), nameof(inputFolder));      // We don't escape quotes below so we'll
            Covenant.Requires<ArgumentException>(!isoPath.Contains('"'), nameof(isoPath));              // reject paths including quotes.

            label = label ?? string.Empty;

            // We're going to use a function from the Microsoft Technet Script Center:
            //
            //      https://gallery.technet.microsoft.com/scriptcenter/New-ISOFile-function-a8deeffd

            const string newIsoFileFunc =
@"function New-IsoFile  
{  
  <#  
   .Synopsis  
    Creates a new .iso file  
   .Description  
    The New-IsoFile cmdlet creates a new .iso file containing content from chosen folders  
   .Example  
    New-IsoFile ""c:\tools"",""c:Downloads\utils""  
    This command creates a .iso file in $env:temp folder (default location) that contains c:\tools and c:\downloads\utils folders. The folders themselves are included at the root of the .iso image.  
   .Example 
    New-IsoFile -FromClipboard -Verbose 
    Before running this command, select and copy (Ctrl-C) files/folders in Explorer first.  
   .Example  
    dir c:\WinPE | New-IsoFile -Path c:\temp\WinPE.iso -BootFile ""${env:ProgramFiles(x86)}\Windows Kits\10\Assessment and Deployment Kit\Deployment Tools\amd64\Oscdimg\efisys.bin"" -Media DVDPLUSR -Title ""WinPE"" 
    This command creates a bootable .iso file containing the content from c:\WinPE folder, but the folder itself isn't included. Boot file etfsboot.com can be found in Windows ADK. Refer to IMAPI_MEDIA_PHYSICAL_TYPE enumeration for possible media types: http://msdn.microsoft.com/en-us/library/windows/desktop/aa366217(v=vs.85).aspx  
   .Notes 
    NAME:  New-IsoFile  
    AUTHOR: Chris Wu 
    LASTEDIT: 03/23/2016 14:46:50  
#>  
  
  [CmdletBinding(DefaultParameterSetName='Source')]Param( 
    [parameter(Position=1,Mandatory=$true,ValueFromPipeline=$true, ParameterSetName='Source')]$Source,  
    [parameter(Position=2)][string]$Path = ""$env:temp\$((Get-Date).ToString('yyyyMMdd-HHmmss.ffff')).iso"",  
    [ValidateScript({Test-Path -LiteralPath $_ -PathType Leaf})][string]$BootFile = $null, 
    [ValidateSet('CDR','CDRW','DVDRAM','DVDPLUSR','DVDPLUSRW','DVDPLUSR_DUALLAYER','DVDDASHR','DVDDASHRW','DVDDASHR_DUALLAYER','DISK','DVDPLUSRW_DUALLAYER','BDR','BDRE')][string] $Media = 'DVDPLUSRW_DUALLAYER', 
    [string]$Title = (Get-Date).ToString(""yyyyMMdd-HHmmss.ffff""),  
    [switch]$Force, 
    [parameter(ParameterSetName='Clipboard')][switch]$FromClipboard 
  ) 
 
  Begin {  
    ($cp = new-object System.CodeDom.Compiler.CompilerParameters).CompilerOptions = '/unsafe' 
    if (!('ISOFile' -as [type])) {  
      Add-Type -CompilerParameters $cp -TypeDefinition @' 
public class ISOFile  
{ 
  public unsafe static void Create(string Path, object Stream, int BlockSize, int TotalBlocks)  
  {  
    int bytes = 0;  
    byte[] buf = new byte[BlockSize];  
    var ptr = (System.IntPtr)(&bytes);  
    var o = System.IO.File.OpenWrite(Path);  
    var i = Stream as System.Runtime.InteropServices.ComTypes.IStream;  
  
    if (o != null) { 
      while (TotalBlocks-- > 0) {  
        i.Read(buf, BlockSize, ptr); o.Write(buf, 0, bytes);  
      }  
      o.Flush(); o.Close();  
    } 
  } 
}  
'@  
    } 
  
    if ($BootFile) { 
      if('BDR','BDRE' -contains $Media) { Write-Warning ""Bootable image doesn't seem to work with media type $Media"" } 
      ($Stream = New-Object -ComObject ADODB.Stream -Property @{Type=1}).Open()  # adFileTypeBinary 
      $Stream.LoadFromFile((Get-Item -LiteralPath $BootFile).Fullname) 
      ($Boot = New-Object -ComObject IMAPI2FS.BootOptions).AssignBootImage($Stream) 
    } 
 
    $MediaType = @('UNKNOWN','CDROM','CDR','CDRW','DVDROM','DVDRAM','DVDPLUSR','DVDPLUSRW','DVDPLUSR_DUALLAYER','DVDDASHR','DVDDASHRW','DVDDASHR_DUALLAYER','DISK','DVDPLUSRW_DUALLAYER','HDDVDROM','HDDVDR','HDDVDRAM','BDROM','BDR','BDRE') 
 
    Write-Verbose -Message ""Selected media type is $Media with value $($MediaType.IndexOf($Media))"" 
    ($Image = New-Object -com IMAPI2FS.MsftFileSystemImage -Property @{VolumeName=$Title}).ChooseImageDefaultsForMediaType($MediaType.IndexOf($Media)) 
  
    if (!($Target = New-Item -Path $Path -ItemType File -Force:$Force -ErrorAction SilentlyContinue)) { Write-Error -Message ""Cannot create file $Path. Use -Force parameter to overwrite if the target file already exists.""; break } 
  }  
 
  Process { 
    if($FromClipboard) { 
      if($PSVersionTable.PSVersion.Major -lt 5) { Write-Error -Message 'The -FromClipboard parameter is only supported on PowerShell v5 or higher'; break } 
      $Source = Get-Clipboard -Format FileDropList 
    } 
 
    foreach($item in $Source) { 
      if($item -isnot [System.IO.FileInfo] -and $item -isnot [System.IO.DirectoryInfo]) { 
        $item = Get-Item -LiteralPath $item 
      } 
 
      if($item) { 
        Write-Verbose -Message ""Adding item to the target image: $($item.FullName)"" 
        try { $Image.Root.AddTree($item.FullName, $true) } catch { Write-Error -Message ($_.Exception.Message.Trim() + ' Try a different media type.') } 
      } 
    } 
  } 
 
  End {  
    if ($Boot) { $Image.BootImageOptions=$Boot }  
    $Result = $Image.CreateResultImage()  
    [ISOFile]::Create($Target.FullName,$Result.ImageStream,$Result.BlockSize,$Result.TotalBlocks) 
    Write-Verbose -Message ""Target image ($($Target.FullName)) has been created"" 
    $Target 
  } 
} 
";
            // Delete any existing ISO file.

            File.Delete(isoPath);

            // Use the version of Powershell installed along with the neon-cli, if present,
            // otherwise just launch Powershell from the PATH.

            var neonKubeProgramFolder = Environment.GetEnvironmentVariable("NEONDESKTOP_PROGRAM_FOLDER");
            var powershellPath        = "powershell";

            if (neonKubeProgramFolder != null)
            {
                var path = Path.Combine(neonKubeProgramFolder, powershellPath);

                if (File.Exists(path))
                {
                    powershellPath = path;
                }
            }

            // Generate a temporary script file and run it.

            using (var tempFile = new TempFile(suffix: ".ps1"))
            {
                var script = newIsoFileFunc;

                script += $"Get-ChildItem \"{inputFolder}\" | New-ISOFile -path \"{isoPath}\" -Title \"{label}\"";

                File.WriteAllText(tempFile.Path, script);

                var result = NeonHelper.ExecuteCapture(powershellPath,
                    new object[]
                    {
                        "-f", tempFile.Path
                    });

                result.EnsureSuccess();
            }
        }

        /// <summary>
        /// <para>
        /// Creates an ISO file containing the <b>neon-init.sh</b> script that 
        /// will be used for confguring the node on first boot.  This includes disabling
        /// the APT package update services, optionally setting a secure password for the
        /// <b>sysadmin</b> account, and configuring the network interface with a
        /// static IP address.
        /// </para>
        /// <para>
        /// This override has obtains network settings from a <see cref="ClusterDefinition"/>
        /// and <see cref="NodeDefinition"/>.
        /// </para>
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <param name="nodeDefinition">The node definition.</param>
        /// <param name="newPassword">Optionally specifies the new SSH password to be configured on the node.</param>
        /// <returns>A <see cref="TempFile"/> that references the generated ISO file.</returns>
        /// <remarks>
        /// <para>
        /// The hosting manager will call this for each node being prepared and then
        /// insert the ISO into the node VM's DVD/CD drive before booting the node
        /// for the first time.  The <b>neon-init</b> service configured on
        /// the corresponding node templates will look for this DVD and script and
        /// execute it early during the node boot process.
        /// </para>
        /// <para>
        /// The ISO file reference is returned as a <see cref="TempFile"/>.  The
        /// caller should call <see cref="TempFile.Dispose()"/> when it's done
        /// with the file to ensure that it is deleted.
        /// </para>
        /// </remarks>
        public static TempFile CreateNeonInitIso(
            ClusterDefinition   clusterDefinition,
            NodeDefinition      nodeDefinition,
            string              newPassword = null)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));
            Covenant.Requires<ArgumentNullException>(nodeDefinition != null, nameof(nodeDefinition));

            var clusterNetwork = clusterDefinition.Network;

            return CreateNeonInitIso(
                address:        nodeDefinition.Address,
                subnet:         clusterNetwork.PremiseSubnet,
                gateway:        clusterNetwork.Gateway,
                nameServers:    clusterNetwork.Nameservers,
                newPassword:    newPassword);
        }

        /// <summary>
        /// <para>
        /// Creates an ISO file containing the <b>neon-init.sh</b> script that 
        /// will be used for confguring the node on first boot.  This includes disabling
        /// the APT package update services, optionally setting a secure password for the
        /// <b>sysadmin</b> account, and configuring the network interface with a
        /// static IP address.
        /// </para>
        /// <para>
        /// This override has explict parameters for configuring the network.
        /// </para>
        /// </summary>
        /// <param name="address">The IP address to be assigned the the VM.</param>
        /// <param name="subnet">The network subnet to be configured.</param>
        /// <param name="gateway">The network gateway to be configured.</param>
        /// <param name="nameServers">The nameserver addresses to be configured.</param>
        /// <param name="newPassword">Optionally specifies the new SSH password to be configured on the node.</param>
        /// <returns>A <see cref="TempFile"/> that references the generated ISO file.</returns>
        /// <remarks>
        /// <para>
        /// The hosting manager will call this for each node being prepared and then
        /// insert the ISO into the node VM's DVD/CD drive before booting the node
        /// for the first time.  The <b>neon-init</b> service configured on
        /// the corresponding node templates will look for this DVD and script and
        /// execute it early during the node boot process.
        /// </para>
        /// <para>
        /// The ISO file reference is returned as a <see cref="TempFile"/>.  The
        /// caller should call <see cref="TempFile.Dispose()"/> when it's done
        /// with the file to ensure that it is deleted.
        /// </para>
        /// </remarks>
        public static TempFile CreateNeonInitIso(
            string              address,
            string              subnet,
            string              gateway,
            IEnumerable<string> nameServers,
            string              newPassword = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(address), nameof(address));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(subnet), nameof(subnet));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(gateway), nameof(gateway));
            Covenant.Requires<ArgumentNullException>(nameServers != null, nameof(nameServers));
            Covenant.Requires<ArgumentNullException>(nameServers.Count() > 0, nameof(nameServers));

            var sbNameservers = new StringBuilder();

            // Generate the [neon-init.sh] script.

            foreach (var nameserver in nameServers)
            {
                sbNameservers.AppendWithSeparator(nameserver.ToString(), ",");
            }

            var changePasswordScript =
$@"
#------------------------------------------------------------------------------
# Change the [sysadmin] user password from the hardcoded [sysadmin0000] password
# to something secure.  We're doing this here before the network is configured means 
# that there will be no time when bad guys can SSH into the node using the insecure
# password.

echo 'sysadmin:{newPassword}' | chpasswd
";
            if (String.IsNullOrWhiteSpace(newPassword))
            {
                // Clear the change password script when there's no password.

                changePasswordScript = "\r\n";
            }

            var nodePrepScript =
$@"# This script is called by the [neon-init] service when the prep DVD
# is inserted on boot.  This script handles setting a secure SSH password
# as well as configuring the network interface to a static IP address.
#
# The first parameter will be passed as the path where the DVD is mounted.

mountFolder=${{1}}

#------------------------------------------------------------------------------
# Sleep for a bit in an attempt to ensure that the system is actually ready.
#
# https://github.com/nforgeio/neonKUBE/issues/980

sleep 10

#------------------------------------------------------------------------------
# Disable the [apt-timer] and [apt-daily] services.  We're doing this 
# for two reasons:
#
#   1. These services interfere with with [apt-get] usage during
#      cluster setup and is also likely to interfere with end-user
#      configuration activities as well.
#
#   2. Automatic updates for production and even test clusters is
#      just not a great idea.  You just don't want a random update
#      applied in the middle of the night which might cause trouble.
#
# We're going to implement our own cluster updating machanism
# that will be smart enough to update the nodes such that the
# impact on cluster workloads will be limited.

systemctl stop apt-daily.timer
systemctl mask apt-daily.timer

systemctl stop apt-daily.service
systemctl mask apt-daily.service

# It may be possible for the auto updater to already be running so we'll
# wait here for it to release any lock files it holds.

while fuser /var/{{lib /{{dpkg,apt/lists}},cache/apt/archives}}/lock; do
    sleep 1
done
{changePasswordScript}
#------------------------------------------------------------------------------
# Configure the network.

echo ""Configure network: {address}""

# Make a backup copy of any original netplan files to the [/etc/neon-init/netplan-backup]
# folder so it will be possible to restore these if we need to reset the [neon-init] state.

mkdir -p /etc/neon-init/netplan-backup
cp -r /etc/netplan/* /etc/neon-init/netplan-backup

# Remove any existing netplan files so we can update the configuration.

rm /etc/netplan/*

cat <<EOF > /etc/netplan/static.yaml
# Static network configuration is initialized during first boot by the 
# [neon-init] service from a virtual ISO inserted during cluster prepare.

network:
  version: 2
  renderer: networkd
  ethernets:
    eth0:
     dhcp4: no
     addresses: [{address}/{NetworkCidr.Parse(subnet).PrefixLength}]
     gateway4: {gateway}
     nameservers:
       addresses: [{sbNameservers}]
EOF

echo ""Restart network""

while true; do

    netplan apply
    if [ ! $? ] ; then
        echo ""ERROR: Network restart failed.""
        sleep 1
    else
        break
fi

done

echo ""Node is prepared.""
exit 0
";
            nodePrepScript = NeonHelper.ToLinuxLineEndings(nodePrepScript);

            // Create an ISO that includes the script and return the ISO TempFile.
            //
            // NOTE:
            //
            // that the ISO needs to be created in an unencrypted folder so that Hyper-V 
            // can mount it to a VM.  By default, [neon-cli] will redirect the [TempFolder] 
            // and [TempFile] classes locate their folder and files here:
            //
            //      /USER/.neonkube/...     - which is encrypted on Windows

            var orgTempPath = Path.GetTempPath();

            using (var tempFolder = new TempFolder(folder: orgTempPath))
            {
                File.WriteAllText(Path.Combine(tempFolder.Path, "neon-init.sh"), nodePrepScript);

                // Note that the ISO needs to be created in an unencrypted folder
                // (not /USER/neonkube/...) so that Hyper-V can mount it to a VM.
                //
                var isoFile = new TempFile(suffix: ".iso", folder: orgTempPath);

                KubeHelper.CreateIsoFile(tempFolder.Path, isoFile.Path, "cidata");

                return isoFile;
            }
        }

        /// <summary>
        /// Returns the path to the <b>ssh-keygen.exe</b> tool to be used for creating
        /// and managing SSH keys.
        /// </summary>
        /// <returns>The fully qualified path to the executable.</returns>
        /// <exception cref="FileNotFoundException">Thrown if the executable could not be found.</exception>
        private static string GetSshKeyGenPath()
        {
            // The version of [ssh-keygen.exe] included with later versions of Windows doesn't
            // work for us because it cannot create a key without a passphrase when called from
            // a script or other process.
            //
            // We're going to use a version of this tool deployed with the Git tools for Windows.
            // This will be installed with neonDESKTOP and is also available as part of the
            // neonKUBE Git repo as a fall back for Neon developers that haven't installed 
            // the desktop yet.

            // Look for the installed version first.

            var desktopProgramFolder = Environment.GetEnvironmentVariable("NEONDESKTOP_PROGRAM_FOLDER");
            var path1                = desktopProgramFolder != null ? Path.Combine(Environment.GetEnvironmentVariable("NEONDESKTOP_PROGRAM_FOLDER"), "SSH", "ssh-keygen.exe") : null;

            if (path1 != null && File.Exists(path1))
            {
                return Path.GetFullPath(path1);
            }

            // Fall back to the executable from our Git repo.

            var repoFolder = Environment.GetEnvironmentVariable("NF_ROOT");
            var path2      = repoFolder != null ? Path.Combine(repoFolder, "External", "SSH", "ssh-keygen.exe") : null;

            if (path2 != null && File.Exists(path2))
            {
                return Path.GetFullPath(path2);
            }

            throw new FileNotFoundException($"Cannot locate [ssh-keygen.exe] at [{path1}] or [{path2}].");
        }

        /// <summary>
        /// Creates a SSH key for a neonKUBE cluster.
        /// </summary>
        /// <param name="clusterName">The cluster name.</param>
        /// <param name="userName">Optionally specifies the user name (defaults to <b>root</b>).</param>
        /// <returns>A <see cref="KubeSshKey"/> holding the public and private parts of the key.</returns>
        public static KubeSshKey GenerateSshKey(string clusterName, string userName = "root")
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(clusterName), nameof(clusterName));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(userName), nameof(userName));

            var sshKeyGenPath = GetSshKeyGenPath();

            using (var tempFolder = new TempFolder(TempFolder))
            {
                //-------------------------------------------------------------
                // Generate and load the public and private keys.

                var result = NeonHelper.ExecuteCapture(sshKeyGenPath,
                    new object[]
                    {
                        "-t", "rsa",
                        "-b", "2048",
                        "-N", "''",
                        "-C", $"{userName}@{clusterName}",
                        "-f", Path.Combine(tempFolder.Path, "key")
                    });

                if (result.ExitCode != 0)
                {
                    throw new KubeException("Cannot generate SSH key:\r\n\r\n" + result.AllText);
                }

                var publicPUB = File.ReadAllText(Path.Combine(tempFolder.Path, "key.pub"));
                var privateOpenSSH = File.ReadAllText(Path.Combine(tempFolder.Path, "key"));

                //-------------------------------------------------------------
                // We also need the public key in PEM format.

                result = NeonHelper.ExecuteCapture(sshKeyGenPath,
                    new object[]
                    {
                        "-f", Path.Combine(tempFolder.Path, "key.pub"),
                        "-e",
                        "-m", "pem",
                    });

                if (result.ExitCode != 0)
                {
                    throw new KubeException("Cannot convert SSH public key to PEM:\r\n\r\n" + result.AllText);
                }

                var publicOpenSSH = result.OutputText;

                //-------------------------------------------------------------
                // Also convert the public key to SSH2 (RFC 4716).

                result = NeonHelper.ExecuteCapture(sshKeyGenPath,
                    new object[]
                    {
                        "-f", Path.Combine(tempFolder.Path, "key.pub"),
                        "-e",
                    });

                if (result.ExitCode != 0)
                {
                    throw new KubeException("Cannot convert SSH public key to SSH2:\r\n\r\n" + result.AllText);
                }

                var publicSSH2 = result.OutputText;

                // Strip out the comment header line if one was added during the conversion.

                var sbPublicSSH2 = new StringBuilder();

                using (var reader = new StringReader(publicSSH2))
                {
                    foreach (var line in reader.Lines())
                    {
                        if (!line.StartsWith("Comment: "))
                        {
                            sbPublicSSH2.AppendLine(line);
                        }
                    }
                }

                publicSSH2 = sbPublicSSH2.ToString();

                //-------------------------------------------------------------
                // We need the private key as PEM

                File.Copy(Path.Combine(tempFolder.Path, "key"), Path.Combine(tempFolder.Path, "key.pem"));

                result = NeonHelper.ExecuteCapture(sshKeyGenPath,
                    new object[]
                    {
                        "-f", Path.Combine(tempFolder.Path, "key.pem"),
                        "-p",
                        "-P", "''",
                        "-N", "''",
                        "-m", "pem",
                    });

                if (result.ExitCode != 0)
                {
                    throw new KubeException("Cannot convert SSH private key to PEM:\r\n\r\n" + result.AllText);
                }

                var privatePEM = File.ReadAllText(Path.Combine(tempFolder.Path, "key.pem"));

                //-------------------------------------------------------------
                // We need to obtain the MD5 fingerprint from the public key.

                result = NeonHelper.ExecuteCapture(sshKeyGenPath,
                    new object[]
                    {
                        "-l",
                        "-f", Path.Combine(tempFolder.Path, "key.pub"),
                        "-E", "md5",
                    });

                if (result.ExitCode != 0)
                {
                    throw new KubeException("Cannot generate SSH public key MD5 fingerprint:\r\n\r\n" + result.AllText);
                }

                var fingerprintMd5 = result.OutputText.Trim();

                //-------------------------------------------------------------
                // We also need the SHA256 fingerprint.

                result = NeonHelper.ExecuteCapture(sshKeyGenPath,
                    new object[]
                    {
                        "-l",
                        "-f", Path.Combine(tempFolder.Path, "key.pub"),
                        "-E", "sha256"
                    });

                if (result.ExitCode != 0)
                {
                    throw new KubeException("Cannot generate SSH public key SHA256 fingerprint:\r\n\r\n" + result.AllText);
                }

                var fingerprintSha2565 = result.OutputText.Trim();

                //-------------------------------------------------------------
                // Return the key information.

                return new KubeSshKey()
                {
                    PublicPUB         = publicPUB,
                    PublicOpenSSH     = publicOpenSSH,
                    PublicSSH2        = publicSSH2,
                    PrivateOpenSSH    = privateOpenSSH,
                    PrivatePEM        = privatePEM,
                    FingerprintMd5    = fingerprintMd5,
                    FingerprintSha256 = fingerprintSha2565
                };
            }
        }

        /// <summary>
        /// <para>
        /// Ensures that at least one cluster node is enabled for cluster ingress
        /// network traffic.
        /// </para>
        /// <note>
        /// It is possible for the user to have set <see cref="NodeDefinition.Ingress"/>
        /// to <c>false</c> for all nodes.  We're going to pick a reasonable set of
        /// nodes in this case.  I there are 3 or more workers, then only the workers
        /// will receive traffic, otherwise all nodes will receive traffic.
        /// </note>
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        public static void EnsureIngressNodes(ClusterDefinition clusterDefinition)
        {
            if (!clusterDefinition.Nodes.Any(node => node.Ingress))
            {
                var workerCount = clusterDefinition.Workers.Count();

                if (workerCount < 3)
                {
                    foreach (var node in clusterDefinition.Nodes)
                    {
                        node.Ingress = true;
                    }
                }
                else
                {
                    foreach (var worker in clusterDefinition.Workers)
                    {
                        worker.Ingress = true;
                    }
                }
            }
        }

        /// <summary>
        /// Returns the OpenSSH configuration file used for cluster nodes.
        /// </summary>
        public static string OpenSshConfig =>
@"# FILE:	       sshd_config
# CONTRIBUTOR: Jeff Lill
# COPYRIGHT:   Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
#
# Licensed under the Apache License, Version 2.0 (the ""License"");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
# http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an ""AS IS"" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.
#
# This file is written to neonKUBE nodes during cluster preparation.
#
# See the sshd_config(5) manpage for details

# Make it easy for operators to customize this config.
Include /etc/ssh/sshd_config.d/*

# What ports, IPs and protocols we listen for
# Port 22
# Use these options to restrict which interfaces/protocols sshd will bind to
# ListenAddress ::
# ListenAddress 0.0.0.0
Protocol 2
# HostKeys for protocol version 2
HostKey /etc/ssh/ssh_host_rsa_key
# HostKey /etc/ssh/ssh_host_dsa_key
# HostKey /etc/ssh/ssh_host_ecdsa_key
# HostKey /etc/ssh/ssh_host_ed25519_key
# Privilege Separation is turned on for security
UsePrivilegeSeparation yes

# Lifetime and size of ephemeral version 1 server key
KeyRegenerationInterval 3600
ServerKeyBits 1024

# Logging
SyslogFacility AUTH
LogLevel INFO

# Authentication:
LoginGraceTime 120
PermitRootLogin no
StrictModes yes

RSAAuthentication yes
PubkeyAuthentication yes
# AuthorizedKeysFile	%h/.ssh/authorized_keys

# Don't read the user's ~/.rhosts and ~/.shosts files
IgnoreRhosts yes
# For this to work you will also need host keys in /etc/ssh_known_hosts
RhostsRSAAuthentication no
# similar for protocol version 2
HostbasedAuthentication no
# Uncomment if you don't trust ~/.ssh/known_hosts for RhostsRSAAuthentication
# IgnoreUserKnownHosts yes

# To enable empty passwords, change to yes (NOT RECOMMENDED)
PermitEmptyPasswords no

# Change to yes to enable challenge-response passwords (beware issues with
# some PAM modules and threads)
ChallengeResponseAuthentication no

# Change to no to disable tunnelled clear text passwords
PasswordAuthentication yes

# Kerberos options
# KerberosAuthentication no
# KerberosGetAFSToken no
# KerberosOrLocalPasswd yes
# KerberosTicketCleanup yes

# GSSAPI options
# GSSAPIAuthentication no
# GSSAPICleanupCredentials yes

AllowTcpForwarding no
X11Forwarding no
X11DisplayOffset 10
PermitTunnel no
PrintMotd no
PrintLastLog yes
TCPKeepAlive yes
UsePrivilegeSeparation yes
# UseLogin no

# MaxStartups 10:30:60
# Banner /etc/issue.net

# Allow client to pass locale environment variables
AcceptEnv LANG LC_*

Subsystem sftp /usr/lib/openssh/sftp-server

# Set this to 'yes' to enable PAM authentication, account processing,
# and session processing. If this is enabled, PAM authentication will
# be allowed through the ChallengeResponseAuthentication and
# PasswordAuthentication.  Depending on your PAM configuration,
# PAM authentication via ChallengeResponseAuthentication may bypass
# the setting of ""PermitRootLogin without-password"".
# If you just want the PAM account and session checks to run without
# PAM authentication, then enable this but set PasswordAuthentication
# and ChallengeResponseAuthentication to 'no'.
UsePAM yes

# Allow connections to be idle for up to an 10 minutes (600 seconds)
# before terminating them.  This configuration pings the client every
# 30 seconds for up to 20 times without a response:
#
#   20*30 = 600 seconds

ClientAliveInterval 30
ClientAliveCountMax 20
TCPKeepAlive yes
";

        /// <summary>
        /// Verifies that the current user has administrator privileges.
        /// </summary>
        /// <param name="message">Optional message.</param>
        /// <exception cref="SecurityException">Thrown when the user does not have administrator privileges.</exception>
        public static void VerifyAdminPrivileges(string message = null)
        {
            if (NeonHelper.IsWindows)
            {
#pragma warning disable CA1416
                var principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());

                if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
                {
                    throw new SecurityException(message ?? "Admin privileges are required.");
                }
#pragma warning restore CA1416
            }
            else if (NeonHelper.IsOSX)
            {
                throw new NotImplementedException();
            }
            else if (NeonHelper.IsLinux)
            {
                throw new NotImplementedException();
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Ensures that the current process has enhanced admin privileges.
        /// </summary>
        /// <exception cref="KubeException">Thrown when enhanced privileges are not available.</exception>
        public static void EnsureAdminPrivileges()
        {
            if (NeonHelper.IsWindows)
            {
#pragma warning disable CA1416
                var principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());

                if (!principal.IsInRole(WindowsBuiltInRole.Administrator))
                {
                    throw new KubeException("Enhanced admin privileges are required but are not assigned.");
                }
#pragma warning restore CA1416
            }
            else if (NeonHelper.IsOSX)
            {
                // $todo(jefflill): Implement this?

                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Downloads a single part node image from a URI or a multi-part download to a local folder.
        /// </summary>
        /// <param name="imageUri">The node image URI.</param>
        /// <param name="imagePath">The local path where the image will be written.</param>
        /// <param name="progressAction">Optional progress action that will be called with operation percent complete.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The path to the downloaded file.</returns>
        /// <exception cref="SocketException">Thrown for network errors.</exception>
        /// <exception cref="HttpException">Thrown for HTTP network errors.</exception>
        /// <exception cref="OperationCanceledException">Thrown when the operation was cancelled.</exception>
        /// <remarks>
        /// <para>
        /// This supports source URIs referencing a single node image file as well
        /// as URIs referencing a multi-part <see cref="DownloadManifest"/> serialized as 
        /// JSON and with the <see cref="DeploymentHelper.DownloadManifestContentType"/>
        /// Content-Type.
        /// </para>
        /// </remarks>
        public static async Task<string> DownloadNodeImageAsync(
            string                      imageUri, 
            string                      imagePath,
            DownloadProgressDelegate    progressAction    = null, 
            CancellationToken           cancellationToken = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(imageUri), nameof(imageUri));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(imagePath), nameof(imagePath));

            await SyncContext.ClearAsync;

            // $note(jefflill):
            //
            // We're not going to use a retry policy here because restarting a multi-GB download potentially several
            // times is not likely to be what the user wants and the mult-part download is restartable and does retry.

            var imageFolder = Path.GetDirectoryName(imagePath);

            Directory.CreateDirectory(imageFolder);

            using (var client = new HttpClient())
            {
                // Execute a HEAD request on the source URI to obtain the content type.  We'll
                // use this to decide whether to download a single or multi-part file.

                var request  = new HttpRequestMessage(HttpMethod.Head, imageUri);
                var response = await client.SendAsync(request);

                response.EnsureSuccessStatusCode();

                if (string.Equals(response.Content.Headers.ContentType.MediaType, DeploymentHelper.DownloadManifestContentType, StringComparison.InvariantCultureIgnoreCase))
                {
                    // Multi-part download.

                    return await DownloadMultiPartNodeImageAsync(imageUri, imagePath, progressAction);
                }

                // Download the single part file.

                if (File.Exists(imagePath))
                {
                    return imagePath;
                }

                response = await client.GetAsync(imageUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken: cancellationToken);

                response.EnsureSuccessStatusCode();

                var contentLength = response.Content.Headers.ContentLength;

                try
                {
                    progressAction?.Invoke(DownloadProgressType.Download, 0);

                    using (var fileStream = new FileStream(imagePath, FileMode.Create, FileAccess.ReadWrite))
                    {
                        using (var downloadStream = await response.Content.ReadAsStreamAsync())
                        {
                            var buffer = new byte[64 * 1024];
                            int cb;

                            while (true)
                            {
                                cb = await downloadStream.ReadAsync(buffer, 0, buffer.Length);

                                if (cb == 0)
                                {
                                    break;
                                }

                                await fileStream.WriteAsync(buffer, 0, cb);

                                if (contentLength.HasValue)
                                {
                                    var percentComplete = (int)(((double)fileStream.Length / (double)contentLength) * 100.0);

                                    progressAction?.Invoke(DownloadProgressType.Download, percentComplete);
                                }
                            }
                        }
                    }

                    progressAction?.Invoke(DownloadProgressType.Download, 100);

                    return imagePath;
                }
                catch
                {
                    // Ensure that the template file is are deleted if there were any
                    // errors to help avoid using a corrupted template.

                    if (File.Exists(imagePath))
                    {
                        File.Delete(imagePath);
                    }

                    throw;
                }
            }
        }

        /// <summary>
        /// Downloads a multi-part node image to a local folder.
        /// </summary>
        /// <param name="imageUri">The node image multi-part download information URI.</param>
        /// <param name="imagePath">The local path where the image will be written.</param>
        /// <param name="progressAction">Optional progress action that will be called with operation percent complete.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The path to the downloaded file.</returns>
        /// <exception cref="SocketException">Thrown for network errors.</exception>
        /// <exception cref="HttpException">Thrown for HTTP network errors.</exception>
        /// <exception cref="OperationCanceledException">Thrown when the operation was cancelled.</exception>
        /// <remarks>
        /// <para>
        /// This checks to see if the target file already exists and will download
        /// only what's required to update the file to match the source.  This means
        /// that partially completed downloads can restart essentially where they
        /// left off.
        /// </para>
        /// </remarks>
        public static async Task<string> DownloadMultiPartNodeImageAsync(
            string                      imageUri, 
            string                      imagePath,
            DownloadProgressDelegate    progressAction    = null,
            CancellationToken           cancellationToken = default)
        {
            Covenant.Requires<ArgumentNullException>(imageUri != null, nameof(imageUri));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(imagePath), nameof(imagePath));

            await SyncContext.ClearAsync;

            var imageFolder = Path.GetDirectoryName(imagePath);

            Directory.CreateDirectory(imageFolder);

            // Download the URI and parse a [Download] instance from it.

            using (var client = new HttpClient())
            {
                var request     = new HttpRequestMessage(HttpMethod.Get, imageUri);
                var response    = await client.SendAsync(request, cancellationToken: cancellationToken);
                var contentType = response.Content.Headers.ContentType.MediaType;

                response.EnsureSuccessStatusCode();

                if (!string.Equals(contentType, DeploymentHelper.DownloadManifestContentType, StringComparison.InvariantCultureIgnoreCase))
                {
                    throw new KubeException($"[{imageUri}] has unsupported [Content-Type={contentType}].  [{DeploymentHelper.DownloadManifestContentType}] is expected.");
                }

                var jsonText = await response.Content.ReadAsStringAsync();
                var download = NeonHelper.JsonDeserialize<DownloadManifest>(jsonText);

                // Download the multi-part file.

                return await DeploymentHelper.DownloadMultiPartAsync(download, imagePath, progressAction, cancellationToken: cancellationToken);
            }
        }

        private static string harborImageSyncDisabled = "harbor-image-sync-disabled";

        /// <summary>
        /// Helper method to Get the Harbor image sync status.
        /// </summary>
        /// <returns></returns>
        public static async Task<bool> GetDisableHarborImageSyncAsync()
        {
            var k8s = new KubernetesWithRetry(KubernetesClientConfiguration.BuildDefaultConfig());

            var configs = await k8s.ListNamespacedConfigMapAsync(KubeNamespaces.NeonSystem, labelSelector: "neon-kv=true");

            if (!configs.Items.Any(c => c.Metadata.Name == harborImageSyncDisabled))
            {
                await k8s.CreateNamespacedConfigMapAsync(new V1ConfigMap()
                {
                    Metadata = new V1ObjectMeta()
                    {
                        Name              = harborImageSyncDisabled,
                        NamespaceProperty = KubeNamespaces.NeonSystem,
                        Labels            = new Dictionary<string, string>()
                        {
                            { "neon-kv", "true" }
                        }
                    },
                    Data = new Dictionary<string, string>()
                    {
                        { "value", "false" }
                    }
                },
                namespaceParameter: KubeNamespaces.NeonSystem);
            }

            var value = await k8s.ReadNamespacedConfigMapAsync(harborImageSyncDisabled, KubeNamespaces.NeonSystem);

            return bool.Parse(value.Data["value"]);
        }

        /// <summary>
        /// Helper method to set the Harbor image sync status.
        /// </summary>
        /// <returns></returns>
        public static async Task SetDisableHarborImageSyncAsync(bool value)
        {
            var k8s = new KubernetesWithRetry(KubernetesClientConfiguration.BuildDefaultConfig());

            var configMap = new V1ConfigMap()
            {
                Metadata = new V1ObjectMeta()
                {
                    Name              = harborImageSyncDisabled,
                    NamespaceProperty = KubeNamespaces.NeonSystem,
                    Labels            = new Dictionary<string, string>()
                    {
                        { "neon-kv", "true" }
                    }
                },
                Data = new Dictionary<string, string>()
                {
                    { "value", value.ToString() }
                }
            };

            var configs = await k8s.ListNamespacedConfigMapAsync(KubeNamespaces.NeonSystem, labelSelector: "neon-kv=true");

            if (!configs.Items.Any(c => c.Metadata.Name == harborImageSyncDisabled))
            {
                await k8s.CreateNamespacedConfigMapAsync(configMap, KubeNamespaces.NeonSystem);
            }
            else
            {
                await k8s.ReplaceNamespacedConfigMapAsync(configMap, configMap.Name(), KubeNamespaces.NeonSystem);

            }
        }
    }
}
