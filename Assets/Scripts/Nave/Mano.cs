using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// La mano: agarra y suelta cosas con clic sostenido. Port del
/// gancho.gd de Godot, con la misma idea central:
///
/// El agarre es un RESORTE de rigidez FIJA, no un teletransporte. Como
/// la fuerza es siempre la misma, mover algo pesado cuesta y algo
/// liviano vuela — eso sale solo de F = m·a, sin simular nada. Si
/// mañana cargás la nave y pesa más, todo se recalibra gratis.
///
/// Va en la nave, junto al BrazoIK.
/// </summary>
public class Mano : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private BrazoIK brazo;
    [Tooltip("El Rigidbody2D de la nave — recibe la reacción de lo que agarres.")]
    [SerializeField] private Rigidbody2D nave;

    [Header("Detección")]
    [Tooltip("Si la mano no toca nada justo, agarra lo más cercano " +
        "dentro de este radio. Es para que pescar cosas chicas no sea " +
        "pixel-perfect.")]
    [SerializeField] private float radioAgarre = 0.3f;

    [Tooltip("Qué capas se pueden agarrar.")]
    [SerializeField] private LayerMask capasAgarrables = ~0;

    [Header("Fuerza")]
    [Tooltip("Rigidez del resorte, en fuerza por unidad de separación. " +
        "FIJA a propósito: ver comentario de arriba.")]
    [SerializeField] private float rigidez = 300f;

    [Tooltip("Tope de fuerza, para que una separación grande no mande " +
        "la pieza a la estratósfera.")]
    [SerializeField] private float fuerzaMaxima = 2000f;

    [Tooltip("Cuánto de la reacción se le devuelve a la nave. 0 = la " +
        "nave es inamovible (poco creíble); 1 = reacción completa.")]
    [Range(0f, 1f)]
    [SerializeField] private float reaccionNave = 0.5f;

    [Tooltip("Cuánto resiste la mano a que la pieza le gire entre los " +
        "dedos. Con 0 pivotea libre y, si la agarraste de un borde, " +
        "queda girando sola.")]
    [SerializeField] private float frenoGiro = 5f;

    [Tooltip("Pasada esta distancia la mano no da más y suelta.")]
    [SerializeField] private float distanciaRuptura = 3f;

    /// <summary>Lo que tenemos agarrado ahora, o null.</summary>
    public Rigidbody2D Agarrado { get; private set; }

    // En qué punto de la pieza la agarraste, en coordenadas LOCALES de
    // ella. Se fija al hacer clic y no cambia: eso es lo que hace que
    // la mano quede pegada al lugar que tocaste, en vez de que la
    // pieza te lleve su centro a la mano.
    private Vector2 _puntoLocal;

    private void Update()
    {
        if (Mouse.current == null || brazo == null)
        {
            return;
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Agarrar();
        }
        else if (Mouse.current.leftButton.wasReleasedThisFrame)
        {
            Soltar();
        }
    }

    private void FixedUpdate()
    {
        if (Agarrado == null || brazo == null)
        {
            return;
        }

        Vector2 mano = brazo.ManoGlobal;
        Vector2 punto = Agarrado.transform.TransformPoint(_puntoLocal);
        Vector2 separacion = mano - punto;

        if (separacion.magnitude > distanciaRuptura)
        {
            Soltar();
            return;
        }

        Vector2 fuerza = Vector2.ClampMagnitude(separacion * rigidez, fuerzaMaxima);
        Agarrado.AddForceAtPosition(fuerza, punto);

        // Tercera ley de Newton: si tirás de algo pesado, algo tira de
        // vos. Sin esto la nave sería un ancla mágica.
        if (nave != null && reaccionNave > 0f)
        {
            nave.AddForce(-fuerza * reaccionNave);
        }

        // Freno de giro: mide la velocidad angular de la pieza, no la
        // del punto agarrado. Si midiera el punto, una pieza girando
        // justo alrededor de ese punto daría velocidad cero y nunca se
        // frenaría — pasaba exactamente eso en la versión de Godot.
        if (frenoGiro > 0f)
        {
            Agarrado.AddTorque(-Agarrado.angularVelocity * frenoGiro * Time.fixedDeltaTime);
        }
    }

    private void Agarrar()
    {
        if (Agarrado != null)
        {
            return;
        }

        Vector2 mano = brazo.ManoGlobal;

        // primero: ¿la mano está justo encima de algo? Eso gana siempre,
        // sin importar el tamaño de la pieza
        Collider2D tocado = Physics2D.OverlapPoint(mano, capasAgarrables);

        // si no, el radio de cortesía: lo más cercano que haya cerca
        if (tocado == null)
        {
            Collider2D[] cerca = Physics2D.OverlapCircleAll(mano, radioAgarre, capasAgarrables);
            float mejorDist = float.MaxValue;
            foreach (var c in cerca)
            {
                if (c.attachedRigidbody == null || c.attachedRigidbody == nave)
                {
                    continue;
                }
                float d = Vector2.Distance(c.ClosestPoint(mano), mano);
                if (d < mejorDist)
                {
                    mejorDist = d;
                    tocado = c;
                }
            }
        }

        if (tocado == null || tocado.attachedRigidbody == null || tocado.attachedRigidbody == nave)
        {
            return;
        }

        Agarrado = tocado.attachedRigidbody;
        // el punto exacto donde tocaste, guardado en local: si la pieza
        // rota, el punto rota con ella, que es lo que uno espera
        _puntoLocal = Agarrado.transform.InverseTransformPoint(mano);
    }

    private void Soltar()
    {
        // No se le toca la velocidad: sale despedida con el impulso que
        // traía, como si de verdad la hubieras soltado en movimiento.
        Agarrado = null;
    }

    private void OnDrawGizmosSelected()
    {
        if (brazo == null)
        {
            return;
        }
        Gizmos.color = new Color(1f, 0.75f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(brazo.ManoGlobal, radioAgarre);
    }
}
