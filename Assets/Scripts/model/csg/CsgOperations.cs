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
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;

using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.import;
using com.google.apps.peltzer.client.model.util;

namespace com.google.apps.peltzer.client.model.csg
{

    public class CsgOperations
    {
        private const float COPLANAR_EPS = 0.001f;

        public enum CsgOperation
        {
            INACTIVE,
            UNION,
            INTERSECT,
            SUBTRACT,
            SPLIT,
            PAINT_INTERSECT
        }

        /// <summary>
        ///   Performs a CSG operation on all intersecting meshes in a model.
        /// </summary>
        /// <returns>true if the brush intersects with meshes in the scene.</returns>
        public static bool CsgMeshFromModel(Model model, SpatialIndex spatialIndex, MMesh brush, CsgOperation csgOp = CsgOperation.SUBTRACT)
        {
            Bounds bounds = brush.bounds;

            List<Command> commands = new List<Command>();
            HashSet<int> intersectingMeshIds;
            if (spatialIndex.FindIntersectingMeshes(brush.bounds, out intersectingMeshIds))
            {
                foreach (int meshId in intersectingMeshIds)
                {
                    MMesh mesh = model.GetMesh(meshId);
                    List<MMesh> results = DoCsgOperation(mesh, brush, csgOp);
                    commands.Add(new DeleteMeshCommand(mesh.id));
                    bool isFirstResult = true;
                    foreach (MMesh result in results)
                    {
                        MMesh meshToAdd = result;
                        if (!isFirstResult)
                        {
                            meshToAdd = result.CloneWithNewId(model.GenerateMeshId());
                        }

                        if (model.CanAddMesh(meshToAdd))
                        {
                            commands.Add(new AddMeshCommand(meshToAdd));
                        }
                        else
                        {
                            // Abort everything if an invalid mesh would be generated.
                            return false;
                        }
                        isFirstResult = false;
                    }
                }
            }
            if (commands.Count > 0)
            {
                model.ApplyCommand(new CompositeCommand(commands));
                return true;
            }
            return false;
        }

        /// <summary>
        ///   Performs CSG on two meshes.  Returns any resulting meshes for the operation.
        ///   If the result is an empty space, returns an empty list.
        /// </summary>
        public static List<MMesh> DoCsgOperation(MMesh brush, MMesh target, CsgOperation csgOp = CsgOperation.SUBTRACT)
        {
            if (csgOp == CsgOperation.SPLIT)
            {
                List<MMesh> splitResults = new List<MMesh>();
                splitResults.AddRange(DoCsgOperation(brush, target, CsgOperation.SUBTRACT));
                splitResults.AddRange(DoCsgOperation(brush, target, CsgOperation.INTERSECT));
                return splitResults;
            }

            List<MMesh> meshes = new List<MMesh>();

            // If the objects don't overlap, we have fast paths:
            if (!brush.bounds.Intersects(target.bounds))
            {
                switch (csgOp)
                {
                    case CsgOperation.INTERSECT:
                        // No intersection when bounds don't overlap
                        return meshes;
                    case CsgOperation.SUBTRACT:
                        // Subtracting non-overlapping object returns the original
                        MMesh clone = brush.Clone();
                        meshes.Add(clone);
                        return meshes;
                    case CsgOperation.UNION:
                        // Union of non-overlapping objects is both objects
                        MMesh brushClone = brush.Clone();
                        MMesh targetClone = target.Clone();
                        meshes.Add(brushClone);
                        meshes.Add(targetClone);
                        return meshes;
                    case CsgOperation.PAINT_INTERSECT:
                        // No intersection when bounds don't overlap
                        return meshes;

                }
            }

            // Our epsilons aren't very good for operations that are either very small or very big,
            // so translate and scale the two csg shapes so they're centered around the origin
            // and reasonably sized. This prevents a lot of floating point error in the ensuing maths.
            //
            // Here's a good article for comparing floating point numbers:
            // https://randomascii.wordpress.com/2012/02/25/comparing-floating-point-numbers-2012-edition/
            Vector3 operationalCenter = (brush.bounds.center + target.bounds.center) / 2.0f;
            float averageRadius = (brush.bounds.extents.magnitude + target.bounds.extents.magnitude) / 2.0f;
            Vector3 operationOffset = -operationalCenter;
            float operationScale = 1.0f / averageRadius;
            if (operationScale < 1.0f)
            {
                operationScale = 1.0f;
            }

            Bounds operationBounds = new Bounds();
            foreach (int vertexId in brush.GetVertexIds())
            {
                operationBounds.Encapsulate((brush.VertexPositionInModelCoords(vertexId) + operationOffset) * operationScale);
            }
            foreach (int vertexId in target.GetVertexIds())
            {
                operationBounds.Encapsulate((target.VertexPositionInModelCoords(vertexId) + operationOffset) * operationScale);
            }
            operationBounds.Expand(0.01f);

            CsgContext ctx = new CsgContext(operationBounds);

            CsgObject leftObj = ToCsg(ctx, brush, operationOffset, operationScale);
            CsgObject rightObj = ToCsg(ctx, target, operationOffset, operationScale);
            List<CsgPolygon> result = null;

            switch (csgOp)
            {
                case CsgOperation.UNION:
                    result = CsgUnion(ctx, leftObj, rightObj);
                    break;
                case CsgOperation.INTERSECT:
                    result = CsgIntersect(ctx, leftObj, rightObj);
                    break;
                case CsgOperation.SUBTRACT:
                    result = CsgSubtract(ctx, leftObj, rightObj);
                    break;
                case CsgOperation.PAINT_INTERSECT:
                    result = CsgPaintIntersect(ctx, leftObj, rightObj);
                    break;
            }

            if (result != null && result.Count > 0)
            {
                HashSet<string> combinedRemixIds = null;
                if (brush.remixIds != null || target.remixIds != null)
                {
                    combinedRemixIds = new HashSet<string>();
                    if (brush.remixIds != null) combinedRemixIds.UnionWith(brush.remixIds);
                    if (target.remixIds != null) combinedRemixIds.UnionWith(target.remixIds);
                }
                MMesh resultMesh = FromPolys(
                  brush.id,
                  brush.offset,
                  brush.rotation,
                  result,
                  operationOffset,
                  operationScale,
                  combinedRemixIds);
                meshes.Add(resultMesh);
            }

            return meshes;
        }

