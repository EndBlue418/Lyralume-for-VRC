Shader "Lyralume/Fume Glyph Display"
{
    Properties
    {
        _MainTex("Font SDF", 2D) = "white" {}
        _SongTime("Playback Seconds", Float) = 0
        _StageY("Stage Y", Float) = -1000
        _PrintDuration("Print Duration", Range(0.06,0.4)) = 0.16
        _PrintStrength("Print Strength", Range(0,1)) = 0.65
        _GlowStrength("Soft Halo", Range(0,0.5)) = 0.16
        _AfterglowSeconds("Afterglow Half Life", Range(0.3,6)) = 1.8
        _HistoryOpacity("History Opacity", Range(0,0.3)) = 0.055
        _RecentHistoryOpacity("Previous Line Opacity", Range(0.15,0.6)) = 0.38
        _HistoryLayerStrength("History Layering", Range(0,1)) = 1
        _PendingOpacity("Pending Opacity", Range(0,0.15)) = 0.018
        _SheenStrength("Singing Glyph Sheen", Range(0,0.18)) = 0.09
        _SustainGlow("Long Note Halo", Range(0,0.2)) = 0.09
        _ParagraphRetreat("Paragraph Retreat", Range(0,0.04)) = 0.015
        _EndingProgress("Closing Progress", Range(0,1)) = 0
        _FinalLineHold("Final Paragraph Hold", Range(0,1)) = 0
        _AccentColor("Album Accent", Color) = (1,1,1,1)
        _AccentBlend("Album Accent Blend", Range(0,1)) = 0
        _ActiveColorStrength("Singing And Print Color", Range(0,1)) = 0.78
        _ColorReleaseSeconds("Singing Color Release", Range(0.1,1.2)) = 0.55
        _KeywordColorStrength("Keyword Saturation", Range(0,1)) = 0.98
        _KeywordReleaseSeconds("Keyword Release After Line", Range(0.2,3)) = 1.2
        _RefrainColorStrength("Repeated Section Cover Color", Range(0,1)) = 0.92
        _RefrainHalo("Repeated Section Halo",Range(0,0.15)) = 0.055
        _InterludeAmount("Instrumental Breathing", Range(0,1)) = 0
        _LoadOpacity("Quick Load Fade", Range(0,1)) = 1
        _EndOpacity("Quick Ending Fade", Range(0,1)) = 1
        _StatusBrandOpacity("Status Brand Fade", Range(0,1)) = 1
        _StatusBrandTex("Original Lyralume Artwork",2D)="white" {}
    }
    SubShader
    {
        // Paragraph pivots are object-local UV data; dynamic batching would make vertices world-space.
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "DisableBatching"="True" }
        Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _SongTime, _StageY;
            float _PrintDuration, _PrintStrength, _GlowStrength, _AfterglowSeconds, _HistoryOpacity, _PendingOpacity;
            float _SheenStrength, _SustainGlow;
            float _RecentHistoryOpacity, _HistoryLayerStrength;
            float _ParagraphRetreat, _EndingProgress, _FinalLineHold;
            float4 _AccentColor;
            float _AccentBlend, _InterludeAmount;
            float _ActiveColorStrength, _ColorReleaseSeconds;
            float _KeywordColorStrength, _KeywordReleaseSeconds, _RefrainColorStrength;
            float _RefrainHalo;
            float _LoadOpacity, _EndOpacity;
            float _StatusBrandOpacity;
            sampler2D _StatusBrandTex;
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; float2 timing:TEXCOORD1; float2 lineData:TEXCOORD2; float2 paragraphPivot:TEXCOORD3; float2 paragraphExit:TEXCOORD4; float2 historyExit:TEXCOORD5; float2 styling:TEXCOORD6; float2 sectionTiming:TEXCOORD7; float4 color:COLOR; };
            struct v2f { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float2 timing:TEXCOORD1; float2 lineData:TEXCOORD2; float stamp:TEXCOORD3; float visible:TEXCOORD4; float hold:TEXCOORD5; float localX:TEXCOORD6; float4 paragraph:TEXCOORD7; float4 screen:TEXCOORD8; float4 styling:TEXCOORD9; };
            v2f vert(appdata v)
            {
                float retire=smoothstep(0,0.5,_SongTime-v.paragraphExit.x)*step(0.5,v.paragraphExit.y);
                v.vertex.xy=lerp(v.vertex.xy,v.paragraphPivot,retire*_ParagraphRetreat);
                v2f o; o.pos=UnityObjectToClipPos(v.vertex);
                o.paragraph=float4(retire,step(9999999,v.paragraphExit.x)*step(0.5,v.paragraphExit.y),v.historyExit.x,v.paragraphExit.x);
                if(abs(_WorldSpaceCameraPos.y-_StageY)>200) o.pos=float4(0,0,0,1);
                o.screen=ComputeScreenPos(o.pos); o.uv=v.uv; o.timing=v.timing; o.lineData=v.lineData; o.stamp=v.color.r; o.visible=v.color.a; o.hold=v.color.g*6; o.localX=v.color.b; o.styling=float4(v.styling,v.sectionTiming); return o;
            }
            float3 keywordColor(float id)
            {
                if(id<1.5) return float3(0.64,0.16,1);
                if(id<2.5) return float3(1,0.07,0.13);
                if(id<3.5) return float3(0.10,1,0.30);
                if(id<4.5) return float3(1,0.44,0.72);
                if(id<5.5) return float3(1,0.48,0.22);
                if(id<6.5) return float3(0.30,0.66,1);
                if(id<7.5) return float3(0.50,0.44,1);
                if(id<8.5) return float3(1,0.82,0.32);
                if(id<9.5) return float3(0.78,0.58,1);
                if(id<10.5) return float3(0.36,0.36,0.36); // Hiding / numbness: gray, without changing opacity.
                if(id<11.5) return float3(0.48,0.28,0.82); // Fear / anxiety.
                if(id<12.5) return float3(0.40,0.49,0.65); // Uncertainty.
                if(id<13.5) return float3(0.32,0.80,0.68); // Calm / relief.
                if(id<14.5) return float3(0.72,0.34,0.48); // Guilt / shame.
                if(id<15.5) return float3(0.65,0.82,0.26); // Envy / possessiveness.
                if(id<16.5) return float3(0.52,0.28,0.11); // Brown.
                if(id<17.5) return float3(0.95,0.95,0.95); // Literal white.
                if(id<18.5) return float3(0.16,0.16,0.16); // Readable near-black.
                if(id<19.5) return float3(0.60,0.65,0.72); // Silver color, no metallic surface effect.
                if(id<20.5) return float3(1,0.92,0.10); // Yellow.
                return float3(0.05,0.90,1); // Cyan.
            }
            float4 frag(v2f i):SV_Target
            {
                float2 screenUV=i.screen.xy/max(i.screen.w,0.0001);
                float screenX=screenUV.x;
                float edgeFade=smoothstep(0.005,0.05,screenX)*(1-smoothstep(0.95,0.995,screenX));
                float verticalFade=smoothstep(0.005,0.05,screenUV.y)*(1-smoothstep(0.95,0.995,screenUV.y));
                float readingMask=lerp(1,edgeFade*verticalFade,step(0.5,i.lineData.y))*saturate(_LoadOpacity)*saturate(_EndOpacity);
                float age=_SongTime-i.timing.x;
                float brand=step(2.5,i.lineData.y);
                readingMask*=lerp(1,saturate(_StatusBrandOpacity),brand);
                if(brand>0.5 && i.stamp>0.5)
                { float4 art=tex2D(_StatusBrandTex,i.uv); return float4(art.rgb,art.a*i.visible*readingMask); }
                float lit=smoothstep(0,0.085,age);
                float hold=max(i.hold,0.3)*(_AfterglowSeconds/1.8);
                float trail=exp2(-max(_SongTime-i.lineData.x-0.25,0)/max(hold,0.1));
                // A stable, legible album-derived singing color; no per-letter random palette.
                float3 accent=lerp(float3(0.68,0.50,1),_AccentColor.rgb,saturate(_AccentBlend));
                accent=clamp(accent/max(max(accent.r,accent.g),max(accent.b,0.01)),0.25,1);
                float3 activeTint=lerp(float3(1,0.995,0.985),accent,saturate(_ActiveColorStrength)*0.86);
                float3 sectionAccent=lerp(float3(0.68,0.50,1),_AccentColor.rgb,saturate(_AccentBlend));
                float whiteFloor=min(sectionAccent.r,min(sectionAccent.g,sectionAccent.b))*0.45;
                sectionAccent=saturate((sectionAccent-whiteFloor)/max(max(sectionAccent.r,max(sectionAccent.g,sectionAccent.b))-whiteFloor,0.01));
                float3 sectionTint=lerp(float3(1,0.995,0.985),sectionAccent,saturate(_RefrainColorStrength));
                float sectionEnvelope=step(0.99,i.styling.y)*smoothstep(i.styling.z,i.styling.z+0.6,_SongTime)*(1-smoothstep(i.styling.w,i.styling.w+0.9,_SongTime))*step(0.5,i.lineData.y);
                activeTint=lerp(activeTint,sectionTint,sectionEnvelope);
                float keywordId=floor(i.styling.x+0.001);
                float keyword=step(0.5,keywordId);
                float tier=frac(i.styling.x);
                float keywordWeight=tier<0.06?1:(tier<0.19?0.45:0.12);
                float repeat=saturate(frac(i.styling.y)*5);
                activeTint=lerp(activeTint,sectionTint,repeat*0.18*lit);
                activeTint=lerp(activeTint,lerp(float3(1,0.995,0.985),keywordColor(keywordId),saturate(_KeywordColorStrength)*keywordWeight),keyword);
                if(i.stamp>0.5)
                {
                    float duration=_PrintDuration*lerp(1,1.35,step(1.5,i.lineData.y));
                    float noteDuration=max(i.timing.y-i.timing.x,0.04);
                    duration=lerp(min(duration,max(0.06,noteDuration*0.8)),duration,step(1.5,i.lineData.y));
                    float pulse=smoothstep(0,0.022,age)*(1-smoothstep(duration*0.35,duration,age));
                    return float4(activeTint,pulse*_PrintStrength*0.78*step(0.5,i.lineData.y)*i.visible*readingMask);
                }
                float sdf=tex2D(_MainTex,i.uv).a;
                float edge=max(fwidth(sdf),0.005);
                float face=smoothstep(0.5-edge,0.5+edge,sdf);
                float glowPhase=saturate(age/clamp((i.timing.y-i.timing.x)*0.8,0.18,0.75));
                float glowEnvelope=smoothstep(0,0.7,glowPhase)*(1-0.65*smoothstep(0.7,1,glowPhase));
                float glow=smoothstep(0.34,0.5,sdf)*(1-face)*lit*trail*_GlowStrength*glowEnvelope;
                float aligned=step(0.5,i.lineData.y)*(1-step(1.5,i.lineData.y));
                float noteLength=max(i.timing.y-i.timing.x,0.04);
                float noteRelease=1-smoothstep(i.timing.y,i.timing.y+0.45,_SongTime);
                float sustain=smoothstep(0.65,1.3,noteLength)*smoothstep(0.12,max(0.3,noteLength*0.7),age)*noteRelease*aligned;
                // The halo slowly broadens through a real timed sustain; no simulated LRC long notes.
                float sustainSpread=smoothstep(0.15,max(noteLength,0.3),age);
                float sustainTail=smoothstep(i.timing.y,i.timing.y+0.18,_SongTime)*(1-smoothstep(i.timing.y+0.18,i.timing.y+0.85,_SongTime))*smoothstep(0.65,1.3,noteLength)*aligned;
                glow+=smoothstep(lerp(0.34,0.24,sustainSpread),0.49,sdf)*(1-face)*(sustain+sustainTail*0.35)*_SustainGlow;
                glow+=smoothstep(0.32,0.49,sdf)*(1-face)*lit*sectionEnvelope*(1-i.paragraph.x)*_RefrainHalo;
                float sheenClock=age/clamp(noteLength,0.16,0.65);
                float sheen=(1-smoothstep(0.08,0.32,abs(i.localX-lerp(-0.25,1.25,saturate(sheenClock))))) *
                    smoothstep(0,0.035,age)*(1-smoothstep(0.8,1.15,sheenClock))*aligned;
                float opacity=lerp(_PendingOpacity,lerp(_HistoryOpacity,1,trail),lit);
                // Keep the immediately preceding lyric readable; older lines become quiet article context.
                // Immutable timeline metadata skips blank rows and reconstructs correctly after seeks.
                float older=smoothstep(i.paragraph.z,i.paragraph.z+0.8,_SongTime);
                sectionEnvelope*=1-older;
                float trailAtNext=exp2(-max(i.paragraph.w-i.lineData.x-0.25,0)/max(hold,0.1));
                float recentLevel=min(_RecentHistoryOpacity,lerp(_HistoryOpacity,1,trailAtNext));
                float historyLevel=lerp(recentLevel,_HistoryOpacity*lerp(1,0.55,smoothstep(i.paragraph.z+2,i.paragraph.z+5,_SongTime)),older);
                opacity=lerp(opacity,historyLevel,i.paragraph.x*saturate(_HistoryLayerStrength));
                opacity=max(opacity,lerp(0.62,0.22,saturate(_EndingProgress))*saturate(_FinalLineHold)*i.paragraph.y*lit);
                float singing=aligned*lit*noteRelease;
                float3 tint=lerp(float3(1,0.995,0.985),sectionTint,repeat*0.12*lit*(1-older));
                tint=lerp(tint,sectionTint,sectionEnvelope*lit);
                tint=lerp(tint,keywordColor(keywordId),keyword*keywordWeight*(1-lit)*0.18);
                float haloShare=glow/max(face*opacity+glow,0.001);
                // Real YRC sustains to its true end; LRC colors only the short visual entrance.
                float lrcEnd=i.timing.x+_PrintDuration*1.35;
                float colorEnd=lerp(i.timing.y,lrcEnd,step(1.5,i.lineData.y));
                float colorEnvelope=lit*(1-smoothstep(colorEnd,colorEnd+max(_ColorReleaseSeconds,0.1),_SongTime))*step(0.5,i.lineData.y);
                // Keyword color is a retained visual accent, not an invented LRC singing clock.
                float keywordEnvelope=keyword*lit*(1-smoothstep(i.lineData.x+0.25,i.lineData.x+0.25+max(_KeywordReleaseSeconds,0.2),_SongTime))*(1-older)*step(0.5,i.lineData.y);
                colorEnvelope=max(colorEnvelope,keywordEnvelope);
                tint=lerp(tint,activeTint,colorEnvelope);
                tint*=1-_SheenStrength*singing*(1-sheen);
                tint*=lerp(float3(1,1,1),_AccentColor.rgb,saturate(_AccentBlend)*haloShare*0.35);
                tint=lerp(tint,float3(0.949,0.91,1),brand);
                return float4(tint,saturate(face*opacity+glow)*i.visible*readingMask*(1-saturate(_EndingProgress)*0.3)*(1-i.paragraph.x*0.08)*(1-saturate(_InterludeAmount)*0.3));
            }
            ENDCG
        }
    }
}

