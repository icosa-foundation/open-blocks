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
using com.google.apps.peltzer.client.model.main;

namespace com.google.apps.peltzer.client.tools
{
    /// <summary>
    ///   Component for handling 3D volume selection (box and sphere).
    /// </summary>
    public class VolumeSelector : MonoBehaviour
    {
        /// <summary>
        /// The type of volume selection.
        /// </summary>
        public enum VolumeType { BOX, SPHERE }

        // Visual feedback objects
        private GameObject volumeVisual;
        private Material volumeMaterial;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;

        // Selection state
        private bool isSelecting = false;
        private Vector3 startPosition;
        private Vector3 currentPosition;
        private VolumeType currentVolumeType = VolumeType.BOX;

        // References
        private WorldSpace worldSpace;
        private SpatialIndex spatialIndex;

        // Transparency for volume visual
        private const float VOLUME_ALPHA = 0.3f;
        private const float VOLUME_EDGE_ALPHA = 0.6f;

        /// <summary>
        /// Initialize the volume selector.
        /// </summary>
        public void Setup(WorldSpace worldSpace, SpatialIndex spatialIndex, Material baseMaterial)
        {
            this.worldSpace = worldSpace;
            this.spatialIndex = spatialIndex;

            // Create visual feedback object
            volumeVisual = new GameObject("VolumeSelector");
            volumeVisual.transform.SetParent(transform);
            meshFilter = volumeVisual.AddComponent<MeshFilter>();
            meshRenderer = volumeVisual.AddComponent<MeshRenderer>();

            // Create material with transparency
            volumeMaterial = new Material(baseMaterial);
            volumeMaterial.color = new Color(0.2f, 0.6f, 1.0f, VOLUME_ALPHA);
            meshRenderer.material = volumeMaterial;

            volumeVisual.SetActive(false);
        }

        /// <summary>
        /// Start volume selection.
        /// </summary>
        public void StartSelection(Vector3 position, VolumeType volumeType)
        {
            isSelecting = true;
            startPosition = position;
            currentPosition = position;
            currentVolumeType = volumeType;
            volumeVisual.SetActive(true);
            UpdateVisual();
        }

        /// <summary>
        /// Update the current selection position.
        /// </summary>
        public void UpdateSelection(Vector3 position)
        {
            if (!isSelecting) return;
            currentPosition = position;
            UpdateVisual();
        }

        /// <summary>
        /// End volume selection and return selected elements.
        /// </summary>
        public VolumeSelectionResult EndSelection(Selector.SelectorOptions options)
        {
            if (!isSelecting)
            {
                return new VolumeSelectionResult();
            }

            VolumeSelectionResult result = PerformSelection(options);

            isSelecting = false;
            volumeVisual.SetActive(false);

            return result;
        }

        /// <summary>
        /// Cancel the current selection.
        /// </summary>
        public void CancelSelection()
        {
            isSelecting = false;
            volumeVisual.SetActive(false);
        }

        /// <summary>
        /// Check if currently selecting.
        /// </summary>
        public bool IsSelecting()
        {
            return isSelecting;
        }

        /// <summary>
        /// Get the current volume type.
        /// </summary>
        public VolumeType GetVolumeType()
        {
            return currentVolumeType;
        }

        /// <summary>
        /// Set the volume type.
        /// </summary>
        public void SetVolumeType(VolumeType volumeType)
        {
            currentVolumeType = volumeType;
            if (isSelecting)
            {
                UpdateVisual();
            }
        }

        /// <summary>
        /// Update the visual representation of the selection volume.
        /// </summary>
        private void UpdateVisual()
        {
            if (currentVolumeType == VolumeType.BOX)
            {
                UpdateBoxVisual();
            }
            else
            {
                UpdateSphereVisual();
            }
        }

        /// <summary>
        /// Update the box visual.
        /// </summary>
        private void UpdateBoxVisual()
        {
            Vector3 centerModel = (startPosition + currentPosition) / 2f;
            Vector3 sizeModel = new Vector3(
                Mathf.Abs(currentPosition.x - startPosition.x),
                Mathf.Abs(currentPosition.y - startPosition.y),
                Mathf.Abs(currentPosition.z - startPosition.z)
            );

            // Convert from model space to world space
            volumeVisual.transform.position = worldSpace.ModelToWorld(centerModel);
            volumeVisual.transform.localScale = Vector3.one;
            volumeVisual.transform.rotation = Quaternion.identity;

            // Scale size from model space to world space for visual mesh
            Vector3 sizeWorld = sizeModel * worldSpace.scale;
            Mesh boxMesh = CreateWireframeCube(sizeWorld);
            meshFilter.mesh = boxMesh;
        }

