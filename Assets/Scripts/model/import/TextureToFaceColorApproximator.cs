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

using System;
using System.Collections.Generic;
using UnityEngine;
using com.google.apps.peltzer.client.model.core;

namespace com.google.apps.peltzer.client.model.import
{
    internal static class TextureToFaceColorApproximator
    {
        // Maximum texture dimension for readable copies (larger textures are downscaled to save memory)
        private const int MAX_READABLE_DIMENSION = 1024;

        // Target coverage: sample approximately once per 256 texels (16x16 pixel area)
        // Balances quality (capturing texture detail) vs performance (fewer samples)
        private const float TARGET_TEXELS_PER_SAMPLE = 256f;

        // Minimum samples per face ensures we capture color variation even on small faces
        // 3 samples = one near each vertex of the triangle
        private const int MIN_SAMPLES_PER_FACE = 3;

        // Maximum samples per face caps computation cost on large faces
        // 9 samples provides good coverage without excessive overhead
        private const int MAX_SAMPLES_PER_FACE = 9;

        // Minimum triangle area in UV space (below this, treat as degenerate/point)
        private const float MIN_TRIANGLE_AREA = 1e-6f;

        // Hash bias for generating pseudo-random sample distribution (golden ratio hash constant)
        // Used to decorrelate sample positions across different faces
        private const int HASH_BIAS = unchecked((int)0x9E3779B9);

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private static readonly Dictionary<Texture, Texture2D> readableTextureCache =
            new Dictionary<Texture, Texture2D>();

        private static readonly HashSet<int> debuggedTextures = new HashSet<int>();

        /// <summary>
        /// Clears the texture cache and destroys cached readable texture copies.
        /// Call this after completing texture import operations to free memory.
        /// </summary>
        public static void ClearCache()
        {
            foreach (var pair in readableTextureCache)
            {
                if (pair.Value != null && pair.Key != pair.Value)
                {
                    // Only destroy textures we created (readable copies), not source textures
                    UnityEngine.Object.Destroy(pair.Value);
                }
            }
            readableTextureCache.Clear();
        }

        public static bool TryComputeFaceColors(
            Mesh mesh,
            int[] triangles,
            Material material,
            out List<Color> faceColors,
            out string debugMessage)
        {
            faceColors = null;
            debugMessage = null;

            if (mesh == null || triangles == null || triangles.Length == 0 || material == null)
            {
                return false;
            }

            Vector2[] uvs = mesh.uv;
            Color[] vertexColors = mesh.colors;
            bool hasVertexColors = vertexColors != null && vertexColors.Length > 0;

            // If we have vertex colors but no UVs and no texture, use vertex colors only
            if (hasVertexColors && (uvs == null || uvs.Length == 0))
            {
                if (!TryGetReadableTexture(material, out _, out _))
                {
                    // No texture and no UVs - compute face colors from vertex colors
                    return TryComputeFaceColorsFromVertexColors(triangles, vertexColors, out faceColors, out debugMessage);
                }
            }

            // If no UVs, we can't sample texture
            if (uvs == null || uvs.Length == 0)
            {
                return false;
            }

            if (!TryGetReadableTexture(material, out Texture2D readableTexture, out Color materialTint))
            {
                // No texture available - if we have vertex colors, use them
                if (hasVertexColors)
                {
                    return TryComputeFaceColorsFromVertexColors(triangles, vertexColors, out faceColors, out debugMessage);
                }
                return false;
            }

            int faceCount = triangles.Length / 3;
            List<Color> colors = new List<Color>(faceCount);
            int meshSeed = CombineHash(mesh.GetInstanceID(), faceCount);
            int faceIndex = 0;
            for (int i = 0; i < triangles.Length; i += 3, faceIndex++)
            {
                int v0 = triangles[i];
                int v1 = triangles[i + 1];
                int v2 = triangles[i + 2];

                if (!IsValidUvIndex(uvs, v0) || !IsValidUvIndex(uvs, v1) || !IsValidUvIndex(uvs, v2))
                {
                    colors = null;
                    return false;
                }

                Color sampled = SampleFaceColor(
                    readableTexture,
                    materialTint,
                    uvs[v0],
                    uvs[v1],
                    uvs[v2],
                    CombineHash(meshSeed, faceIndex)
                );

                // Multiply by vertex color if present (matches standard material rendering)
                if (hasVertexColors && IsValidUvIndex(vertexColors, v0) && IsValidUvIndex(vertexColors, v1) && IsValidUvIndex(vertexColors, v2))
                {
                    Color avgVertexColor = (vertexColors[v0] + vertexColors[v1] + vertexColors[v2]) / 3f;
                    sampled *= avgVertexColor;
                }

                colors.Add(sampled);
            }

            faceColors = colors;
            debugMessage = hasVertexColors
                ? $"Synthesized {faceCount} face colours from texture '{readableTexture.name}' multiplied by vertex colors"
                : $"Synthesized {faceCount} face colours from texture '{readableTexture.name}'";
            return true;
        }

