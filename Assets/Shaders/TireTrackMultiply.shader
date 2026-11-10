Shader "Custom/TireTrackMultiply"
{
    Properties
    {
        [MainTexture] _BaseMap("Tread Texture", 2D) = "white" {}
        [MainColor] _BaseColor("Tread Tint", Color) = (1.0, 1.0, 1.0, 1.0)
        _DarknessIntensity("Darkness Multiplier", Range(0.1, 3.0)) = 1.0
        _MinDarkness("Maximum Darkening Clamping", Range(0.0, 1.0)) = 0.05
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+100"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend DstColor Zero
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

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float _DarknessIntensity;
                float _MinDarkness;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = (_BaseMap_ST.x != 0.0f && _BaseMap_ST.y != 0.0f) ? TRANSFORM_TEX(input.uv, _BaseMap) : input.uv;
                output.color = input.color;
                output.fogCoord = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);

                // Use alpha channel as tread mask
                half mask = texColor.a;
                half alphaFactor = saturate(mask * input.color.a * _DarknessIntensity);

                // Multiply blend math:
                // Final = DstColor * OutputRGB
                // When alphaFactor is 0.0 (background/faded), OutputRGB must be pure 1.0 (untouched ground).
                // When alphaFactor is 1.0 (fresh tread mark), OutputRGB darkens the ground to targetDarkness.
                half3 targetDarkness = max(_BaseColor.rgb * input.color.rgb, half3(_MinDarkness, _MinDarkness, _MinDarkness));
                half3 outputRgb = lerp(half3(1.0h, 1.0h, 1.0h), targetDarkness, alphaFactor);

                // Fog handling for Multiply: distant marks blend toward 1.0 so they don't darken fogged backgrounds
                outputRgb = MixFogColor(outputRgb, half3(1.0h, 1.0h, 1.0h), input.fogCoord);

                return half4(outputRgb, 1.0h);
            }
            ENDHLSL
        }
    }
}
