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

using UnityEngine;
using UnityEngine.XR.Hands;

namespace com.google.apps.peltzer.client.model.controller
{
    /// <summary>
    /// Static helpers for wrist pose, virtual thumbstick axis, and fallback raw-joint
    /// gesture classification when package-provided gesture state is unavailable.
    /// All distance thresholds are in meters.
    /// </summary>
    public static class HandGestureDetector
    {
        // Hysteretic thresholds to avoid rapid toggling.
        private const float PINCH_ENGAGE_DIST = 0.025f;
        private const float PINCH_RELEASE_DIST = 0.040f;
        // Finger tip must be within this distance of the palm centre to count as curled.
        private const float CURL_DIST = 0.055f;
        // Palm-relative axis range that maps to a fully-deflected virtual thumbstick.
        private const float AXIS_RANGE = 0.06f;

        /// <summary>Index + thumb pinch — maps to Trigger.</summary>
        public static bool IsIndexPinching(XRHand hand, bool wasActivePreviousFrame)
        {
            float threshold = wasActivePreviousFrame ? PINCH_RELEASE_DIST : PINCH_ENGAGE_DIST;
            return PinchDistance(hand, XRHandJointID.IndexTip) < threshold;
        }

        /// <summary>Middle + thumb pinch — maps to SecondaryButton.</summary>
        public static bool IsMiddlePinching(XRHand hand, bool wasActivePreviousFrame)
        {
            float threshold = wasActivePreviousFrame ? PINCH_RELEASE_DIST : PINCH_ENGAGE_DIST;
            return PinchDistance(hand, XRHandJointID.MiddleTip) < threshold;
        }

        /// <summary>Pinky + thumb pinch — maps to ApplicationMenu.</summary>
        public static bool IsPinkyPinching(XRHand hand, bool wasActivePreviousFrame)
        {
            float threshold = wasActivePreviousFrame ? PINCH_RELEASE_DIST : PINCH_ENGAGE_DIST;
            return PinchDistance(hand, XRHandJointID.LittleTip) < threshold;
        }

        /// <summary>
        /// Fist / grip gesture — at least two of {middle, ring, little} finger tips are
        /// curled close to the palm. Maps to Grip.
        /// </summary>
        public static bool IsFistGrip(XRHand hand)
        {
            int curled = 0;
            if (IsFingerCurled(hand, XRHandJointID.MiddleTip)) curled++;
            if (IsFingerCurled(hand, XRHandJointID.RingTip)) curled++;
            if (IsFingerCurled(hand, XRHandJointID.LittleTip)) curled++;
            return curled >= 2;
        }

        /// <summary>
        /// Returns the wrist pose, which is used to drive the controller transform.
        /// </summary>
        public static bool TryGetWristPose(XRHand hand, out Pose pose)
        {
            return hand.GetJoint(XRHandJointID.Wrist).TryGetPose(out pose);
        }

        /// <summary>
        /// Returns a 2-D axis representing where the index finger is pointing relative
        /// to the palm's local frame. Suitable for driving the virtual thumbstick.
        ///   +Y = palm-up direction
        ///   +X = palm-right direction
        /// When the hand is in a relaxed pose the axis stays near (0,0).
        /// </summary>
        public static Vector2 GetThumbstickAxis(XRHand hand)
        {
            if (!hand.GetJoint(XRHandJointID.Palm).TryGetPose(out Pose palmPose) ||
                !hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out Pose indexTipPose))
            {
                return Vector2.zero;
            }

            Vector3 toIndex = indexTipPose.position - palmPose.position;
            float x = Vector3.Dot(toIndex, palmPose.rotation * Vector3.right);
            float y = Vector3.Dot(toIndex, palmPose.rotation * Vector3.up);
            return Vector2.ClampMagnitude(new Vector2(x, y) / AXIS_RANGE, 1f);
        }

        // --- private helpers ---

        private static float PinchDistance(XRHand hand, XRHandJointID fingerTipId)
        {
            if (!hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out Pose thumbPose) ||
                !hand.GetJoint(fingerTipId).TryGetPose(out Pose fingerPose))
            {
                return float.MaxValue;
            }
            return Vector3.Distance(thumbPose.position, fingerPose.position);
        }

        private static bool IsFingerCurled(XRHand hand, XRHandJointID tipId)
        {
            if (!hand.GetJoint(XRHandJointID.Palm).TryGetPose(out Pose palmPose) ||
                !hand.GetJoint(tipId).TryGetPose(out Pose tipPose))
            {
                return false;
            }
            return Vector3.Distance(palmPose.position, tipPose.position) < CURL_DIST;
        }
    }
}
