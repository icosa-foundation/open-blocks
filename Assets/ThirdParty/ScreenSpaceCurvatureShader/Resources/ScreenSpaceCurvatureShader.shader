Shader "Hidden/Custom/ScreenSpaceCurvature"
{
    HLSLINCLUDE

        #define CURVATURE_OFFSET 1

        #include "Packages/com.unity.postprocessing/PostProcessing/Shaders/StdLib.hlsl"

        #if !defined(SHADER_TARGET_GLSL) && !defined(SHADER_API_PSSL) && !defined(SHADER_API_GLES3) && !defined(SHADER_API_VULKAN) && !(defined(SHADER_API_METAL) && defined(UNITY_COMPILER_HLSLCC))
            #define sampler2D_float sampler2D
        #endif

        TEXTURE2D_SAMPLER2D(_MainTex, sampler_MainTex);
        float4 _MainTex_ST;
        float4 _MainTex_TexelSize;
        sampler2D _CameraDepthNormalsTexture;
        float _curvature_ridge;
        float _curvature_valley;

        struct Varyings
        {
            float4 vertex : SV_POSITION;
            float2 texcoord[5] : TEXCOORD0;
        };

        float curvature_soft_clamp(float curvature, float control)
        {
            if (curvature < 0.5 / control)
                return curvature * (1.0 - curvature * control);
            return 0.25 / control;
        }

        float calculate_curvature(float2 up, float2 down, float2 left, float2 right, float ridge, float valley)
        {
            float2 normal_up = DecodeViewNormalStereo(tex2D(_CameraDepthNormalsTexture, up)).rg;
            float2 normal_down = DecodeViewNormalStereo(tex2D(_CameraDepthNormalsTexture, down)).rg;
            float2 normal_left = DecodeViewNormalStereo(tex2D(_CameraDepthNormalsTexture, left)).rg;
            float2 normal_right = DecodeViewNormalStereo(tex2D(_CameraDepthNormalsTexture, right)).rg;

            float normal_diff = ((normal_up.g - normal_down.g) + (normal_right.r - normal_left.r));

            if (normal_diff < 0.0)
                return -2.0 * curvature_soft_clamp(-normal_diff, valley);

            return 2.0 * curvature_soft_clamp(normal_diff, ridge);
        }

        Varyings Vert(AttributesDefault v)
        {
            Varyings o;

            o.vertex = float4(v.vertex.xy, 0.0, 1.0);
            float2 texcoord = TransformTriangleVertexToUV(v.vertex.xy);

            #if UNITY_UV_STARTS_AT_TOP
                texcoord = texcoord * float2(1.0, -1.0) + float2(0.0, 1.0);
            #endif

            o.texcoord[0] = UnityStereoScreenSpaceUVAdjust(texcoord, _MainTex_ST);
            o.texcoord[1] = UnityStereoScreenSpaceUVAdjust(texcoord + _MainTex_TexelSize.xy * float2(0,CURVATURE_OFFSET), _MainTex_ST);
            o.texcoord[2] = UnityStereoScreenSpaceUVAdjust(texcoord + _MainTex_TexelSize.xy * float2(0,-CURVATURE_OFFSET), _MainTex_ST);
            o.texcoord[3] = UnityStereoScreenSpaceUVAdjust(texcoord + _MainTex_TexelSize.xy * float2(-CURVATURE_OFFSET,0), _MainTex_ST);
            o.texcoord[4] = UnityStereoScreenSpaceUVAdjust(texcoord + _MainTex_TexelSize.xy * float2(CURVATURE_OFFSET,0), _MainTex_ST);

            return o;
        }

        float4 Frag(Varyings i) : SV_Target
        {
            float4 baseColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.texcoord[0]);

            float curvature = calculate_curvature(i.texcoord[1], i.texcoord[2], i.texcoord[3], i.texcoord[4], _curvature_ridge, _curvature_valley);

            baseColor.rgb *= 2.0 * (curvature + 1.0);

            return baseColor;
        }

    ENDHLSL

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM

                #pragma vertex Vert
                #pragma fragment Frag

            ENDHLSL
        }
    }
}
