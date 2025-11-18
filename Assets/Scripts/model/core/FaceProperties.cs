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

        public FaceProperties(int materialId)
        {
            this.materialId = materialId;
        }

        /// <summary>
        ///   Creates FaceProperties from an arbitrary color.
        ///   If the color matches a palette color, uses that.
        ///   Otherwise, creates a new custom color entry.
        /// </summary>
        /// <param name="color">The color to create face properties for.</param>
        /// <returns>FaceProperties with the appropriate material ID.</returns>
        public static FaceProperties FromColor(Color32 color)
        {
            int id = render.MaterialRegistry.GetOrCreateMaterialId(color);
            return new FaceProperties(id);
        }

        /// <summary>
        ///   Gets the actual color for this face's material.
        ///   Works for both palette and custom colors.
        /// </summary>
        /// <returns>The Color32 of this face.</returns>
        public Color32 GetColor()
        {
            return render.MaterialRegistry.GetMaterialColor32ById(materialId);
        }

        /// <summary>
        ///   Checks if this face uses a custom color (not from the legacy palette).
        /// </summary>
        /// <returns>True if this is a custom color (ID >= 1000).</returns>
        public bool IsCustomColor()
        {
            return render.MaterialRegistry.IsCustomMaterialId(materialId);
        }
    }
}
