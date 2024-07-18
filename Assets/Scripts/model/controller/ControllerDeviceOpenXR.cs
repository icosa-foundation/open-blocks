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

using TiltBrush;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace com.google.apps.peltzer.client.model.controller
{
    /// <summary>
    ///   Controller SDK logic for OpenXR
    /// </summary>
    public class ControllerDeviceOpenXR : ControllerDevice
    {
        private UnityEngine.XR.InputDevice device;
        private readonly UnityXRInputAction actionSet = new ();

        // Haptics.
        // private OVRHapticsClip rumbleHapticsClip;
        private AudioClip rumbleClip;

        private bool isBrush;

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
            // What do we need to do here?
        }

        public bool IsTrackedObjectValid { get; set; }

        public Vector3 GetVelocity()
        {
            // TODO
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
                    return FindAction("PrimaryButton").IsPressed();
                case ButtonId.SecondaryButton:
                    return FindAction("SecondaryButton").IsPressed();
                case ButtonId.ApplicationMenu:
                    return FindAction("ThumbButton").IsPressed();
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
                    return FindAction("PrimaryButton").WasPressedThisFrame();
                case ButtonId.SecondaryButton:
                    return FindAction("SecondaryButton").WasPressedThisFrame();
                case ButtonId.ApplicationMenu:
                    return FindAction("ThumbButton").WasPressedThisFrame();
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
                    return FindAction("PrimaryButton").WasReleasedThisFrame();
                case ButtonId.SecondaryButton:
                    return FindAction("SecondaryButton").WasReleasedThisFrame();
                case ButtonId.ApplicationMenu:
                    return FindAction("ThumbButton").WasReleasedThisFrame();
                default:
                    return false;
            }
        }

        public bool IsTriggerHalfPressed()
        {
            return FindAction("TriggerAxis").ReadValue<float>() > 0.5f;
        }

        public bool WasTriggerJustReleasedFromHalfPress()
        {
            throw new System.NotImplementedException();
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
                    return FindAction("PrimaryTouch").WasPressedThisFrame();
                case ButtonId.SecondaryButton:
                    return FindAction("SecondaryTouch").WasPressedThisFrame();
                case ButtonId.ApplicationMenu:
                    return FindAction("ThumbTouch").WasPressedThisFrame();
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
            return TouchpadLocationHelper.GetTouchpadLocation(GetDirectionalAxis());
        }

        public Vector2 GetTriggerScale()
        {
            // TODO
            // throw new System.NotImplementedException();
            return Vector2.one;
        }

        public void TriggerHapticPulse(ushort durationMicroSec = 500)
        {
            // TODO
            // throw new System.NotImplementedException();
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
