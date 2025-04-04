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
using com.google.apps.peltzer.client.model.controller;
using com.google.apps.peltzer.client.model.main;
using UnityEngine;

namespace com.google.apps.peltzer.client.tools.utils
{
    /// <summary>
    /// Constructs and renders a grid of points, snapped to the universal grid.
    /// </summary>
    public class GridHighlighter
    {
        private Mesh sphereMesh;

        private List<Matrix4x4> matrices = new();
        private List<Vector3> gridVertices = new();

        private float sphereScale = 0.005f;

        public void InitGrid(int numVertsPerRow, int gridSkip = 1)
        {
            // Create a sphere mesh for the grid highlight
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphereMesh = sphere.GetComponent<MeshFilter>().mesh;
            Object.Destroy(sphere);

            int x, y, z;
            float curX, curY, curZ;
            int index = 0;
            for (x = 0; x < numVertsPerRow; x++)
            {
                curX = (x - (Mathf.FloorToInt(numVertsPerRow / 2))) * GridUtils.GRID_SIZE;
                for (y = 0; y < numVertsPerRow; y++)
                {
                    curY = (y - (Mathf.FloorToInt(numVertsPerRow / 2))) * GridUtils.GRID_SIZE;
                    for (z = 0; z < numVertsPerRow; z++)
                    {
                        curZ = (z - (Mathf.FloorToInt(numVertsPerRow / 2))) * GridUtils.GRID_SIZE;
                        gridVertices.Add(new Vector3(curX, curY, curZ));
                        matrices.Add(new Matrix4x4());

                        index++;
                    }
                }
            }
        }

        public void Render(Vector3 unsnappedGridCenter, Matrix4x4 objectToWorld, Material renderMat, float scale)
        {
            // Scale to the correct grid granularity, then translate to the correct model position, then apply model to
            // world transform. This should result in the correct model->world matrix for the grid's vertices.
            Vector3 gridCenter = GridUtils.SnapToGrid(unsnappedGridCenter / scale) * scale;
            Matrix4x4 gridTransform = objectToWorld * Matrix4x4.TRS(gridCenter, Quaternion.identity, Vector3.one)
              * Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, scale));
            for (var i = 0; i < gridVertices.Count; i++)
            {
                matrices[i] = Matrix4x4.TRS(gridTransform.MultiplyPoint3x4(gridVertices[i]), Quaternion.identity, new Vector3(sphereScale, sphereScale, sphereScale));
            }
            Graphics.DrawMeshInstanced(sphereMesh, 0, renderMat, matrices);
        }
    }
}