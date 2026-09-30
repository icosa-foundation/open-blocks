// Copyright 2022 The Open Brush Authors
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
using System.Linq;
using Polyhydra.Core;

using UnityEngine;
using Object = System.Object;

namespace TiltBrush
{
    public class PolyhydraTool : MonoBehaviour
    {

        //the parent of all of our tool's visual indicator objects
        private GameObject m_toolDirectionIndicator;

        //the controller that this tool is attached to
        private Transform m_BrushController;

        // Set true when the tool is activated so we can detect when it's released
        private bool m_WasClicked = false;

        // The position of the pointed when m_ClickedLastUpdate was set to true;
        private TrTransform m_FirstPositionClicked_CS;

        private Mesh previewMesh;
        private Material previewMaterial;
        [SerializeField] private Material snapGhostMaterial;

        //whether this tool should follow the controller or not
        private bool m_LockToController;

        private bool m_ValidWidgetFoundThisFrame;
        private EditableModelWidget LastIntersectedEditableModelWidget;

        private HashSet<EditableModelWidget> m_WidgetsModifiedThisClick;
        private Quaternion m_StencilSnappedRot;
        private bool m_StencilSnapped;

        //Init is similar to Awake(), and should be used for initializing references and other setup code
        public void Init()
        {
            m_toolDirectionIndicator = transform.GetChild(0).gameObject;
            m_WidgetsModifiedThisClick = new HashSet<EditableModelWidget>();
        }

