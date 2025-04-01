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
    /// This class exists primarily to hold the static method for RenderVertices when SELECT is set. It may be possible to
    /// consolidate this with the other Vertes*Style classes in the future.
    /// </summary>
    public class VertexSelectStyle
    {

        public static Material material;
        public static Mesh vertexMesh;
        private static List<Matrix4x4> matrices = new List<Matrix4x4>();
        // Renders vertex highlights.
        // There are some obvious optimization opportunities here if profiling shows them to be necessary (mostly reusing
        // vertex geometry frame to frame) - 37281287
        public static void RenderVertices(Model model,
          HighlightUtils.TrackedHighlightSet<VertexKey> vertexHighlights,
          WorldSpace worldSpace)
        {
            // Renders vertex highlights.
            HashSet<VertexKey> keys = vertexHighlights.getKeysForStyle((int)VertexStyles.VERTEX_SELECT);
            if (keys.Count == 0) { return; }
            Vector3[] vertices = new Vector3[vertexHighlights.RenderableCount()];
            float scaleFactor = InactiveRenderer.GetVertScaleFactor(worldSpace);
            int i = 0;
            matrices.Clear();
            foreach (VertexKey key in keys)
            {
                if (!model.HasMesh(key.meshId)) { continue; }
                MMesh mesh = model.GetMesh(key.meshId);
                if (!mesh.HasVertex(key.vertexId))
                {
                    continue;
                }
                vertices[i] = mesh.VertexPositionInModelCoords(key.vertexId);
                float animPct = vertexHighlights.GetAnimPct(key);
                // get consistent scale for all vertices
                matrices.Add(worldSpace.modelToWorld * Matrix4x4.TRS(vertices[i], Quaternion.identity, Vector3.one * (scaleFactor * animPct)));
                i++;
            }
            Graphics.DrawMeshInstanced(vertexMesh, 0, material, matrices);
        }
    }
}
