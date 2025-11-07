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
using CommonUsages = UnityEngine.XR.CommonUsages;

namespace com.google.apps.peltzer.client.model.controller
{
    /// <summary>
    ///   Controller SDK logic for OpenXR
    /// </summary>
    public class ControllerDeviceDesktop : ControllerDevice
    {
        public void Update()
        {
            // No-op
        }
        public bool IsTrackedObjectValid
        {
            get
            {
                return true;
            }
            set
            {
                // No-op
            }
        }
        public Vector3 GetVelocity()
        {
            return Vector3.zero;
        }
        public bool IsPressed(ButtonId buttonId)
        {
            return false;
        }
        public bool WasJustPressed(ButtonId buttonId)
        {
            return false;
        }
        public bool WasJustReleased(ButtonId buttonId)
        {
            return false;
        }
        public bool IsTriggerHalfPressed()
        {
            return false;
        }
        public bool WasTriggerJustReleasedFromHalfPress()
        {
            return false;
        }
        public bool IsTouched(ButtonId buttonId)
        {
            return false;
        }
        public Vector2 GetDirectionalAxis()
        {
            return Vector2.zero;
        }
        public TouchpadLocation GetTouchpadLocation()
        {
            return new TouchpadLocation();
        }
        public Vector2 GetTriggerScale()
        {
            return Vector2.zero;
        }
        public void TriggerHapticPulse(ushort durationMicroSec = 500)
        {
            // No-op
        }
    }
}
