using System;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;

namespace OpenBlocks.Editor
{
    public class OpenBlocksSimpleLitShaderGUI : SimpleLitShader
    {
        private static readonly int EnableSlider = Shader.PropertyToID("_EnableSlider");
        private static readonly int SlideValue = Shader.PropertyToID("_SlideValue");
        private static readonly int OverrideColor = Shader.PropertyToID("_OverrideColor");
        private static readonly int OverrideAmount = Shader.PropertyToID("_OverrideAmount");
        private static readonly int MultiplicitiveAlpha = Shader.PropertyToID("_MultiplicitiveAlpha");
        // OpenBlocks properties
        private MaterialEditor openBlocksMaterialEditor;
        private MaterialProperty[] properties;
        private MaterialHeaderScopeList materialScopeList = new MaterialHeaderScopeList(UInt32.MaxValue);
        private GUIContent openBlocks = new("Open Blocks Properties", "Open Blocks specific properties and keywords");

        private new bool m_FirstTimeApply = true;

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            openBlocksMaterialEditor = materialEditor;
            this.properties = properties;
            var targetMat = materialEditor.target as Material;

            if (m_FirstTimeApply)
            {
                materialScopeList.RegisterHeaderScope(openBlocks, 1, DrawOpenBlocksOptions);
                m_FirstTimeApply = false;
            }
            materialScopeList.DrawHeaders(openBlocksMaterialEditor, targetMat);

            base.FindProperties(properties);
            base.OnGUI(materialEditor, properties);
        }

        public void DrawOpenBlocksOptions(Material material)
        {
            if (material == null)
                throw new ArgumentNullException("material");

            EditorGUIUtility.labelWidth = 0f;

            // make separate tab for open blocks specific properties
            MaterialProperty sliderKeyword = FindProperty("_EnableSlider", properties);
            MaterialProperty slideValue = FindProperty("_SlideValue", properties);
            MaterialProperty overrideColor = FindProperty("_OverrideColor", properties);
            MaterialProperty overrideAmount = FindProperty("_OverrideAmount", properties);
            MaterialProperty multiplicitiveAlpha = FindProperty("_MultiplicitiveAlpha", properties);

            GUIContent labelSlider = new GUIContent("Enable Slider Keyword", "Currently used for the complexity slider in the model UI.");
            GUIContent labelSlideValue = new GUIContent("Slide Value", "The value of the slider. This is used to set the value of the slider in the model UI.");
            GUIContent labelOverrideValue = new GUIContent("Override Value");
            GUIContent labelOverrideAmount = new GUIContent("Override Amount");
            GUIContent labelMultiplicitiveAlpha = new GUIContent("Multiplicitive Alpha");

            // OpenBlocks specific properties
            if (material.HasProperty(EnableSlider))
            {
                EditorGUI.BeginChangeCheck();
                openBlocksMaterialEditor.ShaderProperty(sliderKeyword, labelSlider);
                bool sliderKeywordEnabled = sliderKeyword.floatValue > 0.5f;
                if (EditorGUI.EndChangeCheck())
                {
                    sliderKeyword.floatValue = sliderKeywordEnabled ? 1.0f : 0.0f;
                    material.SetFloat(EnableSlider, sliderKeyword.floatValue);
                }

                // show slide value slider only if slider keyword is enabled
                if (sliderKeywordEnabled)
                {
                    EditorGUI.BeginChangeCheck();
                    openBlocksMaterialEditor.ShaderProperty(slideValue, labelSlideValue, 1);
                    if (EditorGUI.EndChangeCheck())
                    {
                        slideValue.floatValue = Mathf.Clamp(slideValue.floatValue, 0.0f, 1.0f);
                        material.SetFloat(SlideValue, slideValue.floatValue);
                    }
                }
            }

            if (material.HasProperty(OverrideColor))
            {
                EditorGUI.BeginChangeCheck();
                openBlocksMaterialEditor.ShaderProperty(overrideColor, labelOverrideValue);
                if (EditorGUI.EndChangeCheck())
                {
                    overrideColor.colorValue = new Color(overrideColor.colorValue.r, overrideColor.colorValue.g, overrideColor.colorValue.b, 1.0f);
                    material.SetColor(OverrideColor, overrideColor.colorValue);
                }
            }

            if (material.HasProperty(OverrideAmount))
            {
                EditorGUI.BeginChangeCheck();
                openBlocksMaterialEditor.ShaderProperty(overrideAmount, labelOverrideAmount);
                if (EditorGUI.EndChangeCheck())
                {
                    overrideAmount.floatValue = Mathf.Clamp(overrideAmount.floatValue, 0.0f, 1.0f);
                    material.SetFloat(OverrideAmount, overrideAmount.floatValue);
                }
            }

            if (material.HasProperty(MultiplicitiveAlpha))
            {
                EditorGUI.BeginChangeCheck();
                openBlocksMaterialEditor.ShaderProperty(multiplicitiveAlpha, labelMultiplicitiveAlpha);
                if (EditorGUI.EndChangeCheck())
                {
                    multiplicitiveAlpha.floatValue = Mathf.Clamp(overrideAmount.floatValue, 0.0f, 1.0f);
                    material.SetFloat(MultiplicitiveAlpha, multiplicitiveAlpha.floatValue);
                }
            }
        }
    }
}