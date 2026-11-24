Shader "EggsOfIron/Map/Terrain"
{
    Properties { _Detail("Detail", Range(0,1)) = .55 _DetailScale("Detail scale", Float) = 1.2 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            Cull Off ZWrite Off ZTest Always
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float3 positionOS:POSITION; float4 color:COLOR; float2 detail:TEXCOORD1; };
            struct Varyings { float4 positionCS:SV_POSITION; float4 color:COLOR; float2 p:TEXCOORD0; float2 detail:TEXCOORD1; };
            TEXTURE2D(_CoastalShading); SAMPLER(sampler_CoastalShading);
            CBUFFER_START(UnityPerMaterial)
            float4 _MapSize, _DeepOcean, _ShallowOcean;
            float _Detail, _DetailScale;
            CBUFFER_END
            Varyings vert(Attributes i) { Varyings o; o.positionCS=TransformObjectToHClip(i.positionOS); o.p=i.positionOS.xy; o.color=i.color; o.detail=i.detail; return o; }
            float hash(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float noise(float2 p) { float2 i=floor(p), f=frac(p); f=f*f*(3-2*f); return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y); }
            float3 waterColor(float2 position, float shallows, float pixelWidth)
            {
                float3 water = lerp(_DeepOcean.rgb, _ShallowOcean.rgb, shallows);
                float time = _Time.y;
                float patches = noise(position * .028);
                float drift = noise(position * .043 + float2(.016, -.011) * time);
                float2 alongWind = float2(dot(position, float2(.94, .34)), dot(position, float2(-.34, .94)));
                float ripples = noise(alongWind * float2(.075, .24) + float2(drift * .65, time * .025));
                float scattered = smoothstep(.57, .84, patches) * smoothstep(.65, .9, ripples);
                float visibility = 1 - smoothstep(1.5, 4, pixelWidth);
                water *= 1 + .032 * (drift - .5);
                return lerp(water, float3(.42, .72, .82), scattered * visibility * lerp(.018, .032, shallows));
            }
            half4 frag(Varyings i):SV_Target
            {
                float2 mapUV = i.p / max(_MapSize.xy, float2(1, 1));
                float shallows = SAMPLE_TEXTURE2D(_CoastalShading, sampler_CoastalShading, mapUV).r;
                float pixelWidth = length(fwidth(i.p));
                float2 p=i.p*_DetailScale;
                float h=i.detail.x, biome=i.detail.y;
                if (h < .1999)
                    return half4(saturate(waterColor(i.p, shallows, pixelWidth)), 1);

                float grain=(noise(p*1.7)-.5)*.11 + (noise(p*.18)-.5)*.15;
                float3 c=i.color.rgb;
                // Repeated triangular ridges with light and dark flanks, deterministic in map space.
                float2 tile=floor(p/3.5); float2 q=frac(p/3.5)-.5;
                q.x+=(hash(tile)-.5)*.35;
                float peak=(1-abs(q.x)*3.2)*.65;
                float mountain=step(abs(q.x),.31)*step(-.32,q.y)*step(q.y,peak-.27);
                float ridge=mountain*(q.x<0 ? .32 : -.4);
                float snow=mountain*step(.66,h)*step(peak-.37,q.y)*.25;
                float mountains=smoothstep(.48,.72,h);
                float desert=step(.5,biome)*step(biome,2.5);
                float dunes=sin(p.y*3 + noise(p*.25)*5)*.035*desert;
                c*=1+_Detail*(grain+ridge*mountains+dunes);
                c+=_Detail*snow*mountains*.22;
                return half4(saturate(c),1);
            }
            ENDHLSL
        }
    }
}
