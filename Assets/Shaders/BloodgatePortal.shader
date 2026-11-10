Shader "Custom/BloodgatePortal"
{
    Properties
    {
        [Header(HDR Color Palette)]
        [HDR] _DarkColor("Dark Void Color", Color) = (0.18, 0.01, 0.02, 1.0)
        [HDR] _BloodColor("Blood Core Color", Color) = (1.8, 0.08, 0.12, 1.0)
        [HDR] _FireRimColor("Fire Rim Color", Color) = (3.5, 1.8, 0.35, 1.0)
        [HDR] _EmberColor("Ember Flare Color", Color) = (4.0, 2.5, 0.6, 1.0)

        [Header(Vortex Dynamics)]
        _SwirlSpeed("Swirl Speed", Range(0.1, 5.0)) = 1.2
        _Turbulence("Turbulence Frequency", Range(0.2, 3.0)) = 1.0
        _TwistStrength("Twist Strength", Range(1.0, 10.0)) = 3.5

        [Header(Event Horizon and Outer Rim)]
        _VoidSize("Event Horizon Radius", Range(0.05, 0.8)) = 0.32
        _GlowIntensity("Global HDR Glow", Range(0.5, 4.0)) = 1.5
        _OverallAlpha("Overall Opacity", Range(0.0, 1.0)) = 0.95
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 positionWS   : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _DarkColor;
                half4 _BloodColor;
                half4 _FireRimColor;
                half4 _EmberColor;
                float _SwirlSpeed;
                float _Turbulence;
                float _TwistStrength;
                float _VoidSize;
                float _GlowIntensity;
                float _OverallAlpha;
            CBUFFER_END

            // --- Fast Procedural Noise & FBM ---
            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash(i + float2(0.0, 0.0)), hash(i + float2(1.0, 0.0)), u.x),
                           lerp(hash(i + float2(0.0, 1.0)), hash(i + float2(1.0, 1.0)), u.x), u.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                float2x2 rot = float2x2(cos(0.5), sin(0.5), -sin(0.5), cos(0.5));
                for (int i = 0; i < 4; ++i)
                {
                    v += a * noise(p);
                    p = mul(rot, p) * 2.0;
                    a *= 0.5;
                }
                return v;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Centered coordinates [-1, 1]
                float2 uv = (input.uv - 0.5) * 2.0;
                float r = length(uv);

                // Discard fragments outside the unit radius
                if (r > 1.0)
                {
                    discard;
                }

                float t = _Time.y;

                // --- Continuous 2D Rotation Tensor Swirl (Zero Seams) ---
                float twistAngle = -log(r + 0.04) * _TwistStrength - t * _SwirlSpeed * 2.5;
                float cosA = cos(twistAngle);
                float sinA = sin(twistAngle);
                float2 uvSwirl = float2(
                    uv.x * cosA - uv.y * sinA,
                    uv.x * sinA + uv.y * cosA
                );

                // Counter-swirling secondary turbulence
                float counterAngle = log(r + 0.1) * 1.5 + t * _SwirlSpeed * 1.2;
                float cosB = cos(counterAngle);
                float sinB = sin(counterAngle);
                float2 uvCounter = float2(
                    uv.x * cosB - uv.y * sinB,
                    uv.x * sinB + uv.y * cosB
                );

                // Seamless FBM noise sampling
                float n1 = fbm(uvSwirl * 3.2 * _Turbulence);
                float n2 = fbm(uvCounter * 5.0 * _Turbulence + float2(t * 0.4, t * 0.4));
                float flow = smoothstep(0.25, 0.75, n1 * 0.65 + n2 * 0.45);

                // Black hole event horizon (pitch black core)
                float horizon = smoothstep(_VoidSize, _VoidSize + 0.07, r);

                // Blood & accretion disk color interpolation
                half3 col = lerp(_DarkColor.rgb, _BloodColor.rgb, flow);
                half3 fireRim = _FireRimColor.rgb * _GlowIntensity;
                col = lerp(col, fireRim, pow(flow, 2.2) * (1.0 - smoothstep(_VoidSize, 0.95, r)));

                // Outer boundary glow
                float edge = smoothstep(0.99, 0.68, r);
                float rimGlow = pow(saturate(1.0 - abs(r - 0.72) * 2.2), 3.0) * 0.85;
                col += fireRim * rimGlow;

                // Pulsing event horizon perimeter ring
                float pulse = sin(t * 4.5) * 0.015;
                float horizonEdge = smoothstep(0.04, 0.0, abs(r - (_VoidSize + pulse)));
                col += _EmberColor.rgb * (_GlowIntensity * horizonEdge * 1.2);

                // Black hole void inside center
                col *= horizon;

                // Edge fade for smooth circular boundary
                float alpha = edge * (r > _VoidSize * 0.75 ? 1.0 : 0.85) * _OverallAlpha;

                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
