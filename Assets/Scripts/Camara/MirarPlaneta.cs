using UnityEngine;

/// <summary>
/// La cámara apunta siempre al planeta, sin importar hacia dónde la
/// empuje SeguirJugador. Con cámara en Perspective esto SÍ se nota
/// (a diferencia de Ortográfica, donde la dirección no cambia el
/// encuadre) — por eso esto solo tiene sentido ahora que la pasaste a
/// Perspective.
///
/// Va en la Main Camera, junto a SeguirJugador y ZoomCamara.
/// </summary>
public class MirarPlaneta : MonoBehaviour
{
    [SerializeField] private Transform planeta;

    [Tooltip("Qué tan rápido la cámara gira hasta apuntar al planeta. " +
        "Más alto = reacciona al toque; más bajo = gira de a poco, " +
        "más cinematográfico.")]
    [SerializeField] private float velocidadGiro = 6f;

    private void LateUpdate()
    {
        // LateUpdate, después de que SeguirJugador ya movió la cámara
        // este frame — si no, apuntaríamos con la posición VIEJA y
        // quedaría medio frame atrasado. El orden entre dos LateUpdate
        // de scripts distintos no está 100% garantizado por Unity,
        // pero con todo lo demás ya suavizado (el propio follow, el
        // zoom), un desfasaje de 1 frame acá no se nota.
        if (planeta == null)
        {
            return;
        }

        Vector3 direccion = planeta.position - transform.position;
        if (direccion.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Quaternion objetivo = Quaternion.LookRotation(direccion, Vector3.up);
        float t = 1f - Mathf.Exp(-velocidadGiro * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, objetivo, t);
    }
}
