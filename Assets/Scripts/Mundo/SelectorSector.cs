using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// La selección de sector sobre el planeta: el planeta tiene entre 2 y 5
/// sectores repartidos alrededor, y con A y D vas pasando de uno al
/// otro. El elegido queda siempre de frente a la cámara.
///
/// Gira el PLANETA y no la cámara: visualmente es lo mismo (el sector
/// termina mirándote de frente) pero la cámara 3D tiene encima el
/// seguimiento y el zoom, y pelearse con eso sale más caro que mover
/// una bola.
///
/// Mientras está prendido, el giro de siempre del planeta se apaga: dos
/// cosas girando lo mismo a la vez no dejan que ninguna llegue.
///
/// Lo prende y lo apaga ControladorMaximo al entrar y salir con la T.
/// Va en un objeto vacío de la escena.
/// </summary>
public class SelectorSector : MonoBehaviour
{
    [Tooltip("El planeta que gira. Vacío = lo busca por nombre.")]
    [SerializeField] private Transform planeta;

    [Header("Sectores")]
    [Min(1)]
    [SerializeField] private int minSectores = 2;

    [Min(1)]
    [SerializeField] private int maxSectores = 5;

    [Tooltip("0 = un planeta distinto cada partida. Otro número arma " +
        "siempre los mismos sectores, que sirve para probar.")]
    [SerializeField] private int semilla;

    [Tooltip("Cuánto puede subir o bajar un sector respecto del ecuador, " +
        "en grados. 0 = todos en la misma franja y el giro se ve plano.")]
    [Range(0f, 60f)]
    [SerializeField] private float desnivelMaximo = 25f;

    [Header("Giro")]
    [Tooltip("Qué tan rápido llega al sector elegido, en segundos. " +
        "0 = salta seco.")]
    [SerializeField] private float suavizado = 0.25f;

    [Tooltip("Un retoque, en grados, por si el sector queda corrido " +
        "hacia un lado. Normalmente va en 0: adónde tiene que mirar se " +
        "calcula solo, a partir de dónde está la cámara.")]
    [SerializeField] private float offsetGrados;

    [Tooltip("La cámara que mira el planeta. Vacío = la busca.")]
    [SerializeField] private Camera camara;

    /// <summary>Cuántos sectores tiene este planeta.</summary>
    public int Sectores { get; private set; }

    /// <summary>Cuál está elegido ahora, de 0 a Sectores - 1.</summary>
    public int Actual { get; private set; }

    /// <summary>Avisa cada vez que cambia el sector elegido.</summary>
    public event Action<int> Cambio;

    /// <summary>Cuántos grados ocupa cada sector.</summary>
    public float GradosPorSector => Sectores > 0 ? 360f / Sectores : 360f;

    private GirarPlaneta _giroPropio;

    // cada sector está a su propia altura: así pasar de uno a otro no es
    // solo girar de lado, también sube o baja
    private float[] _latitudes = new float[0];

    /// <summary>A qué altura está un sector, en grados desde el ecuador.</summary>
    public float LatitudDe(int sector)
    {
        if (_latitudes == null || sector < 0 || sector >= _latitudes.Length)
        {
            return 0f;
        }
        return _latitudes[sector];
    }

    /// <summary>El planeta que está girando.</summary>
    public Transform Planeta => planeta;

    /// <summary>
    /// Le dice cuál es el planeta. Lo llama ControladorMaximo antes de
    /// prenderlo: buscarlo por nombre acá no sirve, porque para cuando
    /// esto despierta el planeta puede estar escondido y GameObject.Find
    /// no ve lo que está apagado.
    /// </summary>
    public void Usar(Transform cual)
    {
        if (cual == null)
        {
            return;
        }
        planeta = cual;
        _giroPropio = cual.GetComponent<GirarPlaneta>();
    }

    private void Awake()
    {
        if (planeta == null)
        {
            GirarPlaneta g = FindAnyObjectByType<GirarPlaneta>(FindObjectsInactive.Include);
            if (g != null) planeta = g.transform;
        }
        if (planeta != null && _giroPropio == null)
        {
            _giroPropio = planeta.GetComponent<GirarPlaneta>();
        }
        Repartir();
    }

