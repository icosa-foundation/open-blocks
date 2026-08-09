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

namespace com.google.apps.peltzer.client.model.render
{
    /// <summary>
    ///   A place to get materials by id.
    /// </summary>
    public class MaterialRegistry
    {
        public enum MaterialType
        {
            PAPER,
            GEM,
            GLASS
        }

        // Taken from UI docs, starting at: go/oblinks
        public static int[] rawColors = {
      0xBA68C8,
      0x9C27B0,
      0x673AB7,
      0x80DEEA,
      0x00BCD4,
      0x039BE5,
      0xF8BBD0,
      0xF06292,
      0xF44336,
      0x8BC34A,
      0x4CAF50,
      0x009688,
      0xFFEB3B,
      0xFF9800,
      0xFF5722,
      0xCFD8DC,
      0x78909C,
      0x455A64,
      0xFFCC88,
      0xDD9944,
      0x795548,
      0xFFFFFF,
      0x9E9E9E,
      0x1A1A1A,
    };

        // Unity Materials with albedo set.
        private static Material[] materialsWithAlbedo = null;
        private static Dictionary<int, Material> customMaterialsWithAlbedo = null;

        // Our custom MaterialAndColor, with albedo unset, as we use vertex colours.
        private static MaterialAndColor[] materials = null;
        private static MaterialAndColor[] previewMaterials = null;
        private static MaterialAndColor[] highlightMaterials = null;
        private static Color32[] color32s = null;

        private static MaterialAndColor highlightSilhouetteMaterial;

        // Custom color support (arbitrary RGB colors beyond the fixed palette)
        private static Dictionary<int, Color32> customColors = null;
        private static Dictionary<Color32, int> colorToIdCache = null;
        private static Dictionary<int, MaterialAndColor> customPreviewMaterials =
          new Dictionary<int, MaterialAndColor>();
        private static Dictionary<int, MaterialAndColor> customHighlightMaterials =
          new Dictionary<int, MaterialAndColor>();
        private static int nextCustomId = CUSTOM_COLOR_START;

        // The material ID space, which the .blocks file format depends on:
        //   0-23    the legacy palette (rawColors)
        //   24-27   the special materials (glass, gem, and the two wireframes)
        //   28-999  unused, reserved for future fixed materials
        //   1000+   custom colors, allocated at runtime and stored per-file
        public const int CUSTOM_COLOR_START = 1000;

        public static int GLASS_ID = rawColors.Length;
        public static int GEM_ID = rawColors.Length + 1;
        public static int PINK_WIREFRAME_ID = rawColors.Length + 2;
        public static int GREEN_WIREFRAME_ID = rawColors.Length + 3;
        public static int HIGHLIGHT_SILHOUETTE_ID = rawColors.Length + 4;

        public static readonly int RED_ID = 8;
        public static readonly int DEEP_ORANGE_ID = 14;
        public static readonly int YELLOW_ID = 12;
        public static readonly int WHITE_ID = 21;
        public static readonly int BLACK_ID = 23;

        private static readonly string BASE_SHADER = "Universal Render Pipeline/Simple Lit";
        private static readonly int MultiplicitiveAlpha = Shader.PropertyToID("_MultiplicitiveAlpha");

