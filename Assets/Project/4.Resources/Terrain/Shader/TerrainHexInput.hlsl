#ifndef CUSTOM_TERRAIN_HEX_INPUT_INCLUDED
#define CUSTOM_TERRAIN_HEX_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DebugMipmapStreamingMacros.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _MainTex_ST;
    half4 _BaseColor;
    half _Cutoff;

    UNITY_TEXTURE_STREAMING_DEBUG_VARS_FOR_TEX(_Control);
    float4 _Splat0_TexelSize, _Splat1_TexelSize, _Splat2_TexelSize, _Splat3_TexelSize;
    UNITY_TEXTURE_STREAMING_DEBUG_VARS_FOR_TEX(_Splat0);
    UNITY_TEXTURE_STREAMING_DEBUG_VARS_FOR_TEX(_Splat1);
    UNITY_TEXTURE_STREAMING_DEBUG_VARS_FOR_TEX(_Splat2);
    UNITY_TEXTURE_STREAMING_DEBUG_VARS_FOR_TEX(_Splat3);
CBUFFER_END

#define _Surface 0.0 // Terrain is always opaque

CBUFFER_START(_Terrain)
    half _NormalScale0, _NormalScale1, _NormalScale2, _NormalScale3;
    half _Metallic0, _Metallic1, _Metallic2, _Metallic3;
    half _Smoothness0, _Smoothness1, _Smoothness2, _Smoothness3;
    half4 _DiffuseRemapScale0, _DiffuseRemapScale1, _DiffuseRemapScale2, _DiffuseRemapScale3;
    half4 _MaskMapRemapOffset0, _MaskMapRemapOffset1, _MaskMapRemapOffset2, _MaskMapRemapOffset3;
    half4 _MaskMapRemapScale0, _MaskMapRemapScale1, _MaskMapRemapScale2, _MaskMapRemapScale3;

    float4 _Control_ST;
    float4 _Control_TexelSize;
    half _DiffuseHasAlpha0, _DiffuseHasAlpha1, _DiffuseHasAlpha2, _DiffuseHasAlpha3;
    half _LayerHasMask0, _LayerHasMask1, _LayerHasMask2, _LayerHasMask3;
    half4 _Splat0_ST, _Splat1_ST, _Splat2_ST, _Splat3_ST;
    half _HeightTransition;
    half _NumLayersCount;

    // Hex params (custom)
    float _HexScale;
    float _HexRotationStrength;
    float _HexContrast;

    #ifdef UNITY_INSTANCING_ENABLED
    float4 _TerrainHeightmapRecipSize;
    float4 _TerrainHeightmapScale;
    #endif
    #ifdef SCENESELECTIONPASS
    int _ObjectId;
    int _PassValue;
    #endif
CBUFFER_END


TEXTURE2D(_Control);    SAMPLER(sampler_Control);
TEXTURE2D(_Splat0);     SAMPLER(sampler_Splat0);
TEXTURE2D(_Splat1);     SAMPLER(sampler_Splat1);
TEXTURE2D(_Splat2);     SAMPLER(sampler_Splat2);
TEXTURE2D(_Splat3);     SAMPLER(sampler_Splat3);

TEXTURE2D(_Normal0);     SAMPLER(sampler_Normal0);
TEXTURE2D(_Normal1);     SAMPLER(sampler_Normal1);
TEXTURE2D(_Normal2);     SAMPLER(sampler_Normal2);
TEXTURE2D(_Normal3);     SAMPLER(sampler_Normal3);

TEXTURE2D(_Mask0);      SAMPLER(sampler_Mask0);
TEXTURE2D(_Mask1);      SAMPLER(sampler_Mask1);
TEXTURE2D(_Mask2);      SAMPLER(sampler_Mask2);
TEXTURE2D(_Mask3);      SAMPLER(sampler_Mask3);

TEXTURE2D(_MainTex);       SAMPLER(sampler_MainTex);
TEXTURE2D(_SpecGlossMap);  SAMPLER(sampler_SpecGlossMap);
TEXTURE2D(_MetallicTex);   SAMPLER(sampler_MetallicTex);

