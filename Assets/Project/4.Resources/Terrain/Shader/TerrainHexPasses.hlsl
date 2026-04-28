#ifndef CUSTOM_TERRAIN_HEX_PASSES_INCLUDED
#define CUSTOM_TERRAIN_HEX_PASSES_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GBufferOutput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 texcoord : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 uvMainAndLM              : TEXCOORD0;
    float4 uvSplat01                : TEXCOORD1;
    float4 uvSplat23                : TEXCOORD2;

    #if defined(_NORMALMAP) && !defined(ENABLE_TERRAIN_PERPIXEL_NORMAL)
        half4 normal                : TEXCOORD3;
        half4 tangent               : TEXCOORD4;
        half4 bitangent             : TEXCOORD5;
    #else
        half3 normal                : TEXCOORD3;
        half3 vertexSH              : TEXCOORD4;
    #endif

    #ifdef _ADDITIONAL_LIGHTS_VERTEX
        half4 fogFactorAndVertexLight : TEXCOORD6;
    #else
        half  fogFactor             : TEXCOORD6;
    #endif

    float3 positionWS               : TEXCOORD7;

    #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
        float4 shadowCoord          : TEXCOORD8;
    #endif

#if defined(DYNAMICLIGHTMAP_ON)
    float2 dynamicLightmapUV        : TEXCOORD9;
#endif

#ifdef USE_APV_PROBE_OCCLUSION
    float4 probeOcclusion           : TEXCOORD10;
#endif

    float4 clipPos                  : SV_POSITION;
    UNITY_VERTEX_OUTPUT_STEREO
};

void InitializeInputData(Varyings IN, half3 normalTS, out InputData inputData)
{
    inputData = (InputData)0;
    inputData.positionWS = IN.positionWS;
    inputData.positionCS = IN.clipPos;

    #if defined(_NORMALMAP) && !defined(ENABLE_TERRAIN_PERPIXEL_NORMAL)
        half3 viewDirWS = half3(IN.normal.w, IN.tangent.w, IN.bitangent.w);
        inputData.tangentToWorld = half3x3(-IN.tangent.xyz, IN.bitangent.xyz, IN.normal.xyz);
        inputData.normalWS = TransformTangentToWorld(normalTS, inputData.tangentToWorld);
        half3 SH = 0;
    #elif defined(ENABLE_TERRAIN_PERPIXEL_NORMAL)
        half3 viewDirWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);
        float2 sampleCoords = (IN.uvMainAndLM.xy / _TerrainHeightmapRecipSize.zw + 0.5f) * _TerrainHeightmapRecipSize.xy;
        half3 normalWS = TransformObjectToWorldNormal(normalize(SAMPLE_TEXTURE2D(_TerrainNormalmapTexture, sampler_TerrainNormalmapTexture, sampleCoords).rgb * 2 - 1));
        half3 tangentWS = cross(GetObjectToWorldMatrix()._13_23_33, normalWS);
        inputData.normalWS = TransformTangentToWorld(normalTS, half3x3(-tangentWS, cross(normalWS, tangentWS), normalWS));
        half3 SH = IN.vertexSH;
    #else
        half3 viewDirWS = GetWorldSpaceNormalizeViewDir(IN.positionWS);
        inputData.normalWS = IN.normal;
        half3 SH = IN.vertexSH;
    #endif

    inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);
    inputData.viewDirectionWS = viewDirWS;

    #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
        inputData.shadowCoord = IN.shadowCoord;
    #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
        inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
    #else
        inputData.shadowCoord = float4(0, 0, 0, 0);
    #endif

    #ifdef _ADDITIONAL_LIGHTS_VERTEX
        inputData.fogCoord = InitializeInputDataFog(float4(IN.positionWS, 1.0), IN.fogFactorAndVertexLight.x);
        inputData.vertexLighting = IN.fogFactorAndVertexLight.yzw;
    #else
        inputData.fogCoord = InitializeInputDataFog(float4(IN.positionWS, 1.0), IN.fogFactor);
    #endif

    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.clipPos);

    #if defined(DEBUG_DISPLAY)
    #if defined(DYNAMICLIGHTMAP_ON)
        inputData.dynamicLightmapUV = IN.dynamicLightmapUV;
    #endif
    #if defined(LIGHTMAP_ON)
        inputData.staticLightmapUV = IN.uvMainAndLM.zw;
    #else
        inputData.vertexSH = SH;
    #endif
    #if defined(USE_APV_PROBE_OCCLUSION)
        inputData.probeOcclusion = IN.probeOcclusion;
    #endif
    #endif
}

