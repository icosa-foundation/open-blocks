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
using UnityEngine.XR.Hands;
using UnityEngine.XR.Hands.Gestures;

namespace com.google.apps.peltzer.client.model.controller
{
    /// <summary>
    /// Implements ControllerDevice using OpenXR hand tracking data.
    /// XR Hands common gestures are used for primary hand input:
    ///   Pinch        → Trigger
    ///   Grasp firm   → Grip
    /// Raw joint heuristics are kept only for custom gestures not exposed as common
    /// hand gestures by XR Hands:
    ///   Middle pinch → SecondaryButton
    ///   Pinky pinch  → ApplicationMenu
    ///   Index point direction (palm-relative) → directional axis / touchpad
    /// Haptic calls are silently ignored (hands have no actuators).
    /// </summary>
    public class HandTrackingDevice : ControllerDevice
    {
        private const string PINCH_LOG_PREFIX = "HPIN0422B";
        private const float PINCH_PRESS_THRESHOLD = 0.98f;
        private const float PINCH_RELEASE_THRESHOLD = 0.95f;
        private const float TRIGGER_HALF_PRESS_THRESHOLD = 0.5f;

        private readonly bool isRightHand;
        private XRHandSubsystem handSubsystem;

        // Per-frame gesture state (current and previous frame for edge detection).
        private bool isPinching;
        private bool wasPinchingLastFrame;
        private bool isGripping;
        private bool wasGrippingLastFrame;
        private bool isMenuGesture;
        private bool wasMenuGestureLastFrame;
        private bool isSecondaryGesture;
        private bool wasSecondaryGestureLastFrame;

        // Touchpad emulation.
        private bool touchpadCurrentlyPressed;
        private bool touchpadWasPressedLastFrame;

        // Trigger half-press tracking.
        private bool wasTriggerHalfPressed;
        private float triggerValue;
        private float lastLoggedPinchValue = -1f;
        private float lastLoggedPinchDistance = -1f;

        // Velocity.
        private Vector3 lastWristPosition;
        private Vector3 velocity;
        private bool hasWristPosition;

        public bool IsTrackedObjectValid { get; set; }

        public HandTrackingDevice(bool isRightHand)
        {
            this.isRightHand = isRightHand;
        }

        private XRHand CurrentHand
        {
            get
            {
                if (handSubsystem == null)
                {
                    var subsystems = new List<XRHandSubsystem>();
                    SubsystemManager.GetSubsystems(subsystems);
                    handSubsystem = subsystems.Count > 0 ? subsystems[0] : null;
                }
                if (handSubsystem == null) return default;
                return isRightHand ? handSubsystem.rightHand : handSubsystem.leftHand;
            }
        }

        public void Update()
        {
            XRHand hand = CurrentHand;
            // Shift previous-frame state.
            wasPinchingLastFrame = isPinching;
            wasGrippingLastFrame = isGripping;
            wasMenuGestureLastFrame = isMenuGesture;
            wasSecondaryGestureLastFrame = isSecondaryGesture;
            touchpadWasPressedLastFrame = touchpadCurrentlyPressed;

            IsTrackedObjectValid = hand.isTracked;
            if (!hand.isTracked)
            {
                ClearCurrentState();
                return;
            }

            // Velocity from wrist movement.
            if (HandGestureDetector.TryGetWristPose(hand, out Pose wristPose))
            {
                if (hasWristPosition)
                    velocity = (wristPose.position - lastWristPosition) / Time.deltaTime;
                lastWristPosition = wristPose.position;
                hasWristPosition = true;
            }

            // Evaluate current gestures. Trigger comes from the built-in generic finger-shape
            // pinch metric, which is based on thumb-tip/index-tip proximity.
            isPinching = TryGetPinchState(hand, out triggerValue);
            isGripping = TryGetGripState(hand);
            isMenuGesture = HandGestureDetector.IsPinkyPinching(hand, wasMenuGestureLastFrame);
            isSecondaryGesture = HandGestureDetector.IsMiddlePinching(hand, wasSecondaryGestureLastFrame);

            // Touchpad activated when the virtual axis is outside the centre region or when pinching.
            TouchpadLocation location = GetTouchpadLocation();
            touchpadCurrentlyPressed =
                (location != TouchpadLocation.CENTER && location != TouchpadLocation.NONE) || isPinching;
        }

        public Vector3 GetVelocity() => velocity;

        public bool IsPressed(ButtonId buttonId)
        {
            switch (buttonId)
            {
                case ButtonId.Trigger: return isPinching;
                case ButtonId.Grip: return isGripping;
                case ButtonId.ApplicationMenu: return isMenuGesture;
                case ButtonId.SecondaryButton: return isSecondaryGesture;
                case ButtonId.Touchpad: return touchpadCurrentlyPressed;
                default: return false;
            }
        }

        public bool WasJustPressed(ButtonId buttonId)
        {
            switch (buttonId)
            {
                case ButtonId.Trigger: return isPinching && !wasPinchingLastFrame;
                case ButtonId.Grip: return isGripping && !wasGrippingLastFrame;
                case ButtonId.ApplicationMenu: return isMenuGesture && !wasMenuGestureLastFrame;
                case ButtonId.SecondaryButton: return isSecondaryGesture && !wasSecondaryGestureLastFrame;
                case ButtonId.Touchpad: return touchpadCurrentlyPressed && !touchpadWasPressedLastFrame;
                default: return false;
            }
        }

