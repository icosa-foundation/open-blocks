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
using NUnit.Framework;
using UnityEngine;
using com.google.apps.peltzer.client.model.util;

namespace com.google.apps.peltzer.client.model.core
{
    [TestFixture]
    public class MeshUtilTest
    {
        [Test]
        public void TestNonSplitSimple()
        {
            // Create a three point triangle.
            Vertex v1 = new Vertex(1, new Vector3(0, 0, 0));
            Vertex v2 = new Vertex(2, new Vector3(0, 0, 1));
            Vertex v3 = new Vertex(3, new Vector3(0, 1, 0));

            Vector3 normal = new Vector3(1, 0, 0);

            Face face = new Face(1,
              new List<int>(new int[] { 1, 2, 3 }).AsReadOnly(),
              normal,
              new FaceProperties());

            Dictionary<int, Vertex> vertById = new Dictionary<int, Vertex>();
            vertById[1] = v1;
            vertById[2] = v2;
            vertById[3] = v3;
            Dictionary<int, Face> facesById = new Dictionary<int, Face>();
            facesById[1] = face;

            MMesh mesh = new MMesh(1, Vector3.zero, Quaternion.identity, vertById, facesById);

            // Face has only three verts, should never be split.
            MMesh meshCopy = mesh.Clone();
            MMesh.GeometryOperation operation = meshCopy.StartOperation();
            operation.ModifyVertexMeshSpace(1, new Vector3(0.1f, 0, 0));
            MeshUtil.SplitFaceIfNeeded(operation, mesh.GetFace(1), 1);
            operation.Commit();
            NUnit.Framework.Assert.AreEqual(1, mesh.faceCount, "Should not have split face.");
        }

        [Test]
        public void TestNonSplit()
        {
            // Create a square in a plane.
            Vertex v1 = new Vertex(1, new Vector3(0, 0, 0));
            Vertex v2 = new Vertex(2, new Vector3(0, 0, 1));
            Vertex v3 = new Vertex(3, new Vector3(0, 1, 1));
            Vertex v4 = new Vertex(4, new Vector3(0, 1, 0));

            Vector3 normal = new Vector3(1, 0, 0);

            Face face = new Face(1,
              new List<int>(new int[] { 1, 2, 3, 4 }).AsReadOnly(),
              normal,
              new FaceProperties());

            Dictionary<int, Vertex> vertById = new Dictionary<int, Vertex>();
            vertById[1] = v1;
            vertById[2] = v2;
            vertById[3] = v3;
            vertById[4] = v4;
            Dictionary<int, Face> facesById = new Dictionary<int, Face>();
            facesById[1] = face;

            MMesh mesh = new MMesh(1, Vector3.zero, Quaternion.identity, vertById, facesById);

            // Move first corner, but within the plane.
            MMesh meshCopy = mesh.Clone();
            MMesh.GeometryOperation operation = meshCopy.StartOperation();
            operation.ModifyVertexMeshSpace(1, new Vector3(0, -1, -1));
            MeshUtil.SplitFaceIfNeeded(operation, mesh.GetFace(1), 1);
            operation.Commit();
            NUnit.Framework.Assert.AreEqual(1, mesh.faceCount, "Should not have split face.");
        }