        private static bool TryComputeFaceColorsFromVertexColors(
            int[] triangles,
            Color[] vertexColors,
            out List<Color> faceColors,
            out string debugMessage)
        {
            faceColors = null;
            debugMessage = null;

            if (triangles == null || triangles.Length == 0 || vertexColors == null || vertexColors.Length == 0)
            {
                return false;
            }

            int faceCount = triangles.Length / 3;
            List<Color> colors = new List<Color>(faceCount);

            for (int i = 0; i < triangles.Length; i += 3)
            {
                int v0 = triangles[i];
                int v1 = triangles[i + 1];
                int v2 = triangles[i + 2];

                if (!IsValidUvIndex(vertexColors, v0) || !IsValidUvIndex(vertexColors, v1) || !IsValidUvIndex(vertexColors, v2))
                {
                    return false;
                }

                Color avgColor = (vertexColors[v0] + vertexColors[v1] + vertexColors[v2]) / 3f;
                colors.Add(avgColor);
            }

            faceColors = colors;
            debugMessage = $"Synthesized {faceCount} face colours from vertex colors";
            return true;
        }

        public static bool TrySampleAverageColor(
            Material material,
            IList<Vector2> faceUvs,
            out Color sampledColor)
        {
            sampledColor = Color.black;

            if (material == null || faceUvs == null || faceUvs.Count == 0)
            {
                return false;
            }

            if (!TryGetReadableTexture(material, out Texture2D readableTexture, out Color materialTint))
            {
                return false;
            }

            if (faceUvs.Count < 3)
            {
                sampledColor = Sample(readableTexture, materialTint, faceUvs[0]);
                return true;
            }

            Color accumulator = Color.black;
            float totalArea = 0f;
            Vector2 baseUv = faceUvs[0];
            int faceSeed = ComputeFaceSeed(faceUvs);
            for (int i = 1; i < faceUvs.Count - 1; i++)
            {
                Vector2 uv1 = faceUvs[i];
                Vector2 uv2 = faceUvs[i + 1];
                float triangleArea = ComputeTriangleArea(baseUv, uv1, uv2);
                if (triangleArea < MIN_TRIANGLE_AREA)
                {
                    continue;
                }

                int triangleSeed = CombineHash(faceSeed, i);
                Color sampled = SampleFaceColor(
                    readableTexture,
                    materialTint,
                    baseUv,
                    uv1,
                    uv2,
                    triangleSeed
                );
                accumulator += sampled * triangleArea;
                totalArea += triangleArea;
            }

            if (totalArea < MIN_TRIANGLE_AREA)
            {
                sampledColor = Sample(readableTexture, materialTint, faceUvs[0]);
                return true;
            }

            sampledColor = accumulator / totalArea;
            return true;
        }

