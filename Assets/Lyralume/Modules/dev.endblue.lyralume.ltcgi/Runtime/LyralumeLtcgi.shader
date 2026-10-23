Shader "Lyralume/LTCGI Output"
{
    Properties {
        _MainTex("Lyralume Output",2D)="black"{}
        _Color("Screen Tint",Color)=(1,1,1,1)
        _EmissionIntensity("Brightness",Float)=1
        _ScreenOpacity("Whole Screen Fade",Range(0,1))=1
    }
    SubShader {
        Lighting Off Blend One Zero Cull Off ZWrite Off ZTest Always
        Pass {
            CGPROGRAM
            #include "UnityCustomRenderTexture.cginc"
            #pragma vertex CustomRenderTextureVertexShader
            #pragma fragment frag
            sampler2D _MainTex; float4 _Color; float _EmissionIntensity,_ScreenOpacity;
            float4 frag(v2f_customrendertexture i):SV_Target {
                return float4(tex2D(_MainTex,i.globalTexcoord.xy).rgb*_Color.rgb*_EmissionIntensity*saturate(_ScreenOpacity),1);
            }
            ENDCG
        }
    }
}
