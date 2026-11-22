Shader "Custom/Tier2ShinyEnemy"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5
        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        [HDR] _EmissionColor("Emission Color", Color) = (0.0, 0.0, 0.0, 1.0)

        [Header(Tier 2 Shiny Effect)]
        [Toggle] _ShineEnabled("Shine Enabled", Float) = 0.0
        [HDR] _ShineColor("Shine Glint Color", Color) = (2.4, 2.25, 1.75, 1.0)
        _ShineCycleDuration("Shine Cycle Duration", Range(0.5, 10.0)) = 2.8
        _ShineSweepWidth("Shine Sweep Width", Range(0.03, 0.5)) = 0.16
        _ShineWorldSpread("Shine World Spread", Range(0.0, 0.25)) = 0.045
        _ShineHeightSlope("Shine Height Slope", Range(0.0, 1.0)) = 0.22

        [Header(Tier 2 Rim Shimmer)]
        [HDR] _RimColor("Rim Shimmer Color", Color) = (0.95, 0.88, 0.65, 1.0)
        _RimPower("Rim Fresnel Power", Range(1.0, 8.0)) = 4.0
        _RimIntensity("Rim Shimmer Intensity", Range(0.0, 2.0)) = 0.14

        [Header(Spawn Materialization Effect)]
        [Toggle] _MaterializeEnabled("Materialize Enabled", Float) = 0.0
        _MaterializeProgress("Materialize Progress", Range(0.0, 1.0)) = 1.0
        _MaterializeMinY("Materialize Min Y", Float) = 0.0
        _MaterializeHeight("Materialize Height", Float) = 1.8
        _MaterializeEdgeWidth("Materialize Edge Width", Range(0.01, 0.5)) = 0.14
        [HDR] _MaterializeEdgeColor("Materialize Edge Color", Color) = (2.6, 0.32, 0.04, 1.0)
        [HDR] _MaterializeRimColor("Materialize Rim Color", Color) = (1.6, 0.05, 0.02, 1.0)
        _MaterializeRimPower("Materialize Rim Power", Range(1.0, 8.0)) = 3.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
        }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _EmissionColor;
            half4 _ShineColor;
            half4 _RimColor;
            half4 _MaterializeEdgeColor;
            half4 _MaterializeRimColor;
            half _Smoothness;
            half _Metallic;
            float _ShineEnabled;
            float _ShineCycleDuration;
            float _ShineSweepWidth;
            float _ShineWorldSpread;
            float _ShineHeightSlope;
            float _RimPower;
            float _RimIntensity;
            float _MaterializeEnabled;
            float _MaterializeProgress;
            float _MaterializeMinY;
            float _MaterializeHeight;
            float _MaterializeEdgeWidth;
            float _MaterializeRimPower;
        CBUFFER_END

        inline float ComputeMaterializeCutoffY()
        {
            return _MaterializeMinY + max(_MaterializeHeight, 0.01) * saturate(_MaterializeProgress);
        }

        inline void ClipIfMaterializing(float positionWSY)
        {
            if (_MaterializeEnabled > 0.5)
            {
                clip(ComputeMaterializeCutoffY() - positionWSY);
            }
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment

            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF
            #pragma shader_feature_local_fragment _EMISSION

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                #ifdef _ADDITIONAL_LIGHTS_VERTEX
                half4 fogFactorAndVertexLight : TEXCOORD2;
                #else
                half fogFactor : TEXCOORD2;
                #endif
                DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 3);
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                float4 shadowCoord : TEXCOORD4;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings LitPassVertex(Attributes input)
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

                half fogFactor = 0;
                #if !defined(_FOG_FRAGMENT)
                fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
                #endif

                #ifdef _ADDITIONAL_LIGHTS_VERTEX
                half3 vertexLight = VertexLighting(vertexInput.positionWS, normalInput.normalWS);
                output.fogFactorAndVertexLight = half4(fogFactor, vertexLight);
                #else
                output.fogFactor = fogFactor;
                #endif

                OUTPUT_SH(output.normalWS.xyz, output.vertexSH);

                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                output.shadowCoord = GetShadowCoord(vertexInput);
                #endif

                return output;
            }

            half4 LitPassFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                ClipIfMaterializing(input.positionWS.y);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = NormalizeNormalPerPixel(input.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                inputData.shadowCoord = input.shadowCoord;
                #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
                #else
                inputData.shadowCoord = float4(0, 0, 0, 0);
                #endif

                #ifdef _ADDITIONAL_LIGHTS_VERTEX
                inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactorAndVertexLight.x);
                inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
                #else
                inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
                #endif

                inputData.bakedGI = SAMPLE_GI(0, input.vertexSH, inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = _BaseColor.rgb;
                surfaceData.alpha = _BaseColor.a;
                surfaceData.metallic = _Metallic;
                surfaceData.smoothness = _Smoothness;
                surfaceData.normalTS = half3(0, 0, 1);
                surfaceData.occlusion = 1.0h;

                half3 shineEmission = half3(0.0h, 0.0h, 0.0h);
                if (_ShineEnabled > 0.5)
                {
                    float3 objectOriginWS = GetAbsolutePositionWS(UNITY_MATRIX_M._m03_m13_m23);
                    float relHeight = input.positionWS.y - objectOriginWS.y;
                    float cycleDuration = max(_ShineCycleDuration, 0.1);
                    float phase = frac(_Time.y / cycleDuration
                        - dot(input.positionWS.xz, float2(_ShineWorldSpread, _ShineWorldSpread * 0.7))
                        + relHeight * _ShineHeightSlope);
                    float halfWidth = max(_ShineSweepWidth * 0.5, 0.001);
                    float distFromSweep = abs(phase - halfWidth);
                    float sweepMask = 1.0 - smoothstep(0.0, halfWidth, distFromSweep);
                    sweepMask *= sweepMask;

                    half ndotv = saturate(dot(inputData.normalWS, inputData.viewDirectionWS));
                    half fresnel = pow(1.0h - ndotv, (half)_RimPower);

                    shineEmission = _ShineColor.rgb * (half)sweepMask
                        + _RimColor.rgb * (fresnel * (half)_RimIntensity);
                }

                half3 materializeEmission = half3(0.0h, 0.0h, 0.0h);
                if (_MaterializeEnabled > 0.5)
                {
                    float cutoffY = ComputeMaterializeCutoffY();
                    float distFromEdge = max(0.0, cutoffY - input.positionWS.y);
                    float edgeMask = 1.0 - smoothstep(0.0, max(_MaterializeEdgeWidth, 0.001), distFromEdge);
                    edgeMask *= edgeMask;

                    half matNdotv = saturate(dot(inputData.normalWS, inputData.viewDirectionWS));
                    half matFresnel = pow(1.0h - matNdotv, (half)_MaterializeRimPower);
                    float rimFade = 1.0 - smoothstep(0.85, 1.0, saturate(_MaterializeProgress));

                    materializeEmission = _MaterializeEdgeColor.rgb * (half)edgeMask
                        + _MaterializeRimColor.rgb * (matFresnel * (half)rimFade);
                }

                surfaceData.emission = _EmissionColor.rgb + shineEmission + materializeEmission;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return color;
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
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionWS = positionWS;

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                float3 lightDirectionWS = _LightDirection;
                #endif

                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                #if UNITY_REVERSED_Z
                output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                ClipIfMaterializing(input.positionWS.y);
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
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                return output;
            }

            half4 DepthOnlyFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                ClipIfMaterializing(input.positionWS.y);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthNormalsVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 DepthNormalsFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                ClipIfMaterializing(input.positionWS.y);
                return half4(NormalizeNormalPerPixel(input.normalWS), 0.0h);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
