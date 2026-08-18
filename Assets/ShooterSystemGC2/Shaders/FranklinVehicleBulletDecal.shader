Shader "Franklin Game/Vehicle Bullet Decal Mobile"
{
    Properties
    {
        [MainTexture] _BaseMap("Bullet Mark", 2D) = "white" {}
        [MainColor] _Color("Tint", Color) = (1, 1, 1, 1)
        _AlphaCutoff("Alpha Cutoff", Range(0, 0.2)) = 0.025
        _SootStrength("Explosion Soot Darkness", Range(0, 1)) = 0
        _SootOpacity("Large Soot Area Opacity", Range(0, 1)) = 0
        _ImpactCoreScale("Impact Core Scale", Range(0.25, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+10"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Name "VehicleBulletMark"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            // Pull the coplanar mark toward the camera in depth space. The runtime
            // world offset remains for curved vehicle panels; this bias prevents
            // grazing-angle z-fighting while the camera moves or orbits.
            Offset -1, -1
            Cull Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _Color;
                half _AlphaCutoff;
                half _SootStrength;
                half _SootOpacity;
                half _ImpactCoreScale;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _Color;

                // RPG materials use the full-size source alpha as one large irregular black
                // soot area, then composite a reduced copy of the original crater over it.
                // Both layers remain in this single quad/pass. Ordinary bullet materials use
                // the default opacity zero and skip the second sample and all soot math.
                UNITY_BRANCH if (_SootOpacity > 0.001h)
                {
                    half coreScale = max(_ImpactCoreScale, 0.25h);
                    half2 coreUv = (input.uv - 0.5h) / coreScale + 0.5h;
                    half coreInside =
                        step(0.0h, coreUv.x) * step(coreUv.x, 1.0h) *
                        step(0.0h, coreUv.y) * step(coreUv.y, 1.0h);
                    half4 core = SAMPLE_TEXTURE2D(
                        _BaseMap,
                        sampler_BaseMap,
                        coreUv
                    ) * _Color;
                    core.a *= coreInside;

                    half2 centeredUv = input.uv * 2.0h - 1.0h;
                    half radius = length(centeredUv);
                    half haloFade = 1.0h - smoothstep(0.62h, 1.0h, radius);
                    half haloAlpha = saturate(
                        color.a * haloFade * _SootOpacity
                    );

                    half2 coreCenteredUv = coreUv * 2.0h - 1.0h;
                    half coreRadius = length(coreCenteredUv);
                    half coreRing =
                        smoothstep(0.08h, 0.26h, coreRadius) *
                        (1.0h - smoothstep(0.72h, 1.0h, coreRadius));
                    half3 sootColor = half3(0.005h, 0.005h, 0.005h);
                    core.rgb = lerp(
                        core.rgb,
                        sootColor,
                        saturate(coreRing * _SootStrength * core.a)
                    );

                    half haloBehindCore = haloAlpha * (1.0h - core.a);
                    half combinedAlpha = core.a + haloBehindCore;
                    half3 combinedPremultiplied =
                        core.rgb * core.a + sootColor * haloBehindCore;
                    color.rgb = combinedPremultiplied / max(combinedAlpha, 0.001h);
                    color.a = combinedAlpha;
                }

                clip(color.a - _AlphaCutoff);
                return color;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