        [Test]
        public void TestSplit()
        {
            int vertToMove = 1;

            // Create a square in a plane.
            Vertex v1 = new Vertex(vertToMove, new Vector3(0, 0, 0));
            Vertex v2 = new Vertex(2, new Vector3(0, 0, 1));
            Vertex v3 = new Vertex(3, new Vector3(0, 1, 1));
            Vertex v4 = new Vertex(4, new Vector3(0, 1, 0));

            Vector3 normal = new Vector3(1, 0, 0);

            Face face = new Face(1,
              new List<int>(new int[] { 1, 2, 3, 4 }).AsReadOnly(),
              normal,
              new FaceProperties());

            Dictionary<int, Vertex> vertById = new Dictionary<int, Vertex>();
            vertById[vertToMove] = v1;
            vertById[2] = v2;
            vertById[3] = v3;
            vertById[4] = v4;
            Dictionary<int, Face> facesById = new Dictionary<int, Face>();
            facesById[1] = face;

            MMesh mesh = new MMesh(1, Vector3.zero, Quaternion.identity, vertById, facesById);

            // Move first corner, out of plane.
            MMesh meshCopy = mesh.Clone();
            MMesh.GeometryOperation operation = meshCopy.StartOperation();
            operation.ModifyVertexMeshSpace(1, new Vector3(0.1f, 0, 0));
            MeshUtil.SplitFaceIfNeeded(operation, mesh.GetFace(1), 1);
            operation.Commit();

            NUnit.Framework.Assert.AreEqual(2, mesh.faceCount, "Should have split face.");

            // Make sure the vertex was removed from this face.
            Face updatedFace = mesh.GetFace(1);

            string s = updatedFace.id + "   ";
            foreach (int n in updatedFace.vertexIds)
            {
                s += n + ", ";
            }

            NUnit.Framework.Assert.AreEqual(3, updatedFace.vertexIds.Count);
            NUnit.Framework.Assert.False(updatedFace.vertexIds.Contains(vertToMove), "Vertex should have been removed: " + s);

            // Find the other face, it should contain the vert.
            Face newFace = null;
            foreach (Face f in mesh.GetFaces())
            {
                if (f.id != 1)
                {
                    newFace = f;
                }
            }

            NUnit.Framework.Assert.NotNull(newFace);
            NUnit.Framework.Assert.AreEqual(3, newFace.vertexIds.Count);
            NUnit.Framework.Assert.True(newFace.vertexIds.Contains(vertToMove), "Vertex should be in new face.");
        }

        [Test]
        public void TestDeleteCubeFaceMergesNeighborsToSingleVertex()
        {
            MMesh mesh = Primitives.AxisAlignedBox(1, Vector3.zero, Vector3.one, 2);

            MeshUtil.DeleteFaceAndMergeAdjacentFaces(mesh, 3);

            NUnit.Framework.Assert.AreEqual(5, mesh.faceCount);
            NUnit.Framework.Assert.AreEqual(5, mesh.vertexCount);
            NUnit.Framework.Assert.IsTrue(TopologyUtil.HasValidTopology(mesh, true));

            int apexVertexId = -1;
            foreach (Vertex vertex in mesh.GetVertices())
            {
                if (mesh.reverseTable[vertex.id].Count == 4)
                {
                    apexVertexId = vertex.id;
                    break;
                }
            }

            NUnit.Framework.Assert.AreNotEqual(-1, apexVertexId, "Expected a single shared apex vertex.");

            int triangleCount = 0;
            foreach (Face face in mesh.GetFaces())
            {
                if (face.vertexIds.Count == 3)
                {
                    triangleCount++;
                    NUnit.Framework.Assert.True(face.vertexIds.Contains(apexVertexId));
                }
                else
                {
                    NUnit.Framework.Assert.AreEqual(4, face.vertexIds.Count);
                    NUnit.Framework.Assert.False(face.vertexIds.Contains(apexVertexId));
                }
            }

            NUnit.Framework.Assert.AreEqual(4, triangleCount);
        }

        [Test]
        public void TestDeleteCubeTopEdgeCollapsesToSingleVertex()
        {
            MMesh mesh = Primitives.AxisAlignedBox(1, Vector3.zero, Vector3.one, 2);

            MeshUtil.DeleteEdgeAndCollapse(mesh, new EdgeKey(mesh.id, 2, 3));

            NUnit.Framework.Assert.AreEqual(6, mesh.faceCount);
            NUnit.Framework.Assert.AreEqual(7, mesh.vertexCount);
            NUnit.Framework.Assert.IsTrue(TopologyUtil.HasValidTopology(mesh, true));

            int mergedVertexId = -1;
            foreach (Vertex vertex in mesh.GetVertices())
            {
                if (mesh.reverseTable[vertex.id].Count == 4)
                {
                    mergedVertexId = vertex.id;
                    break;
                }
            }

            NUnit.Framework.Assert.AreNotEqual(-1, mergedVertexId, "Expected a single shared collapsed edge vertex.");
        }

