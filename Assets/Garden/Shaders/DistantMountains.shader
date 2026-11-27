// Distant mountain ring around WuzhuangGarden: unlit, alpha-blended, no depth write, no shadows.
// Drawn after all opaque geometry (walls, roofs hide it through the depth test) and before the other transparents.
Shader "Garden/DistantMountains"
{
    Properties
    {
        _BaseMap ("Mountains (RGBA)", 2D) = "white" {}
        _Tint ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-50" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Name "Unlit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; half4 _Tint;
            CBUFFER_END
            struct A { float4 pos : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            V vert(A i) { V o; o.pos = TransformObjectToHClip(i.pos.xyz); o.uv = TRANSFORM_TEX(i.uv, _BaseMap); return o; }
            half4 frag(V i) : SV_Target { return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _Tint; }
            ENDHLSL
        }
    }
}
