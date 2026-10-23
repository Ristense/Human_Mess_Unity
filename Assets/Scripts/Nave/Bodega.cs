using UnityEngine;

/// <summary>
/// La bodega: cuánto material llevás, cuánto te entra, y los créditos.
///
/// Todo en KILOS. La capacidad es la de la nave pelada más lo que le
/// sumen los módulos de expansión que tenga montados — los busca entre
/// sus hijos, así que enchufar y desenchufar un módulo ya cambia el
/// tope sin avisarle a nadie.
///
/// OJO: esto mide solo el MATERIAL. Lo que pesa la nave misma y sus
/// módulos no ocupa lugar en la bodega; eso lo suma PesoNave para la
/// física.
///
/// Va en el Player.
/// </summary>
public class Bodega : MonoBehaviour
{
    [Tooltip("Cuántos kg de material entran en la nave sin módulos de " +
        "expansión.")]
    [SerializeField] private float capacidadBase = 1000f;

    [Tooltip("Cuántos kg llevás ahora. No lo toques a mano en runtime, " +
        "usá SumarCarga().")]
    [SerializeField] private float carga = 0f;

    [SerializeField] private float creditos = 0f;

    [Tooltip("Kg de hielo. Ya está incluido en la Carga.")]
    [SerializeField] private float kgHielo = 0f;

    [Tooltip("Kg de restos de metal. Ya está incluido en la Carga.")]
    [SerializeField] private float kgRestos = 0f;

    [Header("Placas de metal")]
    [Tooltip("Cuántas placas llevás. Usá SumarPlaca().")]
    [SerializeField] private int placas = 0;

    [Tooltip("Cuánto pesan entre todas. Ya está incluido en la Carga.")]
    [SerializeField] private float kgPlacas = 0f;

    [Tooltip("Los tanques de H y O. El hielo que entra se electroliza " +
        "ahí primero y solo lo que sobra se guarda como material.\n\n" +
        "Vacío = lo busca en la nave. Si no hay ninguno, el hielo se " +
        "guarda entero, como antes.")]
    [SerializeField] private SoporteVital soporte;

    [Tooltip("Cada cuántos frames se vuelven a buscar las expansiones. " +
        "Recorrer la jerarquía no es gratis y no cambia tan seguido.")]
    [SerializeField] private int framesPorTick = 10;

    private float _capacidad;
    private int _tick;

    /// <summary>Kg de material que llevás.</summary>
    public float Carga => carga;

    /// <summary>Kg que entran en total, con las expansiones sumadas.</summary>
    public float Capacidad => _capacidad;

    /// <summary>Kg que todavía entran.</summary>
    public float Libre => Mathf.Max(0f, _capacidad - carga);

    /// <summary>De 0 a 1, para la barra.</summary>
    public float Fraccion => _capacidad > 0f ? carga / _capacidad : 0f;

    public bool Llena => carga >= _capacidad;
    public float Creditos => creditos;

    /// <summary>Kg de hielo. Ya está sumado en Carga.</summary>
    public float KgHielo => kgHielo;

    /// <summary>Kg de restos de metal. Ya está sumado en Carga.</summary>
    public float KgRestos => kgRestos;

    /// <summary>Cuántas placas de metal llevás.</summary>
    public int Placas => placas;

    /// <summary>Kg de las placas. Ya está sumado en Carga.</summary>
    public float KgPlacas => kgPlacas;

    private void Awake()
    {
        if (soporte == null)
        {
            soporte = GetComponentInParent<SoporteVital>();
        }
        RecalcularCapacidad();
    }

    private void Update()
    {
        _tick++;
        if (_tick < framesPorTick)
        {
            return;
        }
        _tick = 0;
        RecalcularCapacidad();
    }

    public void RecalcularCapacidad()
    {
        _capacidad = capacidadBase;
        // true para contar las expansiones aunque estén desactivadas:
        // apagar el sprite de un módulo no le saca el volumen
        foreach (ExpansionPeso e in GetComponentsInChildren<ExpansionPeso>(true))
        {
            _capacidad += e.bonus;
        }
    }

    /// <summary>
    /// Intenta cargar `kg`. Entra lo que quepa y devuelve lo que
    /// SOBRÓ — así quien taladra sabe si dejar el resto flotando.
    /// </summary>
    public float SumarCarga(float kg, TipoMaterial tipo = TipoMaterial.Roca)
    {
        // El hielo primero va a los tanques y recién lo que sobra se
        // guarda. Se hace acá y no en quien lo recoge para que valga
        // igual venga del taladro, de la mano o de donde sea: nadie
        // tiene que acordarse de llamar al soporte vital.
        if (tipo == TipoMaterial.Hielo && soporte != null)
        {
            kg = soporte.SumarHielo(kg);
            if (kg <= 0f)
            {
                return 0f;
            }
        }

        float entra = Mathf.Min(kg, Libre);
        carga += entra;
        if (tipo == TipoMaterial.Hielo)
        {
            kgHielo += entra;
        }
        else if (tipo == TipoMaterial.Restos)
        {
            kgRestos += entra;
        }
        return kg - entra;
    }

    /// <summary>
    /// Guarda una placa de `kg`. Una placa no se parte: si no entra
    /// entera, no entra, y devuelve false.
    /// </summary>
    public bool SumarPlaca(float kg)
    {
        if (kg > Libre)
        {
            return false;
        }
        placas++;
        kgPlacas += kg;
        carga += kg;
        return true;
    }

    /// <summary>
    /// Vende el material suelto (no las placas) y lo cobra. Devuelve
    /// cuántos kg se vendieron.
    /// </summary>
    public float Vender(float precioPorKg)
    {
        float vendido = carga - kgPlacas;
        creditos += vendido * precioPorKg;
        carga = kgPlacas;
        kgHielo = 0f;
        kgRestos = 0f;
        return vendido;
    }
}
