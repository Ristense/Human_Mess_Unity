using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// La nave, como cuerpo físico real (Rigidbody2D). El movimiento de
/// tanque se logra aplicando fuerza y torque, no moviendo la posición
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

    private Rigidbody2D _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.gravityScale = 0f;
        // Sin roce: ver comentario de arriba. Si en el Inspector
        // quedaron en otro valor, esto lo fuerza a cero al arrancar.
        _rb.linearDamping = 0f;
        _rb.angularDamping = 0f;
    }

    private void FixedUpdate()
    {
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
            return;
        }

        float turn = 0f;
        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
        {
            turn -= 1f;
        }
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
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
        if (thrust > 0f)
        {
            _rb.AddForce(facing * thrustForce);
        }
        else if (thrust < 0f)
        {
            _rb.AddForce(-facing * reverseForce);
        }

        if (Keyboard.current.spaceKey.isPressed)
        {
            Estabilizar(Time.fixedDeltaTime);
        }

        ClampABounds();
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
            // angularVelocity de Unity viene en GRADOS/seg, no
            // radianes — a diferencia de Godot. Para la cuenta de
            // torque no importa mientras seamos consistentes: lo que
            // querés es "la fuerza que frena exactamente este resto
            // de giro en un paso", y eso sale igual en cualquier
            // unidad angular mientras no mezclés.
            float t = -_rb.angularVelocity * _rb.inertia / dt;
            _rb.AddTorque(Mathf.Clamp(t, -stabilizeTorque, stabilizeTorque));
        }
        else
        {
            _rb.angularVelocity = 0f;
        }
    }

    /// <summary>
    /// No te deja salir del rectángulo del Mapa. Al tocar un borde,
    /// se recorta la posición Y se anula la velocidad que te seguía
    /// empujando para afuera — sin esto último, quedás "pegado" al
    /// borde pero la física te sigue acelerando contra la pared
    /// invisible.
    /// </summary>
    private void ClampABounds()
    {
        if (mapa == null)
        {
            return;
        }

        Rect b = mapa.WorldBounds();
        Vector2 p = _rb.position;
        Vector2 v = _rb.linearVelocity;

        if (p.x < b.xMin)
        {
            p.x = b.xMin;
            v.x = Mathf.Max(v.x, 0f);
        }
        else if (p.x > b.xMax)
        {
            p.x = b.xMax;
            v.x = Mathf.Min(v.x, 0f);
        }

        if (p.y < b.yMin)
        {
            p.y = b.yMin;
            v.y = Mathf.Max(v.y, 0f);
        }
        else if (p.y > b.yMax)
        {
            p.y = b.yMax;
            v.y = Mathf.Min(v.y, 0f);
        }

        _rb.position = p;
        _rb.linearVelocity = v;
    }
}
