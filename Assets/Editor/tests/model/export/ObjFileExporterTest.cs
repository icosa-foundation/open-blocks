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

using com.google.apps.peltzer.client.model.core;
using NUnit.Framework;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEngine;

namespace com.google.apps.peltzer.client.model.export
{
    [TestFixture]
    public class ObjFileExporterTest
    {
        [Test]
        public void ObjFileFromMeshesUsesInvariantDecimalSeparator()
        {
            CultureInfo originalCulture = Thread.CurrentThread.CurrentCulture;
            CultureInfo originalUiCulture = Thread.CurrentThread.CurrentUICulture;

            try
            {
                CultureInfo germanCulture = CultureInfo.GetCultureInfo("de-DE");
                Thread.CurrentThread.CurrentCulture = germanCulture;
                Thread.CurrentThread.CurrentUICulture = germanCulture;

                MMesh mesh = Primitives.AxisAlignedBox(
                  1, Vector3.zero, new Vector3(0.25f, 0.5f, 0.75f), /* materialId */ 2);
                HashSet<int> materials = new HashSet<int>();

                ObjFileExporter.ObjFileFromMeshes(
                  new[] { mesh }, "model.mtl", /* meshRepresentationCache */ null, ref materials,
                  /* triangulated */ false, out byte[] bytes, out int _);

                string obj = Encoding.UTF8.GetString(bytes);
                StringAssert.Contains("v 0.25 -0.5 -0.75", obj);
                StringAssert.DoesNotContain(",", obj);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = originalCulture;
                Thread.CurrentThread.CurrentUICulture = originalUiCulture;
            }
        }

        [Test]
        public void ObjFileFromMeshesPreservesPerCornerAutoSmoothNormals()
        {
            List<Vector3> vertices = new List<Vector3>
            {
                Vector3.zero, Vector3.right, Vector3.up, Vector3.forward,
            };
            List<List<int>> faces = new List<List<int>>
            {
                new List<int> { 0, 1, 2 },
                new List<int> { 1, 0, 3 },
            };
            List<FaceProperties> properties = new List<FaceProperties>
            {
                new FaceProperties(2), new FaceProperties(2),
            };
            MMesh mesh = new MMesh(1, Vector3.zero, Quaternion.identity, vertices, faces, properties);
            mesh.SetAutoSmooth(90f);
            HashSet<int> materials = new HashSet<int>();

            ObjFileExporter.ObjFileFromMeshes(
              new[] { mesh }, "model.mtl", /* meshRepresentationCache */ null, ref materials,
              /* triangulated */ false, out byte[] bytes, out int _);

            string[] faceLines = Encoding.UTF8.GetString(bytes).Split('\n')
              .Where(line => line.StartsWith("f ")).ToArray();
            Assert.AreEqual(2, faceLines.Length);
            foreach (string faceLine in faceLines)
            {
                string[] normalIndices = faceLine.Trim().Split(' ').Skip(1)
                  .Select(vertex => vertex.Substring(vertex.LastIndexOf('/') + 1)).ToArray();
                Assert.AreEqual(2, normalIndices.Distinct().Count(),
                  "Each wedge face should contain blended normals at the shared edge and a flat normal at its boundary corner.");
            }
        }
    }
}
