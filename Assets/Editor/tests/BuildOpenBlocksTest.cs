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

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;

#if !XR_DISABLED
using UnityEditor.XR.Management;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.Android;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;
#endif

[TestFixture]
public class BuildOpenBlocksTest
{
    [Test]
    public void ParseArguments_ParsesGameCiAndOpenBlocksOptions()
    {
        BuildOpenBlocks.Arguments arguments = BuildOpenBlocks.ParseArguments(new[]
        {
            "Unity",
            "-batchmode",
            "-customBuildTarget", "Android",
            "-customBuildPath", "build/open-blocks.aab",
            "-ob-runtime", "AndroidXR",
            "-ob-development", "true",
            "-androidTargetSdkVersion", "AndroidApiLevel35",
            "-androidExportType", "androidAppBundle",
            "-buildVersion", "2.3.4",
            "-androidKeystorePass", "not-inspected-by-this-parser",
        });

        Assert.That(arguments.Target, Is.EqualTo(BuildTarget.Android));
        Assert.That(arguments.Runtime, Is.EqualTo(BuildOpenBlocks.Runtime.AndroidXR));
        Assert.That(arguments.OutputPath, Is.EqualTo("build/open-blocks.aab"));
        Assert.That(arguments.Development, Is.True);
        Assert.That(arguments.AndroidTargetSdkVersion,
            Is.EqualTo(AndroidSdkVersions.AndroidApiLevel35));
        Assert.That(arguments.AndroidExport, Is.EqualTo(BuildOpenBlocks.AndroidExport.AppBundle));
        Assert.That(arguments.Version, Is.EqualTo("2.3.4"));
    }

    [Test]
    public void ParseArguments_BooleanFlagWithoutValueIsTrue()
    {
        BuildOpenBlocks.Arguments arguments = BuildOpenBlocks.ParseArguments(new[]
        {
            "-ob-target", "StandaloneWindows64",
            "-ob-runtime", "OpenXR",
            "-ob-output", "build/OpenBlocks.exe",
            "-ob-development",
            "-quit",
        });

        Assert.That(arguments.Development, Is.True);
    }

    [Test]
    public void ParseArguments_RejectsUnknownOpenBlocksOption()
    {
        Assert.Throws<BuildFailedException>(() => BuildOpenBlocks.ParseArguments(new[]
        {
            "-ob-target", "Android",
            "-ob-runtime", "OpenXR",
            "-ob-output", "build/OpenBlocks.apk",
            "-ob-unknown",
        }));
    }

    [Test]
    public void ParseArguments_RejectsMissingRuntime()
    {
        Assert.Throws<BuildFailedException>(() => BuildOpenBlocks.ParseArguments(new[]
        {
            "-ob-target", "Android",
            "-ob-output", "build/OpenBlocks.apk",
        }));
    }

    [Test]
    public void ParseArguments_RejectsDuplicateTargetAliases()
    {
        Assert.Throws<BuildFailedException>(() => BuildOpenBlocks.ParseArguments(new[]
        {
            "-ob-target", "Android",
            "-customBuildTarget", "Android",
            "-ob-runtime", "OpenXR",
            "-ob-output", "build/OpenBlocks.apk",
        }));
    }

    [Test]
    public void ParseArguments_RejectsIncompatibleRuntimeAndTarget()
    {
        Assert.Throws<BuildFailedException>(() => BuildOpenBlocks.ParseArguments(new[]
        {
            "-ob-target", "StandaloneWindows64",
            "-ob-runtime", "AndroidXR",
            "-ob-output", "build/OpenBlocks.exe",
        }));
    }

