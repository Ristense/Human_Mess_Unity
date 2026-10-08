using UnityEngine;

/// <summary>
/// Ajusta la masa del Rigidbody2D según lo grande que haya salido la
/// pieza. Para los asteroides, que se spawnean con escala al azar: si
/// todos pesan igual, uno enorme se te mueve como una piedrita y se
/// nota feo.
///
/// La física de verdad dice masa ∝ área, o sea escala². Pero eso en un
/// juego es brutal: un asteroide del doble de ancho pesaría 4 veces
/// más y no lo moverías nunca. Por eso "Influencia" mezcla entre la
/// masa del prefab y la masa física real — con 0.15 tenés la pista
/// visual de que el grande pesa más, sin que se vuelva injugable.
///
/// Va en el prefab del asteroide, junto al Rigidbody2D.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class MasaPorTamano : MonoBehaviour
{
    [Tooltip("Qué escala se considera \"normal\". Un objeto con esta " +
        "escala conserva exactamente la masa del prefab.")]
    [SerializeField] private float escalaReferencia = 1f;

    [Tooltip("Cuánto se aplica la física real. 0 = todos pesan igual " +
        "que el prefab. 1 = masa proporcional al área, que es lo " +
        "correcto pero exagerado para jugar. 0.1-0.2 es la zona linda.")]
    [Range(0f, 1f)]
    [SerializeField] private float influencia = 0.15f;

    [Tooltip("Topes, para que ni el más chico quede de papel ni el " +
        "más grande sea inamovible.")]
    [SerializeField] private float masaMinima = 0.1f;
    [SerializeField] private float masaMaxima = 10000f;

    private void Start()
    {
        // Start y no Awake: quien lo spawnea suele setear la escala
        // DESPUÉS de instanciar, y en Awake todavía leeríamos la del
        // prefab.
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb == null || escalaReferencia <= 0f)
        {
            return;
        }

        Vector3 e = transform.lossyScale;
        // promedio de los dos ejes: así una pieza estirada no cuenta
        // solo por su lado largo
        float escala = (Mathf.Abs(e.x) + Mathf.Abs(e.y)) * 0.5f / escalaReferencia;

        // en 2D el "volumen" es área, así que va al cuadrado
        float fisica = escala * escala;

        float factor = Mathf.Lerp(1f, fisica, influencia);
        rb.mass = Mathf.Clamp(rb.mass * factor, masaMinima, masaMaxima);
    }
}
