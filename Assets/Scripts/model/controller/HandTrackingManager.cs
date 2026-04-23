// Copyright 2024 The Open Blocks Authors
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
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.OpenXR;
using com.google.apps.peltzer.client.model.main;

namespace com.google.apps.peltzer.client.model.controller
{
    /// <summary>
    /// Manages the automatic transition between physical controllers and hand tracking.
    ///
    /// When a tracked hand is detected it:
    ///   1. Replaces the PeltzerController / PaletteController's ControllerDevice with a
    ///      HandTrackingDevice for that hand.
    ///   2. Disables the TrackedPoseDriver so it no longer fights the hand-joint transform.
    ///   3. Drives the controller GameObjects' transforms from wrist joint data each frame.
    ///
    /// When the hand is lost, everything reverts to the original controller device and pose
    /// driver so normal controller operation resumes.
    ///
    /// Add this component to any persistent GameObject (e.g. PeltzerMain) via AddComponent
    /// after the controllers have been set up.
    /// </summary>
    public class HandTrackingManager : MonoBehaviour
    {
        private const string LOG_PREFIX = "[HTOWN0422]";
        private const float HAND_SUBSYSTEM_START_DELAY_SEC = 2.0f;
        private const float HAND_OWNERSHIP_SWITCH_DELAY_SEC = 0.2f;
        private const float CONTROLLER_RECLAIM_DELAY_SEC = 0.2f;
        private const float HAND_JOINT_DEBUG_SIZE_M = 0.008f;
        private static readonly Color HAND_MODE_COLOR = new(0.1f, 1f, 0.1f, 1f);
        private static readonly Color CONTROLLER_MODE_COLOR = new(0.1f, 0.6f, 1f, 1f);
        private static readonly Color RIGHT_HAND_DEBUG_COLOR = new(1f, 0.85f, 0.2f, 1f);
        private static readonly Color LEFT_HAND_DEBUG_COLOR = new(1f, 0.35f, 0.2f, 1f);
        private static readonly XRHandJointID[] DEBUG_JOINT_IDS =
        {
            XRHandJointID.Wrist,
            XRHandJointID.Palm,
            XRHandJointID.ThumbMetacarpal,
            XRHandJointID.ThumbProximal,
            XRHandJointID.ThumbDistal,
            XRHandJointID.ThumbTip,
            XRHandJointID.IndexMetacarpal,
            XRHandJointID.IndexProximal,
            XRHandJointID.IndexIntermediate,
            XRHandJointID.IndexDistal,
            XRHandJointID.IndexTip,
            XRHandJointID.MiddleMetacarpal,
            XRHandJointID.MiddleProximal,
            XRHandJointID.MiddleIntermediate,
            XRHandJointID.MiddleDistal,
            XRHandJointID.MiddleTip,
            XRHandJointID.RingMetacarpal,
            XRHandJointID.RingProximal,
            XRHandJointID.RingIntermediate,
            XRHandJointID.RingDistal,
            XRHandJointID.RingTip,
            XRHandJointID.LittleMetacarpal,
            XRHandJointID.LittleProximal,
            XRHandJointID.LittleIntermediate,
            XRHandJointID.LittleDistal,
            XRHandJointID.LittleTip,
        };

        private PeltzerController peltzerController;
        private PaletteController paletteController;

        // Original (controller) devices — cached so we can restore them.
        private ControllerDevice peltzerControllerDevice;
        private ControllerDevice paletteControllerDevice;

        // Hand devices.
        private HandTrackingDevice rightHandDevice;
        private HandTrackingDevice leftHandDevice;

        // Pose drivers (may be null if not present on the GameObject).
        private TrackedPoseDriver peltzerPoseDriver;
        private TrackedPoseDriver palettePoseDriver;
        private OpenXRHandSubsystemManager handSubsystemManager;

        private XRHandSubsystem handSubsystem;
        private bool initialized;
        private bool handSubsystemStartRequested;
        private float controllersMissingSince = -1f;
        private float rightHandUsableSince = -1f;
        private float leftHandUsableSince = -1f;
        private float rightHandUnusableSince = -1f;
        private float leftHandUnusableSince = -1f;
        private float rightControllerTrackedSince = -1f;
        private float leftControllerTrackedSince = -1f;

