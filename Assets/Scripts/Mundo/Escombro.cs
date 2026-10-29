using UnityEngine;

/// <summary>
/// Un pedacito de material suelto, de los que escupe el taladro. Lleva
/// cuántos kg vale: es exactamente lo que se le sacó a la roca, así que
/// lo que entra a la bodega es lo que salió del asteroide, ni más ni
/// menos.
///
/// Al nacer da un "pop": entra chiquito, se pasa de su tamaño y se
/// acomoda. Es para que se note que salió algo — a la escala a la que
/// se trabaja, un escombro que aparece ya en su tamaño final se pierde
/// entre el polvo y las chispas.
///
/// Va en el prefab del escombro, junto a un Rigidbody2D.
/// </summary>
public class Escombro : MonoBehaviour
{
    [Tooltip("Cuántos kg suma a la bodega al tragarlo. Lo pone el " +
        "taladro al crearlo.")]
    public float kg = 1f;

    [Tooltip("De qué es. Lo pone el taladro, según la roca de donde salió.")]
    public TipoMaterial material = TipoMaterial.Roca;

    [Header("Pop al nacer")]
    [Tooltip("Cuánto dura, en segundos. 0 = nace en su tamaño, sin pop.")]
    [Min(0f)]
    [SerializeField] private float segundosPop = 0.18f;

    [Tooltip("De qué tamaño arranca, como fracción del final.")]
    [Range(0f, 1f)]
    [SerializeField] private float escalaInicial = 0.3f;

    [Tooltip("Cuánto se pasa de su tamaño antes de acomodarse. 1 = no " +
        "se pasa y solo crece; 1.35 le da el golpecito que lo hace " +
        "notar.")]
    [Min(1f)]
    [SerializeField] private float sobrepaso = 1.35f;

    [Tooltip("Qué parte del tiempo usa en crecer. El resto lo usa en " +
        "volver. Menos de la mitad entra rápido y se asienta lento, " +
        "que es lo que se lee como golpe.")]
    [Range(0.05f, 0.95f)]
    [SerializeField] private float parteSubida = 0.4f;

    private Vector3 _escalaFinal;
    private float _t;

    private void Start()
    {
        // En Start y no en Awake: el taladro le pone la escala JUSTO
        // después de instanciarlo, y Awake corre adentro del Instantiate
        // — o sea antes. Acá ya está el tamaño de verdad, que es el que
        // hay que recordar para volver a él.
        _escalaFinal = transform.localScale;

        if (segundosPop <= 0f)
        {
            enabled = false;
            return;
        }
        transform.localScale = _escalaFinal * escalaInicial;
    }

    private void Update()
    {
        _t += Time.deltaTime;
        float avance = _t / segundosPop;

        if (avance >= 1f)
        {
            transform.localScale = _escalaFinal;
            // se apaga solo: el pop pasa una vez y puede haber muchos
            // escombros a la vez. El componente apagado se sigue
            // encontrando con GetComponent, que es lo único que le
            // pide el taladro.
            enabled = false;
            return;
        }

        float f = avance < parteSubida
            ? Mathf.SmoothStep(escalaInicial, sobrepaso, avance / parteSubida)
            : Mathf.SmoothStep(sobrepaso, 1f,
                (avance - parteSubida) / (1f - parteSubida));

        transform.localScale = _escalaFinal * f;
    }
}