        /// <summary>
        ///   Must be called before used.  Creates the materials for the given color codes.
        /// </summary>
        public static void init(MaterialLibrary materialLibrary)
        {
            Material baseMaterial = materialLibrary.baseMaterial;
            Material transparentMaterial = materialLibrary.transparentMaterial; // preview material
            Material glassMaterial = materialLibrary.glassMaterial;
            Material glassMaterialPalette = materialLibrary.glassMaterialPalette;
            Material gemMaterialFront = materialLibrary.gemMaterialFront;
            Material gemMaterialBack = materialLibrary.gemMaterialBack;
            Material subtractMaterial = materialLibrary.subtractMaterial;
            Material copyMaterial = materialLibrary.copyMaterial;
            materials = new MaterialAndColor[rawColors.Length + 4];
            materialsWithAlbedo = new Material[rawColors.Length + 4];
            previewMaterials = new MaterialAndColor[rawColors.Length + 4];
            color32s = new Color32[rawColors.Length + 4];
            Material templateMaterial =
              baseMaterial == null ? new Material(Shader.Find(BASE_SHADER)) : new Material(baseMaterial);
            for (int i = 0; i < rawColors.Length; i++)
            {
                materials[i] = new MaterialAndColor(templateMaterial, i);
                materials[i].color = new Color(r(rawColors[i]), g(rawColors[i]), b(rawColors[i]));
                color32s[i] = materials[i].color;
                materialsWithAlbedo[i] = new Material(Shader.Find(BASE_SHADER));
                materialsWithAlbedo[i].color = new Color(r(rawColors[i]), g(rawColors[i]), b(rawColors[i]));
                previewMaterials[i] = new MaterialAndColor(new Material(transparentMaterial), i);
                previewMaterials[i].color = new Color(r(rawColors[i]), g(rawColors[i]), b(rawColors[i]), /* alpha */ 1.0f);
                previewMaterials[i].material.SetFloat(MultiplicitiveAlpha, 0.3f);
            }
            // "Special" materials.
            materials[GLASS_ID] = new MaterialAndColor(glassMaterial, glassMaterial.color, GLASS_ID);
            //new MaterialAndColor(glassMaterial, materialLibrary.glassSpecMaterial, glassMaterial.color, GLASS_ID);
            materialsWithAlbedo[GLASS_ID] = new Material(glassMaterialPalette);
            // because the shader only does the one pass, we have to render the backfaces first, and then the frontfaces
            materials[GEM_ID] = new MaterialAndColor(gemMaterialFront, GEM_ID);
            materials[GEM_ID].material2 = gemMaterialBack;
            materialsWithAlbedo[GEM_ID] = materialLibrary.gemMaterialPaletteFront;
            materials[PINK_WIREFRAME_ID] = new MaterialAndColor(subtractMaterial, PINK_WIREFRAME_ID);
            materialsWithAlbedo[PINK_WIREFRAME_ID] = subtractMaterial;
            materials[GREEN_WIREFRAME_ID] = new MaterialAndColor(copyMaterial, GREEN_WIREFRAME_ID);
            materialsWithAlbedo[GREEN_WIREFRAME_ID] = copyMaterial;

            previewMaterials[GLASS_ID] = new MaterialAndColor(new Material(glassMaterial), GLASS_ID);
            previewMaterials[GEM_ID] = new MaterialAndColor(new Material(gemMaterialFront), GEM_ID);
            previewMaterials[GEM_ID].material2 = gemMaterialBack;

            Color old = previewMaterials[GEM_ID].color;
            previewMaterials[GEM_ID].color = new Color(old.r, old.g, old.b, 0.1f);
            highlightMaterials = new MaterialAndColor[materials.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                MaterialAndColor highlightedVersion = new MaterialAndColor(materials[i].material, i);
                Color32 highlightedVersionColor = highlightedVersion.color;
                Color originalColor = new Color(highlightedVersionColor.r, highlightedVersionColor.g, highlightedVersionColor.b, highlightedVersionColor.a);
                highlightedVersion.color = originalColor * (4.5f - originalColor.maxColorComponent * 3);
                highlightMaterials[i] = highlightedVersion;
            }
            highlightSilhouetteMaterial = new MaterialAndColor(materialLibrary.meshSelectMaterial,
              new Color32(255, 255, 255, 255), HIGHLIGHT_SILHOUETTE_ID);

            // Initialize custom color storage
            customColors = new Dictionary<int, Color32>();
            colorToIdCache = new Dictionary<Color32, int>();
            customMaterialsWithAlbedo = new Dictionary<int, Material>();
            customPreviewMaterials.Clear();
            customHighlightMaterials.Clear();
            nextCustomId = CUSTOM_COLOR_START;
        }

