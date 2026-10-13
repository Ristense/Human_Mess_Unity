using UnityEngine;

/// <summary>
/// Cuánto pesa la nave en total, y se lo aplica a la física.
///
///   peso = casco + módulos montados + material en la bodega
///
/// Hay que recorrer los hijos porque un módulo montado tiene el
/// Rigidbody2D con simulated = false: dejó de ser un cuerpo aparte, así
/// que su masa NO se suma sola a la de la nave. Si no la sumáramos acá,
/// montar diez módulos no te haría ni un gramo más pesado.
///
/// Con "Aplicar A Fisica" el resultado va directo al Rigidbody2D: cargar
/// material te hace acelerar y frenar peor, por F = m·a, sin tocar nada
/// en el script de la nave.
///
/// Va en el Player.
/// </summary>
public class PesoNave : MonoBehaviour
{
    [SerializeField] private Rigidbody2D nave;
    [SerializeField] private Bodega bodega;

    [Tooltip("Que el peso total se le asigne a la masa del Rigidbody2D. " +
        "Apagalo si solo lo querés mostrar en el HUD.")]
    [SerializeField] private bool aplicarAFisica = true;

    [Tooltip("Cada cuántos frames se recalcula. El peso no cambia 60 " +
        "veces por segundo.")]
    [SerializeField] private int framesPorTick = 10;

    // La masa del casco solo, tomada al arrancar. Hace falta guardarla
    // porque, si aplicamos el total a rb.mass, después ya no hay forma
    // de saber cuánto era la nave pelada.
    private float _masaCasco;
    private float _peso;
    private int _tick;

    /// <summary>Kg totales: casco + módulos + material.</summary>
    public float Peso => _peso;

    /// <summary>Kg de la nave pelada, sin nada encima.</summary>
    public float MasaCasco => _masaCasco;

    private void Awake()
    {
        if (nave == null)
        {
            nave = GetComponent<Rigidbody2D>();
        }
        if (bodega == null)
        {
            bodega = GetComponent<Bodega>();
        }
        _masaCasco = nave != null ? nave.mass : 0f;
        Recalcular();
    }

    private void FixedUpdate()
    {
        // FixedUpdate y no Update: si va a tocar rb.mass, que lo haga
        // al ritmo de la física y no en medio de un paso.
        _tick++;
        if (_tick < framesPorTick)
        {
            return;
        }
        _tick = 0;
        Recalcular();
    }

    public void Recalcular()
    {
        _peso = _masaCasco;

        foreach (Rigidbody2D rb in GetComponentsInChildren<Rigidbody2D>(true))
        {
            if (rb != nave)
            {
                _peso += rb.mass;
            }
        }

        if (bodega != null)
        {
            _peso += bodega.Carga;
        }

        if (aplicarAFisica && nave != null && _peso > 0f)
        {
            nave.mass = _peso;
        }
    }
}
