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

using UnityEngine;
using System.Collections.Generic;
using System.IO;
using com.google.apps.peltzer.client.model.core;
using com.google.apps.peltzer.client.model.render;
using System.Linq;
using System;

namespace com.google.apps.peltzer.client.model.import
{
    /// <summary>
    /// Imports obj and mtl files.
    /// </summary>
    public static class ObjImporter
    {
        /// <summary>
        /// Material data parsed from MTL file, before creating Unity Material objects
        /// </summary>
        public class MaterialData
        {
            public string name;
            public Color color = Color.white;
            public string textureReference;
            public int? registryMaterialId;
        }

        /// <summary>
        ///   Creates an MMesh from the contents of a .obj file and the contents of a .mtl file, with the given id.
        ///   Generally an OBJ file will not create meshes that are "topologically correct", so they won't work right
        ///   with a lot of our tools, but should at least be moveable if nothing else.
        /// </summary>
        /// <param name="objFileContents">The contents of a .obj file.</param>
        /// <param name="mtlFileContents">The contents of a .mtl file.</param>
        /// <param name="id">The id of the new MMesh.</param>
        /// <param name="result">The created mesh, or null if it could not be created.</param>
        /// <returns>Whether the MMesh could be created.</returns>
        public static bool MMeshFromObjFile(
          string objFileContents,
          string mtlFileContents,
          int id,
          out MMesh result,
          string textureSearchDirectory = null,
          Dictionary<string, Texture2D> externalTextures = null)
        {
            Dictionary<string, Material> materials = ImportMaterials(mtlFileContents, textureSearchDirectory, externalTextures);
            var success = ImportMeshes(id, objFileContents, materials, out MMesh mmesh);
            if (success)
            {
                result = mmesh;
                return true;
            }
            result = null;
            return false;
        }

        private static int TryGetMaterialId(string materialName)
        {
            if (!string.IsNullOrEmpty(materialName) && materialName.StartsWith("mat"))
            {
                int val = 1;
                if (int.TryParse(materialName.Substring(3), out val))
                {
                    return val;
                }
            }
            return 1;
        }

        /// <summary>
        /// Parse MTL file data without creating Unity objects (background thread safe)
        /// </summary>
        public static Dictionary<string, MaterialData> ParseMaterialData(string materialsString)
        {
            Dictionary<string, MaterialData> materialDataMap = new Dictionary<string, MaterialData>();
            if (string.IsNullOrEmpty(materialsString))
                return materialDataMap;

            using (StringReader reader = new StringReader(materialsString))
            {
                string currentText = reader.ReadLine()?.Trim();
                while (currentText != null)
                {
                    if (currentText.StartsWith("newmtl"))
                    {
                        string materialName = currentText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[1];
                        MaterialData data = new MaterialData { name = materialName };

                        currentText = reader.ReadLine();
                        while (currentText != null && !currentText.StartsWith("newmtl"))
                        {
                            currentText = currentText.Trim();
                            if (currentText.StartsWith("Ka") || currentText.StartsWith("Kd"))
                            {
                                string[] colorString = currentText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                if (colorString.Length >= 4)
                                {
                                    data.color = new Color(float.Parse(colorString[1]), float.Parse(colorString[2]), float.Parse(colorString[3]));
                                }
                            }
                            else if (currentText.StartsWith("map_Kd", StringComparison.OrdinalIgnoreCase))
                            {
                                string[] mapTokens = currentText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                if (mapTokens.Length >= 2)
                                {
                                    data.textureReference = string.Join(" ", mapTokens, 1, mapTokens.Length - 1);
                                }
                            }
                            currentText = reader.ReadLine();
                        }

                        // Check if this references a material registry ID
                        if (materialName.StartsWith("mat"))
                        {
                            if (int.TryParse(materialName.Substring("mat".Length), out int potentialMaterialId))
                            {
                                data.registryMaterialId = potentialMaterialId;
                            }
                        }

                        materialDataMap[materialName] = data;
                    }
                    else
                    {
                        currentText = reader.ReadLine()?.Trim();
                    }
                }
            }
            return materialDataMap;
        }

