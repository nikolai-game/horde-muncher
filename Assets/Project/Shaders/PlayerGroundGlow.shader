Shader "Horde Muncher/Player Ground Glow"
{
    Properties
    {
        [HDR] _GlowColor ("Glow Color", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" }
        Pass
        {
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 PositionOS : POSITION;
                float2 Uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 PositionCS : SV_POSITION;
                float2 Uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _GlowColor;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.PositionCS = TransformObjectToHClip(input.PositionOS.xyz);
                output.Uv = input.Uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float radius = length(input.Uv * 2 - 1);
                float halo = pow(saturate(1 - radius), 1.5) * 0.45;
                float ring = (1 - smoothstep(0.04, 0.15, abs(radius - 0.66))) * 0.8;
                return half4(_GlowColor.rgb, saturate(halo + ring) * _GlowColor.a);
            }
            ENDHLSL
        }
    }
}