        /// <summary>
        ///   Perform the subtract on CsgObjects.  The implementation follows the paper:
        ///   http://vis.cs.brown.edu/results/videos/bib/pdf/Laidlaw-1986-CSG.pdf
        /// </summary>
        private static List<CsgPolygon> CsgSubtract(CsgContext ctx, CsgObject leftObj, CsgObject rightObj)
        {
            SplitObject(ctx, leftObj, rightObj);
            SplitObject(ctx, rightObj, leftObj);
            SplitObject(ctx, leftObj, rightObj);
            ClassifyPolygons(leftObj, rightObj);
            ClassifyPolygons(rightObj, leftObj);

            List<CsgPolygon> polys = SelectPolygons(leftObj, false, null, PolygonStatus.OUTSIDE, PolygonStatus.OPPOSITE);
            polys.AddRange(SelectPolygons(rightObj, true, null, PolygonStatus.INSIDE));

            return polys;
        }

        /// <summary>
        ///   Perform union on CsgObjects
        /// </summary>
        public static List<CsgPolygon> CsgUnion(CsgContext ctx, CsgObject leftObj, CsgObject rightObj)
        {
            SplitObject(ctx, leftObj, rightObj);
            SplitObject(ctx, rightObj, leftObj);
            SplitObject(ctx, leftObj, rightObj);
            ClassifyPolygons(leftObj, rightObj);
            ClassifyPolygons(rightObj, leftObj);

            List<CsgPolygon> polys = SelectPolygons(leftObj, false, null, PolygonStatus.OUTSIDE, PolygonStatus.SAME);
            polys.AddRange(SelectPolygons(rightObj, false, null, PolygonStatus.OUTSIDE));

            return polys;
        }

        /// <summary>
        ///   Perform intersection on CsgObjects
        /// </summary>
        public static List<CsgPolygon> CsgIntersect(CsgContext ctx, CsgObject leftObj, CsgObject rightObj)
        {
            SplitObject(ctx, leftObj, rightObj);
            SplitObject(ctx, rightObj, leftObj);
            SplitObject(ctx, leftObj, rightObj);
            ClassifyPolygons(leftObj, rightObj);
            ClassifyPolygons(rightObj, leftObj);

            List<CsgPolygon> leftInside = SelectPolygons(leftObj, false, null, PolygonStatus.INSIDE);
            List<CsgPolygon> rightInside = SelectPolygons(rightObj, false, null, PolygonStatus.INSIDE);
            List<CsgPolygon> leftCoplanar = SelectPolygons(leftObj, false, null, PolygonStatus.SAME);
            List<CsgPolygon> rightCoplanar = SelectPolygons(rightObj, false, null, PolygonStatus.SAME);

            List<CsgPolygon> polys = new List<CsgPolygon>(
              leftInside.Count + rightInside.Count + Mathf.Max(leftCoplanar.Count, rightCoplanar.Count));
            polys.AddRange(leftInside);
            polys.AddRange(rightInside);
            polys.AddRange(MergeCoplanarPolygonsFavoringSecond(leftCoplanar, rightCoplanar));

            return polys;
        }

