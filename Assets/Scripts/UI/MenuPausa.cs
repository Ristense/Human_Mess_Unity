using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// El menú de Esc: para el juego y te deja seguir, guardar, abrir
/// opciones o irte al menú principal.
///
/// Al abrirse guarda solo, así nunca perdés el rato que llevabas
/// jugando por cerrar mal.
///
/// Mientras está abierto el tiempo queda en cero: la física no avanza,
/// nada se mueve y la nave no se te va a la deriva mientras decidís.
/// Ojo con eso en lo que corra mientras tanto — el menú y los diálogos
/// usan unscaledDeltaTime justamente por esto.
///
/// Los botones se conectan desde el Inspector, en "On Click ()".
///
/// Va en el Canvas del menú de pausa.
/// </summary>
public class MenuPausa : MonoBehaviour
{
    [Header("Paneles")]
    [Tooltip("El panel con los botones. Arranca apagado.")]
    [SerializeField] private GameObject panelMenu;

    [Tooltip("El panel de opciones. Opcional.")]
    [SerializeField] private GameObject panelOpciones;

    [Header("Guardado")]
    [Tooltip("Vacío = lo busca en la escena.")]
    [SerializeField] private Guardado guardado;

    [Tooltip("Guardar solo al abrir el menú.")]
    [SerializeField] private bool guardarAlAbrir = true;

    [Header("Escenas")]
    [Tooltip("El nombre exacto de la escena del menú principal, para el " +
        "botón de salir. Tiene que estar en File > Build Profiles.")]
    [SerializeField] private string escenaMenu = "Menu";

    /// <summary>¿Está abierto?</summary>
    public bool Abierto { get; private set; }

    private void Start()
    {
        Mostrar(panelMenu, false);
        Mostrar(panelOpciones, false);
        Abierto = false;
        // por si venís de una partida que quedó en pausa
        Time.timeScale = 1f;
    }

    private void Update()
    {
        if (Keyboard.current == null
            || !Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            return;
        }

        // Con las opciones abiertas, Esc vuelve al menú en vez de cerrar
        // todo: si no, tocar Esc sin querer adentro de opciones te saca
        // del menú entero.
        if (Abierto && panelOpciones != null && panelOpciones.activeSelf)
        {
            Volver();
            return;
        }

        if (Abierto)
        {
            Continuar();
        }
        else
        {
            Abrir();
        }
    }

    public void Abrir()
    {
        Abierto = true;
        Mostrar(panelMenu, true);
        Mostrar(panelOpciones, false);
        Time.timeScale = 0f;

        if (guardarAlAbrir)
        {
            Buscar()?.Guardar();
        }
    }

    /// <summary>Cierra el menú y sigue jugando.</summary>
    public void Continuar()
    {
        Abierto = false;
        Mostrar(panelMenu, false);
        Mostrar(panelOpciones, false);
        Time.timeScale = 1f;
    }

    /// <summary>Guarda ahora, sin cerrar el menú.</summary>
    public void Guardar()
    {
        Buscar()?.Guardar();
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

    /// <summary>Guarda y se va al menú principal.</summary>
    public void SalirAlMenu()
    {
        Buscar()?.Guardar();
        // el tiempo vuelve ANTES de cambiar de escena: si no, la escena
        // nueva arranca congelada y no hay quién la despierte
        Time.timeScale = 1f;
        SceneManager.LoadScene(escenaMenu);
    }

    private Guardado Buscar()
    {
        if (guardado == null)
        {
            guardado = FindAnyObjectByType<Guardado>();
        }
        return guardado;
    }

    private void Mostrar(GameObject panel, bool visible)
    {
        if (panel == null)
        {
            return;
        }

        // Si le pasaron este mismo objeto, apagarlo lo dejaría mudo: sin
        // Update no habría quién escuche el Esc para volver a prenderlo.
        // En ese caso se prenden y apagan los HIJOS, que es lo que se ve
        // igual, y este sigue vivo escuchando.
        if (panel == gameObject)
        {
            foreach (Transform h in transform)
            {
                h.gameObject.SetActive(visible);
            }
            return;
        }
        panel.SetActive(visible);
    }
}