#if defined(UNITY_INSTANCING_ENABLED) && defined(_TERRAIN_INSTANCED_PERPIXEL_NORMAL)
#define ENABLE_TERRAIN_PERPIXEL_NORMAL
#endif

#ifdef UNITY_INSTANCING_ENABLED
TEXTURE2D(_TerrainHeightmapTexture);
TEXTURE2D(_TerrainNormalmapTexture);
SAMPLER(sampler_TerrainNormalmapTexture);
#endif

UNITY_INSTANCING_BUFFER_START(Terrain)
UNITY_DEFINE_INSTANCED_PROP(float4, _TerrainPatchInstanceData)
UNITY_INSTANCING_BUFFER_END(Terrain)

#ifdef _ALPHATEST_ON
TEXTURE2D(_TerrainHolesTexture);
SAMPLER(sampler_TerrainHolesTexture);

float SampleTerrainHolesTexture(float2 uv)
{
    return SAMPLE_TEXTURE2D(_TerrainHolesTexture, sampler_TerrainHolesTexture, uv).r;
}

void ClipHoles(float2 uv)
{
    float hole = SampleTerrainHolesTexture(uv);
    float epsilon = 0.0005f;
    clip(hole < epsilon ? -1 : 1);
}
#endif

// ========================================================================
// HEX TILING (Mikkelsen-style)
// Samples texture 3 times across adjacent triangular cells of a hex grid,
// each with a per-cell random offset + rotation, blended by barycentric weights.
// Eliminates visible tile repetition without obvious cell boundaries.
// ========================================================================

struct HexCell
{
    float2 uv1, uv2, uv3;
    float3 weights;
    float2 dx, dy;
};

float2 HexHash2(float2 p)
{
    float2 r = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
    return frac(sin(r) * 43758.5453);
}

void HexTriGrid(float2 uv, out float3 w, out float2 v1, out float2 v2, out float2 v3)
{
    // Skew to equilateral triangle grid
    const float2x2 skew = float2x2(1.0, 0.5, 0.0, 0.8660254);
    const float2x2 invSkew = float2x2(1.0, -0.5773503, 0.0, 1.1547005);
    float2 skewedCoord = mul(invSkew, uv);

    float2 baseId = floor(skewedCoord);
    float3 tri;
    tri.xy = frac(skewedCoord);
    tri.z = 1.0 - tri.x - tri.y;

    if (tri.z > 0.0)
    {
        w  = float3(tri.z, tri.y, tri.x);
        v1 = baseId;
        v2 = baseId + float2(0, 1);
        v3 = baseId + float2(1, 0);
    }
    else
    {
        w  = float3(-tri.z, 1.0 - tri.y, 1.0 - tri.x);
        v1 = baseId + float2(1, 1);
        v2 = baseId + float2(1, 0);
        v3 = baseId + float2(0, 1);
    }
}

HexCell ComputeHexCell(float2 uv, float scale, float rotStrength, float contrast)
{
    HexCell o;
    o.dx = ddx(uv);
    o.dy = ddy(uv);

    float3 w;
    float2 v1, v2, v3;
    HexTriGrid(uv * scale, w, v1, v2, v3);

    // Per-cell random rotation + offset
    float2 h1 = HexHash2(v1);
    float2 h2 = HexHash2(v2);
    float2 h3 = HexHash2(v3);

    const float TAU = 6.2831853;
    float a1 = (h1.y - 0.5) * TAU * rotStrength;
    float a2 = (h2.y - 0.5) * TAU * rotStrength;
    float a3 = (h3.y - 0.5) * TAU * rotStrength;

    float2 cs1 = float2(cos(a1), sin(a1));
    float2 cs2 = float2(cos(a2), sin(a2));
    float2 cs3 = float2(cos(a3), sin(a3));

    float2 u1 = uv + h1;
    float2 u2 = uv + h2;
    float2 u3 = uv + h3;

    // Rotate sample UV
    o.uv1 = float2(u1.x * cs1.x - u1.y * cs1.y, u1.x * cs1.y + u1.y * cs1.x);
    o.uv2 = float2(u2.x * cs2.x - u2.y * cs2.y, u2.x * cs2.y + u2.y * cs2.x);
    o.uv3 = float2(u3.x * cs3.x - u3.y * cs3.y, u3.x * cs3.y + u3.y * cs3.x);

    // Sharpen weights with contrast (higher = sharper cell boundaries)
    float3 cw = pow(max(w, 1e-4), contrast);
    cw = cw / (cw.x + cw.y + cw.z);

    // Variance-preserving normalization (Mikkelsen): prevents darkening
    // at cell boundaries and makes seams effectively invisible.
    o.weights = cw / sqrt(dot(cw, cw));

    return o;
}

