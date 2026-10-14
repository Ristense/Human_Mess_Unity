using UnityEngine;

/// <summary>
/// Pega un elemento del Canvas a un objeto del MUNDO: cada frame lo
/// planta en la posición de pantalla de ese objeto, más un corrimiento
/// en píxeles. Para avisos y medidores que van al lado de la mano.
///
/// Es lo mismo que hace HudMano con el arco, pero suelto: así cualquier
/// cosa del HUD puede seguir a algo sin depender de otro indicador (el
/// arco se esconde cuando está vacío, y si la E fuera hija suya se
/// escondería con él).
///
/// Va en el elemento del Canvas que tiene que seguir.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SeguirEnPantalla : MonoBehaviour
{
    [Tooltip("A quién sigue. Normalmente la Mano.")]
    [SerializeField] private Transform objetivo;

    [Tooltip("La cámara del juego. Vacío = Camera.main.")]
    [SerializeField] private Camera camara;

    [Tooltip("Corrimiento en PÍXELES de pantalla respecto al objetivo. " +
        "(-50, 40) = arriba a la izquierda. Ajustalo con el juego " +
        "corriendo, mirando dónde queda.")]
    [SerializeField] private Vector2 desplazamiento = new Vector2(-50f, 40f);

    private RectTransform _rect;
    private Canvas _canvas;
    private RectTransform _canvasRect;

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
        if (objetivo == null || camara == null || _canvas == null)
        {
            return;
        }

        Vector2 pantalla = (Vector2)camara.WorldToScreenPoint(objetivo.position) + desplazamiento;

        if (_canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            // en Overlay el "mundo" del Canvas ES la pantalla
            _rect.position = pantalla;
            return;
        }

        // en los otros modos hay que traducir de píxeles a coordenadas
        // del Canvas, que dependen de su escala y su pivot
        Camera refCam = _canvas.renderMode == RenderMode.ScreenSpaceCamera
            ? _canvas.worldCamera
            : null;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRect, pantalla, refCam, out Vector2 local))
        {
            // localPosition es relativa al PADRE: si la E no cuelga
            // directo del Canvas, hay que pasar de un espacio al otro
            Vector3 mundo = _canvasRect.TransformPoint(local);
            _rect.position = new Vector3(mundo.x, mundo.y, _rect.position.z);
        }
    }
}
