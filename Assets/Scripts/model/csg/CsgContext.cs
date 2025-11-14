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

using com.google.apps.peltzer.client.model.util;

namespace com.google.apps.peltzer.client.model.csg
{
    /// <summary>
    ///   Context for CSG operations.  Unifies vertices and manages adaptive tolerances.
    /// </summary>
    public class CsgContext
    {
        // We allow new points to be added if they are at least 3 epsilons away from existing points.
        private readonly int WIGGLE_ROOM = 3;
        private CollisionSystem<CsgVertex> tree;

        // Adaptive epsilon based on geometry scale
        private float baseEpsilon;
        private float geometryScale;

        public float Epsilon { get { return baseEpsilon; } }
        public float CoplanarEpsilon { get { return baseEpsilon * 10f; } }
        public float VertexMergeEpsilon { get { return baseEpsilon * WIGGLE_ROOM; } }

        public CsgContext(Bounds bounds)
        {
            tree = new NativeSpatial<CsgVertex>();

            // Calculate adaptive epsilon based on geometry size
            // Use the magnitude of the bounds extents as a scale reference
            geometryScale = bounds.extents.magnitude;

            // Base epsilon: use fixed minimum or scale-relative, whichever is larger
            // This ensures small geometries get reasonable tolerance while large ones scale appropriately
            const float MIN_EPSILON = 0.0001f;
            const float EPSILON_SCALE_FACTOR = 1e-6f;
            baseEpsilon = Mathf.Max(MIN_EPSILON, geometryScale * EPSILON_SCALE_FACTOR);

            Debug.Log($"CSG Context: geometry scale = {geometryScale:F4}, epsilon = {baseEpsilon:F6}");
        }

        public CsgVertex CreateOrGetVertexAt(Vector3 loc)
        {
            Bounds bb = new Bounds(loc, Vector3.one * VertexMergeEpsilon);
            CsgVertex closest = null;
            HashSet<CsgVertex> vertices;
            if (tree.IntersectedBy(bb, out vertices))
            {
                float closestDist = float.MaxValue;
                foreach (CsgVertex potential in vertices)
                {
                    float d = Vector3.Distance(loc, potential.loc);
                    if (d < Epsilon && d < closestDist)
                    {
                        closest = potential;
                        closestDist = d;
                    }
                }
            }
            if (closest == null)
            {
                closest = new CsgVertex(loc);
                tree.Add(closest, new Bounds(loc, Vector3.zero));
            }
            return closest;
        }
    }
}
