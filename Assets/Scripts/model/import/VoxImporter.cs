// Copyright 2025 The Open Blocks Authors.
// Based on VoxReader Copyright 2024 Sandro Figo and contributors
// https://github.com/sandrofigo/VoxReader
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

            // Read version number (typically 150)
            int _version = reader.ReadInt32(); // Don't remove, needed to advance stream

            // Read MAIN chunk
            string mainId = Encoding.ASCII.GetString(reader.ReadBytes(4));
            if (mainId != "MAIN")
            {
                Debug.LogError($"Expected MAIN chunk, got: {mainId}");
                return false;
            }

            int mainContentSize = reader.ReadInt32(); // Don't remove, needed to advance stream
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

            // Face directions (normals) - ordered to match faceVertexOffsets
            Vector3Int[] faceDirections = new Vector3Int[]
            {
                new Vector3Int(-1, 0, 0),  // Left
                new Vector3Int(1, 0, 0),   // Right
                new Vector3Int(0, -1, 0),  // Bottom
                new Vector3Int(0, 1, 0),   // Top
                new Vector3Int(0, 0, -1),  // Front
                new Vector3Int(0, 0, 1)    // Back
            };

            // For each face direction, define the 4 vertices (relative to voxel position)
            // Using same winding order as Primitives.CUBE_POINTS
            // Cube vertices: 0=(0,0,0), 1=(1,0,0), 2=(0,1,0), 3=(1,1,0), 4=(0,0,1), 5=(1,0,1), 6=(0,1,1), 7=(1,1,1)
            // CUBE_POINTS: left={0,4,6,2}, right={1,3,7,5}, bottom={0,1,5,4}, top={2,6,7,3}, front={0,2,3,1}, back={4,5,7,6}
            Vector3[][] faceVertexOffsets = new Vector3[][]
            {
                // Left (-X): {0,4,6,2}
                new Vector3[]
                {
                    new Vector3(0, 0, 0), new Vector3(0, 0, 1), new Vector3(0, 1, 1), new Vector3(0, 1, 0)
                },
                // Right (+X): {1,3,7,5}
                new Vector3[]
                {
                    new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(1, 1, 1), new Vector3(1, 0, 1)
                },
                // Bottom (-Y): {0,1,5,4}
                new Vector3[]
                {
                    new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 0, 1), new Vector3(0, 0, 1)
                },
                // Top (+Y): {2,6,7,3}
                new Vector3[]
                {
                    new Vector3(0, 1, 0), new Vector3(0, 1, 1), new Vector3(1, 1, 1), new Vector3(1, 1, 0)
                },
                // Front (-Z): {0,2,3,1}
                new Vector3[]
                {
                    new Vector3(0, 0, 0), new Vector3(0, 1, 0), new Vector3(1, 1, 0), new Vector3(1, 0, 0)
                },
                // Back (+Z): {4,5,7,6}
                new Vector3[]
                {
                    new Vector3(0, 0, 1), new Vector3(1, 0, 1), new Vector3(1, 1, 1), new Vector3(0, 1, 1)
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

            // Cube vertex offsets (8 vertices per cube) - matching Primitives.cs vertex ordering
            // 0=(0,0,0), 1=(1,0,0), 2=(0,1,0), 3=(1,1,0), 4=(0,0,1), 5=(1,0,1), 6=(0,1,1), 7=(1,1,1)
            Vector3[] cubeVertices = new Vector3[]
            {
                new Vector3(0, 0, 0),  // 0
                new Vector3(1, 0, 0),  // 1
                new Vector3(0, 1, 0),  // 2
                new Vector3(1, 1, 0),  // 3
                new Vector3(0, 0, 1),  // 4
                new Vector3(1, 0, 1),  // 5
                new Vector3(0, 1, 1),  // 6
                new Vector3(1, 1, 1)   // 7
            };

            // Cube faces matching Primitives.CUBE_POINTS exactly
            int[][] cubeFaces = new int[][]
            {
                new int[] { 0, 4, 6, 2 },  // Left (-X)
                new int[] { 1, 3, 7, 5 },  // Right (+X)
                new int[] { 0, 1, 5, 4 },  // Bottom (-Y)
                new int[] { 2, 6, 7, 3 },  // Top (+Y)
                new int[] { 0, 2, 3, 1 },  // Front (-Z)
                new int[] { 4, 5, 7, 6 }   // Back (+Z)
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
            // Extracted from the official MagicaVoxel default palette
            uint[] hexColors = new uint[] {
                0xffffffff, 0xffccffff, 0xff99ffff, 0xff66ffff, 0xff33ffff, 0xff00ffff, 0xffffccff, 0xffccccff,
                0xff99ccff, 0xff66ccff, 0xff33ccff, 0xff00ccff, 0xffff99ff, 0xffcc99ff, 0xff9999ff, 0xff6699ff,
                0xff3399ff, 0xff0099ff, 0xffff66ff, 0xffcc66ff, 0xff9966ff, 0xff6666ff, 0xff3366ff, 0xff0066ff,
                0xffff33ff, 0xffcc33ff, 0xff9933ff, 0xff6633ff, 0xff3333ff, 0xff0033ff, 0xffff00ff, 0xffcc00ff,
                0xff9900ff, 0xff6600ff, 0xff3300ff, 0xff0000ff, 0xffffffcc, 0xffccffcc, 0xff99ffcc, 0xff66ffcc,
                0xff33ffcc, 0xff00ffcc, 0xffffcccc, 0xffcccccc, 0xff99cccc, 0xff66cccc, 0xff33cccc, 0xff00cccc,
                0xffff99cc, 0xffcc99cc, 0xff9999cc, 0xff6699cc, 0xff3399cc, 0xff0099cc, 0xffff66cc, 0xffcc66cc,
                0xff9966cc, 0xff6666cc, 0xff3366cc, 0xff0066cc, 0xffff33cc, 0xffcc33cc, 0xff9933cc, 0xff6633cc,
                0xff3333cc, 0xff0033cc, 0xffff00cc, 0xffcc00cc, 0xff9900cc, 0xff6600cc, 0xff3300cc, 0xff0000cc,
                0xffffff99, 0xffccff99, 0xff99ff99, 0xff66ff99, 0xff33ff99, 0xff00ff99, 0xffffcc99, 0xffcccc99,
                0xff99cc99, 0xff66cc99, 0xff33cc99, 0xff00cc99, 0xffff9999, 0xffcc9999, 0xff999999, 0xff669999,
                0xff339999, 0xff009999, 0xffff6699, 0xffcc6699, 0xff996699, 0xff666699, 0xff336699, 0xff006699,
                0xffff3399, 0xffcc3399, 0xff993399, 0xff663399, 0xff333399, 0xff003399, 0xffff0099, 0xffcc0099,
                0xff990099, 0xff660099, 0xff330099, 0xff000099, 0xffffff66, 0xffccff66, 0xff99ff66, 0xff66ff66,
                0xff33ff66, 0xff00ff66, 0xffffcc66, 0xffcccc66, 0xff99cc66, 0xff66cc66, 0xff33cc66, 0xff00cc66,
                0xffff9966, 0xffcc9966, 0xff999966, 0xff669966, 0xff339966, 0xff009966, 0xffff6666, 0xffcc6666,
                0xff996666, 0xff666666, 0xff336666, 0xff006666, 0xffff3366, 0xffcc3366, 0xff993366, 0xff663366,
                0xff333366, 0xff003366, 0xffff0066, 0xffcc0066, 0xff990066, 0xff660066, 0xff330066, 0xff000066,
                0xffffff33, 0xffccff33, 0xff99ff33, 0xff66ff33, 0xff33ff33, 0xff00ff33, 0xffffcc33, 0xffcccc33,
                0xff99cc33, 0xff66cc33, 0xff33cc33, 0xff00cc33, 0xffff9933, 0xffcc9933, 0xff999933, 0xff669933,
                0xff339933, 0xff009933, 0xffff6633, 0xffcc6633, 0xff996633, 0xff666633, 0xff336633, 0xff006633,
                0xffff3333, 0xffcc3333, 0xff993333, 0xff663333, 0xff333333, 0xff003333, 0xffff0033, 0xffcc0033,
                0xff990033, 0xff660033, 0xff330033, 0xff000033, 0xffffff00, 0xffccff00, 0xff99ff00, 0xff66ff00,
                0xff33ff00, 0xff00ff00, 0xffffcc00, 0xffcccc00, 0xff99cc00, 0xff66cc00, 0xff33cc00, 0xff00cc00,
                0xffff9900, 0xffcc9900, 0xff999900, 0xff669900, 0xff339900, 0xff009900, 0xffff6600, 0xffcc6600,
                0xff996600, 0xff666600, 0xff336600, 0xff006600, 0xffff3300, 0xffcc3300, 0xff993300, 0xff663300,
                0xff333300, 0xff003300, 0xffff0000, 0xffcc0000, 0xff990000, 0xff660000, 0xff330000, 0xff0000ee,
                0xff0000dd, 0xff0000bb, 0xff0000aa, 0xff000088, 0xff000077, 0xff000055, 0xff000044, 0xff000022,
                0xff000011, 0xff00ee00, 0xff00dd00, 0xff00bb00, 0xff00aa00, 0xff008800, 0xff007700, 0xff005500,
                0xff004400, 0xff002200, 0xff001100, 0xffee0000, 0xffdd0000, 0xffbb0000, 0xffaa0000, 0xff880000,
                0xff770000, 0xff550000, 0xff440000, 0xff220000, 0xff110000, 0xffeeeeee, 0xffdddddd, 0xffbbbbbb,
                0xffaaaaaa, 0xff888888, 0xff777777, 0xff555555, 0xff444444, 0xff222222, 0xff111111, 0xff000000
            };

            Color[] palette = new Color[256];
            for (int i = 0; i < 256; i++)
            {
                uint c = hexColors[i];
                palette[i] = new Color(
                    ((c >> 16) & 0xFF) / 255f,  // R
                    ((c >> 8) & 0xFF) / 255f,   // G
                    (c & 0xFF) / 255f,          // B
                    ((c >> 24) & 0xFF) / 255f   // A
                );
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
