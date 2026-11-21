Shader "Close the Deal/Animated/Office Shaft"
{
    Properties
    {
        _Tint("Sky-blue shaft colour", Color) = (0.48, 0.76, 1, 1)
        _Strength("Added light, fraction; higher = denser haze", Range(0, 0.12)) = 0.027
        _Drift("Sideways drift, plane widths per second; higher = faster", Range(0, 0.05)) = 0.008
        _BreathSeconds("Seconds per brightness breath; higher = slower", Range(8, 90)) = 23
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" "DisableBatching"="True" }
        Pass
        {
            Name "OfficeShaft"
            Tags { "LightMode"="UniversalForwardOnly" }
            Blend SrcAlpha One
            Cull Off
            ZWrite Off
            ZTest LEqual
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _Strength;
                float _Drift;
                float _BreathSeconds;
            CBUFFER_END
            static const float Tau = 6.28318530718;
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; half3 normalWS : TEXCOORD2; float phase : TEXCOORD3; float fog : TEXCOORD4; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.phase = dot(TransformObjectToWorld(float3(0, 0, 0)), float3(0.71, 0.11, 0.37));
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float drift = sin(_Time.y * _Drift * Tau + input.phase) * 0.1;
                float across = abs(input.uv.x - 0.5 - drift);
                float sides = 1 - smoothstep(0.04, 0.48, across);
                float ends = smoothstep(0, 0.16, input.uv.y) * (1 - smoothstep(0.86, 1, input.uv.y));
                float wisps = 0.76 + 0.12 * sin(input.uv.x * 27 + input.uv.y * 5 - _Time.y * 0.09 + input.phase);
                wisps += 0.08 * sin(input.uv.y * 17 + input.uv.x * 11 + _Time.y * 0.06);
                float breath = 0.82 + 0.18 * sin(_Time.y * Tau / _BreathSeconds + input.phase);
                // Fade edge-on so crossed planes do not read as hard vertical cards.
                float facing = smoothstep(0.08, 0.55, abs(dot(normalize(GetWorldSpaceViewDir(input.positionWS)), normalize(input.normalWS))));
                float nearFade = smoothstep(0.25, 1.0, distance(GetCameraPositionWS(), input.positionWS));
                half3 colour = MixFogColor(_Tint.rgb, half3(0, 0, 0), input.fog);
                return half4(colour, _Strength * sides * ends * wisps * breath * facing * nearFade);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
