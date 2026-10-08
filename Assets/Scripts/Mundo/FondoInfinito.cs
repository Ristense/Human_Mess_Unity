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

    [Tooltip("Cuánto se corre la textura por cada unidad que te " +
        "movés. 0 = el fondo queda clavado en pantalla, como pintado " +
        "en el vidrio. 1 = se mueve como si estuviera a tu distancia. " +
        "Para estrellas lejanas, 0.02-0.08. Dale un valor distinto a " +
        "cada capa: eso es lo que crea la sensación de profundidad.")]
    [SerializeField] private float factor = 0.05f;

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

        // El offset se calcula desde la posición ABSOLUTA y no
        // acumulando frame a frame: así no se va juntando error de
        // redondeo, y si te teletransportás el fondo queda coherente.
        Vector2 p = objetivo.position;
        _mat.mainTextureOffset = new Vector2(
            p.x * factor * correccion.x,
            p.y * factor * correccion.y);
    }
}
