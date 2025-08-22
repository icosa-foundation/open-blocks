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
using com.google.apps.peltzer.client.model.controller;
using com.google.apps.peltzer.client.model.main;

namespace com.google.apps.peltzer.client.desktop_app
{
    /// <summary>
    ///   Handles desktop interactions with SelectableMenuItems.
    /// </summary>
    public class DesktopController : MonoBehaviour
    {
        private SelectableMenuItem currentSelectableMenuItem;
        private GameObject currentHoveredObject;

        private void Update()
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                SelectableMenuItem menuItem = hit.transform.GetComponent<SelectableMenuItem>();
                if (menuItem != null && menuItem.isActive)
                {
                    if (currentHoveredObject != hit.transform.gameObject)
                    {
                        ResetHoveredItem();
                    }
                    Highlight(menuItem, hit.transform.gameObject);
                    currentSelectableMenuItem = menuItem;
                    currentHoveredObject = hit.transform.gameObject;
                }
                else
                {
                    ResetHoveredItem();
                }
            }
            else
            {
                ResetHoveredItem();
            }

            if (Input.GetMouseButtonDown(0) && currentSelectableMenuItem != null)
            {
                currentSelectableMenuItem.ApplyMenuOptions(PeltzerMain.Instance);
            }
        }

        private void Highlight(SelectableMenuItem menuItem, GameObject hoveredObject)
        {
            ChangeMaterialMenuItem changeMaterial = hoveredObject.GetComponent<ChangeMaterialMenuItem>();
            if (changeMaterial != null)
            {
                changeMaterial.SetHovered(true);
            }

            PolyMenuButton polyMenuButton = hoveredObject.GetComponent<PolyMenuButton>();
            if (polyMenuButton != null)
            {
                polyMenuButton.SetHovered(true);
            }

            MenuActionItem menuActionItem = hoveredObject.GetComponent<MenuActionItem>();
            if (menuActionItem != null && IsHoveredButtonThatShouldChangeColour(menuActionItem))
            {
                hoveredObject.GetComponent<Renderer>().material.color = PeltzerController.MENU_BUTTON_LIGHT;
            }
        }

        private void ResetHoveredItem()
        {
            if (currentHoveredObject == null)
            {
                return;
            }

            ChangeMaterialMenuItem changeMaterial = currentHoveredObject.GetComponent<ChangeMaterialMenuItem>();
            if (changeMaterial != null)
            {
                changeMaterial.SetHovered(false);
            }

            PolyMenuButton polyMenuButton = currentHoveredObject.GetComponent<PolyMenuButton>();
            if (polyMenuButton != null)
            {
                polyMenuButton.SetHovered(false);
            }

            MenuActionItem menuActionItem = currentHoveredObject.GetComponent<MenuActionItem>();
            if (menuActionItem != null && IsHoveredButtonThatShouldChangeColour(menuActionItem))
            {
                currentHoveredObject.GetComponent<Renderer>().material.color = PeltzerController.MENU_BUTTON_DARK;
            }

            currentHoveredObject = null;
            currentSelectableMenuItem = null;
        }

        private bool IsHoveredButtonThatShouldChangeColour(MenuActionItem menuActionItem)
        {
            return menuActionItem.action == MenuAction.CLEAR
                || menuActionItem.action == MenuAction.SAVE
                || menuActionItem.action == MenuAction.CANCEL_SAVE
                || menuActionItem.action == MenuAction.NOTHING
                || menuActionItem.action == MenuAction.SHOW_SAVE_CONFIRM
                || menuActionItem.action == MenuAction.SAVE_COPY
                || menuActionItem.action == MenuAction.SAVE_SELECTED
                || menuActionItem.action == MenuAction.PUBLISH
                || menuActionItem.action == MenuAction.UPLOAD
                || menuActionItem.action == MenuAction.NEW_WITH_SAVE
                || (menuActionItem.action == MenuAction.TUTORIAL_PROMPT && !(
                    PeltzerMain.Instance.paletteController.tutorialBeginPrompt.activeInHierarchy ||
                    PeltzerMain.Instance.paletteController.tutorialSavePrompt.activeInHierarchy ||
                    PeltzerMain.Instance.paletteController.tutorialExitPrompt.activeInHierarchy))
                || (menuActionItem.action == MenuAction.BLOCKMODE && !PeltzerMain.Instance.peltzerController.isBlockMode);
        }
    }
}

