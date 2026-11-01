using UnityEngine;

/// <summary>
/// La grilla que se le dibuja encima al planeta durante la selección:
/// una línea por cada división entre sectores, unos paralelos para que
/// se lea como grilla, y un marco que encierra el sector elegido.
///
/// Las líneas cuelgan del planeta, así que giran con él sin que nadie
/// tenga que moverlas: cuando el selector gira la bola para poner un
/// sector de frente, su marco viaja junto.
///
/// Va en el mismo objeto que SelectorSector.
/// </summary>
public class GrillaPlaneta : MonoBehaviour
{
    [SerializeField] private SelectorSector selector;

    [Tooltip("El planeta. Vacío = el que usa el selector.")]
    [SerializeField] private Transform planeta;

    [Header("Dibujo")]
    [Tooltip("El material de las líneas. El neón sirve.")]
    [SerializeField] private Material material;

    [SerializeField] private Color color = new Color(0.5f, 0.9f, 1f, 0.5f);
    [SerializeField] private Color colorElegido = new Color(0.4f, 1f, 0.5f, 1f);

    [Tooltip("Grosor de las líneas, en unidades del mundo.")]
    [SerializeField] private float grosor = 0.01f;

    [Tooltip("Cuánto se despegan de la superficie. Pegadas justo al " +
        "radio, la esfera se las come a pedazos.")]
    [SerializeField] private float separacion = 1.02f;

    [Tooltip("El radio de la esfera, a mano. 0 = lo saca de la malla. " +
        "Ponelo solo si la grilla no te calza con el planeta.")]
    [Min(0f)]
    [SerializeField] private float radioManual;

    [Header("Forma")]
    [Tooltip("Qué tan alto es el recuadro de cada sector, en grados.")]
    [Range(10f, 120f)]
    [SerializeField] private float altoSector = 50f;

    [Tooltip("De cuántos puntos es cada arco. Más = más redondo.")]
    [Min(2)]
    [SerializeField] private int puntosPorArco = 24;

    private Transform _raiz;
    private LineRenderer[] _marcos;
    private float _radio = 0.5f;

    private void OnEnable()
    {
        if (selector == null)
        {
            selector = GetComponent<SelectorSector>();
        }
        if (selector == null)
        {
            return;
        }
        selector.Cambio += Elegir;
        Armar();
        Elegir(selector.Actual);
    }

    private void OnDisable()
    {
        if (selector != null)
        {
            selector.Cambio -= Elegir;
        }
        // se borra entera: fuera de la selección no hay nada que mostrar
        if (_raiz != null)
        {
            Destroy(_raiz.gameObject);
            _raiz = null;
            _marcos = null;
        }
    }

    private void Armar()
    {
        // el mismo que está girando: así no hay dos ideas distintas de
        // cuál es el planeta
        if (planeta == null)
        {
            planeta = selector.Planeta;
        }
        if (planeta == null || _raiz != null)
        {
            Debug.LogWarning("[GrillaPlaneta] no encuentro el planeta.", this);
            return;
        }

        // La grilla NO cuelga del planeta: vive suelta en el mundo y lo
        // sigue desde afuera (ver Seguir). Colgada de él heredaba toda
        // la cadena de escalas de sus padres, y cualquier escala
        // despareja la estiraba distinto según hacia dónde estuviera
        // girado — por eso crecía sola al cambiar de sector.
        _radio = RadioMundo() * separacion;

        var go = new GameObject("Grilla");
        _raiz = go.transform;
        _raiz.localScale = Vector3.one;
        Seguir();

        int n = Mathf.Max(1, selector.Sectores);
        float paso = 360f / n;
        _marcos = new LineRenderer[n];

        // un recuadro por sector, cada uno a la altura que le tocó: por
        // eso pasar de uno a otro no es solo girar de lado
        for (int i = 0; i < n; i++)
        {
            float lon = i * paso;
            float lat = selector.LatitudDe(i);
            _marcos[i] = Linea("Sector " + i,
                Recuadro(lat, lon, paso), color, true);
        }
    }

    /// <summary>Resalta el sector elegido y apaga los demás.</summary>
    private void Elegir(int sector)
    {
        if (_marcos == null)
        {
            return;
        }
        for (int i = 0; i < _marcos.Length; i++)
        {
            if (_marcos[i] == null)
            {
                continue;
            }
            Color c = i == sector ? colorElegido : color;
            _marcos[i].startColor = _marcos[i].endColor = c;
        }
    }

