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

using com.google.apps.peltzer.client.model.controller;
using com.google.apps.peltzer.client.model.core;
using UnityEngine;
using System.Collections.Generic;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.model.util;

namespace com.google.apps.peltzer.client.tools
{
    /// <summary>
    ///   Tool which handles deletion of meshes.
    /// </summary>
    public class Deleter : MonoBehaviour, IBaseTool
    {
        public ControllerMain controllerMain;
        private PeltzerController peltzerController;
        private Model model;
        private Selector selector;
        private AudioLibrary audioLibrary;

        /// <summary>
        /// Whether we are currently deleting all hovered objects.
        /// </summary>
        public bool isDeleting { get; private set; }
        /// <summary>
        /// The set of meshes to delete when this deletion command finishes.
        /// </summary>
        private HashSet<int> meshIdsToDelete = new HashSet<int>();
        /// <summary>
        /// When we last made a noise and buzzed because of a deletion.
        /// </summary>
        private float timeLastDeletionFeedbackPlayed;
        /// <summary>
        /// Leave some time between playing deletion feedback.
        /// </summary>
        private const float INTERVAL_BETWEEN_DELETION_FEEDBACKS = 0.5f;
        /// <summary>
        /// Whether we have shown the snap tooltip for this tool yet. (Show only once because there are no direct
        /// snapping behaviors for Painter and Deleter).
        /// </summary>
        private bool snapTooltipShown = false;

        /// <summary>
        ///   Every tool is implemented as MonoBehaviour, which means it may do no work in its constructor.
        ///   As such, this setup method must be called before the tool is used for it to have a valid state.
        /// </summary>
        public void Setup(Model model, ControllerMain controllerMain, PeltzerController peltzerController,
          Selector selector, AudioLibrary audioLibrary)
        {
            this.model = model;
            this.controllerMain = controllerMain;
            this.peltzerController = peltzerController;
            this.selector = selector;
            this.audioLibrary = audioLibrary;
            controllerMain.ControllerActionHandler += ControllerEventHandler;
        }

        /// <summary>
        /// If we are in delete mode, try and delete all hovered meshes.
        /// </summary>
        public void Update()
        {
            if (!PeltzerController.AcquireIfNecessary(ref peltzerController) ||
                !(peltzerController.mode == ControllerMode.delete || peltzerController.mode == ControllerMode.deletePart))
            {
                return;
            }

            if (peltzerController.mode == ControllerMode.deletePart)
            {
                selector.UpdateInactive(Selector.FACES_EDGES_AND_VERTICES);
                selector.SelectAtPosition(peltzerController.LastPositionModel, Selector.FACES_EDGES_AND_VERTICES);
            }
            else
            {
                // Update the position of the selector even if we aren't deleting yet so the selector can detect which meshes to
                // delete. If we aren't deleting yet we want to hide meshes and show their highlights.
                selector.SelectMeshAtPosition(peltzerController.LastPositionModel, Selector.MESHES_ONLY);

                foreach (int meshId in selector.hoverMeshes)
                {
                    PeltzerMain.Instance.highlightUtils.SetMeshStyleToDelete(meshId);
                }

                if (!isDeleting || selector.hoverMeshes.Count == 0)
                {
                    return;
                }

                // Stop rendering each hovered mesh, and mark it for deletion.
                int[] hoveredKeys = new int[selector.hoverMeshes.Count];
                selector.hoverMeshes.CopyTo(hoveredKeys, 0);
                foreach (int meshId in hoveredKeys)
                {
                    if (meshIdsToDelete.Add(meshId))
                    {
                        model.MarkMeshForDeletion(meshId);
                        PeltzerMain.Instance.highlightUtils.TurnOffMesh(meshId);
                        if (Time.time - timeLastDeletionFeedbackPlayed > INTERVAL_BETWEEN_DELETION_FEEDBACKS)
                        {
                            timeLastDeletionFeedbackPlayed = Time.time;
                            audioLibrary.PlayClip(audioLibrary.deleteSound);
                            peltzerController.TriggerHapticFeedback();
                        }
                    }
                }
            }
        }

        /// <summary>
        ///   Whether this matches the pattern of a 'start deleting' event.
        /// </summary>
        /// <param name="args">The controller event arguments.</param>
        /// <returns>True if this is a start deleting event, false otherwise.</returns>
        private bool IsStartDeletingEvent(ControllerEventArgs args)
        {
            return args.ControllerType == ControllerType.PELTZER
              && args.ButtonId == ButtonId.Trigger
              && args.Action == ButtonAction.DOWN;
        }

