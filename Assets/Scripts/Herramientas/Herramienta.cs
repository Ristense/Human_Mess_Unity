using UnityEngine;

/// <summary>
/// Lo que tienen en común todas las herramientas: una PUNTA, y saber si
/// están calzadas en la mano.
///
/// La punta es lo que trabaja (donde muerde el taladro, de donde sale el
/// perno) y también lo que no puede atravesar las cosas: la Mano le
/// avisa al brazo cuánto sobresale, y el brazo frena antes para que la
/// punta quede en el borde. Igual que la mano sola.
///
/// Va junto al Modulo de la herramienta, en el mismo objeto. Las
/// herramientas concretas (el taladro) heredan de esta.
/// </summary>
public class Herramienta : MonoBehaviour
{
    [Tooltip("La punta de la herramienta. Un hijo vacío puesto justo " +
        "donde termina el dibujo.")]
    [SerializeField] protected Transform punta;

    public Transform Punta => punta;

    /// <summary>
    /// Medio ancho de la cabeza de la herramienta. El brazo frena con este
    /// grosor y no con una línea: así la herramienta no se puede colar en
    /// un agujero más angosto que ella. 0 = se comporta como la mano sola.
    /// </summary>
    public virtual float Grosor => 0f;

    /// <summary>
    /// ¿Se está usando AHORA? (el taladro picando, no solo calzado).
    ///
    /// Mientras algo está en uso, la mano no ofrece agarrar: no se
    /// resalta lo que hay debajo ni se avisa el clic. Si no, picando una
    /// roca te aparece su contorno diciéndote que la podés agarrar, y un
    /// clic te deja colgado de lo mismo que estás cortando.
    ///
    /// Por defecto false: una herramienta que no la pisa nunca estorba.
    /// </summary>
    public virtual bool EnUso => false;

    /// <summary>La Mano que la tiene calzada, o null si no está equipada.</summary>
    protected Mano Usuario { get; private set; }

    /// <summary>¿Está calzada en la mano?</summary>
    public bool EnLaMano => Usuario != null;

    protected virtual void Awake()
    {
        Usuario = GetComponentInParent<Mano>();
    }

    /// <summary>
    /// Unity avisa acá cuando la herramienta cambia de padre: al
    /// equiparla, al guardarla en el rack o al soltarla. Una herramienta
    /// está en la mano si y solo si cuelga del objeto Mano.
    /// </summary>
    protected virtual void OnTransformParentChanged()
    {
        Usuario = GetComponentInParent<Mano>();
    }

    /// <summary>
    /// Dónde está la punta para el brazo (cuánto sobresale la
    /// herramienta). Una herramienta cuya punta se va de viaje (el perno
    /// del anclaje) devuelve acá dónde estaría guardada.
    /// </summary>
    public virtual Vector2 PosicionPunta => punta != null ? (Vector2)punta.position : (Vector2)transform.position;

    /// <summary>
    /// Hacia dónde apunta. En la mano, hacia donde mira la mano (la línea
    /// al mouse): así no depende de dónde quedó el pivote del dibujo, que
    /// puede estar corrido del eje del cañón. Suelta, de la herramienta a
    /// su punta.
    /// </summary>
    protected Vector2 Direccion()
    {
        if (Usuario != null)
        {
            Vector2 mira = Usuario.Mira;
            if (mira.sqrMagnitude > 0.0000001f)
            {
                return mira.normalized;
            }
        }
        if (punta != null)
        {
            Vector2 d = punta.position - transform.position;
            if (d.sqrMagnitude > 0.0000001f)
            {
                return d.normalized;
            }
        }
        return transform.up;
    }
}