    /// <summary>
    /// Cuántos sectores tiene el planeta, y dónde. No se rehace al
    /// entrar y salir: el planeta es el mismo toda la partida.
    /// </summary>
    public void Repartir()
    {
        int max = Mathf.Max(minSectores, maxSectores);

        // UnityEngine.Random y no Random a secas: con "using System"
        // arriba, el nombre choca con System.Random y no compila.
        UnityEngine.Random.State estado = UnityEngine.Random.state;
        if (semilla != 0)
        {
            UnityEngine.Random.InitState(semilla);
        }
        Sectores = UnityEngine.Random.Range(minSectores, max + 1);
        _latitudes = new float[Sectores];
        for (int i = 0; i < Sectores; i++)
        {
            _latitudes[i] = UnityEngine.Random.Range(-desnivelMaximo, desnivelMaximo);
        }
        if (semilla != 0)
        {
            UnityEngine.Random.state = estado;
        }

        Actual = 0;
    }

    private void OnEnable()
    {
        if (planeta == null)
        {
            Debug.LogWarning("[SelectorSector] no encuentro el planeta: " +
                "arrastralo al campo Planeta.", this);
            return;
        }

        if (_giroPropio != null)
        {
            _giroPropio.enabled = false;
        }
        Apuntar(true);
    }

    private void OnDisable()
    {
        // El planeta se queda mirando el sector que elegiste: al salir
        // seguís viendo adónde vas. Tampoco se le devuelve el giro lento
        // de siempre — con el planeta girando, el sector elegido se iría
        // de vista en un rato y la elección no significaría nada.
    }

    private void Update()
    {
        if (planeta == null || Keyboard.current == null)
        {
            return;
        }

        int paso = 0;
        if (Keyboard.current.aKey.wasPressedThisFrame
            || Keyboard.current.leftArrowKey.wasPressedThisFrame)
        {
            paso = -1;
        }
        else if (Keyboard.current.dKey.wasPressedThisFrame
            || Keyboard.current.rightArrowKey.wasPressedThisFrame)
        {
            paso = 1;
        }

        if (paso != 0)
        {
            Mover(paso);
        }
        Apuntar(false);
    }

    /// <summary>Pasa al sector de al lado. Da la vuelta al llegar al final.</summary>
    public void Mover(int paso)
    {
        if (Sectores <= 0)
        {
            return;
        }
        Actual = ((Actual + paso) % Sectores + Sectores) % Sectores;
        Cambio?.Invoke(Actual);
    }

    /// <summary>
    /// Gira el planeta hasta que el sector elegido apunte a la CÁMARA.
    ///
    /// No se gira una cantidad fija de grados: se calcula qué rotación
    /// lleva el punto del sector hasta la dirección en que está la
    /// cámara. Girando a ciegas, el sector caía en cualquier parte —
    /// dependía de hacia dónde estuviera parado el planeta y de dónde
    /// mirara la cámara.
    /// </summary>
    private void Apuntar(bool seco)
    {
        Quaternion objetivo = RotacionPara(Actual);
        if (seco || suavizado <= 0f)
        {
            planeta.localRotation = objetivo;
            return;
        }
        // así el acercamiento es el mismo con cualquier cantidad de FPS
        float t = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.0001f, suavizado));
        planeta.localRotation = Quaternion.Slerp(planeta.localRotation, objetivo, t);
    }

    private Quaternion RotacionPara(int sector)
    {
        // adónde mira el sector, visto desde adentro del planeta
        Vector3 propio = Direccion(LatitudDe(sector),
            sector * GradosPorSector + offsetGrados);

        // y adónde está la cámara, en el espacio del padre del planeta,
        // que es donde vive su localRotation
        Vector3 hacia = Vector3.forward;
        Camera c = Camara();
        if (c != null)
        {
            Vector3 mundo = (c.transform.position - planeta.position).normalized;
            hacia = planeta.parent != null
                ? planeta.parent.InverseTransformDirection(mundo)
                : mundo;
        }

        // Lleva "propio" hasta "hacia" sin dejar que el planeta ruede de
        // costado: se arma como la diferencia entre dos miradas, y las
        // dos tienen el mismo arriba.
        return Quaternion.LookRotation(hacia, Vector3.up)
            * Quaternion.Inverse(Quaternion.LookRotation(propio, Vector3.up));
    }

    /// <summary>
    /// Un punto de la esfera, igual que los que dibuja GrillaPlaneta:
    /// longitud 0 hacia +Z, la latitud hacia +Y.
    /// </summary>
    private static Vector3 Direccion(float latitud, float longitud)
    {
        float la = latitud * Mathf.Deg2Rad;
        float lo = longitud * Mathf.Deg2Rad;
        float cos = Mathf.Cos(la);
        return new Vector3(cos * Mathf.Sin(lo), Mathf.Sin(la), cos * Mathf.Cos(lo));
    }

    private Camera Camara()
    {
        if (camara == null)
        {
            GameObject o = GameObject.Find("3DWorld_Camara");
            if (o != null) camara = o.GetComponent<Camera>();
        }
        return camara;
    }
}
