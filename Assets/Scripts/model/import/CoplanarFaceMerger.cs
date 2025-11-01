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

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;

using com.google.apps.peltzer.client.model.core;

namespace com.google.apps.peltzer.client.model.import
{
    /// <summary>
    ///   Utility for merging adjacent coplanar faces into ngons when importing meshes.
    /// </summary>
    public static class CoplanarFaceMerger
    {
        private const float NORMAL_DOT_THRESHOLD = 0.9995f;
        private const float PLANE_DISTANCE_TOLERANCE = 0.0005f;
        private const float VERTEX_WELD_TOLERANCE = 0.0005f;

        public static void MergeCoplanarFaces(MMesh mesh, bool requireConvexResult = false)
        {
            if (mesh == null || mesh.faceCount == 0)
            {
                return;
            }

            WeldVertices(mesh);

            Dictionary<int, HashSet<int>> adjacency = BuildAdjacency(mesh);
            if (adjacency.Count == 0)
            {
                return;
            }

            HashSet<int> processedFaces = new HashSet<int>();
            HashSet<int> removedFaces = new HashSet<int>();
            List<int> faceIds = mesh.GetFaceIds().ToList();

            MMesh.GeometryOperation operation = mesh.StartOperation();
            bool mergedAny = false;

            foreach (int faceId in faceIds)
            {
                if (processedFaces.Contains(faceId) || removedFaces.Contains(faceId))
                {
                    continue;
                }

                Face seedFace;
                if (!mesh.TryGetFace(faceId, out seedFace))
                {
                    continue;
                }

                if (seedFace.vertexIds.Count < 3)
                {
                    processedFaces.Add(faceId);
                    continue;
                }

                Vector3 seedNormal = seedFace.normal;
                if (seedNormal == Vector3.zero)
                {
                    processedFaces.Add(faceId);
                    continue;
                }

                Vector3 normalizedNormal = seedNormal.normalized;
                Vector3 planePoint = mesh.VertexPositionInMeshCoords(seedFace.vertexIds[0]);
                Plane plane = new Plane(normalizedNormal, planePoint);

                Dictionary<int, Color32> regionVertexColors = BuildInitialVertexColorMap(seedFace);
                List<int> regionList = new List<int>();
                HashSet<int> region = CollectCoplanarRegion(mesh, faceId, seedFace.properties.materialId,
                    plane, normalizedNormal, adjacency, removedFaces, regionVertexColors, regionList);

                processedFaces.UnionWith(region);

                if (region.Count <= 1)
                {
                    continue;
                }

                // Try to build a merged polygon, iteratively removing the last face added if it fails
                // This handles O-shaped rings by breaking them into C-shapes
                HashSet<int> facesToMerge = null;
                List<int> mergedLoop = null;
                for (int attemptSize = regionList.Count; attemptSize >= 2; attemptSize--)
                {
                    facesToMerge = new HashSet<int>(regionList.GetRange(0, attemptSize));
                    if (TryBuildMergedPolygon(mesh, facesToMerge, normalizedNormal, requireConvexResult,
                            out mergedLoop))
                    {
                        break;
                    }
                    facesToMerge = null;
                }

                if (facesToMerge == null || mergedLoop == null)
                {
                    continue;
                }

                foreach (int regionFaceId in facesToMerge)
                {
                    operation.DeleteFace(regionFaceId);
                    removedFaces.Add(regionFaceId);
                }

                operation.AddFace(mergedLoop, seedFace.properties);
                mergedAny = true;
            }

            if (mergedAny)
            {
                operation.Commit();
                mesh.RecalcBounds();
            }
            else
            {
                operation.CommitWithoutRecalculation();
            }
        }

