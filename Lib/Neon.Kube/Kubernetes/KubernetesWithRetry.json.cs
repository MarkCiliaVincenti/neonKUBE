﻿//-----------------------------------------------------------------------------
// FILE:	    KubernetesWithRetry.json.cs
// CONTRIBUTOR: Auto-generated by [prebuilder] tool during pre-build event
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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

// WARNING: This file is automatically generated during the build.
//          Do not edit this manually.

#pragma warning disable CS1591  // Missing XML comment

using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Rest;

using k8s;
using k8s.Models;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;
using Neon.Retry;
using Neon.Tasks;
using Neon.Collections;

namespace Neon.Kube
{
    public partial class KubernetesWithRetry
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// JSON serializer settings for Kubernetes objects.
        /// </summary>
        public static Lazy<JsonSerializerSettings> JsonSerializerSettings =
            new Lazy<JsonSerializerSettings>(
                () =>
                {
                    var settings = new JsonSerializerSettings();

                    settings.Converters.Add(new StringEnumConverter(new CamelCaseNamingStrategy(), allowIntegerValues: false));

                    // Treat missing members as errors for strict parsing.

                    settings.MissingMemberHandling = MissingMemberHandling.Ignore;

                    // Allow cyclic data.

                    settings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;

                    // Serialize dates as UTC like: 2012-07-27T18:51:45Z

                    settings.DateFormatHandling   = DateFormatHandling.IsoDateFormat;
                    settings.DateTimeZoneHandling = DateTimeZoneHandling.Utc;

                    return settings;
                });

        /// <summary>
        /// Serializes a Kubernetes object to JSON text.
        /// </summary>
        /// <param name="value">The value to be serialized.</param>
        /// <param name="format">Output formatting option (defaults to <see cref="Formatting.None"/>).</param>
        /// <returns>The JSON text.</returns>
        public static string JsonSerialize(object value, Formatting format = Formatting.None)
        {
            return JsonConvert.SerializeObject(value, format, JsonSerializerSettings.Value);
        }

        /// <summary>
        /// Deserializes JSON text to a Kubernetes object.
        /// </summary>
        /// <typeparam name="T">The desired output type.</typeparam>
        /// <param name="json">The JSON text.</param>
        /// <returns>The parsed <typeparamref name="T"/>.</returns>
        public static T JsonDeserialize<T>(string json)
        {
            Covenant.Requires<ArgumentNullException>(json != null, nameof(json));

            return JsonConvert.DeserializeObject<T>(json, JsonSerializerSettings.Value);
        }
    }
}
