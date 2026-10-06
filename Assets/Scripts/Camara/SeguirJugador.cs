using UnityEngine;

/// <summary>
/// La cámara persigue al jugador, pero con un retraso suave en vez de
/// quedar pegada exacto a su posición. SmoothDamp (no Lerp) porque
/// tiene un tiempo de llegada CONSTANTE sin importar qué tan rápido se
/// mueva el objetivo, y no overshootea — Lerp sí puede pasarse de
/// largo y rebotar si el jugador frena en seco.
///
/// Va en la Main Camera. Arrastrá al jugador en "Objetivo" desde el
/// Inspector.
/// </summary>
public class SeguirJugador : MonoBehaviour
{
    [Tooltip("El Transform que la cámara persigue.")]
    [SerializeField] private Transform objetivo;

    [Tooltip("Desplazamiento respecto al objetivo. En 2D dejá Z en " +
        "-10 (o lo que uses) para que la cámara quede atrás y vea la " +
        "escena.")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f);

    [Tooltip("Qué tan atrás queda la cámara. Más alto = más delay y " +
        "más \"elástico\"; más bajo = más pegada al jugador. " +
        "0 sería instantáneo.")]
    [SerializeField] private float tiempoSuavizado = 0.15f;

    [Tooltip("Tope de velocidad del movimiento de la cámara, para que " +
        "un teletransporte del jugador no la mande volando de un tirón.")]
    [SerializeField] private float velocidadMaxima = Mathf.Infinity;

    // SmoothDamp necesita guardar la velocidad ENTRE frames para
    // calcular la curva de frenado — por eso es un campo y no una
    // variable local.
    private Vector3 _velocidad = Vector3.zero;

    /// <summary>
    /// Se planta encima del objetivo de una, sin suavizado. Para cuando
    /// el jugador se teletransporta (spawn, respawn): si no, la cámara
    /// cree que el objetivo "se movió" y te hace todo el viaje a la
    /// vista.
    ///
    /// También limpia la velocidad acumulada — si no, SmoothDamp la
    /// arrastra y se pasa de largo en el primer frame.
    /// </summary>
    public void Centrar()
    {
        if (objetivo == null)
        {
            return;
        }
        transform.position = objetivo.position + offset;
        _velocidad = Vector3.zero;
    }

    private void LateUpdate()
    {
        // LateUpdate, no Update: tiene que correr DESPUÉS de que el
        // jugador ya se movió este frame. Si fuera en Update, el orden
        // de ejecución entre scripts no está garantizado y la cámara
        // podría leer la posición VIEJA del jugador, generando un
        // tembleque de un frame.
        if (objetivo == null)
        {
            return;
        }

        Vector3 destino = objetivo.position + offset;
        transform.position = Vector3.SmoothDamp(
            transform.position, destino, ref _velocidad,
            tiempoSuavizado, velocidadMaxima, Time.deltaTime);
    }
}