        private static void WeldVertices(MMesh mesh)
        {
            List<int> vertexIds = mesh.GetVertexIds().ToList();
            if (vertexIds.Count < 2)
            {
                return;
            }

            Dictionary<SpatialHashKey, List<int>> buckets = new Dictionary<SpatialHashKey, List<int>>();
            Dictionary<int, int> remap = new Dictionary<int, int>(vertexIds.Count);
            float toleranceSquared = VERTEX_WELD_TOLERANCE * VERTEX_WELD_TOLERANCE;
            bool changed = false;

            foreach (int vertexId in vertexIds)
            {
                Vertex vertex = mesh.GetVertex(vertexId);
                Vector3 position = vertex.loc;
                SpatialHashKey baseKey = SpatialHashKey.FromPosition(position, VERTEX_WELD_TOLERANCE);

                int representative = FindRepresentativeForPosition(mesh, position, baseKey, buckets, toleranceSquared);
                if (representative == -1)
                {
                    remap[vertexId] = vertexId;
                    AddVertexToBucket(vertexId, baseKey, buckets);
                }
                else
                {
                    remap[vertexId] = representative;
                    changed = true;
                }
            }

            if (!changed)
            {
                return;
            }

            ApplyVertexRemap(mesh, remap);
        }

        private static int FindRepresentativeForPosition(MMesh mesh, Vector3 position, SpatialHashKey baseKey,
            Dictionary<SpatialHashKey, List<int>> buckets, float toleranceSquared)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        SpatialHashKey key = new SpatialHashKey(baseKey.x + dx, baseKey.y + dy, baseKey.z + dz);
                        List<int> bucket;
                        if (!buckets.TryGetValue(key, out bucket))
                        {
                            continue;
                        }

