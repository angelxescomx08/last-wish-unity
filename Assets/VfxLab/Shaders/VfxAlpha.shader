// Alpha-blended particles/sprites for the VFX Lab (smoke, shadows). With _DitherAlpha the alpha becomes a
// Bayer pattern of fully opaque pixels, the classic pixel-art transparency.
Shader "LastWish/VFX/Alpha"
{
    Properties
    {
        _MainTex ("Texture (atlas or sprite)", 2D) = "white" {}
        [Toggle] _DitherAlpha ("Dithered transparency", Float) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off ZTest Always Cull Off Lighting Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "VfxCommon.cginc"
            sampler2D _MainTex; float4 _MainTex_ST;
            float _DitherAlpha;
            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            v2f vert (appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.color = v.color; o.uv = TRANSFORM_TEX(v.uv, _MainTex); return o; }
            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv) * i.color;
                if (_DitherAlpha > 0.5)
                {
                    clip(c.a - VfxBayer4(i.pos.xy));
                    c.a = 1;
                }
                return c;
            }
            ENDCG
        }
    }
}
