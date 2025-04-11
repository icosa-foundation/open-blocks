// Copyright 2024 The Open Blocks Authors
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

Shader "Mogwai/DirectionalUniformSlider"
{
    Properties
    {
        _Color( "Color", Color ) = ( 1, 1, 1, 1 )
        _EmissiveColor("EmissiveColor", Color) = ( 0, 0, 0, 0 )
        _EmissiveAmount("Emissive Amount", Float) = 1
        _Roughness("Roughness", Float) = 0.8
        _Metallic("Metallic", Float) = 1.0
        _RefractiveIndex("Fresnel Effect Refractive Index", Float) = 1.3
        _OverrideColor("Override Color", Color) = (0.5, 0.5, 0.5, 1)
        _OverrideAmount("Override Amount", Float) = 0
        _SlideValue("Slide Value", Range(0, 1)) = 0
        
        // URP required properties
        [HideInInspector] _BaseMap("Base Map", 2D) = "white" {}
        [HideInInspector] _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5
    }
    
    SubShader
    {
        Tags { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline"
        }
        LOD 100

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        
        CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float3 _EmissiveColor;
            float _EmissiveAmount;
            float _Roughness;
            float _Metallic;
            float _RefractiveIndex;
            float3 _OverrideColor;
            float _OverrideAmount;
            half _SlideValue;
            float4 _BaseMap_ST;
            float _Smoothness;
        CBUFFER_END
        ENDHLSL
        
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float4 localPos : TEXCOORD2;
            };

            Varyings vert (Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.localPos = input.positionOS + 0.5;
                return output;
            }

            float4 frag (Varyings input) : SV_Target
            {
                // Get light and shadow data
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                
                // Calculate basic lighting
                float3 normalWS = normalize(input.normalWS);
                float3 lightDir = normalize(mainLight.direction);
                float NdotL = saturate(dot(normalWS, lightDir));
                
                float3 baseColor = _Color.rgb;
                float3 ambientColor = SampleSH(normalWS) * baseColor;
                float3 diffuseColor = mainLight.color * baseColor * NdotL * mainLight.shadowAttenuation;
                
                // Combine lighting
                float3 lightOut = ambientColor + diffuseColor;
                
                // Apply slider effect
                float slide = input.localPos.x < _SlideValue ? 1.0 : 0.0;
                float3 overrideColor = _OverrideColor + _EmissiveColor * _EmissiveAmount;
                
                return float4(lerp(lightOut, overrideColor.rgb, _OverrideAmount) * slide, _Color.a);
            }
            ENDHLSL
        }
    }
}
