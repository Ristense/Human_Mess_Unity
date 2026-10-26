using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// La nave, como cuerpo físico real (Rigidbody2D).
///
/// Controles: W y S empujan adelante y atrás, Q y E corren de costado,
/// A y D giran, Space prende y apaga los amortiguadores. Todo se logra
/// aplicando fuerza y torque, no moviendo la posición a mano — así
/// "mass" es un peso de verdad: si más adelante se vuelve más pesada
/// (cargando chatarra), le va a costar más acelerar y girar, gratis,
/// porque lo calcula el motor de físicas.
///
/// En el espacio no hay roce: Linear Drag y Angular Drag del
/// Rigidbody2D tienen que quedar en 0. Lo que se mueva, se sigue
/// moviendo para siempre a menos que lo frenen los amortiguadores.
///
/// Los amortiguadores son los de Space Engineers: prendidos, la nave
/// se pilotea sola en cada eje que NO estás tocando. Soltás W y frena;
/// avanzás con deriva de costado y los laterales la corrigen solos;
/// soltás A y deja de girar. No son un freno aparte: usan los mismos
/// propulsores, con la misma fuerza y el mismo gas, así que una nave
/// cargada tarda más en frenar y sin hidrógeno no frenan.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class Nave : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("El Mapa que define los límites del mundo.")]
    [SerializeField] private Mapa mapa;

    [Header("Movimiento")]
    [Tooltip("OJO: la escala de estos números depende de tu escena. " +
        "Si 1 unidad = 1 metro (lo típico en Unity), valores chicos " +
        "(10-30) ya mueven la nave con fuerza. Si laburás en píxeles " +
        "como en el proyecto Godot original, vas a necesitar números " +
        "miles de veces más grandes. Probá y ajustá a ojo.")]
    [SerializeField] private float thrustForce = 20f;
    [SerializeField] private float reverseForce = 10f;
    [SerializeField] private float turnTorque = 15f;

    [Tooltip("Empuje de costado (Q y E): los propulsores laterales. " +
        "Más flojo que el principal a propósito — son chicos, están " +
        "para acomodarse al lado de una roca, no para viajar.")]
    [SerializeField] private float lateralForce = 8f;

    [Header("Amortiguadores (Space prende y apaga)")]
    [Tooltip("Arrancar con los amortiguadores prendidos.")]
    [SerializeField] private bool amortiguadoresAlInicio = true;

    [Tooltip("Por debajo de esta velocidad (unidades/seg) se corta a " +
        "cero de una. Sin esto la nave queda derivando un resto " +
        "chiquito eternamente: frenar con fuerza se ACERCA a cero " +
        "pero no llega nunca. Subilo si te sigue deslizando, bajalo " +
        "si el final se siente abrupto.")]
    [SerializeField] private float umbralParada = 0.02f;

    [Tooltip("Lo mismo pero para el giro, en grados/seg.")]
    [SerializeField] private float umbralGiro = 0.5f;

    [Tooltip("Los tanques, para gastar hidrógeno al empujar. Vacío = " +
        "lo busca en la nave. Sin soporte vital los motores andan " +
        "gratis, como antes.")]
    [SerializeField] private SoporteVital soporte;

    [Header("Sonido")]
    [Tooltip("El motor principal: suena mientras empujás de verdad, o " +
        "sea con gas en el tanque. Sin hidrógeno la nave queda muda, " +
        "que es la mejor forma de enterarse.")]
    [SerializeField] private SonidoContinuo sonidoMotor;

    [Tooltip("Los de maniobra: girar y moverse de costado, a mano o " +
        "corrigiendo solos. Opcional — un chorrito corto, más agudo " +
        "que el principal.")]
    [SerializeField] private SonidoContinuo sonidoManiobra;

    private Rigidbody2D _rb;

    /// <summary>¿Están prendidos los amortiguadores?</summary>
    public bool Amortiguadores { get; private set; }

    // Lo que hizo cada grupo de propulsores en el último paso de física,
    // de -1 a 1 (fracción de su fuerza). Sale de lo que de verdad
    // prendió —tu tecla o la corrección automática— y no de las teclas:
    // así los chorros de gas y el sonido muestran a la nave pilotándose
    // sola, y sin hidrógeno no muestran nada.

    /// <summary>Motor principal: 1 adelante a fondo, -1 reversa a fondo.</summary>
    public float Empuje { get; private set; }

    /// <summary>Giro: -1 a la izquierda (A), 1 a la derecha (D).</summary>
    public float Giro { get; private set; }

    /// <summary>Costado: -1 hacia la Q, 1 hacia la E.</summary>
    public float Costado { get; private set; }

    /// <summary>
    /// Descuenta el hidrógeno de este paso de física y dice si alcanzó.
    /// Se llama una sola vez por paso aunque empujes y estabilices a la
    /// vez: si no, apretar W y espacio juntos gastaría el doble.
    /// </summary>
    private bool HayCombustible(float dt)
    {
        if (soporte == null)
        {
            return true;
        }
        if (_gastadoEstePaso)
        {
            return _alcanzoEstePaso;
        }

        _gastadoEstePaso = true;
        _alcanzoEstePaso = soporte.GastarEmpuje(dt);
        return _alcanzoEstePaso;
    }

    private bool _gastadoEstePaso;
    private bool _alcanzoEstePaso;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        if (soporte == null)
        {
            soporte = GetComponentInParent<SoporteVital>();
        }
        _rb.gravityScale = 0f;
        // Sin roce: ver comentario de arriba. Si en el Inspector
        // quedaron en otro valor, esto lo fuerza a cero al arrancar.
        _rb.linearDamping = 0f;
        _rb.angularDamping = 0f;
        Amortiguadores = amortiguadoresAlInicio;
    }

    private void Update()
    {
        // En Update y no en FixedUpdate: "se apretó este frame" puede
        // caer entre dos pasos de física y perderse, y un toggle que a
        // veces no responde es peor que no tenerlo.
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            Amortiguadores = !Amortiguadores;
        }
    }

    private void FixedUpdate()
    {
        _gastadoEstePaso = false;
        Empuje = 0f;
        Giro = 0f;
        Costado = 0f;

        // Keyboard.current (del paquete Input System), NO la clase
        // UnityEngine.Input vieja: con el Input System nuevo activo, la
        // vieja tira excepción. Y si no hay teclado (testeo automatizado)
        // es null, por eso el chequeo.
        if (Keyboard.current == null)
        {
            Sonar();
            return;
        }

        Keyboard k = Keyboard.current;
        float dt = Time.fixedDeltaTime;

        // A y D giran; Q y E mueven de costado, para correrse al lado
        // de una roca sin dejar de apuntarle.
        float turn = Eje(k.aKey.isPressed || k.leftArrowKey.isPressed,
            k.dKey.isPressed || k.rightArrowKey.isPressed);
        float thrust = Eje(k.sKey.isPressed || k.downArrowKey.isPressed,
            k.wKey.isPressed || k.upArrowKey.isPressed);
        float lado = Eje(k.qKey.isPressed, k.eKey.isPressed);

        // "Adelante" es transform.up: el arte de la nave mira hacia
        // arriba. El costado es transform.right, así acompaña a la nave
        // por mucho que la gires.
        Vector2 adelante = transform.up;
        Vector2 derecha = transform.right;

        // Cada eje decide por su cuenta: si lo estás tocando manda tu
        // tecla; si no, y los amortiguadores están prendidos, manda la
        // corrección. Por eso al avanzar con deriva de costado los
        // laterales la van matando solos mientras vos solo apretás W.
        float pideEmpuje = thrust != 0f ? thrust
            : Amortiguadores ? Frenar(Vector2.Dot(_rb.linearVelocity, adelante),
                adelante, thrustForce, reverseForce, dt)
            : 0f;
        float pideCostado = lado != 0f ? lado
            : Amortiguadores ? Frenar(Vector2.Dot(_rb.linearVelocity, derecha),
                derecha, lateralForce, lateralForce, dt)
            : 0f;

        // El gas se cobra una vez por paso para todo lo lineal. La
        // corrección automática gasta igual que tu tecla: si frenar
        // fuera gratis, quedarse sin hidrógeno no costaría nada.
        if ((pideEmpuje != 0f || pideCostado != 0f) && HayCombustible(dt))
        {
            Empuje = pideEmpuje;
            Costado = pideCostado;
            float fAdelante = Empuje >= 0f ? Empuje * thrustForce : Empuje * reverseForce;
            _rb.AddForce(adelante * fAdelante + derecha * (Costado * lateralForce));
        }

        // El giro no gasta gas, igual que antes: ni a mano ni corregido.
        Giro = turn != 0f ? turn
            : Amortiguadores ? FrenarGiro(dt)
            : 0f;
        // Si el giro te sale al revés, dale vuelta el signo acá nomás —
        // depende de cómo mira tu sprite.
        _rb.AddTorque(-Giro * turnTorque);

        Sonar();
        ClampABounds();
    }

    private static float Eje(bool menos, bool mas)
    {
        return (mas ? 1f : 0f) - (menos ? 1f : 0f);
    }

    /// <summary>
    /// Cuánto motor hace falta para matar la velocidad de un eje, como
    /// fracción de -1 a 1 de ese motor.
    ///
    /// Apunta a cancelarla ENTERA en este paso, recortado a lo que el
    /// motor da: lejos de cero va a fondo, y el último paso pide justo
    /// lo que falta y la deja en cero, sin cola. La fuerza disponible es
    /// distinta para cada lado (la reversa es más floja que el
    /// principal), por eso se pasan las dos.
    ///
    /// El resto chiquito se corta a mano: sin eso quedaría un 0.01 u/s
    /// que parece quieto y en un minuto te corrió media nave.
    /// </summary>
    private float Frenar(float v, Vector2 eje, float fuerzaMas, float fuerzaMenos, float dt)
    {
        if (Mathf.Abs(v) <= umbralParada)
        {
            _rb.linearVelocity -= eje * v;
            return 0f;
        }
        float f = -v * _rb.mass / dt;
        return f >= 0f
            ? (fuerzaMas > 0f ? Mathf.Min(f / fuerzaMas, 1f) : 0f)
            : (fuerzaMenos > 0f ? Mathf.Max(f / fuerzaMenos, -1f) : 0f);
    }

    /// <summary>
    /// Lo mismo para el giro, devuelto en la misma escala que A/D.
    ///
    /// angularVelocity viene en GRADOS/seg y AddTorque trabaja en
    /// radianes: sin convertir, el torque pedido salía 57 veces más
    /// grande, siempre pegaba en el tope y se pasaba de largo temblando.
    /// </summary>
    private float FrenarGiro(float dt)
    {
        float w = _rb.angularVelocity;
        if (Mathf.Abs(w) <= umbralGiro)
        {
            _rb.angularVelocity = 0f;
            return 0f;
        }
        if (turnTorque <= 0f)
        {
            return 0f;
        }
        float torque = -w * Mathf.Deg2Rad * _rb.inertia / dt;
        // AddTorque(-Giro * turnTorque), así que Giro = -torque / turnTorque
        return Mathf.Clamp(-torque / turnTorque, -1f, 1f);
    }

    // Por debajo de esto una corrección no cuenta como "prendido" para
    // el sonido: si no, el motor zumbaría con cada empujoncito mínimo.
    private const float UmbralSonido = 0.05f;

    private void Sonar()
    {
        if (sonidoMotor != null)
        {
            sonidoMotor.Activo = Mathf.Abs(Empuje) > UmbralSonido;
        }
        if (sonidoManiobra != null)
        {
            sonidoManiobra.Activo = Mathf.Abs(Giro) > UmbralSonido
                || Mathf.Abs(Costado) > UmbralSonido;
        }
    }

    /// <summary>
    /// No te deja salir de los sectores prendidos del Mapa. Al tocar un
    /// borde, se recorta la posición Y se anula la velocidad que te
    /// seguía empujando para afuera — sin esto último, quedás "pegado"
    /// al borde pero la física te sigue acelerando contra la pared
    /// invisible.
    ///
    /// No alcanza con un rectángulo: el mapa puede tener forma de L o
    /// de T, y entonces hay huecos adentro del rectángulo que lo rodea.
    /// </summary>
    private void ClampABounds()
    {
        if (mapa == null)
        {
            return;
        }

        Vector2 dentro = mapa.Recortar(_rb.position, out Vector2 afuera);
        if (afuera == Vector2.zero)
        {
            return;
        }

        _rb.position = dentro;

        // solo se anula la parte de la velocidad que va hacia afuera:
        // si venís en diagonal contra una pared, seguís deslizando a lo
        // largo de ella en vez de clavarte
        Vector2 v = _rb.linearVelocity;
        float saliendo = Vector2.Dot(v, afuera);
        if (saliendo > 0f)
        {
            v -= afuera * saliendo;
        }
        _rb.linearVelocity = v;
    }
}
