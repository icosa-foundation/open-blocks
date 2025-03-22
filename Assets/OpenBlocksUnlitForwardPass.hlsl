float4x4 _RemesherMeshTransforms[128];
float _MultiplicitiveAlpha;
float _OverrideAmount;
float4 _OverrideColor;
float4 _SelectPositionWorld; // use this if there is only one select position
float _SelectRadius;
float _AnimPct;

struct OpenBlocksAttributes
{
    Attributes attributes;
    half4 color : COLOR;
    #if defined(_FACE_SELECT_STYLE)
    // uvs (TEXCOORD0) is used for animation percentage (based on original shader)
    float3 selectPositionWorld : TANGENT; // use this if there are multiple select positions
    #endif

};

struct OpenBlocksVaryings
{
    Varyings varyings;
    half4 color : COLOR;
    float3 positionWS : TEXCOORD2; // need this for selection radius calculations
    #if defined(_FACE_SELECT_STYLE)
    // uv (TEXCOORD0) is used for animation percentage (based on original shader)
    float3 selectPositionWorld : TANGENT; // use this if there are multiple select positions
    #endif

};

inline half3 GammaToLinearSpace (half3 sRGB)
{
    // Approximate version from http://chilliant.blogspot.com.au/2012/08/srgb-approximations-for-hlsl.html?m=1
    return sRGB * (sRGB * (sRGB * 0.305306011h + 0.682171111h) + 0.012522878h);
}

OpenBlocksVaryings OpenBlocksUnlitPassVertex(OpenBlocksAttributes input)
{
    OpenBlocksVaryings output;

    #if defined(_REMESHER)
    input.attributes.positionOS = mul(_RemesherMeshTransforms[input.attributes.dynamicLightmapUV.x], input.attributes.positionOS);
    input.attributes.normalOS = mul(_RemesherMeshTransforms[input.attributes.dynamicLightmapUV.x], input.attributes.normalOS);
    #endif
    
    // output.varyings = UnlitPassVertex(input.attributes);

    // start from UnlitPassVertex (this is the same except for getting positionWS too)
    UNITY_SETUP_INSTANCE_ID(input.attributes);
    UNITY_TRANSFER_INSTANCE_ID(input.attributes, output.varyings);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output.varyings);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.attributes.positionOS.xyz);

    // want the World space position too so we can compute the selection radius in the fragment shader
    output.positionWS = vertexInput.positionWS;
    
    output.varyings.positionCS = vertexInput.positionCS;
    output.varyings.uv = TRANSFORM_TEX(input.attributes.uv, _BaseMap);
    #if defined(_FOG_FRAGMENT)
    output.varyings.fogCoord = vertexInput.positionVS.z;
    #else
    output.varyings.fogCoord = ComputeFogFactor(vertexInput.positionCS.z);
    #endif

    #if defined(DEBUG_DISPLAY)
    // normalWS and tangentWS already normalize.
    // this is required to avoid skewing the direction during interpolation
    // also required for per-vertex lighting and SH evaluation
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.attributes.normalOS, input.attributes.tangentOS);
    half3 viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);

    // already normalized from normal transform to WS.
    output.varyings.positionWS = vertexInput.positionWS;
    output.varyings.normalWS = normalInput.normalWS;
    output.varyings.viewDirWS = viewDirWS;
    #endif
    // end from UnlitPassVertex

    #if defined(_FACE_SELECT_STYLE)
    output.selectPositionWorld = input.selectPositionWorld;
    #endif
    
    output.color = half4(GammaToLinearSpace(input.color.rgb), input.color.a);
    return output;    
}

void OpenBlocksUnlitPassFragment(
    OpenBlocksVaryings input
    , out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out float4 outRenderingLayers : SV_Target1
#endif
)
{
    Varyings v = input.varyings;
    _BaseColor = input.color * _BaseColor;
    
    UnlitPassFragment(v, outColor
    #ifdef  _WRITE_RENDERING_LAYERS
    , out
    #endif
    );
    
    outColor = half4(outColor.rgb, outColor.a * _MultiplicitiveAlpha);
    outColor = lerp(outColor, _OverrideColor, _OverrideAmount);
    #if defined(_BLEND_TRANSPARENCY)
    float3 to = input.positionWS - _SelectPositionWorld.xyz;
    float distSqr = dot(to, to);
    float ratio = distSqr / (_SelectRadius * _SelectRadius);
    float alpha = smoothstep(1.0,0.0,ratio * ratio) * 0.5; 
    outColor.a = alpha;
    #endif
    #if defined(_FACE_SELECT_STYLE) // send animPct already as squared
    float3 dirToProjected = input.selectPositionWorld - input.positionWS;
    float radiusThreshold = 0.2;
    float animDoneOverride = smoothstep(1.0, 0.98, v.uv.r); // per vertex animation percentage
    radiusThreshold = radiusThreshold * v.uv.r;
    outColor = step(animDoneOverride * length(dirToProjected),radiusThreshold) * outColor;
    #endif
}