        /// <summary>
        /// Update the sphere visual.
        /// </summary>
        private void UpdateSphereVisual()
        {
            float radiusModel = Vector3.Distance(startPosition, currentPosition);

            // Convert from model space to world space
            volumeVisual.transform.position = worldSpace.ModelToWorld(startPosition);
            volumeVisual.transform.localScale = Vector3.one;
            volumeVisual.transform.rotation = Quaternion.identity;

            // Scale radius from model space to world space for visual mesh
            float radiusWorld = radiusModel * worldSpace.scale;
            Mesh sphereMesh = CreateWireframeSphere(radiusWorld, 16, 16);
            meshFilter.mesh = sphereMesh;
        }

        /// <summary>
        /// Perform the actual selection based on the volume.
        /// </summary>
        private VolumeSelectionResult PerformSelection(Selector.SelectorOptions options)
        {
            VolumeSelectionResult result = new VolumeSelectionResult();

            // Debug: check if spatial index is null
            if (spatialIndex == null)
            {
                Debug.LogError("SpatialIndex is null!");
                return result;
            }

            if (currentVolumeType == VolumeType.BOX)
            {
                Bounds bounds = GetSelectionBounds();
                Debug.Log($"Box selection - Bounds: center={bounds.center}, size={bounds.size}");
                PerformBoxSelection(bounds, options, ref result);
            }
            else
            {
                float radius = Vector3.Distance(startPosition, currentPosition);
                Debug.Log($"Sphere selection - Center: {startPosition}, Radius: {radius}");
                PerformSphereSelection(startPosition, radius, options, ref result);
            }

            int vCount = result.vertices != null ? result.vertices.Count : 0;
            int eCount = result.edges != null ? result.edges.Count : 0;
            int fCount = result.faces != null ? result.faces.Count : 0;
            int mCount = result.meshes != null ? result.meshes.Count : 0;
            Debug.Log($"Volume selection found: {vCount} vertices, {eCount} edges, {fCount} faces, {mCount} meshes");

            return result;
        }

        /// <summary>
        /// Get the bounding box for box selection.
        /// </summary>
        private Bounds GetSelectionBounds()
        {
            Vector3 center = (startPosition + currentPosition) / 2f;
            Vector3 size = new Vector3(
                Mathf.Abs(currentPosition.x - startPosition.x),
                Mathf.Abs(currentPosition.y - startPosition.y),
                Mathf.Abs(currentPosition.z - startPosition.z)
            );
            return new Bounds(center, size);
        }

        /// <summary>
        /// Perform box selection.
        /// </summary>
        private void PerformBoxSelection(Bounds bounds, Selector.SelectorOptions options, ref VolumeSelectionResult result)
        {
            Debug.Log($"PerformBoxSelection - includeVertices:{options.includeVertices}, includeEdges:{options.includeEdges}, includeFaces:{options.includeFaces}, includeMeshes:{options.includeMeshes}");

            if (options.includeVertices)
            {
                HashSet<VertexKey> vertices;
                bool found = spatialIndex.FindVerticesInBounds(bounds, out vertices);
                Debug.Log($"FindVerticesInBounds returned {found}, count: {(vertices != null ? vertices.Count : 0)}");
                if (found)
                {
                    result.vertices = vertices;
                }
            }

            if (options.includeEdges)
            {
                HashSet<EdgeKey> edges;
                bool found = spatialIndex.FindEdgesInBounds(bounds, out edges);
                Debug.Log($"FindEdgesInBounds returned {found}, count: {(edges != null ? edges.Count : 0)}");
                if (found)
                {
                    result.edges = edges;
                }
            }

            if (options.includeFaces)
            {
                HashSet<FaceKey> faces;
                bool found = spatialIndex.FindFacesInBounds(bounds, out faces);
                Debug.Log($"FindFacesInBounds returned {found}, count: {(faces != null ? faces.Count : 0)}");
                if (found)
                {
                    result.faces = faces;
                }
            }

            if (options.includeMeshes)
            {
                HashSet<int> meshes;
                bool found = spatialIndex.FindIntersectingMeshes(bounds, out meshes);
                Debug.Log($"FindIntersectingMeshes returned {found}, count: {(meshes != null ? meshes.Count : 0)}");
                if (found)
                {
                    result.meshes = meshes;
                }
            }
        }

