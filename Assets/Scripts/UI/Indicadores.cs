using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Testigos del tablero: se prenden mientras apretás la tecla.
/// Izquierda y derecha en azul al girar, el del medio en rojo al frenar.
///
/// El color va por el MATERIAL y no por Image.color: el color de una
/// Image se guarda como Color32, con tope 1, así que nunca puede pasar
/// de blanco y el Bloom no lo agarra — cambia de tono pero se ve
/// apagado. Para brillar hace falta HDR, y eso solo lo tiene el
/// material. Por eso cada indicador recibe su propia copia del material
/// neón, y el color se le cambia ahí.
///
/// Lee las mismas teclas que Nave.cs (A/D, flechas y Espacio).
///
/// Va en cualquier objeto del Canvas.
/// </summary>
public class Indicadores : MonoBehaviour
{
    [Header("Indicadores")]
    [SerializeField] private Graphic izquierda;
    [SerializeField] private Graphic derecha;
    [SerializeField] private Graphic freno;

    [Tooltip("El material con el shader HumanMess/Neon. Se copia uno " +
        "por indicador al arrancar; este no se toca.")]
    [SerializeField] private Material materialNeon;

    [Header("Colores")]
    [SerializeField] private Color giroPrendido = new Color(0.3f, 0.6f, 1f, 1f);
    [SerializeField] private Color frenoPrendido = new Color(1f, 0.2f, 0.2f, 1f);

    [Tooltip("Cómo se ven apagados. Con alfa bajo quedan como un " +
        "testigo apagado; con alfa 0 desaparecen.")]
    [SerializeField] private Color apagado = new Color(1f, 1f, 1f, 0f);

    [Tooltip("Cuánto brillan prendidos. Por encima de ~1 empieza a " +
        "agarrarlos el Bloom.")]
    [SerializeField] private float intensidad = 4f;

    private static readonly int IdColor = Shader.PropertyToID("_Color");
    private static readonly int IdIntensidad = Shader.PropertyToID("_Intensidad");

    private Material _matIzq;
    private Material _matDer;
    private Material _matFreno;

    private void Awake()
    {
        _matIzq = Preparar(izquierda);
        _matDer = Preparar(derecha);
        _matFreno = Preparar(freno);
    }

    /// <summary>
    /// Le da al indicador su propia copia del material. Si compartieran
    /// el mismo, prender uno los prendería a los tres.
    /// </summary>
    private Material Preparar(Graphic g)
    {
        if (g == null || materialNeon == null)
        {
            return null;
        }
        Material m = new Material(materialNeon);
        m.name = materialNeon.name + " (" + g.name + ")";
        g.material = m;
        // el color de la Image queda en blanco: si no, multiplicaría al
        // del material y lo oscurecería
        g.color = Color.white;
        return m;
    }

    private void Update()
    {
        Keyboard k = Keyboard.current;
        if (k == null)
        {
            return;
        }

        bool izq = k.aKey.isPressed || k.leftArrowKey.isPressed;
        bool der = k.dKey.isPressed || k.rightArrowKey.isPressed;
        bool frenar = k.spaceKey.isPressed;

        Pintar(_matIzq, izquierda, izq, giroPrendido);
        Pintar(_matDer, derecha, der, giroPrendido);
        Pintar(_matFreno, freno, frenar, frenoPrendido);
    }

    private void Pintar(Material m, Graphic g, bool prendido, Color color)
    {
        if (m != null)
        {
            m.SetColor(IdColor, prendido ? color : apagado);
            m.SetFloat(IdIntensidad, prendido ? intensidad : 1f);
            return;
        }

        // sin material neón asignado, al menos que cambie de color
        if (g != null)
        {
            g.color = prendido ? color : apagado;
        }
    }

    private void OnDestroy()
    {
        // las copias las creamos nosotros, así que nos toca borrarlas;
        // si no, quedan colgando en memoria
        Borrar(_matIzq);
        Borrar(_matDer);
        Borrar(_matFreno);
    }

    private static void Borrar(Material m)
    {
        if (m != null)
        {
            Destroy(m);
        }
    }
}
