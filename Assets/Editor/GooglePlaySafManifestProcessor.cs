// Copyright 2020 The Blocks Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

/// <summary>
/// Removes broad filesystem access from the generated Google Play manifest.
/// Other Android distributions retain their existing path-based storage behavior.
/// </summary>
public sealed class GooglePlaySafManifestProcessor : IPostGenerateGradleAndroidProject
{
    private const string GOOGLE_PLAY_DEFINE = "OPEN_BLOCKS_GOOGLE_PLAY";
    private const string LOG_PREFIX = "[OB_SAF_MANIFEST]";
    private const string MANAGE_EXTERNAL_STORAGE = "android.permission.MANAGE_EXTERNAL_STORAGE";

#if OPEN_BLOCKS_GOOGLE_PLAY
    private const bool COMPILED_FOR_GOOGLE_PLAY = true;
#else
    private const bool COMPILED_FOR_GOOGLE_PLAY = false;
#endif

    public int callbackOrder => 1000;

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        string defines = PlayerSettings.GetScriptingDefineSymbols(
          NamedBuildTarget.FromBuildTargetGroup(BuildTargetGroup.Android));
        if (!IsGooglePlayBuild(defines, COMPILED_FOR_GOOGLE_PLAY))
        {
            return;
        }

        string[] manifestCandidates =
        {
            Path.Combine(path, "src", "main", "AndroidManifest.xml"),
            Path.Combine(path, "unityLibrary", "src", "main", "AndroidManifest.xml")
        };
        List<string> manifestPaths = manifestCandidates.Where(File.Exists).Distinct().ToList();
        if (manifestPaths.Count == 0)
        {
            throw new BuildFailedException(
              $"{LOG_PREFIX} Generated library manifest was not found below {path}");
        }

        foreach (string manifestPath in manifestPaths)
        {
            RemoveBroadStorageAccess(manifestPath);
        }

        Debug.Log($"{LOG_PREFIX} Removed broad external-storage access from the Google Play manifest.");
    }

    internal static bool IsGooglePlayBuild(string playerSettingsDefines, bool compiledForGooglePlay)
    {
        return compiledForGooglePlay ||
          (playerSettingsDefines ?? string.Empty)
          .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
          .Contains(GOOGLE_PLAY_DEFINE);
    }

    internal static void RemoveBroadStorageAccess(string manifestPath)
    {
        XNamespace android = "http://schemas.android.com/apk/res/android";
        XDocument manifest = XDocument.Load(manifestPath, LoadOptions.PreserveWhitespace);
        XElement root = manifest.Root ??
          throw new BuildFailedException($"{LOG_PREFIX} Generated manifest has no root element.");

        foreach (XElement permission in root.Elements("uses-permission")
          .Where(element => (string)element.Attribute(android + "name") == MANAGE_EXTERNAL_STORAGE)
          .ToArray())
        {
            permission.Remove();
        }

        root.Element("application")
          ?.Attribute(android + "requestLegacyExternalStorage")
          ?.Remove();
        manifest.Save(manifestPath, SaveOptions.DisableFormatting);
    }
}
