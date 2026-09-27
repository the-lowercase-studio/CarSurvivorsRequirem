Shader "Custom/StylizedCarGlass"
{
    Properties
    {
        [Header(Glass Body and Tint)]
        _BaseColor("Glass Tint Color", Color) = (0.12, 0.32, 0.50, 1.0)
        _InteriorColor("Interior Depth Color", Color) = (0.02, 0.03, 0.05, 1.0)
        _DepthFalloff("Interior Depth Falloff", Range(0.5, 5.0)) = 1.5

        [Header(Fresnel Edge Glaze)]
        _GlazeColor("Glaze / Rim Color", Color) = (0.60, 0.85, 1.0, 1.0)
        _FresnelPower("Fresnel Power", Range(0.5, 8.0)) = 2.5
        _FresnelIntensity("Fresnel Intensity", Range(0.0, 3.0)) = 1.2

        [Header(Simulated Sky and Horizon Reflection)]
        _SkyReflectionColor("Sky Reflection Color", Color) = (0.70, 0.88, 1.0, 1.0)
        _GroundReflectionColor("Ground Reflection Color", Color) = (0.04, 0.05, 0.06, 1.0)
        _HorizonIntensity("Horizon Reflection Intensity", Range(0.0, 2.0)) = 0.75
        _HorizonSharpness("Horizon Band Sharpness", Range(1.0, 30.0)) = 8.0

        [Header(Environment Probes and Specular)]
        _ProbeReflectionIntensity("Reflection Probe Intensity", Range(0.0, 2.0)) = 1.0
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.96
        _SpecularIntensity("Sun Specular Intensity", Range(0.0, 5.0)) = 1.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _InteriorColor;
                float _DepthFalloff;

                half4 _GlazeColor;
                float _FresnelPower;
                float _FresnelIntensity;

                half4 _SkyReflectionColor;
                half4 _GroundReflectionColor;
                float _HorizonIntensity;
                float _HorizonSharpness;

                float _ProbeReflectionIntensity;
                float _Smoothness;
                float _SpecularIntensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                half3  normalWS     : TEXCOORD1;
                half   fogFactor    : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 positionWS = input.positionWS;
                half3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                half3 viewDirWS = SafeNormalize(GetCameraPositionWS() - positionWS);
                half3 reflectVector = reflect(-viewDirWS, normalWS);

                half NdotV = saturate(dot(normalWS, viewDirWS));

                // 1. Fake Interior Depth vs Glass Tint
                half depthFactor = pow(1.0h - NdotV * 0.7h, _DepthFalloff);
                half3 glassBody = lerp(_InteriorColor.rgb, _BaseColor.rgb, depthFactor);

                // 2. Fresnel edge glaze
                half fresnel = pow(1.0h - NdotV, _FresnelPower) * _FresnelIntensity;
                half3 surfaceColor = lerp(glassBody, _GlazeColor.rgb, saturate(fresnel * 0.75h));

                // 3. Simulated Sky & Horizon Reflection (Works everywhere, even without reflection probes)
                half reflectUp = reflectVector.y;
                half skyWeight = saturate(reflectUp * 0.5h + 0.5h);
                half3 skyGradient = lerp(_GroundReflectionColor.rgb, _SkyReflectionColor.rgb, skyWeight);

                // Dynamic horizon streak line across the glass
                half horizonBand = pow(saturate(1.0h - abs(reflectUp)), _HorizonSharpness);
                half3 simulatedReflection = (skyGradient + horizonBand * _SkyReflectionColor.rgb * 0.8h) * _HorizonIntensity;

                // 4. Real Environment Reflection (Reflection Probes / Skybox cubemap)
                #if !defined(_ENVIRONMENTREFLECTIONS_OFF)
                half perceptualRoughness = 1.0h - _Smoothness;
                half3 probeReflection = GlossyEnvironmentReflection(reflectVector, positionWS, perceptualRoughness, 1.0h) * _ProbeReflectionIntensity;
                #else
                half3 probeReflection = half3(0, 0, 0);
                #endif

                // Combine reflections
                half reflBlend = saturate(fresnel * 0.7h + 0.3h);
                half3 totalReflection = (simulatedReflection + probeReflection) * reflBlend;

                // 5. Main Light Specular Highlight & Direct Lighting
                float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half3 halfVec = SafeNormalize(mainLight.direction + viewDirWS);
                half NdotH = saturate(dot(normalWS, halfVec));
                half specPower = exp2(10.0h * _Smoothness + 1.0h);
                half specularTerm = pow(NdotH, specPower) * _SpecularIntensity;
                half3 specularHighlight = specularTerm * mainLight.color * mainLight.shadowAttenuation;

                // Ambient light and subtle diffuse
                half3 ambient = SampleSH(normalWS) * 0.25h;
                half NdotL = saturate(dot(normalWS, mainLight.direction));
                half3 directDiffuse = mainLight.color * NdotL * 0.2h * mainLight.shadowAttenuation;

                half3 finalColor = surfaceColor * (ambient + directDiffuse + 0.85h) + totalReflection + specularHighlight;

                finalColor = MixFog(finalColor, input.fogFactor);

                return half4(finalColor, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
