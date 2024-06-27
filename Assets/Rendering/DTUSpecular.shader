﻿// Copyright 2020 The Blocks Authors
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

Shader "Mogwai/DTUSpecular"
{
  Properties
  {
 		_Color( "Color", Color ) = ( 1, 1, 1, 0 )
    _EmissiveColor("EmissiveColor", Color) = ( 0, 0, 0, 0 )
    _EmissiveAmount("Emissive Amount", Float) = 1
    _Roughness("Roughness",Float) = 0.3
    _Metallic("Metallic", Float) = 0.0
    _Mirror("Mirror", Float) = 0.3
    _BackfaceFade("Backface Fade", Float) = 0.5
    _RefractiveIndex("Fresnel Effect Refractive Index", Float) = 1.33333
    _MultiplicitiveAlpha("Multiplicitive Alpha", Float) = 1.0
    _OverrideColor("Override Color", Color) = (0.5, 0.5, 0.5, 1)
    _OverrideAmount("Override Amount", Float) = 0
  }
  SubShader
  {
    Tags { "RenderType"="Transparent" "Queue"="Transparent+2" }
    LOD 100
    Pass
    {
      Cull Front
      Offset -1, -1
    	Blend One OneMinusSrcColor
    	ZWrite Off
      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma target 5.0
      #include "UnityCG.cginc"
      #include "UnityImageBasedLighting.cginc"
      #include "shaderMath.cginc"
      #define INV_PI 0.31830988618

      struct VertexInput
      {
        float4 position : POSITION;
        float3 normal : NORMAL;
        float2 meshBone : TEXCOORD2;
      };

      struct VertexOutput
      {
        float4 position : SV_POSITION;
        float3 normal : TEXCOORD0;
        float4 worldPosition : TEXCOORD1;

        float4 shadowPosition : TEXCOORD2;
        float4 grabPos : TEXCOORD3;
        float3 uv : TEXCOORD4;
      };

      VertexOutput vert (VertexInput vertex)
      {
        VertexOutput output;
        float4 objectPosition = mul(_RemesherMeshTransforms[vertex.meshBone.x], vertex.position);
        float4 objectNormal = mul(_RemesherMeshTransforms[vertex.meshBone.x], float4(-vertex.normal, 0));
        output.position = UnityObjectToClipPos(objectPosition);
        output.normal = UnityObjectToWorldNormal(objectNormal);
        output.worldPosition = mul(unity_ObjectToWorld, objectPosition);
        output.shadowPosition = mul(_ShadowMatrix, output.worldPosition);
        output.grabPos = ComputeGrabScreenPos(output.position);
        return output;
      }

      float4 _Color;
      sampler2D PreTransparencyTexture;
      float _MultiplicitiveAlpha;
      float4 _OverrideColor;
      float _OverrideAmount;
      float _BackfaceFade;

      float4 frag (VertexOutput fragment) : SV_Target
      {
        float3 lightOut = 0;
        float3 specOut = 0;
        evaluateLights(
          fragment.worldPosition.xyz , // pixelPos
          fragment.normal , // pixelNormal
          _Color, // color
          fragment.shadowPosition, // shadowPosition
          lightOut, // inout diffuseOut
          specOut);
                    return float4(specOut, _Color.a);
         float4 dst = float4(specOut * _MultiplicitiveAlpha + _EmissiveColor.rgb * _EmissiveAmount, 1 * _MultiplicitiveAlpha);
         return lerp(_BackfaceFade * dst, float4(0, 0, 0, 0), _OverrideAmount);
      }
      ENDCG
    }

    Pass
    {
      Cull Back
      Offset -1, -1
    	Blend One One
    	ZWrite Off
      CGPROGRAM
      #pragma vertex vert
      #pragma fragment frag
      #pragma target 5.0
      #include "UnityCG.cginc"
      #include "UnityImageBasedLighting.cginc"
      #include "shaderMath.cginc"
      #define INV_PI 0.31830988618

      struct VertexInput
      {
        float4 position : POSITION;
        float3 normal : NORMAL;
        float2 meshBone : TEXCOORD2;
      };

      struct VertexOutput
      {
        float4 position : SV_POSITION;
        float3 normal : TEXCOORD0;
        float4 worldPosition : TEXCOORD1;

        float4 shadowPosition : TEXCOORD2;
        float4 grabPos : TEXCOORD3;
        float3 uv : TEXCOORD4;
      };

      VertexOutput vert (VertexInput vertex)
      {
        VertexOutput output;
        float4 objectPosition = mul(_RemesherMeshTransforms[vertex.meshBone.x], vertex.position);
        float4 objectNormal = mul(_RemesherMeshTransforms[vertex.meshBone.x], float4(vertex.normal, 0));
        output.position = UnityObjectToClipPos(objectPosition);
        output.normal = UnityObjectToWorldNormal(objectNormal);
        output.worldPosition = mul(unity_ObjectToWorld, objectPosition);
        output.shadowPosition = mul(_ShadowMatrix, output.worldPosition);
        output.grabPos = ComputeGrabScreenPos(output.position);
        return output;
      }

      float4 _Color;
      sampler2D PreTransparencyTexture;
      float _MultiplicitiveAlpha;
      float4 _OverrideColor;
      float _OverrideAmount;
      float4 frag (VertexOutput fragment) : SV_Target
      {
        float3 lightOut = 0;
        float3 specOut = 0;
        evaluateLights(
          fragment.worldPosition.xyz , // pixelPos
          fragment.normal , // pixelNormal
          _Color, // color
          fragment.shadowPosition, // shadowPosition
          lightOut, // inout diffuseOut
          specOut);
          return float4(specOut, _Color.a);
        float4 dst = float4(lightOut * _MultiplicitiveAlpha + _EmissiveColor.rgb * _EmissiveAmount, 1 * _MultiplicitiveAlpha);
        return lerp(dst, _OverrideColor, _OverrideAmount);
      }
      ENDCG
    }
  }
}