using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Pinta una luz entera de un color: todos los SpriteRenderer y las
/// Light2D que cuelguen de un objeto, a cualquier profundidad.
///
/// Va recursivo porque una luz de verdad casi nunca es un solo objeto:
/// es el neón, más el halo, más la Light2D que ilumina alrededor. Todos
/// tienen que cambiar juntos o se ve mal.
///
/// En el sprite se cambia el .color y no el material: el material es
/// compartido, y tocarlo pintaría todas las luces que lo usan.
/// </summary>
public static class Pintura
{
    public static void Pintar(Transform t, Color c)
    {
        if (t == null)
        {
            return;
        }
        if (t.TryGetComponent(out SpriteRenderer sr))
        {
            sr.color = c;
        }
        if (t.TryGetComponent(out Light2D l2d))
        {
            l2d.color = c;
        }

        for (int i = 0; i < t.childCount; i++)
        {
            Pintar(t.GetChild(i), c);
        }
    }
}
