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

using System;
using System.Collections.Generic;
using System.Linq;
using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.main;
using UnityEngine;

namespace com.google.apps.peltzer.client.tools.utils
{
    /// <summary>
    /// This class handles rendering of inactive meshes, caching as much as it can frame to frame.
    /// </summary>
    public class InactiveRenderer
    {
        private static float SCALE_THRESH = 1f;

        private static readonly int SelectPositionWorld = Shader.PropertyToID("_SelectPositionWorld");
        private static readonly int SelectRadius = Shader.PropertyToID("_SelectRadius");

        private HashSet<EdgeKey> edgeSet = new HashSet<EdgeKey>();
        private List<Matrix4x4> edgeMatrices = new List<Matrix4x4>();
        public Mesh edgeMesh; // used to render the inactive thick "wireframe" highlights
        private List<VertexKey> vertexKeys = new List<VertexKey>();
        private List<Matrix4x4> vertexMatrices = new List<Matrix4x4>();
        public Mesh vertexMesh;

        private Model model;
        private WorldSpace worldSpace;
        public Material inactiveEdgeMaterial;
        public Material inactivePointMaterial;
        private Vector3 selectPositionWorld;

        /// <summary>
        /// Sets whether the inactive renderer should render inactive vertices.
        /// </summary>
        public bool showPoints { get; set; }

        /// <summary>
        /// Determines whether the inactive renderer should render inactive edges.
        /// </summary>
        public bool showEdges { get; set; }

        private static float baseVertexScale;
        private static float baseEdgeScale;

        public InactiveRenderer(Model model, WorldSpace worldSpace, MaterialLibrary materialLibrary)
        {
            this.model = model;
            this.worldSpace = worldSpace;
            inactiveEdgeMaterial = new Material(materialLibrary.pointEdgeInactiveMaterial);
            inactivePointMaterial = new Material(materialLibrary.pointEdgeInactiveMaterial);
            // baseVertexScale = inactivePointMaterial.GetFloat("_PointSphereRadius");
            // baseEdgeScale = inactiveEdgeMaterial.GetFloat("_PointSphereRadius");
            // these are the default values for the scale of the vertex and edge highlights
            baseVertexScale = 0.006f;
            baseEdgeScale = 0.005f;
        }

        /// <summary>
        /// Returns the scale factor used for rendering inactive vertices - used by selector to make sure selection radii
        /// match with what the user sees.
        /// </summary>
        public static float GetVertScaleFactor(WorldSpace worldSpace)
        {
            return (Mathf.Min(worldSpace.scale, SCALE_THRESH) / SCALE_THRESH) * baseVertexScale;
        }

        /// <summary>
        /// Returns the scale factor used for rendering inactive edges - used by selector to make sure selection radii
        /// match with what the user sees.
        /// </summary>
        public static float GetEdgeScaleFactor(WorldSpace worldSpace)
        {
            return (Mathf.Min(worldSpace.scale, SCALE_THRESH) / SCALE_THRESH) * baseEdgeScale;
        }

        /// <summary>
        /// Turns on edge wireframes for supplied meshes. (Will use cached data if a mesh has been passed to this method
        /// since the most recent clear.)
        /// </summary>
        public void TurnOnEdgeWireframe(IEnumerable<int> meshIds, HashSet<EdgeKey> selectedEdges, EdgeKey hoveredEdge)
        {
            edgeSet.Clear();
            foreach (int meshId in meshIds)
            {
                if (!model.HasMesh(meshId)) continue;

                MMesh polyMesh = model.GetMesh(meshId);

                foreach (Face curFace in polyMesh.GetFaces())
                {
                    for (int i = 0; i < curFace.vertexIds.Count; i++)
                    {
                        var edge = new EdgeKey(meshId, curFace.vertexIds[i], curFace.vertexIds[(i + 1) % curFace.vertexIds.Count]);

                        if (selectedEdges.Contains(edge) || edge.Equals(hoveredEdge)) continue;

                        edgeSet.Add(edge);
                    }
                }
            }
            if (edgeSet.Count == 0) return;
            float scaleFactor = GetEdgeScaleFactor(worldSpace);
            var sphereRadii = GetVertScaleFactor(worldSpace) * 2;
            edgeMatrices.Clear();
            foreach (EdgeKey key in edgeSet)
            {
                if (!model.HasMesh(key.meshId)) continue;
                MMesh mesh = model.GetMesh(key.meshId);
                if (!mesh.HasVertex(key.vertexId1) || !mesh.HasVertex(key.vertexId2)) continue;
                var v1 = mesh.VertexPositionInModelCoords(key.vertexId1);
                var v2 = mesh.VertexPositionInModelCoords(key.vertexId2);
                float distance = Vector3.Distance(v1, v2) - sphereRadii;
                Vector3 midpoint = (v1 + v2) / 2;
                Vector3 direction = v2 - v1;
                Quaternion rotation = Quaternion.FromToRotation(Vector3.forward, direction);
                Vector3 scale = new Vector3(scaleFactor, scaleFactor, distance);
                edgeMatrices.Add(Matrix4x4.TRS(midpoint, rotation, scale));
            }
        }

