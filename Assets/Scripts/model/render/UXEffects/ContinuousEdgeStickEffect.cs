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
using com.google.apps.peltzer.client.alignment;
using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.main;
using UnityEngine;
using com.google.apps.peltzer.client.tools.utils;

namespace com.google.apps.peltzer.client.model.render
{
    /// <summary>
    /// UX Effect which renders guides for continuous edge sticking (an outline on the target edge when the source mesh
    /// sticks to it.)
    /// </summary>
    class ContinuousEdgeStickEffect : UXEffectManager.UXEffect
    {
        private const float DEFAULT_DURATION = 1.0f;

        private bool inSnapThreshhold = false;
        private Mesh lineMesh;
        private Matrix4x4 matrix;

        /// <summary>
        /// Constructs the effect, Initialize must still be called before the effect starts to take place.
        /// </summary>
        /// <param name="snapTarget">The MMesh id of the target mesh to play the shader on.</param>
        public ContinuousEdgeStickEffect()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lineMesh = go.GetComponent<MeshFilter>().mesh;
            GameObject.Destroy(go); // only need the mesh
        }

        public override void Initialize(MeshRepresentationCache cache, MaterialLibrary materialLibrary,
          WorldSpace worldSpace)
        {
            base.Initialize(cache, materialLibrary.pointEdgeFaceHighlightMaterial, worldSpace);
        }

        public override void Render()
        {
            Graphics.DrawMesh(lineMesh, matrix, effectMaterial, 0);
        }

        public override void Finish()
        {
            UXEffectManager.GetEffectManager().EndEffect(this);
        }

        /// <summary>
        /// Updates the effect based on the supplied EdgeInfo.
        /// </summary>
        public void UpdateFromEdge(EdgeInfo edge)
        {
            float scaleFactor = InactiveRenderer.GetEdgeScaleFactor(worldSpace);

            float distance = Vector3.Distance(edge.edgeStart + edge.edgeVector, edge.edgeStart);
            Vector3 midPoint = (edge.edgeStart + edge.edgeStart + edge.edgeVector) / 2;
            Vector3 direction = edge.edgeVector;
            Quaternion rotation = Quaternion.LookRotation(direction);
            Vector3 scale = new Vector3(scaleFactor, scaleFactor, distance);
            matrix = worldSpace.modelToWorld * Matrix4x4.TRS(midPoint, rotation, scale);
        }
    }
}
