﻿//-----------------------------------------------------------------------------
// FILE:	    AnsibleCommand.Module.Context.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Consul;
using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using ICSharpCode.SharpZipLib.Zip;

using Neon.Cluster;
using Neon.Cryptography;
using Neon.Common;
using Neon.IO;
using Neon.Net;

namespace NeonCli
{
    /// <summary>
    /// Enumerates the output verbosity levels.
    /// </summary>
    public enum AnsibleVerbosity : int
    {
        /// <summary>
        /// Always writes output.
        /// </summary>
        Important = 0,

        /// <summary>
        /// Writes information messages.
        /// </summary>
        Info = 1,

        /// <summary>
        /// Writes trace messages.
        /// </summary>
        Trace = 2
    }

    /// <summary>
    /// Module execution state.
    /// </summary>
    public class ModuleContext
    {
        private List<string>    output = new List<string>();
        private List<string>    error  = new List<string>();

        /// <summary>
        /// The modulke name.
        /// </summary>
        [JsonIgnore]
        public string Module { get; set; }

        /// <summary>
        /// The output verbosity.
        /// </summary>
        [JsonIgnore]
        public AnsibleVerbosity Verbosity { get; set; } = AnsibleVerbosity.Important;

        /// <summary>
        /// The Ansible module arguments.
        /// </summary>
        [JsonIgnore]
        public JObject Arguments { get; set; }

        /// <summary>
        /// Indicates whether the model is being executed in Ansible <b>check mode</b>.
        /// </summary>
        [JsonIgnore]
        public bool CheckMode { get; set; }

        /// <summary>
        /// The cluster login.
        /// </summary>
        [JsonIgnore]
        public ClusterLogin Login { get; set; }

        /// <summary>
        /// Initializes the Ansible module arguments.
        /// </summary>
        /// <param name="argsPath">Path to the Ansible arguments file.</param>
        public void SetArguments(string argsPath)
        {
            Arguments = JObject.Parse(File.ReadAllText(argsPath));

            if (Arguments.TryGetValue<int>("_ansible_verbosity", out var ansibleVerbosity))
            {
                this.Verbosity = (AnsibleVerbosity)ansibleVerbosity;
            }
        }

        //-----------------------------------------------------------------
        // These standard output fields are described here:
        //
        //      http://docs.ansible.com/ansible/latest/common_return_values.html
        //
        // Note that we're not currently implementing the INTERNAL properties.

