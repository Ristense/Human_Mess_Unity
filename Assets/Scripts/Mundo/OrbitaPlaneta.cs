using UnityEngine;

/// <summary>
/// Hace que un planeta orbite alrededor de un centro (el Sol, u otro
/// planeta si querés una luna) — complementa a GirarPlaneta, que es
/// el giro SOBRE SU PROPIO EJE. Esto es el movimiento ALREDEDOR de
/// otra cosa. Van separados a propósito: un planeta gira sobre sí y
/// orbita al Sol con velocidades totalmente independientes.
/// </summary>
public class OrbitaPlaneta : MonoBehaviour
{
    [Tooltip("Alrededor de qué orbita. El Sol, normalmente.")]
    [SerializeField] private Transform centro;

    [Tooltip("Radio de la órbita. Si lo dejás en 0, usa la distancia " +
        "actual al centro al arrancar — así no hace falta calcularla " +
        "a mano, solo ubicás el planeta donde querés en el editor.")]
    [SerializeField] private float radio = 0f;

    [Tooltip("Segundos para una vuelta completa alrededor del centro.")]
    [SerializeField] private float segundosPorOrbita = 600f;

    [Tooltip("Inclinación del plano orbital, en grados. 0 = todos los " +
        "planetas en el mismo plano (prolijo); un par de grados " +
        "distintos por planeta se ve más natural.")]
    [SerializeField] private float inclinacionOrbita = 0f;

    private float _angulo;

    private void Start()
    {
        if (centro == null)
        {
            enabled = false;
            return;
        }

        if (radio <= 0f)
        {
            radio = Vector3.Distance(transform.position, centro.position);
        }

        // arranca en el ángulo que le corresponda a la posición actual,
        // no siempre desde cero — así no "salta" al primer frame
        Vector3 haciaAfuera = transform.position - centro.position;
        _angulo = Mathf.Atan2(haciaAfuera.z, haciaAfuera.x);
    }

    private void Update()
    {
        if (centro == null || segundosPorOrbita <= 0f)
        {
            return;
        }

        _angulo += (Mathf.PI * 2f / segundosPorOrbita) * Time.deltaTime;

        float inclRad = inclinacionOrbita * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(
            Mathf.Cos(_angulo) * radio,
            Mathf.Sin(_angulo) * radio * Mathf.Sin(inclRad),
            Mathf.Sin(_angulo) * radio * Mathf.Cos(inclRad));

        transform.position = centro.position + offset;
    }
}
