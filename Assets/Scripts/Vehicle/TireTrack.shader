Shader "Custom/TireTrack"
{
    Properties
    {
        [MainTexture] _BaseMap ("Print Texture (RGBA)", 2D) = "white" {}
        [MainColor] _BaseColor ("Shadow Color", Color) = (0.32, 0.27, 0.20, 0.65)
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _NormalStrength ("Normal Strength", Range(0, 3)) = 1.2
        _EdgeShadow ("Edge Shadow", Range(0, 3)) = 1.4
        _Ambient ("Ambient", Range(0, 1)) = 0.6
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+10"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Forward"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                half3  normalWS    : TEXCOORD1;
                half4  tangentWS   : TEXCOORD2;
                float4 color       : COLOR;
                float  fogFactor   : TEXCOORD3;
            };

            TEXTURE2D(_BaseMap);   SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half   _NormalStrength;
                half   _EdgeShadow;
                half   _Ambient;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = posInputs.positionCS;
                OUT.uv          = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                OUT.tangentWS   = half4(TransformObjectToWorldDir(IN.tangentOS.xyz), IN.tangentOS.w);
                OUT.color       = IN.color;
                OUT.fogFactor   = ComputeFogFactor(posInputs.positionCS.z);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half alpha = tex.a * _BaseColor.a * IN.color.a;
                half3 albedo = tex.rgb * _BaseColor.rgb;

                // Normal map yang lu supply sendiri -> bikin relief/cekungan
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, IN.uv), _NormalStrength);
                normalTS = normalize(normalTS);

                // Bayangan di tepi/u jung pijakan: makin miring normalnya, makin gelap (kesan dipijak/cekung)
                half slope = 1.0h - saturate(normalTS.z);
                half edgeShadow = saturate(1.0h - slope * _EdgeShadow);

                half3 nWS = normalize(IN.normalWS);
                half3 tWS = normalize(IN.tangentWS.xyz);
                half3 bWS = cross(nWS, tWS) * IN.tangentWS.w;
                half3 normalWS = normalize(tWS * normalTS.x + bWS * normalTS.y + nWS * normalTS.z);

                Light mainLight = GetMainLight();
                half ndl = saturate(dot(normalWS, mainLight.direction));

                // FIX: Ambil ambient asli dari scene Unity (SH), dikali slider _Ambient buat pengali intensitas
                half3 ambient = SampleSH(normalWS) * _Ambient;

                // Sekarang pas malam (mainLight mati & ambient hitam), lighting-nya bakal 100% NOL!
                half3 lighting = mainLight.color * ndl + ambient;

                half3 color = albedo * lighting * edgeShadow;
                color = MixFog(color, IN.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
