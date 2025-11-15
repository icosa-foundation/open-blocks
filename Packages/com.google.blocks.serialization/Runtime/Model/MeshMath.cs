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
using System.Collections.ObjectModel;
using UnityEngine;

namespace com.google.blocks.serialization
{
    /// <summary>
    ///   Math associated with meshes, faces and vertices (simplified for serialization package).
    /// </summary>
    public class MeshMath
    {
        /// <summary>
        /// Calculates a normal from a clockwise wound array of vertices,
        /// </summary>
        /// <param name="vertices"></param>
        /// <param name="verticesById"></param>
        /// <returns></returns>
        public static Vector3 CalculateNormal(ReadOnlyCollection<int> vertices, Dictionary<int, Vertex> verticesById)
        {
            if (vertices.Count == 0) return Vector3.zero;
            // This uses Newell's method, which is proven to generate correct normal for any polygon.
            Vector3 normal = Vector3.zero;
            int count = vertices.Count;
            Vector3 thisPos = verticesById[vertices[0]].loc;
            Vector3 nextPos;
            for (int i = 0, next = 1; i < count; i++, next++)
            {
                // Note: this is cheaper than computing "next % count" at each iteration.
                next = (next == count) ? 0 : next;
                nextPos = verticesById[vertices[next]].loc;
                normal.x += (thisPos.y - nextPos.y) * (thisPos.z + nextPos.z);
                normal.y += (thisPos.z - nextPos.z) * (thisPos.x + nextPos.x);
                normal.z += (thisPos.x - nextPos.x) * (thisPos.y + nextPos.y);
                thisPos = nextPos;
            }
            return Math3d.Normalize(normal);
        }
    }
}