half4 HexSample(HexCell cell, TEXTURE2D_PARAM(tex, samp))
{
    half4 c1 = SAMPLE_TEXTURE2D_GRAD(tex, samp, cell.uv1, cell.dx, cell.dy);
    half4 c2 = SAMPLE_TEXTURE2D_GRAD(tex, samp, cell.uv2, cell.dx, cell.dy);
    half4 c3 = SAMPLE_TEXTURE2D_GRAD(tex, samp, cell.uv3, cell.dx, cell.dy);
    // Per-sample mean removal + variance-preserving sum + add mean back.
    // This is the trick that hides the seam: blend deviations, not values.
    half4 mean = (c1 + c2 + c3) * (1.0h / 3.0h);
    half4 d1 = c1 - mean;
    half4 d2 = c2 - mean;
    half4 d3 = c3 - mean;
    return mean + d1 * cell.weights.x + d2 * cell.weights.y + d3 * cell.weights.z;
}

// ========================================================================
// Standard surface data path (for basemap pass, not used in main splat pass)
// ========================================================================

half4 SampleMetallicSpecGloss(float2 uv, half albedoAlpha)
{
    half4 specGloss;
    specGloss = SAMPLE_TEXTURE2D(_MetallicTex, sampler_MetallicTex, uv);
    specGloss.a = albedoAlpha;
    return specGloss;
}

inline void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData outSurfaceData)
{
    outSurfaceData = (SurfaceData)0;
    half4 albedoSmoothness = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
    outSurfaceData.alpha = 1;

    half4 specGloss = SampleMetallicSpecGloss(uv, albedoSmoothness.a);
    outSurfaceData.albedo = albedoSmoothness.rgb;

    outSurfaceData.metallic = specGloss.r;
    outSurfaceData.specular = half3(0.0h, 0.0h, 0.0h);

    outSurfaceData.smoothness = specGloss.a;
    outSurfaceData.normalTS = SampleNormal(uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap));
    outSurfaceData.occlusion = 1;
    outSurfaceData.emission = 0;
}

void TerrainInstancing(inout float4 positionOS, inout float3 normal, inout float2 uv)
{
#ifdef UNITY_INSTANCING_ENABLED
    float2 patchVertex = positionOS.xy;
    float4 instanceData = UNITY_ACCESS_INSTANCED_PROP(Terrain, _TerrainPatchInstanceData);

    float2 sampleCoords = (patchVertex.xy + instanceData.xy) * instanceData.z;
    float height = UnpackHeightmap(_TerrainHeightmapTexture.Load(int3(sampleCoords, 0)));

    positionOS.xz = sampleCoords * _TerrainHeightmapScale.xz;
    positionOS.y = height * _TerrainHeightmapScale.y;

#ifdef ENABLE_TERRAIN_PERPIXEL_NORMAL
    normal = float3(0, 1, 0);
#else
    normal = _TerrainNormalmapTexture.Load(int3(sampleCoords, 0)).rgb * 2 - 1;
#endif
    uv = sampleCoords * _TerrainHeightmapRecipSize.zw;
#endif
}

void TerrainInstancing(inout float4 positionOS, inout float3 normal)
{
    float2 uv = { 0, 0 };
    TerrainInstancing(positionOS, normal, uv);
}

void TerrainInstancing(inout float4 positionOS)
{
    float3 normal = { 0, 0, 0 };
    TerrainInstancing(positionOS, normal);
}
#endif
