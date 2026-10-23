using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// El taladro excavador: un cabezal con DOS DISCOS lado a lado, como una
/// rozadora de minería. Calzado en la mano, con el clic DERECHO
/// sostenido va comiendo la roca que tiene delante.
///
/// Por qué dos discos y no una punta: una punta cava una aguja, y la
/// herramienta termina metida en un pozo angosto dentro de la roca. Dos
/// discos comen un FRENTE ANCHO y parejo, y el canal que dejan es del
/// ancho de todo el cabezal. Además las formas redondas dejan bordes
/// limpios, sin astillas.
///
/// El cabezal no atraviesa la roca: el brazo frena con su ancho (ver
/// Herramienta.Grosor), así que no se puede colar en nada más angosto.
///
/// Cuánto come no lo decide el tamaño del cabezal, sino "Mordida": área
/// por segundo. Lo que se corta es lo que barren los dos discos al
/// avanzar hacia adelante, y se busca qué tan lejos pueden barrer sin
/// pasarse de lo que toca comer. Por eso el frente avanza parejo, de
/// lado a lado, y no por el medio.
///
/// Lo que come sale como escombros, y una aspiradora en el cabezal los
/// trae a la bodega. Los kg que entran son exactamente los que salieron
/// de la roca.
///
/// Va en el objeto de la herramienta, junto a su Modulo. La "Punta" es
/// el centro del cabezal, entre los dos discos.
/// </summary>
public class TaladroExcavador : Herramienta
{
    [Header("Cabezal")]
    [Tooltip("Radio de cada disco, en unidades.")]
    [SerializeField] private float radioDisco = 0.03f;

    [Tooltip("Distancia entre los centros de los dos discos. Con el doble " +
        "del radio quedan tocándose.")]
    [SerializeField] private float separacion = 0.06f;

    [Tooltip("Hasta dónde llegan a cortar por delante de los discos.")]
    [SerializeField] private float alcanceCorte = 0.03f;

    [Tooltip("Con cuántos lados se dibuja cada disco para cortar. Más = " +
        "más redondo, pero más vértices en la roca.")]
    [SerializeField] private int lados = 16;

    [Header("Ritmo")]
    [Tooltip("Cuánta área come por segundo, en unidades². Es la " +
        "velocidad del taladro.")]
    [SerializeField] private float mordida = 0.01f;

    [Tooltip("Cada cuántos segundos muerde. Mordiscos más espaciados " +
        "(y más grandes) rearman la roca menos veces: es lo que mantiene " +
        "el juego fluido. 0.1 = 10 veces por segundo.")]
    [SerializeField] private float intervalo = 0.1f;

    [Header("Discos (visual)")]
    [Tooltip("Los sprites de los discos, para que giren mientras cava. " +
        "Opcional.")]
    [SerializeField] private Transform[] discos;

    [Tooltip("Grados por segundo. Cada disco gira para su lado, como en " +
        "una rozadora de verdad.")]
    [SerializeField] private float giroDiscos = 900f;

    [Header("Punta (visual)")]
    [Tooltip("Un sprite suelto que gira sobre sí mismo mientras cavás — " +
        "el de adentro de la máscara de la punta. Va aparte de los " +
        "discos porque ahí el sentido lo decide la posición " +
        "izquierda/derecha, y este va al medio. Opcional.")]
    [SerializeField] private Transform puntaGiratoria;

    [Tooltip("Grados por segundo. En negativo gira para el otro lado.")]
    [SerializeField] private float giroPunta = 360f;

    [Header("Escombros")]
    [Tooltip("Lo que escupe. Un prefab chiquito con Rigidbody2D. Vacío " +
        "= no escupe nada y el material va directo a la bodega.")]
    [SerializeField] private GameObject escombroPrefab;

    [Tooltip("Cada cuántos kg sale un escombro.")]
    [Min(0.01f)]
    [SerializeField] private float kgPorEscombro = 1f;

    [Tooltip("Tamaño del escombro al nacer. El prefab viene al tamaño " +
        "del dibujo original, que para la escala de este juego es " +
        "enorme — una unidad es media nave.")]
    [Min(0.001f)]
    [SerializeField] private float escalaEscombro = 0.05f;

