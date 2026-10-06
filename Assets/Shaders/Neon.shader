// Neón para sprites.
//
// La idea es simple: el SpriteRenderer solo te deja elegir colores
// hasta blanco (1.0), y el Bloom necesita pixeles MÁS brillantes que
// eso para largar halo. Este shader multiplica el color por una
// intensidad libre, así que podés salirte del rango y recién ahí el
// Bloom lo agarra.
//
// Sin Bloom prendido en el Volume esto no hace nada visible.
Shader "HumanMess/Neon"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        [HDR] _Color ("Color", Color) = (1, 1, 1, 1)

        // Arriba de 1 es donde empieza la gracia. 3-6 es un neón
        // tranquilo, 10+ ya quema.
        _Intensidad ("Intensidad", Range(0, 20)) = 3

        // En 0 brilla el sprite entero parejo. En 1 solo brillan las
        // partes claras del dibujo y las oscuras quedan apagadas —
        // eso es lo que hace que parezca un tubo encendido y no una
        // calcomanía iluminada.
        _SoloClaros ("Solo las partes claras", Range(0, 1)) = 0
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
                float4 _MainTex_ST;
                half4  _Color;
                half   _Intensidad;
                half   _SoloClaros;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                // el color del SpriteRenderer llega por acá
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * IN.color;

                // luminancia con los pesos de siempre: el ojo ve mucho
                // más el verde que el azul
                half luz = dot(tex.rgb, half3(0.299, 0.587, 0.114));
                half mascara = lerp(1.0h, luz, _SoloClaros);

                half3 rgb = tex.rgb * _Color.rgb * _Intensidad * mascara;
                return half4(rgb, tex.a * _Color.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
