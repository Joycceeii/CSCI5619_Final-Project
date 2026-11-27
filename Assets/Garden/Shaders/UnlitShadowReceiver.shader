// Unlit texture (photo look, like URP/Unlit) that still receives the main light's realtime shadow.
// Used for the WuzhuangGarden ground and walls so the ginseng tree can cast the one shadow in the courtyard.
Shader "Garden/UnlitShadowReceiver"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.55
        _ShadowTint ("Shadow Tint", Color) = (0.62, 0.68, 0.82, 1)
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.45
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float4 _ShadowTint;
            float _ShadowStrength;
            float _Cutoff;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardUnlitShadow"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = TRANSFORM_TEX(i.uv, _BaseMap);
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float4 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                float att = MainLightRealtimeShadow(TransformWorldToShadowCoord(i.positionWS));
                // sunlit = the photo colour; in the tree shadow = darker and slightly cool
                float3 inShadow = lerp(float3(1, 1, 1), _ShadowTint.rgb * 0.8, saturate(_ShadowStrength * 1.4));   // ~60-70 % brightness, slightly cool
                float3 shade = lerp(inShadow, float3(1, 1, 1), att);
                return float4(c.rgb * shade, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Off
            HLSLPROGRAM
            #pragma vertex vertD
            #pragma fragment fragD
            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; };
            V vertD(A i) { V o; o.positionCS = TransformObjectToHClip(i.positionOS.xyz); return o; }
            half fragD(V i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
    FallBack Off
}

