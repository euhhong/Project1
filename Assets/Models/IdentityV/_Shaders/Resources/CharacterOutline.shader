// 캐릭터 윤곽선(FE): 몸을 화면에서 일정한 픽셀 두께(_OutlineWidth)만큼 바깥으로 부풀린 뒤 뒷면만 그린다(인버티드 헐).
// 앞면은 원래 모델이 덮으므로 가장자리에만 얇은 선이 남는다. 머리카락처럼 알파로 오려 낸 부분은 원래 텍스처 알파로 똑같이 오린다(_Cutoff > 0).
// CharacterOutline(캐릭터 루트)이 원래 렌더러 옆에 이 머티리얼로 그리는 렌더러를 하나씩 붙인다. Resources에 둬서 빌드에도 들어간다.
Shader "Project1/Characters/Outline"
{
    Properties
    {
        _OutlineColor ("Color", Color) = (1, 1, 1, 1)
        _OutlineWidth ("Width (px)", Float) = 1.35
        _BaseMap ("Base (alpha for cutout)", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff (0 = off)", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+10" }
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On
            ZTest LEqual

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
                // 화면 픽셀 단위로 일정하게: NDC 한 픽셀 = 2 / 화면 크기, 원근 나눗셈 전이라 w를 곱한다
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
