// Copyright 2022 The Tilt Brush Authors
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
using Polyhydra.Core;
using UnityEngine;

namespace TiltBrush
{
    public enum GeneratorTypes
    {
        FileSystem = 0,
        GeometryData = 1,

        RegularGrids = 2,
        CatalanGrids = 10,
        OneUniformGrids = 11,
        TwoUniformGrids = 12,
        DurerGrids = 13,

        Shapes = 3,

        Radial = 4,
        Waterman = 5,
        Johnson = 6,
        ConwayString = 7,
        Uniform = 8,
        Various = 9,
    }

    public class EditableModelManager : MonoBehaviour
    {
        public static EditableModelManager m_Instance;
        public Material[] m_Materials;

        void Awake()
        {
            // Taking editable model screenshots uses EditableModelManager
            // but doesn't have an App object - so catch the exception
            try
            {
                // App.InitShapeRecipesPath();
            }
            catch (NullReferenceException)
            {
                Debug.LogWarning($"Failed to Init Shape Recipes Path");
            }

            m_Instance = this;

            // CreateExportableMaterials();
        }


    }

}