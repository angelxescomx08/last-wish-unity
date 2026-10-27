// Bloom for the VFX Lab (Built-in pipeline, HDR): soft-knee threshold, box downsample chain, additive tent upsample.
// Only what is brighter than the threshold (the HDR particle cores) glows; the LDR pixel art stays untouched.
Shader "Hidden/LastWish/VFX/Bloom"
{
    Properties { _MainTex ("Source", 2D) = "black" {} }
    CGINCLUDE
    #include "UnityCG.cginc"
    sampler2D _MainTex; float4 _MainTex_TexelSize;
    float4 _Filter;          // x threshold, y threshold - knee, z 2 * knee, w 0.25 / knee
    float _Scatter, _Clamp;
    struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
    v2f vert (appdata_img v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord; return o; }
    half3 Box(float2 uv, float delta)
    {
        float4 o = _MainTex_TexelSize.xyxy * float2(-delta, delta).xxyy;
        half3 s = tex2D(_MainTex, uv + o.xy).rgb + tex2D(_MainTex, uv + o.zy).rgb + tex2D(_MainTex, uv + o.xw).rgb + tex2D(_MainTex, uv + o.zw).rgb;
        return s * 0.25;
    }
    half3 Prefilter(half3 c)
    {
        c = min(c, _Clamp);
        half b = max(c.r, max(c.g, c.b));
        half soft = clamp(b - _Filter.y, 0, _Filter.z);
        soft = soft * soft * _Filter.w;
        half k = max(soft, b - _Filter.x) / max(b, 1e-4);
        return c * k;
    }
    ENDCG
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass // 0 prefilter + downsample
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag (v2f i) : SV_Target { return half4(Prefilter(Box(i.uv, 1)), 1); }
            ENDCG
        }
        Pass // 1 downsample
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag (v2f i) : SV_Target { return half4(Box(i.uv, 1), 1); }
            ENDCG
        }
        Pass // 2 upsample (added onto the larger level)
        {
            Blend One One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            half4 frag (v2f i) : SV_Target { return half4(Box(i.uv, 0.5) * _Scatter, 1); }
            ENDCG
        }
    }
}
