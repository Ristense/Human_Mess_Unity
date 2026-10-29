using UnityEngine;

/// <summary>
/// Fondo que no se termina nunca. El truco: el plano va clavado a la
/// cámara, así que nunca se sale de cuadro, y el parallax se hace
/// corriendo la TEXTURA en vez del objeto.
///
/// Es distinto del script Paralaje, que mueve el objeto: ese sirve
/// para algo puntual y lejano (un planeta), pero un fondo que tiene
/// que cubrir siempre la pantalla se te termina corriendo de la vista
/// por más chico que sea el factor.
///
/// Requisito: la textura tiene que estar en Wrap Mode = Repeat, y el
/// objeto tiene que ser un Quad con MeshRenderer (un SpriteRenderer
/// no respeta el offset de textura).
///
/// Va en cada capa del fondo. Dale distinto Factor a cada una y ahí
/// aparece la profundidad.
/// </summary>
public class FondoInfinito : MonoBehaviour
{
    [Tooltip("A quién sigue. Normalmente la cámara del juego.")]
    [SerializeField] private Transform objetivo;

    [Tooltip("Qué fracción de una vuelta recorre la textura por cada " +
        "Unidades por Vuelta. 0 = queda clavado. Para estrellas " +
        "lejanas, 0.02-0.08.")]
    [SerializeField] private float factor = 0.05f;

    [Tooltip("Cuántas unidades del mundo hacen una vuelta completa " +
        "cuando Factor vale 1. Alto = estrellas más lentas.")]
    [Min(0.01f)]
    [SerializeField] private float unidadesPorVuelta = 1000f;

    [Tooltip("Si tu textura se ve estirada en un eje, acá lo " +
        "compensás para que el desplazamiento no vaya más rápido en " +
        "horizontal que en vertical.")]
    [SerializeField] private Vector2 correccion = Vector2.one;

    [Tooltip("Pegar el plano a la cámara. Dejalo prendido salvo que " +
        "ya lo tengas como hijo de la cámara, ahí es al pedo.")]
    [SerializeField] private bool seguirObjetivo = true;

    private Renderer _render;
    private Material _mat;
    private Vector3 _desfase;
    private Vector3 _inicioObjetivo;
    private Vector2 _offsetInicial;

    private void Start()
    {
        _render = GetComponent<Renderer>();
        if (_render != null)
        {
            // .material y no .sharedMaterial: sharedMaterial es EL
            // asset en disco, y moverle el offset te lo deja
            // modificado para siempre, incluso al salir del Play.
            _mat = _render.material;
        }

        if (objetivo != null)
        {
            // guardamos cómo lo acomodaste respecto a la cámara para
            // respetarlo, sobre todo la Z, que define el orden
            _desfase = transform.position - objetivo.position;
            _inicioObjetivo = objetivo.position;
            _offsetInicial = _mat.mainTextureOffset;
        }
    }

    private void LateUpdate()
    {
        if (objetivo == null || _mat == null)
        {
            return;
        }

        if (seguirObjetivo)
        {
            transform.position = objetivo.position + _desfase;
        }

        // Se mide desde el inicio, no con la posición absoluta del
        // mundo: así el tamaño de los sectores no acelera la textura.
        Vector3 recorrido = objetivo.position - _inicioObjetivo;
        float divisor = Mathf.Max(unidadesPorVuelta, 0.01f);
        _mat.mainTextureOffset = _offsetInicial + new Vector2(
            recorrido.x * factor * correccion.x / divisor,
            recorrido.y * factor * correccion.y / divisor);
    }
}
