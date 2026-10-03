using UnityEngine;

/// <summary>
/// Gira el planeta sobre su propio eje, despacio — un planeta de
/// fondo se nota apenas si se mueve, si lo ves girar rápido se ve de
/// juguete.
///
/// Va en el objeto que tiene la malla del planeta (el MeshRenderer).
/// </summary>
public class GirarPlaneta : MonoBehaviour
{
    [Tooltip("Cuánto tarda una vuelta entera, en segundos.")]
    [SerializeField] private float segundosPorVuelta = 180f;

    [Tooltip("Inclinación del eje, en grados. Girando perfectamente " +
        "derecho se ve de juguete — la Tierra está a 23°.")]
    [SerializeField] private float inclinacion = 23f;

    private void Awake()
    {
        // La inclinación se aplica UNA vez al eje Z local, y después
        // se gira siempre alrededor del eje Y YA inclinado — por eso
        // el giro de abajo usa Space.Self (local), no Space.World:
        // respeta la inclinación en vez de pelearse con ella.
        transform.Rotate(Vector3.forward, inclinacion, Space.Self);
    }

    private void Update()
    {
        if (segundosPorVuelta <= 0f)
        {
            return;
        }
        float gradosPorSegundo = 360f / segundosPorVuelta;
        transform.Rotate(Vector3.up, gradosPorSegundo * Time.deltaTime, Space.Self);
    }
}
