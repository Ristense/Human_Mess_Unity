using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Guarda y carga la partida. Lo llama el menú de Esc: Guardar() al
/// abrirlo, Cargar() cuando elegís seguir.
///
/// Qué se guarda: la semilla del mapa, la nave, la bodega, los tanques
/// y TODOS los asteroides con la forma exacta que tienen ahora — con
/// sus pozos. Por eso las rocas se guardan contorno por contorno y no
/// se regeneran del mapa: un asteroide picado tiene que volver picado.
///
/// Carga sin recargar la escena: borra las rocas que hay y pone las
/// guardadas en su lugar. Así no hay que preocuparse por qué sobrevive
/// a un cambio de escena.
///
/// Va en un objeto vacío de la escena.
/// </summary>
public class Guardado : MonoBehaviour
{
    [Tooltip("Nombre del archivo. Queda en la carpeta de datos del " +
        "juego (Application.persistentDataPath).")]
    [SerializeField] private string archivo = "partida.json";

    [Header("Qué se guarda")]
    [SerializeField] private Rigidbody2D nave;
    [SerializeField] private Bodega bodega;
    [SerializeField] private SoporteVital soporte;
    [SerializeField] private Mapa mapa;
    [SerializeField] private SelectorSector selector;

    [Tooltip("Los prefabs de asteroides, para poder volver a crearlos " +
        "al cargar. Se reconocen por el nombre del prefab.")]
    [SerializeField] private GameObject[] prefabsRocas;

    [Tooltip("Escribe en la Console qué guardó y qué cargó.")]
    [SerializeField] private bool diagnostico = true;

    private string Ruta => Path.Combine(Application.persistentDataPath, archivo);

    /// <summary>¿Hay una partida guardada?</summary>
    public bool HayPartida => File.Exists(Ruta);

    private void Awake()
    {
        if (nave == null)
        {
            Nave n = FindAnyObjectByType<Nave>();
            if (n != null) nave = n.GetComponent<Rigidbody2D>();
        }
        if (bodega == null) bodega = FindAnyObjectByType<Bodega>();
        if (soporte == null) soporte = FindAnyObjectByType<SoporteVital>();
        if (mapa == null) mapa = FindAnyObjectByType<Mapa>();
        if (selector == null)
        {
            selector = FindAnyObjectByType<SelectorSector>(FindObjectsInactive.Include);
        }
    }

    // ------------------------------------------------------------------
    //  Guardar
    // ------------------------------------------------------------------

    public void Guardar()
    {
        var p = new Partida();

        if (mapa != null)
        {
            p.semillaMapa = mapa.Semilla;
        }
        if (selector != null)
        {
            p.sectorPlaneta = selector.Actual;
        }
        if (nave != null)
        {
            p.nave.posicion = nave.transform.position;
            p.nave.giro = nave.rotation;
            p.nave.velocidad = nave.linearVelocity;
            p.nave.velocidadAngular = nave.angularVelocity;
        }
        if (bodega != null)
        {
            p.bodega.carga = bodega.Carga;
            p.bodega.creditos = bodega.Creditos;
            p.bodega.placas = bodega.Placas;
            p.bodega.kgPlacas = bodega.KgPlacas;
            p.bodega.kgHielo = bodega.KgHielo;
            p.bodega.kgRestos = bodega.KgRestos;
        }
        if (soporte != null)
        {
            p.soporte.kgHidrogeno = soporte.KgHidrogeno;
            p.soporte.kgOxigeno = soporte.KgOxigeno;
        }

        foreach (Tallable t in FindObjectsByType<Tallable>(FindObjectsSortMode.None))
        {
            p.rocas.Add(Leer(t));
        }

        File.WriteAllText(Ruta, JsonUtility.ToJson(p, true));
        if (diagnostico)
        {
            Debug.Log($"[Guardado] {p.rocas.Count} rocas en {Ruta}", this);
        }
    }

    private DatosRoca Leer(Tallable t)
    {
        var d = new DatosRoca
        {
            id = Id(t.gameObject),
            posicion = t.transform.position,
            giro = t.transform.eulerAngles.z,
            escala = t.transform.localScale,
            densidad = t.DensidadActual,
            areaOriginal = t.AreaOriginal,
            material = (int)t.Tipo,
            tallada = t.EstaTallado,
        };
        if (t.Cuerpo != null)
        {
            d.velocidad = t.Cuerpo.linearVelocity;
            d.velocidadAngular = t.Cuerpo.angularVelocity;
        }
        d.PonerForma(t.FormaLocal());
        return d;
    }