        /// <summary>
        /// Create Material objects from parsed MaterialData (must be called on main thread)
        /// </summary>
        public static Dictionary<string, Material> CreateMaterialsFromData(
            Dictionary<string, MaterialData> materialDataMap,
            string textureSearchDirectory,
            Dictionary<string, Texture2D> externalTextures)
        {
            Dictionary<string, Material> materials = new Dictionary<string, Material>();
            foreach (var kvp in materialDataMap)
            {
                MaterialData data = kvp.Value;
                Material material = null;

                // Check if this references a material registry entry
                if (data.registryMaterialId.HasValue)
                {
                    material = MaterialRegistry.GetMaterialAndColorById(data.registryMaterialId.Value).material;
                    material.name = data.name;
                }

                if (material == null)
                {
                    material = new Material(Shader.Find("Diffuse"));
                    material.name = data.name;
                    material.color = data.color;
                }

                if (!string.IsNullOrEmpty(data.textureReference))
                {
                    Texture2D texture = TryResolveTexture(data.textureReference, textureSearchDirectory, externalTextures);
                    if (texture != null)
                    {
                        AssignTexture(material, texture);
                    }
                }

                materials[data.name] = material;
            }
            return materials;
        }

        public static Dictionary<string, Material> ImportMaterials(
          string materialsString,
          string textureSearchDirectory,
          Dictionary<string, Texture2D> externalTextures)
        {
            Dictionary<string, Material> materials = new Dictionary<string, Material>();
            if (materialsString == null || materialsString.Length == 0)
                return materials;

            using (StringReader reader = new StringReader(materialsString))
            {
                string currentText = reader.ReadLine().Trim();
                while (currentText != null)
                {
                    if (currentText.StartsWith("newmtl"))
                    {
                        string materialName = currentText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[1];
                        Color materialColor = Color.white;
                        string albedoTextureReference = null;

                        currentText = reader.ReadLine();
                        while (currentText != null && !currentText.StartsWith("newmtl"))
                        {
                            currentText = currentText.Trim();
                            if (currentText.StartsWith("Ka"))
                            {
                                string[] colorString = currentText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                materialColor = new Color(float.Parse(colorString[1]), float.Parse(colorString[2]),
                                  float.Parse(colorString[3]));
                            }
                            else if (currentText.StartsWith("Kd"))
                            {
                                string[] colorString = currentText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                materialColor = new Color(float.Parse(colorString[1]), float.Parse(colorString[2]),
                                  float.Parse(colorString[3]));
                            }
                            else if (currentText.StartsWith("map_Kd", StringComparison.OrdinalIgnoreCase))
                            {
                                string[] mapTokens = currentText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                if (mapTokens.Length >= 2)
                                {
                                    albedoTextureReference = string.Join(" ", mapTokens, 1, mapTokens.Length - 1);
                                }
                            }
                            currentText = reader.ReadLine();
                        }

                        Material material = null;
                        if (materialName.StartsWith("mat"))
                        {
                            int potentialMaterialId;
                            if (int.TryParse(materialName.Substring("mat".Length), out potentialMaterialId))
                            {
                                material = MaterialRegistry.GetMaterialAndColorById(potentialMaterialId).material;
                                material.name = materialName;
                            }
                        }
                        if (material == null)
                        {
                            material = new Material(Shader.Find("Diffuse"));
                            material.name = materialName;
                            material.color = materialColor;
                        }

                        if (!string.IsNullOrEmpty(albedoTextureReference))
                        {
                            Texture2D texture = TryResolveTexture(albedoTextureReference, textureSearchDirectory, externalTextures);
                            if (texture != null)
                            {
                                AssignTexture(material, texture);
                            }
                        }

                        materials.Add(materialName, material);
                    }
                    else
                    {
                        currentText = reader.ReadLine();
                    }
                }
            }
            return materials;
        }

        public class FFace
        {
            public List<int> vertexIds = new List<int>();
            public List<int> texCoordIds = new List<int>();
        }

