Shader "Game/VFX/ShockwaveRing"
{
    Properties
    {
        _Color       ("Color",               Color) = (1, 0.9, 0.7, 1)
        _Emission    ("Emission Intensity",  Range(0, 10)) = 3.0
        _RingThickness ("Ring Thickness",    Range(0.01, 0.5)) = 0.08
        _Softness    ("Edge Softness",       Range(0.001, 0.3)) = 0.04
        _Progress    ("Progress (0..1 quad local)", Range(0, 1)) = 0.5
        _AlphaMul    ("Alpha Multiplier",    Range(0, 2)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent+200"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ShockwaveRing"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct A { float4 p : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 p : SV_POSITION; float2 uv : TEXCOORD0; };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float  _Emission;
                float  _RingThickness;
                float  _Softness;
                float  _Progress;
                float  _AlphaMul;
            CBUFFER_END

            V Vert(A i)
            {
                V o;
                o.p  = TransformObjectToHClip(i.p.xyz);
                o.uv = i.uv;
                return o;
            }

            half4 Frag(V i) : SV_Target
            {
                float2 c = i.uv - 0.5;
                float r = length(c) * 2.0;  // 0..1 (corner=sqrt2지만 clamp)

                float progress = saturate(_Progress);
                float ringCenter = progress;
                float halfT = _RingThickness * 0.5;

                // 링 두께 내부 1, 외부 0 (부드럽게)
                float d = abs(r - ringCenter);
                float ring = 1.0 - smoothstep(halfT - _Softness, halfT, d);

                // 외곽으로 갈수록 페이드 (진행 0.8 이후 빠르게 감쇠)
                float decay = 1.0 - smoothstep(0.7, 1.0, progress);

                float a = ring * decay * _AlphaMul;
                float3 col = _Color.rgb * _Emission * a;
                return half4(col, a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
