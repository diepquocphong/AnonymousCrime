Shader "Franklin Game/Mobile Blob Shadow"
{
    Properties
    {
        _BlobColor("Color", Color) = (0, 0, 0, 1)
        _BlobIntensity("Opacity", Range(0, 1)) = 0.58
        _BlobPower("Fullness", Range(0.25, 8)) = 1.7
        _BlobShape("Shape (0 Ellipse, 1 Rectangle)", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "MobileBlobShadow"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BlobColor;
                half _BlobIntensity;
                half _BlobPower;
                half _BlobShape;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPos : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.screenPos = ComputeScreenPos(output.positionCS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                screenUV = UnityStereoTransformScreenSpaceTex(screenUV);
                float rawDepth = SampleSceneDepth(screenUV);

                #if UNITY_REVERSED_Z
                    if (rawDepth <= 0.00001) return 0;
                #else
                    if (rawDepth >= 0.99999) return 0;
                #endif

                float3 positionWS = ComputeWorldSpacePosition(screenUV, rawDepth, UNITY_MATRIX_I_VP);
                float3 positionOS = TransformWorldToObject(positionWS);

                half2 footprintPoint = abs((half2)positionOS.xz) * 2.0h;
                half ellipseDistance = length(footprintPoint);
                half rectangleDistance = max(footprintPoint.x, footprintPoint.y);
                half radialDistance = saturate(
                    lerp(ellipseDistance, rectangleDistance, saturate(_BlobShape))
                );
                half radialFade = saturate(1.0h - pow(radialDistance, max(_BlobPower, 0.01h)));
                half verticalFade = saturate(1.0h - abs((half)positionOS.y) * 2.0h);
                half alpha = saturate(_BlobColor.a * _BlobIntensity * radialFade * verticalFade);

                return half4(_BlobColor.rgb, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
