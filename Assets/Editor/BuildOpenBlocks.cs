// Copyright 2026 The Open Blocks Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Deterministic command-line player builds for Open Blocks.
/// </summary>
public static class BuildOpenBlocks
{
    internal const string LogPrefix = "[OBXRBUILD]";

    public enum Runtime
    {
        Monoscopic,
        OpenXR,
        AndroidXR,
        MetaQuest,
    }

    internal enum AndroidExport
    {
        Package,
        AppBundle,
        AndroidStudioProject,
    }

    internal sealed class Arguments
    {
        internal BuildTarget Target { get; set; } = BuildTarget.NoTarget;
        internal Runtime Runtime { get; set; }
        internal bool HasRuntime { get; set; }
        internal string OutputPath { get; set; }
        internal bool Development { get; set; }
        internal AndroidSdkVersions? AndroidTargetSdkVersion { get; set; }
        internal AndroidExport AndroidExport { get; set; } = AndroidExport.Package;
        internal string Version { get; set; }
    }

    /// <summary>
    /// Entry point used by Unity Builder's buildMethod input.
    /// </summary>
    public static void CommandLine()
    {
        Arguments arguments = ParseArguments(Environment.GetCommandLineArgs());
        Build(arguments);
    }

    internal static Arguments ParseArguments(IReadOnlyList<string> commandLine)
    {
        var arguments = new Arguments();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0; index < commandLine.Count; ++index)
        {
            string option = commandLine[index];
            switch (option)
            {
                case "-ob-target":
                case "-customBuildTarget":
                    SetOnce(seen, "target", option);
                    arguments.Target = ParseEnum<BuildTarget>(RequireValue(commandLine, ref index, option), option);
                    break;
                case "-ob-runtime":
                    SetOnce(seen, "runtime", option);
                    arguments.Runtime = ParseEnum<Runtime>(RequireValue(commandLine, ref index, option), option);
                    arguments.HasRuntime = true;
                    break;
                case "-ob-output":
                case "-customBuildPath":
                    SetOnce(seen, "output", option);
                    arguments.OutputPath = RequireValue(commandLine, ref index, option);
                    break;
                case "-ob-development":
                    SetOnce(seen, "development", option);
                    arguments.Development = ParseOptionalBoolean(commandLine, ref index, option);
                    break;
                case "-androidTargetSdkVersion":
                    SetOnce(seen, "android-target-sdk", option);
                    string sdkValue = OptionalValue(commandLine, ref index);
                    if (!string.IsNullOrEmpty(sdkValue))
                    {
                        arguments.AndroidTargetSdkVersion =
                            ParseEnum<AndroidSdkVersions>(sdkValue, option);
                    }
                    break;
                case "-androidExportType":
                    SetOnce(seen, "android-export", option);
                    string exportValue = OptionalValue(commandLine, ref index);
                    if (!string.IsNullOrEmpty(exportValue))
                    {
                        arguments.AndroidExport = ParseAndroidExport(exportValue);
                    }
                    break;
                case "-buildVersion":
                    SetOnce(seen, "version", option);
                    arguments.Version = OptionalValue(commandLine, ref index);
                    break;
                default:
                    if (option.StartsWith("-ob-", StringComparison.Ordinal))
                    {
                        throw new BuildFailedException($"Unknown Open Blocks build option '{option}'.");
                    }
                    break;
            }
        }