void InitializeBakedGIData(Varyings IN, inout InputData inputData)
{
    #if defined(_NORMALMAP) && !defined(ENABLE_TERRAIN_PERPIXEL_NORMAL)
        half3 SH = 0;
    #else
        half3 SH = IN.vertexSH;
    #endif

#if defined(_SCREEN_SPACE_IRRADIANCE)
    inputData.bakedGI = SAMPLE_GI(_ScreenSpaceIrradiance, inputData.positionCS.xy);
#elif defined(DYNAMICLIGHTMAP_ON)
    inputData.bakedGI = SAMPLE_GI(IN.uvMainAndLM.zw, IN.dynamicLightmapUV, SH, inputData.normalWS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(IN.uvMainAndLM.zw);
#elif !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
    inputData.bakedGI = SAMPLE_GI(SH,
        GetAbsolutePositionWS(inputData.positionWS),
        inputData.normalWS,
        inputData.viewDirectionWS,
        inputData.positionCS.xy,
        IN.probeOcclusion,
        inputData.shadowMask);
#else
    inputData.bakedGI = SAMPLE_GI(IN.uvMainAndLM.zw, SH, inputData.normalWS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(IN.uvMainAndLM.zw);
#endif
}

// ============================================================
// Hex-aware sampling per layer.
// One HexCell computed per layer, reused for diff/normal/mask
// to keep sample count tractable (3 samples × 3 textures = 9 per layer).
// ============================================================

void HexSplatLayer(float2 uv, TEXTURE2D_PARAM(splat, sSplat),
                   TEXTURE2D_PARAM(nrm, sNrm), half nrmScale,
                   TEXTURE2D_PARAM(mask, sMask), half hasMask,
                   half4 maskRemapScale, half4 maskRemapOffset,
                   out half4 outDiff, out half3 outNormal, out half4 outMask)
{
    HexCell cell = ComputeHexCell(uv, _HexScale, _HexRotationStrength, _HexContrast);

    outDiff = HexSample(cell, TEXTURE2D_ARGS(splat, sSplat));

    #ifdef _NORMALMAP
        half4 nrmRaw = HexSample(cell, TEXTURE2D_ARGS(nrm, sNrm));
        outNormal = UnpackNormalScale(nrmRaw, nrmScale);
    #else
        outNormal = half3(0, 0, 1);
    #endif

    #ifdef _MASKMAP
        half4 maskRaw = HexSample(cell, TEXTURE2D_ARGS(mask, sMask));
        outMask = maskRemapOffset + maskRemapScale * lerp(0.5h, maskRaw, hasMask);
    #else
        outMask = maskRemapOffset + maskRemapScale * 0.5h;
    #endif
}

void SplatmapMixHex(Varyings IN, inout half4 splatControl,
                    out half weight, out half4 mixedDiffuse, out half4 defaultSmoothness,
                    out half3 mixedNormal, out half4 masks[4])
{
    half4 hasMask = half4(_LayerHasMask0, _LayerHasMask1, _LayerHasMask2, _LayerHasMask3);

    half4 diff[4];
    half3 nrm[4];

    HexSplatLayer(IN.uvSplat01.xy,
                  TEXTURE2D_ARGS(_Splat0, sampler_Splat0),
                  TEXTURE2D_ARGS(_Normal0, sampler_Normal0), _NormalScale0,
                  TEXTURE2D_ARGS(_Mask0, sampler_Mask0), hasMask.x,
                  _MaskMapRemapScale0, _MaskMapRemapOffset0,
                  diff[0], nrm[0], masks[0]);

    HexSplatLayer(IN.uvSplat01.zw,
                  TEXTURE2D_ARGS(_Splat1, sampler_Splat1),
                  TEXTURE2D_ARGS(_Normal1, sampler_Normal1), _NormalScale1,
                  TEXTURE2D_ARGS(_Mask1, sampler_Mask1), hasMask.y,
                  _MaskMapRemapScale1, _MaskMapRemapOffset1,
                  diff[1], nrm[1], masks[1]);

    HexSplatLayer(IN.uvSplat23.xy,
                  TEXTURE2D_ARGS(_Splat2, sampler_Splat2),
                  TEXTURE2D_ARGS(_Normal2, sampler_Normal2), _NormalScale2,
                  TEXTURE2D_ARGS(_Mask2, sampler_Mask2), hasMask.z,
                  _MaskMapRemapScale2, _MaskMapRemapOffset2,
                  diff[2], nrm[2], masks[2]);

    HexSplatLayer(IN.uvSplat23.zw,
                  TEXTURE2D_ARGS(_Splat3, sampler_Splat3),
                  TEXTURE2D_ARGS(_Normal3, sampler_Normal3), _NormalScale3,
                  TEXTURE2D_ARGS(_Mask3, sampler_Mask3), hasMask.w,
                  _MaskMapRemapScale3, _MaskMapRemapOffset3,
                  diff[3], nrm[3], masks[3]);

    defaultSmoothness = half4(diff[0].a, diff[1].a, diff[2].a, diff[3].a);
    defaultSmoothness *= half4(_Smoothness0, _Smoothness1, _Smoothness2, _Smoothness3);

#ifndef _TERRAIN_BLEND_HEIGHT
    if (_NumLayersCount <= 4)
    {
        half4 opacityAsDensity = saturate((half4(diff[0].a, diff[1].a, diff[2].a, diff[3].a) - (1 - splatControl)) * 20.0);
        opacityAsDensity += 0.001h * splatControl;
        half4 useOpacityAsDensityParam = { _DiffuseRemapScale0.w, _DiffuseRemapScale1.w, _DiffuseRemapScale2.w, _DiffuseRemapScale3.w };
        splatControl = lerp(opacityAsDensity, splatControl, useOpacityAsDensityParam);
    }
#endif

    weight = dot(splatControl, 1.0h);

#ifdef TERRAIN_SPLAT_ADDPASS
    clip(weight <= 0.005h ? -1.0h : 1.0h);
#endif

#ifndef _TERRAIN_BASEMAP_GEN
    splatControl /= (weight + HALF_MIN);
#endif

    mixedDiffuse  = diff[0] * half4(_DiffuseRemapScale0.rgb * splatControl.rrr, 1.0h);
    mixedDiffuse += diff[1] * half4(_DiffuseRemapScale1.rgb * splatControl.ggg, 1.0h);
    mixedDiffuse += diff[2] * half4(_DiffuseRemapScale2.rgb * splatControl.bbb, 1.0h);
    mixedDiffuse += diff[3] * half4(_DiffuseRemapScale3.rgb * splatControl.aaa, 1.0h);

    #if defined(_NORMALMAP)
        half3 nrmSum = nrm[0] * splatControl.r
                     + nrm[1] * splatControl.g
                     + nrm[2] * splatControl.b
                     + nrm[3] * splatControl.a;
        #if !HALF_IS_FLOAT
            nrmSum.z += half(0.01);
        #else
            nrmSum.z += 1e-5f;
        #endif
        mixedNormal = normalize(nrmSum);
    #else
        mixedNormal = half3(0, 0, 1);
    #endif
}

#ifdef _TERRAIN_BLEND_HEIGHT
void HeightBasedSplatModify(inout half4 splatControl, in half4 masks[4])
{
    half4 splatHeight = half4(masks[0].b, masks[1].b, masks[2].b, masks[3].b) * splatControl.rgba;
    half maxHeight = max(splatHeight.r, max(splatHeight.g, max(splatHeight.b, splatHeight.a)));

    half transition = max(_HeightTransition, 1e-5);
    half4 weightedHeights = splatHeight + transition - maxHeight.xxxx;
    weightedHeights = max(0, weightedHeights);
    weightedHeights = (weightedHeights + 1e-6) * splatControl;
    half sumHeight = max(dot(weightedHeights, half4(1, 1, 1, 1)), 1e-6);
    splatControl = weightedHeights / sumHeight.xxxx;
}
#endif

void SplatmapFinalColor(inout half4 color, half fogCoord)
{
    color.rgb *= color.a;

    #ifndef TERRAIN_GBUFFER
    #ifdef TERRAIN_SPLAT_ADDPASS
        color.rgb = MixFogColor(color.rgb, half3(0,0,0), fogCoord);
    #else
        color.rgb = MixFog(color.rgb, fogCoord);
    #endif
    #endif
}

void SetupTerrainDebugTextureData(inout InputData inputData, float2 uv) {}

// ============================================================
// Vertex
// ============================================================
Varyings SplatmapVert(Attributes v)
{
    Varyings o = (Varyings)0;

    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    TerrainInstancing(v.positionOS, v.normalOS, v.texcoord);

    VertexPositionInputs vpi = GetVertexPositionInputs(v.positionOS.xyz);

    o.uvMainAndLM.xy = v.texcoord;
    o.uvMainAndLM.zw = v.texcoord * unity_LightmapST.xy + unity_LightmapST.zw;

    o.uvSplat01.xy = TRANSFORM_TEX(v.texcoord, _Splat0);
    o.uvSplat01.zw = TRANSFORM_TEX(v.texcoord, _Splat1);
    o.uvSplat23.xy = TRANSFORM_TEX(v.texcoord, _Splat2);
    o.uvSplat23.zw = TRANSFORM_TEX(v.texcoord, _Splat3);

#if defined(DYNAMICLIGHTMAP_ON)
    o.dynamicLightmapUV = v.texcoord * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
#endif

    #if defined(_NORMALMAP) && !defined(ENABLE_TERRAIN_PERPIXEL_NORMAL)
        half3 viewDirWS = GetWorldSpaceNormalizeViewDir(vpi.positionWS);
        float4 vertexTangent = float4(cross(float3(0, 0, 1), v.normalOS), 1.0);
        VertexNormalInputs nIn = GetVertexNormalInputs(v.normalOS, vertexTangent);

        o.normal = half4(nIn.normalWS, viewDirWS.x);
        o.tangent = half4(nIn.tangentWS, viewDirWS.y);
        o.bitangent = half4(nIn.bitangentWS, viewDirWS.z);
    #else
        o.normal = TransformObjectToWorldNormal(v.normalOS);
        OUTPUT_SH4(vpi.positionWS, o.normal.xyz, GetWorldSpaceNormalizeViewDir(vpi.positionWS), o.vertexSH, o.probeOcclusion);
    #endif

    half fogFactor = 0;
    #if !defined(_FOG_FRAGMENT)
        fogFactor = ComputeFogFactor(vpi.positionCS.z);
    #endif

    #ifdef _ADDITIONAL_LIGHTS_VERTEX
        o.fogFactorAndVertexLight.x = fogFactor;
        o.fogFactorAndVertexLight.yzw = VertexLighting(vpi.positionWS, o.normal.xyz);
    #else
        o.fogFactor = fogFactor;
    #endif

    o.positionWS = vpi.positionWS;
    o.clipPos = vpi.positionCS;

    #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
        o.shadowCoord = GetShadowCoord(vpi);
    #endif

    return o;
}

// ============================================================
// Fragment
// ============================================================
#ifdef TERRAIN_GBUFFER
GBufferFragOutput SplatmapFragment(Varyings IN)
#else
void SplatmapFragment(
    Varyings IN
    , out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
    )
#endif
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
#ifdef _ALPHATEST_ON
    ClipHoles(IN.uvMainAndLM.xy);
#endif

    half3 normalTS = half3(0.0h, 0.0h, 1.0h);

    float2 splatUV = (IN.uvMainAndLM.xy * (_Control_TexelSize.zw - 1.0f) + 0.5f) * _Control_TexelSize.xy;
    half4 splatControl = SAMPLE_TEXTURE2D(_Control, sampler_Control, splatUV);

    half alpha = dot(splatControl, 1.0h);

    half4 masks[4];
    half weight;
    half4 mixedDiffuse;
    half4 defaultSmoothness;

    SplatmapMixHex(IN, splatControl, weight, mixedDiffuse, defaultSmoothness, normalTS, masks);

#ifdef _TERRAIN_BLEND_HEIGHT
    if (_NumLayersCount <= 4)
        HeightBasedSplatModify(splatControl, masks);
#endif

    half3 albedo = mixedDiffuse.rgb;

    half4 defaultMetallic = half4(_Metallic0, _Metallic1, _Metallic2, _Metallic3);
    half4 defaultOcclusion = half4(_MaskMapRemapScale0.g, _MaskMapRemapScale1.g, _MaskMapRemapScale2.g, _MaskMapRemapScale3.g)
                           + half4(_MaskMapRemapOffset0.g, _MaskMapRemapOffset1.g, _MaskMapRemapOffset2.g, _MaskMapRemapOffset3.g);

    half4 hasMask = half4(_LayerHasMask0, _LayerHasMask1, _LayerHasMask2, _LayerHasMask3);
    half4 maskSmoothness = half4(masks[0].a, masks[1].a, masks[2].a, masks[3].a);
    defaultSmoothness = lerp(defaultSmoothness, maskSmoothness, hasMask);
    half smoothness = dot(splatControl, defaultSmoothness);

    half4 maskMetallic = half4(masks[0].r, masks[1].r, masks[2].r, masks[3].r);
    defaultMetallic = lerp(defaultMetallic, maskMetallic, hasMask);
    half metallic = dot(splatControl, defaultMetallic);

    half4 maskOcclusion = half4(masks[0].g, masks[1].g, masks[2].g, masks[3].g);
    defaultOcclusion = lerp(defaultOcclusion, maskOcclusion, hasMask);
    half occlusion = dot(splatControl, defaultOcclusion);

    InputData inputData;
    InitializeInputData(IN, normalTS, inputData);
    SetupTerrainDebugTextureData(inputData, IN.uvMainAndLM.xy);

#if defined(_DBUFFER)
    half3 specular = half3(0.0h, 0.0h, 0.0h);
    ApplyDecal(IN.clipPos, albedo, specular, inputData.normalWS, metallic, occlusion, smoothness);
#endif

    InitializeBakedGIData(IN, inputData);

#ifdef TERRAIN_GBUFFER
    BRDFData brdfData;
    InitializeBRDFData(albedo, metallic, half3(0,0,0), smoothness, alpha, brdfData);

    half4 color;
    Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS, inputData.shadowMask);
    MixRealtimeAndBakedGI(mainLight, inputData.normalWS, inputData.bakedGI, inputData.shadowMask);
    color.rgb = GlobalIllumination(brdfData, (BRDFData)0, 0, inputData.bakedGI, occlusion, inputData.positionWS,
                                   inputData.normalWS, inputData.viewDirectionWS, inputData.normalizedScreenSpaceUV);
    color.a = alpha;
    SplatmapFinalColor(color, inputData.fogCoord);

    brdfData.albedo.rgb *= alpha;
    brdfData.diffuse.rgb *= alpha;
    brdfData.specular.rgb *= alpha;
    brdfData.reflectivity *= alpha;
    inputData.normalWS = inputData.normalWS * alpha;
    smoothness *= alpha;

    return PackGBuffersBRDFData(brdfData, inputData, smoothness, color.rgb, occlusion);
#else
    half4 color = UniversalFragmentPBR(inputData, albedo, metallic, half3(0,0,0), smoothness, occlusion, half3(0,0,0), alpha);
    SplatmapFinalColor(color, inputData.fogCoord);

    outColor = half4(color.rgb, 1.0h);

#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
#endif
}