                        for (int i = 0; i < bucket.Count; i++)
                        {
                            int candidateId = bucket[i];
                            Vector3 candidatePosition = mesh.GetVertex(candidateId).loc;
                            if ((candidatePosition - position).sqrMagnitude <= toleranceSquared)
                            {
                                return candidateId;
                            }
                        }
                    }
                }
            }

            return -1;
        }

        private static void AddVertexToBucket(int vertexId, SpatialHashKey key, Dictionary<SpatialHashKey, List<int>> buckets)
        {
            List<int> bucket;
            if (!buckets.TryGetValue(key, out bucket))
            {
                bucket = new List<int>();
                buckets[key] = bucket;
            }

            bucket.Add(vertexId);
        }

        private static void ApplyVertexRemap(MMesh mesh, Dictionary<int, int> remap)
        {
            mesh.RecalcReverseTable();

            HashSet<int> facesToModify = new HashSet<int>();
            HashSet<int> verticesToDelete = new HashSet<int>();

            foreach (KeyValuePair<int, int> pair in remap)
            {
                if (pair.Key == pair.Value)
                {
                    continue;
                }

                HashSet<int> facesUsingVertex;
                if (mesh.reverseTable.TryGetValue(pair.Key, out facesUsingVertex))
                {
                    foreach (int faceId in facesUsingVertex)
                    {
                        facesToModify.Add(faceId);
                    }
                }

                verticesToDelete.Add(pair.Key);
            }

            if (facesToModify.Count == 0 && verticesToDelete.Count == 0)
            {
                return;
            }

            MMesh.GeometryOperation operation = mesh.StartOperation();

            foreach (int faceId in facesToModify)
            {
                Face face = mesh.GetFace(faceId);
                List<int> updatedVertexIds = new List<int>(face.vertexIds.Count);
                for (int i = 0; i < face.vertexIds.Count; i++)
                {
                    int vertexId = face.vertexIds[i];
                    int mapped;
                    if (!remap.TryGetValue(vertexId, out mapped))
                    {
                        mapped = vertexId;
                    }

                    updatedVertexIds.Add(mapped);
                }

                operation.ModifyFace(faceId, updatedVertexIds, face.properties);
            }

            foreach (int vertexId in verticesToDelete)
            {
                operation.DeleteVertex(vertexId);
            }

            operation.CommitWithoutRecalculation();
        }

        private static Dictionary<int, Color32> BuildInitialVertexColorMap(Face face)
        {
            Dictionary<int, Color32> vertexColors = new Dictionary<int, Color32>();
            List<Color32> colors = face.GetColors();
            if (colors == null || colors.Count == 0)
            {
                return vertexColors;
            }

            int limit = Mathf.Min(colors.Count, face.vertexIds.Count);
            for (int i = 0; i < limit; i++)
            {
                vertexColors[face.vertexIds[i]] = colors[i];
            }

            return vertexColors;
        }

        private static bool TryIntegrateFaceColors(Face face, Dictionary<int, Color32> regionVertexColors)
        {
            if (regionVertexColors == null)
            {
                return true;
            }

            List<Color32> colors = face.GetColors();
            if (colors == null || colors.Count == 0)
            {
                return true;
            }

            if (colors.Count != face.vertexIds.Count)
            {
                return false;
            }

            List<KeyValuePair<int, Color32>> pendingAssignments = null;

            for (int i = 0; i < face.vertexIds.Count; i++)
            {
                int vertexId = face.vertexIds[i];
                Color32 color = colors[i];
                Color32 existing;
                if (regionVertexColors.TryGetValue(vertexId, out existing))
                {
                    if (!ColorsMatch(existing, color))
                    {
                        return false;
                    }
                }
                else
                {
                    if (pendingAssignments == null)
                    {
                        pendingAssignments = new List<KeyValuePair<int, Color32>>();
                    }

                    pendingAssignments.Add(new KeyValuePair<int, Color32>(vertexId, color));
                }
            }

            if (pendingAssignments != null)
            {
                foreach (KeyValuePair<int, Color32> assignment in pendingAssignments)
                {
                    regionVertexColors[assignment.Key] = assignment.Value;
                }
            }

            return true;
        }

        private static bool ColorsMatch(Color32 a, Color32 b)
        {
            return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
        }

        private struct SpatialHashKey
        {
            public readonly int x;
            public readonly int y;
            public readonly int z;

            public SpatialHashKey(int x, int y, int z)
            {
                this.x = x;
                this.y = y;
                this.z = z;
            }

            public static SpatialHashKey FromPosition(Vector3 position, float cellSize)
            {
                return new SpatialHashKey(
                    Mathf.FloorToInt(position.x / cellSize),
                    Mathf.FloorToInt(position.y / cellSize),
                    Mathf.FloorToInt(position.z / cellSize));
            }

            public override bool Equals(object obj)
            {
                if (!(obj is SpatialHashKey))
                {
                    return false;
                }

                SpatialHashKey other = (SpatialHashKey)obj;
                return x == other.x && y == other.y && z == other.z;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 + x;
                    hash = hash * 31 + y;
                    hash = hash * 31 + z;
                    return hash;
                }
            }
        }

        private static Dictionary<int, HashSet<int>> BuildAdjacency(MMesh mesh)
        {
            Dictionary<int, HashSet<int>> adjacency = new Dictionary<int, HashSet<int>>(mesh.faceCount);
            Dictionary<EdgeKey, List<int>> edgeToFaces = new Dictionary<EdgeKey, List<int>>();

            foreach (Face face in mesh.GetFaces())
            {
                adjacency[face.id] = new HashSet<int>();
                AddFaceEdges(face.vertexIds, face.id, edgeToFaces);
            }

            foreach (KeyValuePair<EdgeKey, List<int>> pair in edgeToFaces)
            {
                List<int> faces = pair.Value;
                for (int i = 0; i < faces.Count; i++)
                {
                    for (int j = i + 1; j < faces.Count; j++)
                    {
                        adjacency[faces[i]].Add(faces[j]);
                        adjacency[faces[j]].Add(faces[i]);
                    }
                }
            }

            return adjacency;
        }

        private static void AddFaceEdges(ReadOnlyCollection<int> vertexIds, int faceId,
            Dictionary<EdgeKey, List<int>> edgeToFaces)
        {
            for (int i = 0; i < vertexIds.Count; i++)
            {
                int start = vertexIds[i];
                int end = vertexIds[(i + 1) % vertexIds.Count];
                EdgeKey key = new EdgeKey(start, end);
                if (!edgeToFaces.TryGetValue(key, out List<int> faces))
                {
                    faces = new List<int>();
                    edgeToFaces[key] = faces;
                }

                faces.Add(faceId);
            }
        }

        private static HashSet<int> CollectCoplanarRegion(MMesh mesh, int startFaceId, int materialId, Plane plane,
            Vector3 normalizedNormal, Dictionary<int, HashSet<int>> adjacency, HashSet<int> removedFaces,
            Dictionary<int, Color32> regionVertexColors, List<int> regionList)
        {
            HashSet<int> region = new HashSet<int>();
            Queue<int> queue = new Queue<int>();
            queue.Enqueue(startFaceId);

            while (queue.Count > 0)
            {
                int currentFaceId = queue.Dequeue();
                if (region.Contains(currentFaceId) || removedFaces.Contains(currentFaceId))
                {
                    continue;
                }

                Face currentFace;
                if (!mesh.TryGetFace(currentFaceId, out currentFace))
                {
                    continue;
                }

                if (currentFace.vertexIds.Count < 3)
                {
                    continue;
                }

                if (currentFace.properties.materialId != materialId)
                {
                    continue;
                }

                if (!IsCoplanar(mesh, currentFace, plane, normalizedNormal))
                {
                    continue;
                }

                if (!TryIntegrateFaceColors(currentFace, regionVertexColors))
                {
                    continue;
                }

                region.Add(currentFaceId);
                regionList.Add(currentFaceId);

                if (!adjacency.TryGetValue(currentFaceId, out HashSet<int> neighbors))
                {
                    continue;
                }

                foreach (int neighborId in neighbors)
                {
                    if (!region.Contains(neighborId))
                    {
                        queue.Enqueue(neighborId);
                    }
                }
            }

            return region;
        }

        private static bool IsCoplanar(MMesh mesh, Face face, Plane plane, Vector3 normalizedNormal)
        {
            Vector3 faceNormal = face.normal;
            if (faceNormal == Vector3.zero)
            {
                List<Vector3> positions = new List<Vector3>(face.vertexIds.Count);
                foreach (int vertexId in face.vertexIds)
                {
                    positions.Add(mesh.VertexPositionInMeshCoords(vertexId));
                }

                faceNormal = MeshMath.CalculateNormal(positions);
                if (faceNormal == Vector3.zero)
                {
                    return false;
                }
            }

            Vector3 normalizedFaceNormal = faceNormal.normalized;
            if (Vector3.Dot(normalizedFaceNormal, normalizedNormal) < NORMAL_DOT_THRESHOLD)
            {
                return false;
            }

            foreach (int vertexId in face.vertexIds)
            {
                Vector3 point = mesh.VertexPositionInMeshCoords(vertexId);
                if (Mathf.Abs(plane.GetDistanceToPoint(point)) > PLANE_DISTANCE_TOLERANCE)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryBuildMergedPolygon(MMesh mesh, HashSet<int> region, Vector3 expectedNormal,
            bool requireConvexResult, out List<int> mergedLoop)
        {
            List<OrientedEdge> orientedEdges = new List<OrientedEdge>();
            Dictionary<EdgeKey, int> edgeUseCounts = new Dictionary<EdgeKey, int>();

            foreach (int faceId in region)
            {
                Face face = mesh.GetFace(faceId);
                for (int i = 0; i < face.vertexIds.Count; i++)
                {
                    int start = face.vertexIds[i];
                    int end = face.vertexIds[(i + 1) % face.vertexIds.Count];
                    EdgeKey key = new EdgeKey(start, end);
                    OrientedEdge edge = new OrientedEdge(start, end, key);
                    orientedEdges.Add(edge);

                    if (!edgeUseCounts.TryGetValue(key, out int count))
                    {
                        count = 0;
                    }

                    edgeUseCounts[key] = count + 1;
                }
            }

            List<OrientedEdge> boundaryEdges = new List<OrientedEdge>();
            foreach (OrientedEdge edge in orientedEdges)
            {
                if (edgeUseCounts[edge.Key] == 1)
                {
                    boundaryEdges.Add(edge);
                }
            }

            if (boundaryEdges.Count == 0)
            {
                mergedLoop = null;
                return false;
            }

            if (!TryBuildSingleLoop(boundaryEdges, out mergedLoop))
            {
                mergedLoop = null;
                return false;
            }

            if (mergedLoop.Count < 3)
            {
                mergedLoop = null;
                return false;
            }

            EnsureOrientation(mesh, mergedLoop, expectedNormal);

            if (requireConvexResult && !IsConvexPolygon(mesh, mergedLoop, expectedNormal))
            {
                mergedLoop = null;
                return false;
            }

            return true;
        }

        private static bool IsConvexPolygon(MMesh mesh, List<int> polygon, Vector3 expectedNormal)
        {
            if (polygon == null || polygon.Count < 3)
            {
                return false;
            }

            Vector3 normalizedNormal = expectedNormal.normalized;
            const float concaveTolerance = -1e-5f;

            for (int i = 0; i < polygon.Count; i++)
            {
                Vector3 a = mesh.VertexPositionInMeshCoords(polygon[i]);
                Vector3 b = mesh.VertexPositionInMeshCoords(polygon[(i + 1) % polygon.Count]);
                Vector3 c = mesh.VertexPositionInMeshCoords(polygon[(i + 2) % polygon.Count]);

                Vector3 ab = b - a;
                Vector3 bc = c - b;
                Vector3 cross = Vector3.Cross(ab, bc);
                float dot = Vector3.Dot(cross, normalizedNormal);

                if (dot < concaveTolerance)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryBuildSingleLoop(List<OrientedEdge> boundaryEdges, out List<int> loop)
        {
            loop = null;
            if (boundaryEdges.Count == 0)
            {
                return false;
            }

            Dictionary<int, List<int>> edgesByStart = new Dictionary<int, List<int>>();
            for (int i = 0; i < boundaryEdges.Count; i++)
            {
                if (!edgesByStart.TryGetValue(boundaryEdges[i].Start, out List<int> indices))
                {
                    indices = new List<int>();
                    edgesByStart[boundaryEdges[i].Start] = indices;
                }

                indices.Add(i);
            }

            HashSet<int> usedEdges = new HashSet<int>();
            int startEdgeIndex = 0;
            OrientedEdge startEdge = boundaryEdges[startEdgeIndex];
            int startVertex = startEdge.Start;
            int currentVertex = startVertex;
            int currentEdgeIndex = startEdgeIndex;
            List<int> polygon = new List<int>(boundaryEdges.Count);

            while (true)
            {
                polygon.Add(currentVertex);
                usedEdges.Add(currentEdgeIndex);
                int nextVertex = boundaryEdges[currentEdgeIndex].End;
                currentVertex = nextVertex;
                if (currentVertex == startVertex)
                {
                    break;
                }

                if (!edgesByStart.TryGetValue(currentVertex, out List<int> nextCandidates))
                {
                    return false;
                }

                int nextEdgeIndex = -1;
                foreach (int candidateIndex in nextCandidates)
                {
                    if (!usedEdges.Contains(candidateIndex))
                    {
                        nextEdgeIndex = candidateIndex;
                        break;
                    }
                }

                if (nextEdgeIndex == -1)
                {
                    return false;
                }

                currentEdgeIndex = nextEdgeIndex;
            }

            if (usedEdges.Count != boundaryEdges.Count)
            {
                return false;
            }

            loop = polygon;
            return true;
        }

        private static void EnsureOrientation(MMesh mesh, List<int> polygon, Vector3 expectedNormal)
        {
            List<Vector3> positions = new List<Vector3>(polygon.Count);
            foreach (int vertexId in polygon)
            {
                positions.Add(mesh.VertexPositionInMeshCoords(vertexId));
            }

            Vector3 normal = MeshMath.CalculateNormal(positions);
            if (normal == Vector3.zero)
            {
                return;
            }

            if (Vector3.Dot(normal.normalized, expectedNormal) < 0f)
            {
                polygon.Reverse();
            }
        }

        private readonly struct EdgeKey
        {
            public readonly int A;
            public readonly int B;

            public EdgeKey(int first, int second)
            {
                if (first < second)
                {
                    A = first;
                    B = second;
                }
                else
                {
                    A = second;
                    B = first;
                }
            }

            public override bool Equals(object obj)
            {
                if (obj is EdgeKey other)
                {
                    return A == other.A && B == other.B;
                }

                return false;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (A * 397) ^ B;
                }
            }
        }

        private readonly struct OrientedEdge
        {
            public int Start { get; }
            public int End { get; }
            public EdgeKey Key { get; }

            public OrientedEdge(int start, int end, EdgeKey key)
            {
                Start = start;
                End = end;
                Key = key;
            }
        }
    }
}
