using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// El mapa: una grilla de sectores. El mundo es UNO SOLO y continuo —
/// los sectores son una grilla LÓGICA encima del espacio para saber dónde
/// estás, qué sembrar en cada lado y qué llevás limpiado.
///
/// La grilla es un MOLDE (3x3 por defecto), y en cada partida se prende
/// solo una parte: entre Min y Max sectores, siempre conectados por los
/// lados. La forma crece desde el sector inicial, sumando cada vez un
/// vecino al azar de los que ya están prendidos — por eso no puede
/// quedar ningún pedazo suelto, y el sector inicial (donde está la
/// base) existe siempre.
///
/// Coordenadas con signo, centro en (0, 0) y la Y para ARRIBA, como el
/// mundo: en un 3x3 van de (-1, -1) abajo-izquierda a (1, 1)
/// arriba-derecha. (0, -1) es el de abajo al medio.
///
/// sectorSize está en unidades de MUNDO, no en píxeles.
/// </summary>
public class Mapa : MonoBehaviour
{
    [Header("Molde")]
    [SerializeField] private int cols = 3;
    [SerializeField] private int rows = 3;

    [Tooltip("Cuánto mide un sector en unidades de mundo.")]
    [SerializeField] private Vector2 sectorSize = new Vector2(32f, 18f);

    [Header("Forma de esta partida")]
    [Tooltip("El sector donde arrancás. Siempre queda prendido: la " +
        "forma crece desde acá.")]
    [SerializeField] private Vector2Int sectorInicial = Vector2Int.zero;

    [Min(1)]
    [SerializeField] private int minSectores = 2;

    [Min(1)]
    [SerializeField] private int maxSectores = 9;

    [Tooltip("Forma nueva en cada partida. Apagalo para que salga " +
        "siempre la de World Seed — sirve para probar una forma puntual.")]
    [SerializeField] private bool formaAleatoria = true;

    [Tooltip("Con Forma Aleatoria apagada, decide la forma y lo que se " +
        "siembra. Cambialo para probar otra.")]
    [SerializeField] private int worldSeed = 0;

    [Header("Gizmos (solo editor)")]
    [SerializeField] private bool dibujarGizmos = true;
    [SerializeField] private Color colorLinea = new Color(0.4f, 0.8f, 1f, 0.6f);
    [SerializeField] private Color colorApagado = new Color(0.4f, 0.8f, 1f, 0.1f);
    [SerializeField] private Color colorSectorInicial = new Color(0.3f, 1f, 0.5f, 0.15f);

    private HashSet<Vector2Int> _activos;
    private int _semilla;

    public int Cols => cols;
    public int Rows => rows;
    public Vector2 SectorSize => sectorSize;
    public Vector2Int SectorInicial => sectorInicial;

    /// <summary>La semilla de esta partida, por si querés repetir una forma que te gustó.</summary>
    public int Semilla => _semilla;

    private void Awake()
    {
        Generar();
    }

    private void OnValidate()
    {
        // tocás algo en el Inspector y el gizmo se rearma con lo nuevo
        _activos = null;
    }

    // ------------------------------------------------------------------
    //  La forma
    // ------------------------------------------------------------------

    /// <summary>
    /// Arma la forma de esta partida. Corre solo al arrancar; llamalo de
    /// nuevo si querés otro mapa sin reiniciar.
    /// </summary>
    public void Generar()
    {
        _semilla = formaAleatoria && Application.isPlaying
            ? Random.Range(int.MinValue, int.MaxValue)
            : worldSeed;
        var rng = new System.Random(_semilla);

        List<Vector2Int> molde = Molde();
        Vector2Int inicio = Recortado(sectorInicial);

        int min = Mathf.Clamp(minSectores, 1, molde.Count);
        int max = Mathf.Clamp(maxSectores, min, molde.Count);
        int objetivo = rng.Next(min, max + 1);

        _activos = new HashSet<Vector2Int> { inicio };

        // La frontera son los vecinos de lo prendido que todavía están
        // apagados. Sacar siempre de acá es lo que garantiza que todo
        // quede conectado: nunca se prende algo que no toque la forma.
        var frontera = new List<Vector2Int>();
        SumarVecinos(inicio, frontera);

        while (_activos.Count < objetivo && frontera.Count > 0)
        {
            int i = rng.Next(frontera.Count);
            Vector2Int s = frontera[i];
            frontera.RemoveAt(i);

            if (_activos.Add(s))
            {
                SumarVecinos(s, frontera);
            }
        }
    }

