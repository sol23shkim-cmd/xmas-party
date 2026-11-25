// ACOutline.shader — 뒷면을 법선 방향으로 부풀려 그리는 단색 테두리 (URP, 조명 없음).
// 쿠키 달리기 장애물에 빨간 테두리를 둘러 먹는 것과 구분하는 데 씀.
Shader "XmasParty/ACOutline"
{
    Properties
    {
        _Color ("Color (linear)", Color) = (0.9, 0.05, 0.1, 1)
        _Width ("Width (world m)", Float) = 0.05
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+1" }
        Pass
        {
            Name "Outline"
            Tags { "LightMode"="UniversalForward" }
            Cull Front
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Color; float _Width;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            float4 vert(A v) : SV_POSITION
            {
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                float3 nw = normalize(TransformObjectToWorldNormal(v.normalOS));
                ws += nw * _Width;
                return TransformWorldToHClip(ws);
            }
            half4 frag() : SV_Target { return half4(_Color.rgb, 1); }
            ENDHLSL
        }
    }
}
