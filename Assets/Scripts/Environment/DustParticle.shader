Shader "Custom/DustParticleAlpha"
{
    Properties
    {
        _MainTex ("Dust Texture (alpha)", 2D) = "white" {}
        _TintColor ("Dust Color", Color) = (0.80, 0.72, 0.58, 1)
        _Alpha ("Opacity", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "DustParticle"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                half4  color       : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4  _TintColor;
                half   _Alpha;
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half texAlpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a;
                half alpha = texAlpha * i.color.a * _Alpha;

                Light mainLight = GetMainLight();
                half3 ambient = SampleSH(float3(0, 1, 0));
                half3 lighting = (mainLight.color * 0.5h) + ambient;

                return half4(_TintColor.rgb * lighting, alpha);
            }
            ENDHLSL
        }
    }
}