    // ------------------------------------------------------------------
    //  Cargar
    // ------------------------------------------------------------------

    /// <summary>
    /// Para engancharla al botón. Los botones de Unity solo listan
    /// métodos que no devuelven nada, y Cargar() devuelve si pudo.
    /// </summary>
    public void CargarPartida()
    {
        Cargar();
    }

    public bool Cargar()
    {
        if (!HayPartida)
        {
            return false;
        }

        Partida p = JsonUtility.FromJson<Partida>(File.ReadAllText(Ruta));
        if (p == null)
        {
            Debug.LogWarning("[Guardado] el archivo no se entiende.", this);
            return false;
        }

        // El mapa primero: lo demás se ubica adentro de él.
        if (mapa != null)
        {
            mapa.GenerarCon(p.semillaMapa);
        }

        if (nave != null)
        {
            nave.position = p.nave.posicion;
            nave.rotation = p.nave.giro;
            nave.linearVelocity = p.nave.velocidad;
            nave.angularVelocity = p.nave.velocidadAngular;
            // la Z no la toca Rigidbody2D, y asignarle un Vector2 a
            // transform.position la borraría
            Vector3 v = nave.transform.position;
            nave.transform.position = new Vector3(v.x, v.y, p.nave.posicion.z);
        }

        bodega?.Restaurar(p.bodega.carga, p.bodega.creditos, p.bodega.placas,
            p.bodega.kgPlacas, p.bodega.kgHielo, p.bodega.kgRestos);
        soporte?.Restaurar(p.soporte.kgHidrogeno, p.soporte.kgOxigeno);

        PonerRocas(p.rocas);

        if (diagnostico)
        {
            Debug.Log($"[Guardado] cargada: {p.rocas.Count} rocas", this);
        }
        return true;
    }

    /// <summary>
    /// Saca las rocas que haya y pone las guardadas. Se borran todas en
    /// vez de intentar emparejarlas: después de un rato de taladro no
    /// queda ninguna como nació, y adivinar cuál es cuál saldría peor
    /// que rehacerlas.
    /// </summary>
    private void PonerRocas(List<DatosRoca> rocas)
    {
        foreach (Tallable t in FindObjectsByType<Tallable>(FindObjectsSortMode.None))
        {
            Destroy(t.gameObject);
        }

        foreach (DatosRoca d in rocas)
        {
            GameObject prefab = Prefab(d.id);
            if (prefab == null)
            {
                Debug.LogWarning($"[Guardado] no tengo el prefab '{d.id}': " +
                    "agregalo a Prefabs Rocas.", this);
                continue;
            }

            GameObject go = Instantiate(prefab, d.posicion,
                Quaternion.Euler(0f, 0f, d.giro));
            go.transform.localScale = d.escala;

            // Apagado antes de que corra su Start: si no, le vuelve a
            // calcular la masa por el tamaño y pisa la que traía.
            if (go.TryGetComponent(out MasaPorTamano mpt))
            {
                mpt.enabled = false;
            }

            Tallable t = go.GetComponent<Tallable>();
            if (t == null)
            {
                continue;
            }
            if (d.tallada)
            {
                t.Restaurar(d.densidad, d.areaOriginal, d.Forma());
            }

            if (t.Cuerpo != null)
            {
                t.Cuerpo.linearVelocity = d.velocidad;
                t.Cuerpo.angularVelocity = d.velocidadAngular;
            }
        }
    }

    // ------------------------------------------------------------------
    //  Prefabs
    // ------------------------------------------------------------------

    /// <summary>
    /// El nombre del prefab del que salió, sin los agregados que le pone
    /// Unity al instanciarlo ni los que les pone el tallado a los
    /// pedazos que se desprenden.
    /// </summary>
    private static string Id(GameObject go)
    {
        string n = go.name;
        n = n.Replace("(Clone)", "");
        n = n.Replace("(pedazo)", "");
        return n.Trim();
    }

    private GameObject Prefab(string id)
    {
        if (prefabsRocas == null)
        {
            return null;
        }
        foreach (GameObject g in prefabsRocas)
        {
            if (g != null && g.name == id)
            {
                return g;
            }
        }
        return null;
    }

    /// <summary>Borra la partida guardada.</summary>
    public void Borrar()
    {
        if (HayPartida)
        {
            File.Delete(Ruta);
        }
    }
}
