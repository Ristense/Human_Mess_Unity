using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Corre la cámara hacia donde está el mouse, para poder mirar
/// alrededor de la nave sin alejarse.
///
/// El problema que resuelve: con la cámara cerca se ve lindo pero se
/// juega a ciegas, y alejarla para ver te deja la nave diminuta. Esto
/// deja mirar sin alejar: el mouse hace de "cabeza".
///
/// Se asoma solo un rato (ver <see cref="segundosAsomado"/>) y vuelve
/// sola aunque sigas con el mouse afuera. Es a propósito: la posición
/// de descanso de la cámara tiene que ser la nave, porque es lo que
/// estás piloteando. Si se quedara asomada para siempre, a la larga
/// jugarías descentrado sin darte cuenta.
///
/// Va en la misma cámara que SeguirJugador, que es quien le suma el
/// desplazamiento.
/// </summary>
public class AsomarCamara : MonoBehaviour
{
    [Tooltip("El zoom, para no asomarse cuando ya estás lejos. Vacío = " +
        "lo busca en esta cámara.")]
    [SerializeField] private ZoomCamara zoom;

    [Header("Cuánto")]
    [Tooltip("Cuánto se corre como máximo, en PANTALLAS. 0.5 = media " +
        "pantalla hacia cada lado con el mouse en el borde.")]
    [Min(0f)]
    [SerializeField] private float maximoEnPantallas = 0.5f;

    [Tooltip("Qué parte del centro de la pantalla no cuenta, de 0 a 1. " +
        "Sin esto la cámara nunca queda del todo quieta, porque el " +
        "mouse casi nunca está en el píxel exacto del centro.")]
    [Range(0f, 0.9f)]
    [SerializeField] private float zonaMuerta = 0.12f;

    [Header("Cuándo vuelve")]
    [Tooltip("Cuántos segundos aguanta asomada antes de volver sola a " +
        "la nave, aunque dejes el mouse afuera.")]
    [Min(0.1f)]
    [SerializeField] private float segundosAsomado = 5f;

    [Header("Qué tan lento")]
    [Tooltip("Segundos que tarda en llegar. Alto a propósito: el " +
        "desplazamiento tiene que ser lento, si no marea — la cámara " +
        "ya se mueve con la nave y dos movimientos rápidos a la vez " +
        "se leen como un tirón.")]
    [Min(0f)]
    [SerializeField] private float suavizado = 0.6f;

    [Header("Zoom")]
    [Tooltip("Desde qué fracción de alejamiento deja de asomarse, de 0 " +
        "a 1. 0.6 = con la cámara al 60% de lejos o más, no se asoma: " +
        "ahí ya ves los alrededores y el movimiento solo molestaría.")]
    [Range(0f, 1f)]
    [SerializeField] private float noAsomarDesde = 0.6f;

    private Camera _cam;
    private Vector2 _actual;
    private Vector2 _velocidad;
    private float _tiempoAfuera;
    private bool _bloqueado;

    /// <summary>
    /// Cuánto hay que correr la cámara, en unidades de mundo. Se lo
    /// suma SeguirJugador a su destino.
    /// </summary>
    public Vector2 Desplazamiento => _actual;

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        if (zoom == null)
        {
            zoom = GetComponent<ZoomCamara>();
        }
    }

    private void LateUpdate()
    {
        Vector2 objetivo = Objetivo();

        // SmoothDamp y no Lerp: frena solo al llegar y no overshootea,
        // que con un movimiento lento se notaría como un rebote.
        _actual = suavizado > 0f
            ? Vector2.SmoothDamp(_actual, objetivo, ref _velocidad, suavizado,
                Mathf.Infinity, Time.deltaTime)
            : objetivo;
    }

    private Vector2 Objetivo()
    {
        if (_cam == null || Mouse.current == null)
        {
            return Vector2.zero;
        }

        // Con la cámara lejos no se asoma. Vuelve al centro en vez de
        // quedarse clavada donde estaba al alejar.
        if (zoom != null && zoom.Fraccion >= noAsomarDesde)
        {
            _tiempoAfuera = 0f;
            _bloqueado = false;
            return Vector2.zero;
        }

        // Se mide contra el CENTRO de la pantalla y no contra la nave.
        // Parece lo mismo —con la cámara en reposo la nave está en el
        // centro— pero no lo es: medir contra la nave se realimenta,
        // porque al correrse la cámara la nave se va para el otro lado
        // de la pantalla, el mouse queda más lejos de ella, y la cámara
        // se corre todavía más. Se dispara sola hasta el tope.
        Vector2 media = new Vector2(Screen.width, Screen.height) * 0.5f;
        if (media.x <= 0f || media.y <= 0f)
        {
            return Vector2.zero;
        }

        Vector2 mouse = Mouse.current.position.ReadValue();
        Vector2 desde = new Vector2(
            (mouse.x - media.x) / media.x,
            (mouse.y - media.y) / media.y);

        float fuerza = Mathf.InverseLerp(zonaMuerta, 1f, desde.magnitude);
        bool afuera = fuerza > 0f;

        // El reloj corre solo mientras estás asomado. Al volver al
        // centro se rearma, así que podés volver a asomarte enseguida:
        // el límite es a cada vistazo, no un presupuesto que se gasta.
        if (afuera && !_bloqueado)
        {
            _tiempoAfuera += Time.deltaTime;
            if (_tiempoAfuera >= segundosAsomado)
            {
                _bloqueado = true;
            }
        }
        else if (!afuera)
        {
            _tiempoAfuera = 0f;
            _bloqueado = false;
        }

        if (_bloqueado || fuerza <= 0f)
        {
            return Vector2.zero;
        }

        // El tope se mide en pantallas, así que acompaña al zoom solo:
        // de cerca se asoma poco en unidades de mundo, de lejos más,
        // pero siempre lo mismo en lo que ves.
        float altoMundo = _cam.orthographic
            ? _cam.orthographicSize * 2f
            : 2f * Mathf.Abs(transform.position.z)
                * Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad);

        Vector2 tope = new Vector2(altoMundo * _cam.aspect, altoMundo) * maximoEnPantallas;
        Vector2 dir = Vector2.ClampMagnitude(desde, 1f) * fuerza;
        return Vector2.Scale(dir, tope);
    }
}
