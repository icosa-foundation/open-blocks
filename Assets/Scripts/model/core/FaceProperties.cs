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

namespace com.google.apps.peltzer.client.model.core
{

    /// <summary>
    ///   Properties for Faces.  A value-type.
    /// </summary>
    public struct FaceProperties
    {
        public int materialId { get; private set; }

        // Texture IDs (0 = no texture assigned)
        public int albedoTextureId { get; private set; }
        public int bumpTextureId { get; private set; }

        // Texture UV transform properties
        public Vector2 textureScale { get; private set; }
        public Vector2 textureOffset { get; private set; }

        // Simple constructor for backwards compatibility (no textures)
        public FaceProperties(int materialId)
        {
            this.materialId = materialId;
            this.albedoTextureId = 0;
            this.bumpTextureId = 0;
            this.textureScale = Vector2.one;
            this.textureOffset = Vector2.zero;
        }

        // Full constructor with texture support
        public FaceProperties(int materialId,
                             int albedoTextureId = 0,
                             int bumpTextureId = 0,
                             Vector2? textureScale = null,
                             Vector2? textureOffset = null)
        {
            this.materialId = materialId;
            this.albedoTextureId = albedoTextureId;
            this.bumpTextureId = bumpTextureId;
            this.textureScale = textureScale ?? Vector2.one;
            this.textureOffset = textureOffset ?? Vector2.zero;
        }

        /// <summary>
        /// Returns true if this face has any textures assigned.
        /// </summary>
        public bool HasTextures()
        {
            return albedoTextureId != 0 || bumpTextureId != 0;
        }

        /// <summary>
        /// Creates a copy with a new material ID.
        /// </summary>
        public FaceProperties WithMaterialId(int newMaterialId)
        {
            return new FaceProperties(newMaterialId, albedoTextureId, bumpTextureId, textureScale, textureOffset);
        }

        /// <summary>
        /// Creates a copy with new texture IDs.
        /// </summary>
        public FaceProperties WithTextures(int newAlbedoTextureId, int newBumpTextureId)
        {
            return new FaceProperties(materialId, newAlbedoTextureId, newBumpTextureId, textureScale, textureOffset);
        }
    }
}
