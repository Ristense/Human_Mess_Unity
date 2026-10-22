using UnityEngine;

/// <summary>
/// Arma un satélite poniendo piezas al azar en los puntos que le
/// marcaste en el cuerpo. La idea es no tener que dibujar satélites
/// enteros: dibujás unos pocos cuerpos y unas pocas piezas, y de la
/// combinatoria salen todos.
///
/// Va en el objeto del cuerpo. En el editor le colgás hijos vacíos
/// donde quieras que pueda ir algo, los arrastrás al grupo que
/// corresponda, y listo.
///
/// Hay dos grupos porque no todas las piezas son iguales:
///
///   - PRINCIPALES: antenas, paneles solares, platos. Cosas grandes,
///     que se ven de lejos y definen la silueta. Van en los puntos
///     salientes del cuerpo.
///
///   - ADORNOS: tapas, escotillas, cajas, cables. Detalle chico que
///     rompe la monotonía pero no cambia de qué se trata el objeto.
///
/// Están separados para poder dosificarlos distinto: querés casi
/// siempre un par de paneles (probabilidad alta, pocos puntos) y
/// adornos salpicados (probabilidad media, muchos puntos). Con una
/// sola lista eso no se puede.
///
/// Cada punto se resuelve solo: o le toca una pieza al azar de su
/// lista, o queda vacío. Un punto vacío no es un bug — es lo que hace
/// que dos satélites del mismo cuerpo no se vean iguales.
/// </summary>
public class CuerpoSatelite : MonoBehaviour
{
    [System.Serializable]
    public class Grupo
    {
        [Tooltip("Los hijos vacíos del cuerpo donde puede ir una pieza " +
            "de este grupo. La pieza se instancia COMO HIJA del punto, " +
            "así que la posición y el ángulo los define el punto: si lo " +
            "girás en el editor, la pieza sale girada.")]
        public Transform[] puntos;

        [Tooltip("Los prefabs que pueden tocar acá. Se elige uno al " +
            "azar por cada punto que se llene.")]
        public GameObject[] piezas;

        [Tooltip("Qué tan probable es que un punto se llene. En 1 se " +
            "llenan todos y todos los satélites quedan igual de " +
            "cargados; bajarlo es lo que da variedad.")]
        [Range(0f, 1f)]
        public float probabilidad = 0.7f;

        [Tooltip("Cuántos puntos de este grupo se pueden llenar como " +
            "mucho. En 0 o menos, sin tope.")]
        public int tope = 0;

        [Tooltip("La pieza se orienta sola apuntando hacia AFUERA del " +
            "centro del cuerpo, sin importar cómo hayas dejado el " +
            "punto. Para que funcione, dibujá las piezas mirando hacia " +
            "ARRIBA (+Y): el script las gira desde ahí.\n\n" +
            "Con esto no hace falta rotar cada hijo vacío a mano — el " +
            "de abajo sale mirando abajo y el de la izquierda mirando " +
            "a la izquierda, solos. Apagalo si querés controlar el " +
            "ángulo vos, rotando cada punto en el editor.")]
        public bool mirarHaciaAfuera = true;

        [Tooltip("Redondea la orientación al múltiplo más cercano de " +
            "estos grados. Con 90 las piezas quedan siempre rectas " +
            "(arriba / abajo / izquierda / derecha), que es lo que " +
            "querés en un cuerpo con forma de caja.\n\n" +
            "Hace falta porque el ángulo sale del centro del cuerpo " +
            "hasta el punto: si el punto no está alineado con el " +
            "centro — dos adornos abajo, uno a cada lado del eje — la " +
            "dirección da en diagonal y la pieza sale inclinada hacia " +
            "afuera. Redondeando a 90 se endereza sola.\n\n" +
            "En 0 no redondea: el ángulo queda exacto, útil para " +
            "cuerpos redondos donde las piezas van radiales.")]
        public float snapGrados = 90f;

        [Tooltip("Giro extra al azar sobre la pieza, en grados (±). Se " +
            "suma encima de la orientación, así que no la rompe. Para " +
            "paneles dejalo en 0; para adornos un poco de desorden " +
            "queda bien.")]
        public float giroAlAzar;

        [Tooltip("Variación de tamaño, como factor. (1, 1) = todas " +
            "iguales.")]
        public Vector2 rangoEscala = Vector2.one;
    }

    [Header("Piezas grandes (antenas, paneles, platos)")]
    [SerializeField] private Grupo principales = new Grupo();

    [Header("Adornos (tapas, cajas, cables)")]
    [SerializeField] private Grupo adornos = new Grupo();

    [Header("Azar")]
    [Tooltip("Misma semilla = mismo satélite siempre. En 0 cambia cada " +
        "vez que arranca el juego. Sirve para que un satélite puesto a " +
        "mano en la escena se vea igual entre partidas.")]
    [SerializeField] private int semilla;