        private static float r(int raw)
        {
            return ((raw >> 16) & 255) / 255.0f;
        }

        private static float g(int raw)
        {
            return ((raw >> 8) & 255) / 255.0f;
        }

        private static float b(int raw)
        {
            return (raw & 255) / 255.0f;
        }

        /// <summary>
        ///   Get a MaterialAndColor given a materialId.
        ///   Supports both legacy palette materials (0-27) and custom colors (1000+).
        /// </summary>
        /// <param name="materialId">The material id.</param>
        /// <returns>A Material.</returns>
        public static MaterialAndColor GetMaterialAndColorById(int materialId)
        {
            // For tests, if we haven't been initialized, do it now.
            if (materials == null)
            {
                MaterialLibrary matLib = new MaterialLibrary();
                matLib.glassMaterial = new Material(matLib.glassMaterial);
                // matLib.glassSpecMaterial = new Material(matLib.transparentMaterial);
                matLib.gemMaterialFront = new Material(matLib.gemMaterialFront);
                matLib.gemMaterialBack = new Material(matLib.gemMaterialBack);
                matLib.copyMaterial = new Material(Shader.Find(BASE_SHADER));
                matLib.subtractMaterial = new Material(Shader.Find(BASE_SHADER));
                Debug.Log("initializing mats in wrong place - this is an error if a test isn't running.");
                init(matLib);
            }

            // Fast path: legacy palette materials
            if (materialId >= 0 && materialId < materials.Length)
            {
                return materials[materialId];
            }

            // Custom color: use base material with custom color
            if (customColors != null && customColors.TryGetValue(materialId, out Color32 color))
            {
                return new MaterialAndColor(materials[0].material, color, materialId);
            }

            // Fallback: return first material (for backwards compatibility with old files)
            Debug.LogWarning($"Unknown material ID: {materialId}, returning default material");
            return materials[0];
        }

        /// <summary>
        ///   Get the Material id closest to a given color.
        /// </summary>
        /// <param name="color">The color.</param>
        /// <returns>A materialId.</returns>
        public static int GetMaterialIdClosestToColor(Color color)
        {
            // Find the similarity to each color in rawColors
            float minDistance = float.MaxValue;
            int closestMaterialId = 0;
            for (int i = 0; i < rawColors.Length; i++)
            {
                Color materialColor = new Color(r(rawColors[i]), g(rawColors[i]), b(rawColors[i]));
                Color colorDiff = (color - materialColor);
                float distance = new Vector3(colorDiff.r, colorDiff.g, colorDiff.b).sqrMagnitude;
                if (distance < 0.0001f)
                {
                    closestMaterialId = i;
                    break;
                }
                else if (distance < minDistance)
                {
                    minDistance = distance;
                    closestMaterialId = i;
                }
            }
            return closestMaterialId;
        }

        /// <summary>
        ///   Get a Material's color given a materialId.
        ///   Supports both legacy palette materials and custom colors.
        /// </summary>
        /// <param name="materialId">The material id.</param>
        /// <returns>A Color.</returns>
        public static Color GetMaterialColorById(int materialId)
        {
            // Legacy palette colors
            if (materialId >= 0 && materialId < rawColors.Length)
            {
                return new Color(r(rawColors[materialId]), g(rawColors[materialId]), b(rawColors[materialId]));
            }

            // Special materials (glass, gem, etc.)
            if (materialId >= 0 && materialId < color32s.Length)
            {
                Color32 c = color32s[materialId];
                return new Color(c.r / 255f, c.g / 255f, c.b / 255f, c.a / 255f);
            }

            // Custom colors
            if (customColors != null && customColors.TryGetValue(materialId, out Color32 color))
            {
                return new Color(color.r / 255f, color.g / 255f, color.b / 255f, color.a / 255f);
            }

            // Fallback: white
            return new Color(1f, 1f, 1f, 1f);
        }