        private bool hasLoggedState;
        private bool lastRightTracked;
        private bool lastLeftTracked;
        private bool lastRightControllerTracked;
        private bool lastLeftControllerTracked;
        private bool lastControllersAbsentStable;
        private bool lastRightHandUsableStable;
        private bool lastLeftHandUsableStable;
        private bool lastRightHandUnusableStable;
        private bool lastLeftHandUnusableStable;
        private bool lastRightControllerTrackedStable;
        private bool lastLeftControllerTrackedStable;
        private bool lastPeltzerUsingHand;
        private bool lastPaletteUsingHand;
        private bool lastPeltzerPoseDriverEnabled;
        private bool lastPalettePoseDriverEnabled;
        private bool lastPeltzerIsRight;
        private Renderer peltzerModeIndicator;
        private Renderer paletteModeIndicator;
        private Material peltzerModeIndicatorMaterial;
        private Material paletteModeIndicatorMaterial;
        private Transform[] rightHandJointDebugPoints;
        private Transform[] leftHandJointDebugPoints;
        private bool lastIndicatorPeltzerUsingHand;
        private bool lastIndicatorPaletteUsingHand;
        private bool indicatorsInitialized;

        // -------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------

        /// <summary>
        /// Call this once, after PeltzerController.Setup() and PaletteController.Setup() have run.
        /// </summary>
        public void Setup(PeltzerController peltzer, PaletteController palette)
        {
            peltzerController = peltzer;
            paletteController = palette;

            peltzerControllerDevice = peltzer.controller;
            paletteControllerDevice = palette.controller;

            peltzerPoseDriver = peltzer.GetComponent<TrackedPoseDriver>();
            palettePoseDriver = palette.GetComponent<TrackedPoseDriver>();

            handSubsystemManager = GetComponent<OpenXRHandSubsystemManager>();
            if (handSubsystemManager == null)
                handSubsystemManager = gameObject.AddComponent<OpenXRHandSubsystemManager>();

            rightHandDevice = new HandTrackingDevice(isRightHand: true);
            leftHandDevice = new HandTrackingDevice(isRightHand: false);
            peltzerModeIndicator = CreateModeIndicator(peltzer.transform, "PeltzerModeIndicator", out peltzerModeIndicatorMaterial);
            paletteModeIndicator = CreateModeIndicator(palette.transform, "PaletteModeIndicator", out paletteModeIndicatorMaterial);
            rightHandJointDebugPoints = CreateHandJointDebugPoints("RightHandDebugPoints", RIGHT_HAND_DEBUG_COLOR);
            leftHandJointDebugPoints = CreateHandJointDebugPoints("LeftHandDebugPoints", LEFT_HAND_DEBUG_COLOR);
            UpdateModeIndicators(false, false, force: true);

            initialized = true;
        }

        // -------------------------------------------------------------------
        // Unity lifecycle
        // -------------------------------------------------------------------

        private void Start()
        {
            RefreshHandSubsystemReference();
            Debug.Log($"{LOG_PREFIX} start subsystemFound={handSubsystem != null} subsystemRunning={handSubsystem != null && handSubsystem.running}");
        }