        public bool WasJustReleased(ButtonId buttonId)
        {
            switch (buttonId)
            {
                case ButtonId.Trigger: return !isPinching && wasPinchingLastFrame;
                case ButtonId.Grip: return !isGripping && wasGrippingLastFrame;
                case ButtonId.ApplicationMenu: return !isMenuGesture && wasMenuGestureLastFrame;
                case ButtonId.SecondaryButton: return !isSecondaryGesture && wasSecondaryGestureLastFrame;
                case ButtonId.Touchpad: return !touchpadCurrentlyPressed && touchpadWasPressedLastFrame;
                default: return false;
            }
        }

        public bool IsTriggerHalfPressed()
        {
            bool isHalfPressed = triggerValue > TRIGGER_HALF_PRESS_THRESHOLD;
            if (isHalfPressed) wasTriggerHalfPressed = true;
            return isHalfPressed;
        }

        public bool WasTriggerJustReleasedFromHalfPress()
        {
            if (wasTriggerHalfPressed && triggerValue <= TRIGGER_HALF_PRESS_THRESHOLD)
            {
                wasTriggerHalfPressed = false;
                return true;
            }
            return false;
        }

        // Touch === press for hands (no capacitive sensors).
        public bool IsTouched(ButtonId buttonId) => IsPressed(buttonId);

        public Vector2 GetDirectionalAxis()
        {
            XRHand hand = CurrentHand;
            if (!hand.isTracked) return Vector2.zero;
            return HandGestureDetector.GetThumbstickAxis(hand);
        }

        public TouchpadLocation GetTouchpadLocation()
        {
            return TouchpadLocationHelper.GetTouchpadLocation(GetDirectionalAxis());
        }

        public Vector2 GetTriggerScale()
        {
            return new Vector2(triggerValue, 0f);
        }

        // Hands have no haptic actuators.
        public void TriggerHapticPulse(ushort durationMicroSec = 500) { }

        private Handedness CurrentHandedness => isRightHand ? Handedness.Right : Handedness.Left;

        private void ClearCurrentState()
        {
            isPinching = false;
            isGripping = false;
            isMenuGesture = false;
            isSecondaryGesture = false;
            touchpadCurrentlyPressed = false;
            triggerValue = 0f;
            lastLoggedPinchValue = -1f;
            lastLoggedPinchDistance = -1f;
            velocity = Vector3.zero;
            hasWristPosition = false;
        }

        private bool TryGetPinchState(XRHand hand, out float pinchAmount)
        {
            pinchAmount = 0f;
            float tipDistance = -1f;

            XRFingerShape fingerShape = hand.CalculateFingerShape(
                XRHandFingerID.Index,
                XRFingerShapeTypes.Pinch);
            if (!fingerShape.TryGetPinch(out float currentPinchValue))
                return false;

            if (hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out Pose indexTipPose) &&
                hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out Pose thumbTipPose))
            {
                tipDistance = Vector3.Distance(indexTipPose.position, thumbTipPose.position);
            }

            pinchAmount = currentPinchValue;
            float threshold = wasPinchingLastFrame ? PINCH_RELEASE_THRESHOLD : PINCH_PRESS_THRESHOLD;
            bool isPressed = currentPinchValue >= threshold;

            bool pinchStateChanged = isPressed != wasPinchingLastFrame;
            bool pinchValueChanged = lastLoggedPinchValue < 0f || Mathf.Abs(currentPinchValue - lastLoggedPinchValue) >= 0.1f;
            bool pinchDistanceChanged = tipDistance >= 0f &&
                (lastLoggedPinchDistance < 0f || Mathf.Abs(tipDistance - lastLoggedPinchDistance) >= 0.005f);
            if (pinchStateChanged || pinchValueChanged || pinchDistanceChanged)
            {
                string handLabel = isRightHand ? "right" : "left";
                string distanceText = tipDistance >= 0f ? $"{tipDistance:F4}" : "n/a";
                Debug.Log(
                    $"[{PINCH_LOG_PREFIX}] {handLabel} pinchValue={currentPinchValue:F3} " +
                    $"tipDistance={distanceText} threshold={threshold:F3} pressed={isPressed} " +
                    $"wasPressed={wasPinchingLastFrame}");
                lastLoggedPinchValue = currentPinchValue;
                lastLoggedPinchDistance = tipDistance;
            }

            return isPressed;
        }

        private bool TryGetGripState(XRHand hand)
        {
            if (TryGetCommonGestures(out XRCommonHandGestures commonGestures) &&
                commonGestures.TryGetGraspFirmState(out bool isGraspFirm))
            {
                return isGraspFirm;
            }

            return HandGestureDetector.IsFistGrip(hand);
        }

        private bool TryGetCommonGestures(out XRCommonHandGestures commonGestures)
        {
            commonGestures = null;
            return handSubsystem != null &&
                handSubsystem.running &&
                handSubsystem.TryGetCommonGestures(CurrentHandedness, out commonGestures);
        }
    }
}
