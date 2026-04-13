Shader "Game/OcclusionRevealURP"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _Metallic("Metallic", Range(0, 1)) = 0
        _Smoothness("Smoothness", Range(0, 1)) = 0.45
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
        [HideInInspector] _Cull("Cull", Float) = 2

        [HideInInspector] _OcclusionFade("Occlusion Fade", Float) = 0
        [HideInInspector] _OcclusionCenter("Occlusion Center", Vector) = (0, 0, 0, 0)
        [HideInInspector] _OcclusionRadius("Occlusion Radius", Float) = 1.85
        [HideInInspector] _OcclusionFeather("Occlusion Feather", Float) = 0.9
        [HideInInspector] _OcclusionAlpha("Occlusion Alpha", Float) = 0.18
        [HideInInspector] _OcclusionRimColor("Occlusion Rim Color", Color) = (1, 0.42, 0.12, 1)
        [HideInInspector] _OcclusionRimStrength("Occlusion Rim Strength", Float) = 1.15
        [HideInInspector] _OcclusionNoiseScale("Occlusion Noise Scale", Float) = 4
        [HideInInspector] _OcclusionNoiseSpeed("Occlusion Noise Speed", Float) = 0.7
        [HideInInspector] _OcclusionDitherStrength("Occlusion Dither Strength", Float) = 0.28
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Metallic;
                half _Smoothness;
                half _Cutoff;
                half _Cull;
                float _OcclusionFade;
                float4 _OcclusionCenter;
                float _OcclusionRadius;
                float _OcclusionFeather;
                float _OcclusionAlpha;
                float4 _OcclusionRimColor;
                float _OcclusionRimStrength;
                float _OcclusionNoiseScale;
                float _OcclusionNoiseSpeed;
                float _OcclusionDitherStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 local = frac(p);
                float2 smoothLocal = local * local * (3.0 - 2.0 * local);

                float a = Hash21(cell);
                float b = Hash21(cell + float2(1.0, 0.0));
                float c = Hash21(cell + float2(0.0, 1.0));
                float d = Hash21(cell + float2(1.0, 1.0));

                return lerp(lerp(a, b, smoothLocal.x), lerp(c, d, smoothLocal.x), smoothLocal.y);
            }

            float Fbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                float frequency = 1.0;

                for (int octave = 0; octave < 4; octave++)
                {
                    value += ValueNoise(p * frequency) * amplitude;
                    frequency *= 2.03;
                    amplitude *= 0.5;
                    p = mul(float2x2(0.8, -0.6, 0.6, 0.8), p + 11.17);
                }

                return value;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;

                float feather = max(_OcclusionFeather, 0.001);
                float radius = max(_OcclusionRadius, 0.001);
                float3 toRevealCenter = input.positionWS - _OcclusionCenter.xyz;
                float2 revealPlane = toRevealCenter.xz;
                float distanceToCenter = length(toRevealCenter);
                float angle = atan2(revealPlane.y, revealPlane.x);

                float noiseScale = max(_OcclusionNoiseScale, 0.001);
                float burnDrift = _Time.y * _OcclusionNoiseSpeed * 0.12;
                float2 radialNoiseUv = revealPlane / max(radius, 0.001);
                float edgeNoise = Fbm(radialNoiseUv * noiseScale * 1.35 + float2(burnDrift, -burnDrift * 0.7));
                float charNoise = Fbm(radialNoiseUv * noiseScale * 3.2 - float2(burnDrift * 1.7, burnDrift));
                float lobeNoise = sin(angle * 7.0 + edgeNoise * 6.2831853) * 0.5
                    + sin(angle * 13.0 + charNoise * 4.2) * 0.25;

                float raggedRadius = radius + (edgeNoise - 0.5) * feather * 1.6 + lobeNoise * feather * 0.22;
                float signedEdgeDistance = distanceToCenter - raggedRadius;
                float interiorMask = 1.0 - smoothstep(-feather * 0.35, feather * 0.75, signedEdgeDistance);
                float edgeBand = 1.0 - smoothstep(0.0, feather * 0.85, abs(signedEdgeDistance));
                float innerCharBand = 1.0 - smoothstep(0.0, feather * 1.25, abs(signedEdgeDistance + feather * 0.28));
                float emberBand = 1.0 - smoothstep(0.0, feather * 0.22, abs(signedEdgeDistance + feather * 0.06));

                float chunkMask = smoothstep(0.53, 0.93, charNoise + edgeBand * 0.15);
                float ashBite = chunkMask * edgeBand * _OcclusionDitherStrength * 1.35;
                float reveal = saturate((interiorMask + ashBite) * _OcclusionFade);
                float styledRim = saturate((edgeBand * 0.6 + innerCharBand * 0.45 + emberBand * 0.85) * _OcclusionFade);

                half alpha = lerp(baseSample.a, min(baseSample.a, (half)_OcclusionAlpha), (half)reveal);
                half3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                Light mainLight = GetMainLight();
                half mainLightAmount = saturate(dot(normalWS, mainLight.direction));
                half3 ambient = SampleSH(normalWS);
                half3 color = baseSample.rgb * (ambient + mainLight.color * mainLightAmount);
                half soot = (half)saturate((innerCharBand * 0.55 + edgeBand * 0.35) * _OcclusionFade);
                color = lerp(color, color * half3(0.08, 0.06, 0.045), soot);
                color += half3(_OcclusionRimColor.rgb) * (half)(styledRim * _OcclusionRimStrength);
                color = MixFog(color, input.fogFactor);

                return half4(color, alpha);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }

    FallBack Off
}
