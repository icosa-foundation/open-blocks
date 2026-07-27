// Copyright 2020 The Blocks Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0

using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

[TestFixture]
public class GooglePlaySafManifestProcessorTest
{
    private string directory;
    private string manifestPath;

    [SetUp]
    public void SetUp()
    {
        directory = Path.Combine(
          Path.GetTempPath(),
          $"OpenBlocksSafManifestTest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        manifestPath = Path.Combine(directory, "AndroidManifest.xml");
        File.WriteAllText(
          manifestPath,
          "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
          "<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\">" +
          "<uses-permission android:name=\"android.permission.MANAGE_EXTERNAL_STORAGE\"/>" +
          "<uses-permission android:name=\"android.permission.INTERNET\"/>" +
          "<application android:requestLegacyExternalStorage=\"true\"/>" +
          "</manifest>");
    }

    [TearDown]
    public void TearDown()
    {
        string resolvedDirectory = Path.GetFullPath(directory);
        string resolvedTemp = Path.GetFullPath(Path.GetTempPath());
        Assert.That(
          resolvedDirectory.StartsWith(resolvedTemp, StringComparison.OrdinalIgnoreCase),
          Is.True);
        if (Directory.Exists(resolvedDirectory))
        {
            Directory.Delete(resolvedDirectory, true);
        }
    }

    [Test]
    public void RemovesBroadStorageAccessButPreservesOtherPermissions()
    {
        GooglePlaySafManifestProcessor.RemoveBroadStorageAccess(manifestPath);

        XNamespace android = "http://schemas.android.com/apk/res/android";
        XElement root = XDocument.Load(manifestPath).Root;
        string[] permissions = root.Elements("uses-permission")
          .Select(element => (string)element.Attribute(android + "name"))
          .ToArray();

        Assert.That(permissions, Does.Not.Contain("android.permission.MANAGE_EXTERNAL_STORAGE"));
        Assert.That(permissions, Does.Contain("android.permission.INTERNET"));
        Assert.That(
          root.Element("application").Attribute(android + "requestLegacyExternalStorage"),
          Is.Null);
    }
}
