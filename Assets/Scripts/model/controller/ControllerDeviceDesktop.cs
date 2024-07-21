// Copyright 2020 The Blocks Authors
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

namespace com.google.apps.peltzer.client.model.controller
{
    class ControllerDeviceDesktop : ControllerDevice
    {
        private const float LIGHT_TRIGGER_PULL_THRESHOLD = 0.01f;
        // The most-recent thumbstick location.
        private Vector2 currentPad = Vector2.zero;

        // An Oculus controller's validity is determined in OculusHandTrackingManager.
        private bool isValid;
        private bool wasValidOnLastUpdate;
        public bool IsTrackedObjectValid
        {
            get { return isValid; }
            set { isValid = value; }
        }

        // We must manually track velocity in the Oculus SDK.
        private Transform transform;
        private Vector3 worldPositionOnLastUpdate;
        private Vector3 velocity;

        // We must manually track button releases in the Oculus SDK.
        private bool triggerPressed;
        private bool gripPressed;
        private bool secondaryButtonPressed;
        private bool applicationButtonPressed;
        private bool touchpadPressed;
        private bool triggerHalfPressed;
        private bool triggerWasPressedOnLastUpdate;
        private bool gripWasPressedOnLastUpdate;
        private bool secondaryButtonWasPressedOnLastUpdate;
        private bool applicationButtonWasPressedOnLastUpdate;
        private bool touchpadWasPressedOnLastUpdate;
        private bool triggerWasHalfPressedOnLastUpdate;

        // Haptics.
        private AudioClip rumbleClip;

        public enum MouseButton
        {
            Left,
            Right,
            Middle,
            CtrlRight,
            CtrlLeft
        }

        // Constructor, taking in a transform such that it can be regularly updated.
        public ControllerDeviceDesktop(Transform transform)
        {
            this.transform = transform;
            if (rumbleClip != null)
            {
                // rumbleHapticsClip = new OVRHapticsClip(rumbleClip);
            }
        }

        // Update loop (to be called manually, this is not a MonoBehavior).
        public void Update()
        {
            if (!isValid)
            {
                // In an invalid state, nothing is pressed.
                triggerPressed = false;
                gripPressed = false;
                secondaryButtonPressed = false;
                applicationButtonPressed = false;
                touchpadPressed = false;
                velocity = Vector3.zero;
                currentPad = Vector2.zero;

                // Return before calculating releases, and without updating any 'previous state' variables.
                return;
            }

            // Update the latest thumbstick location, if possible.
            currentPad = Input.mousePosition;

            // Update velocity only when we have two subsequent valid updates.
            if (wasValidOnLastUpdate)
            {
                velocity = (transform.position - worldPositionOnLastUpdate) / Time.deltaTime;
            }
            else
            {
                velocity = Vector3.zero;
            }

            // Update 'previous state' variables.
            triggerWasPressedOnLastUpdate = triggerPressed;
            gripWasPressedOnLastUpdate = gripPressed;
            secondaryButtonWasPressedOnLastUpdate = secondaryButtonPressed;
            applicationButtonWasPressedOnLastUpdate = applicationButtonPressed;
            touchpadWasPressedOnLastUpdate = touchpadPressed;
            worldPositionOnLastUpdate = transform.position;
            wasValidOnLastUpdate = isValid;
            triggerWasHalfPressedOnLastUpdate = triggerHalfPressed;


            // Find which buttons are currently pressed.
            triggerPressed = IsPressedInternal(ButtonId.Trigger);
            gripPressed = IsPressedInternal(ButtonId.Grip);
            secondaryButtonPressed = IsPressedInternal(ButtonId.SecondaryButton);
            applicationButtonPressed = IsPressedInternal(ButtonId.ApplicationMenu);
            touchpadPressed = IsPressedInternal(ButtonId.Touchpad);
            triggerHalfPressed = IsTriggerHalfPressedInternal();
        }

        // A mapping from ButtonId to Mouse Button.
        private static bool MouseButtonFromButtonId(ButtonId buttonId, out MouseButton mouseButton)
        {
            switch (buttonId)
            {
                case ButtonId.ApplicationMenu:
                    mouseButton = MouseButton.CtrlRight;
                    return true;
                case ButtonId.Touchpad:
                    mouseButton = MouseButton.Middle;
                    return true;
                case ButtonId.Trigger:
                    mouseButton = MouseButton.Left;
                    return true;
                case ButtonId.Grip:
                    mouseButton = MouseButton.Right;
                    return true;
                case ButtonId.SecondaryButton:
                    mouseButton = MouseButton.CtrlLeft;
                    return true;
            }

            mouseButton = MouseButton.Left;
            return false;
        }

