using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Testigos del tablero. Izquierda y derecha en azul mientras girás;
/// el del medio en rojo mientras los amortiguadores están prendidos.
/// Es un estado y no una tecla (Space los prende y apaga), así que el
/// testigo es la única forma de saber cómo los dejaste.
///
/// El color va por el MATERIAL y no por Image.color: el color de una
/// Image se guarda como Color32, con tope 1, así que nunca puede pasar
/// de blanco y el Bloom no lo agarra — cambia de tono pero se ve
/// apagado. Para brillar hace falta HDR, y eso solo lo tiene el
/// material. Por eso cada indicador recibe su propia copia del material
/// neón, y el color se le cambia ahí.
///
/// El giro lee las mismas teclas que Nave.cs (A/D y flechas).
///
/// Va en cualquier objeto del Canvas.
/// </summary>
public class Indicadores : MonoBehaviour
{
    [Header("Indicadores")]
    [SerializeField] private Graphic izquierda;
    [SerializeField] private Graphic derecha;
    [SerializeField] private Graphic freno;

    [Tooltip("De dónde sale si los amortiguadores están prendidos. " +
        "Vacío = la busca en la escena.")]
    [SerializeField] private Nave nave;

    [Tooltip("Dibujo del testigo con los amortiguadores APAGADOS. " +
        "Vacío = no se cambia el sprite y solo cambia el color.")]
    [SerializeField] private Sprite frenoApagado;

    [Tooltip("Dibujo con los amortiguadores PRENDIDOS.")]
    [SerializeField] private Sprite frenoPrendidoSprite;

    [Tooltip("Cómo se ve el testigo del estabilizador apagado.\n\n" +
        "Tiene su propio color y no usa el \"Apagado\" de abajo porque " +
        "ese está en alfa 0: los de giro son teclas y desaparecen al " +
        "soltarlas, pero este es un ESTADO. Su dibujo apagado es la " +
        "base y tiene que verse siempre — si no, no sabrías que el " +
        "estabilizador existe hasta prenderlo.")]
    [SerializeField] private Color frenoApagadoColor = new Color(1f, 1f, 1f, 0.35f);

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
        if (nave == null)
        {
            nave = FindAnyObjectByType<Nave>();
        }
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
        bool frenar = nave != null && nave.Amortiguadores;

        Pintar(_matIzq, izquierda, izq, giroPrendido);
        Pintar(_matDer, derecha, der, giroPrendido);
        Pintar(_matFreno, freno, frenar, frenoPrendido, frenoApagadoColor);
        Dibujo(freno, frenar ? frenoPrendidoSprite : frenoApagado);
    }

    /// <summary>
    /// Le cambia el dibujo a un testigo. Con el sprite en null no toca
    /// nada: así el que no usa dos dibujos se queda con el suyo.
    /// </summary>
    private static void Dibujo(Graphic g, Sprite s)
    {
        if (s == null || !(g is Image img) || img.sprite == s)
        {
            return;
        }
        img.sprite = s;
    }

    /// <summary>
    /// Prende o apaga un testigo. "colorApagado" en null usa el de
    /// todos; el del estabilizador pasa el suyo porque tiene que
    /// quedar visible apagado.
    /// </summary>
    private void Pintar(Material m, Graphic g, bool prendido, Color color,
        Color? colorApagado = null)
    {
        Color off = colorApagado ?? apagado;
        if (m != null)
        {
            m.SetColor(IdColor, prendido ? color : off);
            m.SetFloat(IdIntensidad, prendido ? intensidad : 1f);
            return;
        }

        // sin material neón asignado, al menos que cambie de color
        if (g != null)
        {
            g.color = prendido ? color : off;
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
