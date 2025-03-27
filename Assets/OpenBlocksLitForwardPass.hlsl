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

// GEM SHADER
float invFacetSize;
float entropy;

float _FacetSize;
float _Roughness;
samplerCUBE _RefractTex;
// samplerCUBE _EnvCubeMap;

struct OpenBlocksAttributes
{
    Attributes attributes;
    half4 color : COLOR;

    #if defined(_FACE_SELECT_STYLE)
    // uvs (TEXCOORD0) is used for animation percentage (based on original shader)
    // float3 selectPosition : TANGENT; // use this if there are multiple select positions
    #endif
};

struct OpenBlocksVaryings
{
    Varyings varyings;
    half4 color : COLOR;
    
    #if defined(_FACE_SELECT_STYLE)
    // uv (TEXCOORD0) is used for animation percentage (based on original shader)
    float3 selectPosition : TANGENT; // use this if there are multiple select positions
    #endif

    #if defined(_GEM_EFFECT)
    // float isFacing : VFACE;
    float3 tangent : TEXCOORD10;
    float3 normal : TEXCOORD11;
    float3 binormal : TEXCOORD12;
    float3 positionOS : TEXCOORD13;
    float4x4 meshTransform : TEXCOORD14;
    #endif
    
};

inline float2x3 InsertMeshEffect(OpenBlocksVaryings input, Varyings v)
{
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

    // float matAlpha = v.positionWS.y <= yPivot ? 1 * input.color.a : 0.3 * input.color.a;
    float matAlpha = step(v.positionWS.y, yPivot) * (1 * input.color.a) + (1 - step(v.positionWS.y, yPivot)) * (0.3 * input.color.a);
    
    return float2x3(effectPct, matAlpha, effectColor.x, effectColor.y, effectColor.z, effectColor.w);

}

inline float4 InsertMeshEffectApplyColor(float4 outColor, float effectPct, float matAlpha, float4 effectColor)
{
    outColor = outColor + effectColor * effectPct * _MaxEffectEmissive;
    outColor.a = matAlpha;
    return outColor;
}

inline float4 BlendTransparency(Varyings v, float4 outColor)
{
    float3 to = v.positionWS - _SelectPositionWorld.xyz;
    float distSqr = dot(to, to);
    float ratio = distSqr / (_SelectRadius * _SelectRadius);
    float alpha = smoothstep(1.0, 0.0, ratio * ratio);
    outColor.a = alpha;
    return outColor;
}

#if defined(_FACE_SELECT_STYLE)
inline float4 FaceSelectStyle(OpenBlocksVaryings input, Varyings v, float4 outColor)
{
    float3 dirToProjected = input.selectPosition - v.positionWS;
    float radiusThreshold = 0.2;
    float animDoneOverride = smoothstep(1.0, 0.98, v.uv.r); // per vertex animation percentage
    radiusThreshold = radiusThreshold * v.uv.r;
    return step(animDoneOverride * length(dirToProjected),radiusThreshold) * outColor;
}
#endif

////////////// GEM SHADER /////////////

#define triCellHeightScale  0.86602540378

inline float proceduralNoise(float2 val) {
    val %= 1.0;
    float xy = (val.x + 4.0) * (val.y + 13.0) * 100000.0;
    return (fmod((fmod(xy, 13.0) + 1.0) * (fmod(xy, 123.0) + 1.0), .05) - 0.025);
}

inline float2 vorGridCoords(float2 coords, float cellSize) {

    float2 multCoords = coords * invFacetSize;
    multCoords.y = multCoords.y / triCellHeightScale;
    float offsetRow = floor(multCoords.y + 0.5) % 2;
    float notOffsetRow = 1 - offsetRow;
    return (floor(multCoords + float2(notOffsetRow * 0.5, 0.5)) + float2(offsetRow * 0.5, 0)) * float2(1, triCellHeightScale) * cellSize;
        
      
}

inline float2 randomOffset(float2 inUv, float cellSize) {
    return cellSize * 0.5 * saturate(entropy * float2(proceduralNoise(inUv.xy), proceduralNoise(inUv.xy + float2(0.1231256645, 0.2358789))));
}      

static const float2 pointOffsets[7] = {
    {-0.5, triCellHeightScale},
    {0.5, triCellHeightScale},
    {-1.0, 0.0},
    {0.0, 0.0},
    {1.0, 0.0},
    {-0.5, -triCellHeightScale},
    {0.5, -triCellHeightScale}
};