    private static readonly Vector2Int[] Lados =
    {
        Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down,
    };

    private void SumarVecinos(Vector2Int s, List<Vector2Int> frontera)
    {
        foreach (Vector2Int d in Lados)
        {
            Vector2Int v = s + d;
            if (EnMolde(v) && !_activos.Contains(v))
            {
                frontera.Add(v);
            }
        }
    }

    private HashSet<Vector2Int> Activos()
    {
        if (_activos == null)
        {
            Generar();
        }
        return _activos;
    }

    /// <summary>¿Este sector está prendido en esta partida?</summary>
    public bool Activo(Vector2Int s)
    {
        return Activos().Contains(s);
    }

    /// <summary>Cuántos sectores a cada lado del centro. En un 3x3, (1, 1).</summary>
    private Vector2Int Half()
    {
        return new Vector2Int((cols - 1) / 2, (rows - 1) / 2);
    }

    private bool EnMolde(Vector2Int s)
    {
        Vector2Int h = Half();
        return s.x >= -h.x && s.x <= h.x && s.y >= -h.y && s.y <= h.y;
    }

    private Vector2Int Recortado(Vector2Int s)
    {
        Vector2Int h = Half();
        return new Vector2Int(Mathf.Clamp(s.x, -h.x, h.x), Mathf.Clamp(s.y, -h.y, h.y));
    }

    /// <summary>
    /// Todos los casilleros del molde, prendidos o no. Para dibujar el
    /// mapa entero con los huecos a la vista.
    /// </summary>
    public List<Vector2Int> TodosLosSectores() => Molde();

    /// <summary>Todos los casilleros del molde, prendidos o no.</summary>
    private List<Vector2Int> Molde()
    {
        var salida = new List<Vector2Int>();
        Vector2Int h = Half();
        for (int y = -h.y; y <= h.y; y++)
        {
            for (int x = -h.x; x <= h.x; x++)
            {
                salida.Add(new Vector2Int(x, y));
            }
        }
        return salida;
    }

    // ------------------------------------------------------------------
    //  Consultas
    // ------------------------------------------------------------------

    /// <summary>Los sectores PRENDIDOS de esta partida, para recorrerlos con un foreach.</summary>
    public List<Vector2Int> AllSectors()
    {
        var salida = new List<Vector2Int>();
        foreach (Vector2Int s in Molde())
        {
            if (Activo(s))
            {
                salida.Add(s);
            }
        }
        return salida;
    }

    /// <summary>
    /// El rectángulo del molde entero. OJO: con sectores apagados,
    /// adentro de este rectángulo hay huecos. Para saber si un punto
    /// está en el mapa de verdad, usá Contiene().
    /// </summary>
    public Rect WorldBounds()
    {
        Vector2 size = new Vector2(cols, rows) * sectorSize;
        Vector2 origen = (Vector2)transform.position - size * 0.5f;
        return new Rect(origen, size);
    }

    /// <summary>El centro en mundo del sector (sx, sy).</summary>
    public Vector2 SectorCenterWorld(Vector2Int s)
    {
        return (Vector2)transform.position + new Vector2(s.x, s.y) * sectorSize;
    }

    private Rect RectSector(Vector2Int s)
    {
        return new Rect(SectorCenterWorld(s) - sectorSize * 0.5f, sectorSize);
    }

    /// <summary>
    /// En qué sector cae un punto del mundo. Se recorta al molde: un
    /// punto afuera devuelve el del borde más cercano.
    /// </summary>
    public Vector2Int WorldToSector(Vector2 worldPos)
    {
        return Recortado(SectorCrudo(worldPos));
    }

    private Vector2Int SectorCrudo(Vector2 worldPos)
    {
        Vector2 rel = worldPos - (Vector2)transform.position;
        return new Vector2Int(
            Mathf.RoundToInt(rel.x / sectorSize.x),
            Mathf.RoundToInt(rel.y / sectorSize.y));
    }

    /// <summary>¿Este punto cae dentro de un sector prendido?</summary>
    public bool Contiene(Vector2 worldPos)
    {
        Vector2Int s = SectorCrudo(worldPos);
        return EnMolde(s) && Activo(s);
    }