        private static bool TryGetReadableTexture(
            Material material,
            out Texture2D readableTexture,
            out Color materialTint)
        {
            readableTexture = null;
            materialTint = GetMaterialTint(material);

            Texture sourceTexture = ExtractTexture(material);
            if (sourceTexture == null)
            {
                return false;
            }

            if (readableTextureCache.TryGetValue(sourceTexture, out Texture2D cached))
            {
                readableTexture = cached;
                return readableTexture != null;
            }

            Texture2D texture2D = sourceTexture as Texture2D;
            if (texture2D == null)
            {
                readableTextureCache[sourceTexture] = null;
                return false;
            }

            if (texture2D.isReadable && texture2D.width <= MAX_READABLE_DIMENSION && texture2D.height <= MAX_READABLE_DIMENSION)
            {
                readableTexture = texture2D;
                readableTextureCache[sourceTexture] = readableTexture;
                return true;
            }

            Texture2D readableCopy = CreateReadableCopy(texture2D);
            readableTextureCache[sourceTexture] = readableCopy;
            readableTexture = readableCopy;
            return readableTexture != null;
        }

        private static Texture2D CreateReadableCopy(Texture2D texture)
        {
            try
            {
                int targetWidth = Mathf.Min(texture.width, MAX_READABLE_DIMENSION);
                int targetHeight = Mathf.Min(texture.height, MAX_READABLE_DIMENSION);

                RenderTextureFormat format = RenderTextureFormat.Default;
#if UNITY_EDITOR
                if (texture.format == TextureFormat.RGB565)
                {
                    format = RenderTextureFormat.RGB565;
                }
#endif
                RenderTexture temporary = RenderTexture.GetTemporary(
                    targetWidth,
                    targetHeight,
                    0,
                    format,
                    RenderTextureReadWrite.Default);

                Graphics.Blit(texture, temporary);

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = temporary;

                Texture2D readable = new Texture2D(targetWidth, targetHeight, TextureFormat.RGBA32, false);
                readable.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
                readable.Apply();
                readable.wrapMode = texture.wrapMode;
                readable.filterMode = texture.filterMode;
                readable.name = texture.name;

                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(temporary);

                return readable;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to create readable copy of texture '{texture?.name}': {e.Message}");
                return null;
            }
        }

        private static Texture ExtractTexture(Material material)
        {
            if (material == null)
            {
                return null;
            }

            if (material.HasTexture(BaseMapId))
            {
                return material.GetTexture(BaseMapId);
            }

            if (material.HasTexture(MainTexId))
            {
                return material.GetTexture(MainTexId);
            }

            return material.mainTexture;
        }

        private static bool HasTexture(this Material material, int propertyId)
        {
            return material != null && material.HasProperty(propertyId) && material.GetTexture(propertyId) != null;
        }

        private static Color SampleFaceColor(
            Texture2D texture,
            Color materialTint,
            Vector2 uv0,
            Vector2 uv1,
            Vector2 uv2,
            int faceSeed)
        {
            // Calculate triangle area using normalized UVs (for consistent area measurement)
            Vector2 uv0_normalized = NormalizeUV(uv0);
            Vector2 uv1_normalized = NormalizeUV(uv1);
            Vector2 uv2_normalized = NormalizeUV(uv2);
            float triangleArea = ComputeTriangleArea(uv0_normalized, uv1_normalized, uv2_normalized);

            if (triangleArea < MIN_TRIANGLE_AREA)
            {
                return Sample(texture, materialTint, uv0);
            }

            float texelArea = triangleArea * texture.width * texture.height;
            int sampleCount = DetermineSampleCount(texelArea);
            if (sampleCount <= 0)
            {
                return Sample(texture, materialTint, uv0);
            }

            Color accumulator = Color.black;
            for (int i = 0; i < sampleCount; i++)
            {
                Vector3 barycentric = GenerateBarycentricSample(i, sampleCount, faceSeed);
                // Interpolate in original UV space - Sample() will handle wrapping correctly
                Vector2 uv = barycentric.x * uv0 + barycentric.y * uv1 + barycentric.z * uv2;
                accumulator += Sample(texture, materialTint, uv);
            }

            return accumulator / sampleCount;
        }

        private static Vector2 NormalizeUV(Vector2 uv)
        {
            float u = uv.x % 1.0f;
            float v = uv.y % 1.0f;
            if (u < 0) u += 1.0f;
            if (v < 0) v += 1.0f;
            return new Vector2(u, v);
        }

