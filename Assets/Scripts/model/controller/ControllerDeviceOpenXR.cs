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

#if OPENXR_SUPPORTED
using TiltBrush;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.OpenXR.Input;
using CommonUsages = UnityEngine.XR.CommonUsages;

namespace com.google.apps.peltzer.client.model.controller
{
    /// <summary>
    ///   Controller SDK logic for OpenXR
    /// </summary>
    public class ControllerDeviceOpenXR : ControllerDevice
    {
        private UnityEngine.XR.InputDevice device;
        private readonly UnityXRInputAction actionSet = new();

        // Haptics.
        // private OVRHapticsClip rumbleHapticsClip;
        private AudioClip rumbleClip;

        private bool isBrush;
        private bool wasTriggerHalfPressed;
        private bool touchpadCurrentlyPressed;
        private bool touchpadWasPressedLastFrame;

        private string actionMap
        {
            get => isBrush ? "Brush" : "Wand";
        }

        // Constructor, taking in a transform such that it can be regularly updated.
        public ControllerDeviceOpenXR(Transform transform)
        {
            // TODO do we need this?
            // this.transform = transform;
            if (rumbleClip != null)
            {
                // rumbleHapticsClip = new OVRHapticsClip(rumbleClip);
            }
        }

        public void Update()
        {
            touchpadWasPressedLastFrame = touchpadCurrentlyPressed;
            touchpadCurrentlyPressed = TouchpadActivated();
        }

        public bool TouchpadActivated()
        {
            // The Touch thumbstick is considered 'pressed' is it is in one of the far quadrants, or if it is in the center
            // and has actually been depressed. This allows users to simply flick the thumbstick to choose an option, rather
            // than having to move and press-in the thumbstick, which is tiresome.
            var location = GetTouchpadLocation();
            if (location != TouchpadLocation.CENTER && location != TouchpadLocation.NONE) return true;
            bool clickedIn = FindAction("ThumbButton").IsPressed();
            if (location == TouchpadLocation.CENTER && clickedIn) return true;
            return false;
        }

        public bool IsTrackedObjectValid
        {
            // TODO
            get => true;
            set
            {
                // TODO
            }
        }

        public Vector3 GetVelocity()
        {
            if (device.TryGetFeatureValue(CommonUsages.deviceVelocity, out Vector3 velocity))
            {
                return velocity;
            }
            return Vector3.zero;
        }


        private InputAction FindAction(string actionName)
        {
            return actionSet.asset.FindActionMap($"{actionMap}").FindAction($"{actionName}");
        }

        public bool IsPressed(ButtonId buttonId)
        {
            switch (buttonId)
            {
                case ButtonId.Trigger:
                    return FindAction("TriggerButton").IsPressed();
                case ButtonId.Grip:
                    return FindAction("GripButton").IsPressed();
                case ButtonId.Touchpad:
                    return touchpadCurrentlyPressed;
                case ButtonId.SecondaryButton:
                    return FindAction("SecondaryButton").IsPressed();
                case ButtonId.ApplicationMenu:
                    return FindAction("PrimaryButton").IsPressed();
                default:
                    return false;
            }
        }

        public bool WasJustPressed(ButtonId buttonId)
        {
            switch (buttonId)
            {
                case ButtonId.Trigger:
                    return FindAction("TriggerButton").WasPressedThisFrame();
                case ButtonId.Grip:
                    return FindAction("GripButton").WasPressedThisFrame();
                case ButtonId.Touchpad:
                    return !touchpadWasPressedLastFrame && touchpadCurrentlyPressed;
                case ButtonId.SecondaryButton:
                    return FindAction("SecondaryButton").WasPressedThisFrame();
                case ButtonId.ApplicationMenu:
                    return FindAction("PrimaryButton").WasPressedThisFrame();
                default:
                    return false;
            }
        }

        public bool WasJustReleased(ButtonId buttonId)
        {
            switch (buttonId)
            {
                case ButtonId.Trigger:
                    return FindAction("TriggerButton").WasReleasedThisFrame();
                case ButtonId.Grip:
                    return FindAction("GripButton").WasReleasedThisFrame();
                case ButtonId.Touchpad:
                    return touchpadWasPressedLastFrame && !touchpadCurrentlyPressed;
                case ButtonId.SecondaryButton:
                    return FindAction("SecondaryButton").WasReleasedThisFrame();
                case ButtonId.ApplicationMenu:
                    return FindAction("PrimaryButton").WasReleasedThisFrame();
                default:
                    return false;
            }
        }

        public bool IsTriggerHalfPressed()
        {
            wasTriggerHalfPressed = true;
            return FindAction("TriggerAxis").ReadValue<float>() > 0.5f;
        }

        public bool WasTriggerJustReleasedFromHalfPress()
        {
            if (wasTriggerHalfPressed && FindAction("TriggerAxis").ReadValue<float>() < 0.5f)
            {
                wasTriggerHalfPressed = false;
                return true;
            }
            return false;
        }

        public bool IsTouched(ButtonId buttonId)
        {
            switch (buttonId)
            {
                case ButtonId.Trigger:
                    return FindAction("TriggerTouch").WasPressedThisFrame();
                case ButtonId.Grip:
                    return FindAction("GripTouch").WasPressedThisFrame();
                case ButtonId.Touchpad:
                    return touchpadCurrentlyPressed;
                case ButtonId.SecondaryButton:
                    return FindAction("SecondaryButton").WasPressedThisFrame();
                case ButtonId.ApplicationMenu:
                    return FindAction("PrimaryButton").WasPressedThisFrame();
                default:
                    return false;
            }
        }

        public Vector2 GetDirectionalAxis()
        {
            return FindAction("ThumbAxis").ReadValue<Vector2>();
        }

        public TouchpadLocation GetTouchpadLocation()
        {
            Vector2 position = GetDirectionalAxis();
            return TouchpadLocationHelper.GetTouchpadLocation(position);
        }

        public Vector2 GetTriggerScale()
        {
            return new Vector2(FindAction("TriggerAxis").ReadValue<float>(), 0);
        }

        public void TriggerHapticPulse(ushort durationMicroSec = 500)
        {
            float durationSec = durationMicroSec / 1000000f;
            device.SendHapticImpulse(0, 0.75f, durationSec);
        }

        public void InitAsBrush()
        {
            isBrush = true;
            device = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            var bindingGroup = actionSet.OculusTouchControllerScheme.bindingGroup;
            actionSet.bindingMask = InputBinding.MaskByGroup(bindingGroup);
            actionSet.Brush.Enable();
            actionSet.Wand.Disable();
        }

        public void InitAsWand()
        {
            isBrush = false;
            device = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            var bindingGroup = actionSet.OculusTouchControllerScheme.bindingGroup;
            actionSet.bindingMask = InputBinding.MaskByGroup(bindingGroup);
            actionSet.Brush.Disable();
            actionSet.Wand.Enable();
        }
    }
}
#endif
