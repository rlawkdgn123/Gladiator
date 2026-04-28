#ifndef CUSTOM_TERRAIN_HEX_DEPTHNORMALS_PASS_INCLUDED
#define CUSTOM_TERRAIN_HEX_DEPTHNORMALS_PASS_INCLUDED

#include "TerrainHexPasses.hlsl"

struct AttributesDepthNormal
{
    float4 positionOS : POSITION;
    half3 normalOS    : NORMAL;
    float2 texcoord   : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct VaryingsDepthNormal
{
    float4 uvMainAndLM   : TEXCOORD0;
    float4 uvSplat01     : TEXCOORD1;
    float4 uvSplat23     : TEXCOORD2;

    #if defined(_NORMALMAP) && !defined(ENABLE_TERRAIN_PERPIXEL_NORMAL)
        half4 normal     : TEXCOORD3;
        half4 tangent    : TEXCOORD4;
        half4 bitangent  : TEXCOORD5;
    #else
        half3 normal     : TEXCOORD3;
    #endif

    float4 clipPos       : SV_POSITION;
    UNITY_VERTEX_OUTPUT_STEREO
};

VaryingsDepthNormal DepthNormalOnlyVertex(AttributesDepthNormal v)
{
    VaryingsDepthNormal o = (VaryingsDepthNormal)0;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    TerrainInstancing(v.positionOS, v.normalOS, v.texcoord);

    const VertexPositionInputs vpi = GetVertexPositionInputs(v.positionOS.xyz);

    o.uvMainAndLM.xy = v.texcoord;
    o.uvMainAndLM.zw = v.texcoord * unity_LightmapST.xy + unity_LightmapST.zw;
    o.uvSplat01.xy = TRANSFORM_TEX(v.texcoord, _Splat0);
    o.uvSplat01.zw = TRANSFORM_TEX(v.texcoord, _Splat1);
    o.uvSplat23.xy = TRANSFORM_TEX(v.texcoord, _Splat2);
    o.uvSplat23.zw = TRANSFORM_TEX(v.texcoord, _Splat3);

    #if defined(_NORMALMAP) && !defined(ENABLE_TERRAIN_PERPIXEL_NORMAL)
        half3 viewDirWS = GetWorldSpaceNormalizeViewDir(vpi.positionWS);
        float4 vertexTangent = float4(cross(float3(0, 0, 1), v.normalOS), 1.0);
        VertexNormalInputs nIn = GetVertexNormalInputs(v.normalOS, vertexTangent);

        o.normal = half4(nIn.normalWS, viewDirWS.x);
        o.tangent = half4(nIn.tangentWS, viewDirWS.y);
        o.bitangent = half4(nIn.bitangentWS, viewDirWS.z);
    #else
        o.normal = TransformObjectToWorldNormal(v.normalOS);
    #endif

    o.clipPos = vpi.positionCS;
    return o;
}

void DepthNormalOnlyFragment(
    VaryingsDepthNormal IN
    , out half4 outNormalWS : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
    )
{
    #ifdef _ALPHATEST_ON
        ClipHoles(IN.uvMainAndLM.xy);
    #endif

    float2 splatUV = (IN.uvMainAndLM.xy * (_Control_TexelSize.zw - 1.0f) + 0.5f) * _Control_TexelSize.xy;
    half4 splatControl = SAMPLE_TEXTURE2D(_Control, sampler_Control, splatUV);

    half3 normalTS = half3(0.0h, 0.0h, 1.0h);

    #ifdef _NORMALMAP
        // Hex sample normals to match forward pass orientation
        HexCell c0 = ComputeHexCell(IN.uvSplat01.xy, _HexScale, _HexRotationStrength, _HexContrast);
        HexCell c1 = ComputeHexCell(IN.uvSplat01.zw, _HexScale, _HexRotationStrength, _HexContrast);
        HexCell c2 = ComputeHexCell(IN.uvSplat23.xy, _HexScale, _HexRotationStrength, _HexContrast);
        HexCell c3 = ComputeHexCell(IN.uvSplat23.zw, _HexScale, _HexRotationStrength, _HexContrast);

        half3 n0 = UnpackNormalScale(HexSample(c0, TEXTURE2D_ARGS(_Normal0, sampler_Normal0)), _NormalScale0);
        half3 n1 = UnpackNormalScale(HexSample(c1, TEXTURE2D_ARGS(_Normal1, sampler_Normal1)), _NormalScale1);
        half3 n2 = UnpackNormalScale(HexSample(c2, TEXTURE2D_ARGS(_Normal2, sampler_Normal2)), _NormalScale2);
        half3 n3 = UnpackNormalScale(HexSample(c3, TEXTURE2D_ARGS(_Normal3, sampler_Normal3)), _NormalScale3);
        half3 nrm = n0 * splatControl.r + n1 * splatControl.g + n2 * splatControl.b + n3 * splatControl.a;
        nrm.z += 1e-5;
        normalTS = normalize(nrm);
    #endif

    #if defined(_NORMALMAP) && !defined(ENABLE_TERRAIN_PERPIXEL_NORMAL)
        half3 normalWS = TransformTangentToWorld(normalTS, half3x3(-IN.tangent.xyz, IN.bitangent.xyz, IN.normal.xyz));
    #elif defined(ENABLE_TERRAIN_PERPIXEL_NORMAL)
        float2 sampleCoords = (IN.uvMainAndLM.xy / _TerrainHeightmapRecipSize.zw + 0.5f) * _TerrainHeightmapRecipSize.xy;
        half3 nWS = TransformObjectToWorldNormal(normalize(SAMPLE_TEXTURE2D(_TerrainNormalmapTexture, sampler_TerrainNormalmapTexture, sampleCoords).rgb * 2 - 1));
        half3 tWS = cross(GetObjectToWorldMatrix()._13_23_33, nWS);
        half3 normalWS = TransformTangentToWorld(normalTS, half3x3(-tWS, cross(nWS, tWS), nWS));
    #else
        half3 normalWS = IN.normal;
    #endif

    normalWS = NormalizeNormalPerPixel(normalWS);
    outNormalWS = half4(normalWS, 0.0);

    #ifdef _WRITE_RENDERING_LAYERS
        outRenderingLayers = EncodeMeshRenderingLayer();
    #endif
}

#endif
