Shader "Hidden/Custom/CaviFree"
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


        //////////////////////////////////

        float CalculateCurvature(float2 left, float2 right, float2 down, float2 up, float exponent, float multiplier)
        {
            float resultX = left.x - right.x;
            float resultY = up.y - down.y;
            float totalResult = resultX + resultY;

            // we raise curvature to some power to control the sensitivity
            // of which angles we highlight and which ones we don't
            float curvature = 0.5 + sign(totalResult) * pow(abs(totalResult * multiplier), exponent);
            return clamp(curvature, 0.0, 1.0);
        }

        float GetCurvatureAtPoint(float2 offset, float exponent, float multiplier)
        {
            float2 leftRight = float2(1.0 / _ScreenParams.x, 0);
            float2 upDown = float2(0, 1.0 / _ScreenParams.y);
            float2 up = DecodeViewNormalStereo(tex2D(_CameraDepthNormalsTexture, upDown + offset.y)).rg;
            float2 down = DecodeViewNormalStereo(tex2D(_CameraDepthNormalsTexture, upDown + offset.y)).rg;
            float2 left = DecodeViewNormalStereo(tex2D(_CameraDepthNormalsTexture, leftRight + offset.x)).rg;
            float2 right = DecodeViewNormalStereo(tex2D(_CameraDepthNormalsTexture, leftRight + offset.x)).rg;
            return CalculateCurvature(left, right, down, up, exponent, multiplier);
        }

        float GetAverageCurvature(int radius, float exponent, float multiplier, float sharpness)
        {
            float totalWeight = 0.0;
            float curvature = 0.0;

            // 0.0001 to prevent nasty divisions by zero when calculating weights
            sharpness = clamp(1.0 - sharpness, 0.0001, 1.0);

            // sample points around the current pixel, giving each one of them
            // a weight depending on the distance to the centre
            for (int i = -radius; i <= radius; i++)
            {
                for (int j = -radius; j <= radius; j++)
                {
                    float2 pixelOffset = float2(i, j);
                    float2 uvOffset = pixelOffset / _ScreenParams.xy;
                    float weight = 1 / (dot(pixelOffset, pixelOffset) + sharpness);
                    totalWeight += weight;
                    curvature += weight * GetCurvatureAtPoint(uvOffset, exponent, multiplier);
                }
            }

            curvature /= totalWeight;
            return curvature;
        }

        //////////////////////////////////


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

        float blendSoftLight(float base, float blend) {
	        return (blend<0.5)?(2.0*base*blend+base*base*(1.0-2.0*blend)):(sqrt(base)*(2.0*blend-1.0)+2.0*base*(1.0-blend));
        }

        float3 blendSoftLight(float3 base, float3 blend) {
	        return float3(blendSoftLight(base.r,blend.r),blendSoftLight(base.g,blend.g),blendSoftLight(base.b,blend.b));
        }

        float3 blendSoftLight(float3 base, float3 blend, float opacity) {
	        return (blendSoftLight(base, blend) * opacity + base * (1.0 - opacity));
        }

        float4 Frag(Varyings i) : SV_Target
        {
            float4 baseColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.texcoord[0]);

            float intensity = 4.5;
            float angleSensitivity = 2.13;
            float edgeIntensityMultiplier = 0.53;

            float exponent = angleSensitivity;
            float multiplier = edgeIntensityMultiplier;

            int radius = 10;
            float sharpness = 0.0;

            float curvature = GetAverageCurvature(radius, exponent, multiplier, sharpness);
            //(i.texcoord[1], i.texcoord[4], _curvature_ridge, _curvature_valley);

            // baseColor.rgb *= blendSoftLight(baseColor.rgb, curvature, intensity);
            baseColor.rgb = curvature * 1.0 * float3(1.0, 1.0, 1.0);

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
