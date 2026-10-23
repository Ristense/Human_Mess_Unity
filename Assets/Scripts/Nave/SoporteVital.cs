using UnityEngine;

/// <summary>
/// Los tanques de hidrógeno y oxígeno de la nave, y lo que se gasta.
///
/// El hielo que entra se electroliza: sale 2 de hidrógeno por 1 de
/// oxígeno, que es la proporción del agua. Primero se llenan los
/// tanques; lo que sobra queda como hielo en la bodega, para vender o
/// para después.
///
/// Los dos tanques se llenan y se vacían en la misma proporción, así
/// que se acaban juntos. Es a propósito: tener medio tanque de uno y
/// nada del otro no agrega decisiones, solo obliga a mirar dos barras
/// en vez de una.
///
/// Va en la nave, junto a la Bodega — que es quien le pasa el hielo
/// antes de guardarlo.
/// </summary>
public class SoporteVital : MonoBehaviour
{
    [Header("Tanques")]
    [Tooltip("Cuántos kg entran en total entre los dos tanques.")]
    [Min(0.01f)]
    [SerializeField] private float capacidad = 60f;

    [Tooltip("Partes de hidrógeno por cada parte de oxígeno. 2 = la " +
        "proporción del agua.")]
    [Min(0.01f)]
    [SerializeField] private float partesHidrogeno = 2f;

    [Min(0.01f)]
    [SerializeField] private float partesOxigeno = 1f;

    [Header("Consumo")]
    [Tooltip("Cuántos MINUTOS dura el oxígeno lleno. Se gasta solo, " +
        "por respirar: el reloj corre aunque no hagas nada.")]
    [Min(0.1f)]
    [SerializeField] private float minutosOxigeno = 30f;

    [Tooltip("Cuántos MINUTOS de empuje CONTINUO da el hidrógeno " +
        "lleno. Este no corre solo: baja únicamente mientras acelerás " +
        "o frenás con los motores.\n\n" +
        "Esa es la diferencia que hace que valga la pena mirar las dos " +
        "barras: el oxígeno te apura, el hidrógeno te limita hasta " +
        "dónde podés ir. Y el hielo es uno solo, así que llenar un " +
        "tanque es decidir entre respirar más rato o volar más lejos.")]
    [Min(0.1f)]
    [SerializeField] private float minutosEmpuje = 8f;

    [Tooltip("Apagalo para que no se gaste. Para probar otras cosas " +
        "sin quedarte sin aire a los 30 minutos.")]
    [SerializeField] private bool consume = true;

    [Header("Arranque")]
    [Tooltip("Con cuánto arranca la partida, de 0 a 1. 1 = tanques " +
        "llenos.")]
    [Range(0f, 1f)]
    [SerializeField] private float alInicio = 1f;

    private float _kgH;
    private float _kgO;

    /// <summary>Cuánto queda de hidrógeno, de 0 a 1.</summary>
    public float Hidrogeno => CapacidadH > 0f ? _kgH / CapacidadH : 0f;

    /// <summary>Cuánto queda de oxígeno, de 0 a 1.</summary>
    public float Oxigeno => CapacidadO > 0f ? _kgO / CapacidadO : 0f;

    /// <summary>Lo que queda del que esté peor, de 0 a 1. Para el aviso.</summary>
    public float Reserva => Mathf.Min(Hidrogeno, Oxigeno);

    /// <summary>Minutos de aire que quedan.</summary>
    public float MinutosAire => Oxigeno * minutosOxigeno;

    /// <summary>Minutos de empuje continuo que quedan.</summary>
    public float MinutosEmpuje => Hidrogeno * minutosEmpuje;

    /// <summary>¿Queda hidrógeno para los motores?</summary>
    public bool HayCombustible => _kgH > 0f;

    /// <summary>¿Se acabó alguno de los dos?</summary>
    public bool Vacio => Reserva <= 0f;

    public float CapacidadH => capacidad * FraccionH;
    public float CapacidadO => capacidad * (1f - FraccionH);

    private float FraccionH
    {
        get
        {
            float total = partesHidrogeno + partesOxigeno;
            return total > 0f ? partesHidrogeno / total : 0.5f;
        }
    }

    private void Awake()
    {
        _kgH = CapacidadH * alInicio;
        _kgO = CapacidadO * alInicio;
    }

    /// <summary>
    /// Mete hielo a los tanques y devuelve los kg que NO entraron, para
    /// que la bodega los guarde como material.
    ///
    /// Se reparte por la proporción y se corta por el tanque que primero
    /// se llena: si el de oxígeno ya está al tope, el hidrógeno sobrante
    /// no tiene con qué respirarse, así que vuelve como hielo en vez de
    /// desaparecer.
    /// </summary>
    public float SumarHielo(float kg)
    {
        if (kg <= 0f)
        {
            return 0f;
        }

        float libreH = Mathf.Max(0f, CapacidadH - _kgH);
        float libreO = Mathf.Max(0f, CapacidadO - _kgO);

        // cuánto hielo haría falta para llenar cada uno, y se usa el
        // menor: más que eso desbordaría al otro
        float porH = FraccionH > 0f ? libreH / FraccionH : float.MaxValue;
        float porO = (1f - FraccionH) > 0f ? libreO / (1f - FraccionH) : float.MaxValue;

        float usado = Mathf.Min(kg, Mathf.Min(porH, porO));
        if (usado <= 0f)
        {
            return kg;
        }

        _kgH = Mathf.Min(CapacidadH, _kgH + usado * FraccionH);
        _kgO = Mathf.Min(CapacidadO, _kgO + usado * (1f - FraccionH));

        return kg - usado;
    }

    /// <summary>
    /// Gasta hidrógeno por <paramref name="dt"/> segundos de motor
    /// encendido. Devuelve false si el tanque está seco, para que la
    /// nave sepa que no puede empujar.
    ///
    /// Lo llama la nave, no se descuenta solo: el hidrógeno solo se va
    /// cuando de verdad se empuja.
    /// </summary>
    public bool GastarEmpuje(float dt)
    {
        if (!consume)
        {
            return true;
        }
        if (_kgH <= 0f || minutosEmpuje <= 0f)
        {
            return false;
        }

        _kgH = Mathf.Max(0f, _kgH - CapacidadH * (dt / (minutosEmpuje * 60f)));
        return true;
    }

    private void Update()
    {
        if (!consume || minutosOxigeno <= 0f)
        {
            return;
        }

        // Solo el oxígeno corre con el reloj. El hidrógeno lo descuenta
        // la nave al empujar, en GastarEmpuje.
        _kgO = Mathf.Max(0f,
            _kgO - CapacidadO * (Time.deltaTime / (minutosOxigeno * 60f)));
    }
}