        /// <summary>
        /// For those modules that implement backup=no|yes when manipulating files, a path to the backup file created.
        /// </summary>
        [JsonProperty(PropertyName = "backup_file", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public string BackupFile { get; set; } = null;

        /// <summary>
        /// A boolean indicating if the task had to make changes.
        /// </summary>
        [JsonProperty(PropertyName = "changed", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(false)]
        public bool Changed { get; set; } = false;

        /// <summary>
        /// A boolean that indicates if the task was failed or not.
        /// </summary>
        [JsonProperty(PropertyName = "failed", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(false)]
        public bool Failed { get; set; } = false;

        /// <summary>
        /// A string with a generic message relayed to the user.
        /// </summary>
        [JsonProperty(PropertyName = "msg", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue("")]
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Some modules execute command line utilities or are geared
        /// for executing commands directly (raw, shell, command, etc), 
        /// this field contains <b>return code</b>k of these utilities.
        /// </summary>
        [JsonProperty(PropertyName = "rc", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0)]
        public int ReturnCode { get; set; } = 0;

        /// <summary>
        /// If this key exists, it indicates that a loop was present for the task 
        /// and that it contains a list of the normal module <b>result</b> per item.
        /// </summary>
        [JsonProperty(PropertyName = "results", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<ModuleContext> Results { get; set; } = null;

        /// <summary>
        /// A boolean that indicates if the task was skipped or not.
        /// </summary>
        [JsonProperty(PropertyName = "skipped", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(false)]
        public bool Skipped { get; set; } = false;

        /// <summary>
        /// Some modules execute command line utilities or are geared for executing 
        /// commands  directly (raw, shell, command, etc), this field contains the 
        /// error output of these utilities.
        /// </summary>
        [JsonProperty(PropertyName = "stderr", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue("")]
        public string StdErr { get; set; } = string.Empty;

        /// <summary>
        /// When stdout is returned, Ansible always provides a list of strings, each
        /// containing one item per line from the original output.
        /// </summary>
        [JsonProperty(PropertyName = "stderr_lines", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<string> StdErrLines { get; set; } = null;

        /// <summary>
        /// Some modules execute command line utilities or are geared for executing 
        /// commands directly (raw, shell, command, etc). This field contains the
        /// normal output of these utilities.
        /// </summary>
        [JsonProperty(PropertyName = "stdout", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue("")]
        public string StdOut { get; set; } = string.Empty;

        /// <summary>
        /// When stdout is returned, Ansible always provides a list of strings, each
        /// containing one item per line from the original output.
        /// </summary>
        [JsonProperty(PropertyName = "stdout_lines", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<string> StdOutLines { get; set; } = null;

        /// <summary>
        /// Indicates whether one or more errors have been reported.
        /// </summary>
        public bool HasErrors { get; private set; }

        /// <summary>
        /// Writes a line of text to the standard output lines.
        /// </summary>
        /// <param name="verbosity">The verbosity level for this message.</param>
        /// <param name="value">The text to be written.</param>
        public void WriteLine(AnsibleVerbosity verbosity, string value = null)
        {
            if (verbosity <= this.Verbosity)
            {
                output.Add(value ?? string.Empty);
            }
        }

        /// <summary>
        /// Writes a line of text to the standard error lines.
        /// </summary>
        /// <param name="value">The text to be written.</param>
        public void WriteErrorLine(string value = null)
        {
            HasErrors = true;

            error.Add(value ?? string.Empty);
        }

        /// <summary>
        /// Renders the instance as a JSON string.
        /// </summary>
        public override string ToString()
        {
            // Set [StdErrLines] and [StdOutLines] if necessary.

            if (!string.IsNullOrEmpty(StdErr))
            {
                StdErrLines = StdErr.ToLines().ToList();
            }
            else if (error.Count > 0)
            {
                StdErrLines = error;
            }

            if (!string.IsNullOrEmpty(StdOut))
            {
                StdOutLines = StdOut.ToLines().ToList();
            }
            else if (output.Count > 0)
            {
                StdOutLines = output;
            }

            return NeonHelper.JsonSerialize(this, Formatting.Indented);
        }

        //---------------------------------------------------------------------
        // Parsing helpers

        /// <summary>
        /// Attempts to parse a boolean string.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <param name="errorMessage">The optional context error message to log when the input is not valid.</param>
        /// <returns>The parsed value or <c>null</c> if the input was invalid.</returns>
        public bool? ParseBoolString(string input, string errorMessage = null)
        {
            switch (input.ToLowerInvariant())
            {
                case "0":
                case "no":
                case "false":

                    return false;

                case "1":
                case "yes":
                case "true":

                    return true;

                default:

                    if (errorMessage != null)
                    {
                        WriteErrorLine(errorMessage);
                    }
                    return null;
            }
        }

        /// <summary>
        /// Attempts to parse a long string.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <param name="errorMessage">The optional context error message to log when the input is not valid.</param>
        /// <returns>The parsed value or <c>null</c> if the input was invalid.</returns>
        public long? ParseLongString(string input, string errorMessage = null)
        {
            if (long.TryParse(input, out var value))
            {
                return value;
            }
            else
            {
                if (errorMessage != null)
                {
                    WriteErrorLine(errorMessage);
                }

                return null;
            }
        }

        /// <summary>
        /// Attempts to parse an enumeration string.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <param name="errorMessage">The optional context error message to log when the input is not valid.</param>
        /// <returns>The parsed value or <c>null</c> if the input was invalid.</returns>
        public TEnum? ParseEnumString<TEnum>(string input, string errorMessage = null)
            where TEnum : struct
        {
            if (!Enum.TryParse<TEnum>(input, true, out var value))
            {
                if (errorMessage != null)
                {
                    WriteErrorLine(errorMessage);
                }

                return null;
            }
            else
            {
                return value;
            }
        }

        /// <summary>
        /// Parses a boolean.
        /// </summary>>
        /// <param name="argName">The argument name.</param>
        /// <returns>The parsed boolean or <c>null</c>.</returns>
        public bool? ParseBool(string argName)
        {
            if (!Arguments.TryGetValue(argName, out var jToken))
            {
                return null;
            }

            var value = ParseBoolString(jToken.ToObject<string>());

            if (!value.HasValue)
            {
                WriteErrorLine($"[{argName}] is not a valid boolean.");
            }

            return value;
        }

        /// <summary>
        /// Parses a string.
        /// </summary>>
        /// <param name="argName">The argument name.</param>
        /// <returns>The parsed string or <c>null</c>.</returns>
        public string ParseString(string argName, Func<string, bool> validator = null)
        {
            if (!Arguments.TryGetValue(argName, out var jToken))
            {
                return null;
            }

            try
            {
                var value = jToken.ToObject<string>();

                if (validator != null && !validator(value))
                {
                    WriteErrorLine($"[{argName}={value}] is not valid.");
                }

                return value;
            }
            catch
            {
                WriteErrorLine($"[{argName}] is not a valid boolean.");
                return null;
            }
        }

        /// <summary>
        /// Parses an integer.
        /// </summary>>
        /// <param name="argName">The argument name.</param>
        /// <param name="validator">Optional validation function.</param>
        /// <returns>The parsed integer or <c>null</c>.</returns>
        public int? ParseInt(string argName, Func<int, bool> validator = null)
        {
            if (!Arguments.TryGetValue(argName, out var jToken))
            {
                return null;
            }

            try
            {
                var valueString = jToken.ToObject<string>();
                var value       = int.Parse(valueString);

                if (validator != null && !validator(value))
                {
                    WriteErrorLine($"[{argName}={value}] is not valid.");
                }

                return value;
            }
            catch
            {
                WriteErrorLine($"[{argName}] is not a valid integer.");
                return null;
            }
        }

        /// <summary>
        /// Parses an integer.
        /// </summary>>
        /// <param name="argName">The argument name.</param>
        /// <param name="validator">Optional validation function.</param>
        /// <returns>The parsed double or <c>null</c>.</returns>
        public double? ParseDouble(string argName, Func<double, bool> validator = null)
        {
            if (!Arguments.TryGetValue(argName, out var jToken))
            {
                return null;
            }

            try
            {
                var valueString = jToken.ToObject<string>();
                var value       = double.Parse(valueString);

                if (validator != null && !validator(value))
                {
                    WriteErrorLine($"[{argName}={value}] is not valid.");
                }

                return value;
            }
            catch
            {
                WriteErrorLine($"[{argName}] is not a valid double.");
                return null;
            }
        }

        /// <summary>
        /// Parses an enumeration.
        /// </summary>
        /// <typeparam name="TEnum">The enumeration type.</typeparam>
        /// <param name="argName">The argument name.</param>
        /// <returns>The enumeration value or <c>null</c>.</returns>
        public TEnum? ParseEnum<TEnum>(string argName)
            where TEnum : struct
        {
            if (!Arguments.TryGetValue(argName, out var jToken))
            {
                return null;
            }

            try
            {
                var valueString = jToken.ToObject<string>();

                try
                {
                    return (TEnum?)NeonHelper.ParseEnum<TEnum>(valueString, ignoreCase: true);
                }
                catch
                {
                    WriteErrorLine($"[{argName}] is not a valid boolean.");
                    return null;
                }
            }
            catch
            {
                WriteErrorLine($"[{argName}] is not a valid boolean.");
                return null;
            }
        }

        /// <summary>
        /// Parses a Docker time interval.
        /// </summary>
        /// <param name="argName">The argument name.</param>
        /// <returns>The parsed duration in nanoseconds or <c>null</c>.</returns>
        public long? ParseDockerInterval(string argName)
        {
            if (!Arguments.TryGetValue(argName, out var jToken))
            {
                return null;
            }

            try
            {
                var orgValue = jToken.ToObject<string>();
                var value = orgValue;
                var units = 1000000000L;    // default unit is 1s = 1000000000ns

                if (string.IsNullOrEmpty(value))
                {
                    return null;
                }

                if (value.EndsWith("ns", StringComparison.InvariantCultureIgnoreCase))
                {
                    units = 1L;
                    value = value.Substring(0, value.Length - 2);
                }
                else if (value.EndsWith("us", StringComparison.InvariantCultureIgnoreCase))
                {
                    units = 1000L;
                    value = value.Substring(0, value.Length - 2);
                }
                else if (value.EndsWith("ms", StringComparison.InvariantCultureIgnoreCase))
                {
                    units = 1000000L;
                    value = value.Substring(0, value.Length - 2);
                }
                else if (value.EndsWith("s", StringComparison.InvariantCultureIgnoreCase))
                {
                    units = 1000000000L;
                    value = value.Substring(0, value.Length - 1);
                }
                else if (value.EndsWith("m", StringComparison.InvariantCultureIgnoreCase))
                {
                    units = 60 * 1000000000L;
                    value = value.Substring(0, value.Length - 1);
                }
                else if (value.EndsWith("h", StringComparison.InvariantCultureIgnoreCase))
                {
                    units = 60 * 60 * 1000000000L;
                    value = value.Substring(0, value.Length - 1);
                }
                else if (!char.IsDigit(value.Last()))
                {
                    WriteErrorLine($"[{argName}={orgValue}] has an unknown unit.");
                    return null;
                }

                if (long.TryParse(value, out var time))
                {
                    if (time < 0)
                    {
                        WriteErrorLine($"[{argName}={orgValue}] cannot be negative.");
                        return null;
                    }

                    return time * units;
                }
                else
                {
                    WriteErrorLine($"[{argName}={orgValue}] is not a valid duration.");
                    return null;
                }
            }
            catch
            {
                WriteErrorLine($"[{argName}] cannot be converted into a string.");
                return null;
            }
        }

        /// <summary>
        /// Parses a Docker memory size.
        /// </summary>
        /// <param name="argName">The argument name.</param>
        /// <returns>The parsed memory size in bytes.</returns>
        public long? ParseDockerMemorySize(string argName)
        {
            if (!Arguments.TryGetValue(argName, out var jToken))
            {
                return null;
            }

            try
            {
                var orgValue = jToken.ToObject<string>();
                var value    = orgValue;
                var units    = 1L;    // default unit is 1 byte

                if (string.IsNullOrEmpty(value))
                {
                    return null;
                }

                if (value.EndsWith("b", StringComparison.InvariantCultureIgnoreCase))
                {
                    units = 1L;
                    value = value.Substring(0, value.Length - 1);
                }
                else if (value.EndsWith("k", StringComparison.InvariantCultureIgnoreCase))
                {
                    units = 1000L;
                    value = value.Substring(0, value.Length - 1);
                }
                else if (value.EndsWith("m", StringComparison.InvariantCultureIgnoreCase))
                {
                    units = 1000000L;
                    value = value.Substring(0, value.Length - 1);
                }
                else if (value.EndsWith("g", StringComparison.InvariantCultureIgnoreCase))
                {
                    units = 1000000000L;
                    value = value.Substring(0, value.Length - 1);
                }
                else if (!char.IsDigit(value.Last()))
                {
                    WriteErrorLine($"[{argName}={orgValue}] has an unknown unit.");
                    return null;
                }

                if (long.TryParse(value, out var size))
                {
                    if (size < 0)
                    {
                        WriteErrorLine($"[{argName}={orgValue}] cannot be negative.");
                        return null;
                    }

                    return size * units;
                }
                else
                {
                    WriteErrorLine($"[{argName}={orgValue}] is not a valid memory size.");
                    return null;
                }
            }
            catch
            {
                WriteErrorLine($"[{argName}] cannot be converted into a string.");
                return null;
            }
        }

        /// <summary>
        /// Parses an argument as a string array.
        /// </summary>
        /// <param name="array">The output array.</param>
        /// <param name="argName">The argument name.</param>
        public List<String> ParseStringArray(string argName)
        {
            var array = new List<string>();

            if (!Arguments.TryGetValue(argName, out var jToken))
            {
                return array;
            }

            var jArray = jToken as JArray;

            if (jArray == null)
            {
                WriteErrorLine($"[{argName}] is not an array.");
                return array;
            }

            foreach (var item in jArray)
            {
                try
                {
                    array.Add(item.ToObject<string>());
                }
                catch
                {
                    WriteErrorLine($"[{argName}] array as one or more invalid elements.");
                    return array;
                }
            }

            return array;
        }

        /// <summary>
        /// Parses an argument as an <see cref="IPAddress"/> array.
        /// </summary>
        /// <param name="argName">The argument name.</param>
        public List<IPAddress> ParseIPAddressArray(string argName)
        {
            var array       = new List<IPAddress>();
            var stringArray = ParseStringArray(argName);

            foreach (var item in stringArray)
            {
                if (IPAddress.TryParse(item, out var address))
                {
                    array.Add(address);
                }
                else
                {
                    WriteErrorLine($"[{argName}] is includes invalid IP address [{item}].");
                }
            }

            return array;
        }
    }
}
