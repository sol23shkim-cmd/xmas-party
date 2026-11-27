// ACGlass.shader — 비치는 유리 (스노우볼 등, 11-27). 가운데는 거의 투명, 가장자리(프레넬)와 하이라이트만 밝게.
// 색 값은 감마 공간으로 받아 마지막에 리니어로 바꿈 (ACToon과 같은 규칙).
Shader "XmasParty/ACGlass"
{
    Properties
    {
        _Color ("Tint (gamma) + base alpha", Color) = (0.86, 0.94, 1.0, 0.08)
        _Rim ("Rim strength", Float) = 0.6
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Name "Glass"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Color; float _Rim;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct V { float4 pos : SV_POSITION; float3 n : TEXCOORD0; float3 wp : TEXCOORD1; };
            V vert(A v)
            {
                V o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.pos = p.positionCS; o.wp = p.positionWS;
                o.n = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }
            half4 frag(V i) : SV_Target
            {
                float3 n = normalize(i.n);
                float3 vd = normalize(GetWorldSpaceViewDir(i.wp));
                float f = pow(1.0 - saturate(dot(n, vd)), 3.0);
                float3 L = normalize(float3(-0.45, 0.8, 0.35));
                float3 h = normalize(L + vd);
                float sp = pow(saturate(dot(n, h)), 90.0) * 1.4;
                float sp2 = pow(saturate(dot(n, normalize(float3(0.5, 0.3, 0.6) + vd))), 40.0) * 0.35;
                float a = saturate(_Color.a + f * _Rim + sp + sp2);
                float3 c = _Color.rgb * (0.85 + 0.3 * f) + sp + sp2;
                c = pow(saturate(c), 2.2);
                return half4(c, a);
            }
            ENDHLSL
        }
    }
}
