using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Indicador de la mano en el Canvas: lo planta encima de la mano y le
/// pasa el progreso de la tecla.
///
/// Va en Screen Space - Overlay en vez de World Space porque el
/// proyecto trabaja a una escala chiquita (1 unidad ≈ media nave): un
/// Canvas en World Space ahí necesita escalas tipo 0.0005 y se ve
/// borroso. Acá la UI vive en píxeles de pantalla, nítida, y lo único
/// que viaja del mundo a la pantalla es la posición.
///
/// Va en el objeto raíz del indicador, adentro del Canvas.
/// </summary>
public class HudMano : MonoBehaviour
{
    [Header("Qué sigue")]
    [Tooltip("De dónde sale el progreso.")]
    [SerializeField] private Mano mano;

    [Tooltip("El objeto a seguir — normalmente la Mano.")]
    [SerializeField] private Transform objetivo;

    [Tooltip("La cámara del juego. Vacío = Camera.main.")]
    [SerializeField] private Camera camara;

    [Tooltip("Corrimiento en PÍXELES de pantalla. (0, 60) lo pone " +
        "arriba de la mano sin taparla.")]
    [SerializeField] private Vector2 desplazamiento = new Vector2(0f, 60f);

    [Header("Qué muestra")]
    [Tooltip("Para un aro: Image con Image Type = Filled y Fill " +
        "Method = Radial 360. El Slider de Unity no hace radial.")]
    [SerializeField] private Image relleno;

    [Tooltip("Alternativa si preferís una barra común.")]
    [SerializeField] private Slider barra;

    [SerializeField] private float suavizado = 0.06f;
    [SerializeField] private bool esconderEnReposo = true;

    private RectTransform _rect;
    private Canvas _canvas;
    private RectTransform _canvasRect;
    private float _mostrado;
    private float _velocidad;
    private bool _visible = true;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
        _canvas = GetComponentInParent<Canvas>();
        if (_canvas != null)
        {
            _canvasRect = _canvas.rootCanvas.GetComponent<RectTransform>();
        }
        if (camara == null)
        {
            camara = Camera.main;
        }
    }

    private void LateUpdate()
    {
        if (mano == null || camara == null)
        {
            return;
        }

        float valor = mano.Progreso;
        _mostrado = suavizado > 0f
            ? Mathf.SmoothDamp(_mostrado, valor, ref _velocidad, suavizado)
            : valor;

        if (esconderEnReposo)
        {
            bool visible = _mostrado > 0.001f;
            if (_visible != visible)
            {
                _visible = visible;
                // Se apagan los HIJOS, nunca este objeto: apagarse a
                // sí mismo frenaría el LateUpdate y no habría quién lo
                // volviera a prender.
                foreach (Transform h in transform)
                {
                    h.gameObject.SetActive(visible);
                }
            }
            if (!visible)
            {
                return;
            }
        }

        if (relleno != null)
        {
            relleno.fillAmount = _mostrado;
        }
        if (barra != null)
        {
            barra.value = _mostrado;
        }

        Seguir();
    }

    private void Seguir()
    {
        if (objetivo == null || _canvas == null)
        {
            return;
        }

        Vector2 pantalla = camara.WorldToScreenPoint(objetivo.position);
        pantalla += desplazamiento;

        if (_canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            // En Overlay, el "mundo" del Canvas ES la pantalla: se le
            // puede asignar la posición en píxeles tal cual.
            _rect.position = pantalla;
            return;
        }

        // En los otros modos hay que traducir de píxeles a coordenadas
        // del Canvas, que dependen de su escala y su pivot.
        if (_canvasRect == null)
        {
            return;
        }
        Camera refCam = _canvas.renderMode == RenderMode.ScreenSpaceCamera
            ? _canvas.worldCamera
            : null;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect, pantalla, refCam, out Vector2 local))
        {
            _rect.localPosition = local;
        }
    }
}
