Shader "Lyralume/Overview Glyph"
{
    Properties { _MainTex("Existing Glyph Atlas",2D)="white"{} _StageY("Stage Y",Float)=-5000 _ViewRect("Current View",Vector)=(0,0,0,0) }
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent" "DisableBatching"="True"}
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;float _StageY;float4 _ViewRect;
            struct v2f{float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float2 at:TEXCOORD1;};
            v2f vert(appdata_base v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);if(abs(_WorldSpaceCameraPos.y-_StageY)>200)o.pos=float4(0,0,0,1);o.uv=v.texcoord;o.at=v.vertex.xy;return o;}
            float4 frag(v2f i):SV_Target{float a=tex2D(_MainTex,i.uv).a;float e=max(fwidth(a),0.005);float inside=step(_ViewRect.x,i.at.x)*step(i.at.x,_ViewRect.z)*step(_ViewRect.y,i.at.y)*step(i.at.y,_ViewRect.w);return float4(lerp(float3(.42,.4,.48),float3(.86,.7,1),inside),smoothstep(.5-e,.5+e,a));}
            ENDCG
        }
    }
}

