// Copyright 2025 The Open Blocks Authors
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

using UnityEngine;
using System.Collections.Generic;
using System.IO;
using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.render;
using System;
using System.Text;

namespace com.google.apps.peltzer.client.model.import
{
    /// <summary>
    /// Imports MagicaVoxel .vox files.
    ///
    /// Based on the MagicaVoxel VOX format specification:
    /// https://github.com/ephtracy/voxel-model/blob/master/MagicaVoxel-file-format-vox.txt
    /// </summary>
    public static class VoxImporter
    {
        private const float VOXEL_SIZE = 0.1f; // Size of each voxel in Unity units

        /// <summary>
        /// Voxel data structure
        /// </summary>
        private struct Voxel
        {
            public int x, y, z;
            public byte colorIndex;

            public Voxel(int x, int y, int z, byte colorIndex)
            {
                this.x = x;
                this.y = y;
                this.z = z;
                this.colorIndex = colorIndex;
            }
        }

        /// <summary>
        /// Import options for VOX files
        /// </summary>
        public class VoxImportOptions
        {
            /// <summary>
            /// If true, generates separate cube geometry for each voxel.
            /// If false, generates optimized mesh with internal face culling.
            /// </summary>
            public bool generateSeparateCubes = false;

            /// <summary>
            /// Scale factor to apply to the voxel model
            /// </summary>
            public float scale = VOXEL_SIZE;
        }