        private bool IsPressedInternal(ButtonId buttonId)
        {
            if (!isValid) return false;

            MouseButton mouseButton;
            if (!MouseButtonFromButtonId(buttonId, out mouseButton)) return false;

            if (buttonId == ButtonId.Touchpad)
            {
                return
                    Input.GetKey(KeyCode.LeftArrow) ||
                    Input.GetKey(KeyCode.RightArrow) ||
                    Input.GetKey(KeyCode.UpArrow) ||
                    Input.GetKey(KeyCode.DownArrow);
            }
            else
            {
                switch (mouseButton)
                {
                    case MouseButton.Left:
                        return Input.GetMouseButton(0);
                    case MouseButton.Right:
                        return Input.GetMouseButton(1);
                    case MouseButton.Middle:
                        return Input.GetMouseButton(2);
                    case MouseButton.CtrlRight:
                        return Input.GetMouseButton(1) && Input.GetKey(KeyCode.LeftControl);
                    case MouseButton.CtrlLeft:
                        return Input.GetMouseButton(0) && Input.GetKey(KeyCode.LeftControl);
                }
                return false;
            }
        }

        private bool IsTriggerHalfPressedInternal()
        {
            if (!isValid) return false;

            // Only record as half pressed if the trigger is not pressed.
            return OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger) >= LIGHT_TRIGGER_PULL_THRESHOLD
              && !triggerPressed;
        }

        // Interface method implementations begin.

        public Vector3 GetVelocity()
        {
            return velocity;
        }

        public bool IsPressed(ButtonId buttonId)
        {
            if (!isValid) return false;

            switch (buttonId)
            {
                case ButtonId.ApplicationMenu:
                    return applicationButtonPressed;
                case ButtonId.Touchpad:
                    return touchpadPressed;
                case ButtonId.Trigger:
                    return triggerPressed;
                case ButtonId.Grip:
                    return gripPressed;
                case ButtonId.SecondaryButton:
                    return secondaryButtonPressed;
            }

            return false;
        }

        public bool IsTriggerHalfPressed()
        {
            if (!isValid) return false;
            return triggerHalfPressed;
        }

        public bool WasTriggerJustReleasedFromHalfPress()
        {
            if (!isValid) return false;
            return !triggerHalfPressed && !triggerPressed && triggerWasHalfPressedOnLastUpdate;
        }

        public bool WasJustPressed(ButtonId buttonId)
        {
            if (!isValid) return false;

            switch (buttonId)
            {
                case ButtonId.ApplicationMenu:
                    return applicationButtonPressed && !applicationButtonWasPressedOnLastUpdate;
                case ButtonId.Touchpad:
                    return touchpadPressed && !touchpadWasPressedOnLastUpdate;
                case ButtonId.Trigger:
                    return triggerPressed && !triggerWasPressedOnLastUpdate;
                case ButtonId.Grip:
                    return gripPressed && !gripWasPressedOnLastUpdate;
                case ButtonId.SecondaryButton:
                    return secondaryButtonPressed && !secondaryButtonWasPressedOnLastUpdate;
            }

            return false;
        }

        public bool WasJustReleased(ButtonId buttonId)
        {
            if (!isValid) return false;

            switch (buttonId)
            {
                case ButtonId.ApplicationMenu:
                    return !applicationButtonPressed && applicationButtonWasPressedOnLastUpdate;
                case ButtonId.Touchpad:
                    return !touchpadPressed && touchpadWasPressedOnLastUpdate;
                case ButtonId.Trigger:
                    return !triggerPressed && triggerWasPressedOnLastUpdate;
                case ButtonId.Grip:
                    return !gripPressed && gripWasPressedOnLastUpdate;
                case ButtonId.SecondaryButton:
                    return !secondaryButtonPressed && secondaryButtonWasPressedOnLastUpdate;
            }

            return false;
        }

        public bool IsTouched(ButtonId buttonId)
        {
            return false;
        }

        public Vector2 GetDirectionalAxis()
        {
            return currentPad;
        }

        public TouchpadLocation GetTouchpadLocation()
        {
            return TouchpadLocationHelper.GetTouchpadLocation(currentPad);
        }

        public Vector2 GetTriggerScale()
        {
            return new Vector2(0, 0);
        }

        public void TriggerHapticPulse(ushort durationMicroSec = 500)
        {

        }
    }
}
