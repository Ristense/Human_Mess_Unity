using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// La nave, como cuerpo físico real (Rigidbody2D).
///
/// Controles: W y S empujan adelante y atrás, A y D corren de costado,
/// Q y E giran, Space frena. Todo se logra aplicando fuerza y torque,
/// no moviendo la posición
/// a mano — así "mass" es un peso de verdad: si más adelante se
/// vuelve más pesada (cargando chatarra), le va a costar más acelerar
/// y girar, gratis, porque lo calcula el motor de físicas.
///
/// En el espacio no hay roce: Linear Drag y Angular Drag del
/// Rigidbody2D tienen que quedar en 0. Lo que se mueva, se sigue
/// moviendo para siempre a menos que el jugador frene con el
/// estabilizador.
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

    [Tooltip("Empuje de costado (A y D): los propulsores laterales. " +
        "Más flojo que el principal a propósito — son chicos, están " +
        "para acomodarse al lado de una roca, no para viajar.")]
    [SerializeField] private float lateralForce = 8f;

    [Header("Estabilizador (tecla Space)")]
    [Tooltip("No es un truco que frena gratis: dispara contra-fuerza " +
        "real, en la dirección opuesta a como te estés moviendo/" +
        "girando ahora mismo. Al ser fuerza real, una nave más pesada " +
        "tarda más en frenar — mismo F = m·a que el resto del juego.")]
    [SerializeField] private float stabilizeForce = 18f;
    [SerializeField] private float stabilizeTorque = 14f;

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

    [Tooltip("Los de maniobra: girar y frenar con el estabilizador. " +
        "Opcional — un chorrito corto, más agudo que el principal.")]
    [SerializeField] private SonidoContinuo sonidoManiobra;

    private Rigidbody2D _rb;

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
    }

    private void FixedUpdate()
    {
        _gastadoEstePaso = false;

        // Keyboard.current (del paquete Input System), NO la clase
        // UnityEngine.Input vieja. Este proyecto viene configurado en
        // Project Settings > Player > Active Input Handling con el
        // Input System nuevo, y ahí la clase vieja tira excepción en
        // vez de simplemente no andar — avisa fuerte para que no te
        // quede un bug silencioso.
        //
        // Si no hay teclado conectado (poco probable, pero pasa en
        // testeo automatizado) Keyboard.current es null — por eso el
        // chequeo antes de leerlo.
        if (Keyboard.current == null)
        {
            Sonar(false, false);
            return;
        }

        // Q y E giran; A y D mueven de costado. Antes A y D giraban,
        // pero para trabajar pegado a una roca lo que hace falta es
        // correrse de lado sin dejar de apuntarle.
        float turn = 0f;
        if (Keyboard.current.qKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
        {
            turn -= 1f;
        }
        if (Keyboard.current.eKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
        {
            turn += 1f;
        }
        // Si el giro te sale al revés de lo que esperás, dale vuelta
        // el signo acá nomás — depende de cómo mira tu sprite.
        _rb.AddTorque(-turn * turnTorque);

        float thrust = 0f;
        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
        {
            thrust += 1f;
        }
        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
        {
            thrust -= 1f;
        }

        // "Adelante" es transform.up: asumimos que el arte de la nave
        // mira hacia arriba por defecto, igual que en Godot.
        Vector2 facing = transform.up;

        // El sonido sale de si el motor PRENDIÓ, no de si apretaste la
        // tecla: sin hidrógeno la nave queda muda, y esa es la forma
        // más rápida de darte cuenta de que te quedaste sin gas.
        bool empujando = false;
        if (thrust != 0f && HayCombustible(Time.fixedDeltaTime))
        {
            _rb.AddForce(thrust > 0f ? facing * thrustForce : -facing * reverseForce);
            empujando = true;
        }

        float lado = 0f;
        if (Keyboard.current.aKey.isPressed)
        {
            lado -= 1f;
        }
        if (Keyboard.current.dKey.isPressed)
        {
            lado += 1f;
        }

        // transform.right, porque "adelante" es transform.up: el
        // costado acompaña a la nave por mucho que la gires.
        bool deCostado = false;
        if (lado != 0f && HayCombustible(Time.fixedDeltaTime))
        {
            _rb.AddForce((Vector2)transform.right * (lado * lateralForce));
            deCostado = true;
        }

        // El estabilizador también son los motores: frenar gasta igual
        // que acelerar. Si no, quedarse sin hidrógeno no costaría nada
        // mientras se pudiera frenar gratis.
        bool frenando = false;
        if (Keyboard.current.spaceKey.isPressed && HayCombustible(Time.fixedDeltaTime))
        {
            Estabilizar(Time.fixedDeltaTime);
            frenando = true;
        }

        // Girar, frenar y correrse de lado son los mismos propulsores
        // chicos, así que comparten sonido.
        Sonar(empujando, turn != 0f || frenando || deCostado);

        ClampABounds();
    }

    private void Sonar(bool motor, bool maniobra)
    {
        if (sonidoMotor != null)
        {
            sonidoMotor.Activo = motor;
        }
        if (sonidoManiobra != null)
        {
            sonidoManiobra.Activo = maniobra;
        }
    }

    /// <summary>
    /// Frena apuntando a CERO, no a "bastante despacio" — la fuerza
    /// se calcula para cancelar la velocidad ENTERA en este paso de
    /// física, recortada a un tope para que no sea un tirón
    /// instantáneo. Así se acerca a cero cada vez más despacio a
    /// medida que falta menos, sin resto y sin necesitar umbral.
    ///
    /// Unity expone Rigidbody2D.inertia directo — en Godot hacía
    /// falta ir a buscarlo por PhysicsServer2D porque body_get_param
    /// no lo daba bien. Acá no hace falta ese rodeo.
    /// </summary>
    private void Estabilizar(float dt)
    {
        if (dt <= 0f)
        {
            return;
        }

        if (_rb.linearVelocity.magnitude > umbralParada)
        {
            Vector2 f = -_rb.linearVelocity * _rb.mass / dt;
            _rb.AddForce(Vector2.ClampMagnitude(f, stabilizeForce));
        }
        else
        {
            // El último tramo se corta a mano. Frenando con fuerza la
            // velocidad decae de forma exponencial: cada paso queda
            // menos, pero nunca llega a cero. Y un resto de 0.01 u/s
            // parece quieto en un frame y te corrió media nave en un
            // minuto. Por eso el salto final, que a esta escala no se
            // ve pero termina el movimiento de verdad.
            _rb.linearVelocity = Vector2.zero;
        }

        if (Mathf.Abs(_rb.angularVelocity) > umbralGiro)
        {
            // angularVelocity de Unity viene en GRADOS/seg y AddTorque
            // trabaja en radianes: hay que convertir. Sin esto el torque
            // pedido salía 57 veces más grande, siempre pegaba en el tope
            // y con un tope alto se pasaba de largo y temblaba.
            float t = -_rb.angularVelocity * Mathf.Deg2Rad * _rb.inertia / dt;
            _rb.AddTorque(Mathf.Clamp(t, -stabilizeTorque, stabilizeTorque));
        }
        else
        {
            _rb.angularVelocity = 0f;
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