    [Test]
    public void GetBuildOptions_AppliesDevelopmentAndAndroidStudioOptions()
    {
        var arguments = new BuildOpenBlocks.Arguments
        {
            Target = BuildTarget.Android,
            Runtime = BuildOpenBlocks.Runtime.AndroidXR,
            HasRuntime = true,
            OutputPath = "build/android-project",
            Development = true,
            AndroidExport = BuildOpenBlocks.AndroidExport.AndroidStudioProject,
        };

        BuildOptions options = BuildOpenBlocks.GetBuildOptions(arguments);

        Assert.That(options.HasFlag(BuildOptions.Development), Is.True);
        Assert.That(options.HasFlag(BuildOptions.AcceptExternalModificationsToPlayer), Is.True);
    }

#if !XR_DISABLED
    [Test]
    public void AndroidOpenXrProfile_UsesCrossDeviceControllerFeatures()
    {
        IReadOnlyList<System.Type> features = GetRequiredFeatures(BuildOpenBlocks.Runtime.OpenXR);

        CollectionAssert.AreEquivalent(new[]
        {
            typeof(FoveatedRenderingFeature),
            typeof(MetaQuestTouchPlusControllerProfile),
            typeof(OculusTouchControllerProfile),
            typeof(PICO4ControllerProfile),
        }, features);
    }

    [Test]
    public void AndroidXrProfile_DoesNotEnableUnusedArFoundationProviders()
    {
        IReadOnlyList<System.Type> features = GetRequiredFeatures(BuildOpenBlocks.Runtime.AndroidXR);

        CollectionAssert.AreEquivalent(new[]
        {
            typeof(AndroidXRSupportFeature),
            typeof(FoveatedRenderingFeature),
            typeof(HandInteractionProfile),
        }, features);
        CollectionAssert.DoesNotContain(features, typeof(ARCameraFeature));
        CollectionAssert.DoesNotContain(features, typeof(ARSessionFeature));
    }

    [Test]
    public void MetaQuestProfile_UsesMetaBuildAndControllerFeatures()
    {
        IReadOnlyList<System.Type> features = GetRequiredFeatures(BuildOpenBlocks.Runtime.MetaQuest);

        CollectionAssert.AreEquivalent(new[]
        {
            typeof(FoveatedRenderingFeature),
            typeof(MetaQuestFeature),
            typeof(MetaQuestTouchPlusControllerProfile),
            typeof(OculusTouchControllerProfile),
        }, features);
    }

    [Test]
    public void MonoscopicProfile_HasNoOpenXrFeatures()
    {
        IReadOnlyList<System.Type> features = GetRequiredFeatures(BuildOpenBlocks.Runtime.Monoscopic);

        Assert.That(features, Is.Empty);
    }

    [Test]
    public void RuntimeSettings_RestoreAndroidXrStateAfterDispose()
    {
        var arguments = CreateArguments(BuildTarget.Android, BuildOpenBlocks.Runtime.AndroidXR);
        AndroidApplicationEntry applicationEntry = PlayerSettings.Android.applicationEntry;
        bool resizeableActivity = PlayerSettings.Android.resizeableActivity;
        AndroidSdkVersions minSdkVersion = PlayerSettings.Android.minSdkVersion;
        bool useDefaultGraphicsApis = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android);
        GraphicsDeviceType[] graphicsApis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
        XRGeneralSettings generalSettings =
            XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
        List<XRLoader> loaders = generalSettings.Manager.activeLoaders.ToList();
        bool initManagerOnStart = generalSettings.InitManagerOnStart;
        OpenXRSettings openXrSettings =
            OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
        Dictionary<OpenXRFeature, bool> featureStates = openXrSettings.GetFeatures()
            .ToDictionary(feature => feature, feature => feature.enabled);

        using (new OpenBlocksRuntimeSettings(arguments))
        {
            Assert.That(PlayerSettings.Android.applicationEntry,
                Is.EqualTo(AndroidApplicationEntry.GameActivity));
            Assert.That(PlayerSettings.Android.resizeableActivity, Is.True);
            Assert.That((int)PlayerSettings.Android.minSdkVersion,
                Is.GreaterThanOrEqualTo((int)AndroidSdkVersions.AndroidApiLevel26));
            CollectionAssert.AreEqual(
                new[] { GraphicsDeviceType.Vulkan },
                PlayerSettings.GetGraphicsAPIs(BuildTarget.Android));
            Assert.That(generalSettings.InitManagerOnStart, Is.True);
            Assert.That(openXrSettings.GetFeature<AndroidXRSupportFeature>().enabled, Is.True);
            Assert.That(openXrSettings.GetFeature<ARSessionFeature>().enabled, Is.False);
        }

