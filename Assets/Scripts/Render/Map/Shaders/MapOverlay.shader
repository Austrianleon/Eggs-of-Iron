Shader "EggsOfIron/Map/Overlay"
{
    Properties { _Palette("Political palette", 2D)="white" {} _Opacity("Opacity", Range(0,1))=.3 _UsePalette("Use palette",Float)=0 _SpriteTex("Sprite", 2D)="white" {} _UseSprite("Use sprite", Float)=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off ZWrite Off ZTest Always
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float3 positionOS:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            TEXTURE2D(_Palette); SAMPLER(sampler_Palette);
            TEXTURE2D(_SpriteTex); SAMPLER(sampler_SpriteTex);
            CBUFFER_START(UnityPerMaterial)
            float _Opacity, _UsePalette, _UseSprite;
            CBUFFER_END
            Varyings vert(Attributes i) { Varyings o; o.positionCS=TransformObjectToHClip(i.positionOS); o.color=i.color; o.uv=i.uv; return o; }
            half4 frag(Varyings i):SV_Target { half4 c=i.color; if (_UseSprite>.5) c *= SAMPLE_TEXTURE2D(_SpriteTex,sampler_SpriteTex,i.uv); if (_UsePalette>.5)
                {
                    c*=SAMPLE_TEXTURE2D(_Palette,sampler_Palette,i.uv);
                    // Preserve kingdom hues while fitting their imported colors to the muted map palette.
                    half luma=dot(c.rgb,half3(.2126,.7152,.0722));
                    c.rgb=lerp(luma.xxx,c.rgb,.75)*.72;
                } c.a*=_Opacity; return c; }
            ENDHLSL
        }
    }
}