    [Tooltip("Cuánto varía el tamaño de uno a otro, como fracción. " +
        "0 = todos idénticos, que se nota y queda feo. 0.25 = entre " +
        "el 75% y el 125%.")]
    [Range(0f, 0.9f)]
    [SerializeField] private float variacionEscala = 0.25f;

    [Tooltip("Polvo y chispas del corte. Va ADEMÁS de los escombros, " +
        "no en vez de: los escombros siguen siendo los que se aspiran " +
        "y pesan. Esto es solo el adorno.\n\n" +
        "Vacío = no sale nada y todo lo demás funciona igual.")]
    [SerializeField] private ParticleSystem particulasCorte;

    [Tooltip("Cuántas partículas por escombro producido. Si el " +
        "ParticleSystem ya tiene su propia emisión por tiempo, dejalo " +
        "bajo o en 0 para no duplicar.")]
    [Min(0)]
    [SerializeField] private int particulasPorEscombro = 6;

    [Tooltip("Con qué velocidad salen despedidos, hacia afuera de la roca.")]
    [SerializeField] private float velocidadEscupida = 0.3f;

    [Tooltip("Tope de escombros que pueden nacer de UN mordisco. Lo que " +
        "pase de ahí va directo a la bodega, sin objeto físico.\n\n" +
        "Es una red de seguridad, no un ajuste estético: los escombros " +
        "salen de kg/kgPorEscombro, y kg es area x densidad. Una roca " +
        "chica y pesada tiene densidad altísima, así que un solo " +
        "mordisco podía instanciar cientos de GameObjects en un frame " +
        "y voltear el editor por falta de memoria.")]
    [Min(1)]
    [SerializeField] private int maxEscombrosPorMordisco = 12;

    [Header("Color de las chispas segun el material")]
    [Tooltip("Cada chispa se pinta segun de qué estés sacando material, " +
        "así se nota a simple vista qué estás picando. Pisa el Start " +
        "Color del ParticleSystem, que para esto da igual.")]
    [SerializeField] private Color colorRoca = new Color(0.62f, 0.60f, 0.57f);

    [Tooltip("El hielo tira a celeste.")]
    [SerializeField] private Color colorHielo = new Color(0.64f, 0.86f, 0.95f);

    [Tooltip("La chatarra saca chispa de verdad: naranja encendido.")]
    [SerializeField] private Color colorRestos = new Color(1f, 0.45f, 0.12f);

    [Header("Aspiradora")]
    [Tooltip("Hasta dónde llega, desde el cabezal.")]
    [SerializeField] private float radioAspirado = 0.4f;

    [Tooltip("Con cuánta aceleración trae los escombros. Va por masa, así " +
        "que los trae igual de rápido pesen lo que pesen.")]
    [SerializeField] private float fuerzaAspirado = 3f;

    [Tooltip("Cuánto los frena además de traerlos. Sin esto el escombro " +
        "acelera, se pasa de largo de la boca y queda orbitando para " +
        "siempre — en el espacio no hay nada que lo frene.")]
    [SerializeField] private float frenoAspirado = 4f;

    [Tooltip("Más cerca que esto, el escombro entra a la bodega.")]
    [SerializeField] private float radioTragado = 0.03f;

    [SerializeField] private bool mostrarGizmos = true;

    [Tooltip("Escribe en la Console, en cada mordisco, cuántos objetos " +
        "tallables toca el corte y cuántos vértices tienen entre todos. " +
        "Si esos números suben sin parar mientras cavás, ahí está el " +
        "cuelgue. Apagalo cuando termines: escribe mucho.")]
    [SerializeField] private bool diagnostico;

    /// <summary>¿Está cavando ahora mismo?</summary>
    public bool Taladrando { get; private set; }

    /// <summary>
    /// El brazo frena con el radio de UN disco: el canal que cavan los
    /// dos es el doble de ancho, así que el cabezal entra cómodo en lo
    /// que él mismo abrió, pero no en una grieta más angosta.
    /// </summary>
    public override float Grosor => radioDisco;

