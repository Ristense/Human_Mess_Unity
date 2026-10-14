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

    [Header("Escombros")]
    [Tooltip("Lo que escupe. Un prefab chiquito con Rigidbody2D. Vacío " +
        "= no escupe nada y el material va directo a la bodega.")]
    [SerializeField] private GameObject escombroPrefab;

    [Tooltip("Cada cuántos kg sale un escombro.")]
    [SerializeField] private float kgPorEscombro = 1f;

    [Tooltip("Con qué velocidad salen despedidos, hacia afuera de la roca.")]
    [SerializeField] private float velocidadEscupida = 0.3f;

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
    private float _acumulado;

    private void Update()
    {
        Taladrando = EnLaMano && punta != null
            && Mouse.current != null && Mouse.current.rightButton.isPressed;

        if (Taladrando && discos != null)
        {
            for (int i = 0; i < discos.Length; i++)
            {
                if (discos[i] != null)
                {
                    float sentido = i % 2 == 0 ? 1f : -1f;
                    discos[i].Rotate(0f, 0f, giroDiscos * sentido * Time.deltaTime);
                }
            }
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
        foreach (Collider2D c in Physics2D.OverlapCircleAll(centro + dir * (alcanceCorte * 0.5f), radio))
        {
            Tallable t = c.GetComponentInParent<Tallable>();
            if (t == null || !_vistos.Add(t))
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
            float area = t.Tallar(Barrido(centro, dir, avance));
            if (area > 0f)
            {
                Producir(area * densidad, centro, dir, cuerpo);
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

    private void Producir(float kg, Vector2 centro, Vector2 dir, Rigidbody2D roca)
    {
        _kgPendiente += kg;

        if (escombroPrefab == null)
        {
            Cargar(_kgPendiente);
            _kgPendiente = 0f;
            return;
        }

        Vector2 vRoca = roca != null ? roca.linearVelocity : Vector2.zero;
        while (_kgPendiente >= kgPorEscombro)
        {
            _kgPendiente -= kgPorEscombro;

            // nace en el frente de corte y sale para afuera, hacia el
            // taladro, abierto en abanico
            Vector2 pos = centro + dir * (radioDisco + alcanceCorte * 0.5f)
                + new Vector2(-dir.y, dir.x) * Random.Range(-separacion, separacion) * 0.5f;
            GameObject go = Instantiate(escombroPrefab, pos, Quaternion.identity);

            Escombro e = go.GetComponent<Escombro>();
            if (e == null)
            {
                e = go.AddComponent<Escombro>();
            }
            e.kg = kgPorEscombro;

            Rigidbody2D rb = go.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                float giro = Random.Range(-60f, 60f);
                Vector2 salida = Quaternion.Euler(0f, 0f, giro) * -dir;
                rb.linearVelocity = vRoca + salida * velocidadEscupida;
                _escombros.Add(rb);
            }
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
                    bodega.SumarCarga(e != null ? e.kg : rb.mass);
                    Destroy(rb.gameObject);
                    _escombros.RemoveAt(i);
                }
                continue;
            }

            Vector2 a = d / dist * fuerzaAspirado - (rb.linearVelocity - vBoca) * frenoAspirado;
            rb.AddForce(a * rb.mass);
        }
    }

    private void Cargar(float kg)
    {
        Bodega bodega = GetComponentInParent<Bodega>();
        if (bodega != null)
        {
            bodega.SumarCarga(kg);
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
