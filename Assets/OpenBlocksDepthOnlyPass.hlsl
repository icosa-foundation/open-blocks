float4x4 _RemesherMeshTransforms[128];

struct DepthOnlyAttributes
{
    Attributes attributes;
    float2 dynamicLightmapUV  : TEXCOORD2;
};

Varyings OpenBlocksDepthOnlyPassVertex(DepthOnlyAttributes input)
{
    #if defined(_REMESHER)
    input.attributes.position = mul(_RemesherMeshTransforms[input.dynamicLightmapUV.x], input.attributes.position);
    #endif
                
    return DepthOnlyVertex(input.attributes);          
}