// 가려진 상대 윤곽선(FE): 벽·바닥 너머로 보이는 빨간 윤곽선. HiddenOpponentOutline(상대 캐릭터 루트)이 렌더러마다 두 벌 붙인다.
// 1) 마스크(_OutlineWidth 0, 색 안 씀): 몸 모양을 스텐실(128 비트)에 찍는다 — 깊이 무시(ZTest Always).
// 2) 선(_OutlineWidth 몇 픽셀): 화면 픽셀 두께만큼 부풀린 몸을 깊이 무시하고 그리되, 마스크가 찍힌 곳(몸 안)은 빼서 가장자리 선만 남는다.
// 마스크는 큐 4000, 선은 4001이라 모든 마스크가 먼저 그려진다. 머리카락 등 알파로 오린 부분은 같이 오린다(_Cutoff > 0).
Shader "Project1/Characters/HiddenOpponentOutline"
{
    Properties
    {
        _OutlineColor ("Color", Color) = (1, 0.12, 0.12, 1)
        _OutlineWidth ("Width (px)", Float) = 3
        _BaseMap ("Base (alpha for cutout)", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff (0 = off)", Range(0, 1)) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Comp", Float) = 6
        [Enum(UnityEngine.Rendering.StencilOp)] _StencilOp ("Stencil Op", Float) = 0
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Overlay+1" "IgnoreProjector" = "True" }
        Pass
        {
            Name "HiddenOutline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Off
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha
            ColorMask [_ColorMask]
            Stencil
            {
                Ref 128
                ReadMask 128
                WriteMask 128
                Comp [_StencilComp]
                Pass [_StencilOp]
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;
                float4 _BaseMap_ST;
                float _Cutoff;
            CBUFFER_END
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                float4 clip = TransformObjectToHClip(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float2 normalCS = mul((float3x3)UNITY_MATRIX_VP, normalWS).xy;
                float len = max(length(normalCS), 1e-5);
                clip.xy += (normalCS / len) * (_OutlineWidth * 2.0 / _ScreenParams.xy) * clip.w;
                o.positionCS = clip;
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                if (_Cutoff > 0)
                {
                    half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a;
                    clip(a - _Cutoff);
                }
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
