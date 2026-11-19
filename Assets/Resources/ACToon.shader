// ACToon.shader — 동물의 숲풍 툰 셰이더 (URP). 미리보기 렌더러(Tools/acgen/viewer.html)와 같은 계산.
// 정점 색 RGB = 기본색, A = 색바꿈 마스크, UV0.x = AO.  _Tint는 감마 공간 값(SetVector)으로 넘김.
Shader "XmasPlaza/ACToon"
{
    Properties
    {
        _Tint ("Tint (gamma)", Vector) = (1,1,1,1)
        _HasTint ("Has Tint", Float) = 0
        _Gloss ("Gloss", Float) = 0
        _Emit ("Emission", Float) = 0
        _AmbTop ("Ambient Top", Vector) = (1.0, 1.0, 1.04, 1)
        _AmbBottom ("Ambient Bottom", Vector) = (0.86, 0.82, 0.86, 1)
        _ShadowTint ("Shadow Tint", Vector) = (0.8, 0.8, 0.95, 1)
        _LightTint ("Light Tint", Vector) = (1.04, 1.0, 0.94, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _Tint; float _HasTint; float _Gloss; float _Emit;
            float4 _AmbTop; float4 _AmbBottom; float4 _ShadowTint; float4 _LightTint;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1;
                float4 color : TEXCOORD2; float ao : TEXCOORD3; float fog : TEXCOORD4;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color; o.ao = v.uv.x;
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 base = i.color.rgb;                       // 감마 공간 색
                if (_HasTint > 0.5) base = lerp(base, base * _Tint.rgb, i.color.a);

                float4 sc = TransformWorldToShadowCoord(i.positionWS);
                Light ml = GetMainLight(sc);
                float3 L = normalize(ml.direction);
                float3 V = normalize(GetWorldSpaceViewDir(i.positionWS));
                float ndl = dot(n, L);
                float lit = smoothstep(-0.05, 0.22, ndl) * lerp(0.15, 1.0, ml.shadowAttenuation);
                float3 mainCol = LinearToSRGB(ml.color);         // 빛 색도 감마 공간으로
                float3 light = lerp(_ShadowTint.rgb, _LightTint.rgb * mainCol, lit);
                float sky = n.y * 0.5 + 0.5;
                float3 amb = lerp(_AmbBottom.rgb, _AmbTop.rgb, sky);
                float ao = lerp(0.55, 1.0, i.ao);
                float3 c = base * light * amb * ao;

                // 추가 조명 (벽난로·램프의 따뜻한 빛)
                #if defined(_ADDITIONAL_LIGHTS)
                uint count = GetAdditionalLightsCount();
                #if USE_CLUSTER_LIGHT_LOOP || USE_FORWARD_PLUS
                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                LIGHT_LOOP_BEGIN(count)
                    Light al = GetAdditionalLight(lightIndex, i.positionWS, half4(1,1,1,1));
                    float t = smoothstep(-0.2, 0.4, dot(n, al.direction)) * al.distanceAttenuation * al.shadowAttenuation;
                    c += base * LinearToSRGB(al.color) * t * 0.6;
                LIGHT_LOOP_END
                #else
                for (uint li = 0; li < count; li++)
                {
                    Light al = GetAdditionalLight(li, i.positionWS, half4(1,1,1,1));
                    float t = smoothstep(-0.2, 0.4, dot(n, al.direction)) * al.distanceAttenuation * al.shadowAttenuation;
                    c += base * LinearToSRGB(al.color) * t * 0.6;
                }
                #endif
                #endif

                float rim = pow(1.0 - saturate(dot(n, V)), 3.0) * 0.22 * lit;
                float3 H = normalize(L + V);
                float spec = smoothstep(0.55, 0.62, pow(saturate(dot(n, H)), 40.0)) * _Gloss;
                c += rim + spec * 0.85;
                c = lerp(c, base * 1.25 + 0.08, saturate(_Emit));   // 스스로 빛나는 조각 (불, 전구, 창)
                c = pow(max(c, 0), 1.0 / 1.05);
                float3 linearC = SRGBToLinear(c);
                linearC = MixFog(linearC, i.fog);
                return half4(linearC, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection; float3 _LightPosition;
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            float4 vert(A v) : SV_POSITION
            {
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                float3 nw = TransformObjectToWorldNormal(v.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 ld = normalize(_LightPosition - ws);
                #else
                float3 ld = _LightDirection;
                #endif
                float4 c = TransformWorldToHClip(ApplyShadowBias(ws, nw, ld));
                #if UNITY_REVERSED_Z
                c.z = min(c.z, UNITY_NEAR_CLIP_VALUE);
                #else
                c.z = max(c.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return c;
            }
            half4 frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct A { float4 positionOS : POSITION; };
            float4 vert(A v) : SV_POSITION { return TransformObjectToHClip(v.positionOS.xyz); }
            half frag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
