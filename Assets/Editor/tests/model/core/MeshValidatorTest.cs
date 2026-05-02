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
using NUnit.Framework;
using UnityEngine;
using com.google.apps.peltzer.client.model.util;

namespace com.google.apps.peltzer.client.model.core
{
    [TestFixture]
    public class MeshValidatorTest
    {
        [Test]
        public void TestIsValidMeshRejectsFlattenedBox()
        {
            MMesh mesh = Primitives.AxisAlignedBox(1, Vector3.zero, Vector3.one, 2);
            MMesh.GeometryOperation operation = mesh.StartOperation();

            foreach (Vertex vertex in mesh.GetVertices())
            {
                Vector3 position = mesh.VertexPositionInMeshCoords(vertex.id);
                operation.ModifyVertexMeshSpace(vertex.id, new Vector3(position.x, -1.0f, position.z));
            }

            operation.Commit();

            Assert.False(MeshValidator.IsValidMesh(mesh, new HashSet<int>(mesh.GetVertexIds())));
        }

        [Test]
        public void TestIsValidMeshRejectsDeletingSquarePyramidApex()
        {
            MMesh mesh = Primitives.AxisAlignedBox(1, Vector3.zero, Vector3.one, 2);
            MeshUtil.DeleteFaceAndMergeAdjacentFaces(mesh, 3);

            int apexVertexId = -1;
            foreach (Vertex vertex in mesh.GetVertices())
            {
                if (mesh.reverseTable[vertex.id].Count == 4)
                {
                    apexVertexId = vertex.id;
                    break;
                }
            }

            Assert.AreNotEqual(-1, apexVertexId, "Expected square pyramid apex vertex.");

            MeshUtil.DeleteVertexAndMergeAdjacentFaces(mesh, apexVertexId);

            Assert.False(MeshValidator.IsValidMesh(mesh, new HashSet<int>(mesh.GetVertexIds())));
        }

        [Test]
        public void TestDeletingSquarePyramidBaseVertexIsValidAfterFixup()
        {
            MMesh mesh = Primitives.AxisAlignedBox(1, Vector3.zero, Vector3.one, 2);
            MeshUtil.DeleteFaceAndMergeAdjacentFaces(mesh, 3);

            int apexVertexId = -1;
            int baseVertexId = -1;
            foreach (Vertex vertex in mesh.GetVertices())
            {
                int faceUseCount = mesh.reverseTable[vertex.id].Count;
                if (faceUseCount == 4)
                {
                    apexVertexId = vertex.id;
                }
                else if (faceUseCount == 3)
                {
                    baseVertexId = vertex.id;
                }
            }

            Assert.AreNotEqual(-1, apexVertexId, "Expected square pyramid apex vertex.");
            Assert.AreNotEqual(-1, baseVertexId, "Expected deletable base vertex.");

            MMesh originalMesh = mesh.Clone();
            MeshUtil.DeleteVertexAndMergeAdjacentFaces(mesh, baseVertexId);
            MeshFixer.FixMutatedMesh(originalMesh, mesh, new HashSet<int>(mesh.GetVertexIds()),
                /* splitNonCoplanarFaces */ true, /* mergeAdjacentCoplanarFaces */ false);

            Assert.True(MeshValidator.IsValidMesh(mesh, new HashSet<int>(mesh.GetVertexIds())));
            Assert.AreEqual(4, mesh.vertexCount);
            Assert.AreEqual(4, mesh.faceCount);
            Assert.True(TopologyUtil.HasValidTopology(mesh, true));
        }
    }
}