        /// <summary>
        ///   Keep the left object geometry but recolor intersecting faces with the dominant
        ///   material from the right object.
        /// </summary>
        private static List<CsgPolygon> CsgPaintIntersect(CsgContext ctx, CsgObject leftObj, CsgObject rightObj)
        {
            SplitObject(ctx, leftObj, rightObj);
            SplitObject(ctx, rightObj, leftObj);
            SplitObject(ctx, leftObj, rightObj);
            ClassifyPolygons(leftObj, rightObj);
            ClassifyPolygons(rightObj, leftObj);

            FaceProperties? paintProperties = DetermineDominantFaceProperties(rightObj);

            List<CsgPolygon> recolored = SelectPolygons(
              leftObj,
              invert: false,
              overwriteFaceProperties: paintProperties,
              PolygonStatus.INSIDE,
              PolygonStatus.SAME,
              PolygonStatus.OPPOSITE);
            List<CsgPolygon> untouched = SelectPolygons(
              leftObj,
              invert: false,
              overwriteFaceProperties: null,
              PolygonStatus.OUTSIDE);

            recolored.AddRange(untouched);

            // Include any polygons that remain unclassified to preserve original geometry.
            List<CsgPolygon> unknown = SelectPolygons(
              leftObj,
              invert: false,
              overwriteFaceProperties: null,
              PolygonStatus.UNKNOWN);
            recolored.AddRange(unknown);

            return recolored;
        }

        private static List<CsgPolygon> MergeCoplanarPolygonsFavoringSecond(
          List<CsgPolygon> primary, List<CsgPolygon> secondary)
        {
            if (primary.Count == 0 && secondary.Count == 0)
            {
                return new List<CsgPolygon>();
            }

            if (primary.Count == 0)
            {
                return new List<CsgPolygon>(secondary);
            }

            if (secondary.Count == 0)
            {
                return new List<CsgPolygon>(primary);
            }

            Dictionary<string, CsgPolygon> merged =
              new Dictionary<string, CsgPolygon>(primary.Count + secondary.Count);

            foreach (CsgPolygon polygon in primary)
            {
                merged[BuildPolygonKey(polygon)] = polygon;
            }

            foreach (CsgPolygon polygon in secondary)
            {
                merged[BuildPolygonKey(polygon)] = polygon;
            }

            return new List<CsgPolygon>(merged.Values);
        }

        private static string BuildPolygonKey(CsgPolygon polygon)
        {
            int vertexCount = polygon.vertices.Count;
            int[] vertexIds = new int[vertexCount];

            for (int i = 0; i < vertexCount; i++)
            {
                vertexIds[i] = RuntimeHelpers.GetHashCode(polygon.vertices[i]);
            }

            int startIndex = 0;
            for (int i = 1; i < vertexCount; i++)
            {
                if (vertexIds[i] < vertexIds[startIndex])
                {
                    startIndex = i;
                }
            }

            StringBuilder builder = new StringBuilder(vertexCount * 12);
            for (int i = 0; i < vertexCount; i++)
            {
                int index = (startIndex + i) % vertexCount;
                builder.Append(vertexIds[index]);
                builder.Append(',');
            }

            return builder.ToString();
        }

        /// <summary>
        ///   Select all of the polygons in the object with any of the given statuses.
        /// </summary>
        private static List<CsgPolygon> SelectPolygons(CsgObject obj, bool invert, FaceProperties? overwriteFaceProperties, params PolygonStatus[] status)
        {
            HashSet<PolygonStatus> selectedStatus = new HashSet<PolygonStatus>(status);
            List<CsgPolygon> polys = new List<CsgPolygon>();

            foreach (CsgPolygon poly in obj.polygons)
            {
                if (selectedStatus.Contains(poly.status))
                {
                    CsgPolygon polyToAdd = poly;
                    if (invert)
                    {
                        polyToAdd = poly.Invert();
                    }
                    if (overwriteFaceProperties.HasValue)
                    {
                        polyToAdd.faceProperties = overwriteFaceProperties.Value;
                    }
                    polys.Add(polyToAdd);
                }
            }

            return polys;
        }

