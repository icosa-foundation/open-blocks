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
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

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

/// <summary>
/// Applies runtime-specific settings for the duration of one player build.
/// </summary>
internal sealed class OpenBlocksRuntimeSettings : IDisposable
{
    private readonly List<IDisposable> m_Settings = new();
    private bool m_Disposed;

    internal OpenBlocksRuntimeSettings(BuildOpenBlocks.Arguments arguments)
    {
        try
        {
#if XR_DISABLED
            if (arguments.Runtime != BuildOpenBlocks.Runtime.Monoscopic)
            {
                throw new BuildFailedException(
                    "XR_DISABLED builds only support the Monoscopic runtime.");
            }
#else
            m_Settings.Add(new TemporaryAndroidRuntimeSettings(arguments));
            m_Settings.Add(new TemporaryGraphicsApis(arguments));
            m_Settings.Add(new TemporaryXrLoader(arguments));
            m_Settings.Add(new TemporaryOpenXrFeatures(arguments));
#endif
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

        for (int index = m_Settings.Count - 1; index >= 0; --index)
        {
            try
            {
                m_Settings[index].Dispose();
            }
            catch (Exception exception)
            {
                Debug.LogError($"{BuildOpenBlocks.LogPrefix} Failed to restore runtime settings: {exception}");
            }
        }
        Debug.Log($"{BuildOpenBlocks.LogPrefix} Restored runtime-specific build settings.");
    }

#if !XR_DISABLED
    internal static IReadOnlyList<Type> GetRequiredOpenXrFeatureTypes(
        BuildOpenBlocks.Arguments arguments)
    {
        return TemporaryOpenXrFeatures.GetRequiredFeatures(arguments);
    }

    private sealed class TemporaryAndroidRuntimeSettings : IDisposable
    {
        private readonly bool m_ShouldRestore;
        private readonly AndroidApplicationEntry m_ApplicationEntry;
        private readonly bool m_ResizeableActivity;
        private readonly AndroidSdkVersions m_MinSdkVersion;
        private readonly UIOrientation m_Orientation;
        private bool m_Disposed;

        internal TemporaryAndroidRuntimeSettings(BuildOpenBlocks.Arguments arguments)
        {
            if (arguments.Target != BuildTarget.Android ||
                (arguments.Runtime != BuildOpenBlocks.Runtime.AndroidXR &&
                 arguments.Runtime != BuildOpenBlocks.Runtime.MetaQuest))
            {
                return;
            }

            m_ShouldRestore = true;
            m_ApplicationEntry = PlayerSettings.Android.applicationEntry;
            m_ResizeableActivity = PlayerSettings.Android.resizeableActivity;
            m_MinSdkVersion = PlayerSettings.Android.minSdkVersion;
            m_Orientation = PlayerSettings.defaultInterfaceOrientation;

            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.GameActivity;
            if (arguments.Runtime == BuildOpenBlocks.Runtime.AndroidXR)
            {
                PlayerSettings.Android.resizeableActivity = true;
                if ((int)PlayerSettings.Android.minSdkVersion <
                    (int)AndroidSdkVersions.AndroidApiLevel26)
                {
                    PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
                }
            }
            else
            {
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            }

            Debug.Log($"{BuildOpenBlocks.LogPrefix} Applied {arguments.Runtime} Android Player Settings.");
        }

        public void Dispose()
        {
            if (m_Disposed || !m_ShouldRestore)
            {
                return;
            }
            m_Disposed = true;
            PlayerSettings.Android.applicationEntry = m_ApplicationEntry;
            PlayerSettings.Android.resizeableActivity = m_ResizeableActivity;
            PlayerSettings.Android.minSdkVersion = m_MinSdkVersion;
            PlayerSettings.defaultInterfaceOrientation = m_Orientation;
        }
    }

    private sealed class TemporaryGraphicsApis : IDisposable
    {
        private readonly BuildTarget m_Target;
        private readonly bool m_ShouldRestore;
        private readonly bool m_UseDefaultGraphicsApis;
        private readonly GraphicsDeviceType[] m_GraphicsApis;
        private bool m_Disposed;

        internal TemporaryGraphicsApis(BuildOpenBlocks.Arguments arguments)
        {
            if (arguments.Target != BuildTarget.Android ||
                arguments.Runtime == BuildOpenBlocks.Runtime.Monoscopic)
            {
                return;
            }

            m_Target = arguments.Target;
            m_ShouldRestore = true;
            m_UseDefaultGraphicsApis = PlayerSettings.GetUseDefaultGraphicsAPIs(m_Target);
            m_GraphicsApis = PlayerSettings.GetGraphicsAPIs(m_Target);
            try
            {
                PlayerSettings.SetUseDefaultGraphicsAPIs(m_Target, false);
                PlayerSettings.SetGraphicsAPIs(m_Target, new[] { GraphicsDeviceType.Vulkan });
                Debug.Log($"{BuildOpenBlocks.LogPrefix} Selected Vulkan for {arguments.Runtime}.");
            }
            catch
            {
                Restore();
                throw;
            }
        }

        public void Dispose()
        {
            if (m_Disposed || !m_ShouldRestore)
            {
                return;
            }
            m_Disposed = true;
            Restore();
        }

        private void Restore()
        {
            PlayerSettings.SetGraphicsAPIs(m_Target, m_GraphicsApis);
            PlayerSettings.SetUseDefaultGraphicsAPIs(m_Target, m_UseDefaultGraphicsApis);
        }
    }

    private sealed class TemporaryXrLoader : IDisposable
    {
        private readonly BuildTargetGroup m_TargetGroup;
        private readonly UnityEngine.XR.Management.XRGeneralSettings m_GeneralSettings;
        private readonly List<XRLoader> m_Loaders;
        private readonly bool m_InitManagerOnStart;
        private bool m_Disposed;

        internal TemporaryXrLoader(BuildOpenBlocks.Arguments arguments)
        {
            m_TargetGroup = BuildPipeline.GetBuildTargetGroup(arguments.Target);
            m_GeneralSettings =
                XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(m_TargetGroup);
            if (m_GeneralSettings == null || m_GeneralSettings.Manager == null)
            {
                throw new BuildFailedException(
                    $"Could not load XR Management settings for {m_TargetGroup}.");
            }

            m_Loaders = m_GeneralSettings.Manager.activeLoaders.ToList();
            m_InitManagerOnStart = m_GeneralSettings.InitManagerOnStart;

            try
            {
                if (arguments.Runtime == BuildOpenBlocks.Runtime.Monoscopic)
                {
                    if (!m_GeneralSettings.Manager.TrySetLoaders(new List<XRLoader>()))
                    {
                        throw new BuildFailedException(
                            $"Could not disable XR loaders for {m_TargetGroup}.");
                    }
                    m_GeneralSettings.InitManagerOnStart = false;
                }
                else
                {
                    XRLoader openXrLoader = m_Loaders.FirstOrDefault(loader => loader is OpenXRLoader);
                    if (openXrLoader == null)
                    {
                        throw new BuildFailedException(
                            $"OpenXR loader is not assigned for {m_TargetGroup}.");
                    }
                    if (!m_GeneralSettings.Manager.TrySetLoaders(new List<XRLoader> { openXrLoader }))
                    {
                        throw new BuildFailedException(
                            $"Could not select the OpenXR loader for {m_TargetGroup}.");
                    }
                    m_GeneralSettings.InitManagerOnStart = true;
                }

                EditorUtility.SetDirty(m_GeneralSettings);
                EditorUtility.SetDirty(m_GeneralSettings.Manager);
                Debug.Log($"{BuildOpenBlocks.LogPrefix} XR loader profile={arguments.Runtime}, TargetGroup={m_TargetGroup}.");
            }
            catch
            {
                Restore();
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
            Restore();
        }

        private void Restore()
        {
            if (!m_GeneralSettings.Manager.TrySetLoaders(m_Loaders))
            {
                Debug.LogError($"{BuildOpenBlocks.LogPrefix} Could not restore XR loaders for {m_TargetGroup}.");
            }
            m_GeneralSettings.InitManagerOnStart = m_InitManagerOnStart;
            EditorUtility.SetDirty(m_GeneralSettings);
            EditorUtility.SetDirty(m_GeneralSettings.Manager);
        }
    }

    private sealed class TemporaryOpenXrFeatures : IDisposable
    {
        private static readonly Type[] GenericAndroidOpenXrFeatures =
        {
            typeof(FoveatedRenderingFeature),
            typeof(MetaQuestTouchPlusControllerProfile),
            typeof(OculusTouchControllerProfile),
            typeof(PICO4ControllerProfile),
        };

        private static readonly Type[] AndroidXrFeatures =
        {
            typeof(AndroidXRSupportFeature),
            typeof(FoveatedRenderingFeature),
            typeof(HandInteractionProfile),
        };

        private static readonly Type[] MetaQuestFeatures =
        {
            typeof(FoveatedRenderingFeature),
            typeof(MetaQuestFeature),
            typeof(MetaQuestTouchPlusControllerProfile),
            typeof(OculusTouchControllerProfile),
        };

        private static readonly Type[] StandaloneOpenXrFeatures =
        {
            typeof(OculusTouchControllerProfile),
        };

        private readonly OpenXRSettings m_Settings;
        private readonly Dictionary<OpenXRFeature, bool> m_EnabledStates;
        private bool m_Disposed;

        internal TemporaryOpenXrFeatures(BuildOpenBlocks.Arguments arguments)
        {
            BuildTargetGroup targetGroup = BuildPipeline.GetBuildTargetGroup(arguments.Target);
            m_Settings = OpenXRSettings.GetSettingsForBuildTargetGroup(targetGroup);
            if (m_Settings == null)
            {
                throw new BuildFailedException(
                    $"Could not load OpenXR settings for {targetGroup}.");
            }

            OpenXRFeature[] features = m_Settings.GetFeatures();
            m_EnabledStates = features.ToDictionary(feature => feature, feature => feature.enabled);
            try
            {
                foreach (OpenXRFeature feature in features)
                {
                    feature.enabled = false;
                }

                IReadOnlyList<Type> requiredFeatures = GetRequiredFeatures(arguments);
                foreach (Type requiredType in requiredFeatures)
                {
                    OpenXRFeature requiredFeature = m_Settings.GetFeature(requiredType);
                    if (requiredFeature == null)
                    {
                        throw new BuildFailedException(
                            $"Required OpenXR feature {requiredType.FullName} is not installed for {targetGroup}.");
                    }
                    requiredFeature.enabled = true;
                }

                EditorUtility.SetDirty(m_Settings);
                string featureNames = string.Join(", ", requiredFeatures.Select(type => type.Name));
                Debug.Log($"{BuildOpenBlocks.LogPrefix} Required OpenXR features for {arguments.Runtime}: {featureNames}.");
            }
            catch
            {
                Restore();
                throw;
            }
        }

        internal static IReadOnlyList<Type> GetRequiredFeatures(BuildOpenBlocks.Arguments arguments)
        {
            if (arguments.Runtime == BuildOpenBlocks.Runtime.Monoscopic)
            {
                return Array.Empty<Type>();
            }
            if (arguments.Target == BuildTarget.StandaloneWindows64)
            {
                return StandaloneOpenXrFeatures;
            }

            return arguments.Runtime switch
            {
                BuildOpenBlocks.Runtime.OpenXR => GenericAndroidOpenXrFeatures,
                BuildOpenBlocks.Runtime.AndroidXR => AndroidXrFeatures,
                BuildOpenBlocks.Runtime.MetaQuest => MetaQuestFeatures,
                _ => throw new BuildFailedException(
                    $"No OpenXR feature profile exists for {arguments.Runtime}."),
            };
        }

        public void Dispose()
        {
            if (m_Disposed)
            {
                return;
            }
            m_Disposed = true;
            Restore();
        }

        private void Restore()
        {
            foreach (KeyValuePair<OpenXRFeature, bool> state in m_EnabledStates)
            {
                state.Key.enabled = state.Value;
            }
            EditorUtility.SetDirty(m_Settings);
        }
    }
#endif
}
