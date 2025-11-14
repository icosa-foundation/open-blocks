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
using System.Linq;
using System.Text;
using UnityEngine;

namespace com.google.apps.peltzer.client.model.csg
{
    public class PolyEdge
    {
        CsgVertex a;
        CsgVertex b;

        public PolyEdge(CsgVertex a, CsgVertex b)
        {
            this.a = a;
            this.b = b;
        }

        public PolyEdge Reversed()
        {
            return new PolyEdge(b, a);
        }

        public override bool Equals(object obj)
        {
            if (obj is PolyEdge)
            {
                PolyEdge other = (PolyEdge)obj;
                return a == other.a && b == other.b;
            }
            return false;
        }

        public override int GetHashCode()
        {
            int hc = 13;
            hc = (hc * 31) + a.GetHashCode();
            hc = (hc * 31) + b.GetHashCode();
            return hc;
        }
    }

    public class CsgUtil
    {

        // Do some sanity checks on a polygon split:
        //  1) All polys should have the same normal.
        //  2) Each vertex from the original poly should be in at least one splitPoly
        //  3) Each split poly should share at least one edge with one other (the edge should be reversed)
        //  4) No edge should be in more than one poly (in the same order)
        //  5) No vertex should be in the same poly more than once
        //  6) Every edge in the initial polygon should be in a split, except those *edges* that were split.
        //     We pass in numSplitEdges to tell the test how many that should be.
        public static bool IsValidPolygonSplit(CsgPolygon initialPoly, List<CsgPolygon> splitPolys, int numSplitEdges)
        {
            List<HashSet<CsgVertex>> vertsForPolys = new List<HashSet<CsgVertex>>();
            List<HashSet<PolyEdge>> edgesForPolys = new List<HashSet<PolyEdge>>();

            // Set up some datastructures, check normals while we are looping.
            foreach (CsgPolygon poly in splitPolys)
            {
                vertsForPolys.Add(new HashSet<CsgVertex>(poly.vertices));
                edgesForPolys.Add(Edges(poly));
                if (Vector3.Distance(initialPoly.plane.normal, poly.plane.normal) > 0.001f)
                {
                    Console.Write("Normals do not match: " + initialPoly.plane.normal + " vs " + poly.plane.normal);
                    return false;
                }
            }

            // Look for each vertex from the original poly:
            foreach (CsgVertex vert in initialPoly.vertices)
            {
                bool found = false;
                foreach (HashSet<CsgVertex> verts in vertsForPolys)
                {
                    if (verts.Contains(vert))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    Console.Write("Vertex from original poly is missing from split polys");
                    return false;
                }
            }

            // For each poly, find another poly with a matching edge (going the other direction)
            for (int i = 0; i < edgesForPolys.Count; i++)
            {
                HashSet<PolyEdge> polyEdges = edgesForPolys[i];
                bool foundEdge = false;
                for (int j = 0; j < edgesForPolys.Count; j++)
                {
                    if (i == j)
                    {
                        continue;  // Don't compare polygon to itself
                    }
                    foreach (PolyEdge edge in polyEdges)
                    {
                        if (edgesForPolys[j].Contains(edge.Reversed()))
                        {
                            foundEdge = true;
                        }
                    }
                }
                if (!foundEdge)
                {
                    Console.Write("Poly " + i + " does not have any edges in other polys");
                    return false;
                }
            }

            // Check that the total number of edges is the same as the sum of all edges in all splits
            // i.e. there are no duplicate edges.
            HashSet<PolyEdge> alledges = new HashSet<PolyEdge>();
            int sum = 0;
            foreach (HashSet<PolyEdge> edges in edgesForPolys)
            {
                sum += edges.Count;
                alledges.UnionWith(edges);
            }
            if (sum != alledges.Count)
            {
                Console.Write("Found duplicate edges.");
                return false;
            }

            // Check to make sure no polys have the same vertex more than once.
            for (int i = 0; i < vertsForPolys.Count; i++)
            {
                // The 'Set' should have the same number of verts as the 'List'
                if (vertsForPolys[i].Count != splitPolys[i].vertices.Count)
                {
                    Console.Write("Found duplicate vertex");
                    return false;
                }
            }

            // Look for all edges in the list above.  The count should be the same number of edges
            // in the initial poly minus the number of edges that were split.
            int count = numSplitEdges;
            HashSet<PolyEdge> initialEdges = Edges(initialPoly);
            foreach (PolyEdge initialEdge in initialEdges)
            {
                if (alledges.Contains(initialEdge))
                {
                    count++;
                }
            }
            if (initialEdges.Count != count)
            {
                Console.Write("Edges from initial poly are missing");
                return false;
            }

            return true;
        }

        private static HashSet<PolyEdge> Edges(CsgPolygon poly)
        {
            HashSet<PolyEdge> edges = new HashSet<PolyEdge>();

            for (int i = 0; i < poly.vertices.Count; i++)
            {
                CsgVertex a = poly.vertices[i];
                CsgVertex b = poly.vertices[(i + 1) % poly.vertices.Count];
                edges.Add(new PolyEdge(a, b));
            }

            return edges;
        }

        /// <summary>
        /// Comprehensive validation to detect degenerate polygons that could cause CSG failures.
        /// </summary>
        public static bool IsValidPolygon(CsgPolygon polygon, float epsilon)
        {
            if (polygon == null || polygon.vertices == null || polygon.vertices.Count < 3)
            {
                return false;
            }

            return IsValidPolygon(polygon.vertices, epsilon);
        }

