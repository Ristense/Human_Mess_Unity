using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Mueve la placa bajo el soldador. El soldador NO se mueve: se queda
/// clavado en un punto de la ventana y lo que se pasea es el metal,
/// como si miraras por un visor que recorre la plancha.
///
/// Se hace moviendo la placa de verdad y no con un shader a propósito:
/// así el juego sabe en todo momento qué pedazo de metal tiene abajo
/// del soldador, que es lo que después decide dónde se corta.
///
/// Va en la placa (el objeto con PlacaCorte), que tiene que ser hija del
/// marco y más grande que él. El marco necesita un Rect Mask 2D para que
/// lo que sobra no se vea.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class PlacaMovil : MonoBehaviour
{
    [Tooltip("El marco de la ventana. Vacío = el padre.")]
    [SerializeField] private RectTransform marco;

    [Tooltip("Dónde está clavado el soldador, adentro del marco. " +
        "Vacío = el centro del marco.")]
    [SerializeField] private RectTransform soldador;

    [Tooltip("La cámara del Canvas. Vacío = Camera.main.")]
    [SerializeField] private Camera camara;

    [Header("Movimiento")]
    [Tooltip("Cuánto se mueve la placa por cada píxel que movés el " +
        "mouse. 1 = la placa se corre lo mismo que el mouse.")]
    [SerializeField] private float sensibilidad = 1f;

    [Tooltip("Mover el mouse a la derecha muestra lo que hay a la " +
        "derecha, o sea que la placa se va para la izquierda. Apagalo " +
        "si preferís arrastrar la placa como si la empujaras.")]
    [SerializeField] private bool comoMirar = true;

    [Tooltip("Qué tan rápido llega, en segundos. 0 = clavado al mouse.")]
    [SerializeField] private float suavizado = 0.05f;

    [Header("Giro")]
    [Tooltip("Cuánto se ladea la placa, en grados, al correrla de lado " +
        "a lado. 0 = no gira.")]
    [SerializeField] private float giroMaximo = 8f;

    [Header("Hasta dónde llega")]
    [Tooltip("Cuánto puede pasarse el soldador del borde de la placa, " +
        "en píxeles. 0 = justo hasta el borde. Un poco de más deja " +
        "cortar cómodo en las esquinas.")]
    [SerializeField] private float margen = 20f;

    private RectTransform _rect;
    private Vector2 _velocidad;
    private float _velocidadGiro;
    private float _giro;

    /// <summary>
    /// Dónde está el soldador, en coordenadas locales de la PLACA. Es el
    /// punto que se está por cortar.
    /// </summary>
    public Vector2 PuntoSoldador
    {
        get
        {
            // Resuelto acá y no solo en Awake: los gizmos y cualquier
            // cosa del editor leen esto SIN que haya corrido Awake, y
            // ahí _rect todavía es null.
            if (_rect == null)
            {
                _rect = GetComponent<RectTransform>();
            }

            Vector3 mundo = soldador != null
                ? soldador.position
                : (marco != null ? marco.position : _rect.position);
            return _rect.InverseTransformPoint(mundo);
        }
    }

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
        if (marco == null)
        {
            marco = transform.parent as RectTransform;
        }
        if (camara == null)
        {
            camara = Camera.main;
        }
    }

    private void LateUpdate()
    {
        if (marco == null || Mouse.current == null)
        {
            return;
        }

        // De dónde está el mouse respecto al centro del marco, en
        // coordenadas del marco: así anda igual con cualquier resolución
        // y con el Canvas escalado.
        Camera refCam = CamaraDelCanvas();
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                marco, Mouse.current.position.ReadValue(), refCam, out Vector2 local))
        {
            return;
        }

        float signo = comoMirar ? -1f : 1f;
        Vector2 destino = local * (sensibilidad * signo);

        // el giro sale de cuán corrida está la placa de lado: moverse a
        // un costado la ladea, como si el soldador la rodeara
        Rect m = marco.rect;
        float mitad = Mathf.Max(1f, m.width * 0.5f);
        float giroDestino = Mathf.Clamp(-destino.x / mitad, -1f, 1f) * giroMaximo;

        _giro = suavizado > 0f
            ? Mathf.SmoothDamp(_giro, giroDestino, ref _velocidadGiro, suavizado)
            : giroDestino;
        _rect.localRotation = Quaternion.Euler(0f, 0f, _giro);

        destino = Limitar(destino);
        _rect.anchoredPosition = suavizado > 0f
            ? Vector2.SmoothDamp(_rect.anchoredPosition, destino, ref _velocidad, suavizado)
            : destino;
    }

    /// <summary>
    /// El tope no es que la placa llene el marco, sino que el SOLDADOR
    /// no se salga de la placa. Es lo que deja llegar a los bordes y a
    /// las esquinas: con el soldador arriba del marco, exigir que la
    /// placa lo llenara dejaba media plancha fuera de alcance.
    /// </summary>
    private Vector2 Limitar(Vector2 destino)
    {
        Rect p = _rect.rect;
        Vector2 s = PuntoEnMarco();

        // dónde cae el soldador adentro de la placa, con la placa ladeada
        Quaternion giro = Quaternion.Euler(0f, 0f, _giro);
        Vector2 dentro = Quaternion.Inverse(giro) * (s - destino);

        dentro.x = Mathf.Clamp(dentro.x, p.xMin - margen, p.xMax + margen);
        dentro.y = Mathf.Clamp(dentro.y, p.yMin - margen, p.yMax + margen);

        return s - (Vector2)(giro * dentro);
    }

    /// <summary>Dónde está clavado el soldador, en coordenadas del marco.</summary>
    private Vector2 PuntoEnMarco()
    {
        if (soldador == null)
        {
            return Vector2.zero;   // el centro del marco
        }
        return marco.InverseTransformPoint(soldador.position);
    }

    private Camera CamaraDelCanvas()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }
        return canvas.worldCamera != null ? canvas.worldCamera : camara;
    }
}