        private void Update()
        {
            if (!initialized) return;

            EnsureHandSubsystemStartedIfControllersMissing();
            if (handSubsystem == null) return;

            bool peltzerIsRight = PeltzerMain.Instance.peltzerControllerInRightHand;
            bool rightControllerTracked = IsControllerTracked(InputDeviceCharacteristics.Right);
            bool leftControllerTracked = IsControllerTracked(InputDeviceCharacteristics.Left);
            bool controllersAbsentStable = AreControllersAbsentStable(rightControllerTracked, leftControllerTracked);
            bool rightTracked = handSubsystem.rightHand.isTracked;
            bool leftTracked = handSubsystem.leftHand.isTracked;
            bool rightHandUsable = IsHandPoseUsable(handSubsystem.rightHand);
            bool leftHandUsable = IsHandPoseUsable(handSubsystem.leftHand);
            bool rightHandUsableStable = UpdateStableSince(ref rightHandUsableSince, rightHandUsable, HAND_OWNERSHIP_SWITCH_DELAY_SEC);
            bool leftHandUsableStable = UpdateStableSince(ref leftHandUsableSince, leftHandUsable, HAND_OWNERSHIP_SWITCH_DELAY_SEC);
            bool rightHandUnusableStable = UpdateStableSince(ref rightHandUnusableSince, !rightHandUsable, CONTROLLER_RECLAIM_DELAY_SEC);
            bool leftHandUnusableStable = UpdateStableSince(ref leftHandUnusableSince, !leftHandUsable, CONTROLLER_RECLAIM_DELAY_SEC);
            bool rightControllerTrackedStable = UpdateStableSince(ref rightControllerTrackedSince, rightControllerTracked, CONTROLLER_RECLAIM_DELAY_SEC);
            bool leftControllerTrackedStable = UpdateStableSince(ref leftControllerTrackedSince, leftControllerTracked, CONTROLLER_RECLAIM_DELAY_SEC);

            bool peltzerHandEligible = controllersAbsentStable && (peltzerIsRight ? rightHandUsableStable : leftHandUsableStable);
            bool paletteHandEligible = controllersAbsentStable && (peltzerIsRight ? leftHandUsableStable : rightHandUsableStable);
            bool peltzerControllerEligible = peltzerIsRight ? rightControllerTrackedStable && rightHandUnusableStable : leftControllerTrackedStable && leftHandUnusableStable;
            bool paletteControllerEligible = peltzerIsRight ? leftControllerTrackedStable && leftHandUnusableStable : rightControllerTrackedStable && rightHandUnusableStable;
            HandTrackingDevice peltzerHandDevice = peltzerIsRight ? rightHandDevice : leftHandDevice;
            HandTrackingDevice paletteHandDevice = peltzerIsRight ? leftHandDevice : rightHandDevice;

            LogStateIfChanged(
                rightControllerTracked,
                leftControllerTracked,
                rightTracked,
                leftTracked,
                controllersAbsentStable,
                rightHandUsableStable,
                leftHandUsableStable,
                rightHandUnusableStable,
                leftHandUnusableStable,
                rightControllerTrackedStable,
                leftControllerTrackedStable,
                peltzerController.controller == peltzerHandDevice,
                paletteController.controller == paletteHandDevice,
                peltzerPoseDriver == null || peltzerPoseDriver.enabled,
                palettePoseDriver == null || palettePoseDriver.enabled,
                peltzerIsRight);

            // Switch peltzer (brush) controller ↔ hand.
            if (peltzerHandEligible && peltzerController.controller == peltzerControllerDevice)
            {
                Debug.Log(
                    $"{LOG_PREFIX} swap peltzer->hand peltzerIsRight={peltzerIsRight} " +
                    $"rightControllerTracked={rightControllerTracked} leftControllerTracked={leftControllerTracked} " +
                    $"rightHandUsableStable={rightHandUsableStable} leftHandUsableStable={leftHandUsableStable}");
                peltzerController.controller = peltzerHandDevice;
                if (peltzerPoseDriver != null) peltzerPoseDriver.enabled = false;
            }
            else if (peltzerControllerEligible && peltzerController.controller == peltzerHandDevice)
            {
                Debug.Log(
                    $"{LOG_PREFIX} swap peltzer->controller peltzerIsRight={peltzerIsRight} " +
                    $"rightControllerTracked={rightControllerTracked} leftControllerTracked={leftControllerTracked} " +
                    $"rightHandUnusableStable={rightHandUnusableStable} leftHandUnusableStable={leftHandUnusableStable}");
                peltzerController.controller = peltzerControllerDevice;
                if (peltzerPoseDriver != null) peltzerPoseDriver.enabled = true;
            }

            // Switch palette (wand) controller ↔ hand.
            if (paletteHandEligible && paletteController.controller == paletteControllerDevice)
            {
                Debug.Log(
                    $"{LOG_PREFIX} swap palette->hand peltzerIsRight={peltzerIsRight} " +
                    $"rightControllerTracked={rightControllerTracked} leftControllerTracked={leftControllerTracked} " +
                    $"rightHandUsableStable={rightHandUsableStable} leftHandUsableStable={leftHandUsableStable}");
                paletteController.controller = paletteHandDevice;
                if (palettePoseDriver != null) palettePoseDriver.enabled = false;
            }
            else if (paletteControllerEligible && paletteController.controller == paletteHandDevice)
            {
                Debug.Log(
                    $"{LOG_PREFIX} swap palette->controller peltzerIsRight={peltzerIsRight} " +
                    $"rightControllerTracked={rightControllerTracked} leftControllerTracked={leftControllerTracked} " +
                    $"rightHandUnusableStable={rightHandUnusableStable} leftHandUnusableStable={leftHandUnusableStable}");
                paletteController.controller = paletteControllerDevice;
                if (palettePoseDriver != null) palettePoseDriver.enabled = true;
            }

            UpdateModeIndicators(
                peltzerController.controller == peltzerHandDevice,
                paletteController.controller == paletteHandDevice);
        }

