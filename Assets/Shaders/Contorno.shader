// Contorno de un sprite. Dibuja SOLO el borde de afuera: adentro del
// sprite y lejos de él queda transparente.
//
// Por eso va en un renderer aparte, encima del original, y el original
// conserva su material de siempre — con sus Light2D y todo. Si este
// shader reemplazara al material del sprite, al resaltarlo se apagaría
// la iluminación.
//
// Cómo encuentra el borde: para cada píxel mira a sus vecinos a
// "Grosor" píxeles de PANTALLA de distancia — no de textura: con
// sprites de alta resolución, unos pocos píxeles de textura son menos
// de un píxel en pantalla y el contorno no se ve.
//
// Si él es transparente pero algún vecino no, está justo afuera del
// dibujo: eso es contorno.
Shader "HumanMess/Contorno"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        [HDR] _Color ("Color", Color) = (1, 1, 1, 1)
        _Grosor ("Grosor (pixeles de pantalla)", Range(0, 12)) = 2
        _Margen ("Margen (pixeles de pantalla)", Range(0, 20)) = 0

        // Los llena el script ResaltarBajoMano, por objeto. Ver frag.
        [HideInInspector] _RectUV ("Rect UV del sprite", Vector) = (0, 0, 1, 1)
        [HideInInspector] _Expansion ("Expansion", Float) = 1
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
                half4 _Color;
                float _Grosor;
                float _Margen;
                float4 _RectUV;
                float _Expansion;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            half Alfa(float2 uv)
            {
                // Fuera del rectángulo del sprite no hay nada. Hay que
                // cortarlo a mano: si el sprite viene de una hoja con
                // varios, leer afuera agarraría pedazos del vecino.
                float2 t = (uv - _RectUV.xy) / _RectUV.zw;
                float dentro = step(0, t.x) * step(t.x, 1) * step(0, t.y) * step(t.y, 1);
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a * dentro;
            }

            // "Engorda" el dibujo r píxeles: devuelve 1 si hay algo
            // opaco a esa distancia o menos. Mira 8 direcciones a radio
            // r y a la mitad de r — con un solo radio, una parte fina
            // del dibujo (una antena) podía caer entre dos muestras y
            // quedarse sin contorno.
            half Engordar(float2 uv, float r)
            {
                // fwidth da cuánto cambia la UV de un píxel de PANTALLA al
                // siguiente. Multiplicando por eso, "r" queda en píxeles de
                // pantalla: el contorno mide lo mismo con cualquier
                // resolución de textura y a cualquier zoom.
                float2 d = fwidth(uv) * r;
                // las diagonales se acortan para que el borde salga
                // parejo y no más gordo en las esquinas
                float2 dd = d * 0.7071;
                half m = 0;
                [unroll]
                for (int i = 1; i <= 2; i++)
                {
                    float k = i * 0.5;
                    m = max(m, Alfa(uv + float2( d.x,  0) * k));
                    m = max(m, Alfa(uv + float2(-d.x,  0) * k));
                    m = max(m, Alfa(uv + float2( 0,  d.y) * k));
                    m = max(m, Alfa(uv + float2( 0, -d.y) * k));
                    m = max(m, Alfa(uv + float2( dd.x,  dd.y) * k));
                    m = max(m, Alfa(uv + float2(-dd.x,  dd.y) * k));
                    m = max(m, Alfa(uv + float2( dd.x, -dd.y) * k));
                    m = max(m, Alfa(uv + float2(-dd.x, -dd.y) * k));
                }
                return m;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // El script agranda este renderer "_Expansion" veces para
                // que haya lugar afuera del dibujo. Acá se hace la cuenta
                // inversa: se achica la imagen dentro del rectángulo en
                // la misma proporción, alrededor del centro. Las dos
                // cosas se cancelan — el dibujo queda del tamaño real,
                // pero ahora con aire alrededor para el anillo.
                float2 t = (IN.uv - _RectUV.xy) / _RectUV.zw;
                t = (t - 0.5) * _Expansion + 0.5;
                float2 uv = _RectUV.xy + t * _RectUV.zw;

                // El contorno es un anillo: lo que queda entre engordar
                // el dibujo "margen + grosor" y engordarlo solo "margen".
                // Restar el de adentro es lo que deja el hueco
                // transparente entre el sprite y la línea.
                half afuera = Engordar(uv, _Margen + _Grosor);
                half adentro = max(Alfa(uv),
                                   _Margen > 0 ? Engordar(uv, _Margen) : 0);

                half borde = saturate(afuera - adentro);

                half4 c = _Color * IN.color;
                c.a *= borde;
                return c;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
