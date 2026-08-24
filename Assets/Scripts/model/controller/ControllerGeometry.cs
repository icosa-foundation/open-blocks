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
using UnityEngine.Serialization;

namespace com.google.apps.peltzer.client.model.controller
{

    /// <summary>
    /// A class to abstract out controller geometry
    /// </summary>
    public class ControllerGeometry : MonoBehaviour
    {
        private void Awake()
        {
            ResetTooltipActivationState();
        }

        public void ResetTooltipActivationState()
        {
            Transform tooltipsRoot = transform.Find("ControllerUI/Tooltips");
            if (tooltipsRoot == null)
            {
                return;
            }

            foreach (Transform tooltip in tooltipsRoot.GetComponentsInChildren<Transform>(includeInactive: true))
            {
                if (IsTooltipCard(tooltip))
                {
                    tooltip.gameObject.SetActive(false);
                }
            }
        }

        private static bool IsTooltipCard(Transform transform)
        {
            bool hasBackground = false;
            bool hasTip = false;
            foreach (Transform child in transform)
            {
                hasBackground |= child.name == "bg";
                hasTip |= child.name.StartsWith("tip", System.StringComparison.OrdinalIgnoreCase);
            }
            return hasBackground && hasTip;
        }

        public static void SetTooltipText(GameObject tooltip, string text)
        {
            TextMesh textMesh = tooltip.GetComponentInChildren<TextMesh>(includeInactive: true);
            if (textMesh != null)
            {
                textMesh.text = text;
            }
        }

        public BaseControllerAnimation baseControllerAnimation;

        [Header("Geometry")]
        public GameObject trigger;
        public GameObject gripLeft;
        public GameObject gripRight;
        // The transforms on appMenuButton are completely wrong because of errors with the prefab. This
        // holder is used so that we can correctly manipulate the transform.
        public GameObject appMenuButtonHolder;
        public GameObject appMenuButton;
        public GameObject secondaryButton;
        public GameObject systemButton;
        public GameObject thumbstick;
        public GameObject touchpad;
        public GameObject segmentedTouchpad;
        public GameObject touchpadLeft;
        public GameObject touchpadRight;
        public GameObject touchpadUp;
        public GameObject touchpadDown;
        public GameObject handleBase;

        [Header("Overlays")]
        public GameObject volumeInserterOverlay;
        public GameObject freeformOverlay;
        public GameObject freeformChangeFaceOverlay;
        public GameObject paintOverlay;
        public GameObject modifyOverlay;
        public GameObject moveOverlay;
        public GameObject deleteOverlay;
        public GameObject menuOverlay;
        public GameObject undoRedoOverlay;
        public GameObject resizeOverlay;
        public GameObject resetZoomOverlay;
        public GameObject OnMoveOverlay;
        public GameObject OnMenuOverlay;
        public GameObject OnUndoRedoOverlay;

        [Header("Tool Tips")]
        public GameObject applicationButtonTooltipRoot;
        public GameObject applicationButtonTooltipLeft;
        public GameObject applicationButtonTooltipRight;

        public GameObject groupTooltipRoot;
        public GameObject groupLeftTooltip;
        public GameObject groupRightTooltip;
        public GameObject ungroupLeftTooltip;
        public GameObject ungroupRightTooltip;

        public GameObject shapeTooltips;
        public GameObject csgTooltips;

        public GameObject menuLeftTooltip;
        public GameObject menuRightTooltip;
        public GameObject menuUpTooltip;
        public GameObject menuDownTooltip;

        public GameObject modifyTooltips;
        public GameObject modifyTooltipLeft;
        public GameObject modifyTooltipRight;
        public GameObject modifyTooltipUp;
        public GameObject modifyTooltipDown;
        [FormerlySerializedAs("modifyTooltipApplicationButton")]
        public GameObject modifyCoplanarTooltip;

        public GameObject moverTooltips;
        public GameObject moverTooltipLeft;
        public GameObject moverTooltipRight;
        public GameObject moverTooltipUp;
        public GameObject moverTooltipDown;

        public GameObject freeformTooltips;
        public GameObject freeformTooltipLeft;
        public GameObject freeformTooltipRight;
        public GameObject freeformTooltipUp;
        public GameObject freeformTooltipDown;
        public GameObject freeformTooltipCenter;

        public GameObject volumeInserterTooltips;
        public GameObject volumeInserterTooltipLeft;
        public GameObject volumeInserterTooltipRight;
        public GameObject volumeInserterTooltipUp;
        public GameObject volumeInserterTooltipDown;

        public GameObject paintTooltips;
        public GameObject paintTooltipLeft;
        public GameObject paintTooltipRight;

        public GameObject deleteTooltips;
        public GameObject deleteTooltipLeft;
        public GameObject deleteTooltipRight;

        public GameObject resizeUpTooltip;
        public GameObject resizeDownTooltip;

        public GameObject undoRedoLeftTooltip;
        public GameObject undoRedoRightTooltip;

        public GameObject grabTooltips;
        public GameObject zoomLeftTooltip;
        public GameObject zoomRightTooltip;
        public GameObject moveLeftTooltip;
        public GameObject moveRightTooltip;
        public GameObject snapLeftTooltip;
        public GameObject snapRightTooltip;
        public GameObject straightenLeftTooltip;
        public GameObject straightenRightTooltip;

        public GameObject snapGrabAssistLeftTooltip;
        public GameObject snapGrabAssistRightTooltip;
        public GameObject snapGrabHoldLeftTooltip;
        public GameObject snapGrabHoldRightTooltip;
        public GameObject snapStrokeLeftTooltip;
        public GameObject snapStrokeRightTooltip;
        public GameObject snapShapeInsertLeftTooltip;
        public GameObject snapShapeInsertRightTooltip;
        public GameObject snapModifyLeftTooltip;
        public GameObject snapModifyRightTooltip;
        public GameObject snapPaintOrEraseLeftTooltip;
        public GameObject snapPaintOrEraseRightTooltip;

        [Header("IconLocations")]
        public GameObject groupButtonIcon;

        public GameObject[] overlays;
    }
}