        /// <summary>
        ///   Whether this matches the pattern of a 'stop deleting' event.
        /// </summary>
        /// <param name="args">The controller event arguments.</param>
        /// <returns>True if this is a stop deleting event, false otherwise.</returns>
        private bool IsFinishDeletingEvent(ControllerEventArgs args)
        {
            return args.ControllerType == ControllerType.PELTZER
              && args.ButtonId == ButtonId.Trigger
              && args.Action == ButtonAction.UP;
        }

        /// <summary>
        ///   An event handler that listens for controller input and delegates accordingly.
        /// </summary>
        /// <param name="sender">The sender of the controller event.</param>
        /// <param name="args">The controller event arguments.</param>
        private void ControllerEventHandler(object sender, ControllerEventArgs args)
        {
            if (peltzerController.mode == ControllerMode.delete)
            {
                if (IsStartDeletingEvent(args))
                {
                    StartDeleting();
                }
                else if (IsFinishDeletingEvent(args))
                {
                    FinishDeleting();
                }
                else if (IsSetSnapTriggerTooltipEvent(args) && !snapTooltipShown)
                {
                    // Show tool tip about the snap trigger.
                    PeltzerMain.Instance.paletteController.ShowSnapAssistanceTooltip();
                    snapTooltipShown = true;
                }
            }
            else if (peltzerController.mode == ControllerMode.deletePart)
            {
                if (IsStartDeletingEvent(args))
                {
                    DeleteAPart();
                }
            }
        }

        private void DeleteAPart()
        {
            if (selector.hoverFace != null)
            {
                DeleteFace(selector.hoverFace);
            }
            else if (selector.hoverVertex != null)
            {
                DeleteVertex(selector.hoverVertex);
            }
            else if (selector.hoverEdge != null)
            {
                DeleteEdge(selector.hoverEdge);
            }
        }

        private void DeleteVertex(VertexKey vertexKey)
        {
            MMesh originalMesh = model.GetMesh(vertexKey.meshId);
            MMesh mesh = originalMesh.Clone();
            MeshUtil.DeleteVertexAndMergeAdjacentFaces(mesh, vertexKey.vertexId);
            TryApplyValidatedMesh(mesh, originalMesh,
              $"Deleter: Cannot delete vertex {vertexKey.vertexId} in mesh {mesh.id} - resulting mesh would be invalid.");
        }

        private int FindLastEdgeVertexInFace(EdgeKey edge, Face face)
        {
            int face1EdgeKey1Index = -1;
            for (int i = 0; i < face.vertexIds.Count; i++)
            {
                if (face.vertexIds[i] == edge.vertexId1)
                {
                    if (face.vertexIds[(i + 1) % face.vertexIds.Count] == edge.vertexId2)
                    {
                        return (i + 1) % face.vertexIds.Count;
                    }
                    else
                    {
                        return i;
                    }
                }
            }
            return -1;
        }

