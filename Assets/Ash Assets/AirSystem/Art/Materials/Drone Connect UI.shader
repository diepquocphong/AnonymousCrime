Shader "Franklin/Air/Drone Connect UI"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Shape ("Shape (0 Rounded, 1 Circle)", Float) = 0
        _HalfSize ("Rounded Half Size", Vector) = (0.398,0.408,0,0)
        _Radius ("Rounded Radius", Float) = 0.095
        _CircleRadius ("Circle Radius", Float) = 0.4
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _Shape;
            float4 _HalfSize;
            float _Radius;
            float _CircleRadius;

            v2f vert(appdata_t input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.texcoord = input.texcoord;
                output.color = input.color * _Color;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed4 color = tex2D(_MainTex, input.texcoord) * input.color;

                // ImageGen source art contains a baked checkerboard instead of
                // alpha. Reconstruct only the complete outer silhouette here;
                // this does not use a UGUI Mask or stencil pass.
                float2 uvOffset = abs(input.texcoord - 0.5);
                float2 distanceToCore = uvOffset - (_HalfSize.xy - _Radius);
                float roundedDistance = length(max(distanceToCore, 0.0)) +
                    min(max(distanceToCore.x, distanceToCore.y), 0.0) - _Radius;
                float circleDistance = length(input.texcoord - 0.5) - _CircleRadius;
                float signedDistance = lerp(
                    roundedDistance,
                    circleDistance,
                    step(0.5, _Shape)
                );
                color.a *= 1.0 - smoothstep(-0.004, 0.004, signedDistance);
                return color;
            }
            ENDCG
        }
    }
}
