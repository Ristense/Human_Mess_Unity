using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dibuja la rueda de opciones de la Mano.
///
/// Es solo la vista: no decide nada ni escucha teclas. La Mano ya sabe
/// si la rueda está abierta, qué opciones hay y cuál estás apuntando;
/// acá se lee eso y se dibuja. Mismo reparto que IndicadorRadial y
/// HudMano, y por la misma razón: si la UI también opinara sobre la
/// lógica, habría dos lugares donde mirar cuando algo se elige mal.
///
/// Las pastillas se crean solas al arrancar, no hay que armar prefabs
/// ni arrastrar nada: se hacen <see cref="maxOpciones"/> y se van
/// prendiendo las que haga falta. Así agregar una opción nueva en la
/// Mano no obliga a volver a tocar la escena.
///
/// Va en Screen Space - Overlay por lo mismo que el HudMano: el juego
/// trabaja a una escala chiquita y un Canvas en World Space ahí se ve
/// borroso. Lo único que viaja del mundo a la pantalla es la posición
/// del centro.
///
/// Va en un objeto vacío adentro del Canvas.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class RuedaMano : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("De dónde salen las opciones y la selección.")]
    [SerializeField] private Mano mano;

    [Header("Pastillas")]
    [Tooltip("Tus botones, uno por ranura de la rueda. El ORDEN de " +
        "esta lista es el orden de la rueda: la 0 arriba y de ahí en " +
        "el sentido del reloj.\n\n" +
        "De cada uno se saca solo lo que necesita: el Image del propio " +
        "objeto (o del primer hijo que tenga uno) para el resaltado, y " +
        "los TextMeshPro de adentro — el primero es el nombre y el " +
        "segundo, si hay, el detalle.\n\n" +
        "Si la dejás vacía se crean pastillas lisas solas, para poder " +
        "probar sin armar arte.")]
    [SerializeField] private List<RectTransform> pastillas = new List<RectTransform>();

    [Header("Forma")]
    [Tooltip("Distancia en píxeles del centro de la rueda al centro de " +
        "cada pastilla.")]
    [SerializeField] private float radio = 110f;

    [Tooltip("Tamaño de cada pastilla, en píxeles.")]
    [SerializeField] private Vector2 tamanoPastilla = new Vector2(120f, 46f);

    [Tooltip("Cuántas pastillas se crean al arrancar. Es el techo de " +
        "opciones que puede mostrar la rueda; de sobra con 8.")]
    [Range(2, 12)]
    [SerializeField] private int maxOpciones = 8;

    [Tooltip("Sprite de fondo de las pastillas. Vacío = rectángulo " +
        "liso, que se ve bien igual. Si le ponés uno con bordes " +
        "redondeados, acordate de dejarlo en modo Sliced.")]
    [SerializeField] private Sprite spritePastilla;

    [Header("Colores")]
    [SerializeField] private Color colorFondo = new Color(0.06f, 0.07f, 0.09f, 0.85f);
    [SerializeField] private Color colorFondoElegido = new Color(0.3f, 1f, 0.6f, 0.95f);
    [SerializeField] private Color colorTexto = new Color(0.8f, 0.85f, 0.9f, 1f);
    [SerializeField] private Color colorTextoElegido = new Color(0.04f, 0.06f, 0.05f, 1f);

    [Header("Texto")]
    [Tooltip("Vacío = la fuente por defecto de TextMesh Pro.")]
    [SerializeField] private TMP_FontAsset fuente;

    [SerializeField] private float tamanoNombre = 20f;
    [SerializeField] private float tamanoDetalle = 13f;

    [Header("Animación")]
    [Tooltip("Segundos del fundido al abrir y cerrar. 0 = instantáneo.")]
    [SerializeField] private float fundido = 0.08f;

    [Tooltip("De qué tamaño nace la rueda al abrirse, como fracción " +
        "del final. 1 = sin crecimiento.")]
    [SerializeField] private float escalaInicial = 0.85f;

    [Tooltip("Cuánto más grande se dibuja la pastilla elegida.")]
    [SerializeField] private float agrandarElegida = 1.12f;

    /// <summary>
    /// Una pastilla ya armada. Se guardan las referencias en vez de
    /// buscarlas con GetComponent cada frame.
    /// </summary>
    private struct Pastilla
    {
        public RectTransform rt;
        public Image fondo;
        public TextMeshProUGUI nombre;
        public TextMeshProUGUI detalle;

        // El color con el que vino el botón. La opción NO elegida se
        // dibuja con esto y no con colorFondo: si pisáramos siempre el
        // color del Inspector, armar los botones con arte propio no
        // serviría de nada porque la rueda lo borraría al abrirse.
        public Color fondoBase;
        public Color textoBase;
    }

    private readonly List<Pastilla> _pastillas = new List<Pastilla>();
    private RectTransform _rt;
    private RectTransform _padre;
    private Canvas _canvas;
    private CanvasGroup _grupo;
    private float _visible;

    private void Awake()
    {
        _rt = GetComponent<RectTransform>();
        _padre = _rt.parent as RectTransform;
        _canvas = GetComponentInParent<Canvas>();

        // La rueda se planta por anchoredPosition respecto del centro
        // del padre, así que los anclajes tienen que estar en el medio.
        // Se fuerzan acá en vez de pedirlo en el Inspector: es un
        // detalle interno y si queda mal la rueda aparece corrida sin
        // que se entienda por qué.
        _rt.anchorMin = new Vector2(0.5f, 0.5f);
        _rt.anchorMax = new Vector2(0.5f, 0.5f);
        _rt.pivot = new Vector2(0.5f, 0.5f);

        _grupo = GetComponent<CanvasGroup>();
        if (_grupo == null)
        {
            _grupo = gameObject.AddComponent<CanvasGroup>();
        }
        // la rueda no se clickea: es solo un cartel. Si dejara que le
        // lleguen eventos, se comería los clics del juego justo cuando
        // está abierta, que es cuando más molesta.
        _grupo.blocksRaycasts = false;
        _grupo.interactable = false;
        _grupo.alpha = 0f;

        // Si pusiste tus botones, se usan esos. Si no, se crean lisos:
        // así el script sirve igual para probar antes de que exista el
        // arte, sin dejar la rueda invisible y sin pistas.
        if (pastillas != null && pastillas.Count > 0)
        {
            for (int i = 0; i < pastillas.Count; i++)
            {
                if (pastillas[i] == null)
                {
                    Debug.LogWarning("[RuedaMano] La ranura " + i + " está " +
                        "vacía en la lista de Pastillas. Se ignora, pero " +
                        "corre el orden de las de abajo.", this);
                    continue;
                }
                _pastillas.Add(Adoptar(pastillas[i]));
            }

            // Si pusiste menos botones que opciones puede haber, se
            // clona el último hasta completar. Es preferible a dejar
            // una opción sin dibujar: el sector igual se puede elegir
            // —la Mano reparte el círculo entre TODAS las opciones— y
            // quedaría una que se ejecuta sin que la veas nunca.
            int tope = Mathf.Max(maxOpciones, _pastillas.Count);
            while (_pastillas.Count > 0 && _pastillas.Count < tope)
            {
                RectTransform molde = _pastillas[_pastillas.Count - 1].rt;
                RectTransform copia = Instantiate(molde, _rt);
                copia.name = molde.name + " (extra " + _pastillas.Count + ")";
                _pastillas.Add(Adoptar(copia));
            }
        }
        else
        {
            for (int i = 0; i < maxOpciones; i++)
            {
                _pastillas.Add(CrearPastilla(i));
            }
        }
    }

    /// <summary>
    /// Toma un botón que armaste vos y le ubica las partes que la
    /// rueda necesita tocar.
    ///
    /// Se buscan por tipo y no por nombre a propósito: atarse a que el
    /// hijo se llame "Texto" es la clase de cosa que se rompe en
    /// silencio el día que lo renombrás.
    /// </summary>
    private Pastilla Adoptar(RectTransform rt)
    {
        Image fondo = rt.GetComponent<Image>();
        if (fondo == null)
        {
            fondo = rt.GetComponentInChildren<Image>(true);
        }

        TextMeshProUGUI[] textos = rt.GetComponentsInChildren<TextMeshProUGUI>(true);

        rt.gameObject.SetActive(false);

        TextMeshProUGUI nom = textos.Length > 0 ? textos[0] : null;

        return new Pastilla
        {
            rt = rt,
            fondo = fondo,
            nombre = nom,
            detalle = textos.Length > 1 ? textos[1] : null,
            fondoBase = fondo != null ? fondo.color : colorFondo,
            textoBase = nom != null ? nom.color : colorTexto
        };
    }

    private Pastilla CrearPastilla(int i)
    {
        var go = new GameObject("Opcion" + i, typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(_rt, false);
        rt.sizeDelta = tamanoPastilla;

        var fondo = go.GetComponent<Image>();
        fondo.sprite = spritePastilla;
        fondo.type = spritePastilla != null ? Image.Type.Sliced : Image.Type.Simple;
        fondo.color = colorFondo;
        fondo.raycastTarget = false;

        TextMeshProUGUI nombre = CrearTexto(rt, "Nombre", tamanoNombre,
            new Vector2(0f, 0.38f), new Vector2(1f, 1f));
        TextMeshProUGUI detalle = CrearTexto(rt, "Detalle", tamanoDetalle,
            new Vector2(0f, 0f), new Vector2(1f, 0.42f));

        go.SetActive(false);

        return new Pastilla
        {
            rt = rt,
            fondo = fondo,
            nombre = nombre,
            detalle = detalle,
            fondoBase = colorFondo,
            textoBase = colorTexto
        };
    }

    private TextMeshProUGUI CrearTexto(RectTransform padre, string nombre,
        float tamano, Vector2 anclaMin, Vector2 anclaMax)
    {
        var go = new GameObject(nombre, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rt = (RectTransform)go.transform;
        rt.SetParent(padre, false);
        rt.anchorMin = anclaMin;
        rt.anchorMax = anclaMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var t = go.GetComponent<TextMeshProUGUI>();
        if (fuente != null)
        {
            t.font = fuente;
        }
        t.fontSize = tamano;
        t.alignment = TextAlignmentOptions.Center;
        // No se toca el modo de wrapping a propósito: la propiedad
        // cambió de nombre entre versiones de TMP y quedaría atada a
        // una. Los textos son de una palabra y la pastilla es ancha.
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        return t;
    }

    private void LateUpdate()
    {
        if (mano == null)
        {
            return;
        }

        bool abierta = mano.RuedaAbierta;

        // El fundido se hace a mano y no con un Animator porque es un
        // solo número: la rueda aparece y desaparece, no tiene estados.
        float objetivo = abierta ? 1f : 0f;
        _visible = fundido > 0f
            ? Mathf.MoveTowards(_visible, objetivo, Time.unscaledDeltaTime / fundido)
            : objetivo;

        _grupo.alpha = _visible;

        if (_visible <= 0.001f)
        {
            // apagada del todo: no hay nada que acomodar
            if (_pastillas.Count > 0 && _pastillas[0].rt.gameObject.activeSelf)
            {
                Esconder();
            }
            return;
        }

        if (abierta)
        {
            Acomodar();
        }

        // Time.unscaledDeltaTime y no deltaTime: si algún día el juego
        // se pausa con timeScale = 0 (un menú, el minijuego), la rueda
        // tiene que seguir respondiendo igual.
        float escala = Mathf.Lerp(escalaInicial, 1f, _visible);
        _rt.localScale = new Vector3(escala, escala, 1f);
        Plantar(mano.CentroRueda);
    }

    /// <summary>
    /// Pone la rueda sobre un punto de la PANTALLA.
    ///
    /// No alcanza con asignar position: eso solo coincide con píxeles
    /// cuando el Canvas está en Screen Space - Overlay. En Screen Space
    /// - Camera el canvas vive en el mundo delante de la cámara, y
    /// mandarle un píxel como posición de mundo tira la rueda a
    /// kilómetros de la escena — se abre igual, pero no se ve.
    ///
    /// ScreenPointToLocalPointInRectangle hace la cuenta bien en los
    /// tres modos. La cámara va en null en Overlay porque ahí no hay
    /// proyección de por medio; en los otros, la del Canvas.
    /// </summary>
    private void Plantar(Vector2 pantalla)
    {
        if (_padre == null)
        {
            _rt.position = pantalla;
            return;
        }

        Camera cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? _canvas.worldCamera
            : null;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _padre, pantalla, cam, out Vector2 local))
        {
            _rt.anchoredPosition = local;
        }
    }

    private void Acomodar()
    {
        IReadOnlyList<AccionMano> ops = mano.Opciones;
        int n = Mathf.Min(ops.Count, _pastillas.Count);
        int elegida = mano.Seleccion;

        for (int i = 0; i < _pastillas.Count; i++)
        {
            Pastilla p = _pastillas[i];
            bool usada = i < n;

            if (p.rt.gameObject.activeSelf != usada)
            {
                p.rt.gameObject.SetActive(usada);
            }
            if (!usada)
            {
                continue;
            }

            float ang = Rueda.AnguloGrados(i, n) * Mathf.Deg2Rad;
            p.rt.anchoredPosition = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * radio;

            bool esta = i == elegida;

            float s = esta ? agrandarElegida : 1f;
            p.rt.localScale = new Vector3(s, s, 1f);

            // Todo lo de acá abajo es opcional: si armaste un botón sin
            // texto de detalle, o sin Image, simplemente no se toca.
            if (p.fondo != null)
            {
                p.fondo.color = esta ? colorFondoElegido : p.fondoBase;
            }

            if (p.nombre != null)
            {
                p.nombre.color = esta ? colorTextoElegido : p.textoBase;
                p.nombre.text = ops[i].nombre;
            }

            if (p.detalle != null)
            {
                p.detalle.color = esta ? colorTextoElegido : p.textoBase;
                p.detalle.alpha = esta ? 0.75f : 0.45f;
                p.detalle.text = ops[i].detalle;
                p.detalle.gameObject.SetActive(!string.IsNullOrEmpty(ops[i].detalle));
            }
        }
    }

    private void Esconder()
    {
        for (int i = 0; i < _pastillas.Count; i++)
        {
            _pastillas[i].rt.gameObject.SetActive(false);
        }
    }
}
