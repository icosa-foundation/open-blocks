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

        [Test]
        public void TestDeletingCubeEdgeIsValidAfterFixup()
        {
            MMesh mesh = Primitives.AxisAlignedBox(1, Vector3.zero, Vector3.one, 2);
            MMesh originalMesh = mesh.Clone();
            EdgeKey edgeKey = new EdgeKey(mesh.id, 2, 3);

            Face face1 = null;
            Face face2 = null;
            foreach (Face face in mesh.GetFaces())
            {
                if (!face.vertexIds.Contains(edgeKey.vertexId1) || !face.vertexIds.Contains(edgeKey.vertexId2))
                {
                    continue;
                }

                if (face1 == null)
                {
                    face1 = face;
                }
                else
                {
                    face2 = face;
                    break;
                }
            }

            Assert.NotNull(face1, "Expected first incident face for cube edge.");
            Assert.NotNull(face2, "Expected second incident face for cube edge.");

            MMesh.GeometryOperation edgeDeletionOperation = mesh.StartOperation();
            edgeDeletionOperation.DeleteFace(face1.id);
            edgeDeletionOperation.DeleteFace(face2.id);

            int face1EdgeKeyIndex = FindLastEdgeVertexInFace(edgeKey, face1);
            int face2EdgeKeyIndex = FindLastEdgeVertexInFace(edgeKey, face2);
            Assert.AreNotEqual(-1, face1EdgeKeyIndex);
            Assert.AreNotEqual(-1, face2EdgeKeyIndex);

            List<int> vertexIds = new List<int>();
            vertexIds.Add(face1.vertexIds[face1EdgeKeyIndex]);
            while (!edgeKey.ContainsVertex(face1.vertexIds[(face1EdgeKeyIndex + 1) % face1.vertexIds.Count]))
            {
                face1EdgeKeyIndex = (face1EdgeKeyIndex + 1) % face1.vertexIds.Count;
                vertexIds.Add(face1.vertexIds[face1EdgeKeyIndex]);
            }

            vertexIds.Add(face2.vertexIds[face2EdgeKeyIndex]);
            while (!edgeKey.ContainsVertex(face2.vertexIds[(face2EdgeKeyIndex + 1) % face2.vertexIds.Count]))
            {
                face2EdgeKeyIndex = (face2EdgeKeyIndex + 1) % face2.vertexIds.Count;
                vertexIds.Add(face2.vertexIds[face2EdgeKeyIndex]);
            }

            edgeDeletionOperation.AddFace(vertexIds, face1.properties);
            edgeDeletionOperation.Commit();

            MeshFixer.FixMutatedMesh(originalMesh, mesh, new HashSet<int>(mesh.GetVertexIds()),
                /* splitNonCoplanarFaces */ true, /* mergeAdjacentCoplanarFaces */ false);

            Assert.True(MeshValidator.IsValidMesh(mesh, new HashSet<int>(mesh.GetVertexIds())));
            Assert.True(TopologyUtil.HasValidTopology(mesh, true));
        }

        [Test]
        public void TestPlanarizedCubeEdgeDeletionIsValidAfterFixup()
        {
            MMesh mesh = Primitives.AxisAlignedBox(1, Vector3.zero, Vector3.one, 2);
            MMesh originalMesh = mesh.Clone();
            EdgeKey edgeKey = new EdgeKey(mesh.id, 2, 3);

            Face face1 = null;
            Face face2 = null;
            foreach (Face face in mesh.GetFaces())
            {
                if (!face.vertexIds.Contains(edgeKey.vertexId1) || !face.vertexIds.Contains(edgeKey.vertexId2))
                {
                    continue;
                }

                if (face1 == null)
                {
                    face1 = face;
                }
                else
                {
                    face2 = face;
                    break;
                }
            }

            Assert.NotNull(face1, "Expected first incident face for cube edge.");
            Assert.NotNull(face2, "Expected second incident face for cube edge.");
            Assert.False(MeshUtil.AreFacesCoplanar(mesh, face1, face2));

            int face1EdgeKeyIndex = FindLastEdgeVertexInFace(edgeKey, face1);
            int face2EdgeKeyIndex = FindLastEdgeVertexInFace(edgeKey, face2);
            Assert.AreNotEqual(-1, face1EdgeKeyIndex);
            Assert.AreNotEqual(-1, face2EdgeKeyIndex);

            List<int> vertexIds = new List<int>();
            vertexIds.Add(face1.vertexIds[face1EdgeKeyIndex]);
            while (!edgeKey.ContainsVertex(face1.vertexIds[(face1EdgeKeyIndex + 1) % face1.vertexIds.Count]))
            {
                face1EdgeKeyIndex = (face1EdgeKeyIndex + 1) % face1.vertexIds.Count;
                vertexIds.Add(face1.vertexIds[face1EdgeKeyIndex]);
            }

            vertexIds.Add(face2.vertexIds[face2EdgeKeyIndex]);
            while (!edgeKey.ContainsVertex(face2.vertexIds[(face2EdgeKeyIndex + 1) % face2.vertexIds.Count]))
            {
                face2EdgeKeyIndex = (face2EdgeKeyIndex + 1) % face2.vertexIds.Count;
                vertexIds.Add(face2.vertexIds[face2EdgeKeyIndex]);
            }

            Assert.True(MeshUtil.TryDeleteEdgeAndMakePlanar(mesh, edgeKey, face1, face2, vertexIds.AsReadOnly()));

            MeshFixer.FixMutatedMesh(originalMesh, mesh, new HashSet<int>(mesh.GetVertexIds()),
                /* splitNonCoplanarFaces */ true, /* mergeAdjacentCoplanarFaces */ false);

            Assert.AreEqual(5, mesh.faceCount);
            Assert.AreEqual(8, mesh.vertexCount);
            Assert.True(MeshValidator.IsValidMesh(mesh, new HashSet<int>(mesh.GetVertexIds())));
            Assert.True(TopologyUtil.HasValidTopology(mesh, true));

            foreach (Face face in mesh.GetFaces())
            {
                Assert.False(MeshUtil.FaceContainsEdge(face, edgeKey.vertexId1, edgeKey.vertexId2));
            }
        }

        private static int FindLastEdgeVertexInFace(EdgeKey edge, Face face)
        {
            for (int i = 0; i < face.vertexIds.Count; i++)
            {
                if (face.vertexIds[i] != edge.vertexId1)
                {
                    continue;
                }

                return face.vertexIds[(i + 1) % face.vertexIds.Count] == edge.vertexId2
                    ? (i + 1) % face.vertexIds.Count
                    : i;
            }

            return -1;
        }
    }
}
