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
    private bool seleccionando;
    private RenderTexture texturaOriginal3D;

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

        if (camaraBase != null)
        {
            camaraBase.gameObject.SetActive(true);
            camaraBase.enabled = !modoSeleccion;
        }

        if (camara3DWorld != null)
        {
            camara3DWorld.gameObject.SetActive(true);
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
