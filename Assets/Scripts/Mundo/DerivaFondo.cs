using UnityEngine;

/// <summary>
/// Acerca y aleja el objeto muy de a poco, en loop — ese vaivén
/// lentísimo que le da vida a un fondo que si no queda demasiado
/// quieto. Pensado para las capas de estrellas.
///
/// Si le ponés distinta velocidad a cada capa, nunca se sincronizan
/// entre ellas y el efecto se siente mucho más orgánico.
/// </summary>
public class DerivaFondo : MonoBehaviour
{
    [Tooltip("Cuánto se mueve respecto a su posición inicial, en " +
        "unidades. Para un fondo lejano con escala grande, puede ser " +
        "un número alto y aún así verse sutil.")]
    [SerializeField] private float amplitud = 200f;

    [Tooltip("Segundos que tarda un ciclo completo (ir y volver). " +
        "Bien alto = apenas perceptible, que es la idea.")]
    [SerializeField] private float segundosPorCiclo = 40f;

    [Tooltip("En qué eje se mueve. (0,0,1) = acercarse/alejarse.")]
    [SerializeField] private Vector3 eje = new Vector3(0f, 0f, 1f);

    [Tooltip("Desfase inicial (0-1). Ponele distinto a cada capa para " +
        "que no se muevan todas al mismo tiempo.")]
    [Range(0f, 1f)]
    [SerializeField] private float desfase = 0f;

    private Vector3 _inicio;

    private void Start()
    {
        _inicio = transform.position;
    }

    private void Update()
    {
        if (segundosPorCiclo <= 0f)
        {
            return;
        }
        float t = (Time.time / segundosPorCiclo + desfase) * Mathf.PI * 2f;
        transform.position = _inicio + eje.normalized * (Mathf.Sin(t) * amplitud);
    }
}
