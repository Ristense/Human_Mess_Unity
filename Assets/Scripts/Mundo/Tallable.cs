using System.Collections.Generic;
using Clipper2Lib;
using UnityEngine;

/// <summary>
/// Algo a lo que se le puede sacar material de verdad: un asteroide.
///
/// No tiene vida ni "progreso de corte". Tiene una FORMA (un polígono), y
/// tallar es restarle otro polígono. Todo lo demás sale de eso:
///
///   - el pozo QUEDA, porque está en la forma y no en una variable
///   - la masa baja con el área, porque ese material ya no está
///   - si el corte deja la forma en dos islas, se parte en dos piezas
///     de verdad, cada una con su masa y su velocidad. Nadie decide
///     "ahora se rompió": es geometría
///
/// Mientras nadie la toca sigue siendo el sprite de siempre. Al primer
/// mordisco se pasa a una malla con la forma recortada y la misma
/// textura, porque a un sprite no se le pueden hacer agujeros.
///
/// Las operaciones entre polígonos son de Clipper2 (Assets/Plugins).
///
/// Va en el prefab del asteroide, junto al Rigidbody2D y al
/// PolygonCollider2D.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PolygonCollider2D))]
public class Tallable : MonoBehaviour
{
    [Tooltip("Un pedazo con menos área que esto (unidades²) se hace " +
        "polvo en vez de quedar como pieza.")]
    [SerializeField] private float areaMinima = 0.0005f;

    [Tooltip("Un pedazo más fino que esto es una astilla, no una " +
        "piedra: se hace polvo. Es lo que evita esos pinchos larguísimos " +
        "que quedan cuando el corte pasa rozando un borde.")]
    [SerializeField] private float grosorMinimo = 0.008f;

    [Tooltip("Un pedazo que se desprende tiene que ser al menos esta " +
        "fracción de la roca ORIGINAL para quedar como pieza; si no, se " +
        "hace polvo. Al ser relativo sirve igual para rocas chicas y " +
        "grandes, y es lo que corta la lluvia de astillas.")]
    [Range(0f, 0.5f)]
    [SerializeField] private float fraccionMinima = 0.03f;

    [Tooltip("Cuánto se simplifica el contorno después de cada mordisco, " +
        "en unidades. Sin esto, cada corte suma vértices y el collider " +
        "termina con cientos de puntos: es el lag que se va juntando.")]
    [SerializeField] private float simplificar = 0.002f;

    [Tooltip("De qué está hecha. Lo que saca el taladro va a la bodega " +
        "como este material.")]
    [SerializeField] private TipoMaterial material = TipoMaterial.Roca;

    // no se llama "Material": taparía al tipo Material de Unity, que esta
    // clase usa para la malla
    public TipoMaterial Tipo => material;

    [Tooltip("Diagnóstico: dibuja en rojo, en la Scene, las piezas que " +
        "la física armó de verdad con el collider. Si no calzan con la " +
        "roca que se ve, el problema está ahí.")]
    [SerializeField] private bool verFisica;

    private void OnDrawGizmos()
    {
        if (!verFisica || _col == null)
        {
            return;
        }
        var grupo = new PhysicsShapeGroup2D();
        _col.GetShapes(grupo);
        var vertices = new List<Vector2>();
        Gizmos.color = Color.red;
        for (int i = 0; i < grupo.shapeCount; i++)
        {
            PhysicsShape2D s = grupo.GetShape(i);
            vertices.Clear();
            grupo.GetShapeVertices(i, vertices);
            if (s.shapeType != PhysicsShapeType2D.Polygon)
            {
                continue;
            }
            // vienen relativos al Rigidbody2D (sin escala)
            Quaternion giro = Quaternion.Euler(0f, 0f, _rb != null ? _rb.rotation : 0f);
            Vector3 origen = _rb != null ? (Vector3)_rb.position : Vector3.zero;
            for (int j = 0; j < vertices.Count; j++)
            {
                Gizmos.DrawLine(origen + giro * vertices[j],
                    origen + giro * vertices[(j + 1) % vertices.Count]);
            }
        }
    }

