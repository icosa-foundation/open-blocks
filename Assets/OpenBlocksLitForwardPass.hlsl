float4x4 _RemesherMeshTransforms[128];
float _MultiplicitiveAlpha;
float _OverrideAmount;
float4 _OverrideColor;
float4 _SelectPositionWorld;
float _SelectRadius;
float4 _EffectColor;
float4 _MeshShaderBounds;
float _AnimPct;
float _MaxEffectEmissive;
float _AnimNoiseScale;
float _AnimNoiseAmplitude;


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
    _BaseColor = input.color * _BaseColor;

    #if defined(_INSERT_MESH)
    float boundsHeight = (_MeshShaderBounds.y - _MeshShaderBounds.x);
    // Do cheap, fake noise for animated wave.  It's good enough.
    float animNoiseShift = 10 * _AnimPct;
    float noise = sin(v.positionWS.x * _AnimNoiseScale + animNoiseShift)
        + sin(v.positionWS.z * _AnimNoiseScale + animNoiseShift);
    noise = noise * 0.24 * boundsHeight;
    
    float4 effectColor = (_EffectColor.rgba + input.color.rgba) * 0.5;

    float yPivot = _MeshShaderBounds.x + _AnimPct * (boundsHeight * 1.4) + noise * _AnimNoiseAmplitude;
    float distanceIn = max( 0, yPivot - v.positionWS.y);
    float effectPct = saturate(v.positionWS.y <= yPivot ? 1 - (distanceIn / (0.4 * boundsHeight)) : 0);

    float matAlpha = v.positionWS.y <= yPivot ? 1 * input.color.a : 0.3 * input.color.a;
                
    #endif
    
    LitPassFragment(v, outColor
    #ifdef  _WRITE_RENDERING_LAYERS
    , out
    #endif
    );

    #if defined(_INSERT_MESH)
    outColor = outColor + effectColor * effectPct * _MaxEffectEmissive;
    outColor.a = matAlpha;
    #else
    outColor = half4(outColor.rgb, outColor.a * _MultiplicitiveAlpha);
    outColor = lerp(outColor, _OverrideColor, _OverrideAmount);
    #endif
    
    #if defined(_BLEND_TRANSPARENCY)
    float3 to = v.positionWS - _SelectPositionWorld.xyz;
    float distSqr = dot(to, to);
    // float alpha = saturate(dist / _SelectRadius);
    float ratio = distSqr / (_SelectRadius * _SelectRadius);
    float alpha = smoothstep(1.0, 0.0, ratio * ratio);
    outColor.a = alpha;
    #endif

    #if defined(_FACE_SELECT_STYLE) // send _AnimPct already as squared
    float3 dirToProjected = _SelectPositionWorld.xyz - v.positionWS;
    float radiusThreshold = 0.2;
    float animDoneOverride = smoothstep(1.0, 0.98, _AnimPct);
    radiusThreshold = radiusThreshold * _AnimPct;
    outColor = step(animDoneOverride * length(dirToProjected),radiusThreshold) * outColor;
    #endif
}