        /// <summary>
        /// Creates an MMesh from the contents of a .vox file with the given id.
        /// </summary>
        /// <param name="voxFileContents">The contents of a .vox file.</param>
        /// <param name="id">The id of the new MMesh.</param>
        /// <param name="result">The created mesh, or null if it could not be created.</param>
        /// <param name="options">Import options (optional).</param>
        /// <returns>Whether the MMesh could be created.</returns>
        public static bool MMeshFromVoxFile(
            byte[] voxFileContents,
            int id,
            out MMesh result,
            VoxImportOptions options = null)
        {
            if (options == null)
            {
                options = new VoxImportOptions();
            }

            try
            {
                using (MemoryStream stream = new MemoryStream(voxFileContents))
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    // Parse VOX file
                    if (!ParseVoxFile(reader, out List<Voxel> voxels, out Color[] palette))
                    {
                        Debug.LogError("Failed to parse VOX file");
                        result = null;
                        return false;
                    }

                    if (voxels.Count == 0)
                    {
                        Debug.LogError("VOX file contains no voxels");
                        result = null;
                        return false;
                    }

                    // Generate MMesh
                    if (options.generateSeparateCubes)
                    {
                        result = GenerateSeparateCubesMesh(id, voxels, palette, options.scale);
                    }
                    else
                    {
                        result = GenerateOptimizedMesh(id, voxels, palette, options.scale);
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error importing VOX file: {ex.Message}\n{ex.StackTrace}");
                result = null;
                return false;
            }
        }

        /// <summary>
        /// Parse a VOX file and extract voxel data and palette
        /// </summary>
        private static bool ParseVoxFile(BinaryReader reader, out List<Voxel> voxels, out Color[] palette)
        {
            voxels = new List<Voxel>();
            palette = GetDefaultPalette();

            // Read header
            string magic = Encoding.ASCII.GetString(reader.ReadBytes(4));
            if (magic != "VOX ")
            {
                Debug.LogError($"Invalid VOX file magic: {magic}");
                return false;
            }

            int version = reader.ReadInt32();
            Debug.Log($"VOX file version: {version}");

            // Read MAIN chunk
            string mainId = Encoding.ASCII.GetString(reader.ReadBytes(4));
            if (mainId != "MAIN")
            {
                Debug.LogError($"Expected MAIN chunk, got: {mainId}");
                return false;
            }

            int mainChunkSize = reader.ReadInt32();
            int mainChildrenSize = reader.ReadInt32();

            // Parse chunks
            long endPosition = reader.BaseStream.Position + mainChildrenSize;
            Vector3Int modelSize = Vector3Int.zero;

            while (reader.BaseStream.Position < endPosition)
            {
                string chunkId = Encoding.ASCII.GetString(reader.ReadBytes(4));
                int chunkSize = reader.ReadInt32();
                int childrenSize = reader.ReadInt32();
                long chunkEnd = reader.BaseStream.Position + chunkSize;

                if (chunkId == "SIZE")
                {
                    modelSize.x = reader.ReadInt32();
                    modelSize.y = reader.ReadInt32();
                    modelSize.z = reader.ReadInt32();
                    Debug.Log($"Model size: {modelSize}");
                }
                else if (chunkId == "XYZI")
                {
                    int numVoxels = reader.ReadInt32();
                    for (int i = 0; i < numVoxels; i++)
                    {
                        byte x = reader.ReadByte();
                        byte y = reader.ReadByte();
                        byte z = reader.ReadByte();
                        byte colorIndex = reader.ReadByte();
                        voxels.Add(new Voxel(x, y, z, colorIndex));
                    }
                    Debug.Log($"Loaded {numVoxels} voxels");
                }
                else if (chunkId == "RGBA")
                {
                    palette = new Color[256];
                    for (int i = 0; i < 256; i++)
                    {
                        byte r = reader.ReadByte();
                        byte g = reader.ReadByte();
                        byte b = reader.ReadByte();
                        byte a = reader.ReadByte();
                        palette[i] = new Color(r / 255f, g / 255f, b / 255f, a / 255f);
                    }
                    Debug.Log("Loaded custom palette");
                }

                // Skip to end of chunk and its children
                reader.BaseStream.Position = chunkEnd + childrenSize;
            }

            return true;
        }

        /// <summary>
        /// Generate optimized mesh with internal face culling
        /// </summary>
        private static MMesh GenerateOptimizedMesh(int id, List<Voxel> voxels, Color[] palette, float scale)
        {
            // Create voxel lookup table for fast neighbor checks
            HashSet<Vector3Int> voxelSet = new HashSet<Vector3Int>();
            Dictionary<Vector3Int, byte> voxelColors = new Dictionary<Vector3Int, byte>();

            foreach (var voxel in voxels)
            {
                Vector3Int pos = new Vector3Int(voxel.x, voxel.y, voxel.z);
                voxelSet.Add(pos);
                voxelColors[pos] = voxel.colorIndex;
            }

            List<Vector3> vertices = new List<Vector3>();
            List<List<int>> faces = new List<List<int>>();
            List<FaceProperties> faceProperties = new List<FaceProperties>();
            Dictionary<Vector3, int> vertexMap = new Dictionary<Vector3, int>();

            // Face directions (normals)
            Vector3Int[] faceDirections = new Vector3Int[]
            {
                new Vector3Int(1, 0, 0),   // Right
                new Vector3Int(-1, 0, 0),  // Left
                new Vector3Int(0, 1, 0),   // Top
                new Vector3Int(0, -1, 0),  // Bottom
                new Vector3Int(0, 0, 1),   // Front
                new Vector3Int(0, 0, -1)   // Back
            };

            // For each face direction, define the 4 vertices (relative to voxel position)
            Vector3[][] faceVertexOffsets = new Vector3[][]
            {
                // Right (+X)
                new Vector3[]
                {
                    new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(1, 1, 1), new Vector3(1, 0, 1)
                },
                // Left (-X)
                new Vector3[]
                {
                    new Vector3(0, 0, 1), new Vector3(0, 1, 1), new Vector3(0, 1, 0), new Vector3(0, 0, 0)
                },
                // Top (+Y)
                new Vector3[]
                {
                    new Vector3(0, 1, 0), new Vector3(1, 1, 0), new Vector3(1, 1, 1), new Vector3(0, 1, 1)
                },
                // Bottom (-Y)
                new Vector3[]
                {
                    new Vector3(0, 0, 1), new Vector3(1, 0, 1), new Vector3(1, 0, 0), new Vector3(0, 0, 0)
                },
                // Front (+Z)
                new Vector3[]
                {
                    new Vector3(0, 0, 1), new Vector3(1, 0, 1), new Vector3(1, 1, 1), new Vector3(0, 1, 1)
                },
                // Back (-Z)
                new Vector3[]
                {
                    new Vector3(1, 0, 0), new Vector3(0, 0, 0), new Vector3(0, 1, 0), new Vector3(1, 1, 0)
                }
            };

            // Generate faces with culling
            foreach (var voxel in voxels)
            {
                Vector3Int voxelPos = new Vector3Int(voxel.x, voxel.y, voxel.z);
                Color voxelColor = palette[voxel.colorIndex - 1]; // Color index is 1-based

                // Check each face direction
                for (int faceIdx = 0; faceIdx < 6; faceIdx++)
                {
                    Vector3Int neighborPos = voxelPos + faceDirections[faceIdx];

                    // Only create face if there's no neighbor (face is exposed)
                    if (!voxelSet.Contains(neighborPos))
                    {
                        List<int> faceVertices = new List<int>();

                        // Add vertices for this face
                        for (int i = 0; i < 4; i++)
                        {
                            Vector3 vertexPos = (new Vector3(voxelPos.x, voxelPos.y, voxelPos.z) +
                                                faceVertexOffsets[faceIdx][i]) * scale;

                            // Reuse existing vertices or add new ones
                            if (!vertexMap.ContainsKey(vertexPos))
                            {
                                vertexMap[vertexPos] = vertices.Count;
                                vertices.Add(vertexPos);
                            }

                            faceVertices.Add(vertexMap[vertexPos]);
                        }

                        faces.Add(faceVertices);
                        faceProperties.Add(new FaceProperties(
                            MaterialRegistry.GetMaterialIdClosestToColor(voxelColor)));
                    }
                }
            }

            Debug.Log($"Generated optimized mesh: {vertices.Count} vertices, {faces.Count} faces");

            MMesh mesh = new MMesh(id, Vector3.zero, Quaternion.identity, vertices, faces, faceProperties);
            ApplyImportOrientationFix(mesh);
            return mesh;
        }

        /// <summary>
        /// Generate mesh with separate cube geometry for each voxel
        /// </summary>
        private static MMesh GenerateSeparateCubesMesh(int id, List<Voxel> voxels, Color[] palette, float scale)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<List<int>> faces = new List<List<int>>();
            List<FaceProperties> faceProperties = new List<FaceProperties>();

            // Cube vertex offsets (8 vertices per cube)
            Vector3[] cubeVertices = new Vector3[]
            {
                new Vector3(0, 0, 0),
                new Vector3(1, 0, 0),
                new Vector3(1, 1, 0),
                new Vector3(0, 1, 0),
                new Vector3(0, 0, 1),
                new Vector3(1, 0, 1),
                new Vector3(1, 1, 1),
                new Vector3(0, 1, 1)
            };

            // Cube faces (6 faces per cube, each with 4 vertex indices)
            int[][] cubeFaces = new int[][]
            {
                new int[] { 1, 2, 6, 5 }, // Right (+X)
                new int[] { 4, 7, 3, 0 }, // Left (-X)
                new int[] { 3, 2, 6, 7 }, // Top (+Y)
                new int[] { 0, 1, 5, 4 }, // Bottom (-Y)
                new int[] { 4, 5, 6, 7 }, // Front (+Z)
                new int[] { 1, 0, 3, 2 }  // Back (-Z)
            };

            // Generate a cube for each voxel
            foreach (var voxel in voxels)
            {
                Vector3 voxelPos = new Vector3(voxel.x, voxel.y, voxel.z) * scale;
                Color voxelColor = palette[voxel.colorIndex - 1]; // Color index is 1-based
                int baseVertexIndex = vertices.Count;

                // Add vertices for this cube
                for (int i = 0; i < cubeVertices.Length; i++)
                {
                    vertices.Add(voxelPos + cubeVertices[i] * scale);
                }

                // Add faces for this cube
                for (int i = 0; i < cubeFaces.Length; i++)
                {
                    List<int> face = new List<int>();
                    for (int j = 0; j < cubeFaces[i].Length; j++)
                    {
                        face.Add(baseVertexIndex + cubeFaces[i][j]);
                    }
                    faces.Add(face);
                    faceProperties.Add(new FaceProperties(
                        MaterialRegistry.GetMaterialIdClosestToColor(voxelColor)));
                }
            }

            Debug.Log($"Generated separate cubes mesh: {vertices.Count} vertices, {faces.Count} faces");

            MMesh mesh = new MMesh(id, Vector3.zero, Quaternion.identity, vertices, faces, faceProperties);
            ApplyImportOrientationFix(mesh);
            return mesh;
        }

        /// <summary>
        /// Get the default MagicaVoxel palette
        /// </summary>
        private static Color[] GetDefaultPalette()
        {
            // MagicaVoxel default palette (256 colors)
            // This is a simplified version - the actual default palette has specific colors
            Color[] palette = new Color[256];
            for (int i = 0; i < 256; i++)
            {
                // Simple grayscale as fallback
                float v = i / 255f;
                palette[i] = new Color(v, v, v, 1f);
            }
            return palette;
        }

        /// <summary>
        /// Apply orientation fix to match the existing importer behavior
        /// </summary>
        private static void ApplyImportOrientationFix(MMesh mesh)
        {
            if (mesh == null)
            {
                return;
            }

            Quaternion orientationFix = Quaternion.Euler(0f, 180f, 0f);
            MMesh.GeometryOperation operation = mesh.StartOperation();
            foreach (int vertexId in mesh.GetVertexIds())
            {
                Vector3 loc = mesh.VertexPositionInMeshCoords(vertexId);
                operation.ModifyVertexMeshSpace(vertexId, orientationFix * loc);
            }
            operation.Commit();
            mesh.offset = orientationFix * mesh.offset;
            mesh.RecalcBounds();
        }
    }
}
