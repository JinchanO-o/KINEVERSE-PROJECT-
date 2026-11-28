Shader "Lab/InvertedHullOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1, 0.0784, 0.5765, 1)
        _OutlineWidth ("Outline Width (world metres)", Range(0, 0.05)) = 0.008
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.25
        _PulseSpeed ("Pulse Speed", Float) = 3
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "InvertedHullOutline"
            Tags { "LightMode" = "UniversalForward" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;
                float _PulseAmount;
                float _PulseSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float width = _OutlineWidth * (1.0 + _PulseAmount * sin(_Time.y * _PulseSpeed));
                // Extrude in world space so the width stays the same on imported models with large scales.
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionCS = TransformWorldToHClip(posWS + normalWS * width);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
