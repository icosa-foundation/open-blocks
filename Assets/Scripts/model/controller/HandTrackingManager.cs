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
using UnityEngine.XR.Hands;
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

        private XRHandSubsystem handSubsystem;
        private bool initialized;

        private bool hasLoggedState;
        private bool lastRightTracked;
        private bool lastLeftTracked;
        private bool lastPeltzerUsingHand;
        private bool lastPaletteUsingHand;
        private bool lastPeltzerPoseDriverEnabled;
        private bool lastPalettePoseDriverEnabled;
        private bool lastPeltzerIsRight;

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

            rightHandDevice = new HandTrackingDevice(isRightHand: true);
            leftHandDevice = new HandTrackingDevice(isRightHand: false);

            initialized = true;
        }

        // -------------------------------------------------------------------
        // Unity lifecycle
        // -------------------------------------------------------------------

        private void Start()
        {
            var subsystems = new List<XRHandSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);
            if (subsystems.Count > 0)
            {
                handSubsystem = subsystems[0];
                if (!handSubsystem.running)
                    handSubsystem.Start();
            }

            Debug.Log($"{LOG_PREFIX} start subsystemFound={handSubsystem != null} subsystemRunning={handSubsystem != null && handSubsystem.running}");
        }

        private void Update()
        {
            if (!initialized || handSubsystem == null) return;

            bool peltzerIsRight = PeltzerMain.Instance.peltzerControllerInRightHand;
            bool rightTracked = handSubsystem.rightHand.isTracked;
            bool leftTracked = handSubsystem.leftHand.isTracked;

            bool peltzerHandTracked = peltzerIsRight ? rightTracked : leftTracked;
            bool paletteHandTracked = peltzerIsRight ? leftTracked : rightTracked;
            HandTrackingDevice peltzerHandDevice = peltzerIsRight ? rightHandDevice : leftHandDevice;
            HandTrackingDevice paletteHandDevice = peltzerIsRight ? leftHandDevice : rightHandDevice;

            LogStateIfChanged(
                rightTracked,
                leftTracked,
                peltzerController.controller == peltzerHandDevice,
                paletteController.controller == paletteHandDevice,
                peltzerPoseDriver == null || peltzerPoseDriver.enabled,
                palettePoseDriver == null || palettePoseDriver.enabled,
                peltzerIsRight);

            // Switch peltzer (brush) controller ↔ hand.
            if (peltzerHandTracked && peltzerController.controller == peltzerControllerDevice)
            {
                Debug.Log($"{LOG_PREFIX} swap peltzer->hand peltzerIsRight={peltzerIsRight} rightTracked={rightTracked} leftTracked={leftTracked}");
                peltzerController.controller = peltzerHandDevice;
                if (peltzerPoseDriver != null) peltzerPoseDriver.enabled = false;
            }
            else if (!peltzerHandTracked && peltzerController.controller == peltzerHandDevice)
            {
                Debug.Log($"{LOG_PREFIX} swap peltzer->controller peltzerIsRight={peltzerIsRight} rightTracked={rightTracked} leftTracked={leftTracked}");
                peltzerController.controller = peltzerControllerDevice;
                if (peltzerPoseDriver != null) peltzerPoseDriver.enabled = true;
            }

            // Switch palette (wand) controller ↔ hand.
            if (paletteHandTracked && paletteController.controller == paletteControllerDevice)
            {
                Debug.Log($"{LOG_PREFIX} swap palette->hand peltzerIsRight={peltzerIsRight} rightTracked={rightTracked} leftTracked={leftTracked}");
                paletteController.controller = paletteHandDevice;
                if (palettePoseDriver != null) palettePoseDriver.enabled = false;
            }
            else if (!paletteHandTracked && paletteController.controller == paletteHandDevice)
            {
                Debug.Log($"{LOG_PREFIX} swap palette->controller peltzerIsRight={peltzerIsRight} rightTracked={rightTracked} leftTracked={leftTracked}");
                paletteController.controller = paletteControllerDevice;
                if (palettePoseDriver != null) palettePoseDriver.enabled = true;
            }
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
        }

        private void OnDestroy()
        {
            // Restore original devices so nothing is left in a broken state.
            if (!initialized) return;
            if (peltzerController != null) peltzerController.controller = peltzerControllerDevice;
            if (paletteController != null) paletteController.controller = paletteControllerDevice;
            if (peltzerPoseDriver != null) peltzerPoseDriver.enabled = true;
            if (palettePoseDriver != null) palettePoseDriver.enabled = true;
        }

        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------

        private void LogStateIfChanged(
            bool rightTracked,
            bool leftTracked,
            bool peltzerUsingHand,
            bool paletteUsingHand,
            bool peltzerPoseEnabled,
            bool palettePoseEnabled,
            bool peltzerIsRight)
        {
            if (hasLoggedState &&
                lastRightTracked == rightTracked &&
                lastLeftTracked == leftTracked &&
                lastPeltzerUsingHand == peltzerUsingHand &&
                lastPaletteUsingHand == paletteUsingHand &&
                lastPeltzerPoseDriverEnabled == peltzerPoseEnabled &&
                lastPalettePoseDriverEnabled == palettePoseEnabled &&
                lastPeltzerIsRight == peltzerIsRight)
            {
                return;
            }

            hasLoggedState = true;
            lastRightTracked = rightTracked;
            lastLeftTracked = leftTracked;
            lastPeltzerUsingHand = peltzerUsingHand;
            lastPaletteUsingHand = paletteUsingHand;
            lastPeltzerPoseDriverEnabled = peltzerPoseEnabled;
            lastPalettePoseDriverEnabled = palettePoseEnabled;
            lastPeltzerIsRight = peltzerIsRight;

            Debug.Log(
                $"{LOG_PREFIX} state peltzerIsRight={peltzerIsRight} " +
                $"rightTracked={rightTracked} leftTracked={leftTracked} " +
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
    }
}
