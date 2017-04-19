﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.DotNet.Cli.Utils
{
    internal class MSBuildForwardingAppWithoutLogging
    {
        private const string MSBuildExeName = "MSBuild.dll";

        private const string SdksDirectoryName = "Sdks";

        private readonly ForwardingAppImplementation _forwardingApp;

        private readonly Dictionary<string, string> _msbuildRequiredEnvironmentVariables =
            new Dictionary<string, string>
            {
                { "MSBuildExtensionsPath", AppContext.BaseDirectory },
                { "CscToolExe", GetRunCscPath() },
                { "MSBuildSDKsPath", GetMSBuildSDKsPath() }
            };

        private readonly IEnumerable<string> _msbuildRequiredParameters =
            new List<string> { "/m", "/v:m" };

        public MSBuildForwardingAppWithoutLogging(IEnumerable<string> argsToForward, string msbuildPath = null)
        {
            _forwardingApp = new ForwardingAppImplementation(
                msbuildPath ?? GetMSBuildExePath(),
                _msbuildRequiredParameters.Concat(argsToForward.Select(Escape)),
                environmentVariables: _msbuildRequiredEnvironmentVariables);
        }

        public virtual ProcessStartInfo GetProcessStartInfo()
        {
            return _forwardingApp
                .GetProcessStartInfo();
        }

        public int Execute()
        {
            return GetProcessStartInfo().Execute();
        }

        private static string Escape(string arg) =>
             // this is a workaround for https://github.com/Microsoft/msbuild/issues/1622
             (arg.StartsWith("/p:RestoreSources=", StringComparison.OrdinalIgnoreCase)) ?
                arg.Replace(";", "%3B")
                   .Replace("://", ":%2F%2F") :
                arg;

        private static string GetMSBuildExePath()
        {
            return Path.Combine(
                AppContext.BaseDirectory,
                MSBuildExeName);
        }

        private static string GetMSBuildSDKsPath()
        {
            var envMSBuildSDKsPath = Environment.GetEnvironmentVariable("MSBuildSDKsPath");

            if (envMSBuildSDKsPath != null)
            {
                return envMSBuildSDKsPath;
            }

            return Path.Combine(
                AppContext.BaseDirectory,
                SdksDirectoryName);
        }

        private static string GetRunCscPath()
        {
            var scriptExtension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".cmd" : ".sh";
            return Path.Combine(AppContext.BaseDirectory, "Roslyn", $"RunCsc{scriptExtension}");
        }
    }
}

