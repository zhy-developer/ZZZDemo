Shader "ZZZ/VFX/Parry Flash"
{
    Properties
    {
        [HDR] _Tint ("Tint", Color) = (1, 0.68, 0.08, 1)
        _Intensity ("Intensity", Range(0, 8)) = 2
        [Enum(Cross,0,Glow,1,Spark,2,HorizontalFlare,3,RadialBurst,4)] _Shape ("Shape", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Name "ParryFlash"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _Intensity;
                float _Shape;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2 - 1;
                float radius2 = dot(p, p);
                // Analytic soft edges keep the flash clean at any scale; no texture or camera script is needed.
                float core = exp2(-radius2 * 90);
                float coverage;
                if (_Shape < 0.5)
                {
                    float horizontal = exp2(-p.y * p.y * 340) * pow(saturate(1 - abs(p.x)), 1.4);
                    float vertical = exp2(-p.x * p.x * 340) * pow(saturate(1 - abs(p.y)), 1.4);
                    coverage = saturate(horizontal + vertical + core * 0.6);
                }
                else if (_Shape < 1.5)
                {
                    coverage = exp2(-radius2 * 7) * saturate(1 - radius2);
                }
                else if (_Shape < 2.5)
                {
                    // Renderer stretching gives sparks their velocity-aligned streaks.
                    coverage = exp2(-radius2 * 5) * saturate(1 - radius2);
                }
                else if (_Shape < 3.5)
                {
                    // Camera-facing horizontal line, with a wider soft shoulder.
                    // Analytic glow remains visible even without scene Bloom.
                    float width = max(fwidth(p.y), 0.004);
                    float lineCoverage = 1 - smoothstep(0.006, 0.006 + width, abs(p.y));
                    float shoulder = exp2(-p.y * p.y * 1800) * 0.28;
                    coverage = saturate(lineCoverage + shoulder) * pow(saturate(1 - abs(p.x)), 0.7);
                }
                else
                {
                    // Unequal sharp rays give the first instant an impact silhouette.
                    // The moving particle layers carry the sparks outward afterwards.
                    float angle = atan2(p.y, p.x + 0.00001);
                    float rays = pow(abs(cos(angle * 7 + 0.4)), 28);
                    float secondary = pow(abs(cos(angle * 11 - 0.7)), 42) * 0.55;
                    float taper = pow(saturate(1 - sqrt(radius2)), 1.2);
                    coverage = saturate((rays + secondary) * taper + core);
                }
                half3 tint = lerp(_Tint.rgb, half3(1, 0.98, 0.82), core * 0.65);
                return half4(tint * input.color.rgb * _Intensity,
                    coverage * input.color.a * _Tint.a);
            }
            ENDHLSL
        }
    }
}
