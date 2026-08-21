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

using com.google.apps.peltzer.client.app;
using com.google.apps.peltzer.client.entitlement;
using NUnit.Framework;
using TiltBrush;
using UnityEngine;

[TestFixture]
public class SteamFrameLoginTest
{
    [TestCase(RuntimePlatform.Android, true, false)]
    [TestCase(RuntimePlatform.Android, false, true)]
    [TestCase(RuntimePlatform.WindowsPlayer, true, true)]
    [TestCase(RuntimePlatform.OSXPlayer, false, true)]
    public void OsCanReachLocalhost_OnlyBlocksAndroidUnderSteam(
      RuntimePlatform platform, bool runningUnderSteam, bool expected)
    {
        Assert.AreEqual(
          expected, PlatformCapabilities.OsCanReachLocalhost(platform, runningUnderSteam));
    }

    [Test]
    public void BuildAuthorizationUrl_AddsCallbackSecretForAutomaticLogin()
    {
        Assert.AreEqual(
          "https://icosa.gallery/device?appId=openblocks&secret=test%20secret",
          DeviceLoginRouting.BuildAuthorizationUrl(
            "https://icosa.gallery/device", useAutomaticCallback: true, secret: "test secret"));
    }

    [Test]
    public void BuildAuthorizationUrl_LeavesManualLoginUrlUnchanged()
    {
        Assert.AreEqual(
          "https://icosa.gallery/device",
          DeviceLoginRouting.BuildAuthorizationUrl(
            "https://icosa.gallery/device", useAutomaticCallback: false, secret: null));
    }

    [Test]
    public void KeyboardHide_NotifiesPendingLoginOfDismissal()
    {
        var gameObject = new GameObject("Keyboard dismissal test");
        try
        {
            var keyboard = gameObject.AddComponent<KeyboardUI>();
            var dismissed = false;
            keyboard.Dismissed += (sender, args) => dismissed = true;

            keyboard.Hide();

            Assert.True(dismissed);
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }
}
