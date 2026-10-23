Shader "Lyralume/Overview Screen"
{
    Properties { _MainTex("Overview",2D)="black"{} _ViewRect("View Rectangle",Vector)=(.4,.4,.6,.6) _Cursor("Camera Center",Vector)=(.5,.5,0,0) _NativeMode("Real Stage Mode",Float)=0 _ContentOpacity("Real Stage Fade",Float)=1 _NativeTint("Real Stage Tint",Color)=(1,1,1,1) _NativeBrightness("Real Stage Brightness",Float)=1 }
    // This material is displayed by a world-space RawImage, never as opaque geometry.
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Cull Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert(appdata v) { v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.pos=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color; return o; }
            sampler2D _MainTex;float4 _ViewRect,_Cursor;
            float _NativeMode,_ContentOpacity,_NativeBrightness;float4 _NativeTint;
            float4 frag(v2f i):SV_Target
            {
                float2 pixel=max(fwidth(i.uv),.0001);float4 r=clamp(_ViewRect,.003,.997);
                float2 edge=min(abs(i.uv-r.xy),abs(i.uv-r.zw))/pixel;
                float inside=step(r.x,i.uv.x)*step(i.uv.x,r.z)*step(r.y,i.uv.y)*step(i.uv.y,r.w);
                float outline=(1-smoothstep(1,2,min(edge.x,edge.y)))*inside;
                float2 c=abs(i.uv-clamp(_Cursor.xy,.008,.992))/pixel;
                float cursor=max((1-smoothstep(1,2,c.x))*(1-smoothstep(5,7,c.y)),(1-smoothstep(1,2,c.y))*(1-smoothstep(5,7,c.x)));
                float2 grid=abs(frac(i.uv*float2(32,18))-.5);float g=step(.49,max(grid.x,grid.y))*.006;
                float3 color=tex2D(_MainTex,i.uv).rgb;
                color=_NativeMode>.5?color*_NativeTint.rgb*_NativeBrightness*saturate(_ContentOpacity):color+float3(.012,.009,.02)+g;
                return float4((color+float3(.65,.28,1)*max(outline,cursor))*i.color.rgb,i.color.a);
            }
            ENDCG
        }
    }
}