        private static Color Sample(Texture2D texture, Color tint, Vector2 uv)
        {
            bool usePointSampling = texture.filterMode == FilterMode.Point;
            Color sampled;

            if (usePointSampling)
            {
                // Point sampling: normalize UV then use GetPixel (doesn't handle wrapping)
                Vector2 normalizedUV = NormalizeUV(uv);
                sampled = SampleNearestPixel(texture, normalizedUV);
            }
            else
            {
                // Bilinear sampling: pass UV directly - GetPixelBilinear handles wrapping correctly
                sampled = texture.GetPixelBilinear(uv.x, uv.y);
            }

            sampled *= tint;
            return sampled;
        }

        private static Color SampleNearestPixel(Texture2D texture, Vector2 uv)
        {
            int pixelX = Mathf.Clamp((int)(uv.x * texture.width), 0, texture.width - 1);
            int pixelY = Mathf.Clamp((int)(uv.y * texture.height), 0, texture.height - 1);

            Color result = texture.GetPixel(pixelX, pixelY);

            return result;
        }

        private static float ComputeTriangleArea(Vector2 uv0, Vector2 uv1, Vector2 uv2)
        {
            Vector2 edge0 = uv1 - uv0;
            Vector2 edge1 = uv2 - uv0;
            return Mathf.Abs(edge0.x * edge1.y - edge0.y * edge1.x) * 0.5f;
        }

        private static int DetermineSampleCount(float texelArea)
        {
            if (texelArea <= 0f)
            {
                return MIN_SAMPLES_PER_FACE;
            }

            float suggestedSamples = texelArea / TARGET_TEXELS_PER_SAMPLE;
            int sampleCount = Mathf.Max(MIN_SAMPLES_PER_FACE, Mathf.CeilToInt(suggestedSamples));
            return Mathf.Min(sampleCount, MAX_SAMPLES_PER_FACE);
        }

        private static Vector3 GenerateBarycentricSample(int sampleIndex, int sampleCount, int faceSeed)
        {
            float u = (sampleIndex + 0.5f) / sampleCount;
            uint hashedIndex = (uint)(CombineHash(faceSeed, sampleIndex) ^ HASH_BIAS);
            float v = RadicalInverseVanDerCorput(hashedIndex);

            float sqrtU = Mathf.Sqrt(u);
            float b0 = 1f - sqrtU;
            float b1 = v * sqrtU;
            float b2 = 1f - b0 - b1;

            return new Vector3(b0, b1, b2);
        }

        private static float RadicalInverseVanDerCorput(uint bits)
        {
            bits = (bits << 16) | (bits >> 16);
            bits = ((bits & 0x55555555u) << 1) | ((bits & 0xAAAAAAAAu) >> 1);
            bits = ((bits & 0x33333333u) << 2) | ((bits & 0xCCCCCCCCu) >> 2);
            bits = ((bits & 0x0F0F0F0Fu) << 4) | ((bits & 0xF0F0F0F0u) >> 4);
            bits = ((bits & 0x00FF00FFu) << 8) | ((bits & 0xFF00FF00u) >> 8);
            return bits * 2.3283064365386963e-10f;
        }

        private static int ComputeFaceSeed(IList<Vector2> faceUvs)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < faceUvs.Count; i++)
                {
                    hash = hash * 31 + faceUvs[i].GetHashCode();
                }
                return hash;
            }
        }

        private static int CombineHash(int seed, int value)
        {
            unchecked
            {
                return seed * 397 ^ value;
            }
        }

        private static bool IsValidUvIndex(Vector2[] uvs, int index)
        {
            return index >= 0 && index < uvs.Length;
        }

        private static bool IsValidUvIndex(Color[] colors, int index)
        {
            return index >= 0 && index < colors.Length;
        }

        private static Color GetMaterialTint(Material material)
        {
            if (material == null)
            {
                return Color.white;
            }

            if (material.HasProperty(BaseColorId))
            {
                return material.GetColor(BaseColorId);
            }

            if (material.HasProperty(ColorId))
            {
                return material.GetColor(ColorId);
            }

            return material.color;
        }
    }
}
