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
    [Tooltip("Lo que cambia de color según si la zona tiene algo " +
        "montado o no. Puede ser un SpriteRenderer o una Light2D — " +
        "lo que tenga, se detecta solo. Dejalo vacío si no usás esto.")]
    [SerializeField] private Component luz;
    [SerializeField] private Color luzLibre = new Color(0.47f, 0.71f, 0.54f);
    [SerializeField] private Color luzOcupada = new Color(0.85f, 0.31f, 0.25f);

    [Header("Gizmos (solo editor)")]
    [SerializeField] private Color colorGizmo = new Color(0.4f, 0.8f, 1f, 0.5f);

    // Qué Modulo hay en cada celda. null = libre. Con cols*rows chico
    // (casi siempre 1x1) un array plano alcanza y sobra.
    private Modulo[] _celdas;

    public int Cols => cols;
    public int Rows => rows;

    private void Awake()
    {
        _celdas = new Modulo[cols * rows];
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
        foreach (Modulo m in _celdas)
        {
            if (m == null)
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
        Vector2 origen = new Vector2(-(cols - 1) * 0.5f, -(rows - 1) * 0.5f) * cellSize;
        Vector2 local = origen + new Vector2(cx, cy) * cellSize;
        return transform.TransformPoint(local);
    }

    /// <summary>
    /// Monta el módulo en (cx, cy): lo reparenta, lo centra en la
    /// celda y lo gira según salidaGrados. Devuelve false si la celda
    /// está ocupada, el tipo no matchea, o el índice no existe.
    /// </summary>
    public bool Montar(Modulo m, int cx = 0, int cy = 0)
    {
        if (!EnRango(cx, cy) || !AceptaA(m) || _celdas[cy * cols + cx] != null)
        {
            return false;
        }

        _celdas[cy * cols + cx] = m;
        m.SetZona(this);

        Transform t = m.transform;
        t.SetParent(transform, worldPositionStays: false);
        Vector2 origen = new Vector2(-(cols - 1) * 0.5f, -(rows - 1) * 0.5f) * cellSize;
        t.localPosition = origen + new Vector2(cx, cy) * cellSize;
        t.localRotation = Quaternion.Euler(0f, 0f, salidaGrados);

        // Un Rigidbody2D montado en algo que ahora es su padre tiene
        // que pasar a Kinematic: si sigue Dynamic, la física lo sigue
        // moviendo en coordenadas de MUNDO e ignora que tiene padre —
        // el mismo bug que tenía el "Dynamic Sprite" fantasma de la
        // nave, pero a propósito evitado acá.
        if (m.TryGetComponent(out Rigidbody2D rb))
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
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

        _celdas[cy * cols + cx] = null;
        m.SetZona(null);
        m.transform.SetParent(null, worldPositionStays: true);

        if (m.TryGetComponent(out Rigidbody2D rb))
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
        }

        RefrescarLuz();
    }

    private bool EnRango(int cx, int cy)
    {
        return cx >= 0 && cx < cols && cy >= 0 && cy < rows;
    }

    /// <summary>
    /// Pinta la luz según si hay algo montado. Soporta SpriteRenderer
    /// (le cambia .color) y Light2D del URP 2D Renderer (le cambia
    /// .color también, mismo nombre de propiedad) sin acoplarse a
    /// cuál de los dos es — por eso "luz" es un Behaviour genérico y
    /// esto prueba con SendMessage... en realidad más simple: se
    /// castea a lo que haya.
    /// </summary>
    private void RefrescarLuz()
    {
        if (luz == null)
        {
            return;
        }
        Color c = HayLibre() ? luzLibre : luzOcupada;

        if (luz is SpriteRenderer sr)
        {
            sr.color = c;
        }
        else if (luz is UnityEngine.Rendering.Universal.Light2D l2d)
        {
            l2d.color = c;
        }
    }

    private void OnDrawGizmos()
    {
        // CellCenterWorld ya pasa por TransformPoint, así que el
        // CENTRO de cada celda sí respeta la Scale del objeto (y la de
        // sus padres). Pero DrawWireCube no escala el tamaño que le
        // pasás — así que si no lo multiplico a mano por lossyScale,
        // el cuadrito queda chico/grande respecto a dónde está
        // realmente, y las celdas se ven separadas aunque no lo estén.
        Vector3 escala = transform.lossyScale;
        Vector3 tamano = Vector3.Scale(new Vector3(cellSize, cellSize, 0f) * 0.95f, escala);

        Gizmos.color = colorGizmo;
        for (int cy = 0; cy < rows; cy++)
        {
            for (int cx = 0; cx < cols; cx++)
            {
                Vector3 centro = CellCenterWorld(cx, cy);
                Gizmos.DrawWireCube(centro, tamano);
            }
        }
    }
}
