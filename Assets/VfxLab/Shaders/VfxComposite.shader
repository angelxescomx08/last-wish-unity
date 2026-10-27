// Final VFX Lab composite: the low-resolution HDR pixel layer upscaled with sharp-bilinear sampling (crisp
// pixels at any window size), highlights pushed toward white, plus the smooth bloom layer at full resolution.
Shader "Hidden/LastWish/VFX/Composite"
{
    Properties
    {
        _MainTex ("Pixel layer", 2D) = "black" {}
        _BloomTex ("Bloom", 2D) = "black" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _MainTex; float4 _MainTex_TexelSize;
            sampler2D _BloomTex;
            float4 _Letterbox;      // xy offset, zw size of the picture inside the destination (uv units)
            float _Scale;           // destination pixels per pixel-layer texel
            float _BloomIntensity, _HotWhite, _Vignette, _BloomEnabled;
            float4 _BloomTint;
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert (appdata_img v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord; return o; }

            float2 SharpBilinear(float2 uv)
            {
                float2 size = _MainTex_TexelSize.zw;
                float2 texel = uv * size;
                float2 fl = floor(texel);
                float2 d = frac(texel) - 0.5;
                float range = 0.5 - 0.5 / max(_Scale, 1.0);
                float2 f = (d - clamp(d, -range, range)) * max(_Scale, 1.0) + 0.5;
                return (fl + f) / size;
            }

            half4 frag (v2f i) : SV_Target
            {
                float2 uv = (i.uv - _Letterbox.xy) / _Letterbox.zw;
                if (any(uv < 0) || any(uv > 1)) return half4(0, 0, 0, 1);
                half3 c = tex2D(_MainTex, SharpBilinear(uv)).rgb;
                half m = max(c.r, max(c.g, c.b));
                half3 col = saturate(c) + max(m - 1.0, 0) * _HotWhite;
                float2 buv = uv;
                #if UNITY_UV_STARTS_AT_TOP
                if (_MainTex_TexelSize.y < 0) buv.y = 1 - buv.y;
                #endif
                col += tex2D(_BloomTex, buv).rgb * _BloomIntensity * _BloomTint.rgb * _BloomEnabled;
                float2 v = (uv - 0.5) * float2(1.0, 0.85);
                col *= 1 - _Vignette * smoothstep(0.35, 0.85, length(v));
                return half4(saturate(col), 1);
            }
            ENDCG
        }
    }
}
