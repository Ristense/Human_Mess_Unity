// Aro de progreso, dibujado por cálculo — no lleva textura.
//
// Se pone en un Quad (o un sprite cuadrado) y la UV se usa como
// coordenada polar: el radio dice si el pixel cae dentro del aro, y el
// ángulo dice si ya se llenó esa parte.
//
// Al ser procedural se ve nítido a cualquier tamaño y no hay que
// dibujar 60 imágenes para los 60 pasos de relleno.
Shader "HumanMess/RadialProgreso"
{
    Properties
    {
        [HDR] _Color ("Color lleno", Color) = (0.4, 0.9, 1, 1)
        _ColorFondo ("Color vacio", Color) = (1, 1, 1, 0.15)

        _Progreso ("Progreso", Range(0, 1)) = 0.35

        [Header(Forma)]
        _Radio ("Radio", Range(0, 0.5)) = 0.38
        _Grosor ("Grosor", Range(0.002, 0.5)) = 0.07
        _Suavidad ("Suavidad del borde", Range(0, 0.1)) = 0.01

        [Header(Orientacion)]
        _AnguloInicio ("Angulo de arranque", Range(0, 360)) = 90
        _Sentido ("Sentido (1 horario, -1 antihorario)", Float) = 1
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

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _ColorFondo;
                float _Progreso;
                float _Radio;
                float _Grosor;
                float _Suavidad;
                float _AnguloInicio;
                float _Sentido;
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
                // centro del quad como origen: la UV va de 0 a 1, así
                // que restando 0.5 queda de -0.5 a 0.5
                float2 p = IN.uv - 0.5;

                // --- ¿el pixel cae en el aro? ---
                float r = length(p);
                float dist = abs(r - _Radio);
                float anillo = 1.0 - smoothstep(_Grosor * 0.5,
                                                _Grosor * 0.5 + _Suavidad,
                                                dist);
                if (anillo <= 0.0)
                {
                    discard;
                }

                // --- ¿esa parte del aro ya se llenó? ---
                // atan2 da -180..180; lo giramos al ángulo de arranque
                // y lo normalizamos a 0..1 dando la vuelta completa.
                float grados = degrees(atan2(p.y, p.x));
                float t = frac((_AnguloInicio - grados * _Sentido) / 360.0);

                // el smoothstep en vez de step es para que el filo del
                // relleno no quede dentado
                float lleno = 1.0 - smoothstep(_Progreso, _Progreso + 0.004, t);

                half4 c = lerp(_ColorFondo, _Color, lleno);
                c.a *= anillo * IN.color.a;
                c.rgb *= IN.color.rgb;
                return c;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
