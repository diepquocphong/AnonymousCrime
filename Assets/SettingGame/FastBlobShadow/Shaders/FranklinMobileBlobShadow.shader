Shader "Franklin Game/Mobile Blob Shadow"
{
    Properties
    {
        _BlobColor("Color", Color) = (0, 0, 0, 1)
        _BlobIntensity("Opacity", Range(0, 1)) = 0.58
        _BlobPower("Fullness", Range(0.25, 8)) = 1.7
        _BlobCore("Dark Core", Range(0, 0.9)) = 0.12
        _BlobShape("Shape (0 Ellipse, 1 Rectangle)", Range(0, 1)) = 0
        _BlobReceiverAbove("Receiver Above", Range(0.02, 0.5)) = 0.22
        _BlobReceiverBelow("Receiver Below", Range(0.02, 0.75)) = 0.40
        _BlobSeamAllowance("Seam Allowance", Range(0, 0.3)) = 0.10
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
                half _BlobCore;
                half _BlobShape;
                half _BlobReceiverAbove;
                half _BlobReceiverBelow;
                half _BlobSeamAllowance;
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
                if (max(footprintPoint.x, footprintPoint.y) >= 1.0h) return 0;

                // Neighboring ground objects can differ slightly in height or
                // normal. Allow more depth below the sampled plane and grow the
                // tolerance toward the footprint edge, while keeping the upper
                // limit small enough to reject vehicle bodies and roofs.
                half signedReceiverHeight = (half)positionOS.y;
                half edgeFactor = saturate(max(footprintPoint.x, footprintPoint.y));
                half aboveLimit = max(
                    _BlobReceiverAbove + _BlobSeamAllowance * edgeFactor,
                    0.01h
                );
                half belowLimit = max(
                    _BlobReceiverBelow + _BlobSeamAllowance * edgeFactor,
                    0.01h
                );
                half receiverLimit = lerp(
                    belowLimit,
                    aboveLimit,
                    step(0.0h, signedReceiverHeight)
                );
                half normalizedReceiverDistance =
                    abs(signedReceiverHeight) / receiverLimit;
                half verticalFade = 1.0h - smoothstep(
                    0.65h,
                    1.0h,
                    normalizedReceiverDistance
                );
                if (verticalFade <= 0.0001h) return 0;

                half radialDistance;
                if (_BlobShape > 0.5h)
                {
                    // An eighth-order superellipse keeps Car box-like. Three
                    // square roots are cheaper than a generic eighth-root pow.
                    half2 boxSquared = footprintPoint * footprintPoint;
                    half2 boxFourth = boxSquared * boxSquared;
                    half2 boxEighth = boxFourth * boxFourth;
                    half boxSum = boxEighth.x + boxEighth.y;
                    radialDistance = sqrt(sqrt(sqrt(boxSum)));
                }
                else
                {
                    radialDistance = length(footprintPoint);
                }
                radialDistance = saturate(radialDistance);
                half edgeFade = saturate(
                    (1.0h - radialDistance) / max(1.0h - _BlobCore, 0.01h)
                );
                edgeFade = edgeFade * edgeFade * (3.0h - 2.0h * edgeFade);
                half radialFade = pow(edgeFade, rcp(max(_BlobPower, 0.01h)));
                half alpha = saturate(_BlobColor.a * _BlobIntensity * radialFade * verticalFade);

                return half4(_BlobColor.rgb, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
