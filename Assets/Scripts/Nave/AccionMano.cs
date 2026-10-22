using System;
using UnityEngine;

/// <summary>
/// Una opción de la rueda de la mano: cómo se llama y qué hace si la
/// elegís.
///
/// Es un struct y no una clase porque la lista se rearma cada vez que
/// se abre la rueda. Son dos o tres por vez, pero pasa muchas veces
/// por partida y no tiene sentido darle trabajo al GC por esto.
///
/// El ORDEN de la lista importa: la primera es la que hace el toque
/// corto de la tecla, sin abrir nada. Por eso se arma siempre dejando
/// adelante lo que la mano venía haciendo sola — así el gesto de
/// siempre sigue dando el mismo resultado y la rueda solo agrega
/// opciones, no cambia costumbres.
/// </summary>
public readonly struct AccionMano
{
    /// <summary>Lo que se lee en la pastilla. Corto, una o dos palabras.</summary>
    public readonly string nombre;

    /// <summary>Renglón chico debajo del nombre, para desambiguar. Puede ir vacío.</summary>
    public readonly string detalle;

    /// <summary>Qué pasa si la elegís.</summary>
    public readonly Action hacer;

    public AccionMano(string nombre, string detalle, Action hacer)
    {
        this.nombre = nombre;
        this.detalle = detalle;
        this.hacer = hacer;
    }
}

/// <summary>
/// La geometría de la rueda, en un solo lugar.
///
/// La usan los dos lados: la Mano para saber qué sector estás
/// eligiendo con el mouse, y RuedaMano para dibujar las pastillas. Si
/// cada uno tuviera su propia cuenta, bastaría con que alguien cambiara
/// un signo para que lo que se ve y lo que se elige dejaran de
/// coincidir — y ese bug es horrible de encontrar, porque todo
/// "funciona", solo que elegís otra cosa.
/// </summary>
public static class Rueda
{
    /// <summary>
    /// Dónde va el sector i, en grados de pantalla (0 = derecha, 90 =
    /// arriba). Arranca arriba y sigue en el sentido del reloj, que es
    /// como se lee un menú de estos.
    /// </summary>
    public static float AnguloGrados(int i, int total)
    {
        if (total <= 0)
        {
            return 90f;
        }
        return 90f - 360f * i / total;
    }

    /// <summary>
    /// Qué sector estás apuntando con <paramref name="dir"/> (el vector
    /// del centro de la rueda al mouse, en píxeles de pantalla).
    ///
    /// Se elige por ángulo más cercano en vez de dividir el círculo en
    /// tajadas: sale la misma respuesta y no hay que andar normalizando
    /// a 0-360 ni cuidando el salto de 359 a 0, que es donde se cuelan
    /// los errores de borde.
    /// </summary>
    public static int IndiceDesde(Vector2 dir, int total)
    {
        if (total <= 0)
        {
            return -1;
        }

        float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        int mejor = 0;
        float mejorDelta = float.MaxValue;
        for (int i = 0; i < total; i++)
        {
            float delta = Mathf.Abs(Mathf.DeltaAngle(ang, AnguloGrados(i, total)));
            if (delta < mejorDelta)
            {
                mejorDelta = delta;
                mejor = i;
            }
        }
        return mejor;
    }
}
