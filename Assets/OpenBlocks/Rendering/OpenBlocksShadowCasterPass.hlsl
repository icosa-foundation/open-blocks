float4x4 _RemesherMeshTransforms[128];
float _MultiplicitiveAlpha;
float _OverrideAmount;
float4 _OverrideColor;

struct ShadowAttributes
{
    Attributes attributes;
    float2 dynamicLightmapUV  : TEXCOORD2;
};
            
Varyings OpenBlocksShadowPassVertex(ShadowAttributes input)
{
    #if defined(_REMESHER)
    input.attributes.positionOS = mul(_RemesherMeshTransforms[input.dynamicLightmapUV.x], input.attributes.positionOS);
    input.attributes.normalOS = mul(_RemesherMeshTransforms[input.dynamicLightmapUV.x], input.attributes.normalOS);
    #endif
                
    return ShadowPassVertex(input.attributes);
                
}