        /// <summary>
        ///   Get a Material's color given a materialId as Color32.
        ///   Supports both legacy palette materials and custom colors.
        /// </summary>
        /// <param name="materialId">The material id.</param>
        /// <returns>A Color32.</returns>
        public static Color32 GetMaterialColor32ById(int materialId)
        {
            // Fast path: legacy palette (includes special materials). color32s is null until init() runs, which
            // needs a MaterialLibrary; custom colours don't, so fall through rather than throwing.
            if (color32s != null && materialId >= 0 && materialId < color32s.Length)
            {
                return color32s[materialId];
            }

            // Custom colors
            if (customColors != null && customColors.TryGetValue(materialId, out Color32 color))
            {
                return color;
            }

            // Fallback: white
            return new Color32(255, 255, 255, 255);
        }

        /// <summary>
        ///   Get a Material, with albedo already set, given a materialId.
        /// </summary>
        /// <param name="materialId">The material id.</param>
        /// <returns>A Material with .color set.</returns>
        public static Material GetMaterialWithAlbedoById(int materialId)
        {
            if (materialId >= 0 && materialId < materialsWithAlbedo.Length)
            {
                return materialsWithAlbedo[materialId];
            }

            if (customColors != null && customColors.TryGetValue(materialId, out Color32 color))
            {
                if (customMaterialsWithAlbedo == null)
                {
                    customMaterialsWithAlbedo = new Dictionary<int, Material>();
                }

                if (!customMaterialsWithAlbedo.TryGetValue(materialId, out Material material))
                {
                    material = new Material(materialsWithAlbedo[0]);
                    material.color = color;
                    customMaterialsWithAlbedo[materialId] = material;
                }
                return material;
            }

            Debug.LogWarning($"Unknown material ID: {materialId}, returning default albedo material");
            return materialsWithAlbedo[0];
        }

        /// <summary>
        ///   Get a preview version (low alpha) of a Material given a materialId.
        /// </summary>
        /// <param name="materialId">The material id.</param>
        /// <returns>A Material.</returns>
        public static MaterialAndColor GetPreviewOfMaterialById(int materialId)
        {
            // For tests, if we haven't been initialized, do it now.
            if (materials == null)
            {
                MaterialLibrary matLib = new MaterialLibrary();
                matLib.glassMaterial = new Material(matLib.glassMaterial);
                // matLib.glassSpecMaterial = new Material(matLib.transparentMaterial);
                matLib.gemMaterialFront = new Material(matLib.gemMaterialFront);
                matLib.gemMaterialBack = new Material(matLib.gemMaterialBack);
                matLib.copyMaterial = new Material(Shader.Find(BASE_SHADER)); // TODO
                matLib.subtractMaterial = new Material(Shader.Find(BASE_SHADER)); // TODO
                Debug.Log("initializing mats in wrong place - this is an error if a test isn't running.");
                init(matLib);
            }
            if (materialId >= 0 && materialId < previewMaterials.Length)
            {
                return previewMaterials[materialId];
            }

            if (customColors != null && customColors.TryGetValue(materialId, out Color32 customColor))
            {
                if (!customPreviewMaterials.TryGetValue(materialId, out MaterialAndColor preview))
                {
                    // Mirrors the palette setup in init(): a dedicated clone of the transparent material, since
                    // callers mutate the returned material's alpha, plus the colour carried on the wrapper.
                    preview = new MaterialAndColor(
                      new Material(previewMaterials[0].material), customColor, materialId);
                    preview.material.SetFloat(MultiplicitiveAlpha, 0.3f);
                    customPreviewMaterials[materialId] = preview;
                }
                return preview;
            }

            Debug.LogWarning($"Unknown material ID: {materialId}, returning default preview material");
            return previewMaterials[0];
        }

        /// <summary>
        ///   Get the material we should show when a user is attempting to make an invalid reshape operation.
        /// </summary>
        /// <returns>A Material.</returns>
        public static MaterialAndColor GetReshaperErrorMaterial()
        {
            return previewMaterials[8]; // Red.
        }

