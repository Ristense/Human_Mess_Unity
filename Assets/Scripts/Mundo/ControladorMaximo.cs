using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controla los objetos que se ocultan durante la selección de planeta.
/// Ponelo en el objeto vacío "Controlador_Maximo".
/// </summary>
public class ControladorMaximo : MonoBehaviour
{
    [Tooltip("Objetos que desaparecen durante la selección del planeta.")]
    [SerializeField] private GameObject[] objetosAocultar;

    [Header("Cámaras")]
    [SerializeField] private Camera camaraBase;
    [SerializeField] private Camera camara3DWorld;
    [SerializeField] private Transform planeta;

    [Tooltip("El Quad que muestra la Render Texture en el juego. Sirve " +
        "para que el planeta se vea del MISMO tamaño al pasar a la " +
        "selección. Vacío = lo busca colgando de Base_Camara.")]
    [SerializeField] private Transform quad;

    [Tooltip("Cuánto más lejos se ve el planeta en la selección. 1 = " +
        "igual que en el juego, 1.5 = se ve a dos tercios del tamaño.")]
    [Min(0.1f)]
    [SerializeField] private float alejar = 1.5f;

    [Tooltip("La elección de sector con A y D. Solo vive mientras dura " +
        "la selección. Vacío = lo busca en la escena.")]
    [SerializeField] private SelectorSector selector;

    private bool seleccionando;
    private RenderTexture texturaOriginal3D;

    // Le reescribe el FOV a la 3D en cada frame para acompañar el zoom.
    // Durante la selección hay que apagarlo: si no, pisa la compensación
    // y el planeta sigue cambiando de tamaño con la rueda.
    private SincronizarZoom3D sincro;

    // el FOV que tenía justo antes de entrar a la selección
    private float fovAntes;

    private void Start()
    {
        BuscarCamaras();
        if (planeta == null)
        {
            GameObject objeto = GameObject.Find("Planeta.001 (1)");
            if (objeto != null) planeta = objeto.transform;
        }
        if (camara3DWorld != null)
        {
            texturaOriginal3D = camara3DWorld.targetTexture;
            fovAntes = camara3DWorld.fieldOfView;
            sincro = camara3DWorld.GetComponent<SincronizarZoom3D>();
        }
        if (selector == null)
        {
            selector = FindAnyObjectByType<SelectorSector>(FindObjectsInactive.Include);
        }
        if (selector == null)
        {
            Debug.LogWarning("[ControladorMaximo] no hay ningún SelectorSector " +
                "en la escena: la T no va a poder elegir sector.", this);
        }
        if (quad == null && camaraBase != null)
        {
            Transform t = camaraBase.transform.Find("Quad");
            if (t != null) quad = t;
        }
        // El juego siempre parte normal: Base_Camara muestra el Quad,
        // alimentado por 3DWorld_Camara y su Render Texture.
        CambiarModo(false);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
        {
            CambiarModo(!seleccionando);
        }
    }

    public void OcultarObjetos()
    {
        CambiarObjetos(false);
    }

    public void MostrarObjetos()
    {
        CambiarObjetos(true);
    }

    public void EntrarSeleccionPlaneta()
    {
        CambiarModo(true);
    }

    public void SalirSeleccionPlaneta()
    {
        CambiarModo(false);
    }