        private static FaceProperties? DetermineDominantFaceProperties(CsgObject obj)
        {
            if (obj.polygons.Count == 0)
            {
                return null;
            }

            Dictionary<int, int> materialUsageCounts = new Dictionary<int, int>();
            foreach (CsgPolygon polygon in obj.polygons)
            {
                int materialId = polygon.faceProperties.materialId;
                int existingCount;
                if (materialUsageCounts.TryGetValue(materialId, out existingCount))
                {
                    materialUsageCounts[materialId] = existingCount + 1;
                }
                else
                {
                    materialUsageCounts[materialId] = 1;
                }
            }

            int selectedMaterialId = obj.polygons[0].faceProperties.materialId;
            int selectedCount = 0;
            foreach (KeyValuePair<int, int> kvp in materialUsageCounts)
            {
                if (kvp.Value > selectedCount)
                {
                    selectedMaterialId = kvp.Key;
                    selectedCount = kvp.Value;
                }
            }

            return new FaceProperties(selectedMaterialId);
        }

        // Section 7:  Classify all polygons in the object.
        private static void ClassifyPolygons(CsgObject obj, CsgObject wrt)
        {
            // Set up adjacency information.
            foreach (CsgPolygon poly in obj.polygons)
            {
                for (int i = 0; i < poly.vertices.Count; i++)
                {
                    int j = (i + 1) % poly.vertices.Count;
                    poly.vertices[i].neighbors.Add(poly.vertices[j]);
                    poly.vertices[j].neighbors.Add(poly.vertices[i]);
                }
            }

            // Classify polys.
            foreach (CsgPolygon poly in obj.polygons)
            {
                if (HasUnknown(poly) || AllBoundary(poly))
                {
                    ClassifyPolygonUsingRaycast(poly, wrt);
                    if (poly.status == PolygonStatus.INSIDE || poly.status == PolygonStatus.OUTSIDE)
                    {
                        VertexStatus newStatus = poly.status == PolygonStatus.INSIDE ? VertexStatus.INSIDE : VertexStatus.OUTSIDE;
                        foreach (CsgVertex vertex in poly.vertices)
                        {
                            PropagateVertexStatus(vertex, newStatus);
                        }
                    }
                }
                else
                {
                    // Use the status of the first vertex that is inside or outside.
                    foreach (CsgVertex vertex in poly.vertices)
                    {
                        if (vertex.status == VertexStatus.INSIDE)
                        {
                            poly.status = PolygonStatus.INSIDE;
                            break;
                        }
                        if (vertex.status == VertexStatus.OUTSIDE)
                        {
                            poly.status = PolygonStatus.OUTSIDE;
                            break;
                        }
                    }
                    AssertOrThrow.True(poly.status != PolygonStatus.UNKNOWN, "Should have classified polygon.");
                }
            }
        }

        // Fig 8.1: Propagate vertex status.
        private static void PropagateVertexStatus(CsgVertex vertex, VertexStatus newStatus)
        {
            if (vertex.status == VertexStatus.UNKNOWN)
            {
                vertex.status = newStatus;
                foreach (CsgVertex neighbor in vertex.neighbors)
                {
                    PropagateVertexStatus(neighbor, newStatus);
                }
            }
        }

        // Fig 7.2: Classify a given polygon by raycasting from its barycenter into the faces of the other object.
        // Public for testing.
        public static void ClassifyPolygonUsingRaycast(CsgPolygon poly, CsgObject wrt)
        {
            float closestPolyDist;
            CsgPolygon closest = FindClosestPolygonUsingRaycast(poly, wrt, out closestPolyDist);

            if (closest == null)
            {
                // Didn't hit any polys, we are outside.
                poly.status = PolygonStatus.OUTSIDE;
                return;
            }

            float dot = Vector3.Dot(poly.plane.normal, closest.plane.normal);
            if (Mathf.Abs(closestPolyDist) < CsgMath.EPSILON)
            {
                poly.status = dot < 0 ? PolygonStatus.OPPOSITE : PolygonStatus.SAME;
            }
            else
            {
                poly.status = dot < 0 ? PolygonStatus.OUTSIDE : PolygonStatus.INSIDE;
            }
        }

