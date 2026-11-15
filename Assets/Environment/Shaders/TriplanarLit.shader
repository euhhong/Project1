// 맵 벽·바닥·지붕용: 월드 좌표로 텍스처를 붙인다(트라이플래너). 늘린 상자(블록아웃)에 UV 없이도 크기에 맞게 반복된다.
// _Tiling = 텍스처 한 장이 덮는 미터 수.
Shader "Project1/Environment/TriplanarLit"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _Tiling ("Meters Per Tile", Float) = 3
        _Smoothness ("Smoothness", Range(0, 1)) = 0.1
        _Blend ("Blend Sharpness", Range(1, 16)) = 6
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Range(0, 4)) = 1
        _CavityStrength ("Cavity (dark grooves)", Range(0, 1)) = 0
        _DetailBump ("Fine Grit Strength", Range(0, 4)) = 0
        _DetailTiling ("Fine Grit Repeat", Float) = 4.3
        _HeightMap ("Height Map (grayscale)", 2D) = "gray" {}
        _Parallax ("Parallax", Range(0, 0.08)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            TEXTURE2D(_HeightMap); SAMPLER(sampler_HeightMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float _Tiling;
                half _Smoothness;
                half _Blend;
                half _BumpScale;
                half _CavityStrength;
                half _DetailBump;
                float _DetailTiling;
                half _Parallax;
            CBUFFER_END

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
                half fogFactor : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.fogFactor = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 n = normalize(i.normalWS);
                float3 w = pow(abs(n), _Blend);
                w /= max(w.x + w.y + w.z, 1e-4);
                float3 uvw = i.positionWS / max(_Tiling, 0.01);
                // 면 방향 부호로 u를 뒤집어 반대쪽 면도 텍스처·노멀이 거울상이 되지 않게(Ben Golus 화이트아웃 방식)
                float3 sgn = float3(n.x < 0 ? -1 : 1, n.y < 0 ? -1 : 1, n.z < 0 ? -1 : 1);
                float2 uvX = float2(uvw.z * sgn.x, uvw.y);
                float2 uvY = float2(uvw.x * sgn.y, uvw.z);
                float2 uvZ = float2(-uvw.x * sgn.z, uvw.y);
                // 시차(패럴랙스): 높이 맵만큼 보는 방향으로 UV를 밀어 튀어나온 돌이 비스듬히 볼 때 앞으로 나와 보이게
                float3 V = GetWorldSpaceNormalizeViewDir(i.positionWS);
                if (_Parallax > 0)
                {
                    half hx = SAMPLE_TEXTURE2D(_HeightMap, sampler_HeightMap, uvX).r - 0.5;
                    half hy = SAMPLE_TEXTURE2D(_HeightMap, sampler_HeightMap, uvY).r - 0.5;
                    half hz = SAMPLE_TEXTURE2D(_HeightMap, sampler_HeightMap, uvZ).r - 0.5;
                    uvX += float2(V.z * sgn.x, V.y) / max(abs(V.x), 0.35) * hx * _Parallax;
                    uvY += float2(V.x * sgn.y, V.z) / max(abs(V.y), 0.35) * hy * _Parallax;
                    uvZ += float2(-V.x * sgn.z, V.y) / max(abs(V.z), 0.35) * hz * _Parallax;
                }
                half3 cx = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvX).rgb;
                half3 cy = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvY).rgb;
                half3 cz = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvZ).rgb;
                half3 albedo = (cx * w.x + cy * w.y + cz * w.z) * _BaseColor.rgb;

                half3 tx = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvX), _BumpScale);
                half3 ty = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvY), _BumpScale);
                half3 tz = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvZ), _BumpScale);
                // 잔결: 같은 노멀을 더 촘촘히(돌 표면의 거친 알갱이) 겹친다
                float dt = _DetailTiling;
                tx.xy += UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvX * dt + 0.37), _DetailBump).xy;
                ty.xy += UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvY * dt + 0.37), _DetailBump).xy;
                tz.xy += UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvZ * dt + 0.37), _DetailBump).xy;
                tx.x *= sgn.x; ty.x *= sgn.y; tz.x *= -sgn.z;
                tx = half3(tx.xy + n.zy, abs(tx.z) * n.x);
                ty = half3(ty.xy + n.xz, abs(ty.z) * n.y);
                tz = half3(tz.xy + n.xy, abs(tz.z) * n.z);
                float3 bentN = normalize(tx.zyx * w.x + ty.xzy * w.y + tz.xyz * w.z);
                // 홈(노멀이 많이 기운 곳)을 어둡게 — 줄눈·틈이 그림자 진 것처럼
                half cavity = saturate(dot(bentN, n));
                cavity = cavity * cavity * cavity;
                albedo *= lerp(1, cavity, _CavityStrength);
                n = bentN;

                InputData d = (InputData)0;
                d.positionWS = i.positionWS;
                d.positionCS = i.positionCS;
                d.normalWS = n;
                d.viewDirectionWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                d.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                d.fogCoord = i.fogFactor;
                d.bakedGI = SampleSH(n);
                d.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                d.shadowMask = half4(1, 1, 1, 1);

                SurfaceData s = (SurfaceData)0;
                s.albedo = albedo;
                s.alpha = 1;
                s.metallic = 0;
                s.smoothness = _Smoothness;
                s.occlusion = 1;
                s.normalTS = half3(0, 0, 1);

                half4 c = UniversalFragmentPBR(d, s);
                c.rgb = MixFog(c.rgb, i.fogFactor);
                return c;
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
