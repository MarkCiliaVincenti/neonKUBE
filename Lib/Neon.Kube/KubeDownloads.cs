﻿//-----------------------------------------------------------------------------
// FILE:	    KubeDownloads.cs
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
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Collections;
using Neon.Common;
using Neon.Net;

namespace Neon.Kube
{
    /// <summary>
    /// Kubernetes related component download URIs.
    /// </summary>
    public static class KubeDownloads
    {
        /// <summary>
        /// The <b>kubectl</b> binary download URI for Linux.
        /// </summary>
        public static readonly string KubeCtlLinuxUri = $"https://storage.googleapis.com/kubernetes-release/release/v{KubeVersions.KubernetesVersion}/linux/amd64/kubectl";

        /// <summary>
        /// The <b>kubectl</b> binary download URI for OS/X.
        /// </summary>
        public static readonly string KubeCtlOsxUri = $"https://storage.googleapis.com/kubernetes-release/release/v{KubeVersions.KubernetesVersion}/bin/darwin/amd64/kubectl";

        /// <summary>
        /// The <b>kubectl</b> binary download URI for Windows.
        /// </summary>
        public static readonly string KubeCtlWindowsUri = $"https://storage.googleapis.com/kubernetes-release/release/v{KubeVersions.KubernetesVersion}/bin/windows/amd64/kubectl.exe";

        /// <summary>
        /// The <b>kubeadm</b> binary download URI for Linux.
        /// </summary>
        public static readonly string KubeAdmLinuxUri = $"https://storage.googleapis.com/kubernetes-release/release/v{KubeVersions.KubernetesVersion}/linux/amd64/kubeadm";

        /// <summary>
        /// The <b>kubelet</b> binary download URI for Linux.
        /// </summary>
        public static readonly string KubeletLinuxUri = $"https://storage.googleapis.com/kubernetes-release/release/v{KubeVersions.KubernetesVersion}/linux/amd64/kubelet";

        /// <summary>
        /// The Helm binary URL for Linux.
        /// </summary>
        public static readonly string HelmLinuxUri = $"https://get.helm.sh/helm-v{KubeVersions.HelmVersion}-linux-amd64.tar.gz";

        /// <summary>
        /// The Helm binary URL for OS/X.
        /// </summary>
        public static readonly string HelmOsxUri = $"https://get.helm.sh/helm-v{KubeVersions.HelmVersion}-darwin-amd64.tar.gz";

        /// <summary>
        /// The Helm binary URL for Windows.
        /// </summary>
        public static readonly string HelmWindowsUri = $"https://get.helm.sh/helm-v{KubeVersions.HelmVersion}-windows-amd64.zip";

        /// <summary>
        /// The Calico RBAC rules download (YAML for kubectl).
        /// </summary>
        public static readonly string CalicoRbacYamlUri = $"https://docs.projectcalico.org/v{KubeVersions.CalicoVersion}/getting-started/kubernetes/installation/hosted/rbac-kdd.yaml";

        /// <summary>
        /// The Calico setup download (YAML for kubectl).
        /// </summary>
        public static readonly string CalicoSetupYamlUri = $"https://docs.projectcalico.org/v{KubeVersions.CalicoVersion}/manifests/calico.yaml";

        /// <summary>
        /// The Istio binary URL for Linux.
        /// </summary>
        public static readonly string IstioLinuxUri = $"https://github.com/istio/istio/releases/download/{KubeVersions.IstioVersion}/istioctl-{KubeVersions.IstioVersion}-linux-amd64.tar.gz";

        /// <summary>
        /// The URI for the public AWS S3 bucket where we persist cluster VM images 
        /// and other things.
        /// </summary>
        public const string NeonPublicBucketUri = "https://neon-public.s3.us-west-2.amazonaws.com";

        /// <summary>
        /// The URI for the cluster manifest (<see cref="ClusterManifest"/>) JSON file for the current
        /// neonKUBE cluster version.
        /// </summary>
        public const string NeonClusterManifestUri = NeonPublicBucketUri + "/cluster-manifests/neonkube-" + KubeVersions.NeonKubeVersion + ".json";