inline float4 voronoiCell(float2 uv, float3 normal, float cellSize, out float3 closestPoint, 
      out float3 secondPoint,
      out float3 thirdPoint,
      out float3 fourthPoint)
{
    float2 vUv = vorGridCoords(uv, cellSize);

    float2 normalFactor = float2(0, 0);//proceduralNoise(0.5 + 0.25 * normalize(normal.xyz).xy).xx;

    float4 nearbyPoints[8];
    nearbyPoints[7].z = 999999999999.0;
    int closest = 7;
    int second = 7;
    int third = 7;
    int fourth = 7;
    int i;
    int j;
    for(i = 0; i <7; i++) {
             
        nearbyPoints[i] = float4(vUv + float2(cellSize, cellSize) * pointOffsets[i], 0, 0);
        nearbyPoints[i] = nearbyPoints[i] + float4(randomOffset(nearbyPoints[i] + normalFactor, cellSize), 0, 0);
        nearbyPoints[i].z = length(nearbyPoints[i].xy - uv);
        
        int a = nearbyPoints[i].z < nearbyPoints[closest].z;
        int b = nearbyPoints[i].z < nearbyPoints[second].z;
        int c = nearbyPoints[i].z < nearbyPoints[third].z;
        int d = nearbyPoints[i].z < nearbyPoints[fourth].z;

        // b ? c : d; => step(0.5, b) * c + (1.0 - step(0.5, b)) * d;
        float sc = step(0.5, c);
        float sb = step(0.5, b);
        float sa = step(0.5, a);
        int temp1 = sc * third + (1.0 - sc) * i;
        int temp2 = sb * second + (1.0 - sb) * i;
        int temp3 = sa * closest + (1.0 - sa) * i;

        fourth = step(0.5, d) * temp1 + (1.0 - sb) * fourth;
        third = sc * temp2 + (1.0 - sc) * third;
        second = sb * temp3 + (1.0 - sb) * second;
        closest = sa * i + (1.0 - sa) * closest;
        
        // fourth = d ? (!c ? i : third) : fourth;
        // third = c ? (!b ? i : second) : third;
        // second = b ? (!a ? i : closest) : second;
        // closest = a ? i : closest;
    }
            
    closestPoint = nearbyPoints[closest];
    secondPoint = nearbyPoints[second];
    thirdPoint = nearbyPoints[third];
    fourthPoint = nearbyPoints[fourth];
     float2 ab = normalize(secondPoint.xy - closestPoint.xy);
     float2 mid = (closestPoint.xy + secondPoint.xy) * 0.5;
     float projection = dot(mid, ab);
     nearbyPoints[closest].w = projection;

    return nearbyPoints[closest];
}

inline float3x3 ObjectToTangentMat(float3 tangent, float3 binormal, float3 normal)
{
    return float3x3(tangent.x, tangent.y, tangent.z, binormal.x, binormal.y, binormal.z, normal.x, normal.y, normal.z);
}

// inline float3 RefractionVector(float fromIOR, float toIOR, float3 inVec, float3 N)
// {
//     float ratio = fromIOR / toIOR;
//     float cos0 = dot(inVec, N);
//     float checkVal = 1 - ratio * ratio * (1 - cos0 * cos0);
//     float3 outVec = checkVal < 0 ? float3(0, 0, 0) : (ratio * inVec + (ratio * cos0 - sqrt(checkVal))) * N;
//     return outVec;
// }

inline half perceptualRoughnessToMipmapLevel(half perceptualRoughness)
{
    return perceptualRoughness * UNITY_SPECCUBE_LOD_STEPS;
}

// inline float4 SampleEnv(float3 sampleVec, float roughness)
// {
//     half perceptualRoughness = roughness;
//     perceptualRoughness = perceptualRoughness * (1.7 - 0.7 * perceptualRoughness);
//     half mip = perceptualRoughnessToMipmapLevel(perceptualRoughness);
//     return texCUBElod(_EnvCubeMap, float4(sampleVec, mip));
// }

inline float3 evaluateFresnelSchlick(float VDotH, float3 F0) {
    return F0 + (1 - F0) * pow(1 - VDotH, 5);
}

inline float getFresnel(float IOR, float VDotH)
{
    float3 F0 = abs((1.0  - IOR) / (1.0 + IOR));
    F0 = F0 * F0;
    return evaluateFresnelSchlick(VDotH, F0);
}

inline float3 borderInfo(float2 uv, float3 a, float3 b)
{
    float2 ba = normalize(b.xy - a.xy);
    float2 bamid = (b.xy + a.xy) * 0.5;
    float baDist = abs(dot(uv - bamid, ba));
    float2 projected = uv + ba * baDist;
    return float3(projected, baDist);
}

