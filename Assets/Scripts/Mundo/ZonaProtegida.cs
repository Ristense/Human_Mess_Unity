using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Un círculo donde no entra nada que ande suelto: asteroides,
/// satélites, chatarra. Se usa alrededor de la base.
///
/// No es una pared: es un campo que empuja hacia afuera, más fuerte
/// cuanto más adentro estás, y que frena lo que viene entrando. Así lo
/// que llega rebota suave en el borde en vez de chocar contra algo
/// invisible — y como es una fuerza de verdad, un asteroide pesado
/// entra un poquito más antes de que lo frene, igual que en la vida.
///
/// La fuerza va escalada por la masa, así que frena igual a todos: si
/// no, lo pesado atravesaría la zona y lo liviano saldría disparado.
///
/// La nave y sus módulos pasan: la nave se excluye a mano, y lo que
/// tenga componente Modulo se ignora siempre.
///
/// CampoObjetos también le pregunta a esto antes de sembrar, así que
/// adentro tampoco aparece nada al arrancar.
///
/// Va en la base.
/// </summary>
public class ZonaProtegida : MonoBehaviour
{
    [Tooltip("Radio de la zona, en unidades de mundo.")]
    [SerializeField] private float radio = 10f;

    [Tooltip("Qué tan fuerte empuja para afuera por cada unidad que " +
        "algo se metió. Más alto = borde más duro.")]
    [SerializeField] private float rigidez = 6f;

    [Tooltip("Cuánto frena lo que viene entrando. Sin esto rebota y " +
        "oscila en el borde un rato largo.")]
    [SerializeField] private float amortiguacion = 4f;

    [Tooltip("Qué capas afecta.")]
    [SerializeField] private LayerMask capas = ~0;

    [Tooltip("La nave, que tiene que poder entrar.")]
    [SerializeField] private Rigidbody2D excluir;

    [SerializeField] private Color colorGizmo = new Color(0.3f, 1f, 0.6f, 0.6f);

    private static readonly List<ZonaProtegida> _todas = new List<ZonaProtegida>();

    // Para no empujar dos veces lo mismo: un asteroide con varios
    // colliders aparece una vez por cada uno en el OverlapCircle.
    private readonly HashSet<Rigidbody2D> _vistos = new HashSet<Rigidbody2D>();

    public float Radio => radio;

    private void OnEnable()
    {
        _todas.Add(this);
    }

    private void OnDisable()
    {
        _todas.Remove(this);
    }

    /// <summary>
    /// ¿Algo de este radio, puesto acá, quedaría adentro de alguna
    /// zona protegida? Para el que siembra objetos.
    /// </summary>
    public static bool Bloquea(Vector2 pos, float radioObjeto)
    {
        foreach (ZonaProtegida z in _todas)
        {
            float r = z.radio + radioObjeto;
            if (((Vector2)z.transform.position - pos).sqrMagnitude < r * r)
            {
                return true;
            }
        }
        return false;
    }

    private void FixedUpdate()
    {
        Vector2 centro = transform.position;
        _vistos.Clear();

        foreach (Collider2D c in Physics2D.OverlapCircleAll(centro, radio, capas))
        {
            Rigidbody2D rb = c.attachedRigidbody;
            if (rb == null || rb == excluir || rb.bodyType != RigidbodyType2D.Dynamic
                || !rb.simulated || !_vistos.Add(rb))
            {
                continue;
            }
            // lo que es de la nave (módulos, herramientas) pasa
            if (rb.GetComponent<Modulo>() != null)
            {
                continue;
            }

            Vector2 desde = rb.worldCenterOfMass - centro;
            float dist = desde.magnitude;
            Vector2 n = dist > 0.0001f ? desde / dist : Vector2.up;

            // Se cuenta el tamaño del objeto: si no, un asteroide grande
            // se metería hasta la mitad antes de que su centro cruce el
            // borde y empiece a sentir el empuje.
            Bounds b = c.bounds;
            float tam = Mathf.Max(b.extents.x, b.extents.y);
            float adentro = radio + tam - dist;
            if (adentro <= 0f)
            {
                continue;
            }

            // solo se frena la velocidad que va HACIA ADENTRO; lo que ya
            // se está yendo, que se vaya tranquilo
            float vEntrando = Mathf.Min(0f, Vector2.Dot(rb.linearVelocity, n));

            Vector2 aceleracion = n * (adentro * rigidez - vEntrando * amortiguacion);
            rb.AddForce(aceleracion * rb.mass);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = colorGizmo;
        Gizmos.DrawWireSphere(transform.position, radio);
    }
}
