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
using System.Text;

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
            List<MMesh> results;
            bool success = MMeshesFromObjFile(objFileContents, mtlFileContents, id, out results, textureSearchDirectory, externalTextures);
            if (success && results.Count > 0)
            {
                result = results[0];
                return true;
            }
            result = null;
            return false;
        }

        public static bool MMeshesFromObjFile(
          string objFileContents,
          string mtlFileContents,
          int baseId,
          out List<MMesh> results,
          string textureSearchDirectory = null,
          Dictionary<string, Texture2D> externalTextures = null)
        {
            results = new List<MMesh>();
            Dictionary<string, Material> materials = ImportMaterials(mtlFileContents, textureSearchDirectory, externalTextures);

            // Split OBJ by groups
            Dictionary<string, string> groupedObjContents = SplitObjByGroups(objFileContents);

            int meshId = baseId;
            foreach (var kvp in groupedObjContents)
            {
                if (ImportMeshes(meshId++, kvp.Value, materials, out MMesh mmesh))
                {
                    results.Add(mmesh);
                }
            }

            return results.Count > 0;
        }

        /// <summary>
        /// Data structure to hold parsed OBJ file components
        /// </summary>
        private class ObjData
        {
            public List<string> vertexLines = new List<string>();      // "v x y z"
            public List<string> texCoordLines = new List<string>();    // "vt u v"
            public List<string> normalLines = new List<string>();      // "vn x y z"
            public Dictionary<string, List<string>> groupFaceLines = new Dictionary<string, List<string>>();
            public Dictionary<string, List<string>> groupMaterialLines = new Dictionary<string, List<string>>();
        }

        /// <summary>
        /// Index remapping for vertices, texture coordinates, and normals
        /// </summary>
        private class IndexRemapping
        {
            public Dictionary<int, int> vertexRemap = new Dictionary<int, int>();
            public Dictionary<int, int> texCoordRemap = new Dictionary<int, int>();
            public Dictionary<int, int> normalRemap = new Dictionary<int, int>();
        }

        /// <summary>
        /// Splits OBJ file content by groups, remapping vertex indices for each group
        /// </summary>
        private static Dictionary<string, string> SplitObjByGroups(string objFileContents)
        {
            // Parse the OBJ file into structured data
            ObjData data = ParseObjData(objFileContents);

            // If no groups found, return entire OBJ as single mesh
            if (data.groupFaceLines.Count == 0)
            {
                Dictionary<string, string> singleGroup = new Dictionary<string, string>();
                singleGroup["Mesh"] = objFileContents;
                return singleGroup;
            }

            // Analyze which vertices/texCoords/normals each group uses
            Dictionary<string, HashSet<int>> vertexUsage = AnalyzeIndexUsage(data.groupFaceLines, 0);
            Dictionary<string, HashSet<int>> texCoordUsage = AnalyzeIndexUsage(data.groupFaceLines, 1);
            Dictionary<string, HashSet<int>> normalUsage = AnalyzeIndexUsage(data.groupFaceLines, 2);

            // Generate remapped OBJ content for each group
            return GenerateGroupedObjContent(data, vertexUsage, texCoordUsage, normalUsage);
        }

        /// <summary>
        /// Parse OBJ file into structured data
        /// </summary>
        private static ObjData ParseObjData(string objFileContents)
        {
            ObjData data = new ObjData();
            string currentGroup = null;
            string currentMaterial = null;

            using (StringReader reader = new StringReader(objFileContents))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                    {
                        continue;
                    }

                    if (trimmed.StartsWith("v "))
                    {
                        data.vertexLines.Add(trimmed);
                    }
                    else if (trimmed.StartsWith("vt "))
                    {
                        data.texCoordLines.Add(trimmed);
                    }
                    else if (trimmed.StartsWith("vn "))
                    {
                        data.normalLines.Add(trimmed);
                    }
                    else if (trimmed.StartsWith("g ") || trimmed.StartsWith("o "))
                    {
                        // Extract group name (everything after "g " or "o ")
                        currentGroup = trimmed.Substring(2).Trim();
                        if (string.IsNullOrEmpty(currentGroup))
                        {
                            currentGroup = "UnnamedGroup";
                        }
                        currentMaterial = null; // Reset material when entering new group
                    }
                    else if (trimmed.StartsWith("usemtl "))
                    {
                        currentMaterial = trimmed;
                    }
                    else if (trimmed.StartsWith("f "))
                    {
                        // Assign to group (or "DefaultGroup" if no group declared)
                        if (currentGroup == null)
                        {
                            currentGroup = "DefaultGroup";
                        }

                        if (!data.groupFaceLines.ContainsKey(currentGroup))
                        {
                            data.groupFaceLines[currentGroup] = new List<string>();
                            data.groupMaterialLines[currentGroup] = new List<string>();
                        }

                        // Add material declaration before first face that uses it
                        if (currentMaterial != null && !data.groupMaterialLines[currentGroup].Contains(currentMaterial))
                        {
                            data.groupMaterialLines[currentGroup].Add(currentMaterial);
                        }

                        data.groupFaceLines[currentGroup].Add(trimmed);
                    }
                }
            }

            return data;
        }

        /// <summary>
        /// Analyze which indices (vertex/texCoord/normal) are used by each group
        /// </summary>
        /// <param name="groupFaceLines">Face lines per group</param>
        /// <param name="indexPosition">0=vertex, 1=texCoord, 2=normal</param>
        private static Dictionary<string, HashSet<int>> AnalyzeIndexUsage(
            Dictionary<string, List<string>> groupFaceLines,
            int indexPosition)
        {
            Dictionary<string, HashSet<int>> usage = new Dictionary<string, HashSet<int>>();

            foreach (var kvp in groupFaceLines)
            {
                string group = kvp.Key;
                usage[group] = new HashSet<int>();

                foreach (string faceLine in kvp.Value)
                {
                    var indices = ParseFaceIndices(faceLine, indexPosition);
                    foreach (int idx in indices)
                    {
                        if (idx >= 0) // -1 means not present
                        {
                            usage[group].Add(idx);
                        }
                    }
                }
            }

            return usage;
        }

        /// <summary>
        /// Parse face line and extract indices at specified position
        /// </summary>
        /// <param name="faceLine">Face line like "f 1/2/3 4/5/6 7/8/9"</param>
        /// <param name="indexPosition">0=vertex, 1=texCoord, 2=normal</param>
        private static List<int> ParseFaceIndices(string faceLine, int indexPosition)
        {
            List<int> indices = new List<int>();
            string[] parts = faceLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 1; i < parts.Length; i++) // Skip "f" at index 0
            {
                string token = parts[i];

                // Handle different face formats: "v", "v/vt", "v/vt/vn", "v//vn"
                if (token.Contains("/"))
                {
                    string[] splitParts = token.Split('/');
                    if (indexPosition < splitParts.Length && !string.IsNullOrEmpty(splitParts[indexPosition]))
                    {
                        if (int.TryParse(splitParts[indexPosition], out int idx))
                        {
                            // Convert to 0-based index (OBJ is 1-indexed)
                            indices.Add(idx > 0 ? idx - 1 : idx);
                        }
                    }
                    else
                    {
                        indices.Add(-1); // Not present
                    }
                }
                else if (indexPosition == 0) // Only vertex index present
                {
                    if (int.TryParse(token, out int idx))
                    {
                        indices.Add(idx > 0 ? idx - 1 : idx);
                    }
                }
                else
                {
                    indices.Add(-1); // Not present
                }
            }

            return indices;
        }

        /// <summary>
        /// Generate remapped OBJ content for each group
        /// </summary>
        private static Dictionary<string, string> GenerateGroupedObjContent(
            ObjData data,
            Dictionary<string, HashSet<int>> vertexUsage,
            Dictionary<string, HashSet<int>> texCoordUsage,
            Dictionary<string, HashSet<int>> normalUsage)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();

            foreach (var kvp in data.groupFaceLines)
            {
                string group = kvp.Key;
                StringBuilder content = new StringBuilder();

                // Build index remapping
                IndexRemapping remap = BuildIndexRemapping(
                    vertexUsage[group],
                    texCoordUsage[group],
                    normalUsage[group]);

                // Add remapped vertices
                foreach (int oldIdx in vertexUsage[group].OrderBy(x => x))
                {
                    if (oldIdx >= 0 && oldIdx < data.vertexLines.Count)
                    {
                        content.AppendLine(data.vertexLines[oldIdx]);
                    }
                }

                // Add remapped texture coordinates
                if (texCoordUsage[group].Count > 0)
                {
                    foreach (int oldIdx in texCoordUsage[group].OrderBy(x => x))
                    {
                        if (oldIdx >= 0 && oldIdx < data.texCoordLines.Count)
                        {
                            content.AppendLine(data.texCoordLines[oldIdx]);
                        }
                    }
                }

                // Add remapped normals
                if (normalUsage[group].Count > 0)
                {
                    foreach (int oldIdx in normalUsage[group].OrderBy(x => x))
                    {
                        if (oldIdx >= 0 && oldIdx < data.normalLines.Count)
                        {
                            content.AppendLine(data.normalLines[oldIdx]);
                        }
                    }
                }

                // Add material declarations
                if (data.groupMaterialLines.ContainsKey(group))
                {
                    foreach (string mtlLine in data.groupMaterialLines[group])
                    {
                        content.AppendLine(mtlLine);
                    }
                }

                // Add remapped faces
                foreach (string faceLine in kvp.Value)
                {
                    string remapped = RemapFaceLine(faceLine, remap);
                    content.AppendLine(remapped);
                }

                result[group] = content.ToString();
            }

            return result;
        }

        /// <summary>
        /// Build index remapping dictionaries (old index -> new index)
        /// </summary>
        private static IndexRemapping BuildIndexRemapping(
            HashSet<int> vertexIndices,
            HashSet<int> texCoordIndices,
            HashSet<int> normalIndices)
        {
            IndexRemapping remap = new IndexRemapping();

            int newIdx = 0;
            foreach (int oldIdx in vertexIndices.OrderBy(x => x))
            {
                remap.vertexRemap[oldIdx] = newIdx++;
            }

            newIdx = 0;
            foreach (int oldIdx in texCoordIndices.OrderBy(x => x))
            {
                if (oldIdx >= 0)
                {
                    remap.texCoordRemap[oldIdx] = newIdx++;
                }
            }

            newIdx = 0;
            foreach (int oldIdx in normalIndices.OrderBy(x => x))
            {
                if (oldIdx >= 0)
                {
                    remap.normalRemap[oldIdx] = newIdx++;
                }
            }

            return remap;
        }

        /// <summary>
        /// Remap face line indices to new values
        /// </summary>
        private static string RemapFaceLine(string faceLine, IndexRemapping remap)
        {
            string[] parts = faceLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            StringBuilder result = new StringBuilder("f");

            for (int i = 1; i < parts.Length; i++) // Skip "f" at index 0
            {
                string token = parts[i];
                result.Append(" ");

                if (token.Contains("/"))
                {
                    string[] indices = token.Split('/');

                    // Remap vertex index
                    if (indices.Length > 0 && !string.IsNullOrEmpty(indices[0]))
                    {
                        int vIdx = int.Parse(indices[0]);
                        vIdx = vIdx > 0 ? vIdx - 1 : vIdx; // Convert to 0-based
                        int newVIdx = remap.vertexRemap[vIdx] + 1; // Back to 1-based
                        result.Append(newVIdx);
                    }

                    result.Append("/");

                    // Remap texture coordinate index
                    if (indices.Length > 1 && !string.IsNullOrEmpty(indices[1]))
                    {
                        int tIdx = int.Parse(indices[1]);
                        tIdx = tIdx > 0 ? tIdx - 1 : tIdx; // Convert to 0-based
                        if (remap.texCoordRemap.ContainsKey(tIdx))
                        {
                            int newTIdx = remap.texCoordRemap[tIdx] + 1; // Back to 1-based
                            result.Append(newTIdx);
                        }
                    }

                    // Remap normal index
                    if (indices.Length > 2)
                    {
                        result.Append("/");
                        if (!string.IsNullOrEmpty(indices[2]))
                        {
                            int nIdx = int.Parse(indices[2]);
                            nIdx = nIdx > 0 ? nIdx - 1 : nIdx; // Convert to 0-based
                            if (remap.normalRemap.ContainsKey(nIdx))
                            {
                                int newNIdx = remap.normalRemap[nIdx] + 1; // Back to 1-based
                                result.Append(newNIdx);
                            }
                        }
                    }
                }
                else
                {
                    // Simple vertex-only format
                    int vIdx = int.Parse(token);
                    vIdx = vIdx > 0 ? vIdx - 1 : vIdx; // Convert to 0-based
                    int newVIdx = remap.vertexRemap[vIdx] + 1; // Back to 1-based
                    result.Append(newVIdx);
                }
            }

            return result.ToString();
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
