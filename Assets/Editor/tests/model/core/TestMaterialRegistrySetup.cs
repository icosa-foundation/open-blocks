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

using System.Runtime.Serialization;
using NUnit.Framework;
using UnityEngine;
using com.google.apps.peltzer.client.model.render;

namespace com.google.apps.peltzer.client.model.core
{
    internal static class TestMaterialRegistry
    {
        private static bool initialized;

        internal static void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            Shader baseShader = Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Standard");
            Assert.NotNull(baseShader, "Expected a fallback shader for test material registry setup.");

            MaterialLibrary materialLibrary =
                (MaterialLibrary)FormatterServices.GetUninitializedObject(typeof(MaterialLibrary));
            materialLibrary.baseMaterial = new Material(baseShader);
            materialLibrary.transparentMaterial = new Material(baseShader);
            materialLibrary.glassMaterial = new Material(baseShader);
            materialLibrary.glassMaterialPalette = new Material(baseShader);
            materialLibrary.gemMaterialFront = new Material(baseShader);
            materialLibrary.gemMaterialBack = new Material(baseShader);
            materialLibrary.gemMaterialPaletteFront = new Material(baseShader);
            materialLibrary.copyMaterial = new Material(baseShader);
            materialLibrary.subtractMaterial = new Material(baseShader);
            materialLibrary.meshSelectMaterial = new Material(baseShader);

            MaterialRegistry.init(materialLibrary);
            initialized = true;
        }
    }

    [SetUpFixture]
    public class TestMaterialRegistrySetup
    {
        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            TestMaterialRegistry.EnsureInitialized();
        }
    }
}
