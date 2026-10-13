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

    [Tooltip("Que el indicador se plante encima del objetivo. " +
        "Apagalo y se queda exactamente donde lo dejaste en el " +
        "editor — útil si lo querés fijo en una esquina.")]
    [SerializeField] private bool seguirObjetivo = true;

    [Tooltip("El objeto a seguir — normalmente la Mano.")]
    [SerializeField] private Transform objetivo;

    [Tooltip("La cámara del juego. Vacío = Camera.main.")]
    [SerializeField] private Camera camara;

    [Tooltip("Corrimiento fijo en PÍXELES de pantalla, sin importar " +
        "hacia dónde mire la mano.")]
    [SerializeField] private Vector2 desplazamiento = new Vector2(0f, 60f);

    [Tooltip("Que el indicador gire junto con la mano.")]
    [SerializeField] private bool rotarConObjetivo = true;

    [Tooltip("Grados de corrección, para alinear la barra como te " +
        "guste respecto a la mano. Si queda cruzada, probá 90 o -90.")]
    [SerializeField] private float offsetRotacion = 0f;

    [Tooltip("Cuántos píxeles se aleja en el sentido CONTRARIO al que " +
        "apunta la mano. Así la barra queda siempre del lado de " +
        "atrás y no te tapa aquello a lo que estás apuntando.")]
    [SerializeField] private float separacionOpuesta = 60f;

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
        if (!seguirObjetivo || objetivo == null || _canvas == null)
        {
            return;
        }

        // El objeto de la mano ya viene rotado por el BrazoIK hacia
        // donde apunta el antebrazo, así que su Z nos sirve tal cual.
        float grados = objetivo.eulerAngles.z + offsetRotacion;

        if (rotarConObjetivo)
        {
            _rect.localRotation = Quaternion.Euler(0f, 0f, grados);
        }

        Vector2 pantalla = camara.WorldToScreenPoint(objetivo.position);
        pantalla += desplazamiento;

        if (separacionOpuesta != 0f)
        {
            // el MENOS es lo que lo manda para el lado contrario al
            // que mira la mano
            float rad = grados * Mathf.Deg2Rad;
            pantalla -= new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * separacionOpuesta;
        }

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
