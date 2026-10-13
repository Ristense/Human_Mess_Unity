// Cinta de peligro: franjas diagonales que desfilan.
//
// El sprite se usa solo como MÁSCARA — su alfa decide dónde hay
// cinta. Por eso conviene exportarlo en blanco: el color sale de acá.
Shader "HumanMess/CintaPeligro"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite (mascara)", 2D) = "white" {}

        [HDR] _ColorA ("Color franja", Color) = (1, 0.8, 0.1, 1)
        [HDR] _ColorB ("Color fondo", Color) = (0.1, 0.1, 0.1, 1)

        _Ancho ("Ancho de franja", Range(0.005, 0.5)) = 0.05
        _Proporcion ("Proporcion A/B", Range(0.05, 0.95)) = 0.5
        _Angulo ("Angulo", Range(0, 360)) = 45
        _Velocidad ("Velocidad", Range(-5, 5)) = 0.3
        _Suavidad ("Suavidad del filo", Range(0, 0.5)) = 0.05
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "PreviewType" = "Plane"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float4 color       : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorA;
                half4 _ColorB;
                float _Ancho;
                float _Proporcion;
                float _Angulo;
                float _Velocidad;
                float _Suavidad;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 mascara = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                // Proyectar la UV sobre la dirección de las franjas:
                // ese producto punto da "a qué distancia estás" medido
                // en perpendicular a las rayas. Girando esa dirección
                // se gira el rayado entero.
                float rad = radians(_Angulo);
                float2 dir = float2(cos(rad), sin(rad));
                float d = dot(IN.uv, dir);

                // frac lo vuelve un diente de sierra que se repite
                // cada _Ancho; el tiempo lo corre y por eso desfila
                float t = frac(d / max(_Ancho, 0.0001) - _Time.y * _Velocidad);

                // smoothstep en vez de step: filo suave, sin escalera
                float s = _Suavidad * 0.5 + 0.0001;
                float franja = smoothstep(_Proporcion - s, _Proporcion + s, t);

                half4 c = lerp(_ColorA, _ColorB, franja);
                c.a *= mascara.a * IN.color.a;
                c.rgb *= IN.color.rgb;
                return c;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
