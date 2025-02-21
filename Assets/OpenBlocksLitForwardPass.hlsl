float4x4 _RemesherMeshTransforms[128];
float _MultiplicitiveAlpha;
float _OverrideAmount;
float4 _OverrideColor;

struct OpenBlocksAttributes
{
    Attributes attributes;
    half4 color : COLOR;
};

struct OpenBlocksVaryings
{
    Varyings varyings;
    half4 color : COLOR;
};

inline half3 GammaToLinearSpace (half3 sRGB)
{
    // Approximate version from http://chilliant.blogspot.com.au/2012/08/srgb-approximations-for-hlsl.html?m=1
    return sRGB * (sRGB * (sRGB * 0.305306011h + 0.682171111h) + 0.012522878h);
}

inline void OpenBlocksInitializeStandardLitSurfaceData(float2 uv, out SurfaceData outSurfaceData)
{
    half4 albedoAlpha = SampleAlbedoAlpha(uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
    outSurfaceData.alpha = Alpha(albedoAlpha.a, _BaseColor, _Cutoff);

    half4 specGloss = SampleMetallicSpecGloss(uv, albedoAlpha.a);
    outSurfaceData.albedo = albedoAlpha.rgb * _BaseColor.rgb;
    outSurfaceData.albedo = AlphaModulate(outSurfaceData.albedo, outSurfaceData.alpha);

#if _SPECULAR_SETUP
    outSurfaceData.metallic = half(1.0);
    outSurfaceData.specular = specGloss.rgb;
#else
    outSurfaceData.metallic = specGloss.r;
    outSurfaceData.specular = half3(0.0, 0.0, 0.0);
#endif

    outSurfaceData.smoothness = specGloss.a;
    outSurfaceData.normalTS = SampleNormal(uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);
    outSurfaceData.occlusion = SampleOcclusion(uv);
    outSurfaceData.emission = SampleEmission(uv, _EmissionColor.rgb, TEXTURE2D_ARGS(_EmissionMap, sampler_EmissionMap));

#if defined(_CLEARCOAT) || defined(_CLEARCOATMAP)
    half2 clearCoat = SampleClearCoat(uv);
    outSurfaceData.clearCoatMask       = clearCoat.r;
    outSurfaceData.clearCoatSmoothness = clearCoat.g;
#else
    outSurfaceData.clearCoatMask       = half(0.0);
    outSurfaceData.clearCoatSmoothness = half(0.0);
#endif

#if defined(_DETAIL)
    half detailMask = SAMPLE_TEXTURE2D(_DetailMask, sampler_DetailMask, uv).a;
    float2 detailUv = uv * _DetailAlbedoMap_ST.xy + _DetailAlbedoMap_ST.zw;
    outSurfaceData.albedo = ApplyDetailAlbedo(detailUv, outSurfaceData.albedo, detailMask);
    outSurfaceData.normalTS = ApplyDetailNormal(detailUv, outSurfaceData.normalTS, detailMask);
#endif
}

OpenBlocksVaryings OpenBlocksLitPassVertex(OpenBlocksAttributes input)
{
    OpenBlocksVaryings output;

    #if defined(_REMESHER)
    input.attributes.positionOS = mul(_RemesherMeshTransforms[input.attributes.dynamicLightmapUV.x], input.attributes.positionOS);
    input.attributes.normalOS = mul(_RemesherMeshTransforms[input.attributes.dynamicLightmapUV.x], input.attributes.normalOS);
    #endif
    
    output.varyings = LitPassVertex(input.attributes);
    output.color = half4(GammaToLinearSpace(input.color.rgb), input.color.a);
    // output.color = half4(input.color.rgb, input.color.a);
    return output;
    
}

void OpenBlocksLitPassFragment(OpenBlocksVaryings input, out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out float4 outRenderingLayers : SV_Target1
#endif
    )
{
    Varyings v = input.varyings;
    _BaseColor = input.color;
    
    LitPassFragment(v, outColor
    #ifdef  _WRITE_RENDERING_LAYERS
    , out
    #endif
    );
    
    outColor = half4(outColor.rgb, outColor.a * _MultiplicitiveAlpha);
    outColor = lerp(outColor, _OverrideColor, _OverrideAmount);
}