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
using com.google.apps.peltzer.client.model.export;
using com.google.apps.peltzer.client.model.render;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace com.google.apps.peltzer.client.serialization
{
    [TestFixture]
    // Tests for round-tripping arbitrary (non-palette) face colours through a PeltzerFile.
    //
    // These deliberately avoid MaterialRegistry.init: it needs a MaterialLibrary full of Unity materials, and
    // none of the custom colour machinery depends on it. Without init the palette is not loaded, so
    // GetOrCreateMaterialId allocates a custom ID for every colour it is given.
    public class CustomColorSerializationTest
    {
        private static readonly Color32 ORANGE = new Color32(213, 94, 0, 255);
        private static readonly Color32 TEAL = new Color32(0, 158, 115, 255);
        private static readonly Color32 TRANSLUCENT_BLUE = new Color32(17, 34, 51, 128);

        [SetUp]
        public void ResetRegistryBefore()
        {
            MaterialRegistry.ClearCustomColors();
        }

        [TearDown]
        public void ResetRegistryAfter()
        {
            MaterialRegistry.ClearCustomColors();
        }

        [Test]
        public void GetOrCreateMaterialIdReusesIdsForRepeatedColors()
        {
            int first = MaterialRegistry.GetOrCreateMaterialId(ORANGE);
            int second = MaterialRegistry.GetOrCreateMaterialId(ORANGE);
            int other = MaterialRegistry.GetOrCreateMaterialId(TEAL);

            Assert.AreEqual(first, second, "The same colour should map back to the same material ID.");
            Assert.AreNotEqual(first, other);
            Assert.IsTrue(MaterialRegistry.IsCustomMaterialId(first));
            Assert.IsTrue(first >= MaterialRegistry.CUSTOM_COLOR_START);
            Assert.AreEqual(2, MaterialRegistry.GetCustomColorCount());
        }

        [Test]
        public void GetOrCreateMaterialIdTreatsAlphaAsSignificant()
        {
            Color32 opaque = new Color32(17, 34, 51, 255);
            int opaqueId = MaterialRegistry.GetOrCreateMaterialId(opaque);
            int translucentId = MaterialRegistry.GetOrCreateMaterialId(TRANSLUCENT_BLUE);

            Assert.AreNotEqual(opaqueId, translucentId,
              "Colours differing only in alpha are distinct and must not share a material ID.");
        }

        [Test]
        public void CustomColorsSurviveARoundTrip()
        {
            int orangeId = MaterialRegistry.GetOrCreateMaterialId(ORANGE);
            int tealId = MaterialRegistry.GetOrCreateMaterialId(TEAL);

            PeltzerFile loaded = RoundTrip(BuildFile(orangeId, tealId));

            Assert.AreEqual(2, loaded.customColorPalette.Count);
            AssertColorsEqual(ORANGE, MaterialRegistry.GetMaterialColor32ById(orangeId));
            AssertColorsEqual(TEAL, MaterialRegistry.GetMaterialColor32ById(tealId));
            AssertFaceMaterialIds(loaded, orangeId, tealId);
        }

        [Test]
        public void CustomColorsSurviveARoundTripThroughAnEmptyRegistry()
        {
            int orangeId = MaterialRegistry.GetOrCreateMaterialId(ORANGE);
            int tealId = MaterialRegistry.GetOrCreateMaterialId(TEAL);
            byte[] bytes = Serialize(BuildFile(orangeId, tealId));

            // Simulate loading into a fresh session: nothing is registered when the file is parsed.
            MaterialRegistry.ClearCustomColors();
            Assert.AreEqual(0, MaterialRegistry.GetCustomColorCount());

            PeltzerFile loaded = Deserialize(bytes);

            Assert.AreEqual(2, MaterialRegistry.GetCustomColorCount());
            AssertColorsEqual(ORANGE, MaterialRegistry.GetMaterialColor32ById(orangeId));
            AssertColorsEqual(TEAL, MaterialRegistry.GetMaterialColor32ById(tealId));
            AssertFaceMaterialIds(loaded, orangeId, tealId);
        }

        [Test]
        public void RegisterCustomMaterialsRestoresThePaletteAfterAReset()
        {
            int orangeId = MaterialRegistry.GetOrCreateMaterialId(ORANGE);
            int tealId = MaterialRegistry.GetOrCreateMaterialId(TEAL);
            PeltzerFile loaded = Deserialize(Serialize(BuildFile(orangeId, tealId)));

            // This is the sequence the load paths follow: parse the file, clear the model (and with it the
            // registry), then load the file's meshes into the model.
            MaterialRegistry.ClearCustomColors();
            loaded.RegisterCustomMaterials();

            Assert.AreEqual(2, MaterialRegistry.GetCustomColorCount());
            AssertColorsEqual(ORANGE, MaterialRegistry.GetMaterialColor32ById(orangeId));
            AssertColorsEqual(TEAL, MaterialRegistry.GetMaterialColor32ById(tealId));
        }

        [Test]
        public void CollidingIdsFromAnotherFileAreRemapped()
        {
            // Save a file whose faces use the first custom ID for orange.
            int orangeId = MaterialRegistry.GetOrCreateMaterialId(ORANGE);
            byte[] bytes = Serialize(BuildFile(orangeId, orangeId));

            // Start a session where that same ID already means teal, so the incoming file collides.
            MaterialRegistry.ClearCustomColors();
            int tealId = MaterialRegistry.GetOrCreateMaterialId(TEAL);
            Assert.AreEqual(orangeId, tealId, "Expected the colliding ID to be handed out again after a reset.");

            PeltzerFile loaded = Deserialize(bytes);

            // Teal keeps the contested ID; orange is moved out of the way and the faces follow it.
            AssertColorsEqual(TEAL, MaterialRegistry.GetMaterialColor32ById(tealId));
            int remappedId = OnlyFaceMaterialId(loaded);
            Assert.AreNotEqual(tealId, remappedId, "The colliding colour should have been given a fresh ID.");
            Assert.IsTrue(MaterialRegistry.IsCustomMaterialId(remappedId));
            AssertColorsEqual(ORANGE, MaterialRegistry.GetMaterialColor32ById(remappedId));
            Assert.IsTrue(loaded.customColorPalette.ContainsKey(remappedId),
              "The retained palette should be keyed by the IDs the meshes actually reference.");
        }

        [Test]
        public void MatchingIdsFromAnotherFileAreNotRemapped()
        {
            int orangeId = MaterialRegistry.GetOrCreateMaterialId(ORANGE);
            byte[] bytes = Serialize(BuildFile(orangeId, orangeId));

            // Same colour already registered against the same ID: loading should leave everything alone.
            PeltzerFile loaded = Deserialize(bytes);

            Assert.AreEqual(1, MaterialRegistry.GetCustomColorCount());
            Assert.AreEqual(orangeId, OnlyFaceMaterialId(loaded));
        }

        [Test]
        public void FilesWithoutCustomColorsStayOnTheLegacyFormatVersion()
        {
            byte[] withoutCustomColors = Serialize(BuildFile(MaterialRegistry.WHITE_ID, MaterialRegistry.BLACK_ID));
            Assert.AreEqual(1, ReadFormatVersion(withoutCustomColors));

            int orangeId = MaterialRegistry.GetOrCreateMaterialId(ORANGE);
            byte[] withCustomColors = Serialize(BuildFile(orangeId, MaterialRegistry.BLACK_ID));
            Assert.AreEqual(2, ReadFormatVersion(withCustomColors));

            // A palette-only file must still load, and must not leave a stray palette behind.
            MaterialRegistry.ClearCustomColors();
            PeltzerFile loaded = Deserialize(withoutCustomColors);
            Assert.AreEqual(0, MaterialRegistry.GetCustomColorCount());
            Assert.AreEqual(0, loaded.customColorPalette.Count);
        }

        [Test]
        public void SerializedSizeEstimateCoversTheCustomPalette()
        {
            // An over-run only costs a reallocation, but PeltzerFileHandler logs an error every time it happens,
            // so a heavily recoloured model should not blow the estimate on every save. An icosphere at this
            // subdivision has plenty of faces to give all of these colours somewhere to live.
            List<int> materialIds = new List<int>();
            for (int i = 0; i < 128; i++)
            {
                materialIds.Add(MaterialRegistry.GetOrCreateMaterialId(new Color32((byte)i, 7, 200, 255)));
            }

            MMesh sphere = Primitives.AxisAlignedIcosphere(
              /* id */ 1000, Vector3.zero, Vector3.one, materialIds[0], /* recursionLevel */ 2);
            PeltzerFile file = BuildFile(Recolor(sphere, materialIds));

            Assert.AreEqual(128, MaterialRegistry.GetCustomColorCount());
            Assert.IsTrue(Serialize(file).Length <= file.GetSerializedSizeEstimate());
        }

        /// <summary>
        /// Builds a single-mesh file out of a box whose faces cycle through the given material IDs.
        /// </summary>
        private static PeltzerFile BuildFile(params int[] materialIds)
        {
            MMesh box = Primitives.AxisAlignedBox(
              /* id */ 1000, Vector3.zero, Vector3.one, materialIds[0]);
            return BuildFile(Recolor(box, new List<int>(materialIds)));
        }

        private static PeltzerFile BuildFile(MMesh mesh)
        {
            return new PeltzerFile(
              new Metadata("Randall Peltzer", "Jun 8, 1984", "1.0"),
              /* zoomFactor */ 1.0f,
              new List<PeltzerMaterial>(),
              new List<MMesh> { mesh });
        }

        /// <summary>
        /// Cycles the given material IDs over the mesh's faces, in place.
        /// </summary>
        private static MMesh Recolor(MMesh mesh, List<int> materialIds)
        {
            MMesh.GeometryOperation operation = mesh.StartOperation();
            int next = 0;
            foreach (Face face in mesh.GetFaces())
            {
                operation.ModifyFace(face.id, face.vertexIds, new FaceProperties(materialIds[next]));
                next = (next + 1) % materialIds.Count;
            }
            operation.CommitWithoutRecalculation();
            return mesh;
        }

        private static byte[] Serialize(PeltzerFile file)
        {
            PolySerializer serializer = new PolySerializer();
            serializer.SetupForWriting(16);
            file.Serialize(serializer);
            serializer.FinishWriting();
            return serializer.ToByteArray();
        }

        private static PeltzerFile Deserialize(byte[] bytes)
        {
            PolySerializer serializer = new PolySerializer();
            serializer.SetupForReading(bytes, 0, bytes.Length);
            return new PeltzerFile(serializer);
        }

        private static PeltzerFile RoundTrip(PeltzerFile file)
        {
            return Deserialize(Serialize(file));
        }

        /// <summary>
        /// Reads just the format version from the head of the top-level chunk.
        /// </summary>
        private static int ReadFormatVersion(byte[] bytes)
        {
            PolySerializer serializer = new PolySerializer();
            serializer.SetupForReading(bytes, 0, bytes.Length);
            serializer.StartReadingChunk(SerializationConsts.CHUNK_PELTZER);
            return serializer.ReadInt();
        }

        private static int OnlyFaceMaterialId(PeltzerFile file)
        {
            HashSet<int> materialIds = new HashSet<int>();
            foreach (MMesh mesh in file.meshes)
            {
                foreach (Face face in mesh.GetFaces())
                {
                    materialIds.Add(face.properties.materialId);
                }
            }
            Assert.AreEqual(1, materialIds.Count, "Expected every face to share one material ID.");
            foreach (int materialId in materialIds)
            {
                return materialId;
            }
            return -1;
        }

        private static void AssertFaceMaterialIds(PeltzerFile file, params int[] expected)
        {
            HashSet<int> actual = new HashSet<int>();
            foreach (MMesh mesh in file.meshes)
            {
                foreach (Face face in mesh.GetFaces())
                {
                    actual.Add(face.properties.materialId);
                }
            }
            CollectionAssert.AreEquivalent(expected, actual);
        }

        private static void AssertColorsEqual(Color32 expected, Color32 actual)
        {
            Assert.AreEqual(expected.r, actual.r, "red");
            Assert.AreEqual(expected.g, actual.g, "green");
            Assert.AreEqual(expected.b, actual.b, "blue");
            Assert.AreEqual(expected.a, actual.a, "alpha");
        }
    }
}
