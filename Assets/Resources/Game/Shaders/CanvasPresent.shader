// Draws the virtual canvas render texture on screen (opaque, bilinear like pygame smoothscale).
Shader "LastWish/CanvasPresent"
{
    Properties { _MainTex ("Texture", 2D) = "black" {} }
    SubShader
    {
        Tags { "Queue" = "Geometry" "RenderType" = "Opaque" }
        Cull Off ZWrite Off ZTest Always Lighting Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert (appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o; }
            fixed4 frag (v2f i) : SV_Target { return fixed4(tex2D(_MainTex, i.uv).rgb, 1); }
            ENDCG
        }
    }
}