    private readonly List<Rigidbody2D> _escombros = new List<Rigidbody2D>();
    private readonly HashSet<Tallable> _vistos = new HashSet<Tallable>();
    private float _kgPendiente;
    private TipoMaterial _tipoPendiente;
    private float _acumulado;

    private void Update()
    {
        Taladrando = EnLaMano && punta != null
            && Mouse.current != null && Mouse.current.rightButton.isPressed;

        if (Taladrando && discos != null)
        {
            for (int i = 0; i < discos.Length; i++)
            {
                if (discos[i] == null)
                {
                    continue;
                }

                // El sentido sale de DÓNDE ESTÁ cada disco, no del orden
                // en el array: el de la izquierda gira horario y el de la
                // derecha antihorario, así las dos caras del frente
                // barren hacia el centro y empujan lo picado hacia la
                // boca de la aspiradora en vez de escupirlo al costado.
                //
                // Antes esto salía de i % 2, y bastaba arrastrarlos en el
                // orden equivocado para que giraran al revés. Midiendo la
                // posición en el espacio LOCAL de la herramienta queda
                // bien siempre, y acompaña por mucho que la gires.
                float lado = transform.InverseTransformPoint(discos[i].position).x;
                float sentido = lado < 0f ? -1f : 1f;
                discos[i].Rotate(0f, 0f, giroDiscos * sentido * Time.deltaTime);
            }
        }

        if (Taladrando && puntaGiratoria != null)
        {
            puntaGiratoria.Rotate(0f, 0f, giroPunta * Time.deltaTime);
        }
    }

    private void Awake()
    {
        // El campo acepta cualquier GameObject, y arrastrar un asteroide
        // ahí es fácil y parece funcionar: salen piedras. Lo que no se ve
        // es que salen gigantes y que cada una es una roca tallable entera.
        // El código ya se protege, pero conviene que el error se vea acá
        // y no recién cuando algo anda lento.
        if (escombroPrefab != null && escombroPrefab.GetComponent<Tallable>() != null)
        {
            Debug.LogWarning("[Taladro] El Escombro Prefab (" + escombroPrefab.name +
                ") tiene Tallable: es una roca, no un escombro. Se le saca " +
                "al nacer, pero conviene armar un prefab propio — un sprite " +
                "chico, Rigidbody2D, un CircleCollider2D y el componente " +
                "Escombro — que es mucho más liviano que un PolygonCollider " +
                "de ~150 puntos por cada piedrita.", this);
        }
    }

    private void FixedUpdate()
    {
        if (EnLaMano)
        {
            Aspirar();
        }

        if (!Taladrando)
        {
            _acumulado = 0f;
            return;
        }

        _acumulado += Time.fixedDeltaTime;
        if (_acumulado >= intervalo)
        {
            Morder(_acumulado);
            _acumulado = 0f;
        }
    }

    // ------------------------------------------------------------------
    //  Morder
    // ------------------------------------------------------------------

    private void Morder(float dt)
    {
        Vector2 centro = punta.position;
        Vector2 dir = Direccion();
        float presupuesto = mordida * dt;

        // todo lo tallable que toque el barrido entero
        _vistos.Clear();
        float radio = separacion * 0.5f + radioDisco + alcanceCorte;
        Collider2D[] tocados = Physics2D.OverlapCircleAll(centro + dir * (alcanceCorte * 0.5f), radio);

        if (diagnostico)
        {
            int vert = 0;
            var contados = new HashSet<Tallable>();
            foreach (Collider2D c in tocados)
            {
                Tallable t = c.GetComponentInParent<Tallable>();
                if (t != null && contados.Add(t))
                {
                    vert += t.Vertices;
                }
            }
            Debug.Log($"[Taladro] colliders={tocados.Length} tallables={contados.Count} " +
                $"vertices={vert} escombros={_escombros.Count}");
        }

        foreach (Collider2D c in tocados)
        {
            Tallable t = c.GetComponentInParent<Tallable>();
            if (t == null || !_vistos.Add(t))
            {
                continue;
            }

            // Un escombro no se muerde NUNCA. Nacen justo en el frente de
            // corte, delante del taladro: si fueran mordibles, cada
            // mordisco soltaría escombros que el paso siguiente vuelve a
            // morder, que sueltan más, y así. Crece exponencial y se come
            // toda la memoria — en el PC de la pega llegó a colgar Windows.
            if (t.GetComponent<Escombro>() != null)
            {
                continue;
            }

            float avance = AvancePara(t, centro, dir, presupuesto);
            if (avance < 0f)
            {
                continue;
            }

            // la densidad antes de tallar: si se come la roca entera,
            // queda marcada para destruirse
            float densidad = t.Densidad;
            Rigidbody2D cuerpo = t.Cuerpo;
            TipoMaterial tipo = t.Tipo;
            float area = t.Tallar(Barrido(centro, dir, avance));
            if (area > 0f)
            {
                Producir(area * densidad, tipo, centro, dir, cuerpo);
            }
        }
    }

