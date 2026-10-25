using UnityEngine;

/// <summary>
/// Conecta la bodega con una BarraProgreso: la barra sube a medida que
/// cargás material. Es el cable entre las dos cosas y nada más — la
/// barra no sabe de bodegas y la bodega no sabe de barras.
///
/// Además pinta la barra y el glow del módulo según qué tan cargado
/// vas. El glow es lo que importa: la barra hay que mirarla, el color
/// del halo se ve de reojo mientras pilotás.
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

    [Header("Color")]
    [Tooltip("El color según la carga: a la izquierda vacío, a la " +
        "derecha lleno.")]
    [SerializeField] private Gradient colorPorCarga = PorDefecto();

    [Tooltip("Pintar también el relleno de la barra. Para que se vea " +
        "bien el sprite del relleno tiene que ser BLANCO, como los " +
        "glows: el color se multiplica, y un sprite rojo pintado de " +
        "verde da negro.")]
    [SerializeField] private bool pintarBarra = true;

    [Tooltip("El glow del módulo. Se pinta todo lo que cuelgue: " +
        "sprites y Light2D. Vacío = solo la barra.")]
    [SerializeField] private Transform luz;

    private SpriteRenderer _rellenoBarra;

    // verde con lugar de sobra, amarillo a mitad de camino, rojo lleno
    private static Gradient PorDefecto()
    {
        var g = new Gradient();
        g.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.47f, 0.71f, 0.54f), 0f),
                new GradientColorKey(new Color(0.95f, 0.78f, 0.25f), 0.6f),
                new GradientColorKey(new Color(0.85f, 0.31f, 0.25f), 1f),
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f),
            });
        return g;
    }

    private void Awake()
    {
        if (barra != null && barra.Relleno != null)
        {
            _rellenoBarra = barra.Relleno.GetComponent<SpriteRenderer>();
        }
    }

    private void Update()
    {
        // Se rebusca cada frame a propósito: el módulo cambia de padre
        // al montarse y desmontarse, así que la referencia no es fija.
        Bodega fuente = bodega != null ? bodega : GetComponentInParent<Bodega>();

        float carga;
        if (fuente != null)
        {
            carga = fuente.Fraccion;
        }
        else if (vaciarSuelto)
        {
            carga = 0f;
        }
        else
        {
            return;
        }

        if (barra != null)
        {
            barra.Valor = carga;
        }

        // Se pinta con la carga REAL y no con lo que muestra la barra
        // (que va suavizada): el aviso tiene que llegar al toque.
        Color c = colorPorCarga.Evaluate(Mathf.Clamp01(carga));
        if (pintarBarra && _rellenoBarra != null)
        {
            _rellenoBarra.color = c;
        }
        if (luz != null)
        {
            Pintura.Pintar(luz, c);
        }
    }
}