        /// <summary>
        ///   Helper method to find the closest polygon to the given polygon using raycasting.
        ///   Extracted for reuse in paint operations.
        /// </summary>
        private static CsgPolygon FindClosestPolygonUsingRaycast(
          CsgPolygon poly, CsgObject wrt, out float closestPolyDist)
        {
            Vector3 rayStart = poly.baryCenter;
            Vector3 rayNormal = poly.plane.normal;
            CsgPolygon closest = null;
            closestPolyDist = float.MaxValue;

            bool done;
            int count = 0;
            do
            {
                done = true;  // Done unless we hit a special case.
                foreach (CsgPolygon otherPoly in wrt.polygons)
                {
                    float dot = Vector3.Dot(rayNormal, otherPoly.plane.normal);
                    bool perp = Mathf.Abs(dot) < CsgMath.EPSILON;
                    bool onOtherPlane = Mathf.Abs(otherPoly.plane.GetDistanceToPoint(rayStart)) < CsgMath.EPSILON;
                    Vector3 projectedToOtherPlane = Vector3.zero;
                    float signedDist = -1f;
                    if (!perp)
                    {
                        CsgMath.RayPlaneIntersection(out projectedToOtherPlane, rayStart, rayNormal, otherPoly.plane);
                        float dist = Vector3.Distance(projectedToOtherPlane, rayStart);
                        signedDist = dist * Mathf.Sign(Vector3.Dot(rayNormal, (projectedToOtherPlane - rayStart)));
                    }

                    if (perp && onOtherPlane)
                    {
                        done = false;
                        break;
                    }
                    else if (perp && !onOtherPlane)
                    {
                        // no intersection
                    }
                    else if (!perp && onOtherPlane)
                    {
                        int isInside = CsgMath.IsInside(otherPoly, projectedToOtherPlane);
                        if (isInside >= 0)
                        {
                            closestPolyDist = 0;
                            closest = otherPoly;
                            break;
                        }
                    }
                    else if (!perp && signedDist > 0)
                    {
                        if (signedDist < closestPolyDist)
                        {
                            int isInside = CsgMath.IsInside(otherPoly, projectedToOtherPlane);
                            if (isInside > 0)
                            {
                                closest = otherPoly;
                                closestPolyDist = signedDist;
                            }
                            else if (isInside == 0)
                            {
                                // On segment, perturb and try again.
                                done = false;
                                break;
                            }
                        }
                    }
                }
                if (!done)
                {
                    // Perturb the normal and try again.
                    rayNormal += new Vector3(
                      UnityEngine.Random.Range(-0.1f, 0.1f),
                      UnityEngine.Random.Range(-0.1f, 0.1f),
                      UnityEngine.Random.Range(-0.1f, 0.1f));
                    rayNormal = rayNormal.normalized;
                }
                count++;
            } while (!done && count < 5);

            return closest;
        }

