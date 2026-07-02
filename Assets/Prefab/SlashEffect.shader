Shader "Custom/SlashEffect"
{
    Properties
    {
        [HDR] _Color ("Slash Color", Color) = (0, 2, 2, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline" 
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "SimpleSlash"
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // ✅ 핵심 수정: X축만 그라데이션 적용
                // Y축은 완전히 무시하여 위아래가 칼날처럼 선명하게 유지됨
                float mask = sin(input.uv.x * 3.14159265);

                half3 finalColor = _Color.rgb * input.color.rgb;
                half finalAlpha = _Color.a * input.color.a * mask;

                return half4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/2D/Sprite-Unlit-Default"
}