        /// <summary>
        /// Gets a highlighted version of the material (brightened).
        /// </summary>
        /// <param name="materialId">The material id.</param>
        /// <returns>A material.</returns>
        public static MaterialAndColor GetHighlightMaterialById(int materialId)
        {
            // For tests, if we haven't been initialized, do it now.
            if (highlightMaterials == null)
            {
                MaterialLibrary matLib = new MaterialLibrary();
                matLib.glassMaterial = new Material(matLib.glassMaterial);
                // matLib.glassSpecMaterial = new Material(matLib.transparentMaterial);
                matLib.gemMaterialFront = new Material(matLib.gemMaterialFront);
                matLib.gemMaterialBack = new Material(matLib.gemMaterialBack);
                matLib.copyMaterial = new Material(Shader.Find(BASE_SHADER));
                matLib.subtractMaterial = new Material(Shader.Find(BASE_SHADER));
                Debug.Log("initializing mats in wrong place - this is an error if a test isn't running.");
                init(matLib);
            }
            if (materialId >= 0 && materialId < highlightMaterials.Length)
            {
                return highlightMaterials[materialId];
            }

            if (customColors != null && customColors.ContainsKey(materialId))
            {
                if (!customHighlightMaterials.TryGetValue(materialId, out MaterialAndColor highlight))
                {
                    // Built exactly as init() builds the palette's highlight materials, so custom colours and
                    // palette colours behave identically here.
                    highlight = new MaterialAndColor(materials[0].material, materialId);
                    Color32 highlightColor = highlight.color;
                    Color originalColor = new Color(
                      highlightColor.r, highlightColor.g, highlightColor.b, highlightColor.a);
                    highlight.color = originalColor * (4.5f - originalColor.maxColorComponent * 3);
                    customHighlightMaterials[materialId] = highlight;
                }
                return highlight;
            }

            Debug.LogWarning($"Unknown material ID: {materialId}, returning default highlight material");
            return highlightMaterials[0];
        }

        public static MaterialAndColor GetHighlightSilhouetteMaterial()
        {
            return highlightSilhouetteMaterial;
        }

        /// <summary>
        ///   Get the number of materials we support.
        /// </summary>
        /// <returns></returns>
        public static int GetNumMaterials()
        {
            return materials.Length;
        }

        /// <summary>
        ///   Returns true if the material is transparent.  This may be needed to render things correctly.
        /// </summary>
        public static bool IsMaterialTransparent(int materialId)
        {
            return materialId == GLASS_ID || materialId == GEM_ID;
        }

        public static MaterialType GetMaterialType(int id)
        {
            // Can't use a switch statement because GEM_ID and GLASS_ID aren't real constants.
            if (id == GEM_ID)
            {
                return MaterialType.GEM;
            }
            if (id == GLASS_ID)
            {
                return MaterialType.GLASS;
            }
            return MaterialType.PAPER;
        }

        // ========== Custom Color Support Methods ==========

        /// <summary>
        ///   Gets or creates a material ID for the given color.
        ///   If the color matches an existing palette or custom color, returns that ID.
        ///   Otherwise, creates a new custom color entry.
        /// </summary>
        /// <param name="color">The color to get or create a material ID for.</param>
        /// <returns>A material ID (palette ID or new custom ID).</returns>
        public static int GetOrCreateMaterialId(Color32 color)
        {
            // Check legacy palette (exact match)
            if (color32s != null)
            {
                for (int i = 0; i < rawColors.Length; i++)
                {
                    if (ColorsEqual(color32s[i], color))
                    {
                        return i;
                    }
                }
            }

            // Ensure custom color storage is initialized
            if (customColors == null || colorToIdCache == null)
            {
                Debug.LogWarning("Custom color storage not initialized, initializing now");
                customColors = new Dictionary<int, Color32>();
                colorToIdCache = new Dictionary<Color32, int>();
                nextCustomId = CUSTOM_COLOR_START;
            }

            // Check existing custom colors
            if (colorToIdCache.TryGetValue(color, out int existingId))
            {
                return existingId;
            }

            // Create new custom color
            int newId = nextCustomId++;
            customColors[newId] = color;
            colorToIdCache[color] = newId;

            return newId;
        }

