// Additive HDR light for particles and emissive sprites in the VFX Lab pixel layer.
// colour = texture.rgb * texture.a * vertex colour * _Intensity * (Custom1.x when _UseCustomIntensity = 1).
// The HDR pixel target keeps values above 1, so only these layers feed the bloom.
Shader "LastWish/VFX/Additive"
{
    Properties
    {
        _MainTex ("Texture (atlas or sprite)", 2D) = "white" {}
        _Intensity ("HDR intensity", Float) = 1
        [Toggle] _UseCustomIntensity ("Multiply by Custom1.x (particle custom data)", Float) = 1
        _Steps ("Posterize steps (0 = off)", Float) = 0
        _Dither ("Dither (0..1)", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }
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
            sampler2D _MainTex; float4 _MainTex_ST;
            float _Intensity, _UseCustomIntensity, _Steps, _Dither;
            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; float3 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; fixed4 color : COLOR; float3 uv : TEXCOORD0; };
            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = float3(TRANSFORM_TEX(v.uv.xy, _MainTex), v.uv.z);
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 t = tex2D(_MainTex, i.uv.xy);
                float a = VfxPixelAlpha(t.a * i.color.a, i.pos.xy, _Steps, _Dither);
                float k = _Intensity * lerp(1.0, i.uv.z, _UseCustomIntensity);
                return float4(t.rgb * i.color.rgb * a * k, 0);
            }
            ENDCG
        }
    }
}
