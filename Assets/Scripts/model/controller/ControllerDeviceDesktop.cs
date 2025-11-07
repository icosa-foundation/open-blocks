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
            throw new System.NotImplementedException();
        }
        public bool IsTrackedObjectValid { get; set; }
        public Vector3 GetVelocity()
        {
            throw new System.NotImplementedException();
        }
        public bool IsPressed(ButtonId buttonId)
        {
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
