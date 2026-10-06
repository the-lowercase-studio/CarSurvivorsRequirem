Shader "Custom/HolographicIndicator"
{
    Properties
    {
        [Header(Layer Toggles)]
        [Toggle] _EnableBorder("Enable Border Frame", Float) = 1.0
        [Toggle] _EnableEndCaps("Enable End Caps", Float) = 1.0
        [Toggle] _EnableGrid("Enable Grid Pattern", Float) = 0.0
        [Toggle] _EnableFill("Enable Fill Background", Float) = 0.0

        [Header(Outer Border Frame)]
        [HDR] _OuterBorderColor("Outer Border Color", Color) = (4.5, 0.55, 0.4, 1.0)
        _OuterBorderWidth("Outer Border Width (Meters)", Float) = 0.045
        _BorderGap("Border Gap (Meters)", Float) = 0.02
        [HDR] _InnerBorderColor("Inner Border Line Color", Color) = (3.2, 0.45, 0.35, 1.0)
        _InnerBorderWidth("Inner Border Line Width (Meters)", Float) = 0.015
        _OuterGlowIntensity("Outer Glow Intensity", Float) = 0.6
        _OuterGlowFalloff("Outer Glow Falloff", Float) = 18.0

        [Header(Holographic Grid)]
        [HDR] _GridColor("Grid Line Color", Color) = (2.6, 0.7, 0.28, 0.95)
        _GridColumns("Grid Columns Across Width", Float) = 8.0
        _GridLineWidth("Grid Line Width (Meters)", Float) = 0.007
        _GridPadding("Grid Padding From Border (Meters)", Float) = 0.012

        [Header(Background Fill)]
        _FillColor("Fill Color", Color) = (0.5, 0.07, 0.05, 0.3)
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
                half4 _GridColor;
                half4 _FillColor;
                float _EnableBorder;
                float _EnableEndCaps;
                float _EnableGrid;
                float _EnableFill;
                float _OuterBorderWidth;
                float _BorderGap;
                float _InnerBorderWidth;
                float _OuterGlowIntensity;
                float _OuterGlowFalloff;
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

            half4 frag(Varyings input) : SV_Target
            {
                float halfWidth = 0.5 * input.totalMeterDim.x;
                float halfLength = 0.5 * input.totalMeterDim.y;

                float distFromEdgeX = halfWidth - abs(input.localMeterPos.x);
                float distFromEdgeZ = halfLength - abs(input.localMeterPos.y);

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

                // Screen-space anti-aliasing clamped so it never exceeds line proportions or inverts smoothstep
                float pixelWidth = max(fwidth(distFromEdge), 0.0006);
                float outerAa = min(pixelWidth, effectiveOuterWidth * 0.35);
                float innerAa = min(pixelWidth, effectiveInnerWidth * 0.35);

                // Outer boundary mask
                float baseMask = smoothstep(0.0 - outerAa, 0.0 + outerAa, distFromEdge);

                half4 result = half4(0.0, 0.0, 0.0, 0.0);

                // 1. Fill Layer
                if (_EnableFill > 0.5)
                {
                    result.rgb = _FillColor.rgb;
                    result.a = _FillColor.a * baseMask;
                }

                float gridMargin = innerBorderEnd + _GridPadding;
                float gridMask = smoothstep(gridMargin - outerAa, gridMargin + outerAa, distFromEdge);

                // 2. Holographic Grid Layer
                if (_EnableGrid > 0.5 && gridMask > 0.001)
                {
                    float cols = max(_GridColumns, 1.0);
                    float cellSize = input.totalMeterDim.x / cols;
                    cellSize = max(cellSize, 0.005);

                    float2 gridPixels = max(fwidth(input.localMeterPos), 0.0006);
                    float2 gridLineWidth = max(float2(_GridLineWidth, _GridLineWidth), gridPixels * 1.2);

                    // Align grid lines symmetrically around center line
                    float2 gridFract = frac((input.localMeterPos + cellSize * 0.5) / cellSize);
                    float2 distToGrid = min(gridFract, 1.0 - gridFract) * cellSize;
                    float2 gridLines2D = saturate(1.0 - distToGrid / gridLineWidth);

                    float gridLine = max(gridLines2D.x, gridLines2D.y) * gridMask;

                    float gAlpha = gridLine * _GridColor.a;
                    result.rgb = lerp(result.rgb, _GridColor.rgb, gAlpha);
                    result.a = max(result.a, gAlpha);
                }

                // 3. Border Frame Layer
                if (_EnableBorder > 0.5)
                {
                    // Outer neon border line
                    float outerLine = smoothstep(0.0 - outerAa, 0.0 + outerAa, distFromEdge) *
                                      smoothstep(effectiveOuterWidth + outerAa, effectiveOuterWidth - outerAa, distFromEdge);

                    // Inner neon border line
                    float innerStart = effectiveOuterWidth + effectiveGap;
                    float innerEnd = innerStart + effectiveInnerWidth;
                    float innerLine = smoothstep(innerStart - innerAa, innerStart + innerAa, distFromEdge) *
                                      smoothstep(innerEnd + innerAa, innerEnd - innerAa, distFromEdge);

                    // Soft glow fading inward from the outer edge, strictly masked within the border frame
                    float glowDist = max(0.0, distFromEdge - effectiveOuterWidth);
                    float borderGlowMask = smoothstep(innerEnd + innerAa, innerEnd - innerAa, distFromEdge);
                    float glow = exp(-glowDist * _OuterGlowFalloff) * _OuterGlowIntensity * baseMask * borderGlowMask;

                    float oAlpha = outerLine * _OuterBorderColor.a;
                    result.rgb = lerp(result.rgb, _OuterBorderColor.rgb, oAlpha);
                    result.a = max(result.a, oAlpha);

                    float iAlpha = innerLine * _InnerBorderColor.a;
                    result.rgb = lerp(result.rgb, _InnerBorderColor.rgb, iAlpha);
                    result.a = max(result.a, iAlpha);

                    // Add soft glow
                    result.rgb += _OuterBorderColor.rgb * glow * (1.0 - outerLine);
                    result.a = max(result.a, glow * 0.6);
                }

                // Discard fully transparent pixels
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
