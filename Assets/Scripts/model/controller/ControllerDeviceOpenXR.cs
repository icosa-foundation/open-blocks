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

namespace com.google.apps.peltzer.client.model.controller
{
    /// <summary>
    ///   Controller SDK logic for OpenXR
    /// </summary>



    public class ControllerDeviceOpenXR : ControllerDevice
    {

        private Transform transform;

        // Haptics.
        private OVRHapticsClip rumbleHapticsClip;
        private AudioClip rumbleClip;

        private bool isBrush = false;

        private string actionMap
        {
            get => isBrush ? "Brush" : "Wand";
        }

        // Constructor, taking in a transform such that it can be regularly updated.
        public ControllerDeviceOpenXR(Transform transform)
        {
            this.transform = transform;
            if (rumbleClip != null)
            {
                rumbleHapticsClip = new OVRHapticsClip(rumbleClip);
            }
        }

        public void Update()
        {
            throw new System.NotImplementedException();
        }

        public bool IsTrackedObjectValid { get; set; }

        public Vector3 GetVelocity()
        {
            throw new System.NotImplementedException();
        }

        private readonly UnityXRInputAction actionSet = new UnityXRInputAction();
        private InputAction FindAction(string actionName)
        {
            return actionSet.asset.FindActionMap($"{actionMap}").FindAction($"{actionName}");
        }

        public bool IsPressed(ButtonId buttonId)
        {
            switch (buttonId)
            {
                case ButtonId.Trigger:
                    return FindAction("TriggerAxis").IsPressed();
                case ButtonId.Grip:
                    return false;
                case ButtonId.Touchpad:
                    return false;
                case ButtonId.SecondaryButton:
                    return false;
                case ButtonId.ApplicationMenu:
                    return false;
                default:
                    return false;
            }
            throw new System.NotImplementedException();
        }

        public bool WasJustPressed(ButtonId buttonId)
        {
            throw new System.NotImplementedException();
        }

        public bool WasJustReleased(ButtonId buttonId)
        {
            throw new System.NotImplementedException();
        }

        public bool IsTriggerHalfPressed()
        {
            throw new System.NotImplementedException();
        }

        public bool WasTriggerJustReleasedFromHalfPress()
        {
            throw new System.NotImplementedException();
        }

        public bool IsTouched(ButtonId buttonId)
        {
            throw new System.NotImplementedException();
        }

        public Vector2 GetDirectionalAxis()
        {
            throw new System.NotImplementedException();
        }

        public TouchpadLocation GetTouchpadLocation()
        {
            throw new System.NotImplementedException();
        }

        public Vector2 GetTriggerScale()
        {
            throw new System.NotImplementedException();
        }

        public void TriggerHapticPulse(ushort durationMicroSec = 500)
        {
            throw new System.NotImplementedException();
        }
    }
}