        Validate(arguments);
        return arguments;
    }

    internal static void Validate(Arguments arguments)
    {
        if (arguments.Target == BuildTarget.NoTarget)
        {
            throw new BuildFailedException("Missing -ob-target or -customBuildTarget.");
        }
        if (!arguments.HasRuntime)
        {
            throw new BuildFailedException("Missing -ob-runtime.");
        }
        if (string.IsNullOrWhiteSpace(arguments.OutputPath))
        {
            throw new BuildFailedException("Missing -ob-output or -customBuildPath.");
        }
        if (arguments.Target != BuildTarget.Android && arguments.AndroidTargetSdkVersion.HasValue)
        {
            throw new BuildFailedException("Android target SDK can only be set for an Android build.");
        }
        if (arguments.Target != BuildTarget.Android && arguments.AndroidExport != AndroidExport.Package)
        {
            throw new BuildFailedException("Android export type can only be set for an Android build.");
        }

        bool validCombination = arguments.Runtime switch
        {
            Runtime.Monoscopic => true,
            Runtime.OpenXR => arguments.Target == BuildTarget.Android ||
                arguments.Target == BuildTarget.StandaloneWindows64,
            Runtime.AndroidXR => arguments.Target == BuildTarget.Android,
            Runtime.MetaQuest => arguments.Target == BuildTarget.Android,
            _ => false,
        };

        if (!validCombination)
        {
            throw new BuildFailedException(
                $"Runtime {arguments.Runtime} does not support target {arguments.Target}.");
        }
    }

    internal static BuildOptions GetBuildOptions(Arguments arguments)
    {
        BuildOptions options = arguments.Development ? BuildOptions.Development : BuildOptions.None;
        if (arguments.Target == BuildTarget.Android &&
            arguments.AndroidExport == AndroidExport.AndroidStudioProject)
        {
            options |= BuildOptions.AcceptExternalModificationsToPlayer;
        }
        return options;
    }

    private static void Build(Arguments arguments)
    {
        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();
        if (scenes.Length == 0)
        {
            throw new BuildFailedException("No enabled scenes are present in Editor Build Settings.");
        }

        string outputPath = Path.GetFullPath(arguments.OutputPath);
        string outputDirectory = arguments.AndroidExport == AndroidExport.AndroidStudioProject
            ? outputPath
            : Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        Debug.Log($"{LogPrefix} Target={arguments.Target}, Runtime={arguments.Runtime}, Output={outputPath}, Development={arguments.Development}, AndroidTargetSdk={arguments.AndroidTargetSdkVersion?.ToString() ?? "unchanged"}, AndroidExport={arguments.AndroidExport}.");

        using (new TemporaryCoreSettings(arguments))
        {
            AssetDatabase.SaveAssets();
            var buildPlayerOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = arguments.Target,
                options = GetBuildOptions(arguments),
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException(
                    $"{LogPrefix} Build failed with result {report.summary.result} and {report.summary.totalErrors} reported errors.");
            }

            Debug.Log($"{LogPrefix} Build succeeded with {report.summary.totalWarnings} warnings in {report.summary.totalTime}.");
        }
    }

    private static T ParseEnum<T>(string value, string option) where T : struct
    {
        if (!Enum.TryParse(value, true, out T parsed) || !Enum.IsDefined(typeof(T), parsed))
        {
            throw new BuildFailedException($"Invalid value '{value}' for {option}.");
        }
        return parsed;
    }

    private static AndroidExport ParseAndroidExport(string value)
    {
        return value switch
        {
            "androidPackage" => AndroidExport.Package,
            "androidAppBundle" => AndroidExport.AppBundle,
            "androidStudioProject" => AndroidExport.AndroidStudioProject,
            _ => throw new BuildFailedException($"Invalid Android export type '{value}'."),
        };
    }

    private static void SetOnce(HashSet<string> seen, string key, string option)
    {
        if (!seen.Add(key))
        {
            throw new BuildFailedException($"Build option '{option}' was supplied more than once.");
        }
    }

    private static string RequireValue(IReadOnlyList<string> commandLine, ref int index, string option)
    {
        string value = OptionalValue(commandLine, ref index);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BuildFailedException($"Build option '{option}' requires a value.");
        }
        return value;
    }

    private static string OptionalValue(IReadOnlyList<string> commandLine, ref int index)
    {
        if (index + 1 >= commandLine.Count ||
            commandLine[index + 1].StartsWith("-", StringComparison.Ordinal))
        {
            return null;
        }
        return commandLine[++index];
    }

    private static bool ParseOptionalBoolean(
        IReadOnlyList<string> commandLine, ref int index, string option)
    {
        string value = OptionalValue(commandLine, ref index);
        if (value == null)
        {
            return true;
        }
        if (!bool.TryParse(value, out bool parsed))
        {
            throw new BuildFailedException($"Invalid Boolean value '{value}' for {option}.");
        }
        return parsed;
    }

    private sealed class TemporaryCoreSettings : IDisposable
    {
        private readonly BuildTarget m_Target;
        private readonly NamedBuildTarget m_NamedBuildTarget;
        private readonly ScriptingImplementation m_ScriptingBackend;
        private readonly string m_DefineSymbols;
        private readonly string m_BundleVersion;
        private readonly AndroidSdkVersions m_AndroidTargetSdkVersion;
        private readonly AndroidArchitecture m_AndroidTargetArchitectures;
        private readonly bool m_BuildAppBundle;
        private readonly bool m_ExportAsGoogleAndroidProject;
        private bool m_Disposed;

        internal TemporaryCoreSettings(Arguments arguments)
        {
            m_Target = arguments.Target;
            BuildTargetGroup targetGroup = BuildPipeline.GetBuildTargetGroup(m_Target);
            m_NamedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(targetGroup);
            m_ScriptingBackend = PlayerSettings.GetScriptingBackend(m_NamedBuildTarget);
            m_DefineSymbols = PlayerSettings.GetScriptingDefineSymbols(m_NamedBuildTarget);
            m_BundleVersion = PlayerSettings.bundleVersion;
            m_AndroidTargetSdkVersion = PlayerSettings.Android.targetSdkVersion;
            m_AndroidTargetArchitectures = PlayerSettings.Android.targetArchitectures;
            m_BuildAppBundle = EditorUserBuildSettings.buildAppBundle;
            m_ExportAsGoogleAndroidProject = EditorUserBuildSettings.exportAsGoogleAndroidProject;

            try
            {
                if (!string.IsNullOrWhiteSpace(arguments.Version))
                {
                    PlayerSettings.bundleVersion = arguments.Version;
                }

                var defines = new HashSet<string>(
                    m_DefineSymbols.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries),
                    StringComparer.Ordinal);
                if (arguments.Runtime == Runtime.Monoscopic)
                {
                    defines.Add("XR_DISABLED");
                }
                else
                {
                    defines.Remove("XR_DISABLED");
                }
                PlayerSettings.SetScriptingDefineSymbols(
                    m_NamedBuildTarget, string.Join(";", defines.OrderBy(value => value)));

                if (m_Target == BuildTarget.Android)
                {
                    PlayerSettings.SetScriptingBackend(
                        m_NamedBuildTarget, ScriptingImplementation.IL2CPP);
                    PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                    if (arguments.AndroidTargetSdkVersion.HasValue)
                    {
                        PlayerSettings.Android.targetSdkVersion = arguments.AndroidTargetSdkVersion.Value;
                    }
                    EditorUserBuildSettings.buildAppBundle =
                        arguments.AndroidExport == AndroidExport.AppBundle;
                    EditorUserBuildSettings.exportAsGoogleAndroidProject =
                        arguments.AndroidExport == AndroidExport.AndroidStudioProject;
                }

                AssetDatabase.SaveAssets();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            if (m_Disposed)
            {
                return;
            }
            m_Disposed = true;

            PlayerSettings.SetScriptingBackend(m_NamedBuildTarget, m_ScriptingBackend);
            PlayerSettings.SetScriptingDefineSymbols(m_NamedBuildTarget, m_DefineSymbols);
            PlayerSettings.bundleVersion = m_BundleVersion;
            if (m_Target == BuildTarget.Android)
            {
                PlayerSettings.Android.targetSdkVersion = m_AndroidTargetSdkVersion;
                PlayerSettings.Android.targetArchitectures = m_AndroidTargetArchitectures;
                EditorUserBuildSettings.buildAppBundle = m_BuildAppBundle;
                EditorUserBuildSettings.exportAsGoogleAndroidProject =
                    m_ExportAsGoogleAndroidProject;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"{LogPrefix} Restored core Player Settings for {m_Target}.");
        }
    }
}
