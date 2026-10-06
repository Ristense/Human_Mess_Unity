using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Una zona de montaje: una grilla de celdas donde se pueden enchufar
/// Modulo. Sirve para el socket de la mano, los slots de herramientas
/// de la nave, los anclajes de un satélite — todo lo que en el juego
/// es "acá se pone una cosa" usa esto.
///
/// Montar() reparenta el Modulo como hijo y lo deja en la posición
/// local de la celda — nada de joints ni de física para esto, porque
/// una vez montado es, literal, PARTE de la nave: se mueve con ella
/// porque es su hijo, no porque algo lo esté sosteniendo.
/// </summary>
public class Zona : MonoBehaviour
{
    [SerializeField] private int cols = 1;
    [SerializeField] private int rows = 1;
    [SerializeField] private float cellSize = 0.5f;

    [Tooltip("Qué Modulo.tipo acepta esta zona. Vacío = acepta cualquiera.")]
    [SerializeField] private string acepta = "modulo";

    [Tooltip("Grados que se le suman a la rotación del módulo al " +
        "montarlo, para que mire \"para afuera\" como corresponda en " +
        "este socket.")]
    [SerializeField] private float salidaGrados = 0f;

    [Header("Luz (opcional)")]
    [Tooltip("El objeto padre de la lucecita de este socket. Se le " +
        "pinta TODO lo que cuelgue: los SpriteRenderer (el neón, el " +
        "glow) y las Light2D, en cualquier nivel. Arrastrá el padre y " +
        "listo. Vacío = no usás esto.")]
    [SerializeField] private Transform luz;

    [Tooltip("Cuándo pasa a \"ocupada\". Con una sola luz para una " +
        "zona de varias celdas tenés que elegir qué te está diciendo: " +
        "si ya no entra nada más (Llena), o si hay algo puesto (Algo).")]
    [SerializeField] private ModoLuz modoLuz = ModoLuz.CuandoHayAlgo;

    [SerializeField] private Color luzLibre = new Color(0.47f, 0.71f, 0.54f);
    [SerializeField] private Color luzOcupada = new Color(0.85f, 0.31f, 0.25f);

    public enum ModoLuz
    {
        /// <summary>Roja apenas montás algo. Con 1 celda da igual que la otra.</summary>
        CuandoHayAlgo,

        /// <summary>Roja solo cuando no queda ni una celda libre.</summary>
        CuandoSeLlena,
    }

    [Header("Gizmos (solo editor)")]
    [SerializeField] private Color colorGizmo = new Color(0.4f, 0.8f, 1f, 0.5f);

    // Qué Modulo hay en cada celda. null = libre. Con cols*rows chico
    // (casi siempre 1x1) un array plano alcanza y sobra.
    private Modulo[] _celdas;

    public int Cols => cols;
    public int Rows => rows;

    // Todas las zonas vivas. Lo llevamos acá y no buscándolas con
    // FindObjectsByType cada vez que soltás algo — eso es carísimo y
    // además caería justo en el peor momento, un frame de gameplay.
    private static readonly List<Zona> _todas = new List<Zona>();

    private void OnEnable()
    {
        if (_celdas == null)
        {
            _celdas = new Modulo[cols * rows];
        }
        _todas.Add(this);
        // arranca con el color correcto, sin esperar a que montes algo
        RefrescarLuz();
    }

    private void OnDisable()
    {
        _todas.Remove(this);
    }

    /// <summary>Lo que está montado en (cx, cy), o null si está libre.</summary>
    public Modulo Montado(int cx = 0, int cy = 0)
    {
        if (!EnRango(cx, cy))
        {
            return null;
        }
        return _celdas[cy * cols + cx];
    }