        // What to do when all the tools run their update functions. Note that this is separate from Unity's Update script
        // All input handling should be done here
        public void UpdateTool()
        {

            // if (m_ValidWidgetFoundThisFrame &&
            //     !m_WidgetsModifiedThisClick.Contains(LastIntersectedEditableModelWidget) && // Don't modify widgets more than once per interaction
            //     InputManager.m_Instance.GetCommand(InputManager.SketchCommands.DuplicateSelection))
            // {
            //     EditableModelWidget ewidget = LastIntersectedEditableModelWidget;
            //     m_WidgetsModifiedThisClick.Add(ewidget);
            //     if (ewidget != null)
            //     {
            //         PolyhydraPanel polyhydraPanel = PanelManager.m_Instance.GetActivePanelByType(BasePanel.PanelType.Polyhydra) as PolyhydraPanel;
            //         if (polyhydraPanel != null)
            //         {
            //             switch (m_CurrentModifyMode)
            //             {
            //                 case ModifyModes.ApplySettings:
            //                     var newPoly = PreviewPolyhedron.m_Instance.m_PolyMesh;
            //                     EditableModelManager.UpdateWidgetFromPolyMesh(ewidget, newPoly, PreviewPolyhedron.m_Instance.m_PolyRecipe.Clone());
            //                     break;
            //
            //                 case ModifyModes.GrabSettings:
            //                     polyhydraPanel.LoadFromWidget(ewidget);
            //                     break;
            //
            //                 case ModifyModes.ApplyColor:
            //
            //                     Color color = PointerManager.m_Instance.CalculateJitteredColor(
            //                         PointerManager.m_Instance.PointerColor
            //                     );
            //                     Color[] colors = Enumerable.Repeat(color, PreviewPolyhedron.m_Instance.m_PolyRecipe.Colors.Length).ToArray();
            //
            //                     SketchMemoryScript.m_Instance.PerformAndRecordCommand(
            //                         new RecolorPolyCommand(ewidget, colors)
            //                     );
            //                     break;
            //
            //                 case ModifyModes.ApplyBrushStrokesToFaces:
            //                     CreateBrushStrokesForPoly(
            //                         ewidget.m_PolyMesh,
            //                         Coords.AsCanvas[ewidget.transform]
            //                     );
            //                     break;
            //
            //                 case ModifyModes.ApplyBrushStrokesToEdges:
            //                     CreateBrushStrokesForPolyEdges(
            //                         ewidget.m_PolyMesh,
            //                         Coords.AsCanvas[ewidget.transform]
            //                     );
            //                     break;
            //             }
            //             AudioManager.m_Instance.PlayDuplicateSound(
            //                 InputManager.m_Instance.GetControllerPosition(InputManager.ControllerName.Brush)
            //             );
            //         }
            //     }
            // }

            // Clear the list of widgets modified this time
            // if (!InputManager.m_Instance.GetCommand(InputManager.SketchCommands.DuplicateSelection))
            // {
            //     m_WidgetsModifiedThisClick.Clear();
            // }
            //
            // if (InputManager.m_Instance.GetCommandDown(InputManager.SketchCommands.Activate))
            // {
            //     m_WasClicked = true;
            //     // Initially click. Store the transform and grab the poly mesh and material.
            //     var rAttachPoint_GS = App.Scene.Pose * rAttachPoint_CS;
            //     Quaternion rot_GS = Quaternion.identity;
            //     var pos_GS = rAttachPoint_GS.translation;
            //     var prevPos_GS = pos_GS;
            //     WidgetManager.m_Instance.MagnetizeToStencils(ref pos_GS, ref rot_GS);
            //     if (prevPos_GS != pos_GS)
            //     {
            //         var pos_CS = App.Scene.Pose.inverse * pos_GS;
            //         var rot_CS = App.Scene.Pose.inverse.rotation * rot_GS;
            //         rAttachPoint_CS.translation = pos_CS;
            //         m_StencilSnappedRot = rot_CS * Quaternion.Euler(90, 0, 0);
            //         m_StencilSnapped = true;
            //     }
            //     m_FirstPositionClicked_CS = rAttachPoint_CS;
            //     previewMesh = PreviewPolyhedron.m_Instance.GetComponent<MeshFilter>().mesh;
            //     previewMaterial = PreviewPolyhedron.m_Instance.GetComponent<MeshRenderer>().material;
            // }


            // var position_CS = SnapToGrid(m_FirstPositionClicked_CS.translation);
            // var drawnVector_CS = SnapToGrid(rAttachPoint_CS.translation) - position_CS;
            // var rotation_CS = SelectionManager.m_Instance.QuantizeAngle(
            //     Quaternion.LookRotation(drawnVector_CS, Vector3.up)
            // );
            // var scale_CS = drawnVector_CS.magnitude;
            //
            // if (InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate))
            // {
            //     Matrix4x4 mat_CS = Matrix4x4.TRS(
            //         position_CS,
            //         m_StencilSnapped ? m_StencilSnappedRot : rotation_CS,
            //         Vector3.one * scale_CS
            //     );
            //     Matrix4x4 mat_GS = App.ActiveCanvas.Pose.ToMatrix4x4() * mat_CS;
            //
            //     Graphics.DrawMesh(previewMesh, mat_GS, previewMaterial, 0);
            //     if (SelectionManager.m_Instance.SnappingAngle != 0 || SelectionManager.m_Instance.SnappingGridSize != 0)
            //     {
            //         var vec = rAttachPoint_CS.translation - m_FirstPositionClicked_CS.translation;
            //         Matrix4x4 ghostMat_CS = Matrix4x4.TRS(
            //             m_FirstPositionClicked_CS.translation,
            //             Quaternion.LookRotation(vec, Vector3.up),
            //             Vector3.one * vec.magnitude
            //         );
            //         Matrix4x4 ghostMat_GS = App.ActiveCanvas.Pose.ToMatrix4x4() * ghostMat_CS;
            //
            //         Graphics.DrawMesh(previewMesh, ghostMat_GS, snapGhostMaterial, 0);
            //     }
            //
            // }
            // else if (!InputManager.m_Instance.GetCommand(InputManager.SketchCommands.Activate))
            // {
            //     if (m_WasClicked)
            //     {
            //         m_WasClicked = false;
            //         var poly = PreviewPolyhedron.m_Instance.m_PolyMesh;
            //         TrTransform tr = TrTransform.TRS(
            //             position_CS,
            //             m_StencilSnapped ? m_StencilSnappedRot : rotation_CS,
            //             scale_CS
            //         );
            //         CreatePolyForCurrentMode(poly, tr);
            //         m_StencilSnapped = false;
            //     }
            // }
        }

        public void CreatePolyForCurrentMode(PolyMesh poly, TrTransform tr)
        {
            EditableModelManager.m_Instance.GeneratePolyMesh(poly, PreviewPolyhedron.m_Instance.m_PolyRecipe, tr);
        }

        protected bool HandleIntersectionWithWidget(Object widget)
        {
            // Only intersect with EditableModelWidget instances
            var editableModelWidget = widget as EditableModelWidget;
            LastIntersectedEditableModelWidget = editableModelWidget;
            m_ValidWidgetFoundThisFrame = widget != null;
            return m_ValidWidgetFoundThisFrame;
        }
    }
}