#if defined(_GEM_EFFECT)
inline float3x4 GemEffect(OpenBlocksVaryings input, bool isFrontFace)
{
    entropy = 25; // taking values from initial material
    float facetDeflection = 1.2;
    float refractMix = 0.9;
    float cellSize = _FacetSize * 2.0; // back faces
    invFacetSize = 1.0 / cellSize;
    
    float2 uv = float2(dot(input.tangent, input.positionOS), dot(input.binormal, input.positionOS));
    float3 a, b, c, d;

    // cellSize = isFrontFace ? _FacetSize : cellSize;
    float sf = step(1.0, isFrontFace);
    cellSize = sf * _FacetSize + (1.0 - sf) * cellSize;
    float4 cell = voronoiCell(uv, normalize(input.normal), cellSize, a, b, c, d);
    
    float3 ba = borderInfo(uv, a, b); // used for front and back faces
    float3 cb = borderInfo(uv, b, c); // back faces only

    float border = pow(smoothstep(0, 0.005, ba.z), .01); // front faces
    // float border2 = pow(ba.z, 0.1) < 0.01 ? 0.0 : 1.0; // back faces
    // float border3 = pow(cb.z, 0.1) < 0.01 ? 0.0 : 1.0; // back faces
    float border2 = step(0.01, pow(ba.z, 0.1));
    float border3 = step(0.01, pow(cb.z, 0.1));

    // float deflectionMul = isFrontFace ? max (0.000000001, border) : max (0.000000001, border2 * border3);
    float deflectionMul = max(0.000000001, border) * sf + max(0.000000001, border2 * border3) * (1.0 - sf);
    
    float2 towardsEdge = normalize(b - a);
    float2 towardsEdge2 = normalize(c - a);
    float2 towardsEdge3 = normalize(c - b);
    // towardsEdge = isFrontFace ? normalize(towardsEdge + towardsEdge2) : normalize(towardsEdge + towardsEdge2 + towardsEdge3);
    towardsEdge = normalize(towardsEdge + towardsEdge2) * sf + normalize(towardsEdge + towardsEdge2 + towardsEdge3) * (1.0 - sf);
    
    float3x3 objectToTangent = ObjectToTangentMat(input.tangent, input.binormal, input.normal);
    float3x3 tangentToObject = transpose(objectToTangent);
    float3 tangentSpaceNormal = normalize(float3(towardsEdge * facetDeflection * deflectionMul, 1 / (facetDeflection * deflectionMul)));
    float3 ennoisenedNormal = mul(unity_ObjectToWorld, mul(input.meshTransform, mul(tangentToObject, tangentSpaceNormal)));

    float3 V = normalize(_WorldSpaceCameraPos - input.varyings.positionWS);
    float3 N = normalize(ennoisenedNormal);
    float3 H = normalize(V + N);
    float3 reflectDir = reflect(V, N);

    // float fresnel = isFrontFace ? getFresnel(2.4, dot(H, N)) : 0;
    // float alpha = isFrontFace ? max(_BaseColor.a, 0.8 * (1 - fresnel)) : 1;
    // float fresnel = getFresnel(2.4, dot(H, N)) * sf;
    // float alpha = max(_BaseColor.a, 0.8 * (1 - fresnel)) * sf + 1 * (1.0 - sf);
    float alpha = 1; // for backfaces only
    
    half perceptualRoughness = _Roughness * (1.7 - 0.7 * _Roughness);
    half mip = perceptualRoughnessToMipmapLevel(perceptualRoughness);
    
    // float NDotH = saturate(dot(N, H));
    float4 diffraction = texCUBElod(_RefractTex, float4(reflectDir, mip));
    // float diffractionAmount = isFrontFace ? saturate(0.25 * dot(N, H)) : 0.9;
    float diffractionAmount = saturate(0.25 * dot(N, H)) * sf + 0.9 * (1.0 - sf);
        
    return float3x4(diffraction.x, diffraction.y, diffraction.z, diffraction.w,
        diffractionAmount, alpha, 1, 1,
        N.x, N.y, N.z, 1);
}
#endif

/////////////////////////////////////////////////

inline half3 GammaToLinearSpace (half3 sRGB)
{
    // Approximate version from http://chilliant.blogspot.com.au/2012/08/srgb-approximations-for-hlsl.html?m=1
    return sRGB * (sRGB * (sRGB * 0.305306011h + 0.682171111h) + 0.012522878h);

    // Precise version, useful for debugging.
    //return half3(GammaToLinearSpaceExact(sRGB.r), GammaToLinearSpaceExact(sRGB.g), GammaToLinearSpaceExact(sRGB.b));
}