        private void LateUpdate()
        {
            if (!initialized || handSubsystem == null) return;

            bool peltzerIsRight = PeltzerMain.Instance.peltzerControllerInRightHand;
            XRHand peltzerHand = peltzerIsRight ? handSubsystem.rightHand : handSubsystem.leftHand;
            XRHand paletteHand = peltzerIsRight ? handSubsystem.leftHand : handSubsystem.rightHand;

            // Only update the transform when in hand mode (pose driver is disabled).
            if (peltzerPoseDriver != null && !peltzerPoseDriver.enabled && peltzerHand.isTracked)
                ApplyWristPose(peltzerController.transform, peltzerHand);

            if (palettePoseDriver != null && !palettePoseDriver.enabled && paletteHand.isTracked)
                ApplyWristPose(paletteController.transform, paletteHand);

            UpdateHandJointDebugPoints(handSubsystem.rightHand, rightHandJointDebugPoints);
            UpdateHandJointDebugPoints(handSubsystem.leftHand, leftHandJointDebugPoints);
        }

        private void OnDestroy()
        {
            // Restore original devices so nothing is left in a broken state.
            if (!initialized) return;
            if (peltzerController != null) peltzerController.controller = peltzerControllerDevice;
            if (paletteController != null) paletteController.controller = paletteControllerDevice;
            if (peltzerPoseDriver != null) peltzerPoseDriver.enabled = true;
            if (palettePoseDriver != null) palettePoseDriver.enabled = true;
            if (handSubsystemManager != null) handSubsystemManager.enabled = false;
            if (peltzerModeIndicatorMaterial != null) Destroy(peltzerModeIndicatorMaterial);
            if (paletteModeIndicatorMaterial != null) Destroy(paletteModeIndicatorMaterial);
            DestroyHandJointDebugPoints(rightHandJointDebugPoints);
            DestroyHandJointDebugPoints(leftHandJointDebugPoints);
        }

        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------

        private void LogStateIfChanged(
            bool rightControllerTracked,
            bool leftControllerTracked,
            bool rightTracked,
            bool leftTracked,
            bool controllersAbsentStable,
            bool rightHandUsableStable,
            bool leftHandUsableStable,
            bool rightHandUnusableStable,
            bool leftHandUnusableStable,
            bool rightControllerTrackedStable,
            bool leftControllerTrackedStable,
            bool peltzerUsingHand,
            bool paletteUsingHand,
            bool peltzerPoseEnabled,
            bool palettePoseEnabled,
            bool peltzerIsRight)
        {
            if (hasLoggedState &&
                lastRightControllerTracked == rightControllerTracked &&
                lastLeftControllerTracked == leftControllerTracked &&
                lastRightTracked == rightTracked &&
                lastLeftTracked == leftTracked &&
                lastControllersAbsentStable == controllersAbsentStable &&
                lastRightHandUsableStable == rightHandUsableStable &&
                lastLeftHandUsableStable == leftHandUsableStable &&
                lastRightHandUnusableStable == rightHandUnusableStable &&
                lastLeftHandUnusableStable == leftHandUnusableStable &&
                lastRightControllerTrackedStable == rightControllerTrackedStable &&
                lastLeftControllerTrackedStable == leftControllerTrackedStable &&
                lastPeltzerUsingHand == peltzerUsingHand &&
                lastPaletteUsingHand == paletteUsingHand &&
                lastPeltzerPoseDriverEnabled == peltzerPoseEnabled &&
                lastPalettePoseDriverEnabled == palettePoseEnabled &&
                lastPeltzerIsRight == peltzerIsRight)
            {
                return;
            }

            hasLoggedState = true;
            lastRightControllerTracked = rightControllerTracked;
            lastLeftControllerTracked = leftControllerTracked;
            lastRightTracked = rightTracked;
            lastLeftTracked = leftTracked;
            lastControllersAbsentStable = controllersAbsentStable;
            lastRightHandUsableStable = rightHandUsableStable;
            lastLeftHandUsableStable = leftHandUsableStable;
            lastRightHandUnusableStable = rightHandUnusableStable;
            lastLeftHandUnusableStable = leftHandUnusableStable;
            lastRightControllerTrackedStable = rightControllerTrackedStable;
            lastLeftControllerTrackedStable = leftControllerTrackedStable;
            lastPeltzerUsingHand = peltzerUsingHand;
            lastPaletteUsingHand = paletteUsingHand;
            lastPeltzerPoseDriverEnabled = peltzerPoseEnabled;
            lastPalettePoseDriverEnabled = palettePoseEnabled;
            lastPeltzerIsRight = peltzerIsRight;

            Debug.Log(
                $"{LOG_PREFIX} state peltzerIsRight={peltzerIsRight} " +
                $"rightControllerTracked={rightControllerTracked} leftControllerTracked={leftControllerTracked} " +
                $"rightTracked={rightTracked} leftTracked={leftTracked} " +
                $"controllersAbsentStable={controllersAbsentStable} " +
                $"rightHandUsableStable={rightHandUsableStable} leftHandUsableStable={leftHandUsableStable} " +
                $"rightHandUnusableStable={rightHandUnusableStable} leftHandUnusableStable={leftHandUnusableStable} " +
                $"rightControllerTrackedStable={rightControllerTrackedStable} leftControllerTrackedStable={leftControllerTrackedStable} " +
                $"peltzerUsingHand={peltzerUsingHand} paletteUsingHand={paletteUsingHand} " +
                $"peltzerPoseEnabled={peltzerPoseEnabled} palettePoseEnabled={palettePoseEnabled}");
        }