    /// <summary>
    /// Qué tan lejos pueden barrer los discos hacia adelante sin llevarse
    /// más de lo que toca comer. Un barrido más corto queda adentro de
    /// uno más largo, así que lo que se llevan crece con el avance, y eso
    /// deja buscarlo partiendo el rango a la mitad. Devuelve -1 si no hay
    /// nada que comer.
    /// </summary>
    private float AvancePara(Tallable t, Vector2 centro, Vector2 dir, float presupuesto)
    {
        float todo = t.AreaQueSeLlevaria(Barrido(centro, dir, alcanceCorte));
        if (todo <= 0f)
        {
            return -1f;
        }
        if (todo <= presupuesto)
        {
            return alcanceCorte;
        }

        float bajo = 0f;
        float alto = alcanceCorte;
        for (int i = 0; i < 8; i++)
        {
            float medio = (bajo + alto) * 0.5f;
            if (t.AreaQueSeLlevaria(Barrido(centro, dir, medio)) <= presupuesto)
            {
                bajo = medio;
            }
            else
            {
                alto = medio;
            }
        }
        return bajo;
    }

    /// <summary>
    /// Lo que barren los dos discos al avanzar "avance" hacia adelante:
    /// cada disco arrastrado en línea recta deja una cápsula (dos medias
    /// lunas unidas por dos rectas). Con avance 0 son los discos solos.
    /// </summary>
    private List<Vector2[]> Barrido(Vector2 centro, Vector2 dir, float avance)
    {
        Vector2 perp = new Vector2(-dir.y, dir.x);
        Vector2 medio = perp * (separacion * 0.5f);
        return new List<Vector2[]>
        {
            Capsula(centro + medio, dir, perp, avance),
            Capsula(centro - medio, dir, perp, avance),
        };
    }

    private Vector2[] Capsula(Vector2 atras, Vector2 dir, Vector2 perp, float avance)
    {
        Vector2 adelante = atras + dir * avance;
        int n = Mathf.Max(3, lados / 2);
        var puntos = new Vector2[(n + 1) * 2];
        int k = 0;

        // media luna de adelante: de un costado al otro, pasando por el frente
        for (int i = 0; i <= n; i++)
        {
            float a = Mathf.Lerp(-90f, 90f, (float)i / n) * Mathf.Deg2Rad;
            puntos[k++] = adelante + (dir * Mathf.Cos(a) + perp * Mathf.Sin(a)) * radioDisco;
        }
        // media luna de atrás: vuelve por el otro lado
        for (int i = 0; i <= n; i++)
        {
            float a = Mathf.Lerp(90f, 270f, (float)i / n) * Mathf.Deg2Rad;
            puntos[k++] = atras + (dir * Mathf.Cos(a) + perp * Mathf.Sin(a)) * radioDisco;
        }
        return puntos;
    }

    // ------------------------------------------------------------------
    //  Escombros
    // ------------------------------------------------------------------

