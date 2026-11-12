// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Fable 5.2 (Claude Code)
//  Generated : 2026-11-12
//  Prompt    : Alex: "i also wanna add a stylised reflection or sheen to the windows/glass"
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : The kit's glass. URP Lit glass is either invisible or a mirror, and the art bible
//              rules out mirrors ("glass is for visibility first", §12). This draws glass the way
//              an illustrator does: almost clear, with a pair of hard diagonal light streaks
//              that slide across the pane as the viewer moves, and a pale edge at grazing angles.
//              It is its own shader because none of that is a lighting response; it is drawn.
// -----------------------------------------------------------------------------
//
// Trade-off: drawn streaks, not a real reflection (reflection probe or planar reflection). A
// real reflection would put the sky and the room in the glass, which is exactly what hides the
// player across the street. Drawn streaks cover a fixed, small share of each pane, so how much
// of the view they cost is a slider, not an accident of the camera angle.
//
// It is unlit and casts no shadow, so sunlight passes through it onto the floor untouched.
Shader "Close the Deal/Glass Sheen"
{
    Properties
    {
        [Header(Glass)]
        [MainColor] _BaseColor("Glass tint (alpha = how solid; keep near 0.05 so players read through two panes)", Color) = (0.702, 0.851, 0.949, 0.05)

        [Header(Streaks)]
        _SheenColor("Streak colour (alpha = strength)", Color) = (1, 1, 1, 0.2)
        _SheenAngle("Streak tilt, degrees from vertical", Range(-80, 80)) = 32
        _SheenSpacing("Distance between streak pairs along the glass, metres", Range(0.5, 12)) = 6
        _SheenWide("Width of the wide streak, metres", Range(0, 2)) = 0.4
        _SheenThin("Width of the thin streak beside it, metres (0 = single streak)", Range(0, 1)) = 0.1
        _SheenSlide("How far the streaks slide as the viewer moves sideways (0 = painted on)", Range(0, 4)) = 1.6

        [Header(Edge)]
        _EdgeColor("Colour at grazing angles (alpha = strength)", Color) = (0.86, 0.93, 1, 0.5)
        _EdgePower("How close to edge-on before the edge colour shows (higher = only at the very edge)", Range(1, 8)) = 4
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Glass"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            // WHY back faces are culled: the kit's panes are thin closed boxes, so whichever side
            // you stand on there is already a face turned toward you. Drawing both faces put every
            // streak on screen twice and doubled its strength. A pane modelled as a single
            // one-sided sheet would need Cull Off instead.
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _SheenColor;
                half4 _EdgeColor;
                float _SheenAngle;
                float _SheenSpacing;
                float _SheenWide;
                float _SheenThin;
                float _SheenSlide;
                float _EdgePower;
            CBUFFER_END

            #define DEG_TO_RAD 0.01745329252
            /// Gap between the wide streak and the thin one, as a fraction of the wide one's width.
            #define SHEEN_GAP 0.45
            /// A pane counts as a wall pane (streaks run along it) unless its normal points this
            /// far up or down; a skylight or glass floor gets its streaks along world X instead.
            #define FLAT_PANE_NORMAL_Y 0.9

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            /// 1 inside a band from 0 to `width` metres, 0 outside, with one-pixel edges.
            half Band(float at, float width, float pixel)
            {
                return saturate(at / pixel + 0.5) * saturate((width - at) / pixel + 0.5) * step(0.0001, width);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                // Which way is "along the glass": horizontal and in the pane. Worked out from the
                // normal, so it is right for every pane, including the curved stair cage.
                float3 along = abs(normalWS.y) < FLAT_PANE_NORMAL_Y
                    ? normalize(cross(float3(0, 1, 0), normalWS))
                    : float3(1, 0, 0);
                // The same pane seen from its two sides must give the same streaks.
                along *= (along.x + along.z * 0.37) < 0.0 ? -1.0 : 1.0;

                float tilt = _SheenAngle * DEG_TO_RAD;
                // Distance across the streaks, metres. Sliding it by the viewer's sideways angle
                // is what makes the streaks move over the pane as you walk past, like a reflection.
                float across = dot(input.positionWS, along) * cos(tilt) + input.positionWS.y * sin(tilt);
                across += dot(viewDirWS, along) * _SheenSlide;
                float cell = across - floor(across / _SheenSpacing) * _SheenSpacing;
                float pixel = max(fwidth(across), 0.00001);

                half streak = Band(cell, _SheenWide, pixel);
                streak = max(streak, Band(cell - _SheenWide * (1.0 + SHEEN_GAP), _SheenThin, pixel));

                // Edge: seen edge-on, glass goes pale and more solid.
                half edge = pow(1.0h - saturate(dot(normalWS, viewDirWS)), _EdgePower);

                half3 color = _BaseColor.rgb;
                half alpha = _BaseColor.a;
                color = lerp(color, _EdgeColor.rgb, edge);
                alpha = saturate(alpha + edge * _EdgeColor.a);
                color = lerp(color, _SheenColor.rgb, streak);
                alpha = saturate(alpha + streak * _SheenColor.a);

                color = MixFog(color, input.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
