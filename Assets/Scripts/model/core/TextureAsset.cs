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
using UnityEngine;

namespace com.google.apps.peltzer.client.model.core
{
    /// <summary>
    /// Types of textures that can be stored in a TextureAsset.
    /// </summary>
    public enum TextureType
    {
        ALBEDO = 0,           // Base color/diffuse texture
        BUMP_NORMAL = 1,      // Normal/bump map
        SPECULAR = 2,         // Specular map
        EMISSION = 3,         // Emissive map
        METALLIC_ROUGHNESS = 4 // Metallic/roughness map
    }

    /// <summary>
    /// Represents a texture asset that can be embedded in a PeltzerFile.
    /// Textures are stored as encoded image data (PNG/JPG) for efficient storage.
    /// </summary>
    public class TextureAsset
    {
        private readonly int _id;
        private readonly string _name;
        private readonly TextureType _type;
        private readonly byte[] _data;
        private readonly int _width;
        private readonly int _height;

        // Cached Unity Texture2D - not serialized, created on demand
        private Texture2D _cachedTexture;

        public int id { get { return _id; } }
        public string name { get { return _name; } }
        public TextureType type { get { return _type; } }
        public byte[] data { get { return _data; } }
        public int width { get { return _width; } }
        public int height { get { return _height; } }

        /// <summary>
        /// Creates a new TextureAsset from encoded image data.
        /// </summary>
        /// <param name="id">Unique ID for this texture within the file</param>
        /// <param name="name">Name/identifier for the texture</param>
        /// <param name="type">Type of texture (albedo, bump, etc.)</param>
        /// <param name="data">Encoded image data (PNG or JPG)</param>
        /// <param name="width">Width in pixels</param>
        /// <param name="height">Height in pixels</param>
        public TextureAsset(int id, string name, TextureType type, byte[] data, int width, int height)
        {
            _id = id;
            _name = name ?? string.Empty;
            _type = type;
            _data = data;
            _width = width;
            _height = height;
            _cachedTexture = null;
        }

        /// <summary>
        /// Creates a TextureAsset from a Unity Texture2D.
        /// The texture will be encoded as PNG for storage.
        /// </summary>
        /// <param name="id">Unique ID for this texture</param>
        /// <param name="name">Name for the texture</param>
        /// <param name="type">Type of texture</param>
        /// <param name="texture">Source Unity texture</param>
        public static TextureAsset FromTexture2D(int id, string name, TextureType type, Texture2D texture)
        {
            if (texture == null)
            {
                throw new ArgumentNullException(nameof(texture));
            }

            // Encode as PNG for lossless storage
            byte[] encodedData = texture.EncodeToPNG();

            return new TextureAsset(id, name, type, encodedData, texture.width, texture.height);
        }

        /// <summary>
        /// Gets or creates a Unity Texture2D from this asset.
        /// The texture is cached after first creation.
        /// </summary>
        /// <returns>Unity Texture2D ready for use in materials</returns>
        public Texture2D GetTexture2D()
        {
            if (_cachedTexture != null)
            {
                return _cachedTexture;
            }

            // Create a new texture and load the encoded data
            _cachedTexture = new Texture2D(_width, _height);
            _cachedTexture.name = _name;

            if (!_cachedTexture.LoadImage(_data))
            {
                Debug.LogError($"Failed to load texture data for {_name}");
                return null;
            }

            // Set appropriate texture settings based on type
            switch (_type)
            {
                case TextureType.ALBEDO:
                    _cachedTexture.filterMode = FilterMode.Bilinear;
                    _cachedTexture.wrapMode = TextureWrapMode.Repeat;
                    break;

                case TextureType.BUMP_NORMAL:
                    _cachedTexture.filterMode = FilterMode.Bilinear;
                    _cachedTexture.wrapMode = TextureWrapMode.Repeat;
                    // Note: Normal maps should be imported as normal map format,
                    // but for embedded textures we handle this at material assignment
                    break;

                default:
                    _cachedTexture.filterMode = FilterMode.Bilinear;
                    _cachedTexture.wrapMode = TextureWrapMode.Repeat;
                    break;
            }

            _cachedTexture.Apply();
            return _cachedTexture;
        }

        /// <summary>
        /// Clears the cached Unity texture to free memory.
        /// The texture can be recreated later by calling GetTexture2D().
        /// </summary>
        public void ClearCache()
        {
            if (_cachedTexture != null)
            {
                UnityEngine.Object.Destroy(_cachedTexture);
                _cachedTexture = null;
            }
        }

        /// <summary>
        /// Creates a copy of this TextureAsset with a new ID.
        /// </summary>
        public TextureAsset Clone(int newId)
        {
            return new TextureAsset(newId, _name, _type, _data, _width, _height);
        }
    }
}
