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

using System.Collections.Generic;
using UnityEngine;

using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.main;
using com.google.apps.peltzer.client.model.render;

namespace com.google.apps.peltzer.client.tools.utils
{
    /// <summary>
    /// This class exists primarily to hold the static method for RenderEdges when INACTIVE is set. It may be possible to
    /// consolidate this with the other Edge*Style classes in the future.
    /// </summary>
    public class EdgeInactiveStyle
    {

        public static Material material;
        public static Vector3 selectPositionModel;
        public static Mesh edgeMesh;
        private static List<Matrix4x4> matrices = new List<Matrix4x4>();

        // Renders edge highlights.
        // There are some obvious optimization opportunities here if profiling shows them to be necessary (mostly reusing
        // edge geometry frame to frame) - 37281287
        public static void RenderEdges(Model model,
          HighlightUtils.TrackedHighlightSet<EdgeKey> edgeHighlights,
          WorldSpace worldSpace)
        {
            HashSet<EdgeKey> keys = edgeHighlights.getKeysForStyle((int)EdgeStyles.EDGE_INACTIVE);
            if (keys.Count == 0) { return; }
            Vector3[] vertices = new Vector3[edgeHighlights.RenderableCount() * 2];
            int i = 0;
            float radius2 = InactiveSelectionHighlighter.INACTIVE_HIGHLIGHT_RADIUS *
                            InactiveSelectionHighlighter.INACTIVE_HIGHLIGHT_RADIUS;
            float scaleFactor = 2;// InactiveRenderer.GetEdgeScaleFactor(worldSpace);
            matrices.Clear();
            material.color = new Color(1, 0, 0, 1.0f);
            foreach (EdgeKey key in keys)
            {
                if (!model.HasMesh(key.meshId)) { continue; }
                MMesh mesh = model.GetMesh(key.meshId);
                if (!mesh.HasVertex(key.vertexId1) || !mesh.HasVertex(key.vertexId2)) continue;
                vertices[i] = mesh.VertexPositionInModelCoords(key.vertexId1);
                Vector3 diff = vertices[i] - selectPositionModel;
                float dist2 = Vector3.Dot(diff, diff);
                float alpha = Mathf.Clamp((radius2 - dist2) / radius2, 0f, 1f);
                float animPct = edgeHighlights.GetAnimPct(key);

                // indices[i] = i;
                // selectData[i] = new Vector2(animPct, alpha);
                // normals[i] = normal;
                i++;
                vertices[i] = mesh.VertexPositionInModelCoords(key.vertexId2);
                diff = vertices[i] - selectPositionModel;
                dist2 = Vector3.Dot(diff, diff);
                alpha = Mathf.Clamp((radius2 - dist2) / radius2, 0f, 1f);
                // if (alpha <= 0.01f) continue;

                // indices[i] = i;
                // selectData[i] = new Vector2(animPct, alpha);
                // normals[i] = normal;

                float distance = Vector3.Distance(vertices[i], vertices[i - 1]);
                Vector3 midpoint = (vertices[i] + vertices[i - 1]) / 2;
                Vector3 direction = vertices[i] - vertices[i - 1];
                Quaternion rotation = Quaternion.FromToRotation(Vector3.forward, direction);
                Vector3 scale = new Vector3(scaleFactor, scaleFactor, distance);
                Matrix4x4 matrix = Matrix4x4.TRS(midpoint, rotation, scale);
                matrices.Add(worldSpace.modelToWorld * matrix);

                i++;
            }
            // edgeRenderMesh.vertices = vertices;
            // These are not actually UVs - we're using the UV channel to pass per-primitive animation data so that edges
            // animate independently.
            // edgeRenderMesh.uv = selectData;
            // Since we're using a line geometry shader we need to set the mesh up to supply data as lines.
            // edgeRenderMesh.SetIndices(indices, MeshTopology.Lines, 0 /* submesh id */, false /* recalculate bounds */);
            if (edgeHighlights.RenderableCount() > 0)
            {
                // Graphics.DrawMesh(edgeRenderMesh, worldSpace.modelToWorld, material, 0);
                Graphics.DrawMeshInstanced(edgeMesh, 0, material, matrices);

            }
        }
    }
}
