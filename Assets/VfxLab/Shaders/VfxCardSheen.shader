// Foil sheen that sweeps diagonally across a card, masked by the card sprite alpha (additive, pixel bands).
Shader "LastWish/VFX/CardSheen"
{
    Properties
    {
        [PerRendererData] _MainTex ("Card sprite (mask)", 2D) = "white" {}
        _Color ("Colour", Color) = (0.55, 0.9, 1, 1)
        _Intensity ("Intensity", Float) = 0.35
        _Period ("Seconds between sweeps", Float) = 3.2
        _Width ("Band width (uv)", Float) = 0.12
        _Phase ("Phase offset", Float) = 0
        _Boost ("Extra (hover/play)", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Blend One One
        ZWrite Off ZTest Always Cull Off Lighting Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "VfxCommon.cginc"
            sampler2D _MainTex;
            fixed4 _Color; float _Intensity, _Period, _Width, _Phase, _Boost;
            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            v2f vert (appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.color = v.color; o.uv = v.uv; return o; }
            fixed4 frag (v2f i) : SV_Target
            {
                float a = tex2D(_MainTex, i.uv).a;
                float t = frac(_Time.y / _Period + _Phase) * 2.2 - 0.6;      // sweep from -0.6 to 1.6
                float d = abs((i.uv.x * 0.7 + (1 - i.uv.y) * 0.3) - t);
                float band = saturate(1 - d / _Width);
                band = VfxPixelAlpha(band, i.pos.xy, 3, 1);
                return float4(_Color.rgb * band * a * (_Intensity + _Boost) * i.color.a, 0);
            }
            ENDCG
        }
    }
}