        private static void ApplyWristPose(Transform target, XRHand hand)
        {
            if (HandGestureDetector.TryGetWristPose(hand, out Pose wristPose))
            {
                target.position = wristPose.position;
                target.rotation = wristPose.rotation;
            }
        }

        private void UpdateModeIndicators(bool peltzerUsingHand, bool paletteUsingHand, bool force = false)
        {
            if (!force &&
                indicatorsInitialized &&
                lastIndicatorPeltzerUsingHand == peltzerUsingHand &&
                lastIndicatorPaletteUsingHand == paletteUsingHand)
            {
                return;
            }

            indicatorsInitialized = true;
            lastIndicatorPeltzerUsingHand = peltzerUsingHand;
            lastIndicatorPaletteUsingHand = paletteUsingHand;

            SetModeIndicatorColor(peltzerModeIndicator, peltzerModeIndicatorMaterial, peltzerUsingHand);
            SetModeIndicatorColor(paletteModeIndicator, paletteModeIndicatorMaterial, paletteUsingHand);
        }

        private static Renderer CreateModeIndicator(Transform parent, string name, out Material materialInstance)
        {
            GameObject indicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            indicator.name = name;
            indicator.transform.SetParent(parent, false);
            indicator.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            indicator.transform.localRotation = Quaternion.identity;
            indicator.transform.localScale = Vector3.one * 0.015f;

            Collider collider = indicator.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            Renderer renderer = indicator.GetComponent<Renderer>();
            Shader shader = Shader.Find("Unlit/Color");
            if (shader == null)
                shader = Shader.Find("Standard");

            materialInstance = new Material(shader);
            renderer.material = materialInstance;
            return renderer;
        }

        private static void SetModeIndicatorColor(Renderer renderer, Material material, bool usingHand)
        {
            if (renderer == null || material == null)
                return;

            Color color = usingHand ? HAND_MODE_COLOR : CONTROLLER_MODE_COLOR;
            material.color = color;
            if (material.HasProperty("_EmissionColor"))
                material.SetColor("_EmissionColor", color);
        }

        private Transform[] CreateHandJointDebugPoints(string rootName, Color color)
        {
            GameObject root = new(rootName);
            root.transform.SetParent(transform, false);

            Transform[] debugPoints = new Transform[DEBUG_JOINT_IDS.Length];
            for (int i = 0; i < DEBUG_JOINT_IDS.Length; ++i)
            {
                GameObject point = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                point.name = $"{rootName}_{DEBUG_JOINT_IDS[i]}";
                point.transform.SetParent(root.transform, false);
                point.transform.localScale = Vector3.one * HAND_JOINT_DEBUG_SIZE_M;

                Collider collider = point.GetComponent<Collider>();
                if (collider != null)
                    Destroy(collider);

                Renderer renderer = point.GetComponent<Renderer>();
                Shader shader = Shader.Find("Unlit/Color");
                if (shader == null)
                    shader = Shader.Find("Standard");

                Material materialInstance = new(shader);
                materialInstance.color = color;
                if (materialInstance.HasProperty("_EmissionColor"))
                    materialInstance.SetColor("_EmissionColor", color);
                renderer.material = materialInstance;
                renderer.enabled = false;

                debugPoints[i] = point.transform;
            }

            return debugPoints;
        }

