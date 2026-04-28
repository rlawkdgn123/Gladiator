Shader "Game/VFX/SlashTrail"
{
    Properties
    {
        [Header(Colors)]
        _CoreColor       ("Core Color",           Color) = (0.95, 0.96, 1.0, 1.0)
        _RimColor        ("Rim Color (warm)",     Color) = (1.15, 1.05, 0.90, 1.0)
        _Emission        ("Emission Intensity",   Range(0, 10)) = 1.2
        _AlphaMultiplier ("Alpha Multiplier",     Range(0, 2)) = 1.0

        [Header(Noise)]
        _NoiseTex     ("Noise Texture (grayscale)", 2D) = "white" {}
        _NoiseScale   ("Noise Tile",     Vector) = (2, 1, 0, 0)
        _NoisePanSpeed("Noise Pan Speed (uv/sec)", Vector) = (-3.0, 0.0, 0, 0)
        _NoiseStrength("Noise Strength", Range(0, 1)) = 0.75

        [Header(Rim Shape)]
        _RimPower     ("Rim Falloff Power (V)",    Range(0.5, 8)) = 2.0
        _CoreWidth    ("Core Width (V center)",    Range(0.0, 1)) = 0.35

        [Header(Fade Curve)]
        _FadePower    ("Alpha Fade Power (age)",   Range(0.5, 6)) = 2.0

        [Header(Blend)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1  // One
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 1  // One (Additive)
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent+100"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "DisableBatching" = "True"
        }

        Pass
        {
            Name "SlashTrailForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            ZTest  LEqual
            Cull   Off

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _CoreColor;
                float4 _RimColor;
                float  _Emission;
                float  _AlphaMultiplier;

                float4 _NoiseTex_ST;
                float4 _NoiseScale;
                float4 _NoisePanSpeed;
                float  _NoiseStrength;

                float  _RimPower;
                float  _CoreWidth;
                float  _FadePower;
            CBUFFER_END

            Varyings Vert (Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv         = IN.uv;
                OUT.color      = IN.color;
                return OUT;
            }

            half4 Frag (Varyings IN) : SV_Target
            {
                // --- UV ---
                // uv.x = 진행 방향 (0 = 오래됨, 1 = 최신)
                // uv.y = 날 두께 방향 (0 = base, 1 = tip 쪽)
                float u = IN.uv.x;
                float v = IN.uv.y;

                // --- Age fade (vertex color alpha에서 옴) ---
                float ageAlpha = IN.color.a;            // 1 = 갓 스폰, 0 = 수명 다함
                float fade     = pow(saturate(ageAlpha), _FadePower);

                // --- Noise panning ---
                float2 nUV  = IN.uv * _NoiseScale.xy + _NoisePanSpeed.xy * _Time.y;
                nUV = TRANSFORM_TEX(nUV, _NoiseTex);
                float noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, nUV).r;
                // 노이즈 마스크: 너무 깨지지 않게 [1-strength, 1] 범위로
                float noiseMask = lerp(1.0 - _NoiseStrength, 1.0, noise);

                // --- Rim shape (V축 기반) ---
                // V=0 또는 V=1 (가장자리)이 어두워지고, 중앙이 밝음.
                float distFromCenter = abs(v - 0.5) * 2.0;  // 0..1
                float rimShape = pow(saturate(distFromCenter), _RimPower);
                float coreShape = 1.0 - smoothstep(0.0, saturate(_CoreWidth), distFromCenter);

                // --- Color blend ---
                // 중앙은 core, 가장자리는 rim
                float3 color = lerp(_RimColor.rgb, _CoreColor.rgb, coreShape);

                // --- Emission (HDR) ---
                color *= _Emission;

                // --- Alpha 합성 ---
                // 가장자리 소프트(rim alpha) + 진행방향 끝 페이드 + 노이즈 + 수명
                float edgeAlpha = 1.0 - rimShape;
                float trailEnd  = smoothstep(0.0, 0.15, u); // 맨 뒤쪽 살짝 페이드
                float alpha = edgeAlpha * noiseMask * fade * trailEnd * _AlphaMultiplier;

                return half4(color * alpha, alpha);   // premultiplied (One,One 적합)
            }
            ENDHLSL
        }
    }

    FallBack Off
}
