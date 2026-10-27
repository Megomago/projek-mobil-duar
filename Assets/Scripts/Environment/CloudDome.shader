Shader "Custom/CloudDome"
{
    Properties
    {
        [MainColor] _Tint ("Cloud Lit", Color) = (1, 1, 1, 1)
        _ShadowTint ("Cloud Shadow", Color) = (0.56, 0.62, 0.72, 1)
        _SunColor ("Sun Glow", Color) = (1, 0.93, 0.8, 1)
        _SunDirection ("Sun Direction", Vector) = (0.35, 0.6, 0.2, 0)
        _Coverage ("Coverage", Range(0, 1)) = 0.45
        _Softness ("Edge Softness", Range(0.01, 0.8)) = 0.28
        _Opacity ("Opacity", Range(0, 1)) = 1
        _Scale ("Cloud Scale", Float) = 2.6
        _Altitude ("Projection Altitude", Range(0.05, 1)) = 0.32
        _HorizonFade ("Horizon Fade", Range(0.01, 0.6)) = 0.12
        _WindSpeed ("Wind Speed", Float) = 0.012
        _WindDirection ("Wind Direction", Vector) = (1, 0, 0.25, 0)
        _SunPower ("Sun Glow Power", Range(1, 16)) = 4
        _SunStrength ("Sun Glow Strength", Range(0, 3)) = 0.7
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent-100"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "CloudDome"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 dir         : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float4 _ShadowTint;
                float4 _SunColor;
                float4 _SunDirection;
                float4 _WindDirection;
                float  _Coverage;
                float  _Softness;
                float  _Opacity;
                float  _Scale;
                float  _Altitude;
                float  _HorizonFade;
                float  _WindSpeed;
                float  _SunPower;
                float  _SunStrength;
            CBUFFER_END

            float Hash21(float2 p)
            {
                p = frac(p * float2(127.1, 311.7));
                p += dot(p, p + 19.19);
                return frac(p.x * p.y);
            }

            float CloudNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float CloudFbm(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5;
                const float2x2 rotation = float2x2(0.80, -0.60, 0.60, 0.80);

                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    value += amplitude * CloudNoise(p);
                    p = mul(rotation, p) * 2.07 + float2(17.3, 9.1);
                    amplitude *= 0.5;
                }

                return value * (1.0 / 0.9375);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.dir = normalize(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 dir = normalize(IN.dir);

                float horizon = smoothstep(-0.03, _HorizonFade, dir.y);
                if (horizon <= 0.001)
                    return half4(0.0, 0.0, 0.0, 0.0);

                float2 windDir = normalize(_WindDirection.xz + float2(0.0001, 0.0));
                float2 wind = windDir * (_Time.y * _WindSpeed);

                float2 uv = dir.xz / max(dir.y + _Altitude, 0.001) * _Scale;
                float2 p = uv + wind;

                float warp = CloudFbm(p * 0.65 + float2(3.7, 8.1));
                float clouds = CloudFbm(p + warp * 1.1);
                float fine = CloudNoise(p * 3.3 + warp * 2.0);
                clouds = clouds * 0.86 + fine * 0.14;

                float threshold = 1.0 - _Coverage;
                float body = smoothstep(threshold, threshold + _Softness, clouds);
                float alpha = body * _Opacity * horizon;

                float3 sunDir = normalize(_SunDirection.xyz);
                float sunAmount = saturate(dot(dir, sunDir));
                float sunGlow = pow(sunAmount, _SunPower);
                float edge = saturate(4.0 * body * (1.0 - body));

                float3 color = lerp(_ShadowTint.rgb, _Tint.rgb, smoothstep(threshold, 1.0, clouds));
                color += _SunColor.rgb * sunGlow * (0.25 + 0.75 * edge) * _SunStrength;

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