        private static void UpdateHandJointDebugPoints(XRHand hand, Transform[] debugPoints)
        {
            if (debugPoints == null)
                return;

            for (int i = 0; i < DEBUG_JOINT_IDS.Length; ++i)
            {
                Transform point = debugPoints[i];
                if (point == null)
                    continue;

                Renderer renderer = point.GetComponent<Renderer>();
                if (hand.isTracked && hand.GetJoint(DEBUG_JOINT_IDS[i]).TryGetPose(out Pose pose))
                {
                    point.position = pose.position;
                    point.rotation = pose.rotation;
                    if (renderer != null) renderer.enabled = true;
                }
                else
                {
                    if (renderer != null) renderer.enabled = false;
                }
            }
        }

        private static void DestroyHandJointDebugPoints(Transform[] debugPoints)
        {
            if (debugPoints == null)
                return;

            for (int i = 0; i < debugPoints.Length; ++i)
            {
                Transform point = debugPoints[i];
                if (point == null)
                    continue;

                Renderer renderer = point.GetComponent<Renderer>();
                if (renderer != null && renderer.material != null)
                    Destroy(renderer.material);
            }

            if (debugPoints.Length > 0 && debugPoints[0] != null && debugPoints[0].parent != null)
                Destroy(debugPoints[0].parent.gameObject);
        }

        private void EnsureHandSubsystemStartedIfControllersMissing()
        {
            if (handSubsystem != null)
                return;

            bool rightControllerTracked = IsControllerTracked(InputDeviceCharacteristics.Right);
            bool leftControllerTracked = IsControllerTracked(InputDeviceCharacteristics.Left);
            bool bothControllersMissing = !rightControllerTracked && !leftControllerTracked;

            if (!bothControllersMissing)
            {
                controllersMissingSince = -1f;
                return;
            }

            if (controllersMissingSince < 0f)
            {
                controllersMissingSince = Time.unscaledTime;
                return;
            }

            if (handSubsystemStartRequested ||
                Time.unscaledTime - controllersMissingSince < HAND_SUBSYSTEM_START_DELAY_SEC)
            {
                return;
            }

            handSubsystemStartRequested = true;
            Debug.Log(
                $"{LOG_PREFIX} request subsystem start rightControllerTracked={rightControllerTracked} " +
                $"leftControllerTracked={leftControllerTracked} delay={HAND_SUBSYSTEM_START_DELAY_SEC:F2}");

            if (handSubsystemManager != null)
                handSubsystemManager.enabled = true;

            RefreshHandSubsystemReference();
            Debug.Log($"{LOG_PREFIX} subsystem start result found={handSubsystem != null} running={handSubsystem != null && handSubsystem.running}");
            if (handSubsystem == null)
            {
                handSubsystemStartRequested = false;
                controllersMissingSince = Time.unscaledTime;
            }
        }

        private bool AreControllersAbsentStable(bool rightControllerTracked, bool leftControllerTracked)
        {
            return !rightControllerTracked &&
                !leftControllerTracked &&
                controllersMissingSince >= 0f &&
                Time.unscaledTime - controllersMissingSince >= HAND_SUBSYSTEM_START_DELAY_SEC;
        }

        private static bool IsHandPoseUsable(XRHand hand)
        {
            return hand.isTracked && HandGestureDetector.TryGetWristPose(hand, out _);
        }

        private static bool UpdateStableSince(ref float stableSince, bool condition, float delaySec)
        {
            if (!condition)
            {
                stableSince = -1f;
                return false;
            }

            if (stableSince < 0f)
                stableSince = Time.unscaledTime;

            return Time.unscaledTime - stableSince >= delaySec;
        }

        private void RefreshHandSubsystemReference()
        {
            handSubsystem = HandTracking.subsystem;
            if (handSubsystem != null)
                return;

            var subsystems = new List<XRHandSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);
            if (subsystems.Count > 0)
                handSubsystem = subsystems[0];
        }

        private static bool IsControllerTracked(InputDeviceCharacteristics handednessCharacteristic)
        {
            var devices = new List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.Controller |
                InputDeviceCharacteristics.TrackedDevice |
                handednessCharacteristic,
                devices);

            for (int i = 0; i < devices.Count; ++i)
            {
                InputDevice device = devices[i];
                if (!device.isValid)
                    continue;

                if (device.TryGetFeatureValue(CommonUsages.isTracked, out bool isTracked))
                    return isTracked;

                return true;
            }

            return false;
        }
    }
}