        public static bool ImportMeshes(int id, string objFileContents, Dictionary<string, Material> materials, out MMesh mmesh)
        {

            if (string.IsNullOrEmpty(objFileContents))
            {
                mmesh = null;
                return false;
            }

            // Default current material, in case they don't have an MTL file.
            bool mtlFileWasSupplied = materials.Count > 0;
            string currentMaterial = "mat0";
            if (!mtlFileWasSupplied)
            {
                materials.Add(currentMaterial, MaterialRegistry.GetMaterialAndColorById(0).material);
            }

            var textureVertices = new List<Vector2>();
            var allFaces = new Dictionary<string, List<FFace>>();

            string[] parts;
            char[] sep = { ' ' };
            char[] sep2 = { ':' };
            char[] sep3 = { '/' };
            string[] sep4 = { "//" };
            using (StringReader reader = new StringReader(objFileContents))
            {

                Dictionary<int, Vertex> verticesById = new Dictionary<int, Vertex>();
                Dictionary<int, Face> facesById = new Dictionary<int, Face>();

                string line = reader.ReadLine();
                while (line != null)
                {
                    if (line.StartsWith("v "))
                    {
                        parts = line.Trim().Split(sep, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length < 4)
                        {
                            Debug.Log("Not enough vertex values");
                            Debug.Log(line);
                            mmesh = null;
                            return false;
                        }
                        try
                        {
                            var v = new Vector3(Convert.ToSingle(parts[1]), Convert.ToSingle(parts[2]), Convert.ToSingle(parts[3]));
                            int vIndex = verticesById.Count;
                            var vert = new Vertex(vIndex, v);
                            verticesById.Add(vIndex, vert);
                        }
                        catch (FormatException)
                        {
                            Debug.Log("Unexpected vertex value");
                            Debug.Log(line);
                            mmesh = null;
                            return false;
                        }
                    }
                    else if (line.StartsWith("vt "))
                    {
                        parts = line.Trim().Split(sep, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Count() < 3)
                        {
                            Debug.Log("Not enough tex vertex values");
                            Debug.Log(line);
                            mmesh = null;
                            return false;
                        }
                        try
                        {
                            textureVertices.Add(new Vector2(Convert.ToSingle(parts[1]), Convert.ToSingle(parts[2])));
                        }
                        catch (FormatException)
                        {
                            Debug.Log("Unexpected tex vertex value");
                            Debug.Log(line);
                            mmesh = null;
                            return false;
                        }
                    }
                    else if (line.StartsWith("usemtl ") && mtlFileWasSupplied)
                    {
                        parts = line.Trim().Split(sep, StringSplitOptions.RemoveEmptyEntries);
                        if (parts[1].Contains(sep2[0]))
                        {
                            currentMaterial = parts[1].Split(sep2, StringSplitOptions.RemoveEmptyEntries)[1];
                        }
                        else
                        {
                            currentMaterial = parts[1];
                        }
                    }
                    else if (line.StartsWith("f "))
                    {
                        parts = line.Trim().Split(sep, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length < 4)
                        {
                            Debug.Log("Not vertex values in a face");
                            Debug.Log(line);
                            mmesh = null;
                            return false;
                        }
                        FFace face = new FFace();
                        bool validFace = true;
                        for (int i = 1; i < parts.Length; i++)
                        {
                            int vertexId = 0;
                            int texCoordId = -1;
                            bool parsed = false;
                            string token = parts[i];

                            if (token.Contains(sep4[0])) // "//":  v//vn
                            {
                                string[] splitParts = token.Split(sep4, StringSplitOptions.RemoveEmptyEntries);
                                if (splitParts.Length > 0)
                                {
                                    parsed = int.TryParse(splitParts[0], out vertexId);
                                }
                            }
                            else if (token.Contains(sep3[0])) // "/": v/vt or v/vt/vn
                            {
                                string[] splitParts = token.Split(sep3, StringSplitOptions.None);
                                if (splitParts.Length > 0 && !string.IsNullOrEmpty(splitParts[0]))
                                {
                                    parsed = int.TryParse(splitParts[0], out vertexId);
                                }
                                if (splitParts.Length > 1 && !string.IsNullOrEmpty(splitParts[1]))
                                {
                                    if (int.TryParse(splitParts[1], out texCoordId))
                                    {
                                        texCoordId = texCoordId > 0 ? texCoordId - 1 : textureVertices.Count + texCoordId;
                                    }
                                    else
                                    {
                                        texCoordId = -1;
                                    }
                                }
                            }
                            else
                            {
                                parsed = int.TryParse(token, out vertexId);
                            }

                            if (parsed)
                            {
                                // Handle negative indices or convert to 0-based index
                                vertexId = vertexId > 0 ? vertexId - 1 : verticesById.Count + vertexId;
                                if (vertexId >= 0 && vertexId < verticesById.Count)
                                {
                                    face.vertexIds.Add(vertexId);
                                    face.texCoordIds.Add(texCoordId);
                                }
                                else
                                {
                                    validFace = false;
                                }
                            }
                            else
                            {
                                validFace = false;
                            }
                        }

                        if (validFace)
                        {
                            if (!allFaces.ContainsKey(currentMaterial))
                            {
                                allFaces.Add(currentMaterial, new List<FFace>());
                            }

                            allFaces[currentMaterial].Add(face);
                        }
                    }
                    line = reader.ReadLine();
                }

                bool allTriangularFaces = true;
                int newFaceIndex = 0;

                // Create one mesh per entry in faceList, as all faces will have the same material.
                foreach (KeyValuePair<string, List<FFace>> faceList in allFaces)
                {
                    bool approximatedFacesForMaterial = false;

                    foreach (FFace face in faceList.Value)
                    {
                        currentMaterial = faceList.Key;
                        var singleFaceIndices = new List<int>(face.vertexIds);

                        Material sourceMaterial = null;
                        materials.TryGetValue(currentMaterial, out sourceMaterial);
                        List<Vector2> faceUvs = CollectFaceUvs(face, textureVertices);
                        int fallbackMaterialId = TryGetMaterialId(currentMaterial);
                        FaceProperties properties = ResolveFaceProperties(
                          sourceMaterial,
                          faceUvs,
                          fallbackMaterialId,
                          ref approximatedFacesForMaterial);

                        var newFace = new Face(
                            newFaceIndex,
                            singleFaceIndices.AsReadOnly(),
                            verticesById,
                            properties
                        );
                        facesById.Add(newFaceIndex++, newFace);
                        if (singleFaceIndices.Count > 3) allTriangularFaces = false;
                    } // foreach face
                } // foreach facelist

                mmesh = new MMesh(id, Vector3.zero, Quaternion.identity, verticesById, facesById);
                CoplanarFaceMerger.MergeCoplanarFaces(mmesh);
                ApplyImportOrientationFix(mmesh);
            }
            return true;
        }