OpenBlocksVaryings OpenBlocksLitPassVertex(OpenBlocksAttributes input)
{
    OpenBlocksVaryings output;
    
    #if defined(_REMESHER)
    input.attributes.positionOS = mul(_RemesherMeshTransforms[input.attributes.dynamicLightmapUV.x], input.attributes.positionOS);
    input.attributes.normalOS = mul(_RemesherMeshTransforms[input.attributes.dynamicLightmapUV.x], input.attributes.normalOS);
    #endif
    
    #if defined(_FACE_SELECT_STYLE)
    output.selectPosition = input.attributes.tangentOS;
    #endif
    
    #if defined(_GEM_EFFECT)
    float3 arbitraryVector = normalize(float3(0.42, -0.21, 0.15));
    float3 alternateArbitraryVector = normalize(float3(0.43, 1.5, 0.15));
    // if the normal is parallel to the arbitrary vector, add a little to it
    float isParallel = step(abs(dot(input.attributes.normalOS, arbitraryVector)), 1); // 1 if parallel, 0 if not
    arbitraryVector.x = arbitraryVector.x + isParallel;
    float3 tangent = normalize(cross(input.attributes.normalOS, arbitraryVector));
    float3 binormal = normalize(cross(input.attributes.normalOS, tangent));
    
    output.normal = normalize(input.attributes.normalOS);
    output.tangent = tangent;
    output.binormal = binormal;
    output.positionOS = input.attributes.positionOS.xyz;
    output.meshTransform = _RemesherMeshTransforms[input.attributes.dynamicLightmapUV.x];
    #endif
    
    output.varyings = LitPassVertex(input.attributes);
    output.color = half4(GammaToLinearSpace(input.color.rgb), input.color.a);
    // output.color = half4(input.color.rgb, input.color.a);
    return output;
}

void OpenBlocksLitPassFragment(OpenBlocksVaryings input, bool facing : SV_IsFrontFace, out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out float4 outRenderingLayers : SV_Target1
#endif
    )
{
    Varyings v = input.varyings;
    _BaseColor = input.color * _BaseColor;

    #if defined(_INSERT_MESH)
    float2x3 effectData = InsertMeshEffect(input, v); // effectPct, matAlpha, effectColor.x, effectColor.y, effectColor.z, effectColor.w
    #endif
    
    #if defined(_GEM_EFFECT)
    float3x4 output = GemEffect(input, facing);
    float3 ennoisenedNormal = float3(output._m20, output._m21, output._m22);
    // v.normalWS = facing ? ennoisenedNormal : -ennoisenedNormal;
    // _BaseColor = facing ? _BaseColor : float4(0.4432, 0.3382, 1, 1);
    float stp = step(1.0, facing);
    v.normalWS = stp * ennoisenedNormal + (1.0 - stp) * -ennoisenedNormal;
    _BaseColor = stp * _BaseColor + (1.0 - stp) * float4(0.4432, 0.3382, 1, 1);
    #endif
    
    LitPassFragment(v, outColor
    #ifdef  _WRITE_RENDERING_LAYERS
    , out
    #endif
    );

    #if defined(_GEM_EFFECT)
    float4 diffraction = float4(output._m00, output._m01, output._m02, output._m03);
    float diffractionAmount = output._m10;
    float alpha = output._m11;

    // outColor = float4(outColor * (1.0 - diffractionAmount) + diffraction * diffractionAmount);
    // outColor.a = 1.0;
    float3 colorOut = outColor.rgb * (1.0 - diffractionAmount) + outColor.rgb * diffraction.rgb * (diffractionAmount) * diffraction.rgb;
    // float3 colorOut = outColor.rgb * (1.0 - diffractionAmount) + (diffractionAmount) * diffraction.rgb;

    // outColor = facing ? float4(colorOut + _BaseColor.rgb * 0.25, alpha) : float4(outColor.rgb * (1.0 - diffractionAmount) + diffraction.rgb * diffractionAmount, alpha);
    // take _BaseColor alpha instead of fresnel alpha calculated in gem shader (looks very similar)
    outColor = stp * float4(colorOut + _BaseColor.rgb * 0.25, _BaseColor.a)
        + (1.0 - stp) * float4(outColor.rgb * (1.0 - diffractionAmount) + diffraction.rgb * diffractionAmount, alpha);
    
    // outColor = outColor * (1.0 - diffractionAmount) + outColor * diffraction * (diffractionAmount);
    // outColor = float4(outColor.rgb + _BaseColor.rbg * 0.01, alpha);
    
    // outColor = diffraction;
    
    #endif

    #if defined(_INSERT_MESH)
    outColor = InsertMeshEffectApplyColor(outColor, effectData._m00, effectData._m01, float4(effectData._m02, effectData._m10, effectData._m11, effectData._m12));
    #else
    outColor = half4(outColor.rgb, outColor.a * _MultiplicitiveAlpha);
    outColor = lerp(outColor, _OverrideColor, _OverrideAmount);
    #endif
    
    #if defined(_BLEND_TRANSPARENCY)
    outColor = BlendTransparency(v, outColor);
    #endif

    #if defined(_FACE_SELECT_STYLE) // send _AnimPct already as squared
    outColor = FaceSelectStyle(input, v, outColor);
    #endif
}

