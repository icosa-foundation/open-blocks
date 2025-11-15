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
    ///   A polygonal face of a MMesh (simplified for serialization package).
    ///   Vertices must be specified in clockwise order.
    /// </summary>
    public class Face
    {
        // The id of this face (unique within the mesh)
        private readonly int _id;
        // The ordered collection of vertex ids that comprise the face, in clockwise order.
        private readonly ReadOnlyCollection<int> _vertexIds;
        // The face normal.
        private Vector3 _normal;
        // The properties of the face - primarily the material.
        private FaceProperties _properties;

        // Read-only getters.
        public int id { get { return _id; } }
        public ReadOnlyCollection<int> vertexIds { get { return _vertexIds; } }
        public Vector3 normal { get { return _normal; } }
        public FaceProperties properties { get { return _properties; } }

        /// <summary>
        /// Constructs a face, calculating its normal.
        /// </summary>
        /// <param name="id">Face id</param>
        /// <param name="vertexIds">Vertex ids for face in clockwise winding order.</param>
        /// <param name="verticesById">Dictionary of vertex ids to vertex data.</param>
        /// <param name="properties">Face properties</param>
        public Face(int id, ReadOnlyCollection<int> vertexIds, Dictionary<int, Vertex> verticesById, FaceProperties properties)
        {
            _id = id;
            _vertexIds = vertexIds;
            _normal = MeshMath.CalculateNormal(vertexIds, verticesById);
            _properties = properties;
        }
    }
}
