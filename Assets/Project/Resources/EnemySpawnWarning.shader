Shader "Horde Muncher/EnemySpawnWarning"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.12, 0.04, 1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END

            float4 Vertex(float3 position : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(position);
            }

            half4 Fragment() : SV_Target
            {
                return _Color;
            }
            ENDHLSL
        }
    }
}
