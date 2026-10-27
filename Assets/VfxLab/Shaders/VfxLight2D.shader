// Fake 2D point light for the Built-in pipeline (there is no Light2D without URP).
// Pass 1 multiplies-and-adds onto what is already drawn (dst * (1 + light)): sprites near the light are lit in
// its colour while black stays black. Pass 2 adds a small spill so the air around the light glows a little.
Shader "LastWish/VFX/Light2D"
{
    Properties
    {
        _MainTex ("Falloff (R)", 2D) = "white" {}
        _Intensity ("Intensity", Float) = 1
        _Spill ("Additive spill", Range(0, 1)) = 0.08
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        ZWrite Off ZTest Always Cull Off Lighting Off
        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex; float4 _MainTex_ST;
        float _Intensity, _Spill;
        struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
        struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
        v2f vert (appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.color = v.color; o.uv = TRANSFORM_TEX(v.uv, _MainTex); return o; }
        float3 Light(v2f i) { return tex2D(_MainTex, i.uv).r * i.color.rgb * i.color.a * _Intensity; }
        ENDCG
        Pass
        {
            Blend DstColor One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            fixed4 frag (v2f i) : SV_Target { return float4(Light(i), 0); }
            ENDCG
        }
        Pass
        {
            Blend One One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            fixed4 frag (v2f i) : SV_Target { return float4(Light(i) * _Spill, 0); }
            ENDCG
        }
    }
}
