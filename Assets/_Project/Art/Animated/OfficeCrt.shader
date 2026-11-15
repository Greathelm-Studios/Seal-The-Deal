// AI-GENERATED SOURCE FILE
// Generator : Codex (GPT-6)
// Generated : 2026-11-15
// Prompt    : REQUEST_ANIM.md; approved mood D in blue, scrolling office CRTs.
// Reviewed : NOT YET REVIEWED BY A HUMAN
// Summary   : One shared, unlit URP material; world-origin phase needs no per-screen updates.
Shader "Close the Deal/Animated/Office CRT"
{
    Properties
    {
        [Header(Screen)]
        _Background("Glass colour, darker = less visible in daylight", Color) = (0.008, 0.022, 0.065, 1)
        [HDR] _Ink("Aqua content, brighter = more screen glow", Color) = (0.24, 0.82, 0.95, 1)
        [HDR] _Paper("White content, brighter = more screen glow", Color) = (0.78, 0.93, 1.05, 1)
        _ScrollSpeed("Page scroll, rows per second; higher = faster", Range(0, 2)) = 0.32
        _ScanStrength("Scanline darkness, fraction; higher = stronger stripes", Range(0, 0.3)) = 0.09
        _Border("Dark border width, fraction of screen; higher = wider", Range(0.01, 0.2)) = 0.065
    }
    SubShader
    {
        // WHY: object origins must survive static/dynamic batching for independent phases;
        // this does not disable the SRP Batcher's material constant-buffer path.
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+10" "DisableBatching"="True" }
        Pass
        {
            Name "OfficeCRT"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Background;
                half4 _Ink;
                half4 _Paper;
                float _ScrollSpeed;
                float _ScanStrength;
                float _Border;
            CBUFFER_END
            static const float PageRows = 15.0;
            static const float SheetColumns = 6.0;
            static const float Scanlines = 180.0;
            static const float Tau = 6.28318530718;
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float phase : TEXCOORD1; float fog : TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                float3 origin = TransformObjectToWorld(float3(0, 0, 0));
                output.phase = frac(sin(dot(origin, float3(12.9898, 78.233, 37.719))) * 43758.5453);
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            float Box(float2 uv, float2 low, float2 high)
            {
                float2 width = max(fwidth(uv), float2(0.001, 0.001));
                float2 mask = smoothstep(low - width, low + width, uv) * (1 - smoothstep(high - width, high + width, uv));
                return mask.x * mask.y;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float page = floor(input.phase * 3.0);
                float body = Box(uv, float2(0.07, 0.08), float2(0.93, 0.78));
                float heading = Box(uv, float2(0.09, 0.84), float2(0.64, 0.88));
                float toolbar = Box(uv, float2(0.07, 0.93), float2(0.93, 0.96));
                float scroll = _Time.y * _ScrollSpeed / PageRows + input.phase;
                float rowY = frac(uv.y + scroll) * PageRows;
                float row = floor(rowY);
                float rowInk = 1 - smoothstep(0.13, 0.28, abs(frac(rowY) - 0.5));
                float2 cells = float2(uv.x * SheetColumns, rowY);
                float grid = (1 - smoothstep(0.015, 0.045, min(frac(cells.x), 1 - frac(cells.x)))) * 0.3;
                grid += (1 - smoothstep(0.015, 0.045, min(frac(cells.y), 1 - frac(cells.y)))) * 0.3;
                float textLength = 0.35 + 0.46 * frac(sin(row * 19.73 + floor(cells.x) * 8.31) * 3241.73);
                float sheet = saturate(grid + rowInk * Box(float2(frac(cells.x), 0.5), float2(0.12, 0), float2(textLength, 1)) * 0.72);
                float document = rowInk * step(0.13, uv.x) * (1 - step(0.46 + 0.39 * frac(sin(row * 3.19) * 41.7), uv.x));
                float chartX = saturate((uv.x - 0.12) / 0.76);
                float barId = floor(chartX * 9.0);
                float barHeight = 0.18 + 0.39 * (0.5 + 0.5 * sin(barId * 1.81 + input.phase * Tau + _Time.y * 0.11));
                float chart = step(frac(chartX * 9.0), 0.66) * step(uv.y, barHeight) * step(0.16, uv.y);
                float lineHeight = 0.48 + 0.13 * sin(chartX * 9.0 + input.phase * Tau + _Time.y * 0.07);
                chart = max(chart * 0.65, 1 - smoothstep(0.006, 0.017, abs(uv.y - lineHeight)));
                chart *= step(0.12, uv.x) * step(uv.x, 0.88);
                float content = page < 1 ? sheet : (page < 2 ? chart : document);
                half3 colour = _Background.rgb + _Ink.rgb * (body * content * 0.78 + toolbar * 0.4) + _Paper.rgb * heading;
                float scan = 1 - _ScanStrength * (0.5 + 0.5 * sin(uv.y * Scanlines * Tau));
                // WHY: fade the scanlines at a distance instead of producing a moving moire pattern.
                scan = lerp(scan, 1, saturate(fwidth(uv.y) * Scanlines));
                float edge = min(min(uv.x, 1 - uv.x), min(uv.y, 1 - uv.y));
                float vignette = smoothstep(0, _Border, edge);
                colour *= scan * lerp(0.12, 1, vignette);
                return half4(MixFog(colour, input.fog), 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