    /// <summary>
    /// El punto más cercano que esté dentro del mapa. Si ya estaba
    /// adentro, devuelve el mismo y "afuera" queda en cero; si no,
    /// "afuera" es la dirección en la que se había salido.
    ///
    /// Con un mapa con huecos no alcanza con recortar contra un
    /// rectángulo: hay que buscar el más cercano entre todos los
    /// sectores prendidos. Como son rectángulos alineados, el punto más
    /// cercano de la unión es el más cercano de entre cada uno.
    /// </summary>
    public Vector2 Recortar(Vector2 worldPos, out Vector2 afuera)
    {
        afuera = Vector2.zero;
        if (Contiene(worldPos))
        {
            return worldPos;
        }

        Vector2 mejor = worldPos;
        float mejorDist = float.MaxValue;
        foreach (Vector2Int s in AllSectors())
        {
            Rect r = RectSector(s);
            Vector2 p = new Vector2(
                Mathf.Clamp(worldPos.x, r.xMin, r.xMax),
                Mathf.Clamp(worldPos.y, r.yMin, r.yMax));
            float d = (p - worldPos).sqrMagnitude;
            if (d < mejorDist)
            {
                mejorDist = d;
                mejor = p;
            }
        }

        Vector2 salida = worldPos - mejor;
        if (salida.sqrMagnitude > 0f)
        {
            afuera = salida.normalized;
        }
        return mejor;
    }

    /// <summary>
    /// Semilla estable de un sector: la misma coordenada da siempre el
    /// mismo número dentro de una partida. Cambia con la semilla de la
    /// partida, así que una forma nueva trae también contenido nuevo.
    /// </summary>
    public int SectorSeed(Vector2Int s)
    {
        int h = s.x * 73856093 ^ s.y * 19349663;
        return h ^ _semilla;
    }

    // ------------------------------------------------------------------
    //  Contorno
    // ------------------------------------------------------------------

    /// <summary>Una esquina del contorno y hacia dónde queda "adentro".</summary>
    public struct Esquina
    {
        public Vector2 posicion;
        public Vector2 adentro;
    }

    /// <summary>
    /// Las esquinas del borde de la forma de esta partida — también las
    /// de adentro, en las formas en L o en T.
    ///
    /// Un vértice de la grilla es esquina según cuántos de los 4
    /// sectores que lo tocan están prendidos: 1 es una esquina hacia
    /// afuera, 3 una hacia adentro, y 2 en diagonal es un punto donde la
    /// forma se pellizca. Con 0, 4, o 2 de un mismo lado, es un tramo de
    /// borde recto o no es borde: no se marca.
    /// </summary>
    public List<Esquina> EsquinasContorno()
    {
        var salida = new List<Esquina>();
        Vector2Int h = Half();

        for (int j = -h.y; j <= h.y + 1; j++)
        {
            for (int i = -h.x; i <= h.x + 1; i++)
            {
                // los 4 sectores que comparten este vértice, en orden:
                // abajo-izq, abajo-der, arriba-der, arriba-izq. Dar la
                // vuelta en orden es lo que deja reconocer la diagonal:
                // son los que quedan enfrentados (0 con 2, 1 con 3).
                Vector2Int[] toca =
                {
                    new Vector2Int(i - 1, j - 1),
                    new Vector2Int(i, j - 1),
                    new Vector2Int(i, j),
                    new Vector2Int(i - 1, j),
                };

                int n = 0;
                Vector2 suma = Vector2.zero;
                bool[] prendido = new bool[4];
                for (int k = 0; k < 4; k++)
                {
                    prendido[k] = Activo(toca[k]);
                    if (prendido[k])
                    {
                        n++;
                        suma += SectorCenterWorld(toca[k]);
                    }
                }

                bool diagonal = n == 2 && prendido[0] == prendido[2];
                if (n != 1 && n != 3 && !diagonal)
                {
                    continue;
                }

                Vector2 pos = (Vector2)transform.position + new Vector2(
                    (i - 0.5f) * sectorSize.x,
                    (j - 0.5f) * sectorSize.y);

                // "adentro" = el promedio de los sectores prendidos que
                // tocan la esquina; sirve para girar el marcador
                salida.Add(new Esquina { posicion = pos, adentro = suma / n });
            }
        }
        return salida;
    }

