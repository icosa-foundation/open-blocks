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

using System;
using System.Collections.Generic;
using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.main;
using UnityEngine;

namespace com.google.apps.peltzer.client.model.controller
{
    public class InsertVolumeOptionsPanel : ToolOptionsPanel
    {
        public List<GameObject> m_OptionPanels;

        void Awake()
        {
            if (PeltzerMain.Instance.peltzerController.shapesMenu == null)
            {
                Debug.LogError("ShapesMenu is not initialized in InsertVolumeOptionsPanel.");
                return;
            }
            PeltzerMain.Instance.peltzerController.shapesMenu.ShapeMenuItemChangedHandler += ShapeChangedEventHandler;
        }

        void OnDestroy()
        {
            PeltzerMain.Instance.peltzerController.shapesMenu.ShapeMenuItemChangedHandler -= ShapeChangedEventHandler;
        }

        private void ShapeChangedEventHandler(int menuItemId)
        {
            foreach (var p in m_OptionPanels)
            {
                p.SetActive(false);
            }
            var optionPanel = m_OptionPanels[menuItemId + 2];
            optionPanel.SetActive(true);
            var sliders = optionPanel.GetComponentsInChildren<Slider>();
            // Set the labels
            foreach (var slider in sliders)
            {
                slider.m_ActionEveryUpdate.Invoke(slider.Value);
            }
        }

        public override void Enable(ControllerMode mode)
        {
            base.Enable(mode);
            var peltzerController = PeltzerMain.Instance.peltzerController;
            ShapeChangedEventHandler(peltzerController.shapesMenu.CurrentItemId);
        }
    }
}