        private static List<Vector2> CollectFaceUvs(FFace face, List<Vector2> textureVertices)
        {
            List<Vector2> faceUvs = new List<Vector2>(face.texCoordIds.Count);
            foreach (int texCoordId in face.texCoordIds)
            {
                if (texCoordId >= 0 && texCoordId < textureVertices.Count)
                {
                    faceUvs.Add(textureVertices[texCoordId]);
                }
            }
            return faceUvs;
        }

        private static FaceProperties ResolveFaceProperties(
          Material material,
          List<Vector2> faceUvs,
          int fallbackMaterialId,
          ref bool approximatedFacesForMaterial)
        {
            if (material != null && faceUvs != null && faceUvs.Count > 0)
            {
                if (TextureToFaceColorApproximator.TrySampleAverageColor(material, faceUvs, out Color sampledColor))
                {
                    approximatedFacesForMaterial = true;
                    return new FaceProperties(MaterialRegistry.GetMaterialIdClosestToColor(sampledColor));
                }
            }

            // If we have a material but texture sampling failed or no UVs, use the material's base color
            if (material != null)
            {
                approximatedFacesForMaterial = true;
                return new FaceProperties(MaterialRegistry.GetMaterialIdClosestToColor(material.color));
            }

            return new FaceProperties(fallbackMaterialId);
        }

