// Copyright 2020 The Blocks Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0

#if UNITY_ANDROID
using System;
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

    public int callbackOrder => 1000;

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        string defines = PlayerSettings.GetScriptingDefineSymbols(
          NamedBuildTarget.FromBuildTargetGroup(BuildTargetGroup.Android));
        bool isGooglePlayBuild = defines
          .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
          .Contains(GOOGLE_PLAY_DEFINE);
        if (!isGooglePlayBuild)
        {
            return;
        }

        string manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
        if (!File.Exists(manifestPath))
        {
            throw new BuildFailedException(
              $"{LOG_PREFIX} Generated manifest was not found at {manifestPath}");
        }

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

        XElement application = root.Element("application");
        application?.Attribute(android + "requestLegacyExternalStorage")?.Remove();
        manifest.Save(manifestPath, SaveOptions.DisableFormatting);
        Debug.Log($"{LOG_PREFIX} Removed broad external-storage access from the Google Play manifest.");
    }
}
#endif
