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

using com.google.apps.peltzer.client.menu;
using com.google.apps.peltzer.client.model.main;
using UnityEngine;
using UnityEngine.Events;

namespace com.google.apps.peltzer.client.model.controller
{
    public class RadioButtonContainer : MonoBehaviour
    {
        public UnityEvent<RadioButtonOption> m_Action;
        private string m_Value;
        public string Value => m_Value;

        private RadioButtonOption[] m_Options;

        void Start()
        {
            m_Options = gameObject.GetComponentsInChildren<RadioButtonOption>();
        }

        /// <summary>
        /// Returns whether or not action is currently allowed.
        /// </summary>
        /// <returns>Whether or not action is allowed.</returns>
        internal bool ActionIsAllowed()
        {
            return PeltzerMain.Instance.restrictionManager.menuActionsAllowed;
        }

        public void ActivateOption(PeltzerMain main, RadioButtonOption activatedOption)
        {
            if (!ActionIsAllowed()) return;
            if (!activatedOption.isActive) return;
            foreach (var option in m_Options)
            {
                option.isCurrentOption = false;
                option.sprite.color = PolyMenuMain.UNSELECTED_ICON_COLOR;
            }
            activatedOption.isCurrentOption = true;
            activatedOption.sprite.color = PolyMenuMain.SELECTED_ICON_COLOR;
            main.audioLibrary.PlayClip(main.audioLibrary.menuSelectSound);
            m_Value = activatedOption.m_Value;
            m_Action.Invoke(activatedOption);
        }
    }
}