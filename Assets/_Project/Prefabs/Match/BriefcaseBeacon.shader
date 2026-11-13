// The briefcase beacon: a soft column of light drawn over everything (no depth test), so the
// briefcase reads through floors and walls. Additive and unlit; brightest at the bottom.
// _Fade is set per frame by Briefcase.cs from the viewer's distance (MatchProfile.BeaconRange).
Shader "Close the Deal/Briefcase Beacon"
{
    Properties
    {
        _BaseColor ("Colour", Color) = (1, 1, 1, 1)
        _Fade ("Fade (0 hidden, 1 full)", Range(0, 1)) = 1
        _Strength ("Strength", Range(0, 2)) = 0.8
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+50" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "Beacon"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Fade;
                half _Strength;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Fades up the column and toward its two side edges.
                half up = 1.0h - saturate(input.uv.y);
                half across = 1.0h - abs(input.uv.x * 2.0h - 1.0h);
                half glow = up * up * across * _Strength * _Fade;
                return half4(_BaseColor.rgb * glow, 0);
            }
            ENDHLSL
        }
    }
}
