// Campo de estrellas procedural + manchas de nebulosa.
//
// ESTRELLAS: cada celda de una grilla invisible tira una moneda (hash)
// para decidir si tiene estrella, dónde cae adentro de la celda, y
// CON QUÉ COLOR de la paleta (5 colores, elegidos por otro hash — así
// no salen todas blancas, como estrellas reales de distinta
// temperatura).
//
// NEBULOSA: una capa aparte, de ruido MÁS grande y MÁS suave (fbm de
// varios octavos, no una grilla con bordes duros como las estrellas),
// recortada con smoothstep para que salgan manchas orgánicas en vez
// de círculos — y con alpha bajo, para que sea un velo difuso, no un
// parche sólido.
Shader "HumanMess/Estrellas"
{
    Properties
    {
        [Header(Paleta de colores)]
        _ColorA ("Color A", Color) = (1, 1, 1, 1)
        _ColorB ("Color B", Color) = (0.7, 0.8, 1, 1)
        _ColorC ("Color C", Color) = (1, 0.85, 0.6, 1)
        _ColorD ("Color D", Color) = (1, 0.6, 0.55, 1)
        _ColorE ("Color E", Color) = (0.75, 0.6, 1, 1)

        [Header(Estrellas)]
        _Tiling ("Celdas (más alto = más estrellas, más chicas)", Float) = 24
        _Densidad ("Densidad (0-1, qué fracción de celdas tiene estrella)", Range(0, 1)) = 0.12
        _TamanoEstrella ("Tamaño del punto", Range(0.01, 0.4)) = 0.08
        _TwinkleVelocidad ("Velocidad del titileo", Float) = 2.0
        _TwinkleFuerza ("Cuánto titila (0 = fijo, 1 = parpadeo fuerte)", Range(0, 1)) = 0.6

        [Header(Manchas de nebulosa)]
        _ManchaEscala ("Escala (más bajo = manchas más grandes)", Float) = 2.0
        _ManchaUmbral ("Umbral (más alto = menos manchas)", Range(0, 1)) = 0.55
        _ManchaSuavidad ("Suavidad del borde", Range(0.01, 0.6)) = 0.25
        _ManchaOpacidad ("Opacidad máxima", Range(0, 1)) = 0.35

        _Seed ("Semilla (cambiala para otro patrón)", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        // Aditivo: tanto las estrellas como las manchas SUMAN luz sobre
        // lo que haya atrás, nunca tapan — así funciona bien sobre
        // cualquier fondo, incluso si se cruzan con el planeta.
        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorA, _ColorB, _ColorC, _ColorD, _ColorE;
                float _Tiling;
                float _Densidad;
                float _TamanoEstrella;
                float _TwinkleVelocidad;
                float _TwinkleFuerza;
                float _ManchaEscala;
                float _ManchaUmbral;
                float _ManchaSuavidad;
                float _ManchaOpacidad;
                float _Seed;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            // Hash determinístico: misma entrada -> siempre la misma salida.
            float hash21(float2 p)
            {
                p = frac(p * float2(127.1, 311.7));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }

            // Ruido suave por interpolación bilineal entre 4 esquinas con
            // hash — lo mismo que usamos para la textura del planeta, acá
            // adentro del shader. Da manchas orgánicas, no una grilla.
            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // 3 octavos: una base grande + dos capas de detalle encima,
            // para que las manchas no se vean como manchas perfectamente
            // redondas sino con bordes irregulares.
            float fbm(float2 p)
            {
                float v = 0.0;
                float amp = 0.55;
                [unroll]
                for (int i = 0; i < 3; i++)
                {
                    v += amp * noise(p);
                    p *= 2.03;
                    amp *= 0.5;
                }
                return v;
            }

            // Elige uno de los 5 colores de la paleta según un hash 0-1.
            half4 colorDePaleta(float h)
            {
                int idx = (int)floor(h * 5.0);
                if (idx <= 0) return _ColorA;
                if (idx == 1) return _ColorB;
                if (idx == 2) return _ColorC;
                if (idx == 3) return _ColorD;
                return _ColorE;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // --- estrellas ---
                float2 suv = IN.uv * _Tiling + _Seed;
                float2 celda = floor(suv);
                float2 local = frac(suv) - 0.5;

                float h = hash21(celda);
                float tieneEstrella = step(1.0 - _Densidad, h);

                float2 offset = float2(hash21(celda + 17.3), hash21(celda + 91.7)) - 0.5;
                float d = length(local - offset * 0.7);
                float punto = smoothstep(_TamanoEstrella, 0.0, d);

                float fase = h * 6.2831853;
                float titileo = lerp(1.0, 0.5 + 0.5 * sin(_Time.y * _TwinkleVelocidad + fase), _TwinkleFuerza);

                float brilloEstrella = punto * tieneEstrella * titileo;
                half4 colorEstrella = colorDePaleta(hash21(celda + 50.0));

                // --- manchas de nebulosa ---
                float2 nuv = IN.uv * _ManchaEscala + _Seed * 0.37 + 100.0;
                float n = fbm(nuv);
                float manchaAlpha = smoothstep(_ManchaUmbral, _ManchaUmbral + _ManchaSuavidad, n) * _ManchaOpacidad;

                // el color de la mancha se mezcla entre dos colores de la
                // paleta según OTRA capa de ruido (más grande todavía),
                // así una misma nube no es de un solo color parejo
                float mezcla = noise(nuv * 0.35 + 7.0);
                half4 colorMancha = lerp(colorDePaleta(mezcla), colorDePaleta(mezcla + 0.37), 0.5);

                // --- combinar ---
                half3 rgb = colorEstrella.rgb * brilloEstrella + colorMancha.rgb * manchaAlpha;
                float alpha = saturate(brilloEstrella + manchaAlpha);

                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
