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

namespace com.google.blocks.serialization
{
    /// <summary>
    /// Math utilities for 3D operations (simplified version for serialization package).
    /// </summary>
    public static class Math3d
    {
        /// <summary>
        /// Normalizes a vector with scaling to avoid precision issues.
        /// </summary>
        /// <param name="vec">The vector to normalize.</param>
        /// <returns>The normalized vector.</returns>
        public static Vector3 Normalize(Vector3 vec)
        {
            Vector3 scaledVec = 1000000f * vec;
            return scaledVec / scaledVec.magnitude;
        }
    }
}
