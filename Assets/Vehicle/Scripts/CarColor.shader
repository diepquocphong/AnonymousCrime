
Shader "ASCL/CarColor" 
{
	Properties 
	{
		_Color ("Main Color", Color) = (1,1,1,1)
		_DiffuseBoost ("Diffuse Boost", Range (0.03,2)) = 1.0
		_MainTex ("Diffuse Map", 2D) = "white" {}
		_SpecColor ("Spec Lighting Color", Color) = (0.5, 0.5, 0.5,1)
		_Gloss ("(R)Specular Boost", Range (0.03, 10.0)) = 0.078125
		_Spec ("(G)Gloss Boost", Range (0.01, 0.3)) = 0.078125
	}

	SubShader 
	{
		Tags { "RenderType"="Opaque" }
		LOD 400
		
		CGPROGRAM
		#pragma surface surf BlinnPhong
		#pragma target 3.0
		
		struct Input 
		{
			float2 uv_MainTex;
			float3 worldRefl;
			float3 viewDir;
			INTERNAL_DATA
		};

		float4 _Color;
		sampler2D _MainTex;
		float _Spec;
		float _Gloss;
		float _DiffuseBoost;

	    void surf (Input IN, inout SurfaceOutput o) 
	    {
			half4 tex = tex2D(_MainTex, IN.uv_MainTex);
			o.Albedo.rgb = tex.rgb;
			o.Albedo.rgb -= tex.a * tex.rgb;
			o.Albedo.rgb += tex.a * _Color * tex.rgb;
			o.Albedo = o.Albedo * _DiffuseBoost;
			o.Specular = _Spec*tex.a;
			o.Gloss = _Gloss*tex.a* tex.rgb;
		}
		ENDCG  
	}
	FallBack "VertexLit"
}



