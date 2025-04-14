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
    // gem shader
    private static readonly int RefractionTexture = Shader.PropertyToID("_RefractTex");
    private static readonly int FacetSize = Shader.PropertyToID("_FacetSize");
    private static readonly int Roughness = Shader.PropertyToID("_Roughness");

    public Material baseMaterial;
    public Material transparentMaterial;
    // public Material highlightMaterial;
    // public Material highlightMaterial2;
    [FormerlySerializedAs("highlightSilhouetteMaterial")] public Material meshSelectMaterial;
    public Material gemMaterialFront;
    public Material gemMaterialBack;
    public Material gemMaterialPaletteFront;
    public Material gemMaterialPaletteBack;
    public Material glassMaterial;
    public Material glassMaterialPalette;
    public Material subtractMaterial;
    public Material subtractMaterialQuest; // Quest cannot do geometry shaders in single pass
    public Material copyMaterial;
    public Material copyMaterialQuest; // Quest cannot do geometry shaders in single pass
    public Material meshInsertEffectMaterialFront;
    public Material meshInsertEffectMaterialBack;
    public Material gridMaterial;
    public Material pointEdgeHighlightMaterial;
    public Material faceHighlightMaterial;
    public Material pointEdgeInactiveMaterial;
    public Material facePaintMaterial;
    public Material faceExtrudeMaterial;
    public Material selectMaterial; // material for yellow selection dot on tool

    public Cubemap gemRefractionMap;
    private void OnEnable()
    {
        if (SystemInfo.deviceName.Contains("Quest"))
        {
            // quest can't do geometry shader so use the non geometry shader version
            copyMaterial = copyMaterialQuest;
            subtractMaterial = subtractMaterialQuest;
        }

        // NOTE: we only need to enable keywpords once when we want to change the shader
        // otherwise keep things commented out!!!

        // for rendering MMeshes the shader needs additional information about the positions of the mesh
        // but for rendering non MMesh things, we don't want that because it messes with geometry
        // baseMaterial.EnableKeyword("_REMESHER");
        // transparentMaterial.EnableKeyword("_REMESHER");
        // glassMaterial.EnableKeyword("_REMESHER");

        // gridMaterial.EnableKeyword("_BLEND_TRANSPARENCY");

        // subtractMaterial.EnableKeyword("_REMESHER");
        // subtractMaterialQuest.EnableKeyword("_REMESHER");

        // meshInsertEffectMaterialBack.EnableKeyword("_REMESHER");
        // meshInsertEffectMaterialBack.EnableKeyword("_INSERT_MESH");
        // meshInsertEffectMaterialFront.EnableKeyword("_REMESHER");
        // meshInsertEffectMaterialFront.EnableKeyword("_INSERT_MESH");

        // pointEdgeHighlightMaterial.DisableKeyword("_REMESHER");
        // pointEdgeHighlightMaterial.SetFloat(OverrideAmount, 0.0f);
        // pointEdgeHighlightMaterial.SetFloat(ZTest, 8.0f); // always (no depth test)

        // faceHighlightMaterial.DisableKeyword("_REMESHER");
        // faceHighlightMaterial.EnableKeyword("_FACE_SELECT_STYLE");
        // faceHighlightMaterial.SetFloat(ZTest, 4.0f);

        // pointEdgeInactiveMaterial.DisableKeyword("_REMESHER");
        // pointEdgeInactiveMaterial.EnableKeyword("_BLEND_TRANSPARENCY");
        // pointEdgeInactiveMaterial.SetFloat(ZTest, 4.0f); // (less than equal)

        // facePaintMaterial.DisableKeyword("_REMESHER");
        // facePaintMaterial.EnableKeyword("_FACE_SELECT_STYLE");

        // faceExtrudeMaterial.DisableKeyword("_REMESHER");
        // faceExtrudeMaterial.EnableKeyword("_FACE_SELECT_STYLE");
        // use face highlight style for face extrude for the time being
        // since original face extrude style didn't work properly anyway
        // faceExtrudeMaterial.DisableKeyword("_FACE_EXTRUDE");
        // faceExtrudeMaterial.SetFloat(ZTest, 4.0f);

        // gemMaterialFront.EnableKeyword("_REMESHER");
        // gemMaterialFront.EnableKeyword("_GEM_EFFECT");
        gemMaterialFront.SetTexture(RefractionTexture, gemRefractionMap);
        gemMaterialFront.SetFloat(FacetSize, 0.06f);
        gemMaterialFront.SetFloat(Roughness, 0.001f);

        // when rendering the gem material we render the backfaces at a tiny offset away from the camera
        // so they render correctly
        // gemMaterialBack.EnableKeyword("_REMESHER");
        // gemMaterialBack.EnableKeyword("_GEM_EFFECT");
        // gemMaterialBack.EnableKeyword("_GEM_EFFECT_BACKFACE_FIX");
        gemMaterialBack.SetTexture(RefractionTexture, gemRefractionMap);
        gemMaterialBack.SetFloat(FacetSize, 0.06f);
        gemMaterialBack.SetFloat(Roughness, 0.001f);

        // because of the way the remesher changes vertex positions in the shader
        // we don't want to use the remesher for meshes that are not MMeshes (e.g. normal Unity meshes)
        // otherwise there are holes in the geometry
        // gemMaterialPaletteFront.DisableKeyword("_REMESHER");
        // gemMaterialPaletteFront.EnableKeyword("_GEM_EFFECT");
        gemMaterialPaletteFront.SetTexture(RefractionTexture, gemRefractionMap);
        gemMaterialPaletteFront.SetFloat(FacetSize, 0.6f);
        gemMaterialPaletteFront.SetFloat(Roughness, 0.01f);

        // gemMaterialPaletteBack.DisableKeyword("_REMESHER");
        // gemMaterialPaletteBack.EnableKeyword("_GEM_EFFECT");
        gemMaterialPaletteBack.SetTexture(RefractionTexture, gemRefractionMap);
        gemMaterialPaletteBack.SetFloat(FacetSize, 0.6f);
        gemMaterialPaletteBack.SetFloat(Roughness, 0.01f);
    }
}