    [Tooltip("Los puntos son hijos vacíos que no se dibujan, pero si " +
        "les colgaste algo de referencia en el editor, esto los " +
        "esconde al armar.")]
    [SerializeField] private bool ocultarPuntosVacios;

    private void Start()
    {
        // System.Random y no Random.Range: así la semilla de este
        // satélite no depende del estado global de Unity, que lo tocan
        // también el campo de asteroides y todo lo demás. Dos satélites
        // con la misma semilla salen idénticos pase lo que pase.
        System.Random rng = semilla != 0
            ? new System.Random(semilla)
            : new System.Random(System.Guid.NewGuid().GetHashCode());

        Armar(principales, rng);
        Armar(adornos, rng);
    }

    private void Armar(Grupo g, System.Random rng)
    {
        if (g == null || g.puntos == null || g.piezas == null || g.piezas.Length == 0)
        {
            return;
        }

        // Primero se vacían TODOS los puntos.
        //
        // Si este cuerpo es un clon de un molde que ya se había armado,
        // viene con las piezas del molde colgando. Sin esta limpieza el
        // clon les sumaba las suyas encima y quedaban dos piezas en el
        // mismo punto. Pasaba de a ratos nomás porque Unity no
        // garantiza en qué orden corren los Start() de scripts
        // distintos: depende de si el molde se armó antes o después de
        // que CampoObjetos lo clonara.
        //
        // Va aparte del bucle de abajo, y no adentro, porque un punto
        // al que no le toca pieza también tiene que quedar vacío.
        foreach (Transform p in g.puntos)
        {
            if (p == null)
            {
                continue;
            }
            for (int c = p.childCount - 1; c >= 0; c--)
            {
                Transform viejo = p.GetChild(c);
                // Se desprende ANTES de destruirlo: Destroy no borra el
                // objeto en el acto, lo marca y lo saca recién al final
                // del frame. Hasta entonces seguiría colgando del punto
                // y contaría como pieza puesta.
                viejo.SetParent(null, false);
                Destroy(viejo.gameObject);
            }
        }

        // Se recorren los puntos en orden al azar para que el tope no
        // se gaste siempre en los primeros del array: con el orden
        // fijo, los últimos puntos no se llenaban nunca.
        int[] orden = Barajar(g.puntos.Length, rng);
        int puestas = 0;
        var yaUsados = new System.Collections.Generic.HashSet<Transform>();

        foreach (int i in orden)
        {
            Transform punto = g.puntos[i];
            if (punto == null)
            {
                continue;
            }
            // Si el mismo punto quedó arrastrado dos veces en el array
            // —fácil de hacer sin darse cuenta— se llenaría dos veces y
            // las piezas se montarían una encima de la otra.
            if (!yaUsados.Add(punto))
            {
                continue;
            }

            bool hayTope = g.tope > 0 && puestas >= g.tope;
            if (hayTope || rng.NextDouble() > g.probabilidad)
            {
                if (ocultarPuntosVacios)
                {
                    punto.gameObject.SetActive(false);
                }
                continue;
            }

            GameObject prefab = g.piezas[rng.Next(g.piezas.Length)];
            if (prefab == null)
            {
                continue;
            }

            GameObject pieza = Instantiate(prefab, punto);
            pieza.transform.localPosition = Vector3.zero;

            float giro = g.giroAlAzar > 0f
                ? (float)(rng.NextDouble() * 2.0 - 1.0) * g.giroAlAzar
                : 0f;

            Vector2 afuera = (Vector2)punto.position - (Vector2)transform.position;
            if (g.mirarHaciaAfuera && afuera.sqrMagnitude > 0.0000001f)
            {
                // El dibujo viene apuntando a +Y y Atan2 mide desde +X:
                // de ahí el -90. Se setea la rotación de MUNDO y no la
                // local porque el padre de la pieza es el punto, no el
                // cuerpo — y lo que queremos alinear es contra el
                // centro del cuerpo. Como igual queda colgando del
                // punto, después acompaña al satélite cuando gira.
                float ang = Mathf.Atan2(afuera.y, afuera.x) * Mathf.Rad2Deg;
                if (g.snapGrados > 0f)
                {
                    ang = Mathf.Round(ang / g.snapGrados) * g.snapGrados;
                }
                pieza.transform.rotation = Quaternion.Euler(0f, 0f, ang - 90f + giro);
            }
            else
            {
                pieza.transform.localRotation = Quaternion.Euler(0f, 0f, giro);
            }

            float escala = Mathf.Lerp(g.rangoEscala.x, g.rangoEscala.y, (float)rng.NextDouble());
            pieza.transform.localScale = Vector3.one * escala;

            puestas++;
        }
    }

    /// <summary>Los índices 0..n-1 en orden al azar (Fisher-Yates).</summary>
    private static int[] Barajar(int n, System.Random rng)
    {
        int[] v = new int[n];
        for (int i = 0; i < n; i++)
        {
            v[i] = i;
        }
        for (int i = n - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (v[i], v[j]) = (v[j], v[i]);
        }
        return v;
    }
}