    // Clipper trabaja con enteros por dentro: 4 decimales = 0.0001
    // unidades, que a PPU 1000 es un décimo de píxel. Con los 2 que trae
    // por defecto, a esta escala las rocas quedaban serruchadas.
    private const int Decimales = 4;

    private Rigidbody2D _rb;
    private PolygonCollider2D _col;
    private SpriteRenderer _sr;

    // La forma, en coordenadas LOCALES del objeto (sin escala).
    private PathsD _forma;

    // kg por unidad² de mundo. Se calcula recién al primer mordisco, para
    // que ya incluya lo que haya ajustado MasaPorTamano en su Start.
    private float _densidad;

    // El área que tenía la roca antes del primer mordisco. Los pedazos
    // la heredan, así el umbral de polvo es siempre relativo a la roca
    // de la que salieron y no al pedacito que ya son.
    private float _areaOriginal;

    private bool _comoMalla;
    private MeshFilter _mf;
    private MeshRenderer _mr;
    private Mesh _mesh;

    // Un material por textura, compartido entre todas las rocas talladas
    // que usen la misma: si no, cada roca mordida crearía el suyo.
    private static readonly Dictionary<Texture, Material> _materiales =
        new Dictionary<Texture, Material>();

    public Rigidbody2D Cuerpo => _rb;