        private void DeleteEdge(EdgeKey edgeKey)
        {
            // Edge delete keeps the older dissolve behavior when that can produce a meaningful result:
            // internal edges are dissolved within the face, and crease edges are dissolved into one bent
            // face when noncoplanar faces are allowed. When noncoplanar faces are not allowed, the
            // default behavior is to try to flatten the merged boundary into one planar face while
            // preserving the two selected endpoints. Explicit edge collapse remains a separate helper.

            MMesh originalMesh = model.GetMesh(edgeKey.meshId);
            MMesh mesh = originalMesh.Clone();

            // Step 1: Find the two faces incident to the edge.
            Face face1 = null;
            Face face2 = null;
            FindIncidentFaces(edgeKey, out face1, out face2);
            if (face1 == null)
            {
                Debug.LogWarning($"Deleter: Cannot delete edge {edgeKey} - no incident faces found.");
                PlayInvalidDeletionFeedback();
                return;
            }

            if (face1 != null && face2 == null)
            {
                //Special case - deleting an internal edge to the face. Delete the vertex that doesn't border other verts in the
                //face - this will be shown in the vert list by a repeated vert in the face ABCDCE.  To fix this, we delete the
                //DC portion of the sequence resulting in ABCE.
                //We do this by iterating through our vert list looking for a vert that is in the edgekey and has edgekey verts
                //on either side - in our example the D.  Then we construct a new face starting with the next element, but cut
                //it off two vertices earlier - so CEAB/
                MMesh.GeometryOperation deleteInternalEdgeOperation = mesh.StartOperation();
                int vertCount = face1.vertexIds.Count;
                // (x - 1) % count doesn't work, but (x + count - 1) % count is mathematically equivalent
                int modulusMinusOne = vertCount - 1;
                int startVert = -1;
                // Find the point
                for (int i = 0; i < vertCount; i++)
                {
                    if (edgeKey.ContainsVertex(face1.vertexIds[i]))
                    {
                        if (edgeKey.ContainsVertex(face1.vertexIds[(i + 1) % vertCount])
                          && edgeKey.ContainsVertex(face1.vertexIds[(i + modulusMinusOne) % vertCount]))
                        {
                            startVert = (i + 1) % vertCount;
                        }
                    }
                }
                List<int> replacementFaceVertIds = new List<int>();
                for (int i = 0; i < vertCount - 2; i++)
                {
                    replacementFaceVertIds.Add(face1.vertexIds[(startVert + i) % vertCount]);
                }
                deleteInternalEdgeOperation.DeleteFace(face1.id);
                deleteInternalEdgeOperation.AddFace(replacementFaceVertIds, face1.properties);
                deleteInternalEdgeOperation.Commit();

                MMesh.GeometryOperation cleanupOp = mesh.StartOperation();
                if (mesh.reverseTable[edgeKey.vertexId1].Count == 0)
                {
                    cleanupOp.DeleteVertex(edgeKey.vertexId1);
                }
                if (mesh.reverseTable[edgeKey.vertexId2].Count == 0)
                {
                    cleanupOp.DeleteVertex(edgeKey.vertexId2);
                }
                cleanupOp.Commit();
                TryApplyValidatedMesh(mesh, originalMesh,
                  $"Deleter: Cannot delete internal edge {edgeKey} - resulting mesh would be invalid.");
                return;
            }

            int face1EdgeKey1Index = FindLastEdgeVertexInFace(edgeKey, face1);
            if (face1EdgeKey1Index == -1)
            {
                Debug.LogWarning($"Deleter: Cannot delete edge {edgeKey} - edge vertices not found in face {face1.id}.");
                return;
            }
            int face2EdgeKeyIndex = FindLastEdgeVertexInFace(edgeKey, face2);
            if (face2EdgeKeyIndex == -1)
            {
                Debug.LogWarning($"Deleter: Cannot delete edge {edgeKey} - edge vertices not found in face {face2.id}.");
                return;
            }

            List<int> vertexIds = new List<int>();
            vertexIds.Add(face1.vertexIds[face1EdgeKey1Index]);
            while (!edgeKey.ContainsVertex(face1.vertexIds[(face1EdgeKey1Index + 1) % face1.vertexIds.Count]))
            {
                face1EdgeKey1Index = (face1EdgeKey1Index + 1) % face1.vertexIds.Count;
                vertexIds.Add(face1.vertexIds[face1EdgeKey1Index]);
            }
            vertexIds.Add(face2.vertexIds[face2EdgeKeyIndex]);
            while (!edgeKey.ContainsVertex(face2.vertexIds[(face2EdgeKeyIndex + 1) % face2.vertexIds.Count]))
            {
                face2EdgeKeyIndex = (face2EdgeKeyIndex + 1) % face2.vertexIds.Count;
                vertexIds.Add(face2.vertexIds[face2EdgeKeyIndex]);
            }

            if (!Features.allowNoncoplanarFaces && !MeshUtil.AreFacesCoplanar(mesh, face1, face2))
            {
                if (!MeshUtil.TryDeleteEdgeAndMakePlanar(mesh, edgeKey, face1, face2, vertexIds.AsReadOnly()))
                {
                    Debug.LogWarning($"Deleter: Cannot delete edge {edgeKey} - no clear planar merged face exists.");
                    PlayInvalidDeletionFeedback();
                    return;
                }

                TryApplyValidatedMesh(mesh, originalMesh,
                  $"Deleter: Cannot delete edge {edgeKey} - resulting planarized mesh would be invalid.");
                return;
            }

            MMesh.GeometryOperation edgeDeletionOperation = mesh.StartOperation();
            edgeDeletionOperation.DeleteFace(face1.id);
            edgeDeletionOperation.DeleteFace(face2.id);

            edgeDeletionOperation.AddFace(vertexIds, face1.properties);
            edgeDeletionOperation.Commit();
            TryApplyValidatedMesh(mesh, originalMesh,
              $"Deleter: Cannot delete edge {edgeKey} - resulting mesh would be invalid.");
            return;
        }

