using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// La placa del minijuego del soldador: una grilla repartida en piezas
/// de formas irregulares, y las líneas por donde hay que cortarlas.
///
/// Cómo se reparten: se siembra una celda al azar y la pieza se va
/// comiendo celdas vecinas hasta llegar a un tamaño al azar. Como
/// siempre crece desde una celda que ya tiene, la pieza sale de una sola
/// parte — es el mismo truco con el que el Mapa arma sus sectores.
///
/// Cada línea entre dos piezas se parte en trocitos. El soldador va
/// quemando los que tiene debajo, y una pieza se suelta recién cuando
/// TODO su contorno quedó cortado. Por eso hay que recorrer la línea
/// entera y no alcanza con quemar un punto.
///
/// Es un componente de UI más (como una Image): tiene Color y Material,
/// así que el neón se le pone igual que al resto del HUD.
///
/// Va en un objeto del Canvas, adentro del cuadro de la ventana.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(CanvasRenderer))]
public class PlacaCorte : MaskableGraphic
{
    [Header("Grilla")]
    [SerializeField] private int cols = 6;
    [SerializeField] private int rows = 8;

    [Header("Piezas")]
    [Tooltip("Lo más chica que puede salir una pieza, en celdas.")]
    [Min(1)]
    [SerializeField] private int minCeldas = 3;

    [Tooltip("Lo más grande.")]
    [Min(1)]
    [SerializeField] private int maxCeldas = 7;

    [Tooltip("0 = una placa distinta cada vez. Cualquier otro número " +
        "arma siempre la misma, que sirve para probar.")]
    [SerializeField] private int semilla;

    [Header("Líneas")]
    [Tooltip("Grosor de la línea de corte, en píxeles del Canvas.")]
    [SerializeField] private float grosor = 2f;

    [Tooltip("Dibujar también el borde de afuera de la placa.")]
    [SerializeField] private bool bordeExterior = true;

    [Tooltip("En cuántos trocitos se parte cada línea. Más trocitos = " +
        "el corte avanza más parejo, pero hay que ser más prolijo.")]
    [Min(1)]
    [SerializeField] private int trozosPorLinea = 4;

    [Tooltip("De qué color queda lo ya cortado. Con alfa 0 desaparece.")]
    [SerializeField] private Color colorCortado = new Color(1f, 1f, 1f, 0.15f);

    /// <summary>
    /// Se llama cuando una pieza queda suelta: todo su contorno cortado.
    /// Trae el número de la pieza; con CeldasDe se sabe de qué tamaño es.
    /// </summary>
    public event Action<int> PiezaLiberada;

    /// <summary>Una línea entre dos piezas, partida en trocitos.</summary>
    private class Linea
    {
        public Vector2 a;
        public Vector2 b;
        public int piezaA;
        public int piezaB;

        // El filo de la placa. Se quema igual que el resto (si no, pasar
        // la llama por ahí no haría nada y se vería muerto), pero no
        // sujeta a nadie: una pieza no necesita que lo cortes para salir.
        public bool exterior;

        // cuánto va quemado cada trocito, de 0 a 1
        public float[] trozos;

        // Se guarda en vez de recalcularse cada vez que se la mira: se
        // la pregunta muchas veces por frame, una por línea y por trocito.
        public bool cortada;

        public bool Revisar()
        {
            foreach (float t in trozos)
            {
                if (t < 1f)
                {
                    return false;
                }
            }
            cortada = true;
            return true;
        }
    }

    // a qué pieza pertenece cada celda
    private int[] _pieza;
    private int _piezas;
    private readonly List<Linea> _lineas = new List<Linea>();
    private readonly HashSet<int> _libres = new HashSet<int>();

    /// <summary>Cuántas piezas quedaron.</summary>
    public int Piezas => _piezas;

    public int Cols => cols;
    public int Rows => rows;

    /// <summary>A qué pieza pertenece una celda. -1 si está fuera de la placa.</summary>
    public int PiezaDe(int x, int y)
    {
        if (_pieza == null || x < 0 || y < 0 || x >= cols || y >= rows)
        {
            return -1;
        }
        return _pieza[y * cols + x];
    }

