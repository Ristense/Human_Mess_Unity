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

    private void Awake()
    {
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
    public float SumarCarga(float kg)
    {
        float entra = Mathf.Min(kg, Libre);
        carga += entra;
        return kg - entra;
    }

    /// <summary>
    /// Vacía la bodega y la cobra. Devuelve cuántos kg se vendieron.
    /// </summary>
    public float Vender(float precioPorKg)
    {
        float vendido = carga;
        creditos += vendido * precioPorKg;
        carga = 0f;
        return vendido;
    }
}
