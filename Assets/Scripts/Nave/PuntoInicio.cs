using UnityEngine;

/// <summary>
/// Pone la nave en la base al empezar la partida.
///
/// Dos formas de decirle dónde: le arrastrás el objeto de la base (lo
/// más directo, y si después la movés en la escena el spawn la sigue
/// sola), o si lo dejás vacío cae al centro del sector que elijas del
/// Mapa.
///
/// Va en el Player, junto al Rigidbody2D.
/// </summary>
public class PuntoInicio : MonoBehaviour
{
    [Tooltip("La base. Si está puesto, manda esto y se ignora el Mapa.")]
    [SerializeField] private Transform punto;

    [Header("Si no hay punto: centro de un sector")]
    [SerializeField] private Mapa mapa;

    [Tooltip("Qué sector del 3x3. (0,-1) es el de abajo al medio.")]
    [SerializeField] private Vector2Int sector = new Vector2Int(0, -1);

    [Header("Opciones")]
    [Tooltip("Que la nave arranque mirando como mira la base.")]
    [SerializeField] private bool copiarRotacion = false;

    [Tooltip("La cámara, para que arranque ya encima de la base en vez " +
        "de venir volando desde donde la dejaste en el editor.")]
    [SerializeField] private SeguirJugador camara;

    private void Start()
    {
        Aparecer();
    }

    /// <summary>
    /// Teletransporta la nave al punto de inicio y la deja quieta.
    /// Público a propósito: lo mismo sirve para respawnear después.
    /// </summary>
    public void Aparecer()
    {
        Vector3 destino;
        if (punto != null)
        {
            destino = punto.position;
        }
        else if (mapa != null)
        {
            destino = mapa.SectorCenterWorld(sector);
        }
        else
        {
            Debug.LogWarning("PuntoInicio: no tengo ni Punto ni Mapa, no muevo nada.", this);
            return;
        }

        // la Z se respeta: en 2D define en qué capa estás dibujado y no
        // tiene nada que ver con dónde aparecés
        destino.z = transform.position.z;

        if (copiarRotacion && punto != null)
        {
            transform.rotation = punto.rotation;
        }

        transform.position = destino;

        // Mover el Transform de un cuerpo físico no le avisa a la
        // física: el cuerpo se entera recién en el próximo paso y, si
        // traía velocidad, sale disparado desde la posición nueva. Por
        // eso se le setea la posición al Rigidbody y se le limpia todo
        // lo que traía.
        if (TryGetComponent(out Rigidbody2D rb))
        {
            rb.position = destino;
            rb.rotation = transform.eulerAngles.z;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        if (camara != null)
        {
            camara.Centrar();
        }
    }
}
