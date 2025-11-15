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

using com.google.blocks.serialization;
using System.Collections.Generic;
using UnityEngine;

namespace Examples
{
    /// <summary>
    /// Basic example demonstrating how to use the Blocks file format serialization package.
    /// </summary>
    public class BasicSerializationExample : MonoBehaviour
    {
        void Start()
        {
            // Create a simple triangle mesh
            MMesh triangleMesh = CreateTriangleMesh();

            // Save to bytes
            List<MMesh> meshes = new List<MMesh> { triangleMesh };
            byte[] data = BlocksFileFormat.SaveToBytes(meshes, "ExampleCreator", "1.0");

            if (data != null)
            {
                Debug.Log($"Successfully serialized mesh to {data.Length} bytes");

                // Load it back
                PeltzerFile loadedFile;
                if (BlocksFileFormat.LoadFromBytes(data, out loadedFile))
                {
                    Debug.Log($"Successfully loaded file with {loadedFile.meshes.Count} meshes");
                    Debug.Log($"Creator: {loadedFile.metadata.creatorName}");
                    Debug.Log($"Created on: {loadedFile.metadata.creationDate}");

                    foreach (MMesh mesh in loadedFile.meshes)
                    {
                        Debug.Log($"Mesh {mesh.id}: {mesh.vertexCount} vertices, {mesh.faceCount} faces");
                    }
                }
            }
        }

        /// <summary>
        /// Creates a simple triangle mesh for demonstration.
        /// </summary>
        MMesh CreateTriangleMesh()
        {
            // Create three vertices forming a triangle
            Dictionary<int, Vertex> vertices = new Dictionary<int, Vertex>
            {
                { 1, new Vertex(1, new Vector3(0, 0, 0)) },
                { 2, new Vertex(2, new Vector3(1, 0, 0)) },
                { 3, new Vertex(3, new Vector3(0.5f, 1, 0)) }
            };

            // Create a face using those vertices
            List<int> vertexIds = new List<int> { 1, 2, 3 };
            Face triangleFace = new Face(1, vertexIds.AsReadOnly(), vertices, new FaceProperties(0));

            Dictionary<int, Face> faces = new Dictionary<int, Face>
            {
                { 1, triangleFace }
            };

            // Create the mesh
            MMesh mesh = new MMesh(
                id: 1,
                offset: Vector3.zero,
                rotation: Quaternion.identity,
                groupId: MMesh.GROUP_NONE,
                vertices: vertices,
                faces: faces
            );

            return mesh;
        }
    }
}