        /// <summary>
        /// Comprehensive validation to detect degenerate vertex lists.
        /// </summary>
        public static bool IsValidPolygon(List<CsgVertex> vertices, float epsilon)
        {
            if (vertices == null || vertices.Count < 3)
            {
                return false;
            }

            // Check 1: Verify sufficient polygon area using Newell's method for robustness
            Vector3 normal = Vector3.zero;
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 v1 = vertices[i].loc;
                Vector3 v2 = vertices[(i + 1) % vertices.Count].loc;
                normal.x += (v1.y - v2.y) * (v1.z + v2.z);
                normal.y += (v1.z - v2.z) * (v1.x + v2.x);
                normal.z += (v1.x - v2.x) * (v1.y + v2.y);
            }

            float area = normal.magnitude * 0.5f;
            if (area < epsilon * epsilon)
            {
                // Zero or near-zero area polygon
                return false;
            }

            // Check 2: Detect near-colinear vertices
            // For each vertex, check if it forms a degenerate triangle with its neighbors
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 v0 = vertices[i].loc;
                Vector3 v1 = vertices[(i + 1) % vertices.Count].loc;
                Vector3 v2 = vertices[(i + 2) % vertices.Count].loc;

                Vector3 edge1 = v1 - v0;
                Vector3 edge2 = v2 - v1;

                float edge1Len = edge1.magnitude;
                float edge2Len = edge2.magnitude;

                // Check for zero-length edges
                if (edge1Len < epsilon || edge2Len < epsilon)
                {
                    return false;
                }

                // Check for colinearity using cross product
                Vector3 cross = Vector3.Cross(edge1, edge2);
                if (cross.magnitude < epsilon * edge1Len * edge2Len)
                {
                    // Vertices are colinear
                    return false;
                }
            }

            // Check 3: Verify no duplicate vertices
            for (int i = 0; i < vertices.Count; i++)
            {
                for (int j = i + 1; j < vertices.Count; j++)
                {
                    if (Vector3.Distance(vertices[i].loc, vertices[j].loc) < epsilon)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Validate a polygon and log detailed diagnostics about why it's invalid.
        /// Useful for debugging CSG failures.
        /// </summary>
        public static bool ValidatePolygonWithDiagnostics(CsgPolygon polygon, float epsilon, string context = "")
        {
            if (polygon == null)
            {
                Debug.LogWarning($"CSG Validation [{context}]: Polygon is null");
                return false;
            }

            if (polygon.vertices == null)
            {
                Debug.LogWarning($"CSG Validation [{context}]: Polygon vertices list is null");
                return false;
            }

            if (polygon.vertices.Count < 3)
            {
                Debug.LogWarning($"CSG Validation [{context}]: Polygon has {polygon.vertices.Count} vertices (need at least 3)");
                return false;
            }

            // Check area
            Vector3 normal = Vector3.zero;
            for (int i = 0; i < polygon.vertices.Count; i++)
            {
                Vector3 v1 = polygon.vertices[i].loc;
                Vector3 v2 = polygon.vertices[(i + 1) % polygon.vertices.Count].loc;
                normal.x += (v1.y - v2.y) * (v1.z + v2.z);
                normal.y += (v1.z - v2.z) * (v1.x + v2.x);
                normal.z += (v1.x - v2.x) * (v1.y + v2.y);
            }

            float area = normal.magnitude * 0.5f;
            if (area < epsilon * epsilon)
            {
                Debug.LogWarning($"CSG Validation [{context}]: Polygon has near-zero area ({area:F8}, threshold {epsilon * epsilon:F8})");
                return false;
            }

            // Check for colinear vertices
            for (int i = 0; i < polygon.vertices.Count; i++)
            {
                Vector3 v0 = polygon.vertices[i].loc;
                Vector3 v1 = polygon.vertices[(i + 1) % polygon.vertices.Count].loc;
                Vector3 v2 = polygon.vertices[(i + 2) % polygon.vertices.Count].loc;

                Vector3 edge1 = v1 - v0;
                Vector3 edge2 = v2 - v1;

                float edge1Len = edge1.magnitude;
                float edge2Len = edge2.magnitude;

                if (edge1Len < epsilon)
                {
                    Debug.LogWarning($"CSG Validation [{context}]: Zero-length edge at vertex {i} (length {edge1Len:F8})");
                    return false;
                }

                Vector3 cross = Vector3.Cross(edge1, edge2);
                if (cross.magnitude < epsilon * edge1Len * edge2Len)
                {
                    Debug.LogWarning($"CSG Validation [{context}]: Colinear vertices at indices {i}, {(i + 1) % polygon.vertices.Count}, {(i + 2) % polygon.vertices.Count}");
                    return false;
                }
            }

            // Check for duplicate vertices
            for (int i = 0; i < polygon.vertices.Count; i++)
            {
                for (int j = i + 1; j < polygon.vertices.Count; j++)
                {
                    float dist = Vector3.Distance(polygon.vertices[i].loc, polygon.vertices[j].loc);
                    if (dist < epsilon)
                    {
                        Debug.LogWarning($"CSG Validation [{context}]: Duplicate vertices at indices {i} and {j} (distance {dist:F8})");
                        return false;
                    }
                }
            }

            return true;
        }
    }
}

