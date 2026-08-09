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
using UnityEngine;
using NUnit.Framework;

namespace com.google.apps.peltzer.client.model.core
{

    [TestFixture]
    // Tests for MMesh.
    public class MMeshTest
    {

        [Test]
        public void TestClone()
        {
            MMesh mesh = Primitives.AxisAlignedBox(
              /* meshId */ 2, Vector3.zero, Vector3.one, /* materialId */ 1);

            MMesh clone = mesh.Clone();

            NUnit.Framework.Assert.AreEqual(mesh.id, clone.id);

            NUnit.Framework.Assert.AreNotSame(mesh.GetFaces(), clone.GetFaces());
            NUnit.Framework.Assert.AreNotSame(mesh.GetFace(0), clone.GetFace(0));

            NUnit.Framework.Assert.AreNotSame(mesh.GetVertices(), clone.GetVertices());
            int vertexTestId = mesh.GetVertexIds().GetEnumerator().Current;
            NUnit.Framework.Assert.AreSame(mesh.GetVertex(vertexTestId), clone.GetVertex(vertexTestId),
              "Vertices are immutable.  They should be shared.");
        }

        [Test]
        public void AutoSmoothBlendsCornersAcrossEdgesWithinAngle()
        {
            MMesh mesh = CreateRightAngleWedge();
            mesh.SetAutoSmooth(90f);

            Vector3 expectedBlend = (Vector3.up + Vector3.forward).normalized;
            List<Vector3> firstFaceNormals = mesh.GetFace(0).GetRenderNormals(mesh);
            List<Vector3> secondFaceNormals = mesh.GetFace(1).GetRenderNormals(mesh);

            AssertClose(firstFaceNormals[0], expectedBlend);
            AssertClose(firstFaceNormals[1], expectedBlend);
            AssertClose(firstFaceNormals[2], Vector3.forward);
            AssertClose(secondFaceNormals[0], expectedBlend);
            AssertClose(secondFaceNormals[1], expectedBlend);
            AssertClose(secondFaceNormals[2], Vector3.up);
        }

        [Test]
        public void AutoSmoothKeepsEdgesAboveAngleHard()
        {
            MMesh mesh = CreateRightAngleWedge();
            mesh.SetAutoSmooth(89f);

            foreach (Face face in mesh.GetFaces())
            {
                foreach (Vector3 normal in face.GetRenderNormals(mesh))
                {
                    AssertClose(normal, face.normal);
                }
            }
        }

        [Test]
        public void FlatShadingIsDefaultAndCanBeRestored()
        {
            MMesh mesh = CreateRightAngleWedge();
            Assert.AreEqual(MMesh.SmoothingMode.Flat, mesh.smoothingMode);

            mesh.SetAutoSmooth(180f);
            mesh.SetFlatShading();

            Assert.AreEqual(MMesh.SmoothingMode.Flat, mesh.smoothingMode);
            foreach (Face face in mesh.GetFaces())
            {
                foreach (Vector3 normal in face.GetRenderNormals(mesh))
                {
                    AssertClose(normal, face.normal);
                }
            }
        }

        [Test]
        public void ClonePreservesSmoothingSettings()
        {
            MMesh mesh = CreateRightAngleWedge();
            mesh.SetAutoSmooth(37f);

            MMesh clone = mesh.Clone();

            Assert.AreEqual(MMesh.SmoothingMode.Auto, clone.smoothingMode);
            Assert.AreEqual(37f, clone.autoSmoothAngle);
        }

        [Test]
        public void GeometryChangesInvalidateAutoSmoothNormals()
        {
            MMesh mesh = CreateRightAngleWedge();
            mesh.SetAutoSmooth(180f);
            Vector3 normalBeforeEdit = mesh.GetFace(0).GetRenderNormals(mesh)[0];

            MMesh.GeometryOperation operation = mesh.StartOperation();
            operation.ModifyVertexMeshSpace(2, new Vector3(0f, 1f, 1f));
            operation.Commit();

            Vector3 normalAfterEdit = mesh.GetFace(0).GetRenderNormals(mesh)[0];
            Assert.Greater(Vector3.Angle(normalBeforeEdit, normalAfterEdit), 1f);
            Vector3 expected = (mesh.GetFace(0).normal + mesh.GetFace(1).normal).normalized;
            AssertClose(normalAfterEdit, expected);
        }

        [Test]
        public void TestBounds()
        {
            MMesh mesh = Primitives.AxisAlignedBox(
              /* meshId */ 2, Vector3.zero, Vector3.one, /* materialId */ 1);

            // Check the mesh bounds.
            Bounds bounds = mesh.bounds;
            AssertClose(bounds.center, Vector3.zero);
            AssertClose(bounds.extents, Vector3.one);

            // Move the mesh and recheck.
            mesh.offset += new Vector3(1, 0, 0);
            mesh.RecalcBounds();
            bounds = mesh.bounds;
            AssertClose(bounds.center, new Vector3(1, 0, 0));
            AssertClose(bounds.extents, Vector3.one);
        }

        private static MMesh CreateRightAngleWedge()
        {
            List<Vector3> vertices = new List<Vector3>
            {
                Vector3.zero,
                Vector3.right,
                Vector3.up,
                Vector3.forward,
            };
            List<List<int>> faces = new List<List<int>>
            {
                new List<int> { 0, 1, 2 },
                new List<int> { 1, 0, 3 },
            };
            List<FaceProperties> properties = new List<FaceProperties>
            {
                new FaceProperties(1),
                new FaceProperties(1),
            };
            return new MMesh(10, Vector3.zero, Quaternion.identity, vertices, faces, properties);
        }

        private static void AssertClose(Vector3 left, Vector3 right)
        {
            NUnit.Framework.Assert.True(Vector3.Distance(left, right) < 0.001f,
              left + " was not similar to " + right);
        }
    }
}