        private static bool HasUnknown(CsgPolygon poly)
        {
            foreach (CsgVertex vertex in poly.vertices)
            {
                if (vertex.status == VertexStatus.UNKNOWN)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool AllBoundary(CsgPolygon poly)
        {
            foreach (CsgVertex vertex in poly.vertices)
            {
                if (vertex.status != VertexStatus.BOUNDARY)
                {
                    return false;
                }
            }
            return true;
        }

        // Public for testing.
        public static void SplitObject(CsgContext ctx, CsgObject toSplit, CsgObject splitBy)
        {
            bool splitPoly;
            int count = 0;
            HashSet<CsgPolygon> alreadySplit = new HashSet<CsgPolygon>();
            do
            {
                splitPoly = false;
                // Temporary guard to prevent infinite loops while there are bugs.
                // TODO(bug) figure out why csg creates so many rejected splits.
                count++;
                if (count > 100)
                {
                    // This usually occurs when csg keeps trying to do the same invalid split over and over.
                    // If the algorithm has reached this point, it usually means that the two meshes are
                    // split enough to perform a pretty good looking csg subtraction. More investigation
                    // should be done on bug and we may be able to remove this guard.
                    return;
                }
                foreach (CsgPolygon toSplitPoly in toSplit.polygons)
                {
                    if (alreadySplit.Contains(toSplitPoly))
                    {
                        continue;
                    }
                    alreadySplit.Add(toSplitPoly);
                    if (toSplitPoly.bounds.Intersects(splitBy.bounds))
                    {
                        foreach (CsgPolygon splitByPoly in splitBy.polygons)
                        {
                            if (toSplitPoly.bounds.Intersects(splitByPoly.bounds)
                                && !Coplanar(toSplitPoly.plane, splitByPoly.plane))
                            {
                                splitPoly = PolygonSplitter.SplitPolys(ctx, toSplit, toSplitPoly, splitByPoly);
                                if (splitPoly)
                                {
                                    break;
                                }
                            }
                        }
                    }
                    if (splitPoly)
                    {
                        break;
                    }
                }
            } while (splitPoly);
        }

        private static bool Coplanar(Plane plane1, Plane plane2)
        {
            return Mathf.Abs(plane1.distance - plane2.distance) < COPLANAR_EPS
              && Vector3.Distance(plane1.normal, plane2.normal) < COPLANAR_EPS;
        }

        // Make an MMesh from a set of CsgPolys.  Each unique CsgVertex should be a unique vertex in the MMesh.
        // Public for testing.
        public static MMesh FromPolys(int id, Vector3 offset, Quaternion rotation, List<CsgPolygon> polys,
          Vector3? csgOffset = null, float? scale = null, HashSet<string> remixIds = null)
        {
            if (!csgOffset.HasValue)
            {
                csgOffset = Vector3.zero;
            }
            if (!scale.HasValue)
            {
                scale = 1.0f;
            }
            Dictionary<CsgVertex, int> vertexToId = new Dictionary<CsgVertex, int>();
            MMesh newMesh = new MMesh(id, Vector3.zero, Quaternion.identity,
              new Dictionary<int, Vertex>(), new Dictionary<int, Face>(), MMesh.GROUP_NONE, remixIds);
            MMesh.GeometryOperation constructionOperation = newMesh.StartOperation();
            foreach (CsgPolygon poly in polys)
            {
                List<int> vertexIds = new List<int>();
                List<Vector3> normals = new List<Vector3>();
                foreach (CsgVertex vertex in poly.vertices)
                {
                    int vertId;
                    if (!vertexToId.TryGetValue(vertex, out vertId))
                    {


                        Vertex meshVertex = constructionOperation.AddVertexMeshSpace(Quaternion.Inverse(rotation) *
                          ((vertex.loc / scale.Value - csgOffset.Value) - offset));
                        vertId = meshVertex.id;
                        vertexToId[vertex] = vertId;
                    }
                    vertexIds.Add(vertId);
                    normals.Add(poly.plane.normal);
                }
                constructionOperation.AddFace(vertexIds, poly.faceProperties);
            }
            constructionOperation.Commit();
            newMesh.offset = offset;
            newMesh.rotation = rotation;

            return newMesh;
        }

        // Convert an MMesh into a CsgObject.
        // Public for testing.
        public static CsgObject ToCsg(CsgContext ctx, MMesh mesh, Vector3? offset = null, float? scale = null)
        {
            if (!offset.HasValue)
            {
                offset = Vector3.zero;
            }
            if (!scale.HasValue)
            {
                scale = 1.0f;
            }
            Dictionary<int, CsgVertex> idToVert = new Dictionary<int, CsgVertex>();
            foreach (int vertexId in mesh.GetVertexIds())
            {
                idToVert[vertexId] = ctx.CreateOrGetVertexAt((mesh.VertexPositionInModelCoords(vertexId) + offset.Value) * scale.Value);
            }

            List<CsgPolygon> polys = new List<CsgPolygon>();
            foreach (Face face in mesh.GetFaces())
            {
                GeneratePolygonsForFace(polys, idToVert, mesh, face);
            }

            return new CsgObject(polys, new List<CsgVertex>(idToVert.Values));
        }

        // Generate CsgPolygons for a Face.  CsgPolygons should be convex and have no holes.
        private static void GeneratePolygonsForFace(
            List<CsgPolygon> polys, Dictionary<int, CsgVertex> idToVert, MMesh mesh, Face face)
        {
            List<CsgVertex> vertices = new List<CsgVertex>();
            if (face.vertexIds.Count < 3)
            {
                Debug.LogWarning($"Invalid Face {face}: {face.vertexIds.Count} verts");
                return;
            }
            foreach (int vertexId in face.vertexIds)
            {
                vertices.Add(idToVert[vertexId]);
            }
            CsgPolygon poly = new CsgPolygon(vertices, face.properties);
            polys.Add(poly);
        }
    }
}