        private static Texture2D TryResolveTexture(
          string textureReference,
          string textureSearchDirectory,
          Dictionary<string, Texture2D> externalTextures)
        {
            if (string.IsNullOrEmpty(textureReference))
            {
                return null;
            }

            Texture2D resolvedTexture = null;
            if (externalTextures != null)
            {
                if (externalTextures.TryGetValue(textureReference, out resolvedTexture))
                {
                    return resolvedTexture;
                }

                string referenceFileName = Path.GetFileName(textureReference);
                if (!string.IsNullOrEmpty(referenceFileName))
                {
                    foreach (KeyValuePair<string, Texture2D> pair in externalTextures)
                    {
                        if (string.Equals(Path.GetFileName(pair.Key), referenceFileName, StringComparison.OrdinalIgnoreCase))
                        {
                            return pair.Value;
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(textureSearchDirectory))
            {
                return null;
            }

            string normalizedReference = textureReference.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            string candidatePath = Path.Combine(textureSearchDirectory, normalizedReference);
            if (!File.Exists(candidatePath))
            {
                string fallbackFileName = Path.GetFileName(normalizedReference);
                if (!string.IsNullOrEmpty(fallbackFileName))
                {
                    candidatePath = Path.Combine(textureSearchDirectory, fallbackFileName);
                }
            }

            if (!File.Exists(candidatePath))
            {
                return null;
            }

            try
            {
                byte[] data = File.ReadAllBytes(candidatePath);
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (texture.LoadImage(data, false))
                {
                    texture.name = Path.GetFileNameWithoutExtension(candidatePath);
                    texture.wrapMode = TextureWrapMode.Repeat;
                    return texture;
                }
                else
                {
                    UnityEngine.Object.Destroy(texture);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to load texture '{textureReference}': {e.Message}");
            }

            return null;
        }

        private static void AssignTexture(Material material, Texture2D texture)
        {
            if (material == null || texture == null)
            {
                return;
            }

            material.mainTexture = texture;
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
            }
            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
            }
        }

        private static List<MeshVerticesAndTriangles>
          BreakIntoMultipleMeshes(List<Vector3> meshVertices, List<int> triangles)
        {
            if (meshVertices.Count < 65000)
            {
                return new List<MeshVerticesAndTriangles>() {
          new MeshVerticesAndTriangles(meshVertices.ToArray(), triangles.ToArray())
        };
            }
            List<MeshVerticesAndTriangles> subMeshes = new List<MeshVerticesAndTriangles>();
            List<int> subMeshTriangles = new List<int>();
            List<Vector3> subMeshVertices = new List<Vector3>();
            Dictionary<int, int> triangleMapping = new Dictionary<int, int>();
            for (int i = 0; i < triangles.Count; i += 3)
            {
                int t1 = triangles[i];
                int t2 = triangles[i + 1];
                int t3 = triangles[i + 2];
                if (!triangleMapping.ContainsKey(t1))
                {
                    triangleMapping.Add(t1, subMeshVertices.Count);
                    subMeshVertices.Add(meshVertices[t1]);
                }
                if (!triangleMapping.ContainsKey(t2))
                {
                    triangleMapping.Add(t2, subMeshVertices.Count);
                    subMeshVertices.Add(meshVertices[t2]);
                }
                if (!triangleMapping.ContainsKey(t3))
                {
                    triangleMapping.Add(t3, subMeshVertices.Count);
                    subMeshVertices.Add(meshVertices[t3]);
                }
                subMeshTriangles.Add(triangleMapping[t1]);
                subMeshTriangles.Add(triangleMapping[t2]);
                subMeshTriangles.Add(triangleMapping[t3]);
                if (subMeshVertices.Count > 64000)
                {
                    subMeshes.Add(new MeshVerticesAndTriangles(subMeshVertices.ToArray(), subMeshTriangles.ToArray()));
                    subMeshVertices = new List<Vector3>();
                    subMeshTriangles = new List<int>();
                    triangleMapping.Clear();
                }
            }
            if (subMeshVertices.Count > 0)
            {
                subMeshes.Add(new MeshVerticesAndTriangles(subMeshVertices.ToArray(), subMeshTriangles.ToArray()));
            }
            return subMeshes;
        }

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
