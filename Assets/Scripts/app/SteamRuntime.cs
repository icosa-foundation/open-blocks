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
#if UNITY_ANDROID && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using UnityEngine;

namespace com.google.apps.peltzer.client.app
{
    public static class PlatformCapabilities
    {
        public static bool OsCanReachLocalhost(RuntimePlatform platform, bool runningUnderSteam)
        {
            return platform != RuntimePlatform.Android || !runningUnderSteam;
        }
    }

    /// <summary>
    /// Detects whether an Android build is running inside Steam's Lepton environment.
    /// </summary>
    public sealed class SteamRuntime : MonoBehaviour
    {
        private const string kLogPrefix = "[OBSF_20260821]";
        private static SteamRuntime instance;
        private static bool initialized;
        private static bool? runningUnderSteam;

        public static bool RunningUnderSteam
        {
            get
            {
                DetectSteam();
                return runningUnderSteam ?? false;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            instance = null;
            initialized = false;
            runningUnderSteam = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitializeOnAndroid()
        {
            if (Application.platform != RuntimePlatform.Android)
            {
                return;
            }

            var gameObject = new GameObject("SteamRuntime");
            DontDestroyOnLoad(gameObject);
            instance = gameObject.AddComponent<SteamRuntime>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            DetectSteam();
            InitializeSteamworks();
        }

        private void Update()
        {
            if (initialized)
            {
                SteamClientApi.RunCallbacks();
            }
        }

        private void OnDestroy()
        {
            if (instance != this)
            {
                return;
            }

            if (initialized)
            {
                SteamClientApi.Shutdown();
            }

            initialized = false;
            instance = null;
        }

        private static void DetectSteam()
        {
            if (runningUnderSteam.HasValue)
            {
                return;
            }

            try
            {
                runningUnderSteam = SteamClientApi.IsSteamRunning();
                Debug.Log($"{kLogPrefix} Steam client running: {runningUnderSteam.Value}");
            }
            catch (Exception exception)
            {
                runningUnderSteam = false;
                Debug.LogWarning($"{kLogPrefix} Unable to detect Steam client: {exception.Message}");
            }
        }

        private static void InitializeSteamworks()
        {
            if (initialized)
            {
                return;
            }

            try
            {
                initialized = SteamClientApi.Initialize(out string errorMessage);
                if (initialized)
                {
                    // SteamAPI_IsSteamRunning currently reports false under Lepton. Successful
                    // initialization with the launch-provided app ID is authoritative.
                    runningUnderSteam = true;
                    Debug.Log($"{kLogPrefix} Steamworks initialized");
                }
                else
                {
                    Debug.Log($"{kLogPrefix} Steamworks unavailable: {errorMessage}");
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"{kLogPrefix} Steamworks initialization failed: {exception.Message}");
            }
        }

        private static class SteamClientApi
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            private const string NativeLibrary = "steam_api";
            private const int SteamErrorMessageSize = 1024;
            private const int InitResultOk = 0;

            public static bool IsSteamRunning() => NativeIsSteamRunning();

            public static bool Initialize(out string errorMessage)
            {
                var errorBuffer = Marshal.AllocHGlobal(SteamErrorMessageSize);
                try
                {
                    var emptyBuffer = new byte[SteamErrorMessageSize];
                    Marshal.Copy(emptyBuffer, 0, errorBuffer, emptyBuffer.Length);
                    var result = NativeInitFlat(errorBuffer);
                    errorMessage = Marshal.PtrToStringAnsi(errorBuffer) ?? string.Empty;
                    return result == InitResultOk;
                }
                finally
                {
                    Marshal.FreeHGlobal(errorBuffer);
                }
            }

            public static void RunCallbacks() => NativeRunCallbacks();

            public static void Shutdown() => NativeShutdown();

            [DllImport(NativeLibrary, EntryPoint = "SteamAPI_IsSteamRunning",
                CallingConvention = CallingConvention.Cdecl)]
            [return: MarshalAs(UnmanagedType.I1)]
            private static extern bool NativeIsSteamRunning();

            [DllImport(NativeLibrary, EntryPoint = "SteamAPI_InitFlat",
                CallingConvention = CallingConvention.Cdecl)]
            private static extern int NativeInitFlat(IntPtr errorMessage);

            [DllImport(NativeLibrary, EntryPoint = "SteamAPI_RunCallbacks",
                CallingConvention = CallingConvention.Cdecl)]
            private static extern void NativeRunCallbacks();

            [DllImport(NativeLibrary, EntryPoint = "SteamAPI_Shutdown",
                CallingConvention = CallingConvention.Cdecl)]
            private static extern void NativeShutdown();
#else
            public static bool IsSteamRunning() => false;

            public static bool Initialize(out string errorMessage)
            {
                errorMessage = "Steamworks is only initialized by Android player builds";
                return false;
            }

            public static void RunCallbacks() { }

            public static void Shutdown() { }
#endif
        }
    }
}
