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

using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.tools;
using UnityEngine;
using UnityEngine.Events;

namespace com.google.apps.peltzer.client.model.controller
{
    public class ToolOptionsPanel : MonoBehaviour
    {
        public ControllerMode m_Mode;
        public Transform m_PopupAnchor;

        public void Enable(bool enable, ControllerMode mode)
        {
            if (gameObject.activeSelf == enable) return; // No change

            gameObject.SetActive(enable);

            // Move the anchor for the popup panels so that they don't overlap an open option panel

            var palette = PeltzerMain.Instance.paletteController;
            var initialAnchor = palette.m_InitialPopupAnchor.transform;
            var popups = palette.m_Popups.transform;

            // This assumes the popup panels transform has been re-parented to the original parent
            // As long as we always disable previous panels first then this is a safe assumption
            popups.SetParent(enable ? m_PopupAnchor : initialAnchor, worldPositionStays: false);
        }
    }
}