        // Finds the two faces incident to a given edge.
        private void FindIncidentFaces(EdgeKey edgeKey, out Face face1, out Face face2)
        {
            MMesh mesh = model.GetMesh(edgeKey.meshId);
            face1 = null;
            face2 = null;
            foreach (Face face in mesh.GetFaces())
            {
                if (face.vertexIds.Contains(edgeKey.vertexId1) && face.vertexIds.Contains(edgeKey.vertexId2))
                { // Could optimise this to be one pass
                    if (face1 == null)
                    {
                        face1 = face;
                    }
                    else
                    {
                        face2 = face;
                        return;
                    }
                }
            }
        }

        private void DeleteFace(FaceKey faceKey)
        {
            MMesh originalMesh = model.GetMesh(faceKey.meshId);
            MMesh mesh = originalMesh.Clone();
            MeshUtil.DeleteFaceAndMergeAdjacentFaces(mesh, faceKey.faceId);
            TryApplyValidatedMesh(mesh, originalMesh,
              $"Deleter: Cannot delete face {faceKey.faceId} in mesh {mesh.id} - resulting mesh would be invalid.");
        }

        private bool TryApplyValidatedMesh(MMesh mesh, MMesh originalMesh, string invalidMessage)
        {
            HashSet<int> updatedVertIds = new HashSet<int>(mesh.GetVertexIds());
            MeshFixer.FixMutatedMesh(originalMesh, mesh, updatedVertIds,
                /* splitNonCoplanarFaces */ true, /* mergeAdjacentCoplanarFaces */ false);
            // if (MeshValidator.(mesh, updatedVertIds))
            // {
                model.ApplyCommand(new ReplaceMeshCommand(mesh.id, mesh));
                return true;
            // }

            Debug.LogWarning(invalidMessage);
            PlayInvalidDeletionFeedback();
            return false;
        }

        private void PlayInvalidDeletionFeedback()
        {
            audioLibrary.PlayClip(audioLibrary.errorSound);
            peltzerController.TriggerHapticFeedback();
        }

        private void StartDeleting()
        {
            isDeleting = true;
        }

        private void FinishDeleting()
        {
            isDeleting = false;

            selector.DeselectAll();

            List<Command> deleteCommands = new List<Command>();
            foreach (int meshId in meshIdsToDelete)
            {
                deleteCommands.Add(new DeleteMeshCommand(meshId));
            }

            if (deleteCommands.Count > 0)
            {
                Command compositeCommand = new CompositeCommand(deleteCommands);
                model.ApplyCommand(compositeCommand);

            }

            meshIdsToDelete.Clear();
        }

        private static bool IsSetSnapTriggerTooltipEvent(ControllerEventArgs args)
        {
            return args.ControllerType == ControllerType.PALETTE
              && args.ButtonId == ButtonId.Trigger
              && args.Action == ButtonAction.LIGHT_DOWN;
        }

        /// <summary>
        ///   Cancel any deletions that have been performed in the current operation.
        /// </summary>
        public bool CancelDeletionsSoFar()
        {
            bool anythingToDo = meshIdsToDelete.Count > 0;
            foreach (int meshId in meshIdsToDelete)
            {
                model.UnmarkMeshForDeletion(meshId);
            }
            meshIdsToDelete.Clear();
            return anythingToDo;
        }

        // Test method.
        public void TriggerUpdateForTest()
        {
            Update();
        }

        // This function returns a point which is a projection from a point to a plane.
        public static Vector3 ProjectPointOnPlane(Vector3 planeNormal, Vector3 planePoint, Vector3 point)
        {
            // First calculate the distance from the point to the plane:
            float distance = Vector3.Dot(planeNormal.normalized, (point - planePoint));

            // Reverse the sign of the distance.
            distance *= -1;

            // Get a translation vector.
            Vector3 translationVector = planeNormal.normalized * distance;

            // Translate the point to form a projection
            return point + translationVector;
        }
    }
}
