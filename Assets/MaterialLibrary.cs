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
// limitations under the License.using UnityEngine;

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
/// <summary>
/// This class primarily exists to provide an editor level interface for assigning materials for various effects
/// other than adding them directly to PeltzerMain.
/// </summary>
public class MaterialLibrary : MonoBehaviour
{
    private static readonly int MultiplicitiveAlpha = Shader.PropertyToID("_MultiplicitiveAlpha");
    private static readonly int ZTest = Shader.PropertyToID("_ZTest");
    private static readonly int OverrideColor = Shader.PropertyToID("_OverrideColor");
    private static readonly int OverrideAmount = Shader.PropertyToID("_OverrideAmount");

    public Material baseMaterial;
    public Material transparentMaterial;
    // public Material highlightMaterial;
    // public Material highlightMaterial2;
    public Material highlightSilhouetteMaterial;
    public Material gemMaterial;
    public Material glassMaterial;
    public Material glassMaterialPalette;
    // public Material glassSpecMaterial;
    public Material subtractMaterial;
    public Material copyMaterial;
    public Material snapEffectMaterial;
    public Material meshInsertEffectMaterialFront;
    public Material meshInsertEffectMaterialBack;
    // public Material meshSelectMaterial;
    public Material gridMaterial;
    public Material pointEdgeHighlightMaterial;
    public Material faceHighlightMaterial;
    public Material pointEdgeInactiveMaterial;
    public Material facePaintMaterial;
    public Material faceExtrudeMaterial;
    public Material selectMaterial; // material for yellow selection dot on tool

    private void OnEnable()
    {
        // for rendering MMeshes the shader needs additional information about the positions of the mesh
        // but for rendering non MMesh things, we don't want that because it messes with geometry
        baseMaterial.EnableKeyword("_REMESHER");
        transparentMaterial.EnableKeyword("_REMESHER");
        glassMaterial.EnableKeyword("_REMESHER");

        meshInsertEffectMaterialBack.EnableKeyword("_REMESHER");
        meshInsertEffectMaterialBack.EnableKeyword("_INSERT_MESH");
        meshInsertEffectMaterialFront.EnableKeyword("_REMESHER");
        meshInsertEffectMaterialFront.EnableKeyword("_INSERT_MESH");

        pointEdgeHighlightMaterial.DisableKeyword("_REMESHER");
        pointEdgeHighlightMaterial.SetFloat(OverrideAmount, 0.0f);
        pointEdgeHighlightMaterial.SetFloat(ZTest, 8.0f); // always (no depth test)

        faceHighlightMaterial.DisableKeyword("_REMESHER");
        faceHighlightMaterial.EnableKeyword("_FACE_SELECT_STYLE");
        faceHighlightMaterial.SetFloat(ZTest, 8.0f);

        pointEdgeInactiveMaterial.DisableKeyword("_REMESHER");
        pointEdgeInactiveMaterial.EnableKeyword("_BLEND_TRANSPARENCY");
        pointEdgeInactiveMaterial.SetFloat(ZTest, 4.0f); // (less than equal)

        facePaintMaterial.DisableKeyword("_REMESHER");
        facePaintMaterial.EnableKeyword("_FACE_SELECT_STYLE");
    }
}