    private void CambiarModo(bool modoSeleccion)
    {
        seleccionando = modoSeleccion;
        CambiarObjetos(!modoSeleccion);

        // Lo que mueve el planeta con A y D y le dibuja la grilla: vive
        // solo mientras estás eligiendo. Se prende el OBJETO y no el
        // componente, porque con el objeto apagado prender el
        // componente no hace nada — ni siquiera corre.
        if (selector != null)
        {
            selector.Usar(planeta);
            selector.gameObject.SetActive(modoSeleccion);
        }

        if (camaraBase != null)
        {
            camaraBase.gameObject.SetActive(true);
            camaraBase.enabled = !modoSeleccion;
        }

        if (camara3DWorld != null)
        {
            camara3DWorld.gameObject.SetActive(true);
            // En modo juego el planeta se ve a través del Quad, que lo
            // agranda o lo achica según cuánta pantalla ocupe. Al
            // renderizar directo ese aumento desaparece y el planeta
            // pega un salto de tamaño. Abriendo o cerrando el lente en
            // la misma proporción, se ve igual en los dos modos.
            if (modoSeleccion)
            {
                fovAntes = camara3DWorld.fieldOfView;
                // primero se apaga el que reescribe el FOV, si no lo pisa
                if (sincro != null) sincro.enabled = false;
                camara3DWorld.fieldOfView = FovCompensado(fovAntes);
            }
            else
            {
                // el FOV vuelve ANTES de prenderlo: al prenderse toma el
                // que haya como su punto de partida, y si tomara el
                // compensado quedaría corrido para siempre
                camara3DWorld.fieldOfView = fovAntes;
                if (sincro != null) sincro.enabled = true;
            }

            camara3DWorld.targetTexture = modoSeleccion
                ? null
                : texturaOriginal3D;

            if (modoSeleccion && planeta != null)
            {
                camara3DWorld.transform.LookAt(planeta.position);
            }

            // En juego alimenta el Quad; en selección renderiza directo.
            camara3DWorld.enabled = true;
        }
    }

    /// <summary>
    /// Qué ángulo tiene que abrir el lente para que, sin el Quad, el
    /// planeta se vea del mismo tamaño que a través de él.
    /// </summary>
    private float FovCompensado(float fovActual)
    {
        float f = AltoDelQuadEnPantalla();
        if (f <= 0.001f)
        {
            return fovActual;
        }
        // abrir más el lente es lo mismo que alejarse: entra más mundo y
        // el planeta queda más chico
        float mitad = Mathf.Tan(fovActual * 0.5f * Mathf.Deg2Rad) / f * alejar;
        return Mathf.Clamp(2f * Mathf.Atan(mitad) * Mathf.Rad2Deg, 1f, 179f);
    }

    /// <summary>
    /// Qué parte del alto de la pantalla ocupa el Quad. 1 = justo toda;
    /// más de 1 = se sale, o sea que agranda lo que muestra.
    /// </summary>
    private float AltoDelQuadEnPantalla()
    {
        if (quad == null || camaraBase == null || Screen.height <= 0)
        {
            return 1f;
        }
        Renderer r = quad.GetComponent<Renderer>();
        if (r == null)
        {
            return 1f;
        }
        Bounds b = r.bounds;
        Vector3 arriba = new Vector3(b.center.x, b.max.y, b.center.z);
        Vector3 abajo = new Vector3(b.center.x, b.min.y, b.center.z);
        float px = Mathf.Abs(camaraBase.WorldToScreenPoint(arriba).y
                           - camaraBase.WorldToScreenPoint(abajo).y);
        return px / Screen.height;
    }

    private void BuscarCamaras()
    {
        if (camaraBase == null)
        {
            GameObject objeto = GameObject.Find("Base_Camara");
            if (objeto != null) camaraBase = objeto.GetComponent<Camera>();
        }

        if (camara3DWorld == null)
        {
            GameObject objeto = GameObject.Find("3DWorld_Camara");
            if (objeto != null) camara3DWorld = objeto.GetComponent<Camera>();
        }
    }

    private void CambiarObjetos(bool mostrar)
    {
        if (objetosAocultar == null)
        {
            return;
        }

        foreach (GameObject objeto in objetosAocultar)
        {
            // El controlador siempre tiene que quedar vivo para poder
            // escuchar la T y volver desde la selección.
            if (objeto != null && !transform.IsChildOf(objeto.transform))
            {
                objeto.SetActive(mostrar);
            }
        }
    }
}
