Shader "Lyralume/Fume Screen Display"
{
    Properties
    {
        _FoliaTex("Folia RenderTexture",2D)="black" {}
        _MainTex("VizVid-managed Video Texture",2D)="black" {}
        _IsAVProVideo("VizVid-managed AVPro Flag",Int)=0
        _Color("Tint",Color)=(1,1,1,1)
        _RenderMode("Visible Modes",Int)=4095
        _IsMirror("Mirror Flip",Int)=1
        _EmissionIntensity("Brightness",Float)=1
        _ScreenOpacity("Fade Through Black",Range(0,1))=1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            #include "Packages/idv.jlchntoz.vrcw-foundation/Shaders/VRCMirrorCameraSelector.cginc"
            sampler2D _FoliaTex; float4 _Color; float _EmissionIntensity, _ScreenOpacity; int _IsMirror;
            struct appdata {float4 vertex:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct v2f {float4 pos:SV_POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO};
            v2f vert(appdata v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_OUTPUT(v2f,o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos=UnityObjectToClipPos(v.vertex); o.uv=v.uv;
                if(!isVisibleInVRC()) o.pos=float4(0,0,0,1);
                if(_IsMirror && isInVRCMirror()) o.uv.x=1-o.uv.x;
                return o;
            }
            float4 frag(v2f i):SV_Target {UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i); return float4(tex2D(_FoliaTex,i.uv).rgb*_Color.rgb*_EmissionIntensity*saturate(_ScreenOpacity),1);}
            ENDCG
        }
    }
}

