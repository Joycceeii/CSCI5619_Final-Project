// Soft contact shadow (blob) under characters and props: multiplies the floor colour by a dark, slightly cool tint.
// Own tiny shader so it does not depend on URP/Unlit's transparent setup.
Shader "Garden/ContactShadow"
{
    Properties
    {
        _BaseMap ("Blob (alpha)", 2D) = "white" {}
        _ShadowColor ("Shadow Color", Color) = (0.22, 0.22, 0.26, 1)
        _Strength ("Strength", Range(0, 1.5)) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Geometry+60" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Blob"
            Tags { "LightMode" = "UniversalForward" }
            Blend DstColor Zero
            ZWrite Off
            ZTest LEqual
            Cull Off
            Offset -1, -1
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; float4 _ShadowColor; float _Strength;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            V vert(A i) { V o; o.positionCS = TransformObjectToHClip(i.positionOS.xyz); o.uv = i.uv; return o; }   // mesh UVs (object-space positions are world-space when the quads get batched)
            float4 frag(V i) : SV_Target
            {
                // radial falloff computed here (no texture): dark under the feet, fading to nothing at the edge
                float r = length(i.uv * 2 - 1);
                float a = saturate((1 - r) * 1.6); a = a * a * (3 - 2 * a) * 0.85;
                return float4(lerp(float3(1, 1, 1), float3(0.22, 0.22, 0.26), a), 1);   // multiply: darken the floor
            }
            ENDHLSL
        }
    }
    FallBack Off
}
