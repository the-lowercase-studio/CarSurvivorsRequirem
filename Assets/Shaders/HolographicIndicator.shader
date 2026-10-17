Shader "Custom/HolographicIndicator"
{
    Properties
    {
        [Header(Layer Toggles)]
        [Toggle] _EnableBorder("Enable Border Frame", Float) = 1.0
        [Toggle] _EnableEndCaps("Enable End Caps", Float) = 1.0
        [Toggle] _EnableEndHazard("Enable Hazard End Cap", Float) = 1.0
        [Toggle] _EnableFill("Enable Fill Background", Float) = 1.0
        [Toggle] _EnableGrunge("Enable Grunge Specks", Float) = 1.0
        [Toggle] _EnableGrid("Enable Grid Pattern", Float) = 0.0

        [Header(Hazard End Cap)]
        [HDR] _HazardStripeColor("Hazard Stripe Color (Orange)", Color) = (4.5, 0.85, 0.15, 1.0)
        _HazardStripeDarkColor("Hazard Stripe Dark Color", Color) = (0.05, 0.04, 0.03, 0.95)
        _HazardBandLength("Hazard Band Length (Meters)", Float) = 1.3
        _HazardStripeWidth("Hazard Stripe Width (Meters)", Float) = 0.22
        _HazardStripeSlope("Hazard Stripe Slope", Float) = 1.0

        [Header(Outer Border Frame)]
        [HDR] _OuterBorderColor("Outer Border Color", Color) = (4.5, 0.85, 0.15, 1.0)
        _OuterBorderWidth("Outer Border Width (Meters)", Float) = 0.045
        _BorderGap("Border Gap (Meters)", Float) = 0.02
        [HDR] _InnerBorderColor("Inner Border Line Color", Color) = (3.2, 0.6, 0.15, 1.0)
        _InnerBorderWidth("Inner Border Line Width (Meters)", Float) = 0.015
        _OuterGlowIntensity("Outer Glow Intensity", Float) = 0.8
        _OuterGlowFalloff("Outer Glow Falloff", Float) = 16.0
        _BorderRoughness("Border Distress Roughness", Float) = 0.35

        [Header(Background Fill and Grunge)]
        _FillColor("Fill Color", Color) = (0.06, 0.06, 0.08, 0.55)
        _GrungeIntensity("Grunge Specks Intensity", Float) = 0.6
        _GrungeScale("Grunge Specks Scale", Float) = 12.0

        [Header(Holographic Grid)]
        [HDR] _GridColor("Grid Line Color", Color) = (2.6, 0.7, 0.28, 0.95)
        _GridColumns("Grid Columns Across Width", Float) = 8.0
        _GridLineWidth("Grid Line Width (Meters)", Float) = 0.007
        _GridPadding("Grid Padding From Border (Meters)", Float) = 0.012
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

            CBUFFER_START(UnityPerMaterial)
                half4 _OuterBorderColor;
                half4 _InnerBorderColor;
                half4 _HazardStripeColor;
                half4 _HazardStripeDarkColor;
                half4 _FillColor;
                half4 _GridColor;
                float _EnableBorder;
                float _EnableEndCaps;
                float _EnableEndHazard;
                float _EnableFill;
                float _EnableGrunge;
                float _EnableGrid;
                float _HazardBandLength;
                float _HazardStripeWidth;
                float _HazardStripeSlope;
                float _OuterBorderWidth;
                float _BorderGap;
                float _InnerBorderWidth;
                float _OuterGlowIntensity;
                float _OuterGlowFalloff;
                float _BorderRoughness;
                float _GrungeIntensity;
                float _GrungeScale;
                float _GridColumns;
                float _GridLineWidth;
                float _GridPadding;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 localMeterPos : TEXCOORD1;
                float2 totalMeterDim : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.uv = input.uv;

                // Unity's standard Plane mesh spans [-5, 5] along X and Z (10 meters at unit scale).
                float4x4 m = GetObjectToWorldMatrix();
                float3 axisX = float3(m._m00, m._m10, m._m20);
                float3 axisZ = float3(m._m02, m._m12, m._m22);
                float scaleX = length(axisX);
                float scaleZ = length(axisZ);

                float worldWidth = max(10.0 * scaleX, 0.001);
                float worldLength = max(10.0 * scaleZ, 0.001);
                output.totalMeterDim = float2(worldWidth, worldLength);

                // Local coordinates in meters relative to rectangle center
                output.localMeterPos = float2(input.positionOS.x * scaleX, input.positionOS.z * scaleZ);

                return output;
            }

            // Pseudo-random noise helpers for distress and grunge specks
            float hash21(float2 p)
            {
                p = frac(p * float2(234.34, 435.345));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }

            float noise2D(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1.0, 0.0));
                float c = hash21(i + float2(0.0, 1.0));
                float d = hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float halfWidth = 0.5 * input.totalMeterDim.x;
                float halfLength = 0.5 * input.totalMeterDim.y;

                float distFromEdgeX = halfWidth - abs(input.localMeterPos.x);
                float distFromFarEnd = halfLength - input.localMeterPos.y;
                float distFromStart = input.localMeterPos.y - (-halfLength);
                float distFromEdgeZ = min(distFromFarEnd, distFromStart);

                float distFromEdge = distFromEdgeX;
                if (_EnableEndCaps > 0.5)
                {
                    distFromEdge = min(distFromEdgeX, distFromEdgeZ);
                }

                // Guard border proportions so narrow width or length never collapses interior
                float maxBorderThickness = min(halfWidth, halfLength) * 0.44;
                float effectiveOuterWidth = min(_OuterBorderWidth, maxBorderThickness * 0.5);
                float effectiveGap = min(_BorderGap, maxBorderThickness * 0.28);
                float effectiveInnerWidth = min(_InnerBorderWidth, maxBorderThickness * 0.22);
                float innerBorderEnd = effectiveOuterWidth + effectiveGap + effectiveInnerWidth;

                // Screen-space anti-aliasing clamped so it never exceeds line proportions
                float pixelWidth = max(fwidth(distFromEdge), 0.0006);
                float outerAa = min(pixelWidth, effectiveOuterWidth * 0.35);
                float innerAa = min(pixelWidth, effectiveInnerWidth * 0.35);

                // Outer boundary mask
                float baseMask = smoothstep(0.0 - outerAa, 0.0 + outerAa, distFromEdge);
                if (baseMask <= 0.0001)
                {
                    discard;
                }

                half4 result = half4(0.0, 0.0, 0.0, 0.0);

                // 1. Hazard End Cap Layer (at the maximum range / terrain collision endpoint)
                float effectiveHazardLength = min(_HazardBandLength, input.totalMeterDim.y * 0.45);
                effectiveHazardLength = max(effectiveHazardLength, 0.15);

                float inHazardBand = 0.0;
                float separatorLine = 0.0;
                float endCapLine = 0.0;

                if (_EnableEndHazard > 0.5)
                {
                    float hazardStart = 0.0;
                    float hazardEnd = effectiveHazardLength;

                    float hazardBandMask = smoothstep(hazardEnd + 0.008, hazardEnd - 0.008, distFromFarEnd) *
                                           smoothstep(hazardStart - 0.008, hazardStart + 0.008, distFromFarEnd);

                    if (hazardBandMask > 0.001)
                    {
                        inHazardBand = hazardBandMask;

                        // 45-degree diagonal alternating orange and dark stripes
                        float stripeCoord = (input.localMeterPos.x + input.localMeterPos.y * _HazardStripeSlope) / max(_HazardStripeWidth, 0.02);
                        float stripeVal = sin(stripeCoord * 6.2831853);
                        float stripeAa = max(fwidth(stripeVal) * 1.5, 0.04);
                        float stripeOrange = smoothstep(-stripeAa, stripeAa, stripeVal);
                        half4 hazardColor = lerp(_HazardStripeDarkColor, _HazardStripeColor, stripeOrange);

                        result = hazardColor;
                        result.a *= baseMask * hazardBandMask;
                    }

                    // Separator line between hazard band and main lane body
                    float distToSeparator = abs(distFromFarEnd - effectiveHazardLength);
                    float sepAa = max(fwidth(distToSeparator), 0.0006);
                    float sepWidth = min(effectiveInnerWidth * 1.2, 0.035);
                    separatorLine = smoothstep(sepWidth + sepAa, sepWidth - sepAa, distToSeparator) * baseMask;

                    // End cap line at the very end of the lane
                    float endCapAa = max(fwidth(distFromFarEnd), 0.0006);
                    endCapLine = smoothstep(sepWidth + endCapAa, sepWidth - endCapAa, distFromFarEnd) * baseMask;
                }

                // 2. Background Fill Layer (for the corridor before the hazard band)
                if (_EnableFill > 0.5 && inHazardBand < 0.999)
                {
                    float fillAlpha = _FillColor.a * baseMask * (1.0 - inHazardBand);
                    result.rgb = lerp(_FillColor.rgb, result.rgb, inHazardBand);
                    result.a = max(result.a, fillAlpha);
                }

                // 3. Grunge / Dirt Specks Layer
                if (_EnableGrunge > 0.5 && result.a > 0.01)
                {
                    float2 grungeUV = input.localMeterPos * _GrungeScale;
                    float specks = noise2D(grungeUV * 2.2);
                    float speckDots = hash21(floor(grungeUV * 4.5));
                    if (speckDots > 0.82 && specks > 0.48)
                    {
                        float speckFactor = saturate((specks - 0.48) / 0.52);
                        result.rgb = lerp(result.rgb, result.rgb * 0.12, speckFactor * _GrungeIntensity);
                    }
                }

                // 4. Holographic Grid Layer (optional)
                float gridMargin = innerBorderEnd + _GridPadding;
                float gridMask = smoothstep(gridMargin - outerAa, gridMargin + outerAa, distFromEdge);
                if (_EnableGrid > 0.5 && gridMask > 0.001)
                {
                    float cols = max(_GridColumns, 1.0);
                    float cellSize = input.totalMeterDim.x / cols;
                    cellSize = max(cellSize, 0.005);

                    float2 gridPixels = max(fwidth(input.localMeterPos), 0.0006);
                    float2 gridLineWidth = max(float2(_GridLineWidth, _GridLineWidth), gridPixels * 1.2);

                    float2 gridFract = frac((input.localMeterPos + cellSize * 0.5) / cellSize);
                    float2 distToGrid = min(gridFract, 1.0 - gridFract) * cellSize;
                    float2 gridLines2D = saturate(1.0 - distToGrid / gridLineWidth);

                    float gridLine = max(gridLines2D.x, gridLines2D.y) * gridMask;
                    float gAlpha = gridLine * _GridColor.a;
                    result.rgb = lerp(result.rgb, _GridColor.rgb, gAlpha);
                    result.a = max(result.a, gAlpha);
                }

                // 5. Border Frame & Neon Glow Layer
                if (_EnableBorder > 0.5)
                {
                    // Subtle procedural roughness along border edge to mimic weathered stencil / asphalt marking
                    float edgeNoise = (_BorderRoughness > 0.0)
                        ? (noise2D(input.localMeterPos * 16.0) - 0.5) * _BorderRoughness * 0.02
                        : 0.0;
                    float distressedDist = distFromEdge + edgeNoise;

                    // Outer neon border line
                    float outerLine = smoothstep(0.0 - outerAa, 0.0 + outerAa, distressedDist) *
                                      smoothstep(effectiveOuterWidth + outerAa, effectiveOuterWidth - outerAa, distressedDist);

                    // Inner neon border line
                    float innerStart = effectiveOuterWidth + effectiveGap;
                    float innerEnd = innerStart + effectiveInnerWidth;
                    float innerLine = smoothstep(innerStart - innerAa, innerStart + innerAa, distressedDist) *
                                      smoothstep(innerEnd + innerAa, innerEnd - innerAa, distressedDist);

                    // Soft glow fading inward from the outer edge
                    float glowDist = max(0.0, distressedDist - effectiveOuterWidth);
                    float borderGlowMask = smoothstep(innerEnd + innerAa, innerEnd - innerAa, distressedDist);
                    float glow = exp(-glowDist * _OuterGlowFalloff) * _OuterGlowIntensity * baseMask * borderGlowMask;

                    float oAlpha = outerLine * _OuterBorderColor.a;
                    result.rgb = lerp(result.rgb, _OuterBorderColor.rgb, oAlpha);
                    result.a = max(result.a, oAlpha);

                    float iAlpha = innerLine * _InnerBorderColor.a;
                    result.rgb = lerp(result.rgb, _InnerBorderColor.rgb, iAlpha);
                    result.a = max(result.a, iAlpha);

                    // Composite separator and end cap border lines
                    float sepAlpha = max(separatorLine, endCapLine) * _OuterBorderColor.a;
                    result.rgb = lerp(result.rgb, _OuterBorderColor.rgb, sepAlpha);
                    result.a = max(result.a, sepAlpha);

                    // Soft glow contribution
                    result.rgb += _OuterBorderColor.rgb * glow * (1.0 - outerLine);
                    result.a = max(result.a, glow * 0.6);
                }

                // Discard fully transparent fragments
                if (result.a <= 0.001)
                {
                    discard;
                }

                return result;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
