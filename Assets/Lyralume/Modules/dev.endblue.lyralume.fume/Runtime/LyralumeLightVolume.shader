Shader "Lyralume/Light Volume Source"
{
    Properties
    {
        _MainTex("Lyralume Output",2D)="black"{}
        _Color("Screen Tint",Color)=(1,1,1,1)
        _EmissionIntensity("Screen Brightness",Float)=1
        _ScreenOpacity("Whole Screen Fade",Range(0,1))=1
    }
    SubShader
    {
        Pass
        {
            ZTest Always ZWrite Off Cull Off Lighting Off Blend One Zero
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _Color;
            float _EmissionIntensity, _ScreenOpacity;
            float4 frag(v2f_img i):SV_Target
            {
                return float4(tex2D(_MainTex,i.uv).rgb*_Color.rgb*_EmissionIntensity*saturate(_ScreenOpacity),1);
            }
            ENDCG
        }
    }
}

