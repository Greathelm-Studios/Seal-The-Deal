// Seal the Deal UI glass (pass B "Aero", built for URP + uGUI).
// One pane = blurred scene behind (from UiGlassBlurFeature), paper tint, hard diagonal sheen streak, 1 px inner
// highlight, hairline border, optional mullions, small corner radius. Navy text goes ON TOP in its own Graphic.
// When the blur is off (_UiGlassBlurOn = 0: feature missing, quality "No blur", or scene without it) the pane
// switches by itself to a plain, denser tint (_FallbackAlpha) over the sharp scene, so text stays legible.
// Alphas are ENGINE (Linear) values: the SVG mockups used higher sRGB alphas for the same look.
Shader "CloseTheDeal/UI/Glass"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite (unused)", 2D) = "white" {}
        _Tint ("Tint (paper)", Color) = (0.953, 0.957, 0.957, 1)
        _TintAlpha ("Tint alpha (engine)", Range(0, 1)) = 0.45
        _FallbackAlpha ("No-blur tint alpha (engine)", Range(0, 1)) = 0.72
        _SheenColor ("Sheen colour", Color) = (0.953, 0.957, 0.957, 1)
        _StreakAlpha ("Streak alpha", Range(0, 0.6)) = 0.16
        _StreakAt ("Streak position (0..1 of width)", Range(0, 1)) = 0.74
        _StreakWidth ("Streak width px", Float) = 26
        _SpecAlpha ("Specular strip alpha", Range(0, 0.6)) = 0.22
        _SpecHeight ("Specular strip height (0..1)", Range(0, 1)) = 0.42
        _Highlight ("Inner highlight alpha", Range(0, 1)) = 0.95
        _BorderColor ("Border colour (navy)", Color) = (0.067, 0.106, 0.22, 1)
        _BorderAlpha ("Border alpha", Range(0, 1)) = 0.38
        _EdgeLight ("Cyan edge-light alpha", Range(0, 1)) = 0.0
        _EdgeColor ("Edge-light colour (cyan)", Color) = (0.125, 0.831, 0.902, 1)
        _Radius ("Corner radius px", Range(0, 4)) = 3
        _Mullions ("Mullions (vertical bars)", Range(0, 4)) = 0
        _BlurMix ("Blur strength", Range(0, 1)) = 1
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [HideInInspector] _ClipRect ("Clip Rect", Vector) = (-32767, -32767, 32767, 32767)
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" "RenderPipeline" = "UniversalPipeline" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Glass"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_UiGlassBlurTex);
            SAMPLER(sampler_UiGlassBlurTex);
            float _UiGlassBlurOn;
            float4 _UiGlassBlurRect;   // where the blurred source sits on screen (x, y, w, h in 0..1)

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint, _SheenColor, _BorderColor, _EdgeColor;
                half _TintAlpha, _FallbackAlpha, _StreakAlpha, _StreakAt, _StreakWidth, _SpecAlpha, _SpecHeight;
                half _Highlight, _BorderAlpha, _EdgeLight, _Radius, _Mullions, _BlurMix;
            CBUFFER_END
            float4 _ClipRect;

            struct Attributes { float4 pos : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; float2 size : TEXCOORD1; };
            struct Varyings
            {
                float4 pos : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; float2 size : TEXCOORD1;
                float4 screen : TEXCOORD2; float2 world : TEXCOORD3;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.pos = TransformObjectToHClip(v.pos.xyz);
                o.color = v.color;
                o.uv = v.uv;
                o.size = max(v.size, float2(1, 1));    // UiGlass writes the pane size in px into uv1
                o.screen = ComputeScreenPos(o.pos);
                o.world = v.pos.xy;
                return o;
            }

            // coverage of a rounded rect (px), 1 inside
            float RoundedCover(float2 p, float2 size, float r)
            {
                float2 q = abs(p - size * 0.5) - (size * 0.5 - r);
                float d = length(max(q, 0)) + min(max(q.x, q.y), 0) - r;
                return saturate(0.5 - d);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 px = i.uv * i.size;
                float cover = RoundedCover(px, i.size, _Radius);
                // RectMask2D
                float2 inClip = step(_ClipRect.xy, i.world) * step(i.world, _ClipRect.zw);
                cover *= inClip.x * inClip.y;

                float on = _UiGlassBlurOn * _BlurMix;
                float2 suv = i.screen.xy / max(i.screen.w, 1e-5);
                float4 br = _UiGlassBlurRect.z > 0 ? _UiGlassBlurRect : float4(0, 0, 1, 1);   // unset = full screen
                suv = (suv - br.xy) / max(br.zw, float2(1e-4, 1e-4));
                half3 blur = SAMPLE_TEXTURE2D(_UiGlassBlurTex, sampler_UiGlassBlurTex, suv).rgb;

                // glass body: blurred scene with the paper tint over it. Without blur: tint only, denser.
                half tintA = lerp(_FallbackAlpha, _TintAlpha, on);
                half3 body = lerp(_Tint.rgb, lerp(blur, _Tint.rgb, tintA), on);
                half alpha = lerp(tintA, 1, on);

                half lit = 0;
                // specular strip across the top (fades down)
                half spec = (1 - smoothstep(0, _SpecHeight, i.uv.y)) * _SpecAlpha;
                // hard diagonal sheen streak, 18 deg from vertical, 24 px + a thin echo
                float dx = px.x - px.y * 0.3249;                       // tan(18 deg)
                float sx = _StreakAt * i.size.x;
                float st = step(sx, dx) * step(dx, sx + _StreakWidth) + step(sx + _StreakWidth + 8, dx) * step(dx, sx + _StreakWidth + 12);
                lit += spec + st * _StreakAlpha;
                // 1 px inner highlight, top and left
                lit += (step(px.y, 1.0) + step(px.x, 1.0)) * _Highlight * 0.5;
                // mullions: hairline + light line at k / (n + 1)
                if (_Mullions > 0.5)
                {
                    float n = floor(_Mullions + 0.5);
                    float k = round(i.uv.x * (n + 1));                    // nearest divider
                    float xm = abs(px.x - k * i.size.x / (n + 1));
                    float inner = step(0.5, k) * step(k, n + 0.5);        // dividers 1..n only
                    body = lerp(body, _BorderColor.rgb, step(xm, 1.0) * 0.16 * inner);
                    lit += step(abs(xm - 2.5), 0.75) * 0.7 * inner;       // light line beside the bar
                }
                body = lerp(body, _SheenColor.rgb, saturate(lit));
                // hairline outer border
                float2 e = min(px, i.size - px);
                float edge = step(min(e.x, e.y), 1.0);
                body = lerp(body, _BorderColor.rgb, edge * _BorderAlpha);
                // cyan edge-light along the bottom, short, fading to the ends
                float endFade = saturate(1 - abs(i.uv.x * 2 - 1) * 1.25);
                body = lerp(body, _EdgeColor.rgb, step(i.size.y - px.y, 2.0) * _EdgeLight * endFade);

                half4 col = half4(body, alpha * cover * i.color.a);
                return col;
            }
            ENDHLSL
        }
    }
}
