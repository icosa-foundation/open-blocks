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
    /// This class exists primarily to hold the static method for RenderEdges when TEMPORARY is set. It may be possible to
    /// consolidate this with the other Edge*Style classes in the future.
    /// </summary>
    public class EdgeTemporaryStyle
    {
        private static int nextId = 0;
        // private static Mesh edgeRenderMesh = new Mesh();
        public class TemporaryEdge
        {
            public int id;
            public Vector3 vertex1PositionModelSpace;
            public Vector3 vertex2PositionModelSpace;

            public TemporaryEdge()
            {
                id = nextId++;
            }

            public override bool Equals(object otherObject)
            {
                if (!(otherObject is TemporaryEdge))
                    return false;

                TemporaryEdge other = (TemporaryEdge)otherObject;
                return other.id == id;
            }

            public override int GetHashCode()
            {
                return id;
            }
        }

        public static Material material;
        public static Mesh edgeMesh;
        private static List<Matrix4x4> matrices = new List<Matrix4x4>();
        private static float animPct = 1.0f; // for temp edges it looks too jittery when animated
        // Renders edge highlights.
        // There are some obvious optimization opportunities here if profiling shows them to be necessary (mostly reusing
        // edge geometry frame to frame) - 37281287
        public static void RenderEdges(Model model,
          HighlightUtils.TrackedHighlightSet<TemporaryEdge> temporaryEdgeHighlights,
          WorldSpace worldSpace)
        {
            HashSet<TemporaryEdge> keys = temporaryEdgeHighlights.getKeysForStyle((int)EdgeStyles.EDGE_SELECT);
            if (keys.Count == 0) { return; }
            float scaleFactor = InactiveRenderer.GetEdgeScaleFactor(worldSpace);
            matrices.Clear();
            foreach (TemporaryEdge key in keys)
            {
                float distance = Vector3.Distance(key.vertex1PositionModelSpace, key.vertex2PositionModelSpace);
                Vector3 midpoint = (key.vertex1PositionModelSpace + key.vertex2PositionModelSpace) / 2;
                Vector3 direction = key.vertex2PositionModelSpace - key.vertex1PositionModelSpace;
                Quaternion rotation = Quaternion.FromToRotation(Vector3.forward, direction);
                Vector3 scale = new Vector3(scaleFactor * animPct, scaleFactor * animPct, distance);
                matrices.Add(worldSpace.modelToWorld * Matrix4x4.TRS(midpoint, rotation, scale));
            }
            if (temporaryEdgeHighlights.RenderableCount() > 0)
            {
                Graphics.DrawMeshInstanced(edgeMesh, 0, material, matrices);
            }
        }
    }
}
