float _OverrideAmount;
float4 _OverrideColor;
float _MultiplicitiveAlpha;
float _SlideValue;

struct OpenBlocksAttributes
{
    Attributes attributes;
    // Add any additional attributes you need here
};

struct OpenBlocksVaryings
{
    Varyings varyings;
    #if defined(_SLIDER)
    float4 localPos : TEXCOORD3;
    #endif
};

OpenBlocksVaryings OpenBlocksLitPassVertexSimple(OpenBlocksAttributes input)
{
    OpenBlocksVaryings output;
    
    output.varyings = LitPassVertexSimple(input.attributes);
    #if defined(_SLIDER)
    output.localPos = input.attributes.positionOS + 0.5;
    #endif
    return output;
}

void OpenBlocksLitPassFragmentSimple(OpenBlocksVaryings input, out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out float4 outRenderingLayers : SV_Target1
#endif
    )
{
   Varyings v = input.varyings;
   
   LitPassFragmentSimple(
       v
       , outColor
       #ifdef _WRITE_RENDERING_LAYERS
       , outRenderingLayers
       #endif
   );
   
    outColor = half4(outColor.rgb, outColor.a * _MultiplicitiveAlpha);
    outColor = lerp(outColor, _OverrideColor, _OverrideAmount);   
   
   # if defined(_SLIDER)
   float slide = step(input.localPos.x, _SlideValue);
   outColor = half4(outColor.rgb * slide, outColor.a);
   #endif
}