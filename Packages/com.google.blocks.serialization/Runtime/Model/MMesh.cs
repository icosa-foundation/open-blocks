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
using UnityEngine;

namespace com.google.blocks.serialization
{
    /// <summary>
    ///   An MMesh represents a mesh in the model (simplified for serialization package).
    ///   Named 'MMesh' to avoid ambiguity with Unity meshes.
    /// </summary>
    public class MMesh
    {
        /// <summary>
        /// Special group ID value meaning "no group".
        /// </summary>
        public const int GROUP_NONE = 0;

        /// <summary>
        /// ID of this MMesh in the model.
        /// </summary>
        private int _id;

        /// <summary>
        /// Offset (position) of this MMesh in model space.
        /// </summary>
        public Vector3 _offset;

        /// <summary>
        /// Rotation of this MMesh in model space.
        /// </summary>
        public Quaternion _rotation = Quaternion.identity;

        /// <summary>
        /// Group ID (for organizing meshes).
        /// </summary>
        public int groupId;

        /// <summary>
        /// Remix IDs (for tracking model ancestry).
        /// </summary>
        public HashSet<string> remixIds;

        private Dictionary<int, Vertex> verticesById;
        private Dictionary<int, Face> facesById;

        public int id { get { return _id; } }
        public Vector3 offset { get { return _offset; } }
        public Quaternion rotation { get { return _rotation; } }
        public int vertexCount { get { return verticesById.Count; } }
        public int faceCount { get { return facesById.Count; } }

        /// <summary>
        /// Creates a new MMesh with the given properties.
        /// </summary>
        public MMesh(int id, Vector3 offset, Quaternion rotation, int groupId,
            Dictionary<int, Vertex> vertices, Dictionary<int, Face> faces, HashSet<string> remixIds = null)
        {
            _id = id;
            _offset = offset;
            _rotation = rotation;
            this.groupId = groupId;
            this.verticesById = vertices;
            this.facesById = faces;
            this.remixIds = remixIds;
        }

        /// <summary>
        /// Gets all vertices in this mesh.
        /// </summary>
        public IEnumerable<Vertex> GetVertices()
        {
            return verticesById.Values;
        }

        /// <summary>
        /// Gets all faces in this mesh.
        /// </summary>
        public IEnumerable<Face> GetFaces()
        {
            return facesById.Values;
        }

        /// <summary>
        /// Gets a vertex by ID.
        /// </summary>
        public Vertex VertexById(int vertexId)
        {
            return verticesById[vertexId];
        }

        /// <summary>
        /// Gets a face by ID.
        /// </summary>
        public Face FaceById(int faceId)
        {
            return facesById[faceId];
        }

        /// <summary>
        /// Writes to PolySerializer.
        /// </summary>
        public void Serialize(PolySerializer serializer)
        {
            serializer.StartWritingChunk(SerializationConsts.CHUNK_MMESH);
            serializer.WriteInt(_id);
            PolySerializationUtils.WriteVector3(serializer, offset);
            PolySerializationUtils.WriteQuaternion(serializer, rotation);
            serializer.WriteInt(groupId);

            // Write vertices.
            serializer.WriteCount(verticesById.Count);
            foreach (Vertex v in verticesById.Values)
            {
                serializer.WriteInt(v.id);
                PolySerializationUtils.WriteVector3(serializer, v.loc);
            }

            // Write faces.
            serializer.WriteCount(facesById.Count);
            foreach (Face face in facesById.Values)
            {
                serializer.WriteInt(face.id);
                serializer.WriteInt(face.properties.materialId);
                PolySerializationUtils.WriteIntList(serializer, face.vertexIds);
                // Repeat the face normal for backwards compatability.
                PolySerializationUtils.WriteVector3List(serializer,
                  Enumerable.Repeat(face.normal, face.vertexIds.Count).ToList());

                // DEPRECATED: Write holes.
                serializer.WriteCount(0);
            }
            serializer.FinishWritingChunk(SerializationConsts.CHUNK_MMESH);

            // If we have any remix IDs, also write a remix info chunk.
            // As per the design of the file format, this chunk will be automatically skipped by older versions
            // that don't expect remix IDs in the file.
            if (remixIds != null)
            {
                serializer.StartWritingChunk(SerializationConsts.CHUNK_MMESH_EXT_REMIX_IDS);
                PolySerializationUtils.WriteStringSet(serializer, remixIds);
                serializer.FinishWritingChunk(SerializationConsts.CHUNK_MMESH_EXT_REMIX_IDS);
            }
        }

