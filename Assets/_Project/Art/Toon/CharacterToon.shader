// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Fable 5.2 (Claude Code)
//  Generated : 2026-11-11
//  Prompt    : Alex: "do what you recommend, as long as it fits the art direction and that
//              2000s corporate future aesthetic" (recommendation: soft building, graphic people)
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : The character half of the art bible's lighting formula, "soft world light + hard
//              character value breaks" (bible §04, §05). Characters need a different lighting
//              model from the building, which stays on URP Lit, so this is its own shader:
//              one base field, one hard cut shadow, an optional hard highlight shape and an
//              optional thin contour. Nothing else uses it.
// -----------------------------------------------------------------------------
//
// Trade-off: hand-written HLSL rather than Shader Graph. Shader Graph has no node for URP's
// clustered (Forward+) additional-light loop or for a second, front-culled contour pass, so
// both would need custom function nodes and a renderer feature anyway. One text file is the
// smaller thing to review and has no renderer asset to edit.
//
// Trade-off: the contour is an extruded back-face shell, not a full-screen edge filter. A
// screen filter outlines every object unless a renderer feature and layer mask are added, and
// the bible forbids "a thick black border around every object" (§05). The shell is per
// material, so it is selective by construction, and it is extruded in screen pixels so it
// keeps its width from across the street.
Shader "Close the Deal/Character Toon"
{
    Properties
    {
        [Header(Base field)]
        [MainColor] _BaseColor("Base colour (the lit side)", Color) = (0.067, 0.106, 0.220, 1)
        [MainTexture] _BaseMap("Base map (multiplies both sides)", 2D) = "white" {}

        [Header(Cut shadow)]
        _ShadowColor("Shadow colour (pick it colder, not just darker)", Color) = (0.035, 0.050, 0.120, 1)
        _ShadowThreshold("Shadow size (0 = none, 1 = everything in shadow)", Range(0, 1)) = 0.5
        _ShadowSoftness("Shadow edge softness (keep tiny: it only hides jaggies)", Range(0.001, 0.2)) = 0.01
        // WHY 0 by default: play happens indoors, under a slab, where the sun's cast shadow
        // covers the whole body and the cut would vanish into one flat shadow tone. The bible
        // allows the cheat ("visual clarity wins over physical correctness", §05): the cut
        // follows the sun's direction even where the sun cannot physically reach.
        _ReceiveShadows("Cast shadows cut in too (0 = ignore, 1 = full)", Range(0, 1)) = 0

        [Header(Hard highlight shape)]
        _HighlightColor("Highlight colour (alpha = strength)", Color) = (1, 1, 1, 1)
        _HighlightSize("Highlight size (0 = off; leather and shoes only)", Range(0, 1)) = 0

        [Header(Scene light)]
        _LightInfluence("How much room light changes the colours (0 = always as painted)", Range(0, 1)) = 0.35

        [Header(Contour)]
        _OutlineColor("Contour colour", Color) = (0.067, 0.106, 0.220, 1)
        _OutlineWidth("Contour width in screen pixels (0 = off)", Range(0, 8)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        // WHY one block shared by every pass: the SRP Batcher only batches a shader whose passes
        // all declare the same UnityPerMaterial layout.
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _ShadowColor;
            half4 _HighlightColor;
            half4 _OutlineColor;
            half _ShadowThreshold;
            half _ShadowSoftness;
            half _ReceiveShadows;
            half _HighlightSize;
            half _LightInfluence;
            half _OutlineWidth;
        CBUFFER_END
        ENDHLSL

        // ---------------------------------------------------------------------------------
        // Base field and cut shadow.
        // ---------------------------------------------------------------------------------
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            // WHY no _MAIN_LIGHT_SHADOWS_SCREEN: the PC renderer has no screen-space shadows
            // feature. If one is added, add that keyword and sample with the screen position.
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            /// Half-Lambert remap: turns N.L from -1..1 into 0..1 so the threshold slider is
            /// symmetric around the terminator (0.5 = the usual light/dark boundary).
            #define HALF_LAMBERT_SCALE 0.5
            #define HALF_LAMBERT_BIAS 0.5

            /// Specular cosine at the largest highlight size. Picked by eye: at 0.8 the shape
            /// covers roughly a quarter of a sphere, past which it stops reading as a highlight.
            #define HIGHLIGHT_WIDEST_COSINE 0.8

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
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
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            /// 1 on the lit side of the cut, 0 on the shadow side, for one light.
            half CutBand(half3 normalWS, half3 lightDirWS, half attenuation)
            {
                half wrapped = dot(normalWS, lightDirWS) * HALF_LAMBERT_SCALE + HALF_LAMBERT_BIAS;
                half lit = wrapped * attenuation;
                return smoothstep(_ShadowThreshold - _ShadowSoftness, _ShadowThreshold + _ShadowSoftness, lit);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb;

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half shadow = lerp(1.0h, mainLight.shadowAttenuation, _ReceiveShadows);
                half cut = CutBand(normalWS, mainLight.direction, shadow);

                // WHY a zero normal: it returns the average ambient colour with no direction in
                // it. Sampling with the real normal would paint a soft gradient across the body,
                // which the bible rules out ("no smooth character gradients", §05).
                half3 ambient = SampleSH(half3(0, 0, 0));

                // WHY clamp at 1: the look pass runs the sun above 1 so white walls bloom. A
                // character field is a chosen colour; room light may darken or tint it but must
                // never push it past what the artist picked.
                half3 litLight = min(ambient + mainLight.color * mainLight.distanceAttenuation, 1.0h);
                half3 shadowLight = min(ambient, 1.0h);
                half3 litField = _BaseColor.rgb * lerp(1.0h, litLight, _LightInfluence);
                half3 shadowField = _ShadowColor.rgb * lerp(1.0h, shadowLight, _LightInfluence);
                half3 color = lerp(shadowField, litField, cut) * albedo;

                #if defined(_ADDITIONAL_LIGHTS)
                    // Local lights (a muzzle flash, an explosion, later a ceiling fitting) add a
                    // second hard-edged patch of the base colour instead of a soft falloff.
                    InputData inputData = (InputData)0;
                    inputData.positionWS = input.positionWS;
                    inputData.normalWS = normalWS;
                    inputData.viewDirectionWS = viewDirWS;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                    half4 shadowMask = half4(1, 1, 1, 1);
                    half3 localLight = 0;
                    uint pixelLightCount = GetAdditionalLightsCount();

                    #if USE_CLUSTER_LIGHT_LOOP
                    // Forward+ keeps extra directional lights outside the clusters; URP's own
                    // lit shaders walk them first in exactly this way (Lighting.hlsl).
                    [loop] for (uint dirIndex = 0; dirIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); dirIndex++)
                    {
                        Light dirLight = GetAdditionalLight(dirIndex, input.positionWS, shadowMask);
                        localLight += dirLight.color * CutBand(normalWS, dirLight.direction, dirLight.shadowAttenuation);
                    }
                    #endif

                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        Light light = GetAdditionalLight(lightIndex, input.positionWS, shadowMask);
                        half reach = saturate(light.distanceAttenuation) * light.shadowAttenuation;
                        localLight += light.color * CutBand(normalWS, light.direction, reach);
                    LIGHT_LOOP_END

                    color += _BaseColor.rgb * albedo * localLight * _LightInfluence;
                #endif

                // Hard highlight: a designed shape on the lit side only, never a glossy falloff.
                half3 halfDir = SafeNormalize(mainLight.direction + viewDirWS);
                half highlightEdge = lerp(1.0h, HIGHLIGHT_WIDEST_COSINE, _HighlightSize);
                half highlight = smoothstep(highlightEdge - _ShadowSoftness, highlightEdge + _ShadowSoftness, dot(normalWS, halfDir));
                // WHY the step: at size 0 the edge sits at exactly 1 and smoothstep would still
                // return 0.5 on a normal facing the half vector, leaving a speck.
                highlight *= cut * _HighlightColor.a * step(0.0001h, _HighlightSize);
                color = lerp(color, _HighlightColor.rgb, highlight);

                color = MixFog(color, input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        // ---------------------------------------------------------------------------------
        // Contour: back faces pushed outward by a fixed number of screen pixels.
        // To switch it off without a wasted draw, disable this pass on the material
        // (the Close the Deal > Art > Build Character Toon Materials tool does that for
        // every material whose width is 0).
        // ---------------------------------------------------------------------------------
        Pass
        {
            Name "Contour"
            // WHY this tag: URP's opaque pass draws SRPDefaultUnlit alongside UniversalForward,
            // which gives a second pass with no renderer feature.
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            /// Clip space spans -1..1 across the screen, so one pixel is 2 / resolution.
            #define CLIP_UNITS_PER_SCREEN 2.0

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half fogFactor : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float4 positionCS = TransformObjectToHClip(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                // Direction of the normal on screen, in pixels, so the width is the same
                // horizontally and vertically whatever the aspect ratio.
                float2 screenDir = mul((float3x3)GetWorldToHClipMatrix(), normalWS).xy * _ScaledScreenParams.xy;
                float screenLen = length(screenDir);
                screenDir = screenLen > 0.00001 ? screenDir / screenLen : float2(0, 0);

                // WHY multiply by w: the perspective divide then cancels it, so the line is
                // _OutlineWidth pixels wide at 2 m and at 12 m across the street alike. A shell
                // sized in metres shrinks to nothing at exactly the range the game is played at.
                float2 pixel = CLIP_UNITS_PER_SCREEN / _ScaledScreenParams.xy;
                positionCS.xy += screenDir * pixel * _OutlineWidth * positionCS.w;

                output.positionCS = positionCS;
                output.fogFactor = ComputeFogFactor(positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return half4(MixFog(_OutlineColor.rgb, input.fogFactor), 1);
            }
            ENDHLSL
        }

        // ---------------------------------------------------------------------------------
        // Shadow caster: characters still throw ordinary shadows onto the soft building.
        // ---------------------------------------------------------------------------------
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            // Set by URP's ShadowUtils for the light being rendered (see URP ShadowCasterPass.hlsl).
            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                output.positionCS = ApplyShadowClamping(positionCS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // ---------------------------------------------------------------------------------
        // Depth and normals prepasses, so ambient occlusion and depth effects see characters.
        // ---------------------------------------------------------------------------------
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Same two encodings as URP's own depth-normals pass (UnlitDepthNormalsPass.hlsl).
                #if defined(_GBUFFER_NORMALS_OCT)
                    float3 normalWS = normalize(input.normalWS);
                    float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                    float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
                    return half4(PackFloat2To888(remappedOctNormalWS), 0.0);
                #else
                    return half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
                #endif
            }
            ENDHLSL
        }
    }

    FallBack Off
}
