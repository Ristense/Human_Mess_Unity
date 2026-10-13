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

    [Tooltip("Pasada esta distancia entre la mano y el punto agarrado, " +
        "la mano no da más y suelta.")]
    [SerializeField] private float distanciaRuptura = 0.5f;
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

    private Vector2 _anclaLocal;
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
        }
        IgnorarContraLaNave(false);
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

    /// <summary>
    /// El agarre es un RESORTE con amortiguador, aplicado EN EL PUNTO
    /// que agarraste. Todo lo demás sale gratis de ahí: lo pesado
    /// cuesta (F = m·a), la pieza pivotea (hay palanca), la nave se
    /// mueve (la reacción es literalmente -F) y al soltar sale
    /// despedida con la velocidad que traía.
    ///
    /// Las tres cuentas finas de abajo son las que hacen la diferencia
    /// entre que funcione y que la pieza salga eyectada. Están acá
    /// porque ya nos pasó: son las mismas correcciones que hubo que
    /// hacer en la versión de Godot.
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
        Vector2 destino = brazo.ManoGlobal;

        if (Vector2.Distance(punto, destino) > distanciaRuptura)
        {
            Soltar();
            return;
        }

        // r se mide desde el CENTRO DE MASA, no desde el origen del
        // objeto. Coinciden solo si el collider está centrado; en una
        // herramienta no lo está, y medir desde el origen da una masa
        // efectiva varias veces mayor que la real.
        Vector2 r = punto - Agarrado.worldCenterOfMass;

        // angularVelocity de Unity viene en GRADOS/seg. Para componer
        // la velocidad del punto hay que pasarla a radianes, si no el
        // término de giro sale ~57 veces más grande.
        float w = Agarrado.angularVelocity * Mathf.Deg2Rad;

        // velocidad DEL PUNTO AGARRADO: la lineal más lo que aporta el
        // giro. Hay que amortiguar esta y no la del centro, porque la
        // fuerza se aplica con palanca y hace rotar la pieza.
        Vector2 vPunto = Agarrado.linearVelocity + w * new Vector2(-r.y, r.x);

        // MASA EFECTIVA en el punto agarrado. Tirar del borde no cuesta
        // lo mismo que tirar del centro: parte del esfuerzo se va en
        // hacerla girar, así que "pesa" menos, a veces 3 o 4 veces.
        //
        // Acá está el bug que te eyecta el asteroide: si amortiguás
        // usando la masa TOTAL, el freno queda más fuerte que la
        // inercia que tiene que frenar. Y un freno así no frena —
        // invierte la velocidad y la devuelve más grande cada paso.
        float inercia = Agarrado.inertia;
        float mEff = Agarrado.mass;
        if (inercia > 0f)
        {
            mEff = 1f / (1f / Agarrado.mass + r.sqrMagnitude / inercia);
        }

        // La rigidez se recorta a lo que el paso de física aguanta. Un
        // resorte con frecuencia propia muy alta para el dt no se
        // integra bien: se pasa de largo y rebota cada vez más fuerte.
        float k = Mathf.Min(rigidez, mEff / (dt * dt) * 0.1f);

        // crítico para ESA masa efectiva, y nunca más fuerte de lo que
        // el paso de física puede resolver
        float c = Mathf.Min(2f * Mathf.Sqrt(k * mEff), mEff / dt * 0.9f);

        // Se recorta SOLO el resorte, nunca la suma: recortar el total
        // hace que al pasarse del tope el recorte se coma también el
        // amortiguador, justo cuando más falta hace.
        Vector2 resorte = Vector2.ClampMagnitude((destino - punto) * k, fuerzaMaxima);
        Vector2 f = resorte - vPunto * c;

        // En el punto agarrado, no en el centro: ahí está la palanca
        // que la hace pivotear. Unity toma el punto en MUNDO y calcula
        // el torque solo.
        Agarrado.AddForceAtPosition(f, punto);

        // El apretón. Hace falta aparte porque el amortiguador de
        // arriba mide la velocidad DEL PUNTO, y una pieza que gira
        // justo alrededor de ese punto lo tiene quieto: el freno no ve
        // nada y la deja girando para siempre.
        if (frenoGiro > 0f && inercia > 0f && !_giroCongelado)
        {
            float tope = inercia / dt * 0.5f;
            Agarrado.AddTorque(Mathf.Clamp(-w * inercia * frenoGiro, -tope, tope));
        }

        TironearNave(f, punto);
    }

    /// <summary>
    /// Acción y reacción: a la nave le llega exactamente la fuerza
    /// opuesta a la que el brazo le hace a la pieza. No hay ninguna
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

        // El torque va dosificado: entero descontrola, porque la nave
        // no tiene amortiguación angular y el giro se acumula sin freno.
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
