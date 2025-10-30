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
using UnityEngine;

using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.util;
using com.google.apps.peltzer.client.model.main;
using Unity.Collections;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace com.google.apps.peltzer.client.model.render
{

    /// <summary>
    /// Should slowly replace the MeshGenContext.
    /// Stores information about where the mesh is located in the Unity mesh buffers
    /// </summary>
    public readonly struct BetterMeshGenContext
    {
        public readonly int meshId;
        public readonly int materialId;
        public readonly Color32 color;
        public readonly int overrideId; // material override id
        public readonly Color32 overrideColor;
        public readonly int transformIndex;
        public readonly int numVertices;
        public readonly int numIndices;
        public readonly int startVertexBuffer;
        public readonly int startIndexBuffer;

        public BetterMeshGenContext(int meshId, int materialId, Color32 color, int overrideId, Color32 overrideColor, int transformIndex, int numVertices, int numIndices, int startVertexBuffer, int startIndexBuffer)
        {
            this.meshId = meshId;
            this.materialId = materialId;
            this.color = color;
            this.overrideId = overrideId;
            this.overrideColor = overrideColor;
            this.transformIndex = transformIndex;
            this.numVertices = numVertices;
            this.numIndices = numIndices;
            this.startVertexBuffer = startVertexBuffer;
            this.startIndexBuffer = startIndexBuffer;
        }
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct VertexData
    {
        public Vector3 position;
        public Vector3 normal;
        public Color32 color;
        public Vector2 transformIndex;
    }

    /// <summary>
    ///   Generates, maintains and renders a collection of Unity Meshes based on a collection of MMeshes.
    ///
    ///   Multiple MMeshes can be coalesced into a single Unity Mesh.  MMeshes that have multiple
    ///   different materials (with different shaders) will be divided between multiple Unity Meshes. (Meaning that you end up with
    ///   a many-to-many relationship between MMeshes and Unity Meshes.)
    ///
    ///   Every time a MMesh is added, we retrieve its mesh data.  We then add it to a Unity Mesh of the same material.  When that mesh is "full" (in 
    ///   this case, has MAX_VERTS_PER_MESH vertices), we create a new Unity Mesh for that material.
    /// </summary>
    /// TODO(bug): Support meshes with more than 65k verts.
    public class ReMesher
    {
        private static readonly int RemesherMeshTransforms = Shader.PropertyToID("_RemesherMeshTransforms");

        // Maximum number of vertices we'd put into a coalesced Mesh.
        public const int MAX_VERTS_PER_MESH = 20000;

        // Maximum number of vertices we allow for any context.
        private const int MAX_VERTS_PER_CONTEXT = 20000;

        // Maximum number of MMesh contexts in a single MeshInfo.
        private const int MAX_CONTEXTS_PER_MESHINFO = 128;

        // For each MMesh, the set of MeshInfos that MMesh contributes triangles to.
        private Dictionary<int, HashSet<MeshInfo>> meshInfosByMesh = new();

        // All MeshInfos that we need to render.
        private HashSet<MeshInfo> allMeshInfos = new();

        // IDs of meshes that we have yet to add-to/remove-from the ReMesher, and haven't gotten around to doing yet.
        // We only add when ReMesher.Flush() is called so that a bunch of those operations can be batched.
        private HashSet<int> meshesPendingAdd = new();
        public HashSet<int> meshesPendingRemove = new();
        private HashSet<MeshInfo> meshInfosPendingRegeneration = new();
        // When hovering over a mesh, depending on the tool, we might want to render the mesh with a specific material.
        private Dictionary<int, int> meshIdToMaterialId = new();


        /// <summary>
        ///   Info about a Unity Mesh that will be drawn at render time.  MeshInfo batches a number of MMeshes together, and
        ///   renders them in a single draw call, passing an array of transform matrix uniforms to position them correctly.
        /// </summary>
        public class MeshInfo
        {
            // The Unity Mesh, itself.
            public Mesh mesh = new();

            // stores simplified contexts for each MMesh so we know where they are located in the Unity mesh buffers
            // if an MMesh has multiple contexts they should be after each other in the buffer as they should be added at the same time!
            public List<BetterMeshGenContext> mMeshContexts = new();

            // The material we draw this mesh with.
            public MaterialAndColor materialAndColor;

            // because the transform of an individual MMesh is passed as a uniform to the shader, we can't
            // use the mesh bounds for culling. Instead of recalculating the bounds every time we add a mesh
            // or someone scales the scene, etc., we just set the bounds to something big.
            // (This is how the initial implementation handled it too.)
            private Bounds bounds = new Bounds(new Vector3(0f, 0f, 0f), new Vector3(999999f, 999999f, 999999f));

            // layout of our vertex buffer
            private VertexAttributeDescriptor[] layout = {
                new(VertexAttribute.Position), // default is float32 x3
                new(VertexAttribute.Normal),   // default is float32 x3
                new(VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
                new(VertexAttribute.TexCoord2, VertexAttributeFormat.Float32, 2)
            };

            private MeshUpdateFlags updateFlags = MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices;

            public int numVerts = 0;
            public int numTriangles = 0;

            // Transform uniforms for each context which are passed to the shader.
            private Matrix4x4[] xformMats = new Matrix4x4[MAX_CONTEXTS_PER_MESHINFO];
            private Stack<int> freeXformMatIndices = new(MAX_CONTEXTS_PER_MESHINFO);

            /// <summary>
            /// Gets the mesh ids of all meshes in this MeshInfo (only used for debugging)
            /// </summary>
            public List<int> GetMeshIds()
            {
                List<int> mmeshes = new List<int>();
                int previousId = -1;
                for (int i = 0; i < mMeshContexts.Count; i++)
                {
                    if (previousId != mMeshContexts[i].meshId)
                    {
                        mmeshes.Add(mMeshContexts[i].meshId);
                    }
                    previousId = mMeshContexts[i].meshId;
                }
                return mmeshes;
            }

            /// <summary>
            /// Gets the number of meshes in this MeshInfo
            /// </summary>
            public int GetNumContexts()
            {
                return mMeshContexts.Count;
            }

            public bool HasSpace(int vertexCount, int indexCount)
            {
                return (numVerts + vertexCount <= MAX_VERTS_PER_MESH) && (numTriangles + indexCount <= 3 * MAX_VERTS_PER_MESH) && (mMeshContexts.Count < MAX_CONTEXTS_PER_MESHINFO);
            }

            /// <summary>
            ///   Builds a Unity Mesh from the given MeshInfo that has correct (in world-space) vertex positions, and does 
            ///   not have any hacks or optimizations that ReMesher relies upon, for export.
            /// </summary>
            public static Mesh BuildExportableMeshFromMeshInfo(MeshInfo meshInfo)
            {
                Mesh exportableMesh = new Mesh();

                using (var meshData = Mesh.AcquireReadOnlyMeshData(meshInfo.mesh))
                {
                    var vertexData = meshData[0].GetVertexData<VertexData>();
                    var tempVertexData = new NativeArray<VertexData>(vertexData.Length, Allocator.Temp);
                    var indexData = meshData[0].GetIndexData<short>();

                    exportableMesh.SetVertexBufferParams(vertexData.Length, meshInfo.layout);
                    exportableMesh.SetIndexBufferParams(indexData.Length, IndexFormat.UInt16);

                    // Put vertices into world space.
                    for (int i = 0; i < vertexData.Length; i++)
                    {
                        int transformIndex = (int)vertexData[i].transformIndex.x;
                        tempVertexData[i] = new VertexData()
                        {
                            position = meshInfo.xformMats[transformIndex].MultiplyPoint(vertexData[i].position),
                            normal = meshInfo.xformMats[transformIndex].MultiplyVector(vertexData[i].normal).normalized, // TODO is that right?
                            color = vertexData[i].color,
                            transformIndex = vertexData[i].transformIndex
                        };
                    }

                    exportableMesh.SetVertexBufferData(vertexData, 0, 0, vertexData.Length, flags: meshInfo.updateFlags);
                    exportableMesh.SetIndexBufferData(indexData, 0, 0, indexData.Length, flags: meshInfo.updateFlags);
                    exportableMesh.subMeshCount = 1;
                    var desc = new SubMeshDescriptor(0, indexData.Length, MeshTopology.Triangles)
                    {
                        firstVertex = 0,
                        vertexCount = vertexData.Length
                    };
                    exportableMesh.SetSubMesh(0, desc);
                    exportableMesh.RecalculateNormals();
                }

                return exportableMesh;
            }

            public bool ContainsMesh(int meshId)
            {
                for (int i = 0; i < mMeshContexts.Count; i++)
                {
                    if (mMeshContexts[i].meshId == meshId)
                    {
                        return true;
                    }
                }
                return false;
            }

            /// <summary>
            /// When meshes are removed from this MeshInfo, we update the mesh buffers and move data in
            /// vertex and index buffers to account for the removed data.
            /// We iterate through all our contexts which should be in the order they were added to the mesh.
            /// We keep track of chunks of data inbetween removed contexts and offset those chunks once we know how big they are,
            /// to close the gaps left by removed contexts.
            ///
            /// // Sadly, we need to offset each index individually otherwise they'll point to the wrong vertex data.
            /// TODO: Use jobs in the future and this should not be a problem.
            /// TODO: Later, instead of copying data around, we could set the indices of removed contexts to 0 which makes them invisible.
            /// Then we can do a combined cleanup of the mesh buffers every couple of seconds.
            /// </summary>
            public void UpdateMeshBuffer(HashSet<int> meshesPendingRemove)
            {
                int offsetVB = 0; // the offset we need to apply to the start indices of later contexts after previous ones were removed
                int offsetIB = 0; // same as above for index buffers
                int startChunkVB = 0; // the start of a chunk that we need to offset after deleting contexts
                int endChunkVB = 0; // the end of the chunk to offset (which can also be the start of a new context to be removed)
                int startChunkIB = 0; // same as above but for index buffers
                int endChunkIB = 0; // same as above but for index buffers
                int listOffset = 0; // we shift our contexts in the list as we go along and remove the flagged ones

                BetterMeshGenContext previousContext = default;

                NativeArray<VertexData> newVertexData;
                NativeArray<short> newIndexData;
                using (var meshData = Mesh.AcquireReadOnlyMeshData(mesh))
                {
                    var vertexData = meshData[0].GetVertexData<VertexData>();
                    var indexData = meshData[0].GetIndexData<short>();

                    newVertexData = new NativeArray<VertexData>(vertexData.Length, Allocator.Temp);
                    newIndexData = new NativeArray<short>(indexData.Length, Allocator.Temp);
                    vertexData.CopyTo(newVertexData);
                    indexData.CopyTo(newIndexData);

                    for (int i = 0; i < mMeshContexts.Count; i++)
                    {
                        var context = mMeshContexts[i];

                        // check if flagged for removal
                        if (meshesPendingRemove.Contains(context.meshId))
                        {

                            listOffset += 1;
                            freeXformMatIndices.Push(context.transformIndex); // free up the transform index

                            // this is the first context to be removed any data before that we can leave as is
                            // we start counting where the first chunk to copy should start
                            if (startChunkVB == 0)
                            {
                                startChunkVB = context.startVertexBuffer + context.numVertices;
                                startChunkIB = context.startIndexBuffer + context.numIndices;
                            }
                            else
                            {
                                if (!meshesPendingRemove.Contains(previousContext.meshId)) // previous context should never be null as the first removed context is handled above
                                {
                                    // if the previous context was a part of the chunk we need to offset, we know that with the current context
                                    // we ended a chunk and can now offset it before computing the start of the next chunk
                                    endChunkVB = context.startVertexBuffer; // exclusive
                                    endChunkIB = context.startIndexBuffer;

                                    NativeArray<VertexData>.Copy(vertexData, startChunkVB, newVertexData, startChunkVB - offsetVB, endChunkVB - startChunkVB);
                                    // NativeArray<short>.Copy(indexData, startChunkIB, newIndexData, startChunkIB - offsetIB, endChunkIB - startChunkIB);
                                    for (int j = 0; j < endChunkIB - startChunkIB; j++)
                                    {
                                        newIndexData[startChunkIB - offsetIB + j] = (short)(indexData[startChunkIB + j] - offsetVB);
                                    }

                                    startChunkVB = context.startVertexBuffer + context.numVertices;
                                    startChunkIB = context.startIndexBuffer + context.numIndices;
                                }
                                else
                                {
                                    // if we keep going and we still have a context to remove, update the start of the chunk
                                    // we will end up here as long as there are multiple contexts to remove after each other
                                    startChunkVB += context.numVertices;
                                    startChunkIB += context.numIndices;
                                }
                            }
                            offsetVB += context.numVertices;
                            offsetIB += context.numIndices;
                        }
                        else
                        {
                            // we add the context with updated buffer offsets in the list accounting for the removed contexts before
                            mMeshContexts[i - listOffset] = new BetterMeshGenContext(
                                context.meshId, context.materialId, context.color, context.overrideId, context.overrideColor,
                                context.transformIndex, context.numVertices, context.numIndices,
                                context.startVertexBuffer - offsetVB, context.startIndexBuffer - offsetIB);
                        }
                        previousContext = context;
                    }

                    // after we went through all contexts, we might still have a chunk to offset at the end of the buffers
                    if (startChunkVB < numVerts)
                    {
                        NativeArray<VertexData>.Copy(vertexData, startChunkVB, newVertexData, startChunkVB - offsetVB, numVerts - startChunkVB);
                        // NativeArray<short>.Copy(indexData, startChunkIB, newIndexData, startChunkIB - offsetIB, numTriangles - startChunkIB);
                        for (int j = 0; j < numTriangles - startChunkIB; j++)
                        {
                            newIndexData[startChunkIB - offsetIB + j] = (short)(indexData[startChunkIB + j] - offsetVB);
                        }
                    }
                } // end of "using"-block for read-only meshData

                // after we moved all remaining contexts up in the list we trim the end 
                var from = mMeshContexts.Count - 1;
                var to = mMeshContexts.Count - listOffset;
                for (int i = from; i >= to; i--)
                {
                    mMeshContexts.RemoveAt(i);
                }

                numVerts -= offsetVB;
                numTriangles -= offsetIB;

                mesh.SetVertexBufferData(newVertexData, 0, 0, numVerts, flags: updateFlags);
                mesh.SetIndexBufferData(newIndexData, 0, 0, numTriangles, flags: updateFlags);

                var desc = new SubMeshDescriptor(0, numTriangles)
                {
                    firstVertex = 0,
                    vertexCount = numVerts
                };
                mesh.SetSubMesh(0, desc);
            }

            // for debugging only
            private void PrintContexts()
            {
                string s = "";
                for (int i = 0; i < mMeshContexts.Count; i++)
                {
                    s += "[" + i + "] id: " + mMeshContexts[i].meshId + "trafoIndex: " + mMeshContexts[i].transformIndex + " startVB: " + mMeshContexts[i].startVertexBuffer + " numVB: " + mMeshContexts[i].numVertices +
                         " startIB: " + mMeshContexts[i].startIndexBuffer + " numIB: " + mMeshContexts[i].numIndices + "\n";
                }
                Debug.Log(s);
            }

            /// <summary>
            ///   Add vertices and triangles to a MeshInfo.  This method assumes we've ensured there is "room" in the
            ///   MeshInfo for the new components.
            /// </summary>
            /// <param name="meshId">The id of the mesh whose context we are adding.</param>
            /// <param name="source">The MehGenContext whose data we're adding to this MeshInfo. TODO should be replaced by BetterMeshGenContext in future.</param>
            public void AddContext(int meshId, MeshGenContext source, Color32 color, bool overrideColor = false)
            {
                int transformIndex = freeXformMatIndices.Pop();
                var betterContext = new BetterMeshGenContext(
                    meshId, materialAndColor.matId, source.colors[0], -1, source.colors[0],
                    transformIndex, source.verts.Count, source.triangles.Count,
                    numVerts, numTriangles);

                var tempVertexBuffer = new NativeArray<VertexData>(source.verts.Count, Allocator.Temp);
                var tempIndexBuffer = new NativeArray<short>(source.triangles.Count, Allocator.Temp);

                Color32 col = (materialAndColor.matId < MaterialRegistry.rawColors.Length) ? (overrideColor ? color : source.colors[0]) : source.colors[0];

                for (int i = 0; i < source.verts.Count; i++)
                {
                    tempVertexBuffer[i] = new VertexData()
                    {
                        position = source.verts[i],
                        normal = source.normals[i],
                        color = col,
                        transformIndex = new Vector2(transformIndex, 0)
                    };
                }

                for (int i = 0; i < source.triangles.Count; i++)
                {
                    tempIndexBuffer[i] = (short)(source.triangles[i] + numVerts);
                }
                mesh.SetVertexBufferData(tempVertexBuffer, 0, numVerts, tempVertexBuffer.Length, flags: updateFlags);
                mesh.SetIndexBufferData(tempIndexBuffer, 0, numTriangles, tempIndexBuffer.Length, flags: updateFlags);

                numVerts += tempVertexBuffer.Length;
                numTriangles += tempIndexBuffer.Length;

                var desc = new SubMeshDescriptor(0, numTriangles)
                {
                    firstVertex = 0,
                    vertexCount = numVerts
                };
                mesh.SetSubMesh(0, desc);

                mMeshContexts.Add(betterContext);
            }

            public void AddContext(int meshId, int materialId, int overrideId, NativeArray<VertexData> vertexData, NativeArray<short> indexData, int indexOffset, Color32 color, Color32 overrideColor, bool useOverride = false)
            {
                int transformIndex = freeXformMatIndices.Pop();
                var betterContext = new BetterMeshGenContext(
                    meshId, materialId, color, overrideId, overrideColor, transformIndex, vertexData.Length,
                    indexData.Length, numVerts, numTriangles);

                var col = useOverride ? overrideColor : color;

                for (int i = 0; i < vertexData.Length; i++)
                {
                    vertexData[i] = new VertexData()
                    {
                        position = vertexData[i].position,
                        normal = vertexData[i].normal,
                        color = col,
                        transformIndex = new Vector2(transformIndex, 0)
                    };
                }

                for (int i = 0; i < indexData.Length; i++)
                {
                    indexData[i] = (short)(indexData[i] - indexOffset + numVerts);
                }
                mesh.SetVertexBufferData(vertexData, 0, numVerts, vertexData.Length, flags: updateFlags);
                mesh.SetIndexBufferData(indexData, 0, numTriangles, indexData.Length, flags: updateFlags);

                numVerts += vertexData.Length;
                numTriangles += indexData.Length;

                var desc = new SubMeshDescriptor(0, numTriangles)
                {
                    firstVertex = 0,
                    vertexCount = numVerts
                };

                mesh.SetSubMesh(0, desc);

                mMeshContexts.Add(betterContext);
            }

            /// <summary>
            /// Updates the array of transform mats for the MMeshes this info renders.
            /// </summary>
            public void UpdateTransforms(Model model)
            {
                for (int i = 0; i < mMeshContexts.Count; i++)
                {
                    xformMats[mMeshContexts[i].transformIndex] = model.GetMesh(mMeshContexts[i].meshId).GetJitteredTransform();
                }
            }

            /// <summary>
            /// Sets the transform mat array as a shader uniform for the supplied material.
            /// </summary>
            public void SetTransforms(Material mat)
            {
                mat.SetMatrixArray(RemesherMeshTransforms, xformMats);
            }

            public MeshInfo()
            {
                mesh.SetVertexBufferParams(MAX_VERTS_PER_MESH, layout);
                mesh.SetIndexBufferParams(3 * MAX_VERTS_PER_MESH, IndexFormat.UInt16);
                mesh.subMeshCount = 1;
                mesh.bounds = bounds;

                for (int i = 0; i < MAX_CONTEXTS_PER_MESHINFO; i++)
                {
                    xformMats[i] = Matrix4x4.identity;
                    freeXformMatIndices.Push(MAX_CONTEXTS_PER_MESHINFO - 1 - i);
                }
            }
        }

        // Meshinfos should not be modified outside of remesher.
        // This exists to enable exporting coalesced meshes.
        public HashSet<MeshInfo> GetAllMeshInfos()
        {
            Flush();
            return allMeshInfos;
        }

        public HashSet<MeshInfo> GetMeshInfosForMMesh(int meshId)
        {
            return meshInfosByMesh.GetValueOrDefault(meshId);
        }

        // All MeshInfos that have room for more triangles to be added, by material.
        private Dictionary<Material, List<MeshInfo>> meshInfosByMaterial = new();

        public void Clear()
        {
            foreach (var meshInfo in allMeshInfos)
            {
                Object.Destroy(meshInfo.mesh);
                Object.Destroy(meshInfo.materialAndColor.material);
            }
            allMeshInfos.Clear();
            meshInfosByMaterial.Clear();
            meshInfosByMesh.Clear();
            meshesPendingAdd.Clear();
            meshInfosPendingRegeneration.Clear();
        }

        /// <summary>
        ///   Add a mesh to be rendered.  A mesh with the same id must exist in the model.
        /// </summary>
        /// <param name="mmesh">The mesh.</param>
        public void Add(MMesh mmesh)
        {
            // Generate or fetch the triangles, etc for the mesh.
            // TODO(bug): This only works because Model.cs happens to update the model before calling the ReMesher.
            //                   We shoud make that more robust.
            // Generate the Unity meshes for this mesh.
            // TODO(bug): We should cache these Meshes for MMeshes too, if possible.
            meshesPendingAdd.Add(mmesh.id);
        }

        public void Add(int meshId)
        {
            meshesPendingAdd.Add(meshId);
        }

        public void Add(int meshId, int materialId)
        {
            meshesPendingAdd.Add(meshId);
            meshIdToMaterialId[meshId] = materialId;
        }

        public void Update(int meshId)
        {
            if (Contains(meshId))
            {
                Remove(meshId);
                Add(meshId);
            }
        }

        public void Update(int meshId, int materialId)
        {
            if (Contains(meshId))
            {
                Remove(meshId);
                Add(meshId, materialId);
            }
        }

        /// <summary>
        /// Flushes any pending deferred operations on the ReMesher.
        /// </summary>
        public void Flush()
        {
            ActuallyRemoveMeshes();
            GenerateMeshesForMMeshes();
        }

        /// <summary>
        ///   Marks a mesh to be removed from being rendered. 
        ///   Actual removal will happen in batch the next time Flush is called.
        /// </summary>
        /// <param name="meshId">The mesh id.</param>
        public bool Remove(int meshId)
        {
            meshesPendingAdd.Remove(meshId);
            if (!meshInfosByMesh.TryGetValue(meshId, out var meshInfos)) return false;

            foreach (var info in meshInfos)
            {
                meshesPendingRemove.Add(meshId);
                meshInfosPendingRegeneration.Add(info);
            }
            meshInfosByMesh.Remove(meshId);
            return true;
        }

        /// <summary>
        ///   Removes the given meshes from ReMesher immediately.
        /// </summary>
        private void ActuallyRemoveMeshes()
        {
            foreach (var meshInfo in meshInfosPendingRegeneration)
            {
                meshInfo.UpdateMeshBuffer(meshesPendingRemove);
            }
            meshInfosPendingRegeneration.Clear();
            meshesPendingRemove.Clear();
        }

        /// <summary>
        ///   For a list of MMeshes, add their triangles to "unfull" MeshInfos.  When any of those
        ///   MeshInfos becomes full, create a new MeshInfo.
        /// </summary>
        /// <param name="mmeshIds"></param>
        private void GenerateMeshesForMMeshes()
        {
            Model model = PeltzerMain.Instance.model;
            HashSet<int> meshesStillPendingAdd = new HashSet<int>();
            foreach (int meshId in meshesPendingAdd)
            {
                // Since this method is called lazily on Flush(), we may have an out of date mesh ID that no longer
                // exists in the model. In that case, skip it.
                if (!model.HasMesh(meshId)) continue;

                var mMesh = model.GetMesh(meshId);

                var useMaterialOverride = meshIdToMaterialId.TryGetValue(meshId, out var materialId);

                Dictionary<int, MeshGenContext> components =
                MeshHelper.MeshComponentsFromMMesh(mMesh, false);
                if (components == null)
                {
                    meshesStillPendingAdd.Add(meshId);
                    continue;
                }

                HashSet<MeshInfo> meshInfos = new HashSet<MeshInfo>();
                foreach (KeyValuePair<int, MeshGenContext> pair in components)
                {
                    // Doing the Assert within an if statement to prevent the string concatenation from occurring unless the
                    // condition has failed.  The concatenation was expensive enough to show up in profiling for large models.
                    if (pair.Value.verts.Count >= MAX_VERTS_PER_CONTEXT)
                    {
                        AssertOrThrow.True(pair.Value.verts.Count < MAX_VERTS_PER_CONTEXT,
                          "MMesh has too many vertices ( " + pair.Value.verts.Count + " vs a max of " + MAX_VERTS_PER_CONTEXT);
                    }
                    MeshInfo infoForMaterial;
                    // Find or create an unfull MeshInfo for the given material
                    if (useMaterialOverride)
                    {
                        infoForMaterial = GetInfoForMaterialAndVertCount(materialId, pair.Value.verts.Count, pair.Value.triangles.Count);
                        meshIdToMaterialId.Remove(meshId);
                        // if we are not using a raw color material for overriding, use the original color (e.g. for highlighting)
                        var overrideColor = MaterialRegistry.rawColors.Length < materialId ? pair.Value.colors[0] : MaterialRegistry.GetMaterialColor32ById(materialId);
                        infoForMaterial.AddContext(mMesh.id, pair.Value, overrideColor, true);
                    }
                    else
                    {
                        infoForMaterial = GetInfoForMaterialAndVertCount(pair.Key, pair.Value.verts.Count, pair.Value.triangles.Count);
                        infoForMaterial.AddContext(mMesh.id, pair.Value, pair.Value.colors[0]);
                    }


                    meshInfos.Add(infoForMaterial);
                }
                meshInfosByMesh[meshId] = meshInfos;
            }

            meshesPendingAdd = meshesStillPendingAdd;
        }

        /// <summary>
        /// Gets a MeshInfo with sufficient space for the given material, or creates a new one if none currently exists.
        /// </summary>
        private MeshInfo GetInfoForMaterialAndVertCount(int materialId, int spaceNeededVerts, int spaceNeededTris)
        {
            List<MeshInfo> infosForMaterial;
            MaterialAndColor materialAndColor = MaterialRegistry.GetMaterialAndColorById(materialId);
            meshInfosByMaterial.TryGetValue(materialAndColor.material, out infosForMaterial);
            if (infosForMaterial == null)
            {
                infosForMaterial = new List<MeshInfo>();
                meshInfosByMaterial.Add(materialAndColor.material, infosForMaterial);
            }
            // Just return the first info with room.
            for (int i = 0; i < infosForMaterial.Count; i++)
            {
                MeshInfo curInfo = infosForMaterial[i];
                if (curInfo.numVerts + spaceNeededVerts < MAX_VERTS_PER_MESH
                    && curInfo.numTriangles + spaceNeededTris < 3 * MAX_VERTS_PER_MESH
                  && curInfo.GetNumContexts() + 1 < MAX_CONTEXTS_PER_MESHINFO)
                {
                    return curInfo;
                }
            }
            // And create one if no viable option was found.
            MeshInfo newInfoForMaterial = new MeshInfo();
            // Cloned to make sure it has its own matrix transform uniform, otherwise other things rendering using the
            // same material will have the wrong transforms.
            newInfoForMaterial.materialAndColor = materialAndColor.Clone();
            allMeshInfos.Add(newInfoForMaterial);
            meshInfosByMaterial[materialAndColor.material].Add(newInfoForMaterial);
            return newInfoForMaterial;
        }

        /// <summary>
        ///   Render the meshes.
        /// </summary>
        public void Render(Model model)
        {
            // Flush to apply any outstanding changes, if necessary.
            Flush();

            WorldSpace worldSpace = PeltzerMain.Instance.worldSpace;

            foreach (MeshInfo meshInfo in allMeshInfos)
            {
                meshInfo.UpdateTransforms(model);
                meshInfo.SetTransforms(meshInfo.materialAndColor.material);
                Graphics.DrawMesh(meshInfo.mesh, worldSpace.modelToWorld, meshInfo.materialAndColor.material, /* Layer */ 0);
                // Graphics.RenderMesh(new RenderParams(meshInfo.materialAndColor.material), meshInfo.mesh, 0, worldSpace.modelToWorld);

                if (meshInfo.materialAndColor.material2)
                {
                    Matrix4x4 matrix = worldSpace.modelToWorld;
                    // when rendering the gem material we render the backfaces at a tiny offset away from the camera
                    // so they render correctly
                    if (meshInfo.materialAndColor.material2.IsKeywordEnabled("_GEM_EFFECT_BACKFACE_FIX"))
                    {
                        matrix = Matrix4x4.Translate(PeltzerMain.Instance.eyeCamera.transform.forward * 0.01f) * matrix;
                    }

                    meshInfo.SetTransforms(meshInfo.materialAndColor.material2);
                    Graphics.DrawMesh(meshInfo.mesh, matrix, meshInfo.materialAndColor.material2, /* Layer */ 0);
                    // Graphics.RenderMesh(new RenderParams(meshInfo.materialAndColor.material2), meshInfo.mesh, 0, matrix);
                }
            }
        }

        /// <summary>
        ///   Update transformations for all meshInfos in the given model.
        /// </summary>
        public void UpdateTransforms(Model model)
        {
            foreach (MeshInfo meshInfo in allMeshInfos)
            {
                meshInfo.UpdateTransforms(model);
            }
        }

        /// <summary>
        /// Checks if the remesher contains the given mesh. This can mean either that the mesh is already added to a MeshInfo,
        /// or is pending to be added. It also checks that the mesh is not pending removal, in which case it returns false.
        /// </summary> <param name="meshId"> id of the current mesh</param>
        /// <returns>checks if the mesh is in the remesher </returns>
        public bool Contains(int meshId)
        {
            return (meshesPendingAdd.Contains(meshId) || meshInfosByMesh.ContainsKey(meshId)) && !meshesPendingRemove.Contains(meshId);
        }

        // Visible for testing.  Walk all MeshInfos and count how many depend on a given mesh.
        public int MeshInMeshInfosCount(int meshId)
        {
            int count = 0;
            foreach (MeshInfo meshInfo in allMeshInfos)
            {
                if (meshInfo.ContainsMesh(meshId))
                {
                    count++;
                }
            }
            return count;
        }
    }
}
