Shader "UI/SpriteSilhouetteOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _OutlineColor ("Outline Color", Color) = (0,0,0,0)
        _OutlineWidth ("Outline Width", Float) = 0
        _OutlineUVRect ("Outline UV Rect", Vector) = (0,0,1,1)
        _OutlineUVStep ("Outline UV Step", Vector) = (0.001,0.001,0,0)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;

            fixed4 _OutlineColor;
            float _OutlineWidth;
            float4 _OutlineUVRect;
            float4 _OutlineUVStep;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            half4 SampleClampedSprite(float2 uv)
            {
                if (uv.x < _OutlineUVRect.x || uv.x > _OutlineUVRect.z ||
                    uv.y < _OutlineUVRect.y || uv.y > _OutlineUVRect.w)
                {
                    return half4(0.0h, 0.0h, 0.0h, 0.0h);
                }

                return tex2D(_MainTex, uv) + _TextureSampleAdd;
            }

            half SampleClampedAlpha(float2 uv)
            {
                float inBounds = step(_OutlineUVRect.x, uv.x) * step(uv.x, _OutlineUVRect.z)
                    * step(_OutlineUVRect.y, uv.y) * step(uv.y, _OutlineUVRect.w);
                return saturate((tex2Dlod(_MainTex, float4(uv, 0.0, 0.0)).a + _TextureSampleAdd.a) * inBounds);
            }

            half SampleRingMaxAlpha(float2 centerUv, float2 stepUV, float radiusScale, int sampleCount)
            {
                half maxAlpha = 0.0h;
                float angleStep = 6.28318530718 / sampleCount;
                float2 radiusUv = stepUV * radiusScale;

                for (int i = 0; i < sampleCount; i++)
                {
                    float s;
                    float c;
                    sincos(i * angleStep, s, c);
                    maxAlpha = max(maxAlpha, SampleClampedAlpha(centerUv + float2(c, s) * radiusUv));
                }

                return maxAlpha;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 baseSample = SampleClampedSprite(IN.texcoord);
                half4 baseColor = baseSample * IN.color;
                half baseAlpha = saturate(baseColor.a);
                half4 color = baseColor;

                if (_OutlineWidth > 0.001 && _OutlineColor.a > 0.001)
                {
                    float2 stepUV = _OutlineUVStep.xy * _OutlineWidth;
                    half maxAlpha = SampleRingMaxAlpha(IN.texcoord, stepUV, 1.0, 64);
                    maxAlpha = max(maxAlpha, SampleRingMaxAlpha(IN.texcoord, stepUV, 0.62, 24));
                    maxAlpha = max(maxAlpha, SampleRingMaxAlpha(IN.texcoord, stepUV, 0.28, 12));

                    half outlineAlpha = saturate(maxAlpha) * _OutlineColor.a * IN.color.a;
                    half outAlpha = baseAlpha + outlineAlpha * (1.0h - baseAlpha);
                    half3 premulRgb = baseColor.rgb * baseAlpha + _OutlineColor.rgb * outlineAlpha * (1.0h - baseAlpha);
                    half3 outRgb = outAlpha > 0.0001h ? (premulRgb / outAlpha) : half3(0.0h, 0.0h, 0.0h);
                    color = half4(outRgb, outAlpha);
                }

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