    private void Producir(float kg, TipoMaterial tipo, Vector2 centro, Vector2 dir, Rigidbody2D roca)
    {
        Vector2 frente = centro + dir * (radioDisco + alcanceCorte * 0.5f);

        if (escombroPrefab == null)
        {
            // Sin prefab el material va directo a la bodega, pero el
            // corte igual tiene que verse: si no, cortar una roca no
            // produciría ningún efecto en pantalla.
            Chispear(frente, -dir, Vector2.zero, particulasPorEscombro, tipo);
            Cargar(kg, tipo);
            return;
        }

        // lo pendiente de otro material se pierde al cambiar de roca: son
        // migajas de menos de un escombro
        if (tipo != _tipoPendiente)
        {
            _kgPendiente = 0f;
            _tipoPendiente = tipo;
        }
        _kgPendiente += kg;

        Vector2 vRoca = roca != null ? roca.linearVelocity : Vector2.zero;
        int nacidos = 0;
        while (_kgPendiente >= kgPorEscombro && nacidos < maxEscombrosPorMordisco)
        {
            _kgPendiente -= kgPorEscombro;
            nacidos++;

            // nace en el frente de corte y sale para afuera, hacia el
            // taladro, abierto en abanico
            Vector2 pos = centro + dir * (radioDisco + alcanceCorte * 0.5f)
                + new Vector2(-dir.y, dir.x) * Random.Range(-separacion, separacion) * 0.5f;
            GameObject go = Instantiate(escombroPrefab, pos, Quaternion.identity);

            // La escala se pone acá y no en el prefab para poder
            // variarla por escombro: si todos salieran del mismo
            // tamaño exacto, un chorro de piedritas idénticas se lee
            // como repetición y no como escombros.
            float s = escalaEscombro * (1f + Random.Range(-variacionEscala, variacionEscala));
            go.transform.localScale = new Vector3(s, s, 1f);

            // Si el prefab trae Tallable (por ejemplo, si se usó un
            // asteroide como molde), se le saca: un escombro es una
            // piedrita para aspirar, no una roca para seguir picando. El
            // filtro de Morder ya impide la cadena, pero así además no
            // carga con el costo de un cuerpo tallable que nunca va a
            // tallarse.
            if (go.TryGetComponent(out Tallable comoRoca))
            {
                Destroy(comoRoca);
            }

            Escombro e = go.GetComponent<Escombro>();
            if (e == null)
            {
                e = go.AddComponent<Escombro>();
            }
            e.kg = kgPorEscombro;
            e.material = tipo;

            // El abanico se calcula una vez y lo comparten el escombro
            // y sus chispas: así el polvo sale para el mismo lado que
            // la piedra y se lee como un solo impacto, no como dos
            // efectos sueltos que casualmente pasan juntos.
            float giro = Random.Range(-60f, 60f);
            Vector2 salida = Quaternion.Euler(0f, 0f, giro) * -dir;

            Rigidbody2D rb = go.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.linearVelocity = vRoca + salida * velocidadEscupida;
                _escombros.Add(rb);
            }

            Chispear(pos, salida, vRoca, particulasPorEscombro, tipo);
        }

