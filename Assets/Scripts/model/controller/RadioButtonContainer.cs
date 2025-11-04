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

        void Awake()
        {
            m_Options = gameObject.GetComponentsInChildren<RadioButtonOption>();
        }

        public void ShowOptionByIndex(int index, bool show)
        {
            m_Options[index].gameObject.SetActive(show);
        }

        /// <summary>
        /// Returns whether or not action is currently allowed.
        /// </summary>
        /// <returns>Whether or not action is allowed.</returns>
        internal bool ActionIsAllowed()
        {
            return PeltzerMain.Instance.restrictionManager.menuActionsAllowed;
        }

        public void SetInitialOption(string[] options)
        {
            Debug.Log($"[RadioButtonContainer] Setting initial option to: {string.Join(",", options)}");
            SetInitialOption(string.Join(",", options));
        }

        public void SetInitialOption(string option)
        {
            foreach (var optionBtn in m_Options)
            {
                if (optionBtn.m_Value == option)
                {
                    optionBtn.isCurrentOption = true;
                    optionBtn.sprite.color = PolyMenuMain.SELECTED_ICON_COLOR;
                }
                else
                {
                    optionBtn.isCurrentOption = false;
                    optionBtn.sprite.color = PolyMenuMain.UNSELECTED_ICON_COLOR;
                }
                m_Value = option;
            }
        }

        public void ActivateOption(PeltzerMain main, RadioButtonOption activatedOptionBtn)
        {
            if (!ActionIsAllowed()) return;
            if (!activatedOptionBtn.isActive) return;
            foreach (var optionBtn in m_Options)
            {
                optionBtn.isCurrentOption = false;
                optionBtn.sprite.color = PolyMenuMain.UNSELECTED_ICON_COLOR;
            }
            activatedOptionBtn.isCurrentOption = true;
            activatedOptionBtn.sprite.color = PolyMenuMain.SELECTED_ICON_COLOR;
            main.audioLibrary.PlayClip(main.audioLibrary.menuSelectSound);
            m_Value = activatedOptionBtn.m_Value;
            m_Action.Invoke(activatedOptionBtn);
        }
    }
}