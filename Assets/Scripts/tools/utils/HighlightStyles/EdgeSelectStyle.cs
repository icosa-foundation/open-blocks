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
    /// This class exists primarily to hold the static method for RenderEdges when SELECT is set. It may be possible to
    /// consolidate this with the other Vertes*Style classes in the future.
    /// </summary>
    public class EdgeSelectStyle
    {
        public static Material material;
        public static Mesh edgeMesh;
        private static List<Matrix4x4> matrices = new List<Matrix4x4>();
        public static void RenderEdges(Model model,
          HighlightUtils.TrackedHighlightSet<EdgeKey> edgeHighlights,
          WorldSpace worldSpace)
        {
            HashSet<EdgeKey> keys = edgeHighlights.getKeysForStyle((int)EdgeStyles.EDGE_SELECT);
            if (keys.Count == 0) { return; }
            Vector3[] vertices = new Vector3[edgeHighlights.RenderableCount() * 2];
            float scaleFactor = InactiveRenderer.GetEdgeScaleFactor(worldSpace);
            var sphereRadii = InactiveRenderer.GetVertScaleFactor(worldSpace) * 2;
            int i = 0;
            matrices.Clear();
            foreach (EdgeKey key in keys)
            {
                if (!model.HasMesh(key.meshId)) { continue; }
                MMesh mesh = model.GetMesh(key.meshId);
                if (!mesh.HasVertex(key.vertexId1) || !mesh.HasVertex(key.vertexId2)) continue;
                float animPct = edgeHighlights.GetAnimPct(key);
                vertices[i] = mesh.VertexPositionInModelCoords(key.vertexId1);
                i++;
                vertices[i] = mesh.VertexPositionInModelCoords(key.vertexId2);

                // compute distance between vertices
                float distance = Vector3.Distance(vertices[i], vertices[i - 1]) - sphereRadii;
                // compute the midpoint between the two vertices
                Vector3 midpoint = (vertices[i] + vertices[i - 1]) / 2;

                // compute the direction vector between the two vertices
                Vector3 direction = vertices[i] - vertices[i - 1];
                // compute the rotation to align the direction vector with the z-axis
                Quaternion rotation = Quaternion.FromToRotation(Vector3.forward, direction);
                // compute the scale to stretch the edge to the correct length
                Vector3 scale = new Vector3(scaleFactor * animPct, scaleFactor * animPct, distance);
                // compute the transformation matrix
                Matrix4x4 matrix = Matrix4x4.TRS(midpoint, rotation, scale);
                matrices.Add(worldSpace.modelToWorld * matrix);

                i++;
            }
            if (edgeHighlights.RenderableCount() > 0)
            {
                Graphics.DrawMeshInstanced(edgeMesh, 0, material, matrices);
            }
        }
    }
}
