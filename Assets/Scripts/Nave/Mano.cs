using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// La mano: agarra con clic sostenido, monta y equipa con la tecla.
///
/// Tiene dos formas de sostener, según "Agarre Rigido":
///
/// RÍGIDO (el que usamos): un FixedJoint2D entre la NAVE y la pieza.
/// Al ser una junta entre dos cuerpos, el solver reparte según las
/// masas: movés el brazo con algo liviano y la pieza te sigue; con un
/// asteroide pesado, el que se corre sos vos. Nunca se separan, porque
/// no hay nada elástico en el medio.
///
/// RESORTE: un TargetJoint2D que tira la pieza hacia la mano, con tope
/// de fuerza. Más suelto y más perdonador, pero con algo pesado la
/// mano se despega del punto de agarre.
///
/// La tecla (E por defecto) es un mantener con tiempo de carga, y hace
/// lo que corresponda: equipar a la mano, anclar en un socket cercano,
/// o sacarse lo que tengas puesto. El clic solo agarra y suelta.
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
    [Tooltip("Junta rígida entre la NAVE y lo que agarres, en vez de " +
        "un resorte hacia la mano. La resuelve el motor con las dos " +
        "masas: agarrás algo pesado y es la nave la que se va hacia " +
        "él, no la mano la que se estira. Nunca se separan.")]
    [SerializeField] private bool agarreRigido = true;

    [Tooltip("Cuánta fuerza aguanta el agarre antes de zafarse, en " +
        "newtons. 0 = no se suelta nunca. Solo para el agarre rígido.")]
    [SerializeField] private float fuerzaRuptura = 0f;

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

    [Header("Orientación")]
    [Tooltip("Que la pieza mantenga el ángulo con el que la agarraste, " +
        "en vez de colgar suelta del punto de agarre. Esto es lo que " +
        "la hace sentir PEGADA a la mano y no colgada de ella.")]
    [SerializeField] private bool pegarOrientacion = true;

    [Tooltip("Qué tan fuerte corrige el ángulo. Más alto = más rígido.")]
    [SerializeField] private float rigidezGiro = 80f;

    [Tooltip("Tope de torque. El equivalente angular de Fuerza Maxima: " +
        "bajalo y las piezas con mucha inercia tardan en acomodarse, " +
        "que es como se siente agarrar algo largo y pesado.")]
    [SerializeField] private float torqueMaximo = 100f;

    [Tooltip("Cuánto de la reacción se le devuelve a la nave. Si se " +
        "sacude feo, bajalo.")]
    [Range(0f, 1f)]
    [SerializeField] private float reaccionNave = 0.3f;

    [Tooltip("Pasada esta distancia la mano no da más y suelta.")]
    [SerializeField] private float distanciaRuptura = 3f;

    [Header("Equipar")]
    [Tooltip("La Zona de la mano, donde se enchufa la herramienta al " +
        "equiparla. Tiene que ser hija del objeto Mano (para que la " +
        "siga) y conviene dejarle Automatico APAGADO, así solo se " +
        "equipa a propósito con la tecla.")]
    [SerializeField] private Zona socketMano;

    [Tooltip("Mantenela apretada mientras hacés clic sobre una " +
        "herramienta y en vez de agarrarla, te la calzás.")]
    [SerializeField] private Key teclaEquipar = Key.E;

    [Tooltip("Cuántos segundos hay que mantener la tecla para que se " +
        "complete. 0 = al toque, sin barra.")]
    [SerializeField] private float tiempoEquipar = 0.6f;

    [Header("Montaje")]
    [Tooltip("Al soltar un Modulo, si hay una celda libre que lo " +
        "acepte a menos de esta distancia, se enchufa sola. Ponelo " +
        "parecido al Cell Size de las zonas.")]
    [SerializeField] private float radioMontaje = 0.3f;

    [Tooltip("Mientras lo tengas agarrado, que la pieza atraviese la " +
        "nave en vez de chocarla. Si no, la estás empujando contra tu " +
        "propio casco y las dos se sacuden.")]
    [SerializeField] private bool atraviesaLaNave = true;

    [Tooltip("Escupe por Consola qué agarra, qué suelta y dónde se " +
        "monta. Para cuando algo se comporta raro y no se ve por qué.")]
    [SerializeField] private bool diagnostico = false;

    /// <summary>Lo que tenemos agarrado ahora, o null.</summary>
    public Rigidbody2D Agarrado { get; private set; }

    /// <summary>
    /// Cuánto va cargada la tecla, de 0 a 1. Para la barrita: si da 0
    /// no hay nada en curso y conviene esconderla.
    /// </summary>
    public float Progreso
    {
        get
        {
            if (tiempoEquipar <= 0f)
            {
                return 0f;
            }
            return Mathf.Clamp01(_cargaTecla / tiempoEquipar);
        }
    }

    private TargetJoint2D _junta;
    private FixedJoint2D _rigida;
    private Vector2 _anclaLocal;
    private float _cargaTecla;
    private bool _esperaSoltar;

    /// <summary>
    /// Deshace el agarre físico, sea del tipo que sea, y le devuelve el
    /// brazo al mouse. Todo lo que suelta tiene que pasar por acá:
    /// olvidarse del AimOverride deja el brazo clavado para siempre.
    /// </summary>
    private void SoltarJuntas()
    {
        if (_junta != null)
        {
            Destroy(_junta);
            _junta = null;
        }
        if (_rigida != null)
        {
            Destroy(_rigida);
            _rigida = null;
        }
        if (brazo != null)
        {
            brazo.AimOverride = null;
        }
        IgnorarContraLaNave(false);
    }

    // Diferencia entre el ángulo de la mano y el de la pieza en el
    // instante del agarre. Guardarla es lo que hace que la pieza NO
    // pegue un salto para alinearse: se queda como la tomaste y de ahí
    // en más acompaña.
    private float _offsetAngulo;

    private bool QuiereEquipar()
    {
        return Keyboard.current != null && Keyboard.current[teclaEquipar].isPressed;
    }

    /// <summary>
    /// Hacia dónde apunta la mano, en grados. Sale de la dirección del
    /// antebrazo, que es lo que de verdad define la muñeca.
    /// </summary>
    private float AnguloMano()
    {
        Vector2 d = brazo.ManoGlobal - brazo.CodoGlobal;
        if (d.sqrMagnitude < 0.000001f)
        {
            return 0f;
        }
        return Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
    }

    // Los pares que pusimos a ignorarse al agarrar. Hay que guardarlos
    // para poder devolverlos como estaban al soltar: Physics2D.Ignore
    // es por PAR de colliders, no un flag del objeto.
    private readonly System.Collections.Generic.List<Collider2D> _ignorados =
        new System.Collections.Generic.List<Collider2D>();
    private Collider2D[] _colsNave;

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

        // Teniéndola ya en la mano, E se la calza. El orden entre clic
        // y tecla no importa: esto se chequea todos los frames, así
        // que si venías con E apretada se equipa apenas la agarrás.
        CargarTecla();
    }

    /// <summary>
    /// E no actúa al toque: hay que sostenerla. Mientras se sostiene
    /// se acumula tiempo, y al llenarse se hace la acción que
    /// corresponda — equipar si tenés algo en la mano, desequipar si
    /// la tenés vacía.
    /// </summary>
    private void CargarTecla()
    {
        if (!QuiereEquipar())
        {
            _cargaTecla = 0f;
            _esperaSoltar = false;
            return;
        }

        // Ya se completó una acción y seguís sin soltar la tecla: no
        // encadenamos otra. Sin esto equipás, y como la mano pasa a
        // estar vacía con algo puesto, arranca sola a desequipar.
        if (_esperaSoltar || !HayAccionDeTecla())
        {
            _cargaTecla = 0f;
            return;
        }

        _cargaTecla += Time.deltaTime;
        if (_cargaTecla < tiempoEquipar)
        {
            return;
        }

        if (Agarrado != null)
        {
            // Primero la mano, y si no entra ahí, el socket más
            // cercano. Así una herramienta se te calza y un módulo
            // —que en la mano no entra— se ancla en la nave, con el
            // mismo gesto.
            if (!Equipar() && !MontarCerca())
            {
                Debug.LogWarning("[Mano] " + Agarrado.name + " no entra " +
                    "ni en la mano ni en ningún socket cerca. Revisá el " +
                    "Tipo de la pieza contra el Acepta de las zonas, que " +
                    "no estén llenas, y el Radio Montaje.", Agarrado);
            }
        }
        else
        {
            Desequipar();
        }

        _cargaTecla = 0f;
        _esperaSoltar = true;
    }

    /// <summary>¿La tecla tiene algo que hacer ahora mismo?</summary>
    private bool HayAccionDeTecla()
    {
        // Con algo en la mano siempre hay algo que intentar, aunque no
        // haya socket de mano: puede terminar anclándose en una zona
        // de la nave.
        if (Agarrado != null)
        {
            return true;
        }
        return socketMano != null && socketMano.Montado() != null;
    }

    /// <summary>
    /// Saca lo que tengas equipado. Si justo hay un socket cerca donde
    /// guardarlo, lo deja ahí; si no, queda flotando donde estaba.
    /// </summary>
    private void Desequipar()
    {
        if (socketMano == null)
        {
            return;
        }

        Modulo pieza = socketMano.Montado();
        if (pieza == null)
        {
            return;
        }

        socketMano.Sacar();

        bool guardada = Zona.MejorCelda(pieza, pieza.transform.position,
                            radioMontaje, out Zona zona, out int cx, out int cy)
                        && zona != socketMano
                        && zona.Montar(pieza, cx, cy);

        if (diagnostico)
        {
            Debug.Log("[Mano] desequipo " + pieza.name
                + (guardada ? " -> guardada en " + zona.name : " -> suelta"), pieza);
        }
    }

    /// <summary>
    /// Pasa lo que tenés agarrado al socket de la mano: deja de ser un
    /// cuerpo suelto sostenido por un joint y pasa a ser parte de la
    /// mano, rígido. Devuelve false si no se pudo.
    /// </summary>
    private bool Equipar()
    {
        if (socketMano == null || Agarrado == null)
        {
            return false;
        }

        Modulo pieza = Agarrado.GetComponent<Modulo>();
        if (pieza == null)
        {
            // El caso más difícil de ver: la pieza parece bien armada
            // pero le falta el componente, así que ninguna Zona la
            // acepta y no pasa nada. Ni error ni nada. Mejor cantarlo.
            Debug.LogWarning("[Mano] " + Agarrado.name + " no tiene " +
                "componente Modulo, no se puede equipar ni montar. " +
                "Agregáselo al mismo objeto que el Rigidbody2D.", Agarrado);
            return false;
        }

        // Preguntar ANTES de soltar el agarre: si no entra, queremos
        // seguir sosteniéndola, no que se nos caiga por haber
        // desarmado el joint para nada.
        if (!socketMano.Cabe(pieza, 0, 0))
        {
            return false;
        }

        Enchufar(socketMano, pieza, 0, 0);
        if (diagnostico)
        {
            Debug.Log("[Mano] equipo " + pieza.name, pieza);
        }
        return true;
    }

    /// <summary>
    /// Ancla lo que tenés agarrado en el socket más cercano que lo
    /// acepte, dentro de radioMontaje. Devuelve false si no hay ninguno.
    /// </summary>
    private bool MontarCerca()
    {
        if (Agarrado == null)
        {
            return false;
        }

        Modulo pieza = Agarrado.GetComponent<Modulo>();
        if (pieza == null)
        {
            return false;
        }

        if (!Zona.MejorCelda(pieza, Agarrado.position, radioMontaje,
                out Zona zona, out int cx, out int cy))
        {
            return false;
        }

        Enchufar(zona, pieza, cx, cy);
        if (diagnostico)
        {
            Debug.Log("[Mano] anclo " + pieza.name + " en " + zona.name, zona);
        }
        return true;
    }

    /// <summary>
    /// Deshace el agarre físico y monta la pieza. El orden importa: el
    /// joint y el filtro de colisiones tienen que desarmarse ANTES de
    /// que la pieza deje de ser un cuerpo simulado.
    /// </summary>
    private void Enchufar(Zona zona, Modulo pieza, int cx, int cy)
    {
        SoltarJuntas();

        zona.Montar(pieza, cx, cy);
        Agarrado = null;

        // La pieza ahora cuelga de la nave, así que la lista de
        // colliders del casco cambió. Si la dejáramos cacheada,
        // el próximo agarre filtraría contra una lista vieja.
        _colsNave = null;
    }

    private void FixedUpdate()
    {
        if (Agarrado == null || brazo == null)
        {
            return;
        }

        // Agarre rígido: no hay nada que corregir frame a frame, la
        // posición y la rotación las resuelve el solver.
        if (_rigida != null)
        {
            // El brazo sigue al mouse normal, y lo que movemos es el
            // punto donde la junta está atada DEL LADO DE LA NAVE.
            //
            // Eso deja que el solver decida quién cede: con algo
            // liviano, la pieza va detrás de la mano; con un asteroide
            // pesado, la que se corre es la nave. Las dos cosas salen
            // de la misma cuenta, sin casos especiales.
            //
            // Clavarle el AimOverride al brazo sería lo contrario:
            // quedaría rígido pero inmóvil, que es lo que pasaba antes.
            _rigida.anchor = nave.transform.InverseTransformPoint(brazo.ManoGlobal);
            return;
        }

        // Si la junta rígida se pasó de Fuerza Ruptura, Unity la
        // destruye sola y nos deja "agarrando" algo sin nada que lo
        // sostenga. Hay que enterarse y soltar de verdad.
        if (_junta == null)
        {
            Soltar();
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

        // Control de giro. Con "pegar orientación" es un resorte
        // angular: corrige hacia el ángulo con el que la agarraste.
        // Sin eso, solo frena, y la pieza queda colgando del punto de
        // agarre como un péndulo.
        //
        // La parte del error es el resorte y la de la velocidad el
        // amortiguador — las dos juntas son un PD. Sin la segunda
        // oscila alrededor del ángulo bueno y no para nunca.
        float torque = 0f;
        if (pegarOrientacion)
        {
            // DeltaAngle y no una resta: toma el camino corto y no se
            // manda una vuelta entera cuando el error cruza los 180º
            float error = Mathf.DeltaAngle(Agarrado.rotation, AnguloMano() + _offsetAngulo);
            torque += error * rigidezGiro;
        }
        // mide la velocidad angular de la PIEZA, no la del punto
        // agarrado: una pieza girando justo alrededor de ese punto
        // daría cero ahí y no se frenaría nunca
        torque -= Agarrado.angularVelocity * frenoGiro;

        if (torque != 0f)
        {
            Agarrado.AddTorque(Mathf.Clamp(torque, -torqueMaximo, torqueMaximo));
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

        if (diagnostico)
        {
            Debug.Log("[Mano] agarro " + Agarrado.name
                + " | mano " + mano
                + " | pieza " + Agarrado.position
                + " | montado? " + (montado != null), Agarrado);
        }

        // el punto exacto donde tocaste, en LOCAL de la pieza: si rota,
        // el agarre rota con ella, que es lo que uno espera
        _anclaLocal = Agarrado.transform.InverseTransformPoint(mano);

        if (agarreRigido && nave != null)
        {
            // FixedJoint2D clava las dos posiciones Y la rotación
            // relativa, y lo hace entre DOS cuerpos: el solver reparte
            // según las masas. Por eso con un asteroide pesado te
            // arrastra a vos — no hay nada que "ceda" en el medio.
            _rigida = nave.gameObject.AddComponent<FixedJoint2D>();
            _rigida.autoConfigureConnectedAnchor = false;
            _rigida.connectedBody = Agarrado;
            _rigida.anchor = nave.transform.InverseTransformPoint(mano);
            _rigida.connectedAnchor = _anclaLocal;
            // 0 Hz = sin resorte, unión dura. Con un valor > 0 se
            // vuelve elástica y volvemos al problema de antes.
            _rigida.frequency = 0f;
            _rigida.dampingRatio = 1f;
            _rigida.enableCollision = false;
            if (fuerzaRuptura > 0f)
            {
                _rigida.breakForce = fuerzaRuptura;
            }
        }
        else
        {
            _junta = Agarrado.gameObject.AddComponent<TargetJoint2D>();
            _junta.autoConfigureTarget = false;
            _junta.anchor = _anclaLocal;
            _junta.target = mano;
            _junta.frequency = frecuencia;
            _junta.dampingRatio = amortiguacion;
            _junta.maxForce = fuerzaMaxima;
        }

        _offsetAngulo = Mathf.DeltaAngle(AnguloMano(), Agarrado.rotation);

        IgnorarContraLaNave(true);
    }

    /// <summary>
    /// Prende o apaga la colisión entre lo que tenemos agarrado y la
    /// nave. Solo mientras está en la mano: suelta, la pieza vuelve a
    /// chocar con todo como cualquier objeto físico.
    /// </summary>
    private void IgnorarContraLaNave(bool ignorar)
    {
        if (!atraviesaLaNave || nave == null)
        {
            return;
        }

        if (ignorar)
        {
            if (_colsNave == null)
            {
                _colsNave = nave.GetComponentsInChildren<Collider2D>(true);
            }

            _ignorados.Clear();
            foreach (Collider2D c in Agarrado.GetComponentsInChildren<Collider2D>(true))
            {
                _ignorados.Add(c);
                foreach (Collider2D n in _colsNave)
                {
                    Physics2D.IgnoreCollision(c, n, true);
                }
            }
            return;
        }

        foreach (Collider2D c in _ignorados)
        {
            // el collider pudo haberse destruido mientras lo sostenías
            if (c == null)
            {
                continue;
            }
            foreach (Collider2D n in _colsNave)
            {
                if (n != null)
                {
                    Physics2D.IgnoreCollision(c, n, false);
                }
            }
        }
        _ignorados.Clear();
    }

    private void Soltar()
    {
        SoltarJuntas();

        // Soltar el clic SIEMPRE suelta la pieza y nada más. Montar es
        // trabajo exclusivo de la tecla, con su tiempo de carga: si
        // acá también se montara, bastaría con largar el clic antes de
        // que se llene la barra para saltearse la espera, y la barra
        // pasaría a ser decorativa.
        if (diagnostico && Agarrado != null)
        {
            Debug.Log("[Mano] suelto " + Agarrado.name
                + " libre en " + Agarrado.position, Agarrado);
        }

        // No se le toca la velocidad: sale despedida
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
        // naranja: hasta dónde llega la mano para agarrar
        Gizmos.color = new Color(1f, 0.75f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(brazo.ManoGlobal, radioAgarre);

        // celeste: desde dónde una celda se "come" la pieza al soltarla.
        // Si este círculo te tapa media nave, está de más — por eso
        // conviene verlo y no adivinarlo.
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.5f);
        Vector2 desde = Agarrado != null ? Agarrado.position : brazo.ManoGlobal;
        Gizmos.DrawWireSphere(desde, radioMontaje);
    }
}