        /// <summary>
        /// Gets a (generous) estimate on how large the serialized mesh will be.
        /// </summary>
        public int GetSerializedSizeEstimate()
        {
            int estimate = 256;  // Headers, offset, rotation, group ID, overhead.
            estimate += 8 + verticesById.Count * 16;  // count + (1 int + 3 floats) per vertex.
            foreach (Face face in facesById.Values)
            {
                estimate += 32;  // ID, material ID, headers.
                estimate += 8 + face.vertexIds.Count * 4;  // count + 1 int per vertex ID
                estimate += 8 + face.vertexIds.Count * 12;  // count + 3 floats per normal
            }
            if (remixIds != null)
            {
                estimate += 32; // list header overhead
                foreach (string remixId in remixIds)
                {
                    estimate += 4 + remixId.Length;
                }
            }
            return estimate;
        }

        /// <summary>
        /// Reads from PolySerializer (deserialization constructor).
        /// </summary>
        public MMesh(PolySerializer serializer)
        {
            serializer.StartReadingChunk(SerializationConsts.CHUNK_MMESH);
            _id = serializer.ReadInt();
            _offset = PolySerializationUtils.ReadVector3(serializer);
            _rotation = PolySerializationUtils.ReadQuaternion(serializer);
            groupId = serializer.ReadInt();

            verticesById = new Dictionary<int, Vertex>();
            facesById = new Dictionary<int, Face>();

            // Read vertices.
            int vertexCount = serializer.ReadCount(0, SerializationConsts.MAX_VERTICES_PER_MESH, "vertexCount");
            for (int i = 0; i < vertexCount; i++)
            {
                int vertexId = serializer.ReadInt();
                Vector3 vertexLoc = PolySerializationUtils.ReadVector3(serializer);
                verticesById[vertexId] = new Vertex(vertexId, vertexLoc);
            }

            // Read faces.
            int faceCount = serializer.ReadCount(0, SerializationConsts.MAX_FACES_PER_MESH, "faceCount");
            for (int i = 0; i < faceCount; i++)
            {
                int faceId = serializer.ReadInt();
                int materialId = serializer.ReadInt();
                List<int> vertexIds =
                  PolySerializationUtils.ReadIntList(serializer, 0, SerializationConsts.MAX_VERTICES_PER_FACE, "vertexIds");

                List<Vector3> normals =
                  PolySerializationUtils.ReadVector3List(serializer, 0, SerializationConsts.MAX_VERTICES_PER_FACE, "normals");

                // Holes are deprecated.  We read their data but don't do anything with it.
                int holeCount = serializer.ReadCount(0, SerializationConsts.MAX_HOLES_PER_FACE, "holes");
                for (int j = 0; j < holeCount; j++)
                {
                    PolySerializationUtils.ReadIntList(serializer, 0,
                      SerializationConsts.MAX_VERTICES_PER_HOLE, "hole vertexIds");
                    PolySerializationUtils.ReadVector3List(serializer, 0,
                      SerializationConsts.MAX_VERTICES_PER_HOLE, "hole normals");
                }

                facesById[faceId] = new Face(faceId, vertexIds.AsReadOnly(), verticesById, new FaceProperties(materialId));
            }

            serializer.FinishReadingChunk(SerializationConsts.CHUNK_MMESH);

            // If the remix IDs chunk is present (it's optional), read it.
            if (serializer.GetNextChunkLabel() == SerializationConsts.CHUNK_MMESH_EXT_REMIX_IDS)
            {
                serializer.StartReadingChunk(SerializationConsts.CHUNK_MMESH_EXT_REMIX_IDS);
                remixIds = PolySerializationUtils.ReadStringSet(serializer, 0, SerializationConsts.MAX_REMIX_IDS_PER_MMESH,
                  "remixIds");
                serializer.FinishReadingChunk(SerializationConsts.CHUNK_MMESH_EXT_REMIX_IDS);
            }
            else
            {
                // No remix IDs present in file.
                remixIds = null;
            }
        }
    }
}