        [Test]
        public void TestAreFacesCoplanarReturnsTrueForAdjacentPlanarFaces()
        {
            Dictionary<int, Vertex> vertById = new Dictionary<int, Vertex>();
            vertById[1] = new Vertex(1, new Vector3(-2, -1, 0));
            vertById[2] = new Vertex(2, new Vector3(0, -1, 0));
            vertById[3] = new Vertex(3, new Vector3(2, -1, 0));
            vertById[4] = new Vertex(4, new Vector3(2, 1, 0));
            vertById[5] = new Vertex(5, new Vector3(0, 1, 0));
            vertById[6] = new Vertex(6, new Vector3(-2, 1, 0));

            FaceProperties properties = new FaceProperties();
            Dictionary<int, Face> facesById = new Dictionary<int, Face>();
            facesById[1] = new Face(1,
                new List<int>(new int[] { 1, 2, 5, 6 }).AsReadOnly(),
                new Vector3(0, 0, -1),
                properties);
            facesById[2] = new Face(2,
                new List<int>(new int[] { 2, 3, 4, 5 }).AsReadOnly(),
                new Vector3(0, 0, -1),
                properties);

            MMesh mesh = new MMesh(1, Vector3.zero, Quaternion.identity, vertById, facesById);

            NUnit.Framework.Assert.IsTrue(MeshUtil.AreFacesCoplanar(mesh, mesh.GetFace(1), mesh.GetFace(2)));
        }

        [Test]
        public void TestDeleteCoplanarSharedEdgeRemovesGloballyRedundantVertices()
        {
            Dictionary<int, Vertex> vertById = new Dictionary<int, Vertex>();
            vertById[1] = new Vertex(1, new Vector3(-2, -1, 0));
            vertById[2] = new Vertex(2, new Vector3(0, -1, 0));
            vertById[3] = new Vertex(3, new Vector3(2, -1, 0));
            vertById[4] = new Vertex(4, new Vector3(2, 1, 0));
            vertById[5] = new Vertex(5, new Vector3(0, 1, 0));
            vertById[6] = new Vertex(6, new Vector3(-2, 1, 0));

            FaceProperties properties = new FaceProperties();
            Dictionary<int, Face> facesById = new Dictionary<int, Face>();
            facesById[1] = new Face(1,
                new List<int>(new int[] { 1, 2, 5, 6 }).AsReadOnly(),
                new Vector3(0, 0, -1),
                properties);
            facesById[2] = new Face(2,
                new List<int>(new int[] { 2, 3, 4, 5 }).AsReadOnly(),
                new Vector3(0, 0, -1),
                properties);

            MMesh mesh = new MMesh(1, Vector3.zero, Quaternion.identity, vertById, facesById);
            EdgeKey edgeKey = new EdgeKey(mesh.id, 2, 5);
            Face face1 = mesh.GetFace(1);
            Face face2 = mesh.GetFace(2);

            List<int> mergedVertexIds = BuildMergedFaceVertexIds(edgeKey, face1, face2);
            MMesh.GeometryOperation operation = mesh.StartOperation();
            operation.DeleteFace(face1.id);
            operation.DeleteFace(face2.id);
            Face mergedFace = operation.AddFace(mergedVertexIds, face1.properties);
            operation.Commit();

            Assert.True(MeshUtil.RemoveRedundantColinearVertices(mesh, new int[] { 2, 5 }));

            Face updatedFace = mesh.GetFace(mergedFace.id);
            CollectionAssert.AreEqual(new int[] { 6, 1, 3, 4 }, updatedFace.vertexIds);
            Assert.AreEqual(4, updatedFace.vertexIds.Count);
            Assert.False(mesh.HasVertex(2));
            Assert.False(mesh.HasVertex(5));
        }

        private static List<int> BuildMergedFaceVertexIds(EdgeKey edgeKey, Face face1, Face face2)
        {
            int face1EdgeKeyIndex = FindLastEdgeVertexInFace(edgeKey, face1);
            int face2EdgeKeyIndex = FindLastEdgeVertexInFace(edgeKey, face2);

            NUnit.Framework.Assert.AreNotEqual(-1, face1EdgeKeyIndex);
            NUnit.Framework.Assert.AreNotEqual(-1, face2EdgeKeyIndex);

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

            return vertexIds;
        }

        private static int FindLastEdgeVertexInFace(EdgeKey edgeKey, Face face)
        {
            for (int i = 0; i < face.vertexIds.Count; i++)
            {
                if (face.vertexIds[i] != edgeKey.vertexId1)
                {
                    continue;
                }

                return face.vertexIds[(i + 1) % face.vertexIds.Count] == edgeKey.vertexId2
                    ? (i + 1) % face.vertexIds.Count
                    : i;
            }

            return -1;
        }
    }
}
