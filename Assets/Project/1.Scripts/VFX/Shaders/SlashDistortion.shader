Shader "Game/VFX/SlashDistortion"
{
    Properties
    {
        _DistortionStrength ("Distortion Strength", Range(0, 0.15)) = 0.025
        _DistortionTex      ("Distortion Noise (RG = offset)", 2D) = "gray" {}
        _DistortionScale    ("Distortion Tile",     Vector) = (2, 1, 0, 0)
        _DistortionPanSpeed ("Distortion Pan Speed (uv/sec)", Vector) = (-2.5, 0.3, 0, 0)
        _EdgeSoftness       ("Edge Softness (V)",   Range(0.5, 6)) = 2.0
        _FadePower          ("Alpha Fade Power",    Range(0.5, 6)) = 2.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent+99"     // 슬래시 본체보다 살짝 먼저
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "DisableBatching" = "True"
        }

        // URP Opaque Texture 필수:
        //   Pipeline Asset > Opaque Texture = ON
        Pass
        {
            Name "SlashDistortion"
            Tags { "LightMode" = "UniversalForward" }

            Blend One Zero   // 왜곡은 화면 덮어쓰기 방식
            ZWrite Off
            ZTest  LEqual
            Cull   Off

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos  : TEXCOORD0;
                float2 uv         : TEXCOORD1;
                float4 color      : COLOR;
            };

            TEXTURE2D(_DistortionTex); SAMPLER(sampler_DistortionTex);

            CBUFFER_START(UnityPerMaterial)
                float  _DistortionStrength;
                float4 _DistortionTex_ST;
                float4 _DistortionScale;
                float4 _DistortionPanSpeed;
                float  _EdgeSoftness;
                float  _FadePower;
            CBUFFER_END

            Varyings Vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.screenPos  = ComputeScreenPos(OUT.positionCS);
                OUT.uv         = IN.uv;
                OUT.color      = IN.color;
                return OUT;
            }

            half4 Frag (Varyings IN) : SV_Target
            {
                // 1) Distortion noise sample (RG = xy offset)
                float2 nUV = IN.uv * _DistortionScale.xy + _DistortionPanSpeed.xy * _Time.y;
                nUV = TRANSFORM_TEX(nUV, _DistortionTex);
                float2 offset = SAMPLE_TEXTURE2D(_DistortionTex, sampler_DistortionTex, nUV).rg;
                offset = (offset - 0.5) * 2.0;  // [-1, 1]

                // 2) Strength × age(vertex.a) × edge softness
                float ageFade = pow(saturate(IN.color.a), _FadePower);
                float distFromCenter = abs(IN.uv.y - 0.5) * 2.0;
                float edgeMask = 1.0 - pow(saturate(distFromCenter), _EdgeSoftness);
                float strength = _DistortionStrength * ageFade * edgeMask;

                // 3) Screen UV에 오프셋 적용해서 opaque texture 샘플
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
                screenUV += offset * strength;

                half3 col = SampleSceneColor(screenUV);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
