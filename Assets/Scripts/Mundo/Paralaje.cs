using UnityEngine;

/// <summary>
/// Parallax de fondo: el objeto se mueve un poquito cuando vos te
/// movés, no nada y no igual — eso es lo que hace que se sienta
/// lejísimos, como un planeta, en vez de pegado a la pantalla (factor
/// 0) o flotando junto con vos como cualquier otra cosa del mundo
/// (factor 1).
///
/// Va en el planeta (o cualquier otro elemento de fondo: estrellas,
/// nebulosas). Arrastrá la nave o la cámara en "Objetivo".
/// </summary>
public class Paralaje : MonoBehaviour
{
    [Tooltip("A quién le copiamos el movimiento — normalmente la nave " +
        "o la Main Camera.")]
    [SerializeField] private Transform objetivo;

    [Tooltip("0 = clavado en el mundo, no lo sigue nada. 1 = se mueve " +
        "igual que el objetivo, como si no estuviera lejos. Para un " +
        "planeta de fondo, algo como 0.05-0.2 da la sensación de " +
        "distancia real.")]
    [Range(0f, 1f)]
    [SerializeField] private float factor = 0.12f;

    private Vector3 _inicioPropio;
    private Vector3 _inicioObjetivo;

    private void Start()
    {
        _inicioPropio = transform.position;
        if (objetivo != null)
        {
            _inicioObjetivo = objetivo.position;
        }
    }

    private void LateUpdate()
    {
        // LateUpdate: corre DESPUÉS de que la nave y la cámara ya se
        // movieron este frame (SeguirJugador también usa LateUpdate),
        // así el planeta reacciona al movimiento de ESTE frame y no
        // al del anterior.
        if (objetivo == null)
        {
            return;
        }

        Vector3 recorrido = objetivo.position - _inicioObjetivo;
        transform.position = _inicioPropio + recorrido * factor;
    }
}
