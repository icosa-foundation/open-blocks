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

namespace com.google.apps.peltzer.client.model.controller
{
    /// <summary>
    /// Implements ControllerDevice using OpenXR hand tracking data.
    /// Gesture → button mapping:
    ///   Index pinch  → Trigger
    ///   Fist         → Grip
    ///   Middle pinch → SecondaryButton
    ///   Pinky pinch  → ApplicationMenu
    ///   Index point direction (palm-relative) → directional axis / touchpad
    /// Haptic calls are silently ignored (hands have no actuators).
    /// </summary>
    public class HandTrackingDevice : ControllerDevice
    {
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
            IsTrackedObjectValid = hand.isTracked;
            if (!hand.isTracked) return;

            // Velocity from wrist movement.
            if (HandGestureDetector.TryGetWristPose(hand, out Pose wristPose))
            {
                if (hasWristPosition)
                    velocity = (wristPose.position - lastWristPosition) / Time.deltaTime;
                lastWristPosition = wristPose.position;
                hasWristPosition = true;
            }

            // Shift previous-frame state.
            wasPinchingLastFrame = isPinching;
            wasGrippingLastFrame = isGripping;
            wasMenuGestureLastFrame = isMenuGesture;
            wasSecondaryGestureLastFrame = isSecondaryGesture;
            touchpadWasPressedLastFrame = touchpadCurrentlyPressed;

            // Evaluate current gestures (hysteretic thresholds).
            isPinching = HandGestureDetector.IsIndexPinching(hand, wasPinchingLastFrame);
            isGripping = HandGestureDetector.IsFistGrip(hand);
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
            // Treat any pinch activity as a half-press so the half-press release edge fires correctly.
            if (isPinching) wasTriggerHalfPressed = true;
            return isPinching;
        }

        public bool WasTriggerJustReleasedFromHalfPress()
        {
            if (wasTriggerHalfPressed && !isPinching)
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
            return new Vector2(isPinching ? 1f : 0f, 0f);
        }

        // Hands have no haptic actuators.
        public void TriggerHapticPulse(ushort durationMicroSec = 500) { }
    }
}