        /// <summary>
        /// Perform sphere selection.
        /// </summary>
        private void PerformSphereSelection(Vector3 center, float radius, Selector.SelectorOptions options,
            ref VolumeSelectionResult result)
        {
            if (options.includeVertices)
            {
                HashSet<VertexKey> vertices;
                if (spatialIndex.FindVerticesInSphere(center, radius, out vertices))
                {
                    result.vertices = vertices;
                }
            }

            if (options.includeEdges)
            {
                HashSet<EdgeKey> edges;
                if (spatialIndex.FindEdgesInSphere(center, radius, out edges))
                {
                    result.edges = edges;
                }
            }

            if (options.includeFaces)
            {
                HashSet<FaceKey> faces;
                if (spatialIndex.FindFacesInSphere(center, radius, out faces))
                {
                    result.faces = faces;
                }
            }

            if (options.includeMeshes)
            {
                // For sphere selection of meshes, use bounding sphere approximation
                Bounds bounds = new Bounds(center, Vector3.one * radius * 2);
                HashSet<int> meshes;
                if (spatialIndex.FindIntersectingMeshes(bounds, out meshes))
                {
                    result.meshes = meshes;
                }
            }
        }

        /// <summary>
        /// Create a wireframe cube mesh.
        /// </summary>
        private Mesh CreateWireframeCube(Vector3 size)
        {
            Mesh mesh = new Mesh();

            Vector3 halfSize = size / 2f;

            // Define 8 corners of the cube
            Vector3[] vertices = new Vector3[]
            {
                new Vector3(-halfSize.x, -halfSize.y, -halfSize.z),
                new Vector3( halfSize.x, -halfSize.y, -halfSize.z),
                new Vector3( halfSize.x,  halfSize.y, -halfSize.z),
                new Vector3(-halfSize.x,  halfSize.y, -halfSize.z),
                new Vector3(-halfSize.x, -halfSize.y,  halfSize.z),
                new Vector3( halfSize.x, -halfSize.y,  halfSize.z),
                new Vector3( halfSize.x,  halfSize.y,  halfSize.z),
                new Vector3(-halfSize.x,  halfSize.y,  halfSize.z)
            };

            // Define 12 edges as line segments (24 indices for 12 lines)
            int[] indices = new int[]
            {
                0, 1, 1, 2, 2, 3, 3, 0,  // Bottom face
                4, 5, 5, 6, 6, 7, 7, 4,  // Top face
                0, 4, 1, 5, 2, 6, 3, 7   // Vertical edges
            };

            mesh.vertices = vertices;
            mesh.SetIndices(indices, MeshTopology.Lines, 0);
            mesh.RecalculateBounds();

            return mesh;
        }

        /// <summary>
        /// Create a wireframe sphere mesh.
        /// </summary>
        private Mesh CreateWireframeSphere(float radius, int latSegments, int lonSegments)
        {
            Mesh mesh = new Mesh();

            List<Vector3> vertices = new List<Vector3>();
            List<int> indices = new List<int>();

            // Create latitude circles
            for (int lat = 0; lat <= latSegments; lat++)
            {
                float theta = lat * Mathf.PI / latSegments;
                float sinTheta = Mathf.Sin(theta);
                float cosTheta = Mathf.Cos(theta);

                for (int lon = 0; lon <= lonSegments; lon++)
                {
                    float phi = lon * 2 * Mathf.PI / lonSegments;
                    float sinPhi = Mathf.Sin(phi);
                    float cosPhi = Mathf.Cos(phi);

                    // Create unit sphere vertices then scale by radius
                    Vector3 position = new Vector3(
                        cosPhi * sinTheta,
                        cosTheta,
                        sinPhi * sinTheta
                    ) * radius;

                    vertices.Add(position);
                }
            }

            // Create indices for wireframe (latitude and longitude lines)
            for (int lat = 0; lat < latSegments; lat++)
            {
                for (int lon = 0; lon < lonSegments; lon++)
                {
                    int current = lat * (lonSegments + 1) + lon;
                    int next = current + lonSegments + 1;

                    // Latitude line
                    indices.Add(current);
                    indices.Add(current + 1);

                    // Longitude line
                    indices.Add(current);
                    indices.Add(next);
                }
            }

            mesh.vertices = vertices.ToArray();
            mesh.SetIndices(indices.ToArray(), MeshTopology.Lines, 0);
            mesh.RecalculateBounds();

            return mesh;
        }
    }

    /// <summary>
    /// Result of a volume selection operation.
    /// </summary>
    public struct VolumeSelectionResult
    {
        public HashSet<VertexKey> vertices;
        public HashSet<EdgeKey> edges;
        public HashSet<FaceKey> faces;
        public HashSet<int> meshes;

        public VolumeSelectionResult(bool dummy)
        {
            vertices = new HashSet<VertexKey>();
            edges = new HashSet<EdgeKey>();
            faces = new HashSet<FaceKey>();
            meshes = new HashSet<int>();
        }

        public bool HasAnySelection()
        {
            return (vertices != null && vertices.Count > 0) ||
                   (edges != null && edges.Count > 0) ||
                   (faces != null && faces.Count > 0) ||
                   (meshes != null && meshes.Count > 0);
        }
    }
}
