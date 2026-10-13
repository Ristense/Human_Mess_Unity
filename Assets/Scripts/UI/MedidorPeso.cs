using UnityEngine;

/// <summary>
/// Conecta la bodega con una BarraProgreso: la barra sube a medida que
/// cargás material. Es el cable entre las dos cosas y nada más — la
/// barra no sabe de bodegas y la bodega no sabe de barras.
///
/// Va en el módulo de expansión, junto a la barra.
/// </summary>
public class MedidorPeso : MonoBehaviour
{
    [Tooltip("De dónde sale la carga. Vacío = lo busca solo hacia " +
        "arriba en la jerarquía, así al montar el módulo en la nave se " +
        "engancha sin configurar nada.")]
    [SerializeField] private Bodega bodega;

    [SerializeField] private BarraProgreso barra;

    [Tooltip("Vaciar la barra cuando el módulo no está montado. Si no, " +
        "se queda congelada en el último valor y parece que mide algo.")]
    [SerializeField] private bool vaciarSuelto = true;

    private void Update()
    {
        if (barra == null)
        {
            return;
        }

        // Se rebusca cada frame a propósito: el módulo cambia de padre
        // al montarse y desmontarse, así que la referencia no es fija.
        Bodega fuente = bodega != null ? bodega : GetComponentInParent<Bodega>();

        if (fuente == null)
        {
            if (vaciarSuelto)
            {
                barra.Valor = 0f;
            }
            return;
        }

        barra.Valor = fuente.Fraccion;
    }
}
