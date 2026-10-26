using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// La mano: agarra con clic sostenido, monta y equipa con la tecla.
///
/// El agarre es un RESORTE con amortiguador aplicado EN EL PUNTO que
/// tocaste, calculado a mano en vez de con un Joint de Unity. Los
/// joints no dejan tocar la masa efectiva ni los topes de integración,
/// y sin eso el agarre se vuelve inestable con cualquier cosa pesada.
///
/// De ese resorte sale todo lo demás, sin casos especiales:
///   - lo pesado cuesta, por F = m·a
///   - la pieza PIVOTEA, porque la fuerza va con palanca
///   - la nave se mueve, porque la reacción es literalmente -F
///   - sale despedida al soltar, porque ya trae velocidad real
///
/// Es el mismo algoritmo de gancho.gd del proyecto en Godot, con las
/// tres correcciones que costaron encontrar: masa efectiva en el punto
/// agarrado, rigidez acotada al paso de física, y recortar el resorte
/// sin recortar el amortiguador.
///
/// La tecla (R por defecto, porque E pasó a ser el giro de la nave)
/// es un mantener con tiempo de carga, y hace
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

    [Tooltip("Los efectos de sonido. Vacío = los busca en la escena. " +
        "Sin ninguno, la mano trabaja muda.")]
    [SerializeField] private Sonidos sonidos;

    [Header("Detección")]
    [Tooltip("Si la mano no toca nada justo, agarra lo más cercano " +
        "dentro de este radio.")]
    [SerializeField] private float radioAgarre = 0.2f;

    [SerializeField] private LayerMask capasAgarrables = ~0;

    [Header("Agarre")]
    [Tooltip("Rigidez del resorte: fuerza por unidad de separación. " +
        "Es FIJA a propósito — como F = m·a, la misma fuerza mueve " +
        "poco a algo pesado y mucho a algo liviano. O sea que \"los " +
        "pesados cuestan\" sale solo de la masa, sin simular nada.\n\n" +
        "Igual se recorta sola a lo que el paso de física aguanta, " +
        "así que ponerla muy alta no rompe nada: simplemente no se " +
        "usa entera.")]
    [SerializeField] private float rigidez = 5000f;

    [Tooltip("Tope de la fuerza del resorte, para que una separación " +
        "grande no genere un número absurdo y mande la pieza a la " +
        "estratósfera.")]
    [SerializeField] private float fuerzaMaxima = 200f;

    [Header("Orientación")]
    [Tooltip("Congelar la rotación de lo que agarres. La pieza no " +
        "puede girar y punto, no hay nada que ajustar. Al soltar se " +
        "le devuelven las restricciones que tenía.")]
    [SerializeField] private bool congelarGiro = true;

    [Tooltip("Si NO congelás el giro: cuánto resiste la mano a que la " +
        "pieza le gire entre los dedos. Hace falta aparte del " +
        "amortiguador porque una pieza que gira justo alrededor del " +
        "punto de agarre lo tiene quieto, y el freno no la ve.")]
    [SerializeField] private float frenoGiro = 8f;

    [Header("Reacción sobre la nave")]
    [Tooltip("Cuánto de la reacción se le devuelve a la nave. En 1 es " +
        "la tercera ley entera: agarrás un asteroide pesado y te " +
        "arrastra a vos. Bajalo si se sacude feo.")]
    [Range(0f, 1f)]
    [SerializeField] private float reaccionNave = 1f;

    [Tooltip("Tope del tirón sobre la nave, en newtons.")]
    [SerializeField] private float tironMaximo = 2000f;

    [Tooltip("Cuánto de la reacción se convierte en GIRO de la nave. " +
        "Entero la descontrola: la nave no tiene amortiguación " +
        "angular y el giro se acumula sin freno.")]
    [Range(0f, 1f)]
    [SerializeField] private float torqueReaccion = 0.12f;

    [Tooltip("Cuánto más allá del largo del brazo se puede ir el punto " +
        "agarrado antes de que la mano no dé más y suelte. Se mide " +
        "desde el hombro: la mano ya no se despega de la pieza.")]
    [SerializeField] private float distanciaRuptura = 0.5f;

    [Header("Equipar")]
    [Tooltip("La Zona de la mano, donde se enchufa la herramienta al " +
        "equiparla. Tiene que ser hija del objeto Mano (para que la " +
        "siga) y conviene dejarle Automatico APAGADO, así solo se " +
        "equipa a propósito con la tecla.")]
    [SerializeField] private Zona socketMano;

    [Tooltip("Mantenela apretada mientras hacés clic sobre una " +
        "herramienta y en vez de agarrarla, te la calzás.")]
    [SerializeField] private Key teclaEquipar = Key.R;

    [Tooltip("Cuántos segundos hay que mantener la tecla para que se " +
        "abra la RUEDA de opciones. Soltarla antes hace directo la " +
        "primera opción, que es lo que la mano hacía sola. 0 = la " +
        "rueda se abre al toque y no hay atajo corto.")]
    [SerializeField] private float tiempoEquipar = 0.6f;

    [Header("Rueda de opciones")]
    [Tooltip("Cuántos píxeles hay que alejar el mouse de la mano para " +
        "que la rueda tome una opción. Adentro de ese círculo no hay " +
        "nada elegido, y soltar la tecla ahí cancela sin hacer nada.")]
    [SerializeField] private float zonaMuertaRueda = 45f;

    [Tooltip("La cámara con la que se pasa la mano a coordenadas de " +
        "pantalla, para saber desde dónde se mide la dirección del " +
        "mouse. Vacío = Camera.main.")]
    [SerializeField] private Camera camara;

    [Header("Montaje")]
    [Tooltip("Al soltar un Modulo, si hay una celda libre que lo " +
        "acepte a menos de esta distancia, se enchufa sola. Ponelo " +
        "parecido al Cell Size de las zonas.")]
    [SerializeField] private float radioMontaje = 0.3f;

    [Tooltip("Mientras tengas agarrado un MÓDULO o herramienta, que " +
        "atraviese la nave en vez de chocarla — si no, lo empujás " +
        "contra tu propio casco y se sacuden. Las rocas y todo lo demás " +
        "chocan siempre: si las atravesara, un asteroide te arrastraría " +
        "a través del casco y quedarías encimado.")]
    [SerializeField] private bool atraviesaLaNave = true;

    [Tooltip("Escupe por Consola qué agarra, qué suelta y dónde se " +
        "monta. Para cuando algo se comporta raro y no se ve por qué.")]
    [SerializeField] private bool diagnostico = false;

    /// <summary>Lo que tenemos agarrado ahora, o null.</summary>
    public Rigidbody2D Agarrado { get; private set; }

    /// <summary>La nave a la que está pegada esta mano.</summary>
    public Rigidbody2D Nave => nave;

    /// <summary>Hacia dónde mira la mano (sin normalizar).</summary>
    public Vector2 Mira => brazo != null ? brazo.Mira : Vector2.zero;

    /// <summary>¿Hay algo calzado en el socket de la mano?</summary>
    public bool TieneEquipado => socketMano != null && socketMano.Montado() != null;

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

    /// <summary>¿Está abierta la rueda de opciones? Para la UI.</summary>
    public bool RuedaAbierta { get; private set; }

    /// <summary>
    /// Lo que ofrece la rueda ahora mismo. Se arma al abrirla y NO se
    /// rearma mientras está abierta: si las opciones cambiaran de orden
    /// bajo el mouse, terminarías eligiendo una cosa distinta de la que
    /// estabas mirando.
    /// </summary>
    public IReadOnlyList<AccionMano> Opciones => _opciones;

    /// <summary>Qué opción estás apuntando, o -1 si estás en la zona muerta.</summary>
    public int Seleccion { get; private set; } = -1;

    /// <summary>Dónde cae la mano en la pantalla, en píxeles. El centro de la rueda.</summary>
    public Vector2 CentroRueda { get; private set; }

    private readonly List<AccionMano> _opciones = new List<AccionMano>();

    // Hacia dónde apuntaba el mouse cuando se abrió la rueda. Mientras
    // elegís, el resorte del agarre tira hacia ACÁ y no hacia el mouse:
    // si no, mover el mouse para elegir te arrastraría la pieza por
    // media pantalla.
    private Vector2 _intencionCongelada;
    private bool _aimCongelado;

    private Vector2 _anclaLocal;
    private DistanceJoint2D _limite;
    private float _cargaTecla;
    private bool _esperaSoltar;
    private bool _giroCongelado;
    private RigidbodyConstraints2D _restriccionesPrevias;

    /// <summary>
    /// Deshace el agarre: le devuelve a la pieza las restricciones que
    /// traía, le devuelve el brazo al mouse y vuelve a habilitar las
    /// colisiones contra la nave. Todo lo que suelta pasa por acá.
    /// </summary>
    private void SoltarJuntas()
    {
        if (_giroCongelado && Agarrado != null)
        {
            Agarrado.constraints = _restriccionesPrevias;
            _giroCongelado = false;
        }
        if (brazo != null)
        {
            brazo.AimOverride = null;
            brazo.Ignorar = null;
        }
        if (_limite != null)
        {
            Destroy(_limite);
            _limite = null;
        }
        IgnorarContraLaNave(false);
    }

    /// <summary>
    /// El largo máximo del brazo, como una restricción dura del motor de
    /// física: el punto agarrado nunca puede quedar más lejos del hombro
    /// que lo que mide el brazo.
    ///
    /// El resorte maneja todo lo que pasa adentro de ese alcance — empujar,
    /// traer, acomodar — pero tiene topes, y cuando la nave y la pieza
    /// vienen las dos en movimiento las inercias le ganan y se separan.
    /// Esto es lo que lo impide: al estirarse del todo, el motor reparte
    /// según las masas y uno arrastra al otro.
    ///
    /// Es una cuerda, no una barra (maxDistanceOnly): no hace nada mientras
    /// estén cerca, y se pone tensa solo al llegar al tope. Y sus dos
    /// anclajes son FIJOS —el hombro en la nave, el punto en la pieza—,
    /// que es lo que la diferencia de la junta rígida que probamos antes:
    /// esa movía el anclaje cada paso y por eso se sacudía.
    /// </summary>
    private void CrearLimite()
    {
        if (nave == null || brazo == null || Agarrado == null)
        {
            return;
        }

        Vector2 hombro = brazo.Hombro;
        Vector2 punto = Agarrado.transform.TransformPoint(_anclaLocal);

        _limite = nave.gameObject.AddComponent<DistanceJoint2D>();
        _limite.autoConfigureConnectedAnchor = false;
        _limite.autoConfigureDistance = false;
        _limite.connectedBody = Agarrado;
        _limite.anchor = nave.transform.InverseTransformPoint(hombro);
        _limite.connectedAnchor = _anclaLocal;
        _limite.maxDistanceOnly = true;

        // Por defecto un joint apaga las colisiones entre sus dos
        // cuerpos. Acá NO: la roca tiene que seguir chocando con el
        // casco, si no te arrastraría a través de él.
        _limite.enableCollision = true;

        // Si lo agarraste con el radio de cortesía, el punto puede estar
        // un poco más lejos que el largo del brazo. Arrancar ahí evita
        // un tirón al agarrar; después se va ajustando (ver FixedUpdate).
        _limite.distance = Mathf.Max(brazo.AlcanceMax, Vector2.Distance(hombro, punto));
    }

    private bool QuiereEquipar()
    {
        return Keyboard.current != null && Keyboard.current[teclaEquipar].isPressed;
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

        // Con la rueda abierta el clic no hace nada: soltarlo acá
        // dejaba caer la herramienta antes de que la R eligiera.
        if (RuedaAbierta)
        {
        }
        else if (Mouse.current.leftButton.wasPressedThisFrame)
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

        Herramienta calzada = HerramientaCalzada();
        brazo.AlcanceExtra = LoQueSobresale(calzada);
        brazo.RadioExtra = calzada != null ? calzada.Grosor : 0f;
    }

    private Herramienta HerramientaCalzada()
    {
        Modulo calzada = socketMano != null ? socketMano.Montado() : null;
        if (calzada != null && calzada.TryGetComponent(out Herramienta h))
        {
            return h;
        }
        return null;
    }

    /// <summary>
    /// Cuánto sobresale hacia adelante la punta de la herramienta calzada,
    /// medido a lo largo del antebrazo. El brazo lo usa para frenar antes:
    /// así la que no atraviesa las rocas es la punta, no solo la mano.
    /// </summary>
    private float LoQueSobresale(Herramienta h)
    {
        if (h == null || h.Punta == null)
        {
            return 0f;
        }

        Vector2 mira = brazo.Mira;
        if (mira.sqrMagnitude < 0.000001f)
        {
            return 0f;
        }
        // proyectado sobre hacia donde mira la mano: si la herramienta va
        // un poco ladeada, lo que cuenta es cuánto adelanta, no la distancia
        Vector2 hastaPunta = h.PosicionPunta - brazo.ManoGlobal;
        return Mathf.Max(0f, Vector2.Dot(hastaPunta, mira.normalized));
    }

    /// <summary>
    /// La tecla tiene dos gestos en vez de uno:
    ///
    ///   - TOQUE corto: hace al toque la primera opción, que es
    ///     exactamente lo que la mano venía haciendo sola. El que ya
    ///     tenía el gesto en los dedos no nota ningún cambio.
    ///   - MANTENER: a los tiempoEquipar segundos abre la rueda,
    ///     elegís moviendo el mouse, y al soltar se hace eso.
    ///
    /// Esto es lo que saca el doble apretón. Antes, con una herramienta
    /// agarrada, equipar y guardar eran la misma tecla dos veces
    /// seguidas: la mano elegía sola y siempre probaba equipar primero,
    /// así que para guardar había que equipar de puro trámite. Ahora
    /// las dos están en la rueda y elegís cuál.
    /// </summary>
    private void CargarTecla()
    {
        if (!QuiereEquipar())
        {
            // Soltaste. Si la rueda alcanzó a abrirse, se hace lo
            // elegido; si no, fue un toque corto y va la primera.
            if (RuedaAbierta)
            {
                CerrarRueda(true);
            }
            else if (!_esperaSoltar && _cargaTecla > 0f)
            {
                ConstruirOpciones();
                EjecutarIndice(0);
            }

            _cargaTecla = 0f;
            _esperaSoltar = false;
            return;
        }

        // Ya se completó una acción y seguís sin soltar la tecla: no
        // encadenamos otra. Sin esto equipás, y como la mano pasa a
        // estar vacía con algo puesto, arranca sola a desequipar.
        if (_esperaSoltar)
        {
            return;
        }

        if (RuedaAbierta)
        {
            ActualizarSeleccion();
            return;
        }

        if (!HayAccionDeTecla())
        {
            _cargaTecla = 0f;
            return;
        }

        _cargaTecla += Time.deltaTime;
        if (tiempoEquipar > 0f && _cargaTecla < tiempoEquipar)
        {
            return;
        }

        AbrirRueda();
    }

    /// <summary>
    /// Arma la lista y abre la rueda. Si no hay nada que ofrecer no se
    /// abre: una rueda vacía es peor que nada, parece que se colgó.
    /// </summary>
    private void AbrirRueda()
    {
        ConstruirOpciones();
        if (_opciones.Count == 0)
        {
            _cargaTecla = 0f;
            return;
        }

        // El aro de progreso ya cumplió su parte: de acá en adelante
        // manda la rueda, y dejarlo lleno abajo solo ensucia.
        _cargaTecla = 0f;
        RuedaAbierta = true;
        Seleccion = -1;

        if (brazo != null)
        {
            _intencionCongelada = brazo.Intencion;

            // Con algo agarrado, el brazo ya va clavado al punto de
            // agarre: eso lo hace FixedUpdate solo, y alcanza con
            // congelar la intención de arriba. Con la mano vacía, en
            // cambio, el brazo sigue al mouse y hay que pararlo, si no
            // se te va de paseo mientras elegís.
            if (Agarrado == null)
            {
                brazo.AimOverride = brazo.ManoGlobal;
                _aimCongelado = true;
            }
        }

        ActualizarSeleccion();
    }

    /// <summary>
    /// Cierra la rueda y, si corresponde, hace lo elegido. Con la
    /// selección en -1 (mouse en el centro) no se hace nada: esa es la
    /// forma de arrepentirse sin soltar lo que tenés.
    /// </summary>
    private void CerrarRueda(bool ejecutar)
    {
        if (ejecutar && Seleccion >= 0)
        {
            EjecutarIndice(Seleccion);
        }

        RuedaAbierta = false;
        Seleccion = -1;

        if (_aimCongelado)
        {
            // Solo se devuelve si lo congelamos NOSOTROS y la mano
            // sigue vacía. Si en el medio se agarró algo, el
            // AimOverride pasó a ser del agarre y ponerlo en null acá
            // le despegaría la mano de la pieza por un frame.
            if (brazo != null && Agarrado == null)
            {
                brazo.AimOverride = null;
            }
            _aimCongelado = false;
        }
    }

    /// <summary>
    /// Qué opción estás apuntando. El centro se recalcula todos los
    /// frames en vez de fijarlo al abrir: la nave se mueve, y si el
    /// centro quedara clavado en pantalla la rueda se despegaría de la
    /// mano y estarías eligiendo contra una referencia que ya no está.
    /// </summary>
    private void ActualizarSeleccion()
    {
        Camera cam = camara != null ? camara : Camera.main;
        Vector2 manoMundo = brazo != null ? brazo.ManoGlobal : (Vector2)transform.position;

        CentroRueda = cam != null
            ? (Vector2)cam.WorldToScreenPoint(manoMundo)
            : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        Vector2 mouse = Mouse.current != null
            ? Mouse.current.position.ReadValue()
            : CentroRueda;

        Vector2 dir = mouse - CentroRueda;
        Seleccion = dir.magnitude < zonaMuertaRueda
            ? -1
            : Rueda.IndiceDesde(dir, _opciones.Count);
    }

    private void EjecutarIndice(int i)
    {
        if (i < 0 || i >= _opciones.Count)
        {
            return;
        }

        _opciones[i].hacer?.Invoke();
        _esperaSoltar = true;
    }

    /// <summary>
    /// Qué puede hacer la tecla ahora mismo. El orden no es decorativo:
    /// la primera es la que sale con el toque corto, y se deja adelante
    /// a propósito la que la mano hacía sola antes de que existiera la
    /// rueda, para no romperle el gesto a nadie.
    /// </summary>
    private void ConstruirOpciones()
    {
        _opciones.Clear();

        if (Agarrado != null)
        {
            Modulo pieza = Agarrado.GetComponent<Modulo>();
            if (pieza == null)
            {
                // Sin componente Modulo no entra en ningún lado, pero
                // soltarlo siempre se puede. El warning de por qué no
                // entra ya lo tira Equipar().
                _opciones.Add(new AccionMano("Soltar", "", Soltar));
                return;
            }

            if (socketMano != null && socketMano.Cabe(pieza, 0, 0))
            {
                _opciones.Add(new AccionMano("Equipar", "a la mano", () => Equipar()));
            }

            if (Zona.MejorCelda(pieza, Agarrado.position, radioMontaje,
                    out Zona zona, out int cx, out int cy)
                && zona != socketMano)
            {
                // Se guarda la celda resuelta ACÁ en vez de volver a
                // buscarla al ejecutar. Si se volviera a buscar, la
                // mejor celda podría terminar siendo la de la mano y
                // "Guardar" te equiparía la pieza: justo lo contrario
                // de lo que elegiste.
                Zona destino = zona;
                int dx = cx;
                int dy = cy;
                _opciones.Add(new AccionMano("Guardar", destino.name,
                    () => MontarEn(destino, dx, dy)));
            }

            _opciones.Add(new AccionMano("Soltar", "", Soltar));
            return;
        }

        if (TieneEquipado)
        {
            Modulo puesta = socketMano.Montado();

            // Declaradas antes del && y no con "out Zona z": si el
            // cortocircuito saltea la llamada, el compilador no puede
            // garantizar que queden asignadas y no las deja usar abajo.
            Zona z = null;
            int zx = 0;
            int zy = 0;
            bool hayDonde = puesta != null
                && Zona.MejorCelda(puesta, puesta.transform.position, radioMontaje,
                    out z, out zx, out zy)
                && z != socketMano;

            if (hayDonde)
            {
                // La celda se resuelve ACÁ y se la lleva el Action, igual
                // que en el camino de arriba. Antes solo servía para
                // decidir si mostrar la opción, y Desequipar la volvía a
                // buscar al ejecutar: para entonces la mano ya se movió
                // al cerrarse la rueda, el slot queda fuera de
                // radioMontaje, y la herramienta terminaba suelta en vez
                // de guardada.
                Zona destino = z;
                int dx = zx;
                int dy = zy;
                _opciones.Add(new AccionMano("Guardar", destino.name,
                    () => Desequipar(true, destino, dx, dy)));
            }

            _opciones.Add(new AccionMano("Soltar", "queda flotando",
                () => Desequipar(false)));
        }
    }

    /// <summary>
    /// Monta lo agarrado en una celda que ya se eligió, sin volver a
    /// buscar cuál es la mejor.
    /// </summary>
    private void MontarEn(Zona zona, int cx, int cy)
    {
        if (Agarrado == null || zona == null)
        {
            return;
        }

        Modulo pieza = Agarrado.GetComponent<Modulo>();
        if (pieza == null || !zona.Cabe(pieza, cx, cy))
        {
            // Se eligió hace medio segundo; si justo en el medio dejó
            // de entrar, mejor caer al camino de siempre que no hacer
            // nada y que parezca que la tecla no respondió.
            MontarCerca();
            return;
        }

        Enchufar(zona, pieza, cx, cy);
        if (diagnostico)
        {
            Debug.Log("[Mano] guardo " + pieza.name + " en " + zona.name, zona);
        }
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
    /// Saca lo que tengas equipado.
    ///
    /// Con <paramref name="guardar"/> en true busca un socket cerca y
    /// lo deja ahí si lo encuentra; en false lo suelta siempre, aunque
    /// haya dónde guardarlo. Esa es la diferencia entre las dos
    /// opciones que ofrece la rueda con algo puesto.
    /// </summary>
    /// <param name="destino">
    /// La celda ya elegida, si viene de la rueda. Manda sobre volver a
    /// buscar la mejor: entre que elegís la opción y soltás la tecla, la
    /// mano se mueve, y una búsqueda nueva puede no encontrar el slot
    /// que tenías enfrente cuando elegiste. Null = buscar ahora, que es
    /// lo que corresponde cuando no hubo elección previa.
    /// </param>
    private void Desequipar(bool guardar = true, Zona destino = null,
        int cx = 0, int cy = 0)
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

        Zona zona = null;
        bool guardada = false;
        if (guardar)
        {
            if (destino != null && destino != socketMano && destino.Cabe(pieza, cx, cy))
            {
                zona = destino;
                guardada = destino.Montar(pieza, cx, cy);
            }
            else if (Zona.MejorCelda(pieza, pieza.transform.position, radioMontaje,
                         out Zona candidata, out int bx, out int by)
                     && candidata != socketMano)
            {
                // sin celda elegida, o la elegida dejó de entrar en el
                // medio: se busca la mejor ahora y listo
                zona = candidata;
                guardada = candidata.Montar(pieza, bx, by);
            }
        }

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

        // Acá y no en MontarEn: Enchufar es por donde pasan todos los
        // caminos que terminan con una pieza en su lugar (la rueda, el
        // montaje automático, guardar lo que tenías puesto).
        if (sonidos != null)
        {
            sonidos.Encajar();
        }

        // La pieza ahora cuelga de la nave, así que la lista de
        // colliders del casco cambió. Si la dejáramos cacheada,
        // el próximo agarre filtraría contra una lista vieja.
        _colsNave = null;
    }

    /// <summary>
    /// El agarre es un RESORTE ENTRE DOS CUERPOS: la pieza y la nave,
    /// unidos por el brazo. No un resorte que tira de la pieza hacia un
    /// punto fijo del mundo.
    ///
    /// La diferencia es lo que hace que agarrar algo pesado no te tire
    /// la nave a la mierda:
    ///
    ///   - el amortiguador frena la velocidad RELATIVA (pieza menos
    ///     nave, en el punto de agarre). Si frenara solo la de la pieza,
    ///     a la nave le llegaría la reacción sin ningún freno: empujones
    ///     sin amortiguar, que con algo pesado se van acumulando.
    ///
    ///   - los topes de rigidez y freno salen de la MASA REDUCIDA del
    ///     par, 1/(1/m_pieza + 1/m_nave). Con un asteroide enorme queda
    ///     ≈ la masa de la nave, así que los topes se ajustan solos a lo
    ///     que la nave aguanta. Con algo liviano queda ≈ la de la pieza.
    ///
    /// Y como en Godot: la mano se queda CLAVADA en el punto agarrado,
    /// y lo que tironea es la INTENCIÓN (el mouse) contra ese punto.
    /// </summary>
    private void FixedUpdate()
    {
        if (Agarrado == null || brazo == null)
        {
            return;
        }

        float dt = Time.fixedDeltaTime;
        if (dt <= 0f)
        {
            return;
        }

        Vector2 punto = Agarrado.transform.TransformPoint(_anclaLocal);

        // la mano queda pegada donde agarraste, no sigue al mouse
        brazo.AimOverride = punto;

        // La intención se recorta al alcance del brazo: un brazo no
        // puede tironear más allá de lo que mide. Sin esto, alejar el
        // mouse a la otra punta de la pantalla tiraba con fuerza máxima.
        Vector2 hombro = brazo.Hombro;
        float alcance = brazo.AlcanceMax;
        // Mientras la rueda está abierta el resorte tira hacia donde
        // estaba el mouse al abrirla, no hacia donde está ahora: si
        // siguiera al mouse de verdad, moverlo para elegir te
        // arrastraría la pieza por media pantalla.
        Vector2 intencion = RuedaAbierta ? _intencionCongelada : brazo.Intencion;
        Vector2 desdeHombro = intencion - hombro;
        if (desdeHombro.magnitude > alcance)
        {
            intencion = hombro + desdeHombro.normalized * alcance;
        }

        // El límite del brazo se va cerrando como un trinquete: si
        // arrancó flojo (agarre de cortesía), cada vez que la pieza se
        // acerca el tope baja a esa distancia, hasta quedar en el largo
        // real del brazo. Nunca se vuelve a aflojar.
        float distancia = Vector2.Distance(punto, hombro);
        if (_limite != null)
        {
            _limite.distance = Mathf.Max(alcance, Mathf.Min(_limite.distance, distancia));
        }

        // Red de seguridad: con el límite puesto esto no debería pasar
        // nunca. Si pasa (sin nave, o algo se trabó), suelta antes que
        // dejar un agarre roto.
        if (distancia > alcance + distanciaRuptura)
        {
            Soltar();
            return;
        }

        // --- la pieza, en el punto agarrado ---
        Vector2 rPieza = punto - Agarrado.worldCenterOfMass;
        Vector2 vPieza = VelocidadEn(Agarrado, rPieza);
        float mPieza = MasaEfectiva(Agarrado, rPieza);

        // --- la nave, en el MISMO punto: ahí está apoyada la mano ---
        Vector2 vNave = Vector2.zero;
        float mRed = mPieza;
        if (nave != null)
        {
            Vector2 rNave = punto - nave.worldCenterOfMass;
            vNave = VelocidadEn(nave, rNave);
            float mNave = MasaEfectiva(nave, rNave);
            mRed = 1f / (1f / mPieza + 1f / mNave);
        }

        // Rigidez acotada a lo que el paso de física aguanta: un resorte
        // demasiado rígido para el dt se pasa de largo y rebota cada vez
        // más fuerte.
        float k = Mathf.Min(rigidez, mRed / (dt * dt) * 0.1f);

        // crítico para esa masa, y nunca más fuerte de lo que el paso de
        // física puede resolver
        float c = Mathf.Min(2f * Mathf.Sqrt(k * mRed), mRed / dt * 0.9f);

        // Se recorta SOLO el resorte, nunca la suma: recortar el total
        // se comería el amortiguador justo cuando más falta.
        Vector2 resorte = Vector2.ClampMagnitude((intencion - punto) * k, fuerzaMaxima);
        Vector2 f = resorte - (vPieza - vNave) * c;

        // En el punto agarrado: ahí está la palanca que la hace pivotear.
        Agarrado.AddForceAtPosition(f, punto);

        // El apretón. Aparte del amortiguador porque una pieza que gira
        // justo alrededor del punto agarrado lo tiene quieto: el freno de
        // arriba no la ve y la dejaría girando para siempre.
        float inercia = Agarrado.inertia;
        if (frenoGiro > 0f && inercia > 0f && !_giroCongelado)
        {
            float w = Agarrado.angularVelocity * Mathf.Deg2Rad;
            float tope = inercia / dt * 0.5f;
            Agarrado.AddTorque(Mathf.Clamp(-w * inercia * frenoGiro, -tope, tope));
        }

        TironearNave(f, punto);
    }

    /// <summary>
    /// Velocidad de un punto de un cuerpo: la lineal más lo que aporta
    /// el giro. r va desde el centro de masa.
    ///
    /// angularVelocity de Unity viene en GRADOS/seg: hay que pasarla a
    /// radianes, si no el término de giro sale ~57 veces más grande.
    /// </summary>
    private static Vector2 VelocidadEn(Rigidbody2D rb, Vector2 r)
    {
        float w = rb.angularVelocity * Mathf.Deg2Rad;
        return rb.linearVelocity + w * new Vector2(-r.y, r.x);
    }

    /// <summary>
    /// Cuánto "pesa" un cuerpo empujado desde un punto que no es su
    /// centro: parte del empuje se va en hacerlo girar, así que se mueve
    /// como si pesara menos. Con palanca puede ser 3 o 4 veces menos.
    /// Amortiguar con la masa total en vez de esta es lo que hacía que
    /// la pieza saliera eyectada.
    /// </summary>
    private static float MasaEfectiva(Rigidbody2D rb, Vector2 r)
    {
        float inercia = rb.inertia;
        if (inercia <= 0f)
        {
            return rb.mass;
        }
        return 1f / (1f / rb.mass + r.sqrMagnitude / inercia);
    }

    /// <summary>
    /// Acción y reacción: a la nave le llega la fuerza opuesta a la que
    /// el brazo le hace a la pieza, en el mismo punto. No hay ninguna
    /// fórmula inventada acá — por eso agarrar un asteroide pesado te
    /// arrastra a vos.
    /// </summary>
    private void TironearNave(Vector2 f, Vector2 punto)
    {
        if (nave == null || reaccionNave <= 0f)
        {
            return;
        }

        Vector2 reac = Vector2.ClampMagnitude(-f * reaccionNave, tironMaximo);
        nave.AddForce(reac);

        // El torque va dosificado: la nave no tiene amortiguación
        // angular propia, y entero puede ponerla a girar. Con el freno
        // relativo de arriba aguanta bastante más que antes — probá
        // subirlo si querés que el tirón también te gire.
        Vector2 palanca = punto - nave.worldCenterOfMass;
        nave.AddTorque((palanca.x * reac.y - palanca.y * reac.x) * torqueReaccion);
    }

    /// <summary>
    /// Lo que agarrarías si hicieras clic ahora mismo, sin agarrarlo.
    /// Para resaltar lo que está bajo la mano: usa exactamente la misma
    /// regla que el agarre, así lo resaltado es siempre lo que te vas a
    /// llevar, y no algo parecido.
    /// </summary>
    public Rigidbody2D Candidato()
    {
        if (brazo == null)
        {
            return null;
        }
        Buscar(brazo.ManoGlobal, out Rigidbody2D rb, out _);
        return rb;
    }

    /// <summary>
    /// La regla de qué se agarra. Sin efectos: solo mira.
    /// "montado" sale con valor si lo encontrado está enchufado en una
    /// zona, para que quien agarra sepa que tiene que desmontarlo.
    /// </summary>
    private void Buscar(Vector2 mano, out Rigidbody2D rb, out Modulo montado)
    {
        rb = null;

        // Lo que esté MONTADO no lo encuentra Physics2D (tiene la
        // física apagada), así que se busca aparte y tiene prioridad:
        // si estás con la mano encima de una herramienta enchufada,
        // querés sacar esa, no agarrar lo que haya atrás.
        //
        // Se ignora el socket de la mano: lo equipado está justo encima
        // de la mano, a distancia cero, y le ganaría a todo — no podrías
        // agarrar nada más, y cada clic te lo arrancaría. Lo equipado
        // se saca con la tecla.
        montado = Zona.MontadoCerca(mano, radioAgarre, socketMano);
        if (montado != null && montado.zonaActual != null)
        {
            rb = montado.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                return;
            }
        }
        montado = null;

        // ¿la mano está justo encima de algo? Eso gana siempre, sin
        // importar el tamaño de la pieza
        Collider2D tocado = Physics2D.OverlapPoint(mano, capasAgarrables);
        if (EsAgarrable(tocado))
        {
            rb = tocado.attachedRigidbody;
            return;
        }

        // si no, el radio de cortesía: lo más cercano que haya cerca
        float mejorDist = float.MaxValue;
        foreach (Collider2D c in Physics2D.OverlapCircleAll(mano, radioAgarre, capasAgarrables))
        {
            if (!EsAgarrable(c))
            {
                continue;
            }
            float d = Vector2.Distance(c.ClosestPoint(mano), mano);
            if (d < mejorDist)
            {
                mejorDist = d;
                rb = c.attachedRigidbody;
            }
        }
    }

    private bool EsAgarrable(Collider2D c)
    {
        return c != null && c.attachedRigidbody != null && c.attachedRigidbody != nave;
    }

    private readonly System.Collections.Generic.List<Collider2D> _colsPieza =
        new System.Collections.Generic.List<Collider2D>();

    /// <summary>
    /// El punto del BORDE de la pieza más cercano a la mano. Si la mano
    /// ya está encima de la pieza (pasa con los módulos, que no frenan
    /// al brazo), ClosestPoint devuelve el mismo punto y el agarre queda
    /// donde tocaste.
    /// </summary>
    private Vector2 PuntoDeAgarre(Rigidbody2D rb, Vector2 mano)
    {
        Vector2 mejor = mano;
        float mejorDist = float.MaxValue;

        int n = rb.GetAttachedColliders(_colsPieza);
        for (int i = 0; i < n; i++)
        {
            Collider2D c = _colsPieza[i];
            if (c == null || c.isTrigger)
            {
                continue;
            }
            Vector2 p = c.ClosestPoint(mano);
            float d = (p - mano).sqrMagnitude;
            if (d < mejorDist)
            {
                mejorDist = d;
                mejor = p;
            }
        }
        return mejor;
    }

    private void Awake()
    {
        if (sonidos == null)
        {
            sonidos = GetComponentInParent<Sonidos>();
        }
        if (sonidos == null)
        {
            sonidos = FindAnyObjectByType<Sonidos>();
        }
    }

    private void Agarrar()
    {
        if (Agarrado != null)
        {
            return;
        }

        Vector2 mano = brazo.ManoGlobal;

        Buscar(mano, out Rigidbody2D rb, out Modulo montado);
        if (rb == null)
        {
            return;
        }

        if (montado != null)
        {
            montado.zonaActual.SacarA(montado);
        }
        Agarrado = rb;

        if (sonidos != null)
        {
            sonidos.Agarrar();
        }

        if (diagnostico)
        {
            Debug.Log("[Mano] agarro " + Agarrado.name
                + " | mano " + mano
                + " | pieza " + Agarrado.position
                + " | montado? " + (montado != null), Agarrado);
        }

        // El punto de agarre, en LOCAL de la pieza: si rota, el agarre
        // rota con ella. Va sobre el BORDE de la pieza y no donde está
        // la mano: con una roca, la mano frena antes de tocarla (ver
        // BrazoIK) y el agarre tiene que quedar en la superficie, no
        // flotando en el aire ni hundido adentro.
        _anclaLocal = Agarrado.transform.InverseTransformPoint(PuntoDeAgarre(Agarrado, mano));

        // lo que agarrás no frena al brazo: la mano está pegada a eso
        if (brazo != null)
        {
            brazo.Ignorar = Agarrado;
        }

        CrearLimite();

        // Guardamos las restricciones que traía en vez de asumir que
        // no tenía ninguna: si la pieza venía con algo congelado de
        // fábrica, al soltarla la dejaríamos distinta de como estaba.
        if (congelarGiro)
        {
            _restriccionesPrevias = Agarrado.constraints;
            Agarrado.constraints |= RigidbodyConstraints2D.FreezeRotation;
            _giroCongelado = true;
        }

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
            // solo los módulos: una roca que atraviesa el casco es
            // justamente lo que te deja encimado con ella
            if (Agarrado.GetComponent<Modulo>() == null)
            {
                return;
            }
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