    /// <summary>
    /// El recuadro de un sector sobre la esfera: sube por un lado, cruza
    /// arriba, baja por el otro y vuelve por abajo.
    /// </summary>
    private Vector3[] Recuadro(float latitud, float longitud, float ancho)
    {
        float a = longitud - ancho * 0.5f;
        float b = longitud + ancho * 0.5f;
        float arriba = latitud + altoSector * 0.5f;
        float abajo = latitud - altoSector * 0.5f;

        var puntos = new System.Collections.Generic.List<Vector3>();
        puntos.AddRange(Arco(abajo, arriba, a));
        puntos.AddRange(Paralelo(arriba, a, b));
        Vector3[] bajada = Arco(abajo, arriba, b);
        System.Array.Reverse(bajada);
        puntos.AddRange(bajada);
        Vector3[] vuelta = Paralelo(abajo, a, b);
        System.Array.Reverse(vuelta);
        puntos.AddRange(vuelta);
        return puntos.ToArray();
    }

    private Vector3[] Arco(float desde, float hasta, float longitud)
    {
        var p = new Vector3[puntosPorArco];
        for (int i = 0; i < puntosPorArco; i++)
        {
            float lat = Mathf.Lerp(desde, hasta, (float)i / (puntosPorArco - 1));
            p[i] = Punto(lat, longitud);
        }
        return p;
    }

    // ------------------------------------------------------------------
    //  Geometría
    // ------------------------------------------------------------------

    private Vector3[] Paralelo(float latitud, float desde, float hasta)
    {
        var p = new Vector3[puntosPorArco];
        for (int i = 0; i < puntosPorArco; i++)
        {
            float lon = Mathf.Lerp(desde, hasta, (float)i / (puntosPorArco - 1));
            p[i] = Punto(latitud, lon);
        }
        return p;
    }

    /// <summary>
    /// Un punto de la esfera. Longitud 0 es hacia +Z, que es el lado que
    /// el selector pone de frente.
    /// </summary>
    private Vector3 Punto(float latitud, float longitud)
    {
        float la = latitud * Mathf.Deg2Rad;
        float lo = longitud * Mathf.Deg2Rad;
        float cos = Mathf.Cos(la);
        return new Vector3(cos * Mathf.Sin(lo), Mathf.Sin(la), cos * Mathf.Cos(lo)) * _radio;
    }

    private LineRenderer Linea(string nombre, Vector3[] puntos, Color c, bool cerrada)
    {
        var go = new GameObject(nombre);
        go.transform.SetParent(_raiz, worldPositionStays: false);
        // la capa importa: si no es una que la cámara 3D dibuja, la
        // grilla existe pero no se ve
        go.layer = planeta.gameObject.layer;

        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = cerrada && puntos.Length > 2;
        lr.sharedMaterial = material;
        lr.startWidth = lr.endWidth = grosor;
        lr.startColor = lr.endColor = c;
        lr.numCornerVertices = 2;
        lr.positionCount = puntos.Length;
        if (puntos.Length > 0)
        {
            lr.SetPositions(puntos);
        }
        return lr;
    }

    /// <summary>
    /// El radio del planeta en SU espacio local: sale de la malla, no de
    /// los bounds del mundo, así la escala del objeto no lo altera — las
    /// líneas son hijas suyas y ya heredan esa escala.
    /// </summary>
    /// <summary>
    /// El radio del planeta en unidades del MUNDO. Sale de la malla y
    /// no de los bounds del renderer: los bounds son una caja alineada
    /// al mundo, así que cambian de tamaño cuando el planeta gira, y la
    /// grilla cambiaría con ellos.
    /// </summary>
    private float RadioMundo()
    {
        if (radioManual > 0f)
        {
            return radioManual;
        }
        MeshFilter mf = planeta.GetComponentInChildren<MeshFilter>();
        if (mf == null || mf.sharedMesh == null)
        {
            return 0.5f;
        }
        Vector3 e = mf.sharedMesh.bounds.extents;
        float local = Mathf.Max(e.x, Mathf.Max(e.y, e.z));
        Vector3 s = mf.transform.lossyScale;
        float escala = Mathf.Max(Mathf.Abs(s.x),
            Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z)));
        return local * escala;
    }

    /// <summary>
    /// Planta la grilla sobre el planeta: en su centro y girada como él.
    /// Va en LateUpdate, después de que el selector lo haya girado.
    /// </summary>
    private void Seguir()
    {
        if (_raiz == null || planeta == null)
        {
            return;
        }
        Renderer r = planeta.GetComponentInChildren<MeshRenderer>();
        Vector3 centro = r != null ? r.bounds.center : planeta.position;
        _raiz.SetPositionAndRotation(centro, planeta.rotation);
    }

    private void LateUpdate()
    {
        Seguir();
    }

}