        // Lo que pasó del tope no se pierde: va derecho a la bodega sin
        // objeto físico. Si no, morder una roca muy densa te regalaría
        // material o, peor, dejaría _kgPendiente creciendo para siempre.
        if (_kgPendiente >= kgPorEscombro)
        {
            float sobra = Mathf.Floor(_kgPendiente / kgPorEscombro) * kgPorEscombro;
            _kgPendiente -= sobra;
            Cargar(sobra, tipo);
        }
    }

    /// <summary>
    /// Con qué color sale la chispa según de qué estés sacando material.
    /// </summary>
    private Color ColorDe(TipoMaterial tipo)
    {
        switch (tipo)
        {
            case TipoMaterial.Hielo:
                return colorHielo;
            case TipoMaterial.Restos:
                return colorRestos;
            default:
                return colorRoca;
        }
    }

    /// <summary>
    /// Tira unas partículas en el punto de corte, saliendo hacia
    /// <paramref name="dirSalida"/>.
    ///
    /// Se emiten a mano con EmitParams en vez de dejar que el
    /// ParticleSystem emita solo, porque la posición y la velocidad
    /// las sabe el taladro: dónde está el frente de corte y para qué
    /// lado escupe. Un emisor configurado en el editor no tiene forma
    /// de saber eso.
    ///
    /// Se respeta el espacio de simulación del sistema: si está en
    /// Local hay que pasarle posición y velocidad en local, si no las
    /// partículas aparecen en cualquier lado.
    /// </summary>
    private void Chispear(Vector2 pos, Vector2 dirSalida, Vector2 vBase, int cuantas,
        TipoMaterial tipo)
    {
        if (particulasCorte == null || cuantas <= 0)
        {
            return;
        }

        Color color = ColorDe(tipo);

        bool local = particulasCorte.main.simulationSpace
            == ParticleSystemSimulationSpace.Local;
        Transform t = particulasCorte.transform;

        for (int i = 0; i < cuantas; i++)
        {
            // cada chispa se abre un poco respecto de la piedra, si no
            // salen todas en fila y parece un solo proyectil
            float giro = Random.Range(-35f, 35f);
            Vector2 v = vBase + (Vector2)(Quaternion.Euler(0f, 0f, giro) * dirSalida)
                * velocidadEscupida * Random.Range(0.4f, 1.3f);

            var ep = new ParticleSystem.EmitParams
            {
                applyShapeToPosition = false,
                position = local ? t.InverseTransformPoint(pos) : (Vector3)pos,
                velocity = local ? t.InverseTransformDirection(v) : (Vector3)v,
                startColor = color
            };

            particulasCorte.Emit(ep, 1);
        }
    }

    private void Aspirar()
    {
        if (punta == null)
        {
            return;
        }

        Vector2 boca = punta.position;
        Bodega bodega = GetComponentInParent<Bodega>();

        // El freno se mide RELATIVO a la nave: si la nave va a toda
        // velocidad, frenar el escombro contra el espacio lo dejaría
        // atrás. Lo que importa es que quede quieto respecto a la boca.
        Rigidbody2D nave = Usuario != null ? Usuario.Nave : null;
        Vector2 vBoca = nave != null ? nave.linearVelocity : Vector2.zero;

        for (int i = _escombros.Count - 1; i >= 0; i--)
        {
            Rigidbody2D rb = _escombros[i];
            if (rb == null)
            {
                _escombros.RemoveAt(i);
                continue;
            }

            Vector2 d = boca - rb.position;
            float dist = d.magnitude;

            // se fue muy lejos: queda suelto en el espacio, como
            // cualquier otra cosa que se puede ir a buscar con la mano
            if (dist > radioAspirado * 4f)
            {
                _escombros.RemoveAt(i);
                continue;
            }
            if (dist > radioAspirado)
            {
                continue;
            }

            if (dist < radioTragado)
            {
                // con la bodega llena se queda flotando en la boca
                if (bodega != null && !bodega.Llena)
                {
                    Escombro e = rb.GetComponent<Escombro>();
                    bodega.SumarCarga(e != null ? e.kg : rb.mass,
                        e != null ? e.material : TipoMaterial.Roca);
                    Destroy(rb.gameObject);
                    _escombros.RemoveAt(i);
                }
                continue;
            }

            Vector2 a = d / dist * fuerzaAspirado - (rb.linearVelocity - vBoca) * frenoAspirado;
            rb.AddForce(a * rb.mass);
        }
    }

    private void Cargar(float kg, TipoMaterial tipo)
    {
        Bodega bodega = GetComponentInParent<Bodega>();
        if (bodega != null)
        {
            bodega.SumarCarga(kg, tipo);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!mostrarGizmos || punta == null)
        {
            return;
        }

        // los discos, y hasta dónde barren
        Gizmos.color = new Color(1f, 0.5f, 0.2f, 0.9f);
        foreach (Vector2[] forma in Barrido(punta.position, Direccion(), alcanceCorte))
        {
            for (int i = 0; i < forma.Length; i++)
            {
                Gizmos.DrawLine(forma[i], forma[(i + 1) % forma.Length]);
            }
        }

        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.4f);
        Gizmos.DrawWireSphere(punta.position, radioAspirado);
    }
}
