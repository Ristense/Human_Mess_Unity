using UnityEngine;

/// <summary>
/// Cualquier cosa que se pueda montar en una Zona: una herramienta, un
/// módulo de carga, lo que sea. Es solo una etiqueta con un tipo —
/// Zona.AceptaA() compara este string contra el suyo para decidir si
/// te deja entrar.
/// </summary>
public class Modulo : MonoBehaviour
{
    [Tooltip("Tiene que matchear el \"Acepta\" de la Zona para poder " +
        "montarse ahí. Ej: \"modulo\", \"herramienta\".")]
    public string tipo = "modulo";

    [Header("Tamaño (en celdas de la Zona)")]
    [Tooltip("Cuántas celdas ocupa de ancho al montarse.")]
    [Min(1)]
    public int cols = 1;

    [Tooltip("Cuántas celdas ocupa de alto al montarse.")]
    [Min(1)]
    public int rows = 1;

    /// <summary>La Zona donde estás montado ahora, o null si estás suelto.</summary>
    public Zona zonaActual { get; private set; }

    public void SetZona(Zona z)
    {
        zonaActual = z;
    }
}