        Assert.That(PlayerSettings.Android.applicationEntry, Is.EqualTo(applicationEntry));
        Assert.That(PlayerSettings.Android.resizeableActivity, Is.EqualTo(resizeableActivity));
        Assert.That(PlayerSettings.Android.minSdkVersion, Is.EqualTo(minSdkVersion));
        Assert.That(PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android),
            Is.EqualTo(useDefaultGraphicsApis));
        CollectionAssert.AreEqual(graphicsApis, PlayerSettings.GetGraphicsAPIs(BuildTarget.Android));
        CollectionAssert.AreEqual(loaders, generalSettings.Manager.activeLoaders);
        Assert.That(generalSettings.InitManagerOnStart, Is.EqualTo(initManagerOnStart));
        foreach (KeyValuePair<OpenXRFeature, bool> state in featureStates)
        {
            Assert.That(state.Key.enabled, Is.EqualTo(state.Value), state.Key.GetType().FullName);
        }
    }

    [Test]
    public void RuntimeSettings_RestoreStateWhenFeatureProfileFails()
    {
        var arguments = CreateArguments(
            BuildTarget.StandaloneLinux64, BuildOpenBlocks.Runtime.OpenXR);
        XRGeneralSettings generalSettings =
            XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
        List<XRLoader> loaders = generalSettings.Manager.activeLoaders.ToList();
        bool initManagerOnStart = generalSettings.InitManagerOnStart;
        OpenXRSettings openXrSettings =
            OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Standalone);
        Dictionary<OpenXRFeature, bool> featureStates = openXrSettings.GetFeatures()
            .ToDictionary(feature => feature, feature => feature.enabled);

        Assert.Throws<BuildFailedException>(() => new OpenBlocksRuntimeSettings(arguments));

        CollectionAssert.AreEqual(loaders, generalSettings.Manager.activeLoaders);
        Assert.That(generalSettings.InitManagerOnStart, Is.EqualTo(initManagerOnStart));
        foreach (KeyValuePair<OpenXRFeature, bool> state in featureStates)
        {
            Assert.That(state.Key.enabled, Is.EqualTo(state.Value), state.Key.GetType().FullName);
        }
    }

    [Test]
    public void RuntimeSettings_SequentialProfilesDoNotLeakOpenXrFeatures()
    {
        OpenXRSettings openXrSettings =
            OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
        Dictionary<OpenXRFeature, bool> featureStates = openXrSettings.GetFeatures()
            .ToDictionary(feature => feature, feature => feature.enabled);

        using (new OpenBlocksRuntimeSettings(
                   CreateArguments(BuildTarget.Android, BuildOpenBlocks.Runtime.AndroidXR)))
        {
            Assert.That(openXrSettings.GetFeature<AndroidXRSupportFeature>().enabled, Is.True);
            Assert.That(openXrSettings.GetFeature<MetaQuestFeature>().enabled, Is.False);
        }

        using (new OpenBlocksRuntimeSettings(
                   CreateArguments(BuildTarget.Android, BuildOpenBlocks.Runtime.MetaQuest)))
        {
            Assert.That(openXrSettings.GetFeature<AndroidXRSupportFeature>().enabled, Is.False);
            Assert.That(openXrSettings.GetFeature<MetaQuestFeature>().enabled, Is.True);
        }

        foreach (KeyValuePair<OpenXRFeature, bool> state in featureStates)
        {
            Assert.That(state.Key.enabled, Is.EqualTo(state.Value), state.Key.GetType().FullName);
        }
    }

    private static IReadOnlyList<System.Type> GetRequiredFeatures(
        BuildOpenBlocks.Runtime runtime)
    {
        return OpenBlocksRuntimeSettings.GetRequiredOpenXrFeatureTypes(
            CreateArguments(BuildTarget.Android, runtime));
    }

    private static BuildOpenBlocks.Arguments CreateArguments(
        BuildTarget target, BuildOpenBlocks.Runtime runtime)
    {
        return new BuildOpenBlocks.Arguments
        {
            Target = target,
            Runtime = runtime,
            HasRuntime = true,
            OutputPath = "build/test-output",
        };
    }
#endif
}
