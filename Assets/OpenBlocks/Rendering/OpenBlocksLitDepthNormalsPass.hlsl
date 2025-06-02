float4x4 _RemesherMeshTransforms[128];

struct LitDepthNormalsAttributes
{
    Attributes attributes;
    float2 dynamicLightmapUV  : TEXCOORD2;
};

Varyings OpenBlocksLitDepthNormalsPassVertex(LitDepthNormalsAttributes input)
{
    #if defined(_REMESHER)
    input.attributes.positionOS = mul(_RemesherMeshTransforms[input.dynamicLightmapUV.x], input.attributes.positionOS);
    input.attributes.normal = mul(_RemesherMeshTransforms[input.dynamicLightmapUV.x], input.attributes.normal);
    #endif
                
    return DepthNormalsVertex(input.attributes);          
}