    /// <summary>
    /// Cuánto empuja el borde hacia adentro en este punto, como un
    /// vector de largo 0 a 1 (más cerca del borde, más largo). Cero si
    /// estás lejos de cualquier borde, o directamente afuera — de eso
    /// se encarga Recortar.
    ///
    /// Solo cuentan los lados del sector cuyo vecino está APAGADO: es
    /// la misma regla que dibuja el contorno, así que empuja
    /// exactamente donde ves la línea y no en las paredes internas.
    ///
    /// Los lados se suman en vez de quedarse con el más cercano: en una
    /// esquina los dos empujan y salís en diagonal, en vez de que la
    /// fuerza salte de una pared a la otra.
    /// </summary>
    public Vector2 EmpujeDesdeBorde(Vector2 worldPos, float margen)
    {
        if (margen <= 0f)
        {
            return Vector2.zero;
        }

        Vector2Int s = WorldToSector(worldPos);
        if (!Activo(s))
        {
            return Vector2.zero;
        }

        Rect r = RectSector(s);
        Vector2 e = Vector2.zero;
        if (!Activo(s + Vector2Int.left))
        {
            e += Vector2.right * Cerca(worldPos.x - r.xMin, margen);
        }
        if (!Activo(s + Vector2Int.right))
        {
            e += Vector2.left * Cerca(r.xMax - worldPos.x, margen);
        }
        if (!Activo(s + Vector2Int.down))
        {
            e += Vector2.up * Cerca(worldPos.y - r.yMin, margen);
        }
        if (!Activo(s + Vector2Int.up))
        {
            e += Vector2.down * Cerca(r.yMax - worldPos.y, margen);
        }
        return e;
    }

    // 0 en el límite del margen, 1 pegado a la pared
    private static float Cerca(float distancia, float margen)
    {
        return distancia >= margen ? 0f : 1f - Mathf.Max(distancia, 0f) / margen;
    }

    /// <summary>
    /// El contorno de la forma de esta partida, como una o más vueltas
    /// cerradas de puntos en mundo. Solo el BORDE: las paredes entre dos
    /// sectores prendidos no van, porque ahí no hay nada que delimitar.
    ///
    /// Se arma con los lados de cada sector prendido cuyo vecino está
    /// apagado, encadenados por sus puntas. Mirar lado por lado y no la
    /// forma entera es lo que hace que salga bien con cualquier figura:
    /// una L, una T, o dos brazos que se tocan solo por una diagonal.
    ///
    /// Puede devolver más de una vuelta: si la forma encierra un hueco,
    /// el agujero es su propio contorno.
    ///
    /// Los puntos en línea recta se juntan en uno solo, así un lado
    /// largo son dos puntos y no uno por sector.
    /// </summary>
    public List<List<Vector2>> ContornoBorde()
    {
        // De cada vértice, a qué vértices sale un lado del borde. Se
        // trabaja en coordenadas ENTERAS de la grilla y no en mundo: dos
        // sectores vecinos tienen que compartir el vértice exacto, y
        // comparar floats para eso es pedir que algún día no cierre.
        var salidas = new Dictionary<Vector2Int, List<Vector2Int>>();

        foreach (Vector2Int s in Activos())
        {
            // Los lados van en sentido antihorario, con lo prendido
            // siempre a la IZQUIERDA. Así las puntas de un lado calzan
            // con las del siguiente y la vuelta se encadena sola.
            Vector2Int ai = new Vector2Int(s.x, s.y);
            Vector2Int ad = new Vector2Int(s.x + 1, s.y);
            Vector2Int rd = new Vector2Int(s.x + 1, s.y + 1);
            Vector2Int ri = new Vector2Int(s.x, s.y + 1);

            if (!Activo(s + Vector2Int.down)) Sumar(salidas, ai, ad);
            if (!Activo(s + Vector2Int.right)) Sumar(salidas, ad, rd);
            if (!Activo(s + Vector2Int.up)) Sumar(salidas, rd, ri);
            if (!Activo(s + Vector2Int.left)) Sumar(salidas, ri, ai);
        }

        var vueltas = new List<List<Vector2>>();
        while (salidas.Count > 0)
        {
            var vuelta = new List<Vector2Int>();
            Vector2Int inicio = PrimeraClave(salidas);
            Vector2Int v = inicio;

            while (Tomar(salidas, v, out Vector2Int siguiente))
            {
                vuelta.Add(v);
                v = siguiente;
                if (v == inicio)
                {
                    break;
                }
            }

            if (vuelta.Count >= 3)
            {
                vueltas.Add(Enderezar(vuelta));
            }
        }
        return vueltas;
    }

