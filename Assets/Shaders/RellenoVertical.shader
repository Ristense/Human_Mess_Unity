// Llena un sprite de abajo hacia arriba, sin deformarlo.
//
// Es para los tanques diegéticos: el dibujo es parte de la nave, así
// que no se puede escalar para "llenarlo" — se vería estirado y
// despegado del casco. Acá el sprite se dibuja entero siempre y lo
// único que cambia es el color según la altura: lleno abajo del nivel,
// vacío arriba.
//
// El nivel se pasa con MaterialPropertyBlock desde BarraTanque, no con
// material.SetFloat: así los dos tanques comparten un material y no se
// rompe el batching.
Shader "HumanMess/RellenoVertical"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}

        [HDR] _Color ("Color lleno", Color) = (0.3, 0.7, 1, 1)
        _ColorVacio ("Color vacio", Color) = (1, 1, 1, 0.12)

        _Relleno ("Relleno", Range(0, 1)) = 0.6

        [Header(Borde)]
        _Suavidad ("Suavidad del borde", Range(0, 0.2)) = 0.01
        [HDR] _ColorBorde ("Color del borde", Color) = (1, 1, 1, 1)
        _Borde ("Grosor del borde", Range(0, 0.2)) = 0.02
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

            // _MainTex_ST va FUERA del CBUFFER a propósito: adentro
            // rompe el SRP Batcher 2D y tira warning. Está anotado en
            // TECNICO.md.
            float4 _MainTex_ST;

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _ColorVacio;
                half4 _ColorBorde;
                float _Relleno;
                float _Suavidad;
                float _Borde;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                // Se usa la UV y no la posición: así el relleno sigue al
                // sprite aunque el objeto esté rotado o escalado, que es
                // lo que pasa cuando el tanque cuelga del casco.
                float y = IN.uv.y;

                // 0 abajo del nivel (lleno), 1 arriba (vacío)
                float vacio = smoothstep(_Relleno - _Suavidad,
                                         _Relleno + _Suavidad, y);

                half4 col = lerp(_Color, _ColorVacio, vacio);

                // una línea en el nivel, para que se lea dónde está aun
                // cuando lleno y vacío tengan poco contraste
                if (_Borde > 0.0)
                {
                    float linea = 1.0 - smoothstep(0.0, _Borde, abs(y - _Relleno));
                    // sin borde cuando está vacío o lleno del todo: ahí
                    // la línea quedaría pegada al canto y se ve como un
                    // error de dibujo
                    linea *= step(0.001, _Relleno) * step(_Relleno, 0.999);
                    col = lerp(col, _ColorBorde, linea * _ColorBorde.a);
                }

                // El alfa del sprite manda: lo transparente sigue
                // transparente, así el relleno respeta la silueta del
                // dibujo en vez de pintar el rectángulo entero.
                col.a *= tex.a * IN.color.a;
                col.rgb *= IN.color.rgb;
                return col;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
