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
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace com.google.apps.peltzer.client.model.controller
{
    public class ToolOptionsPanel : MonoBehaviour
    {
        public ControllerMode m_Mode;
        public Transform m_PopupAnchor;
        public TextMeshPro m_Title;
        public bool m_Allowed = true;

        private bool m_IsOpen;

        public virtual void Enable(ControllerMode mode)
        {
            m_Title.text = mode.ToString();

            if (m_IsOpen || !m_Allowed) return; // No change or we've disabled this panel
            m_IsOpen = true;
            gameObject.SetActive(true);
            var palette = PeltzerMain.Instance.paletteController;
            var popups = palette.m_Popups.transform;
            // Move the anchor for the popup panels so that they don't overlap an open option panel
            // This assumes the popup panels transform has been re-parented to the original parent
            // As long as we always disable previous panels first then this is a safe assumption
            popups.SetParent(m_PopupAnchor, worldPositionStays: false);
        }

        public virtual void Disable()
        {
            if (!m_IsOpen) return; // No change
            m_IsOpen = false;
            gameObject.SetActive(false);
            var palette = PeltzerMain.Instance.paletteController;
            var popups = palette.m_Popups.transform;
            var initialAnchor = palette.m_InitialPopupAnchor.transform;
            // Move the popups back to the initial "closed" anchor position
            popups.SetParent(initialAnchor, worldPositionStays: false);
        }

        public void ClosePanel()
        {
            PeltzerMain.Instance.paletteController.EnableToolOptionPanels(false);
        }
    }
}