        /// <summary>
        ///   Helper method to compare two Color32 values for equality.
        /// </summary>
        private static bool ColorsEqual(Color32 a, Color32 b)
        {
            return a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
        }

        /// <summary>
        ///   Checks if a material ID is a custom color (1000+).
        /// </summary>
        /// <param name="materialId">The material ID to check.</param>
        /// <returns>True if the ID is a custom color.</returns>
        public static bool IsCustomMaterialId(int materialId)
        {
            return materialId >= CUSTOM_COLOR_START;
        }

        /// <summary>
        ///   Gets the total number of custom colors currently registered.
        /// </summary>
        /// <returns>The count of custom colors.</returns>
        public static int GetCustomColorCount()
        {
            return customColors != null ? customColors.Count : 0;
        }

        /// <summary>
        ///   Registers a custom material with a specific ID.
        ///   Used during deserialization to restore custom colors.
        /// </summary>
        /// <param name="materialId">The material ID to register.</param>
        /// <param name="color">The color to associate with this ID.</param>
        public static int RegisterCustomMaterial(int materialId, Color32 color)
        {
            if (materialId < CUSTOM_COLOR_START)
            {
                Debug.LogError($"Attempted to register custom material with invalid ID: {materialId}");
                return materialId;
            }

            // Ensure storage is initialized
            if (customColors == null || colorToIdCache == null)
            {
                customColors = new Dictionary<int, Color32>();
                colorToIdCache = new Dictionary<Color32, int>();
                nextCustomId = CUSTOM_COLOR_START;
            }

            if (colorToIdCache.TryGetValue(color, out int existingColorId))
            {
                return existingColorId;
            }

            if (customColors.ContainsKey(materialId))
            {
                while (customColors.ContainsKey(nextCustomId))
                {
                    nextCustomId++;
                }
                materialId = nextCustomId++;
            }

            // Add to registry without overwriting a color loaded from another file.
            customColors[materialId] = color;
            colorToIdCache[color] = materialId;

            // Update next ID if necessary
            if (materialId >= nextCustomId)
            {
                nextCustomId = materialId + 1;
            }

            return materialId;
        }

        /// <summary>
        ///   Clears all custom colors, destroying the Unity objects created for them.
        ///
        ///   Only safe to call when nothing still references a custom material ID: any surviving mesh would
        ///   fall back to the default material. Callers are responsible for that ordering.
        /// </summary>
        public static void ClearCustomColors()
        {
            if (customColors != null)
            {
                customColors.Clear();
            }
            if (colorToIdCache != null)
            {
                colorToIdCache.Clear();
            }
            // The lazily built Unity materials below are owned by this registry, so they have to be destroyed
            // rather than just dropped - Unity objects are not reclaimed by the garbage collector.
            if (customMaterialsWithAlbedo != null)
            {
                foreach (Material material in customMaterialsWithAlbedo.Values)
                {
                    DestroyMaterial(material);
                }
                customMaterialsWithAlbedo.Clear();
            }
            foreach (MaterialAndColor preview in customPreviewMaterials.Values)
            {
                DestroyMaterial(preview.material);
            }
            customPreviewMaterials.Clear();
            // Highlight entries share the palette's template material, so there is nothing of ours to destroy.
            customHighlightMaterials.Clear();
            nextCustomId = CUSTOM_COLOR_START;
        }

        /// <summary>
        ///   Destroys a material we created, using the immediate variant outside of play mode so that editor
        ///   tests do not leak it.
        /// </summary>
        private static void DestroyMaterial(Material material)
        {
            if (material == null) return;
            if (Application.isPlaying)
            {
                Object.Destroy(material);
            }
            else
            {
                Object.DestroyImmediate(material);
            }
        }
    }
}