    /// <summary>kg por unidad² de mundo.</summary>
    public float Densidad
    {
        get
        {
            AsegurarDensidad();
            return _densidad;
        }
    }

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _col = GetComponent<PolygonCollider2D>();
        _sr = GetComponent<SpriteRenderer>();
        _forma = LeerCollider();
    }

    // ------------------------------------------------------------------
    //  Lo que usan las herramientas
    // ------------------------------------------------------------------

    /// <summary>
    /// Cuánta área (en mundo) se llevaría este corte, sin hacerlo. El
    /// taladro lo usa para dosificar el mordisco.
    /// </summary>
    public float AreaQueSeLlevaria(IReadOnlyList<Vector2[]> corteMundo)
    {
        PathsD inter = Clipper.Intersect(_forma, ALocal(corteMundo),
            FillRule.NonZero, Decimales);
        return Mathf.Abs((float)Clipper.Area(inter)) * Escala;
    }

    /// <summary>
    /// Le saca a la forma los polígonos de "corte" (en coordenadas de
    /// MUNDO; si se pisan entre sí, cuenta la unión). Devuelve cuánta área
    /// se quitó, contando lo que quedó hecho polvo.
    ///
    /// Si la forma queda partida, el pedazo más grande sigue siendo este
    /// objeto y los demás nacen como objetos nuevos. Si no queda nada que
    /// valga la pena, el objeto se destruye.
    /// </summary>
    public float Tallar(IReadOnlyList<Vector2[]> corteMundo)
    {
        AsegurarDensidad();

        PathsD resto = Clipper.Difference(_forma, ALocal(corteMundo),
            FillRule.NonZero, Decimales);

        // Simplificar: borra los vértices que casi no doblan nada. Cada
        // corte agrega puntos, y sin esto el contorno crece sin límite y
        // cada mordisco se vuelve más caro que el anterior.
        float eps = simplificar / Mathf.Sqrt(Mathf.Max(Escala, 0.000001f));
        if (eps > 0f)
        {
            resto = Clipper.SimplifyPaths(resto, eps);
        }

        float antes = AreaMundo(_forma);

        var pedazos = new List<PathD>();
        float despues = 0f;
        foreach (PathD p in resto)
        {
            // Área negativa = un agujero adentro de otro polígono. Con un
            // taladro que muerde desde el borde no deberían salir; si sale
            // alguno se descarta, porque una malla con agujeros no la
            // sabemos armar todavía.
            if (Clipper.Area(p) <= 0)
            {
                continue;
            }
            if (EsPolvo(p))
            {
                continue;
            }
            pedazos.Add(p);
            despues += Mathf.Abs((float)Clipper.Area(p)) * Escala;
        }

        float quitado = antes - despues;

        // Con redondeos, un corte que no tocó nada igual puede mover un
        // vértice una diezmilésima. No vale la pena rearmar todo por eso.
        if (quitado < 0.0000001f)
        {
            return 0f;
        }

        if (pedazos.Count == 0)
        {
            Destroy(gameObject);
            return antes;
        }

        // El pedazo más grande se queda en este objeto. Así lo que esté
        // atado a él (la mano que lo agarra, por ejemplo) sigue atado.
        pedazos.Sort((a, b) => Clipper.Area(b).CompareTo(Clipper.Area(a)));

        Vector2 v = _rb.linearVelocity;
        float w = _rb.angularVelocity;
        Vector2 centroMasa = _rb.worldCenterOfMass;
        for (int i = 1; i < pedazos.Count; i++)
        {
            CrearPedazo(pedazos[i], v, w, centroMasa);
        }

        Aplicar(new PathsD { pedazos[0] });
        return quitado;
    }

    /// <summary>¿Ya se le cortó algo? Desde ahí el sprite no es su forma.</summary>
    public bool EstaTallado => _comoMalla;

    /// <summary>
    /// La forma de verdad, en MUNDO, agrandada "margen" para afuera con
    /// las esquinas redondeadas. El contorno la usa para dibujarse cuando
    /// el sprite ya no sirve.
    /// </summary>
    public List<Vector3> ContornoMundo(float margen)
    {
        PathsD forma = _forma;
        float m = margen / Mathf.Sqrt(Mathf.Max(Escala, 0.000001f));
        if (m > 0f)
        {
            forma = Clipper.InflatePaths(_forma, m, JoinType.Round, EndType.Polygon,
                2.0, Decimales);
        }

        var puntos = new List<Vector3>();
        // con un solo pedazo por objeto, el más grande es el contorno
        PathD mayor = null;
        foreach (PathD p in forma)
        {
            if (mayor == null || Clipper.Area(p) > Clipper.Area(mayor))
            {
                mayor = p;
            }
        }
        if (mayor == null)
        {
            return puntos;
        }
        foreach (PointD v in mayor)
        {
            Vector3 w = transform.TransformPoint(new Vector3((float)v.x, (float)v.y, 0f));
            w.z = transform.position.z;
            puntos.Add(w);
        }
        return puntos;
    }

    // ------------------------------------------------------------------
    //  Forma
    // ------------------------------------------------------------------

    private float Escala
    {
        get
        {
            Vector3 e = transform.lossyScale;
            return Mathf.Abs(e.x * e.y);
        }
    }

    private float AreaMundo(PathsD forma)
    {
        return Mathf.Abs((float)Clipper.Area(forma)) * Escala;
    }

    private void AsegurarDensidad()
    {
        if (_densidad > 0f)
        {
            return;
        }
        float area = AreaMundo(_forma);
        _densidad = area > 0f ? _rb.mass / area : 1f;
        _areaOriginal = area;
    }

    private PathsD LeerCollider()
    {
        var forma = new PathsD();
        for (int i = 0; i < _col.pathCount; i++)
        {
            Vector2[] puntos = _col.GetPath(i);
            var p = new PathD(puntos.Length);
            foreach (Vector2 v in puntos)
            {
                // los puntos del collider son relativos a su offset
                p.Add(new PointD(v.x + _col.offset.x, v.y + _col.offset.y));
            }
            forma.Add(p);
        }
        return forma;
    }

    private PathsD ALocal(IReadOnlyList<Vector2[]> mundo)
    {
        var salida = new PathsD(mundo.Count);
        foreach (Vector2[] poligono in mundo)
        {
            var p = new PathD(poligono.Length);
            foreach (Vector2 v in poligono)
            {
                Vector2 l = transform.InverseTransformPoint(v);
                p.Add(new PointD(l.x, l.y));
            }
            salida.Add(p);
        }
        return salida;
    }

    /// <summary>
    /// ¿Es un pedazo de verdad o polvo? Mirar solo el área no alcanza: un
    /// pincho largo y finito tiene la misma área que una piedrita, pero
    /// uno es basura visual y el otro una piedra. El grosor (2·área /
    /// perímetro) es lo que los separa.
    /// </summary>
    private bool EsPolvo(PathD p)
    {
        float escala = Escala;
        float area = Mathf.Abs((float)Clipper.Area(p)) * escala;
        if (area < areaMinima || area < _areaOriginal * fraccionMinima)
        {
            return true;
        }
        float perimetro = Perimetro(p) * Mathf.Sqrt(escala);
        return perimetro > 0f && 2f * area / perimetro < grosorMinimo;
    }

    private static float Perimetro(PathD p)
    {
        double total = 0;
        for (int i = 0; i < p.Count; i++)
        {
            PointD a = p[i];
            PointD b = p[(i + 1) % p.Count];
            double dx = b.x - a.x;
            double dy = b.y - a.y;
            total += System.Math.Sqrt(dx * dx + dy * dy);
        }
        return (float)total;
    }

    /// <summary>Centro del polígono, en local. La fórmula de siempre del área con signo.</summary>
    private static Vector2 Centroide(PathD p)
    {
        double a = 0, cx = 0, cy = 0;
        for (int i = 0; i < p.Count; i++)
        {
            PointD p0 = p[i];
            PointD p1 = p[(i + 1) % p.Count];
            double cruz = p0.x * p1.y - p1.x * p0.y;
            a += cruz;
            cx += (p0.x + p1.x) * cruz;
            cy += (p0.y + p1.y) * cruz;
        }
        if (System.Math.Abs(a) < 1e-12)
        {
            return new Vector2((float)p[0].x, (float)p[0].y);
        }
        a *= 0.5;
        return new Vector2((float)(cx / (6 * a)), (float)(cy / (6 * a)));
    }

    /// <summary>
    /// Deja la forma nueva en todos lados: collider, masa y malla.
    /// </summary>
    private void Aplicar(PathsD forma)
    {
        _forma = forma;

        _col.offset = Vector2.zero;
        _col.pathCount = forma.Count;
        for (int i = 0; i < forma.Count; i++)
        {
            var puntos = new Vector2[forma[i].Count];
            for (int j = 0; j < puntos.Length; j++)
            {
                puntos[j] = new Vector2((float)forma[i][j].x, (float)forma[i][j].y);
            }
            _col.SetPath(i, puntos);
        }

        // la masa sale del área que queda: el material que se fue ya no pesa
        _rb.mass = Mathf.Max(0.0001f, _densidad * AreaMundo(forma));

        ModoMalla();
        ArmarMalla();
    }

    /// <summary>
    /// Un pedazo que se desprendió: nace como copia de este objeto, con su
    /// propia forma, su parte de la masa, y la velocidad que tenía ese
    /// punto del cuerpo al partirse — si la roca giraba, el pedazo sale
    /// despedido para el lado que corresponde.
    /// </summary>
    private void CrearPedazo(PathD forma, Vector2 v, float w, Vector2 centroMasa)
    {
        GameObject go = Instantiate(gameObject, transform.position, transform.rotation, transform.parent);
        go.name = name.Replace(" (pedazo)", "") + " (pedazo)";
        go.transform.localScale = transform.localScale;

        // Su masa ya sale bien del área: que MasaPorTamano no la vuelva a
        // escalar en su Start. Apagado, Start no corre nunca.
        if (go.TryGetComponent(out MasaPorTamano mpt))
        {
            mpt.enabled = false;
        }

        Tallable t = go.GetComponent<Tallable>();
        t._densidad = _densidad;
        t._areaOriginal = _areaOriginal;
        t.Aplicar(new PathsD { forma });

        Vector2 centro = transform.TransformPoint(Centroide(forma));
        Vector2 r = centro - centroMasa;
        t._rb.linearVelocity = v + w * Mathf.Deg2Rad * new Vector2(-r.y, r.x);
        t._rb.angularVelocity = w;
    }

    // ------------------------------------------------------------------
    //  Malla
    // ------------------------------------------------------------------

    private void ModoMalla()
    {
        if (_comoMalla)
        {
            return;
        }
        _comoMalla = true;

        // Un pedazo nace copiando a su padre, que puede venir ya con la
        // malla puesta: se reusa ese hijo en vez de crear otro.
        Transform hijo = transform.Find("Tallado");
        GameObject go;
        if (hijo != null)
        {
            go = hijo.gameObject;
        }
        else
        {
            go = new GameObject("Tallado");
            go.transform.SetParent(transform, false);
            go.layer = gameObject.layer;
        }

        _mf = go.GetComponent<MeshFilter>();
        if (_mf == null)
        {
            _mf = go.AddComponent<MeshFilter>();
        }
        _mr = go.GetComponent<MeshRenderer>();
        if (_mr == null)
        {
            _mr = go.AddComponent<MeshRenderer>();
        }

        // Malla propia: si compartiera la del padre, tallar un pedazo le
        // cambiaría la forma al otro.
        _mesh = new Mesh { name = "Tallado" };
        _mf.sharedMesh = _mesh;

        if (_sr != null && _sr.sprite != null)
        {
            _mr.sharedMaterial = MaterialPara(_sr);
            _mr.sortingLayerID = _sr.sortingLayerID;
            _mr.sortingOrder = _sr.sortingOrder;
            _sr.enabled = false;
        }
    }

    private static Material MaterialPara(SpriteRenderer sr)
    {
        Texture tex = sr.sprite.texture;
        if (_materiales.TryGetValue(tex, out Material m) && m != null)
        {
            return m;
        }
        m = new Material(sr.sharedMaterial) { mainTexture = tex };
        _materiales[tex] = m;
        return m;
    }

    private void ArmarMalla()
    {
        if (Clipper.Triangulate(_forma, Decimales, out PathsD triangulos) != TriangulateResult.success)
        {
            // si con Delaunay no sale, sin Delaunay suele salir
            if (Clipper.Triangulate(_forma, Decimales, out triangulos, false) != TriangulateResult.success)
            {
                return;
            }
        }

        int n = 0;
        foreach (PathD t in triangulos)
        {
            n += t.Count == 3 ? 3 : 0;
        }

        var vertices = new Vector3[n];
        var uv = new Vector2[n];
        var colores = new Color[n];
        var indices = new int[n];
        Color color = _sr != null ? _sr.color : Color.white;

        int k = 0;
        foreach (PathD t in triangulos)
        {
            if (t.Count != 3)
            {
                continue;
            }
            for (int j = 0; j < 3; j++)
            {
                var p = new Vector2((float)t[j].x, (float)t[j].y);
                vertices[k] = p;
                uv[k] = UVDe(p);
                // el color del sprite (CampoObjetos le da a cada roca un
                // tono distinto) va por vértice, igual que en el sprite
                colores[k] = color;
                indices[k] = k;
                k++;
            }
        }

        _mesh.Clear();
        _mesh.vertices = vertices;
        _mesh.uv = uv;
        _mesh.colors = colores;
        _mesh.triangles = indices;
        _mesh.RecalculateBounds();
    }

    /// <summary>
    /// Qué punto de la textura le corresponde a un punto local: el mismo
    /// que le tocaría en el sprite. Así la malla tallada se ve igual que
    /// la roca original, solo que con el pedazo de menos.
    /// </summary>
    private Vector2 UVDe(Vector2 local)
    {
        if (_sr == null || _sr.sprite == null)
        {
            return Vector2.zero;
        }
        Sprite s = _sr.sprite;
        Texture tex = s.texture;

        Rect r;
        Vector2 corrimiento;
        try
        {
            r = s.textureRect;
            corrimiento = s.textureRectOffset;
        }
        catch (UnityException)
        {
            // empaquetado Tight en un atlas: no hay rectángulo limpio
            r = s.rect;
            corrimiento = Vector2.zero;
        }

        float x = r.x + local.x * s.pixelsPerUnit + s.pivot.x - corrimiento.x;
        float y = r.y + local.y * s.pixelsPerUnit + s.pivot.y - corrimiento.y;
        return new Vector2(x / tex.width, y / tex.height);
    }
}
