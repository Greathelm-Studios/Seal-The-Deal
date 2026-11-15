// AI-GENERATED SOURCE FILE
// Generator : Codex (GPT-6)
// Generated : 2026-11-15
// Prompt    : REQUEST_ANIM.md; cyan-white reception LED tickers for both firms.
// Reviewed : NOT YET REVIEWED BY A HUMAN
// Summary   : Text steps through a fixed dot lattice; no runtime text meshes or texture uploads.
Shader "Close the Deal/Animated/Office Ticker"
{
    Properties
    {
        [NoScaleOffset] _TextMask("Reception message mask, white letters on black", 2D) = "black" {}
        _Panel("Panel colour, darker = stronger contrast", Color) = (0.006, 0.016, 0.035, 1)
        [HDR] _Led("LED colour, brighter = more glow", Color) = (0.62, 1.02, 1.2, 1)
        _DotsAcross("LED columns across the strip; higher = smaller dots", Range(64, 512)) = 224
        _DotsHigh("LED rows up the strip; higher = smaller dots", Range(8, 32)) = 16
        _CellsPerSecond("Message speed, LED columns per second; higher = faster", Range(0, 40)) = 12
        _VisibleFraction("Share of the message visible at once; higher = smaller letters", Range(0.1, 1)) = 0.4375
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+10" }
        Pass
        {
            Name "OfficeTicker"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_TextMask); SAMPLER(sampler_TextMask);
            CBUFFER_START(UnityPerMaterial)
                half4 _Panel;
                half4 _Led;
                float _DotsAcross;
                float _DotsHigh;
                float _CellsPerSecond;
                float _VisibleFraction;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float fog : TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 grid = input.uv * float2(_DotsAcross, _DotsHigh);
                float2 cell = floor(grid) + 0.5;
                float stepOffset = floor(_Time.y * _CellsPerSecond);
                float2 textUV = float2(frac((cell.x + stepOffset) * _VisibleFraction / _DotsAcross), cell.y / _DotsHigh);
                half text = SAMPLE_TEXTURE2D(_TextMask, sampler_TextMask, textUV).r;
                float radius = length(frac(grid) - 0.5);
                float antialias = max(fwidth(radius), 0.025);
                float dots = 1 - smoothstep(0.32 - antialias, 0.32 + antialias, radius);
                float edge = smoothstep(0, 0.025, min(input.uv.x, 1 - input.uv.x));
                half3 colour = _Panel.rgb + _Led.rgb * dots * (0.015 + text * edge);
                return half4(MixFog(colour, input.fog), 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