    /// <summary>De cuántas celdas es una pieza.</summary>
    public int CeldasDe(int pieza)
    {
        if (_pieza == null)
        {
            return 0;
        }
        int n = 0;
        foreach (int p in _pieza)
        {
            if (p == pieza)
            {
                n++;
            }
        }
        return n;
    }

    /// <summary>¿Ya se soltó esta pieza?</summary>
    public bool EstaLibre(int pieza) => _libres.Contains(pieza);

    /// <summary>¿Queda algo por cortar?</summary>
    public bool Terminada => _lineas.Count > 0 && _libres.Count >= _piezas - 1;

    /// <summary>El centro de una celda, en coordenadas locales de este objeto.</summary>
    public Vector2 CentroCelda(int x, int y)
    {
        Rect r = rectTransform.rect;
        return new Vector2(r.xMin + (x + 0.5f) * r.width / cols,
                           r.yMin + (y + 0.5f) * r.height / rows);
    }

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
        Generar();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (_pieza == null || _pieza.Length != cols * rows)
        {
            Generar();
        }
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        // cambiar la grilla en el Inspector rearma la placa
        if (_pieza != null && _pieza.Length != cols * rows)
        {
            Generar();
        }
    }
#endif

    // ------------------------------------------------------------------
    //  Cortar
    // ------------------------------------------------------------------

    /// <summary>
    /// Quema lo que haya bajo la punta. "punto" va en coordenadas
    /// locales de la placa (PlacaMovil.PuntoSoldador lo da así).
    /// Devuelve cuántos trocitos tocó: 0 = la punta está en el aire.
    /// </summary>
    public int Quemar(Vector2 punto, float radio, float cantidad)
    {
        if (_lineas.Count == 0 || cantidad <= 0f)
        {
            return 0;
        }

        int tocados = 0;
        float r2 = radio * radio;
        bool algoSeCorto = false;

        foreach (Linea l in _lineas)
        {
            if (l.cortada)
            {
                continue;
            }
            int n = l.trozos.Length;
            for (int i = 0; i < n; i++)
            {
                if (l.trozos[i] >= 1f)
                {
                    continue;
                }
                // el centro del trocito: la llama tiene ancho, así que
                // quema todos los que le caen cerca y no solo uno
                Vector2 c = Vector2.Lerp(l.a, l.b, (i + 0.5f) / n);
                if ((c - punto).sqrMagnitude > r2)
                {
                    continue;
                }
                l.trozos[i] = Mathf.Min(1f, l.trozos[i] + cantidad);
                tocados++;
            }
            if (l.Revisar())
            {
                algoSeCorto = true;
            }
        }

        if (tocados > 0)
        {
            SetVerticesDirty();
        }
        if (algoSeCorto)
        {
            RevisarPiezas();
        }
        return tocados;
    }

    /// <summary>
    /// Una pieza se suelta cuando TODAS las líneas que la separan de sus
    /// vecinas están cortadas. El borde de afuera no cuenta: ahí no hay
    /// nada que la sujete.
    /// </summary>
    private void RevisarPiezas()
    {
        for (int p = 0; p < _piezas; p++)
        {
            if (_libres.Contains(p))
            {
                continue;
            }
            bool tiene = false;
            bool suelta = true;
            foreach (Linea l in _lineas)
            {
                if (l.exterior || (l.piezaA != p && l.piezaB != p))
                {
                    continue;
                }
                tiene = true;
                if (!l.cortada)
                {
                    suelta = false;
                    break;
                }
            }
            if (tiene && suelta)
            {
                _libres.Add(p);
                PiezaLiberada?.Invoke(p);
            }
        }
    }

    // ------------------------------------------------------------------
    //  Generar
    // ------------------------------------------------------------------

    /// <summary>
    /// Reparte la placa de nuevo. Clic derecho en el componente →
    /// "Generar" para probar formas sin darle Play.
    /// </summary>
    [ContextMenu("Generar")]
    public void Generar()
    {
        int total = cols * rows;
        if (total <= 0)
        {
            return;
        }

        // Si hay semilla, se usa la tirada de Unity pero guardando y
        // devolviendo su estado: si no, fijarla acá le cambiaría la
        // suerte a todo lo demás del juego.
        //
        // UnityEngine.Random y no Random a secas: con "using System"
        // arriba, el nombre choca con System.Random y no compila.
        UnityEngine.Random.State estado = UnityEngine.Random.state;
        if (semilla != 0)
        {
            UnityEngine.Random.InitState(semilla);
        }

        _pieza = new int[total];
        for (int i = 0; i < total; i++)
        {
            _pieza[i] = -1;
        }

        var libres = new List<int>(total);
        for (int i = 0; i < total; i++)
        {
            libres.Add(i);
        }

        var frontera = new List<int>();
        _piezas = 0;
        int max = Mathf.Max(minCeldas, maxCeldas);

        while (libres.Count > 0)
        {
            int semillaCelda = libres[UnityEngine.Random.Range(0, libres.Count)];
            int meta = UnityEngine.Random.Range(minCeldas, max + 1);

            frontera.Clear();
            Tomar(semillaCelda, _piezas, libres, frontera);

            int tiene = 1;
            while (tiene < meta && frontera.Count > 0)
            {
                int k = UnityEngine.Random.Range(0, frontera.Count);
                int celda = frontera[k];
                frontera.RemoveAt(k);
                // pudo haberla tomado otra rama de la misma pieza
                if (_pieza[celda] != -1)
                {
                    continue;
                }
                Tomar(celda, _piezas, libres, frontera);
                tiene++;
            }
            _piezas++;
        }

        JuntarMigajas();
        ArmarLineas();
        _libres.Clear();

        if (semilla != 0)
        {
            UnityEngine.Random.state = estado;
        }
        SetVerticesDirty();
    }

    private void Tomar(int celda, int pieza, List<int> libres, List<int> frontera)
    {
        _pieza[celda] = pieza;
        libres.Remove(celda);

        int x = celda % cols;
        int y = celda / cols;
        Vecina(x - 1, y, frontera);
        Vecina(x + 1, y, frontera);
        Vecina(x, y - 1, frontera);
        Vecina(x, y + 1, frontera);
    }

    private void Vecina(int x, int y, List<int> frontera)
    {
        if (x < 0 || y < 0 || x >= cols || y >= rows)
        {
            return;
        }
        int i = y * cols + x;
        if (_pieza[i] == -1)
        {
            frontera.Add(i);
        }
    }

    /// <summary>
    /// Una pieza puede quedar encerrada antes de llegar a su tamaño: si
    /// quedó demasiado chica, se la regala a la vecina con la que más
    /// lados comparte. Si no, la placa termina con piezas de una celda
    /// que no valen el corte.
    /// </summary>
    private void JuntarMigajas()
    {
        var tamano = new int[_piezas];
        foreach (int p in _pieza)
        {
            tamano[p]++;
        }

        var vecinas = new Dictionary<int, int>();
        for (int p = 0; p < _piezas; p++)
        {
            if (tamano[p] >= minCeldas || tamano[p] == 0)
            {
                continue;
            }

            vecinas.Clear();
            for (int i = 0; i < _pieza.Length; i++)
            {
                if (_pieza[i] != p)
                {
                    continue;
                }
                int x = i % cols;
                int y = i / cols;
                Contar(x - 1, y, p, vecinas);
                Contar(x + 1, y, p, vecinas);
                Contar(x, y - 1, p, vecinas);
                Contar(x, y + 1, p, vecinas);
            }

            int mejor = -1;
            int cuantos = 0;
            foreach (KeyValuePair<int, int> v in vecinas)
            {
                if (v.Value > cuantos)
                {
                    cuantos = v.Value;
                    mejor = v.Key;
                }
            }
            if (mejor < 0)
            {
                continue;   // toda la placa es esta pieza
            }

            for (int i = 0; i < _pieza.Length; i++)
            {
                if (_pieza[i] == p)
                {
                    _pieza[i] = mejor;
                }
            }
            tamano[mejor] += tamano[p];
            tamano[p] = 0;
        }
    }

    private void Contar(int x, int y, int propia, Dictionary<int, int> vecinas)
    {
        int p = PiezaDe(x, y);
        if (p < 0 || p == propia)
        {
            return;
        }
        vecinas.TryGetValue(p, out int n);
        vecinas[p] = n + 1;
    }

    /// <summary>
    /// Las líneas de corte son los bordes entre celdas de piezas
    /// distintas. Se arman una sola vez al generar: después solo se
    /// quema lo que ya está acá.
    /// </summary>
    private void ArmarLineas()
    {
        _lineas.Clear();
        Rect r = rectTransform.rect;
        float cw = r.width / cols;
        float ch = r.height / rows;

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                int p = PiezaDe(x, y);

                int d = PiezaDe(x + 1, y);
                if (d >= 0 && d != p)
                {
                    float px = r.xMin + (x + 1) * cw;
                    Agregar(new Vector2(px, r.yMin + y * ch),
                        new Vector2(px, r.yMin + (y + 1) * ch), p, d);
                }

                int a = PiezaDe(x, y + 1);
                if (a >= 0 && a != p)
                {
                    float py = r.yMin + (y + 1) * ch;
                    Agregar(new Vector2(r.xMin + x * cw, py),
                        new Vector2(r.xMin + (x + 1) * cw, py), p, a);
                }

                if (!bordeExterior)
                {
                    continue;
                }
                // el filo de la placa, celda por celda: así se quema de a
                // tramos como cualquier otra línea
                if (x == 0)
                {
                    Borde(new Vector2(r.xMin, r.yMin + y * ch),
                        new Vector2(r.xMin, r.yMin + (y + 1) * ch), p);
                }
                if (x == cols - 1)
                {
                    Borde(new Vector2(r.xMax, r.yMin + y * ch),
                        new Vector2(r.xMax, r.yMin + (y + 1) * ch), p);
                }
                if (y == 0)
                {
                    Borde(new Vector2(r.xMin + x * cw, r.yMin),
                        new Vector2(r.xMin + (x + 1) * cw, r.yMin), p);
                }
                if (y == rows - 1)
                {
                    Borde(new Vector2(r.xMin + x * cw, r.yMax),
                        new Vector2(r.xMin + (x + 1) * cw, r.yMax), p);
                }
            }
        }
    }

    private void Agregar(Vector2 a, Vector2 b, int piezaA, int piezaB)
    {
        _lineas.Add(new Linea
        {
            a = a,
            b = b,
            piezaA = piezaA,
            piezaB = piezaB,
            trozos = new float[Mathf.Max(1, trozosPorLinea)],
        });
    }

    private void Borde(Vector2 a, Vector2 b, int pieza)
    {
        _lineas.Add(new Linea
        {
            a = a,
            b = b,
            piezaA = pieza,
            piezaB = -1,
            exterior = true,
            trozos = new float[Mathf.Max(1, trozosPorLinea)],
        });
    }

    // ------------------------------------------------------------------
    //  Dibujo
    // ------------------------------------------------------------------

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        // Acá NO se genera: Generar() ensucia los vértices y pedir un
        // redibujo desde adentro del redibujo deja a la UI dando vueltas
        // sin parar. Era el tirón que se sentía al cortar.
        if (_pieza == null)
        {
            return;
        }

        foreach (Linea l in _lineas)
        {
            int n = l.trozos.Length;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = Vector2.Lerp(l.a, l.b, (float)i / n);
                Vector2 b = Vector2.Lerp(l.a, l.b, (i + 1f) / n);
                Trazo(vh, a, b, Color.Lerp(color, colorCortado, l.trozos[i]));
            }
        }

    }

    private static readonly UIVertex[] _quad = new UIVertex[4];

    private void Trazo(VertexHelper vh, Vector2 a, Vector2 b, Color32 c)
    {
        Vector2 d = (b - a).normalized;
        Vector2 n = new Vector2(-d.y, d.x) * (grosor * 0.5f);
        // se estira medio grosor en cada punta: así dos tramos que se
        // cruzan cierran la esquina en vez de dejar una muesca
        a -= d * (grosor * 0.5f);
        b += d * (grosor * 0.5f);

        _quad[0] = Vertice(a - n, c);
        _quad[1] = Vertice(a + n, c);
        _quad[2] = Vertice(b + n, c);
        _quad[3] = Vertice(b - n, c);
        vh.AddUIVertexQuad(_quad);
    }

    private static UIVertex Vertice(Vector2 p, Color32 c)
    {
        UIVertex v = UIVertex.simpleVert;
        v.position = p;
        v.color = c;
        v.uv0 = Vector2.zero;
        return v;
    }
}
