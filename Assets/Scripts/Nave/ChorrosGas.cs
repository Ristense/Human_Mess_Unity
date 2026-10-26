using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Los chorritos de gas de maniobra (el "pssst" de los propulsores
/// reales). Cada grupo prende sus ParticleSystem mientras ese propulsor
/// esté empujando y los apaga cuando para. El motor principal no pasa
/// por acá: ya tiene lo suyo.
///
/// Lee lo que la nave HACE (Nave.Giro y Nave.Costado), no las teclas.
/// Con los amortiguadores prendidos la nave se corrige sola, y esos
/// chorros tienen que verse igual que si los disparara el jugador: es
/// la única forma de entender por qué la nave se está acomodando sin
/// que toques nada. Y sin hidrógeno no sale nada, porque no empuja.
///
/// Cada emisor es un ParticleSystem hijo de la nave, puesto en el
/// costado de donde tiene que SALIR el gas, mirando hacia afuera. El
/// gas sale al lado contrario del empuje: para girar a la derecha,
/// el chorro sale de la izquierda.
/// </summary>
public class ChorrosGas : MonoBehaviour
{
    [Tooltip("De dónde sale lo que hacen los propulsores. Vacío = la " +
        "busca hacia arriba en la jerarquía.")]
    [SerializeField] private Nave nave;

    [Header("Giro (A / D)")]
    [SerializeField] private ParticleSystem[] giroIzquierda;
    [SerializeField] private ParticleSystem[] giroDerecha;

    [Header("Costado (Q / E)")]
    [SerializeField] private ParticleSystem[] costadoQ;
    [SerializeField] private ParticleSystem[] costadoE;

    [Tooltip("Desde qué fracción de empuje se prende un chorro, de 0 a " +
        "1. Las correcciones finas del amortiguador piden muy poquito " +
        "en el último paso; sin este piso, el chorro parpadearía con " +
        "cada una.")]
    [Range(0f, 1f)]
    [SerializeField] private float umbral = 0.05f;

    private readonly HashSet<ParticleSystem> _quieren = new HashSet<ParticleSystem>();
    private readonly List<ParticleSystem> _todos = new List<ParticleSystem>();

    private void Awake()
    {
        if (nave == null)
        {
            nave = GetComponentInParent<Nave>();
        }
    }

    private void Update()
    {
        float giro = nave != null ? nave.Giro : 0f;
        float costado = nave != null ? nave.Costado : 0f;

        // Un mismo sistema puede estar en varias listas (el chorro de la
        // esquina sirve para girar y para el costado). Por eso primero se
        // junta todo lo que ALGUNA lista pide y recién después se
        // prende o apaga: si cada lista decidiera por su cuenta, una lo
        // apagaría y otra lo prendería en el mismo frame.
        _quieren.Clear();
        _todos.Clear();
        Juntar(giroIzquierda, giro < -umbral);
        Juntar(giroDerecha, giro > umbral);
        Juntar(costadoQ, costado < -umbral);
        Juntar(costadoE, costado > umbral);

        foreach (ParticleSystem ps in _todos)
        {
            Poner(ps, _quieren.Contains(ps));
        }
    }

    private void Juntar(ParticleSystem[] sistemas, bool activo)
    {
        if (sistemas == null)
        {
            return;
        }
        foreach (ParticleSystem ps in sistemas)
        {
            if (ps == null)
            {
                continue;
            }
            _todos.Add(ps);
            if (activo)
            {
                _quieren.Add(ps);
            }
        }
    }

    // Se prende y apaga la EMISIÓN, no el objeto: así las partículas ya
    // nacidas terminan su vida y el chorro se desvanece en vez de
    // cortarse de golpe.
    private static void Poner(ParticleSystem ps, bool activo)
    {
        if (activo && !ps.isEmitting)
        {
            ps.Play();
        }
        else if (!activo && ps.isEmitting)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}
