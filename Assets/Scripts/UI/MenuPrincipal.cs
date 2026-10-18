using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// El menú del principio: Jugar, Opciones y Salir.
///
/// "Jugar" no carga el juego de una: primero pasa por la intro, y recién
/// cuando el diálogo termina (o lo saltás) entra a la escena del juego.
///
/// Los botones se conectan desde el Inspector: en el componente Button de
/// cada uno, en "On Click ()", se arrastra el objeto que tiene este script
/// y se elige la función (Jugar, Opciones, Volver o Salir).
///
/// Va en el Canvas del menú.
/// </summary>
public class MenuPrincipal : MonoBehaviour
{
    [Header("Paneles")]
    [Tooltip("El panel con el título y los botones.")]
    [SerializeField] private GameObject panelMenu;

    [Tooltip("El panel de opciones. Opcional.")]
    [SerializeField] private GameObject panelOpciones;

    [Tooltip("El diálogo de la intro. Vacío = Jugar entra directo.")]
    [SerializeField] private Dialogo intro;

    [Header("Juego")]
    [Tooltip("El nombre exacto de la escena del juego. Tiene que estar " +
        "en la lista de escenas del build (File > Build Profiles), si " +
        "no Unity no la encuentra.")]
    [SerializeField] private string escenaJuego = "SampleScene";

    private void Start()
    {
        Mostrar(panelMenu, true);
        Mostrar(panelOpciones, false);
        if (intro != null)
        {
            intro.gameObject.SetActive(false);
        }
    }

    public void Jugar()
    {
        Mostrar(panelMenu, false);

        if (intro == null)
        {
            Entrar();
            return;
        }
        intro.gameObject.SetActive(true);
        intro.Empezar(Entrar);
    }

    public void Opciones()
    {
        Mostrar(panelMenu, false);
        Mostrar(panelOpciones, true);
    }

    public void Volver()
    {
        Mostrar(panelOpciones, false);
        Mostrar(panelMenu, true);
    }

    public void Salir()
    {
#if UNITY_EDITOR
        // en el editor Application.Quit no hace nada: se corta el Play
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void Entrar()
    {
        SceneManager.LoadScene(escenaJuego);
    }

    private static void Mostrar(GameObject panel, bool visible)
    {
        if (panel != null)
        {
            panel.SetActive(visible);
        }
    }
}