        /// <summary>
        /// Turns on vertex wireframes for supplied meshes. (Will use cached data if a mesh has been passed to this method
        /// since the most recent clear.)
        /// </summary>
        public void TurnOnPointWireframe(IEnumerable<int> meshIds, HashSet<VertexKey> selectedVerts, VertexKey hoveredVert)
        {
            // Debug.Log(meshIds.ToList().Count + " " + selectedVerts.Count + " " + hoveredVert);
            vertexKeys.Clear();
            foreach (int meshId in meshIds)
            {
                if (!model.HasMesh(meshId)) continue;

                MMesh polyMesh = model.GetMesh(meshId);

                foreach (int vertId in polyMesh.GetVertexIds())
                {
                    var vertexKey = new VertexKey(meshId, vertId);
                    if (selectedVerts.Contains(vertexKey) || vertexKey.Equals(hoveredVert)) continue;
                    vertexKeys.Add(vertexKey);
                }
            }

            if (vertexKeys.Count == 0) return;
            float scaleFactor = GetVertScaleFactor(worldSpace);
            vertexMatrices.Clear();
            foreach (VertexKey key in vertexKeys)
            {
                if (!model.HasMesh(key.meshId)) continue;
                MMesh mesh = model.GetMesh(key.meshId);
                if (!mesh.HasVertex(key.vertexId)) continue;
                var v = mesh.VertexPositionInModelCoords(key.vertexId);
                vertexMatrices.Add(Matrix4x4.TRS(v, Quaternion.identity, Vector3.one * scaleFactor));
            }
        }

        /// <summary>
        /// Sets the select position to use for rendering inactive elements - this is used to fade out the selection.
        /// </summary>
        /// <param name="selectPositionModel"></param>
        public void SetSelectPosition(Vector3 selectPositionModel)
        {
            selectPositionWorld = worldSpace.ModelToWorld(selectPositionModel);
        }

        /// <summary>
        /// Clears all vertices and edges out of the inactive renderer.
        /// </summary>
        public void Clear()
        {
            edgeMatrices.Clear();
            vertexMatrices.Clear();
            edgeMatricesWorld.Clear();
            vertexMatricesWorld.Clear();

            // If the user has changed the flag, we handle it here so that next time they use the tool it's updated.
            // It's a bit janky, but since this is handling a console command rather than real UX it's okay - if we go
            // with the new radius it will be set from the start and this just goes away.
            InactiveSelectionHighlighter.INACTIVE_HIGHLIGHT_RADIUS = Features.expandedWireframeRadius
              ? InactiveSelectionHighlighter.NEW_INACTIVE_HIGHLIGHT_RADIUS
              : InactiveSelectionHighlighter.OLD_INACTIVE_HIGHLIGHT_RADIUS;
        }

        private List<Matrix4x4> edgeMatricesWorld = new List<Matrix4x4>();
        /// <summary>
        /// Renders the inactive edges.
        /// </summary>
        public void RenderEdges()
        {
            // Debug.Log("Edges: " + showEdges + " " + edgeMatrices.Count);
            if (showEdges && edgeMatrices.Count > 0)
            {
                // the edgeMatrices don't get updated every frame
                // so when we move the model in world space we need to update the edgeMatrices in world space
                edgeMatricesWorld.Clear();
                for (int i = 0; i < edgeMatrices.Count; i++)
                {
                    edgeMatricesWorld.Add(worldSpace.modelToWorld * edgeMatrices[i]);
                }

                if (edgeMatricesWorld.Count > 0)
                {
                    inactiveEdgeMaterial.SetVector(SelectPositionWorld,
                        new Vector4(selectPositionWorld.x, selectPositionWorld.y, selectPositionWorld.z, 0.0f));
                    inactiveEdgeMaterial.SetFloat(SelectRadius, InactiveSelectionHighlighter.INACTIVE_HIGHLIGHT_RADIUS);

                    Graphics.DrawMeshInstanced(edgeMesh, 0, inactiveEdgeMaterial, edgeMatricesWorld);
                }
            }
        }

        private List<Matrix4x4> vertexMatricesWorld = new List<Matrix4x4>();
        /// <summary>
        /// Renders the inactive vertices.
        /// </summary>
        public void RenderPoints()
        {
            // Debug.Log("Points: " + showPoints + " " + vertexMatrices.Count);
            vertexMatricesWorld.Clear();
            for (int i = 0; i < vertexMatrices.Count; i++)
            {
                // TODO: maybe we could only do this when the world space has actually changed
                vertexMatricesWorld.Add(worldSpace.modelToWorld * vertexMatrices[i]);
            }
            if (vertexMatricesWorld.Count > 0)
            {
                inactivePointMaterial.SetVector(SelectPositionWorld,
                    new Vector4(selectPositionWorld.x, selectPositionWorld.y, selectPositionWorld.z, 0.0f));
                inactivePointMaterial.SetFloat(SelectRadius, InactiveSelectionHighlighter.INACTIVE_HIGHLIGHT_RADIUS);

                Graphics.DrawMeshInstanced(vertexMesh, 0, inactivePointMaterial, vertexMatricesWorld);
            }
        }
    }
}
