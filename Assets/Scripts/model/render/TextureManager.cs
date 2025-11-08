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
using com.google.apps.peltzer.client.model.core;
using UnityEngine;

namespace com.google.apps.peltzer.client.model.render
{
    /// <summary>
    /// Manages texture assets for the current model.
    /// Provides texture loading, caching, and retrieval for rendering.
    /// </summary>
    public class TextureManager
    {
        // Singleton instance
        private static TextureManager _instance;

        // Texture storage from the current PeltzerFile
        private Dictionary<int, TextureAsset> textureAssets;

        // Cache of Unity Texture2D objects for rendering
        // This is separate from TextureAsset's internal cache for better management
        private Dictionary<int, Texture2D> loadedTextures;

        private TextureManager()
        {
            textureAssets = new Dictionary<int, TextureAsset>();
            loadedTextures = new Dictionary<int, Texture2D>();
        }

        /// <summary>
        /// Gets the singleton instance of the TextureManager.
        /// </summary>
        public static TextureManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new TextureManager();
                }
                return _instance;
            }
        }

        /// <summary>
        /// Loads textures from a PeltzerFile.
        /// This should be called when a file is loaded.
        /// </summary>
        /// <param name="textures">Dictionary of texture assets from the file</param>
        public void LoadTexturesFromFile(Dictionary<int, TextureAsset> textures)
        {
            ClearAll();

            if (textures != null)
            {
                textureAssets = new Dictionary<int, TextureAsset>(textures);
                Debug.Log($"TextureManager: Loaded {textureAssets.Count} texture assets from file");
            }
        }

        /// <summary>
        /// Gets a Unity Texture2D for rendering.
        /// Textures are loaded and cached on first access.
        /// </summary>
        /// <param name="textureId">The texture ID to retrieve</param>
        /// <returns>Unity Texture2D or null if not found</returns>
        public Texture2D GetTexture(int textureId)
        {
            if (textureId == 0) return null; // 0 = no texture

            // Check if already loaded in cache
            if (loadedTextures.TryGetValue(textureId, out Texture2D cachedTexture))
            {
                if (cachedTexture != null) return cachedTexture;
            }

            // Try to load from asset
            if (textureAssets.TryGetValue(textureId, out TextureAsset asset))
            {
                Texture2D texture = asset.GetTexture2D();
                if (texture != null)
                {
                    loadedTextures[textureId] = texture;
                    return texture;
                }
                else
                {
                    Debug.LogWarning($"TextureManager: Failed to load texture {textureId} ({asset.name})");
                }
            }
            else
            {
                Debug.LogWarning($"TextureManager: Texture ID {textureId} not found in assets");
            }

            return null;
        }

        /// <summary>
        /// Adds a new texture asset to the manager.
        /// Used during import to register new textures.
        /// </summary>
        /// <param name="textureAsset">The texture asset to add</param>
        public void AddTexture(TextureAsset textureAsset)
        {
            if (textureAsset == null) return;

            textureAssets[textureAsset.id] = textureAsset;

            // Clear any cached version to force reload
            if (loadedTextures.ContainsKey(textureAsset.id))
            {
                loadedTextures.Remove(textureAsset.id);
            }

            Debug.Log($"TextureManager: Added texture {textureAsset.id} ({textureAsset.name})");
        }

        /// <summary>
        /// Gets the next available texture ID for creating new textures.
        /// </summary>
        /// <returns>An unused texture ID</returns>
        public int GetNextTextureId()
        {
            int maxId = 0;
            foreach (int id in textureAssets.Keys)
            {
                if (id > maxId) maxId = id;
            }
            return maxId + 1;
        }

        /// <summary>
        /// Gets all texture assets currently managed.
        /// Used when saving a file.
        /// </summary>
        /// <returns>Dictionary of all texture assets</returns>
        public Dictionary<int, TextureAsset> GetAllTextures()
        {
            return new Dictionary<int, TextureAsset>(textureAssets);
        }

        /// <summary>
        /// Checks if a texture with the given ID exists.
        /// </summary>
        public bool HasTexture(int textureId)
        {
            return textureId != 0 && textureAssets.ContainsKey(textureId);
        }

        /// <summary>
        /// Gets information about a texture without loading it.
        /// </summary>
        public TextureAsset GetTextureAsset(int textureId)
        {
            textureAssets.TryGetValue(textureId, out TextureAsset asset);
            return asset;
        }

        /// <summary>
        /// Clears cached Unity Texture2D objects to free memory.
        /// The texture assets remain loaded and can be recreated on demand.
        /// </summary>
        public void ClearTextureCache()
        {
            foreach (var texture in loadedTextures.Values)
            {
                if (texture != null)
                {
                    Object.Destroy(texture);
                }
            }
            loadedTextures.Clear();

            // Also clear TextureAsset internal caches
            foreach (var asset in textureAssets.Values)
            {
                asset.ClearCache();
            }

            Debug.Log("TextureManager: Cleared texture cache");
        }

        /// <summary>
        /// Clears all textures and assets.
        /// Called when a new file is loaded or the model is cleared.
        /// </summary>
        public void ClearAll()
        {
            ClearTextureCache();
            textureAssets.Clear();
            Debug.Log("TextureManager: Cleared all textures");
        }

        /// <summary>
        /// Gets statistics about texture memory usage (estimated).
        /// </summary>
        public string GetMemoryStats()
        {
            int assetCount = textureAssets.Count;
            int loadedCount = loadedTextures.Count;
            long estimatedBytes = 0;

            foreach (var asset in textureAssets.Values)
            {
                estimatedBytes += asset.data.Length;
            }

            return $"Textures: {assetCount} assets ({estimatedBytes / 1024}KB), {loadedCount} loaded";
        }
    }
}
