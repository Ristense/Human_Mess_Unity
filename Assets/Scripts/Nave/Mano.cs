using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// La mano: agarra y suelta cosas con clic sostenido.
///
/// Usa TargetJoint2D, que es el joint que Unity tiene hecho justo para
/// esto: ata un punto del objeto a una posición del mundo. Lo resuelve
/// el motor de física, así que no se descontrola como un resorte
/// aplicado a mano — ese, con rigidez alta y sin amortiguación, entra
/// en oscilación y termina sacudiendo la pieza y la nave juntas.
///
/// Lo que hace que la masa importe es "Fuerza Maxima": el joint nunca
/// tira más fuerte que eso, así que un asteroide pesado se arrastra
/// despacio y uno liviano vuela. Es F = m·a, sin simular nada a mano.
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
        "dentro de este radio.")]
    [SerializeField] private float radioAgarre = 0.2f;

    [SerializeField] private LayerMask capasAgarrables = ~0;

    [Header("Agarre")]
    [Tooltip("Qué tan firme es el agarre, en Hz. Más alto = más rígido, " +
        "se siente casi soldado a la mano. 15-25 es bien firme.")]
    [SerializeField] private float frecuencia = 18f;

    [Tooltip("Cuánto amortigua el rebote. 1 = sin oscilación, pegado y " +
        "quieto. Bajalo si querés que se bambolee un poco.")]
    [Range(0f, 1f)]
    [SerializeField] private float amortiguacion = 1f;

    [Tooltip("Tope de fuerza del agarre. ESTE es el número que hace que " +
        "el peso importe: con poca fuerza, lo pesado no te sigue.")]
    [SerializeField] private float fuerzaMaxima = 2000f;

    [Tooltip("Cuánto resiste la pieza a girar entre los dedos. Con 0 " +
        "pivotea libre y, si la agarraste de un borde, queda girando.")]
    [SerializeField] private float frenoGiro = 5f;

    [Tooltip("Cuánto de la reacción se le devuelve a la nave. Si se " +
        "sacude feo, bajalo.")]
    [Range(0f, 1f)]
    [SerializeField] private float reaccionNave = 0.3f;

    [Tooltip("Pasada esta distancia la mano no da más y suelta.")]
    [SerializeField] private float distanciaRuptura = 3f;

    [Header("Montaje")]
    [Tooltip("Al soltar un Modulo, si hay una celda libre que lo " +
        "acepte a menos de esta distancia, se enchufa sola. Ponelo " +
        "parecido al Cell Size de las zonas.")]
    [SerializeField] private float radioMontaje = 0.3f;

    /// <summary>Lo que tenemos agarrado ahora, o null.</summary>
    public Rigidbody2D Agarrado { get; private set; }

    private TargetJoint2D _junta;

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
        if (Agarrado == null || _junta == null || brazo == null)
        {
            return;
        }

        Vector2 mano = brazo.ManoGlobal;
        _junta.target = mano;

        // el punto agarrado, en mundo, para medir cuánto se estiró
        Vector2 punto = Agarrado.transform.TransformPoint(_junta.anchor);
        if (Vector2.Distance(punto, mano) > distanciaRuptura)
        {
            Soltar();
            return;
        }

        // Tercera ley de Newton: si tirás de algo pesado, algo tira de
        // vos. El joint nos dice con cuánta fuerza está tirando, así
        // que se la devolvemos invertida a la nave.
        if (nave != null && reaccionNave > 0f)
        {
            nave.AddForce(-_junta.reactionForce * reaccionNave);
        }

        // Freno de giro: mide la velocidad angular de la PIEZA, no la
        // del punto agarrado. Si midiera el punto, una pieza girando
        // justo alrededor de ese punto daría cero y nunca se frenaría.
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

        // Lo que esté MONTADO no lo encuentra Physics2D (tiene la
        // física apagada), así que se busca aparte y tiene prioridad:
        // si estás con la mano encima de una herramienta enchufada,
        // querés sacar esa, no agarrar lo que haya atrás.
        Modulo montado = Zona.MontadoCerca(mano, radioAgarre);
        if (montado != null && montado.zonaActual != null)
        {
            montado.zonaActual.SacarA(montado);
            Agarrado = montado.GetComponent<Rigidbody2D>();
            if (Agarrado == null)
            {
                return;
            }
        }
        else
        {
            if (tocado == null || tocado.attachedRigidbody == null
                || tocado.attachedRigidbody == nave)
            {
                return;
            }
            Agarrado = tocado.attachedRigidbody;
        }

        _junta = Agarrado.gameObject.AddComponent<TargetJoint2D>();
        _junta.autoConfigureTarget = false;
        // el punto exacto donde tocaste, en LOCAL: si la pieza rota, el
        // agarre rota con ella, que es lo que uno espera
        _junta.anchor = Agarrado.transform.InverseTransformPoint(mano);
        _junta.target = mano;
        _junta.frequency = frecuencia;
        _junta.dampingRatio = amortiguacion;
        _junta.maxForce = fuerzaMaxima;
    }

    private void Soltar()
    {
        if (_junta != null)
        {
            Destroy(_junta);
            _junta = null;
        }
        // ¿Lo estás soltando encima de un socket? Entonces se enchufa
        // en vez de quedar flotando. Medimos desde la pieza y no desde
        // la mano: lo que tiene que calzar en la celda es la pieza.
        if (Agarrado != null)
        {
            Modulo modulo = Agarrado.GetComponent<Modulo>();
            if (modulo != null && Zona.MejorCelda(modulo, Agarrado.position,
                    radioMontaje, out Zona zona, out int cx, out int cy))
            {
                zona.Montar(modulo, cx, cy);
            }
        }

        // Si no se montó, no se le toca la velocidad: sale despedida
        // con el impulso que traía, como si de verdad la hubieras
        // soltado en movimiento.
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