    private static void Sumar(Dictionary<Vector2Int, List<Vector2Int>> d,
        Vector2Int desde, Vector2Int hasta)
    {
        if (!d.TryGetValue(desde, out List<Vector2Int> lista))
        {
            lista = new List<Vector2Int>();
            d[desde] = lista;
        }
        lista.Add(hasta);
    }

    private static Vector2Int PrimeraClave(Dictionary<Vector2Int, List<Vector2Int>> d)
    {
        foreach (Vector2Int k in d.Keys)
        {
            return k;
        }
        return Vector2Int.zero;
    }

    /// <summary>
    /// Saca un lado que salga de este vértice y lo devuelve. Cada lado
    /// se usa una sola vez: por eso se borra al tomarlo, y por eso un
    /// vértice donde la forma se pellizca (dos brazos que se tocan solo
    /// en la diagonal) no manda el recorrido al mismo lado dos veces.
    /// </summary>
    private static bool Tomar(Dictionary<Vector2Int, List<Vector2Int>> d,
        Vector2Int desde, out Vector2Int hasta)
    {
        hasta = default;
        if (!d.TryGetValue(desde, out List<Vector2Int> lista) || lista.Count == 0)
        {
            return false;
        }
        hasta = lista[lista.Count - 1];
        lista.RemoveAt(lista.Count - 1);
        if (lista.Count == 0)
        {
            d.Remove(desde);
        }
        return true;
    }

    /// <summary>
    /// Pasa la vuelta a mundo tirando los puntos que están en medio de
    /// un tramo recto: solo quedan las esquinas de verdad.
    /// </summary>
    private List<Vector2> Enderezar(List<Vector2Int> vuelta)
    {
        var salida = new List<Vector2>(vuelta.Count);
        int n = vuelta.Count;
        for (int i = 0; i < n; i++)
        {
            Vector2Int antes = vuelta[(i - 1 + n) % n];
            Vector2Int actual = vuelta[i];
            Vector2Int despues = vuelta[(i + 1) % n];
            if (actual - antes != despues - actual)
            {
                salida.Add(VerticeMundo(actual));
            }
        }
        return salida;
    }

    /// <summary>
    /// Un vértice de la grilla en mundo. El (0, 0) del sector del centro
    /// es su esquina de abajo a la izquierda, por eso el medio sector de
    /// corrimiento.
    /// </summary>
    private Vector2 VerticeMundo(Vector2Int v)
    {
        return (Vector2)transform.position
            + new Vector2((v.x - 0.5f) * sectorSize.x, (v.y - 0.5f) * sectorSize.y);
    }

    /// <summary>Las 4 esquinas de un sector: arriba-izq, arriba-der, abajo-der, abajo-izq.</summary>
    public Vector2[] EsquinasSector(Vector2Int s)
    {
        Rect r = RectSector(s);
        return new[]
        {
            new Vector2(r.xMin, r.yMax),
            new Vector2(r.xMax, r.yMax),
            new Vector2(r.xMax, r.yMin),
            new Vector2(r.xMin, r.yMin),
        };
    }

    private void OnDrawGizmos()
    {
        if (!dibujarGizmos)
        {
            return;
        }

        Vector3 size = new Vector3(sectorSize.x, sectorSize.y, 0f);
        foreach (Vector2Int s in Molde())
        {
            Vector3 centro = SectorCenterWorld(s);

            if (!Activo(s))
            {
                Gizmos.color = colorApagado;
                Gizmos.DrawWireCube(centro, size);
                continue;
            }

            if (s == Recortado(sectorInicial))
            {
                Gizmos.color = colorSectorInicial;
                Gizmos.DrawCube(centro, size);
            }

            Gizmos.color = colorLinea;
            Gizmos.DrawWireCube(centro, size);
        }
    }
}
