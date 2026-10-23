Shader "Lyralume/Fume Paper Display"
{
    Properties
    {
        _StageY("Stage Y",Float)=-1000
        _SongTime("Playback Seconds",Float)=0
        _MotionStrength("Background Motion",Range(0,1))=0.65
        _GrainStrength("Paper Grain",Range(0,0.008))=0.0015
        _BassTexture("Player AudioLink Texture",2D)="black" {}
        _BassEnabled("Verified Audio Ownership",Range(0,1))=0
        _GeometryStrength("Bass Geometry",Range(0,1))=0.8
        _HighMidStarStrength("High Mid Starbursts",Range(0,1))=0.8
        _HighMidRotation("Accumulated High Mid Rotation",Float)=0
        _SmoothedBands("Smoothed Bass HighMid Treble And Valid",Vector)=(0,0,0,0)
        _ReadingActivity("Dense Lyric Background Restraint",Range(0,1))=0
        _EndingProgress("Song Closing Calm",Range(0,1))=0
        _RefrainAmount("Repeated Section Atmosphere",Range(0,1))=0
        _TrebleGlintStrength("Treble Long Point Stars",Range(0,1))=0.9
        _StageView("Stage XY And View Size",Vector)=(0,0,17.4,9.8)
        _LyricFocus("Reading Area XY Half Size",Vector)=(0,0,1,1)
        _LyricFocusActive("Reading Area Enabled",Range(0,1))=0
        _CoverTex("Album Background",2D)="black" {}
        _CoverBlend("Album Visibility",Range(0,1))=0
        _CoverStrength("Album Brightness",Range(0,0.3))=0.22
        _CoverBlur("Album Blur Radius",Range(0,0.2))=0.14
        _AccentColor("Album Accent",Color)=(1,1,1,1)
        _AccentBlend("Album Accent Blend",Range(0,1))=0
        _InterludeAmount("Instrumental Breathing",Range(0,1))=0
        _ParallaxStrength("Background Depth Difference",Range(0,0.08))=0.04
    }
    SubShader
    {
        Tags {"Queue"="Background" "RenderType"="Opaque"}
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            float _StageY, _SongTime, _MotionStrength, _GrainStrength;
            sampler2D _BassTexture;
            float4 _BassTexture_TexelSize;
            float _BassEnabled, _GeometryStrength, _HighMidStarStrength, _TrebleGlintStrength;
            float _HighMidRotation;
            float4 _SmoothedBands;
            float _ReadingActivity, _RefrainAmount, _EndingProgress;
            float4 _StageView;
            float4 _LyricFocus;
            float _LyricFocusActive;
            sampler2D _CoverTex;
            float _CoverBlend, _CoverStrength, _CoverBlur;
            float4 _AccentColor;
            float _AccentBlend, _InterludeAmount, _ParallaxStrength;
            struct v2f {float4 pos:SV_POSITION; float2 uv:TEXCOORD0;};
            // Camera-owned unit quad: fill the output even during a wide overview zoom.
            v2f vert(appdata_base v){v2f o; o.pos=float4(v.vertex.xy*2,0,1);if(abs(_WorldSpaceCameraPos.y-_StageY)>200)o.pos=float4(0,0,0,1);o.uv=v.texcoord;return o;}
            float4 shapeRandom(float2 id)
            {
                float4 h=frac(id.xyxy*float4(0.1031,0.1030,0.0973,0.1099)+float4(0.13,0.71,0.37,0.91));
                h+=dot(h,h.wzxy+33.33);
                return frac((h.xxyz+h.yzzw)*h.zywx);
            }
            float segmentDistance(float2 p,float2 a,float2 b)
            {
                float2 edge=b-a;
                return length(p-a-edge*saturate(dot(p-a,edge)/dot(edge,edge)));
            }
            float shapeOutline(float2 p,float size,float kind)
            {
                if(kind<0.5) return abs(length(p)-size);
                if(kind<1.5)
                {
                    float2 d=abs(p)-size*0.78;
                    return abs(length(max(d,0))+min(max(d.x,d.y),0));
                }
                if(kind<2.5)
                {
                    float2 a=float2(0,size),b=float2(0.8660254,-0.5)*size,c=float2(-0.8660254,-0.5)*size;
                    return min(segmentDistance(p,a,b),min(segmentDistance(p,b,c),segmentDistance(p,c,a)));
                }
                float arm=size*0.7;
                return min(segmentDistance(p,float2(-arm,-arm),float2(arm,arm)),
                    segmentDistance(p,float2(-arm,arm),float2(arm,-arm)));
            }
            float scatteredShapes(float2 stage,float bass,float layer)
            {
                const float spacing=4.2;
                float2 cellId=floor(stage/spacing);
                float aa=max(fwidth(stage.x),fwidth(stage.y))*0.75;
                float result=0;
                // Cells only accelerate lookup. Wide independent XY jitter removes visible rows.
                // Neighbor cells prevent shapes being cut off at cell boundaries.
                [loop] for(int y=-1;y<=1;y++) [loop] for(int x=-1;x<=1;x++)
                {
                    float2 id=cellId+float2(x,y);
                    float4 random=shapeRandom(id);
                    // Each seed belongs to just one fixed depth plane; total density stays bounded.
                    float plane=step(0.5,frac(random.x*17.3+random.w*5.7));
                    [branch] if(abs(plane-layer)>0.1) continue;
                    float2 position=(id+0.08+random.xy*0.84)*spacing;
                    float size=0.30+frac(random.z*4)*0.38+bass*(0.12+random.x*0.10);
                    float2 local=stage-position;
                    [branch] if(max(abs(local.x),abs(local.y))>size*1.45+0.08+aa) continue;
                    float angle=random.w*6.2831853;
                    float sine,cosine; sincos(angle,sine,cosine);
                    float2 rotated=float2(local.x*cosine-local.y*sine,local.x*sine+local.y*cosine);
                    float distance=shapeOutline(rotated,size,floor(random.z*4));
                    float width=0.018+bass*0.025;
                    float opacity=0.82+frac(random.x*13.3+random.w)*0.18;
                    result=max(result,(1-smoothstep(width,width+aa,distance))*opacity);
                }
                return result;
            }
            float scatteredStars(float2 stage,float highMid)
            {
                const float spacing=3.15;
                float2 cellId=floor(stage/spacing);
                float aa=max(fwidth(stage.x),fwidth(stage.y))*0.8;
                float result=0;
                [loop] for(int y=-1;y<=1;y++) [loop] for(int x=-1;x<=1;x++)
                {
                    float2 id=cellId+float2(x,y);
                    float4 random=shapeRandom(id+float2(137.7,-81.3));
                    [branch] if(random.w<0.32) continue;
                    // Seeded centers live in article space, never camera-relative UVs.
                    float2 position=(id+0.07+random.xy*0.86)*spacing;
                    position+=(random.yz-0.5)*highMid*0.12;
                    float size=(0.14+random.z*0.15+highMid*(0.10+random.x*0.09))*1.12;
                    float2 local=stage-position;
                    [branch] if(max(abs(local.x),abs(local.y))>size*1.35+0.06+aa) continue;
                    float angle=random.x*6.2831853+_HighMidRotation*(random.w<0.5?-1:1);
                    float sine,cosine;sincos(angle,sine,cosine);
                    float2 rotated=float2(local.x*cosine-local.y*sine,local.x*sine+local.y*cosine);
                    rotated.y*=lerp(0.86,1.05,random.w);
                    // Astroid contour: four curved, inward-bowing sides and sharp points.
                    // Procedural geometry only; no image or sprite sampling.
                    float2 curve=pow(abs(rotated)/max(size,0.001),0.60);
                    float distance=abs(pow(curve.x+curve.y,1.6666667)-1)*size*0.55;
                    float width=0.009+highMid*0.010;
                    float outline=1-smoothstep(width,width+aa,distance);
                    float halo=(1-smoothstep(width+aa,width+aa*3,distance))*0.12*highMid;
                    float shimmer=0.86+0.14*sin(_SongTime*1.1+random.z*6.2831853);
                    result=max(result,(outline+halo)*lerp(0.75,1,random.y)*lerp(1,shimmer,highMid));
                }
                return result;
            }
            float scatteredGlints(float2 stage,float treble)
            {
                const float spacing=3.0;
                float2 cellId=floor(stage/spacing);
                float aa=max(fwidth(stage.x),fwidth(stage.y))*0.8;
                float result=0;
                [loop] for(int y=-1;y<=1;y++) [loop] for(int x=-1;x<=1;x++)
                {
                    float2 id=cellId+float2(x,y);
                    float4 random=shapeRandom(id+float2(-263.4,319.7));
                    [branch] if(random.w<0.38) continue;
                    float2 position=(id+0.06+random.xy*0.88)*spacing;
                    float size=(0.07+random.z*0.025+treble*0.045)*1.30;
                    float2 local=stage-position;
                    [branch] if(max(abs(local.x),abs(local.y))>size*1.5+aa*3)continue;
                    float sine,cosine;sincos(random.y*3.14159265,sine,cosine);
                    float2 p=float2(local.x*cosine-local.y*sine,local.x*sine+local.y*cosine);
                    // Small filled sparkle with a narrow heart and four long curved tips.
                    p.y*=0.88;
                    // Concave astroid-like boundary produces actual pointed cusps, not rounded cross bars.
                    float2 curve=pow(abs(p)/max(size,0.001),0.46);
                    float contour=curve.x+curve.y;
                    float ray=1-smoothstep(0.96,1.04,contour);
                    float core=exp2(-dot(p,p)/max(size*size*0.012,0.00001));
                    // Independent slow phases avoid a full-screen strobe; amplitude follows Treble immediately.
                    float sparkle=0.24+0.76*pow(0.5+0.5*sin(_SongTime*(2.6+random.w*2)+random.x*6.2831853),4);
                    result=max(result,(ray+core*0.35)*sparkle*(0.75+random.z*0.25));
                }
                return result;
            }
            float3 coverSample(float2 uv,float lod){return tex2Dlod(_CoverTex,float4(saturate(uv),0,lod)).rgb;}
            float4 frag(v2f i):SV_Target
            {
                float t=max(_SongTime,0)*_MotionStrength;
                float2 lightA=float2(0.12,0.72)+float2(sin(t*0.051),cos(t*0.037))*0.09;
                float2 lightB=float2(0.87,0.23)+float2(cos(t*0.043),sin(t*0.031))*0.07;
                float2 a=(i.uv-lightA)*float2(1.4,1);
                float2 b=(i.uv-lightB)*float2(1.6,1);
                float haze=exp(-dot(a,a)*5)*0.04+exp(-dot(b,b)*7)*0.017;
                float slope=0.38+sin(t*0.023)*0.015;
                float stroke=1-smoothstep(0.001,0.0025,abs(i.uv.y-(i.uv.x*slope+0.15+sin(t*0.045)*0.012)));
                float center=exp(-dot((i.uv-0.5)*2,(i.uv-0.5)*2)*2);
                float vignette=1-saturate(dot(i.uv-0.5,i.uv-0.5))*0.65;
                float2 grainCell=floor(i.uv*float2(640,360));
                float grain=frac(frac(dot(grainCell,float2(0.06711,0.00584)))*52.9829)-0.5;
                float value=((0.003+haze)*(1-center*0.28)+stroke*0.004)*vignette+grain*_GrainStrength;
                // Local AudioLink texture only. Blend fast Bass with a filtered envelope for clear attacks.
                float attack=tex2D(_BassTexture,float2(0.5,0.5)*_BassTexture_TexelSize.xy).r;
                float filtered=tex2D(_BassTexture,float2(3.5,28.5)*_BassTexture_TexelSize.xy).r;
                float bass=pow(saturate((attack*0.65+filtered*0.35)*1.8),0.65)*_BassEnabled;
                // ALPASS_AUDIOHIGHMIDS=(0,2); filtered High Mid is row 28+2.
                float highAttack=tex2D(_BassTexture,float2(0.5,2.5)*_BassTexture_TexelSize.xy).r;
                float highFiltered=tex2D(_BassTexture,float2(3.5,30.5)*_BassTexture_TexelSize.xy).r;
                float highMid=pow(saturate((highAttack*0.60+highFiltered*0.40)*1.65),0.8)*_BassEnabled;
                // ALPASS_AUDIOTREBLE=(0,3); fast envelope, with a little filtered release.
                float trebleAttack=tex2D(_BassTexture,float2(0.5,3.5)*_BassTexture_TexelSize.xy).r;
                float trebleFiltered=tex2D(_BassTexture,float2(3.5,31.5)*_BassTexture_TexelSize.xy).r;
                float treble=pow(saturate((trebleAttack*0.8+trebleFiltered*0.2)*2.4),0.65)*_BassEnabled;
                if(_SmoothedBands.w>0.5)
                {
                    bass=_SmoothedBands.x*_BassEnabled;
                    highMid=_SmoothedBands.y*_BassEnabled;
                    treble=_SmoothedBands.z*_BassEnabled;
                }
                // These coordinates are fixed to the article stage, independent of camera translation/zoom.
                float2 stage=i.uv*_StageView.zw+_StageView.xy-_StageView.zw*0.5;
                // Visible at rest, clear response on Bass; keep the lyric focus lower contrast.
                float geometry=0;
                [branch] if(_GeometryStrength>0 && _StageView.w<70)
                {
                    float nearShapes=scatteredShapes(stage,bass,0);
                    float farShapes=scatteredShapes(stage*(1-_ParallaxStrength),bass,1);
                    geometry=max(nearShapes,farShapes*0.66)*_GeometryStrength*(0.045+bass*0.17)*(1-center*0.55)*
                        (1-smoothstep(24,70,_StageView.w))*(1+saturate(_InterludeAmount)*0.12);
                }
                float stars=0;
                [branch] if(_HighMidStarStrength>0 && _StageView.w<70)
                    stars=scatteredStars(stage,highMid)*_HighMidStarStrength*(0.055+highMid*0.24)*
                        (1-center*0.35)*(1-smoothstep(24,70,_StageView.w));

                // Broad Gaussian spatial blur plus a bounded mip level avoids separated sharp ghost images.
                float2 outside=max(abs(stage-_LyricFocus.xy)-_LyricFocus.zw,0);
                float readingMask=1-smoothstep(0,1.35,length(outside));
                geometry*=1-readingMask*saturate(_LyricFocusActive)*0.90;
                stars*=1-readingMask*saturate(_LyricFocusActive)*0.94;
                float glints=0;
                [branch] if(_TrebleGlintStrength>0 && treble>0.001 && _StageView.w<70)
                    glints=scatteredGlints(stage,treble)*_TrebleGlintStrength*treble*0.55*
                        (1-smoothstep(24,70,_StageView.w))*(1-readingMask*saturate(_LyricFocusActive)*0.97);
                float aspect=max(_StageView.z/max(_StageView.w,0.01),0.1);
                float2 coverUV=float2(i.uv.x,(i.uv.y-0.5)/aspect+0.5);
                float radiusUV=_CoverBlur, lod=clamp(_CoverBlur*20,0,4);
                float2 dx=float2(radiusUV,0),dy=float2(0,radiusUV);
                float3 cover=coverSample(coverUV,lod)*4;
                cover+=(coverSample(coverUV+dx,lod)+coverSample(coverUV-dx,lod)+
                    coverSample(coverUV+dy,lod)+coverSample(coverUV-dy,lod))*2;
                cover+=coverSample(coverUV+dx+dy,lod)+coverSample(coverUV+dx-dy,lod)+
                    coverSample(coverUV-dx+dy,lod)+coverSample(coverUV-dx-dy,lod);
                cover*=0.0625;
                cover*=saturate(_CoverBlend)*_CoverStrength*vignette*(1-center*0.4);
                float refrain=saturate(_RefrainAmount);
                float3 geometryTint=lerp(float3(1,1,1),_AccentColor.rgb,saturate(_AccentBlend)*lerp(0.64,0.9,refrain));
                float restraint=saturate(_ReadingActivity);
                float3 reactive=(1-smoothstep(0,1,_EndingProgress)*0.88)*(geometry*lerp(1,0.55,restraint)+stars*lerp(1,0.42,restraint)+glints*lerp(1,0.35,restraint))*geometryTint;
                // The existing section envelope eases this lift across the whole chorus.
                // Keep the reading vignette and reactive restraint intact as the cover opens up.
                return float4((max(value,0).xxx+cover)*(1+refrain*0.65)+reactive+geometryTint*refrain*(1-center)*0.012,1);
            }
            ENDCG
        }
    }
}