// ============================================================
// Shadow / Depth passes
// ============================================================
float3 _LightDirection;
float3 _LightPosition;

struct AttributesLean
{
    float4 position    : POSITION;
    float3 normalOS    : NORMAL;
    float2 texcoord    : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct VaryingsLean
{
    float4 clipPos     : SV_POSITION;
    float2 texcoord    : TEXCOORD0;
    UNITY_VERTEX_OUTPUT_STEREO
};

VaryingsLean ShadowPassVertex(AttributesLean v)
{
    VaryingsLean o = (VaryingsLean)0;
    UNITY_SETUP_INSTANCE_ID(v);
    TerrainInstancing(v.position, v.normalOS, v.texcoord);

    float3 positionWS = TransformObjectToWorld(v.position.xyz);
    float3 normalWS = TransformObjectToWorldNormal(v.normalOS);

#if _CASTING_PUNCTUAL_LIGHT_SHADOW
    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
#else
    float3 lightDirectionWS = _LightDirection;
#endif

    float4 clipPos = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

#if UNITY_REVERSED_Z
    clipPos.z = min(clipPos.z, UNITY_NEAR_CLIP_VALUE);
#else
    clipPos.z = max(clipPos.z, UNITY_NEAR_CLIP_VALUE);
#endif

    o.clipPos = clipPos;
    o.texcoord = v.texcoord;
    return o;
}

half4 ShadowPassFragment(VaryingsLean IN) : SV_TARGET
{
#ifdef _ALPHATEST_ON
    ClipHoles(IN.texcoord);
#endif
    return 0;
}

VaryingsLean DepthOnlyVertex(AttributesLean v)
{
    VaryingsLean o = (VaryingsLean)0;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    TerrainInstancing(v.position, v.normalOS);
    o.clipPos = TransformObjectToHClip(v.position.xyz);
    o.texcoord = v.texcoord;
    return o;
}

half4 DepthOnlyFragment(VaryingsLean IN) : SV_TARGET
{
#ifdef _ALPHATEST_ON
    ClipHoles(IN.texcoord);
#endif
#ifdef SCENESELECTIONPASS
    return half4(_ObjectId, _PassValue, 1.0, 1.0);
#endif
    return IN.clipPos.z;
}

#endif
