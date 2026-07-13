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
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace com.google.apps.peltzer.client.model.core
{
    [TestFixture]
    public class GeometryOperationTest
    {
        [Test]
        public void TestTryGetCurrentFaceReturnsFalseAfterFaceDeletedInSameOperation()
        {
            MMesh mesh = Primitives.AxisAlignedBox(1, Vector3.zero, Vector3.one, 2);
            Face originalFace = mesh.GetFace(1);
            MMesh.GeometryOperation operation = mesh.StartOperation();

            operation.ModifyFace(1,
                new List<int>(originalFace.vertexIds).AsReadOnly(),
                originalFace.properties);
            operation.DeleteFace(1);

            Face currentFace;
            Assert.False(operation.TryGetCurrentFace(1, out currentFace));
        }

        [Test]
        public void TestMovingVertexPreservesUv()
        {
            MMesh mesh = Primitives.AxisAlignedBox(1, Vector3.zero, Vector3.one, 2);
            int vertexId = new List<int>(mesh.GetVertexIds())[0];
            Vertex originalVertex = mesh.GetVertex(vertexId);
            Vector2 expectedUv = new Vector2(0.25f, 0.75f);

            Dictionary<int, Vertex> vertices = new Dictionary<int, Vertex>();
            foreach (Vertex vertex in mesh.GetVertices())
            {
                vertices[vertex.id] = vertex.id == vertexId
                  ? new Vertex(vertex.id, vertex.loc, expectedUv)
                  : vertex;
            }
            MMesh texturedMesh = new MMesh(mesh.id, mesh.offset, mesh.rotation, vertices,
              new Dictionary<int, Face>(mesh.GetFaces().ToDictionary(face => face.id, face => face)));

            MMesh.GeometryOperation operation = texturedMesh.StartOperation();
            operation.ModifyVertexMeshSpace(vertexId, originalVertex.loc + Vector3.right);
            operation.ModifyVertexModelSpace(vertexId,
              texturedMesh.MeshCoordsToModelCoords(originalVertex.loc + Vector3.up));
            operation.Commit();

            Assert.AreEqual(expectedUv, texturedMesh.GetVertex(vertexId).uv);
        }
    }
}
