Shader "Custom/FireTailsTrail"
{
    Properties
    {
        [MainTexture] _BaseMap("Optional Scorch Tread Map", 2D) = "white" {}
        _NoiseMap("Flame Noise Map (R8/Greyscale)", 2D) = "white" {}
        [HDR] _CoreColor("Core Molten Color (HDR)", Color) = (2.5, 2.0, 0.8, 1.0)
        [HDR] _OuterColor("Outer Flame Color (HDR)", Color) = (2.0, 0.45, 0.05, 1.0)
        _FlowSpeed("Flame Flow Speed (X, Y)", Vector) = (0.0, -3.0, 0.0, 0.0)
        _NoiseTiling("Noise Tiling (X, Y)", Vector) = (2.0, 4.0, 0.0, 0.0)
        _EmissionBoost("Bloom Boost Intensity", Range(1.0, 6.0)) = 3.0
        _DissolveSoftness("Ember Dissolve Softness", Range(0.05, 0.5)) = 0.2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+10"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off
            Offset -1, -1

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                half4 color       : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half4 color       : COLOR;
                float fogCoord    : TEXCOORD1;
            };

            TEXTURE2D(_BaseMap);   SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NoiseMap);  SAMPLER(sampler_NoiseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _NoiseMap_ST;
                half4 _CoreColor;
                half4 _OuterColor;
                float4 _FlowSpeed;
                float4 _NoiseTiling;
                float _EmissionBoost;
                float _DissolveSoftness;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                output.fogCoord = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 1. Optimized parabolic core cross-section (cubic polynomial replaces expensive pow())
                // uDist: 0 in center, 1 at margins
                half uDist = abs(input.uv.x - 0.5h) * 2.0h;
                half uDist2 = uDist * uDist;
                half coreProfile = saturate(1.0h - uDist2 * uDist); // (1 - u^3), fast 2-cycle MAD curve

                // 2. Hardware texture-sampled flame noise (1 tap on TMU with free bilinear filtering)
                float2 noiseUv = input.uv * _NoiseTiling.xy + _FlowSpeed.xy * _Time.y;
                half noise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, noiseUv).r;

                // 3. Modulate with optional tread mask
                half4 treadTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half treadMask = treadTex.a * treadTex.r;

                // 4. Organic dissolve driven by vertex alpha (trail age)
                half lifeAlpha = input.color.a;
                half flameMask = saturate(coreProfile * (noise * 0.7h + 0.3h) * (treadMask * 0.4h + 0.6h));
                half dissolve = smoothstep(1.0h - lifeAlpha, 1.0h - lifeAlpha + _DissolveSoftness, flameMask);

                // 5. Fast color ramp: (coreProfile * coreProfile) replaces pow(coreProfile, 2.0)
                half coreSq = coreProfile * coreProfile;
                half3 fireColor = lerp(_OuterColor.rgb, _CoreColor.rgb, coreSq) * _EmissionBoost;

                // 6. Fog and final output
                half finalAlpha = dissolve * lifeAlpha;
                fireColor = MixFogColor(fireColor, half3(0, 0, 0), input.fogCoord);

                return half4(fireColor * finalAlpha, finalAlpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