    /// <summary>¿Hay al menos una celda libre en toda la zona?</summary>
    public bool HayLibre()
    {
        if (_celdas == null)
        {
            return false;
        }
        foreach (Modulo m in _celdas)
        {
            if (m == null)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>¿Hay al menos una celda con algo montado?</summary>
    public bool HayAlgoMontado()
    {
        if (_celdas == null)
        {
            return false;
        }
        foreach (Modulo m in _celdas)
        {
            if (m != null)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>¿Este módulo en particular puede montarse acá?</summary>
    public bool AceptaA(Modulo m)
    {
        if (m == null)
        {
            return false;
        }
        return string.IsNullOrEmpty(acepta) || m.tipo == acepta;
    }

    /// <summary>
    /// Centro de la celda (cx, cy) en coordenadas de MUNDO. Las celdas
    /// se acomodan centradas en la zona, igual que en Godot.
    /// </summary>
    public Vector2 CellCenterWorld(int cx, int cy)
    {
        return BloqueCentroWorld(cx, cy, 1, 1);
    }

    /// <summary>
    /// Centro, en MUNDO, de un bloque de w×h celdas apoyado con su
    /// esquina en (cx, cy). Con w=h=1 es el centro de esa celda sola.
    ///
    /// Un módulo de 2 celdas de ancho no va centrado en su celda de
    /// origen sino medio paso más allá — si no, queda colgando media
    /// celda para afuera de la zona.
    /// </summary>
    public Vector2 BloqueCentroWorld(int cx, int cy, int w, int h)
    {
        Vector2 origen = new Vector2(-(cols - 1) * 0.5f, -(rows - 1) * 0.5f) * cellSize;
        Vector2 local = origen + new Vector2(
            cx + (w - 1) * 0.5f,
            cy + (h - 1) * 0.5f) * cellSize;
        return transform.TransformPoint(local);
    }

    /// <summary>
    /// ¿Entra este módulo entero con la esquina en (cx, cy)? Chequea
    /// que todas las celdas que va a tapar existan y estén libres.
    /// </summary>
    public bool Cabe(Modulo m, int cx, int cy)
    {
        if (_celdas == null || !AceptaA(m))
        {
            return false;
        }
        for (int y = cy; y < cy + m.rows; y++)
        {
            for (int x = cx; x < cx + m.cols; x++)
            {
                if (!EnRango(x, y) || _celdas[y * cols + x] != null)
                {
                    return false;
                }
            }
        }
        return true;
    }

    /// <summary>
    /// Monta el módulo con su esquina en (cx, cy): reserva todas las
    /// celdas que ocupa, lo reparenta, lo centra en el bloque y lo gira
    /// según salidaGrados. Devuelve false si no cabe.
    /// </summary>
    public bool Montar(Modulo m, int cx = 0, int cy = 0)
    {
        if (!Cabe(m, cx, cy))
        {
            return false;
        }

        // Todas las celdas del bloque apuntan al MISMO módulo. Así,
        // mirar cualquiera de ellas te dice que está ocupada y por
        // quién, sin tener que llevar una lista aparte de bloques.
        for (int y = cy; y < cy + m.rows; y++)
        {
            for (int x = cx; x < cx + m.cols; x++)
            {
                _celdas[y * cols + x] = m;
            }
        }
        m.SetZona(this);

        Transform t = m.transform;
        t.SetParent(transform, worldPositionStays: false);
        t.localPosition = transform.InverseTransformPoint(
            BloqueCentroWorld(cx, cy, m.cols, m.rows));
        t.localRotation = Quaternion.Euler(0f, 0f, salidaGrados);

        // Montado = deja de ser un cuerpo físico y pasa a ser, lisa y
        // llanamente, parte de su padre.
        //
        // Kinematic NO alcanza: un Rigidbody2D, del tipo que sea,
        // simula en coordenadas de MUNDO e ignora que tiene padre, así
        // que si el padre se mueve la pieza se queda atrás. Con
        // simulated = false la física lo suelta del todo y el Transform
        // vuelve a mandar — recién ahí sigue al padre de verdad.
        //
        // Esto es lo que permite anidar: una herramienta montada en un
        // módulo que a su vez está montado en la nave se mueve con
        // todo, sin que nadie la esté sosteniendo.
        if (m.TryGetComponent(out Rigidbody2D rb))
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = false;
        }

        RefrescarLuz();
        return true;
    }

    /// <summary>Saca lo que haya en (cx, cy) y lo deja suelto en el mundo, como Dynamic de nuevo.</summary>
    public void Sacar(int cx = 0, int cy = 0)
    {
        if (!EnRango(cx, cy))
        {
            return;
        }
        Modulo m = _celdas[cy * cols + cx];
        if (m == null)
        {
            return;
        }

        // Libera TODAS las celdas que ocupaba, no solo la que tocaste:
        // un módulo de 2x1 dejaría media huella ocupada para siempre.
        for (int i = 0; i < _celdas.Length; i++)
        {
            if (_celdas[i] == m)
            {
                _celdas[i] = null;
            }
        }
        m.SetZona(null);
        m.transform.SetParent(null, worldPositionStays: true);

        if (m.TryGetComponent(out Rigidbody2D rb))
        {
            rb.simulated = true;
            rb.bodyType = RigidbodyType2D.Dynamic;
        }

        RefrescarLuz();
    }

    /// <summary>
    /// El hueco de esta zona donde el módulo entra ENTERO que quede
    /// más cerca de "pos" (en mundo). Devuelve la esquina del bloque y
    /// qué tan lejos quedó su centro, al cuadrado.
    /// </summary>
    public bool HuecoMasCerca(Modulo m, Vector2 pos, out int cx, out int cy, out float distSq)
    {
        cx = 0;
        cy = 0;
        distSq = float.MaxValue;
        if (_celdas == null || !AceptaA(m))
        {
            return false;
        }

        bool hay = false;
        // El límite es rows - m.rows: más allá de ahí el módulo ya se
        // sale de la grilla, no tiene sentido ni probar.
        for (int y = 0; y <= rows - m.rows; y++)
        {
            for (int x = 0; x <= cols - m.cols; x++)
            {
                if (!Cabe(m, x, y))
                {
                    continue;
                }
                // distancias al cuadrado: mismo orden de cercanía, sin
                // pagar la raíz cuadrada en cada medición
                Vector2 centro = BloqueCentroWorld(x, y, m.cols, m.rows);
                float d = (centro - pos).sqrMagnitude;
                if (d < distSq)
                {
                    distSq = d;
                    cx = x;
                    cy = y;
                    hay = true;
                }
            }
        }
        return hay;
    }

    /// <summary>
    /// Entre TODAS las zonas de la escena, la celda libre más cercana
    /// a "pos" que acepte este módulo, siempre que caiga dentro de
    /// "radio". Esto es lo que hace que soltar una pieza cerca de un
    /// socket la enchufe sola.
    /// </summary>
    public static bool MejorCelda(Modulo m, Vector2 pos, float radio,
        out Zona zona, out int cx, out int cy)
    {
        zona = null;
        cx = 0;
        cy = 0;
        float mejor = radio * radio;

        foreach (Zona z in _todas)
        {
            if (!z.HuecoMasCerca(m, pos, out int x, out int y, out float d))
            {
                continue;
            }
            if (d <= mejor)
            {
                mejor = d;
                zona = z;
                cx = x;
                cy = y;
            }
        }
        return zona != null;
    }

    /// <summary>
    /// El Modulo MONTADO más cercano a "pos", dentro de "radio".
    ///
    /// Hace falta porque un módulo enchufado tiene simulated = false:
    /// no tiene colliders activos, así que Physics2D no lo encuentra.
    /// Es a propósito — montado no es un cuerpo, es parte de la nave —
    /// pero igual lo tenés que poder volver a agarrar.
    /// </summary>
    public static Modulo MontadoCerca(Vector2 pos, float radio)
    {
        Modulo mejor = null;
        float dist = radio * radio;

        foreach (Zona z in _todas)
        {
            if (z._celdas == null)
            {
                continue;
            }
            for (int i = 0; i < z._celdas.Length; i++)
            {
                Modulo m = z._celdas[i];
                if (m == null)
                {
                    continue;
                }
                float d = ((Vector2)m.transform.position - pos).sqrMagnitude;
                if (d <= dist)
                {
                    dist = d;
                    mejor = m;
                }
            }
        }
        return mejor;
    }

    /// <summary>
    /// Saca este módulo de la celda donde esté, sin que quien llama
    /// tenga que acordarse de en cuál lo había puesto.
    /// </summary>
    public void SacarA(Modulo m)
    {
        if (_celdas == null)
        {
            return;
        }
        for (int i = 0; i < _celdas.Length; i++)
        {
            if (_celdas[i] == m)
            {
                Sacar(i % cols, i / cols);
                return;
            }
        }
    }

    private bool EnRango(int cx, int cy)
    {
        return cx >= 0 && cx < cols && cy >= 0 && cy < rows;
    }

    /// <summary>
    /// Pinta la lucecita del socket según si queda lugar o no.
    /// </summary>
    private void RefrescarLuz()
    {
        if (luz == null)
        {
            return;
        }
        bool ocupada = modoLuz == ModoLuz.CuandoHayAlgo
            ? HayAlgoMontado()
            : !HayLibre();

        Pintar(luz, ocupada ? luzOcupada : luzLibre);
    }

    /// <summary>
    /// Pinta de un color todo lo pintable que cuelgue de "t", a
    /// cualquier profundidad: los SpriteRenderer y las Light2D.
    ///
    /// Va recursivo porque una luz de verdad casi nunca es un solo
    /// objeto — es el neón, más el halo, más la Light2D que ilumina
    /// alrededor. Todos tienen que cambiar juntos o se ve mal.
    ///
    /// En el sprite se cambia el .color, que el shader Neon multiplica
    /// por su color HDR: así el tinte funciona sin tocar el material
    /// (que es compartido, y tocarlo pintaría TODOS los sockets).
    /// </summary>
    private static void Pintar(Transform t, Color c)
    {
        if (t.TryGetComponent(out SpriteRenderer sr))
        {
            sr.color = c;
        }
        if (t.TryGetComponent(out UnityEngine.Rendering.Universal.Light2D l2d))
        {
            l2d.color = c;
        }

        for (int i = 0; i < t.childCount; i++)
        {
            Pintar(t.GetChild(i), c);
        }
    }

    private void OnDrawGizmos()
    {
        // DrawWireCube dibuja SIEMPRE alineado a los ejes del mundo y
        // sin escala: le des el centro que le des, el cuadrito te sale
        // derechito y del tamaño crudo. Por eso, con la nave girada,
        // las celdas se veían sueltas del casco.
        //
        // La solución es no corregir nada a mano: se le pasa la matriz
        // del objeto a Gizmos y a partir de ahí dibujamos en
        // coordenadas LOCALES. Rotación, posición y escala — las tres
        // juntas — las aplica él.
        Matrix4x4 previa = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = colorGizmo;

        Vector2 origen = new Vector2(-(cols - 1) * 0.5f, -(rows - 1) * 0.5f) * cellSize;
        Vector3 tamano = new Vector3(cellSize, cellSize, 0f) * 0.95f;

        for (int cy = 0; cy < rows; cy++)
        {
            for (int cx = 0; cx < cols; cx++)
            {
                Vector3 centro = origen + new Vector2(cx, cy) * cellSize;
                Gizmos.DrawWireCube(centro, tamano);
            }
        }

        // devolver la matriz: Gizmos es global, si la dejás pisada le
        // arruinás el dibujo a cualquier otro script que venga después
        Gizmos.matrix = previa;
    }
}