        /// <summary>
        /// The GitHub repository path where public node images will be published.
        /// </summary>
        public const string PublicNodeImageRepo = "nforgeio/neonKUBE-images";

        /// <summary>
        /// The GitHub repository path where pre-release node images will be published.
        /// </summary>
        public const string PrivateNodeImagesRepo = "nforgeio/neonKUBE-images-dev";

        /// <summary>
        /// Returns the default URI to be used for downloading the prepared neonKUBE virtual machine image 
        /// for the current neonKUBE cluster version.
        /// </summary>
        /// <param name="hostingEnvironment">Specifies the hosting environment.</param>
        /// <param name="readyToGo">Optionally indicates that we'll be provisioning a single node cluster using a ready-to-go image.</param>
        /// <param name="setupDebugMode">Optionally indicates that we'll be provisioning in debug mode.</param>
        /// <param name="baseImageName">
        /// Specifies the base image file name (but not the bucket and path) when <paramref name="setupDebugMode"/><c>==true</c>.
        /// For example: <b>ubuntu-20.04.1.hyperv.amd64.vhdx.gz.manifest</b>
        /// </param>
        /// <returns>The download URI or <c>null</c>.</returns>
        /// <remarks>
        /// <note>
        /// Only one of <paramref name="readyToGo"/> or <paramref name="setupDebugMode"/> may be passed as true.
        /// </note>
        /// </remarks>
        public static string GetDefaultNodeImageUri(HostingEnvironment hostingEnvironment, bool readyToGo = false, bool setupDebugMode = false, string baseImageName = null)
        {
            if (setupDebugMode && string.IsNullOrEmpty(baseImageName))
            {
                throw new NotSupportedException($"[{KubeSetupProperty.BaseImageName}] must be passed when [{nameof(setupDebugMode)}=true].");
            }

            if (readyToGo && setupDebugMode)
            {
                throw new NotSupportedException($"Only one of [{nameof(readyToGo)}] or [{nameof(setupDebugMode)}] may be passed as TRUE.");
            }

            var imageType     = setupDebugMode ? "base" : "node";
            var readyToGoPart = readyToGo ? "-readytogo" : string.Empty;

            switch (hostingEnvironment)
            {
                case HostingEnvironment.BareMetal:

                    throw new NotImplementedException("Cluster setup on bare-metal is not supported yet.");

                case HostingEnvironment.Aws:
                case HostingEnvironment.Azure:
                case HostingEnvironment.Google:

                    if (setupDebugMode)
                    {
                        throw new NotSupportedException("Cluster setup debug mode is not supported for cloud environments.");
                    }

                    throw new NotImplementedException($"Node images are not available for the [{hostingEnvironment}] environment yet.");

                case HostingEnvironment.HyperV:
                case HostingEnvironment.HyperVLocal:

                    if (setupDebugMode)
                    {
                        return $"{NeonPublicBucketUri}/vm-images/hyperv/base/{baseImageName}";
                    }
                    else
                    {
                        return $"{NeonPublicBucketUri}/vm-images/hyperv/node/neonkube{readyToGoPart}-{KubeVersions.NeonKubeVersion}.hyperv.amd64.vhdx.gz.manifest";
                    }

                case HostingEnvironment.XenServer:

                    if (setupDebugMode)
                    {
                        return $"{NeonPublicBucketUri}/vm-images/xenserver/base/{baseImageName}";
                    }
                    else
                    {
                        return $"{NeonPublicBucketUri}/vm-images/xenserver/node/neonkube{readyToGoPart}-{KubeVersions.NeonKubeVersion}.xenserver.amd64.xva.gz.manifest";
                    }

                case HostingEnvironment.Wsl2:

                    if (setupDebugMode)
                    {
                        return $"{NeonPublicBucketUri}/vm-images/wsl2/base/{baseImageName}";
                    }
                    else
                    {
                       return $"{NeonPublicBucketUri}/vm-images/wsl2/node/neonkube{readyToGoPart}-{KubeVersions.NeonKubeVersion}.wsl2.amd64.tar.gz.manifest";
                    }

                default:

                    throw new NotImplementedException($"Node images are not implemented for the [{hostingEnvironment}] environment.");
            }
        }
    }
}
