using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Una secuencia de diálogos: línea por línea, escribiéndose letra por
/// letra. Clic, Espacio o Enter avanzan; si la línea todavía se está
/// escribiendo, el primer toque la completa de una y el segundo pasa a
/// la siguiente. Esc salta todo.
///
/// Las líneas se escriben directo en el Inspector, cada una con quién
/// habla y qué dice.
///
/// Va en el panel del diálogo.
/// </summary>
public class Dialogo : MonoBehaviour
{
    [Serializable]
    public class Linea
    {
        [Tooltip("Quién habla. Vacío = narrador, no se muestra nombre.")]
        public string quien;

        [Tooltip("El rig de quien habla: el objeto con sus piezas y sus " +
            "huesos, de los que mira la cámara del retrato. Vacío = " +
            "narrador, no se muestra a nadie.\n\n" +
            "Los personajes viven todos puestos en la escena; el " +
            "diálogo prende el de esta línea y apaga a los demás.")]
        public GameObject personaje;

        [TextArea(2, 6)]
        public string texto;

        [Tooltip("Opcional: el sonido de letra para ESTA línea. Sirve " +
            "para darle una voz distinta a cada personaje. Vacío = el " +
            "sonido general.")]
        public AudioClip voz;

        [Tooltip("Opcional: el NOMBRE de un estado del Animator del " +
            "personaje (el que se ve en la ventana Animator). Se " +
            "reproduce una vez cuando el retrato ya terminó de entrar, " +
            "mientras se escribe el texto. Vacío = no pasa nada.\n\n" +
            "Si el diálogo avanza antes de que termine, la animación " +
            "se corta: o entra la de la línea nueva, o se apaga el " +
            "personaje.")]
        public string animacion;
    }

    [Header("Textos")]
    [SerializeField] private TMP_Text nombre;
    [SerializeField] private TMP_Text texto;

    [Tooltip("Lo que se dibuja del retrato: el RawImage con la " +
        "RenderTexture de la cámara de retratos. Se funde entero al " +
        "cambiar de personaje.\n\n" +
        "Es un Graphic y no un Image para que acepte el RawImage, que " +
        "es el que muestra RenderTextures.")]
    [SerializeField] private Graphic retrato;

    [Tooltip("Opcional: el marco del retrato. Se esconde junto con el " +
        "monito en las líneas sin personaje (el narrador).")]
    [SerializeField] private GameObject marco;

    [Tooltip("Cuánto dura el fundido del retrato al cambiar de " +
        "personaje, en segundos (la mitad se va, la mitad entra). " +
        "0 = cambia seco, como antes.")]
    [Min(0f)]
    [SerializeField] private float segundosFundido = 0.25f;

    [Tooltip("Opcional: un CanvasGroup en el retrato. Si lo ponés, el " +
        "fundido se le hace a ÉL, así el monito y su marco entran y " +
        "salen juntos. Vacío = se funde solo el retrato, y el marco " +
        "aparece seco.")]
    [SerializeField] private CanvasGroup grupoRetrato;

    [Tooltip("Opcional: algo que aparece cuando la línea terminó de " +
        "escribirse, para avisar que se puede seguir (una flechita).")]
    [SerializeField] private GameObject indicadorSeguir;

    [Header("Ritmo")]
    [SerializeField] private float letrasPorSegundo = 40f;

    [Tooltip("Que Esc salte el diálogo entero.")]
    [SerializeField] private bool sePuedeSaltar = true;

    [Header("Sonido")]
    [Tooltip("El sonidito de cada letra. Uno cortito (un clic, un " +
        "pitido) queda mejor que uno largo.")]
    [SerializeField] private AudioClip sonidoLetra;

    [Tooltip("Suena cada tantas letras. Con 1 a velocidad alta se " +
        "vuelve un zumbido; 2 o 3 queda más natural.")]
    [Min(1)]
    [SerializeField] private int cadaCuantasLetras = 2;

    [Tooltip("Cuánto varía el tono de una letra a otra. Un poquito de " +
        "variación hace que suene a voz y no a máquina.")]
    [Range(0f, 0.5f)]
    [SerializeField] private float variacionTono = 0.08f;

    [Range(0f, 1f)]
    [SerializeField] private float volumen = 0.6f;

    [Tooltip("Vacío = crea uno solo.")]
    [SerializeField] private AudioSource fuente;

    [Header("Lo que se dice")]
    [SerializeField] private Linea[] lineas;

    private Action _alTerminar;
    private int _actual = -1;
    private bool _escribiendo;
    private bool _activo;
    private int _frameInicio;
    private Coroutine _escritura;
    private Coroutine _fundido;
    private GameObject _enEscena;
    private string _animPendiente;

    /// <summary>
    /// Arranca el diálogo desde la primera línea. "alTerminar" se llama
    /// cuando se termina o se salta: ahí quien lo empezó sigue con lo
    /// suyo (por ejemplo, cargar el juego).
    /// </summary>
    public void Empezar(Action alTerminar)
    {
        _alTerminar = alTerminar;
        _actual = -1;
        _activo = true;

        // El retrato arranca vacío y transparente: así el primero entra
        // con su fundido en vez de aparecer de golpe. Y si el diálogo se
        // vuelve a abrir, no queda el monito de la última vez.
        if (_fundido != null)
        {
            StopCoroutine(_fundido);
            _fundido = null;
        }
        PonerRetrato(null);
        Alfa(0f);

        // El clic que apretó "Jugar" es de este mismo frame. Sin esto,
        // ese clic también contaría como "avanzar" y la primera línea se
        // saltaría sin que la veas.
        _frameInicio = Time.frameCount;
        Siguiente();
    }

    private void Update()
    {
        if (!_activo || Time.frameCount == _frameInicio)
        {
            return;
        }

        if (sePuedeSaltar && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Terminar();
            return;
        }

        if (!QuiereAvanzar())
        {
            return;
        }

        if (_escribiendo)
        {
            Completar();
        }
        else
        {
            Siguiente();
        }
    }

    private static bool QuiereAvanzar()
    {
        bool clic = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        bool tecla = Keyboard.current != null
            && (Keyboard.current.spaceKey.wasPressedThisFrame
                || Keyboard.current.enterKey.wasPressedThisFrame);
        return clic || tecla;
    }

    private void Siguiente()
    {
        _actual++;
        if (lineas == null || _actual >= lineas.Length)
        {
            Terminar();
            return;
        }

        if (_escritura != null)
        {
            StopCoroutine(_escritura);
        }
        _escritura = StartCoroutine(Escribir(lineas[_actual]));
    }

    /// <summary>
    /// El efecto de máquina de escribir. Se usa maxVisibleCharacters en
    /// vez de ir agregando letras al texto: así el texto entero ya está
    /// acomodado desde el principio y las palabras no saltan de renglón
    /// a medida que aparecen. También respeta las etiquetas como
    /// &lt;b&gt; o &lt;color&gt;, que no cuentan como letras.
    /// </summary>
    private IEnumerator Escribir(Linea linea)
    {
        if (nombre != null)
        {
            nombre.text = linea.quien;
        }
        MostrarRetrato(linea.personaje, linea.animacion);
        texto.text = linea.texto;
        texto.maxVisibleCharacters = 0;
        texto.ForceMeshUpdate();
        int total = texto.textInfo.characterCount;

        _escribiendo = true;
        if (indicadorSeguir != null)
        {
            indicadorSeguir.SetActive(false);
        }

        AudioClip voz = linea.voz != null ? linea.voz : sonidoLetra;
        int letrasDesdeSonido = cadaCuantasLetras;   // la primera letra suena

        float mostradas = 0f;
        int visibles = 0;
        while (visibles < total)
        {
            // tiempo sin escala: si algún día el juego se pausa con
            // timeScale 0, los diálogos tienen que seguir andando
            mostradas += Time.unscaledDeltaTime * letrasPorSegundo;
            int nuevas = Mathf.Min(total, (int)mostradas);

            // A buena velocidad aparece más de una letra por frame: se
            // revisan todas las nuevas, pero suena una sola vez por frame
            // como mucho, si no se encimarían y saturaría.
            bool sonar = false;
            for (int i = visibles; i < nuevas; i++)
            {
                // los espacios y la puntuación no suenan: así las pausas
                // entre palabras se escuchan como pausas
                if (!char.IsLetterOrDigit(texto.textInfo.characterInfo[i].character))
                {
                    continue;
                }
                letrasDesdeSonido++;
                if (letrasDesdeSonido >= cadaCuantasLetras)
                {
                    letrasDesdeSonido = 0;
                    sonar = true;
                }
            }
            if (sonar)
            {
                SonarLetra(voz);
            }

            visibles = nuevas;
            texto.maxVisibleCharacters = visibles;
            yield return null;
        }

        Completar();
    }

    /// <summary>
    /// Cambia el monito con un fundido.
    ///
    /// Solo si de verdad CAMBIA de sprite: si el mismo personaje habla
    /// dos líneas seguidas, fundirlo igual se vería como un parpadeo
    /// sin motivo — y es el caso más común.
    ///
    /// Va en su propia corrutina, aparte de la que escribe el texto:
    /// las dos corren a la vez, así el texto no espera al retrato.
    /// </summary>
    private void MostrarRetrato(GameObject nuevo, string animacion)
    {
        _animPendiente = animacion;

        if (nuevo == _enEscena)
        {
            // Si todavía está entrando, la corrutina del fundido toca
            // la animación pendiente cuando termine
            if (_fundido == null)
            {
                Animar(nuevo, animacion);
            }
            return;
        }

        if (_fundido != null)
        {
            StopCoroutine(_fundido);
            _fundido = null;
        }

        if (segundosFundido <= 0f)
        {
            PonerRetrato(nuevo);
            Alfa(1f);
            Animar(nuevo, animacion);
            return;
        }
        _fundido = StartCoroutine(Fundir(nuevo));
    }

    private IEnumerator Fundir(GameObject nuevo)
    {
        float mitad = segundosFundido * 0.5f;

        // el que estaba se va primero; si no había nadie, no hay nada
        // que sacar y entra directo
        if (_enEscena != null)
        {
            yield return Rampa(Leer(), 0f, mitad);
        }

        PonerRetrato(nuevo);

        if (nuevo != null)
        {
            yield return Rampa(0f, 1f, mitad);
        }
        _fundido = null;
        Animar(nuevo, _animPendiente);
    }

    /// <summary>
    /// Toca un estado del Animator del personaje, desde el principio.
    /// Corre con tiempo sin escala, igual que el resto del diálogo.
    /// </summary>
    private static void Animar(GameObject quien, string estado)
    {
        if (quien == null || string.IsNullOrEmpty(estado))
        {
            return;
        }
        Animator animador = quien.GetComponentInChildren<Animator>();
        if (animador == null)
        {
            Debug.LogWarning($"Dialogo: {quien.name} no tiene Animator, " +
                $"no se puede tocar '{estado}'.", quien);
            return;
        }
        animador.updateMode = AnimatorUpdateMode.UnscaledTime;
        animador.Play(estado, 0, 0f);
    }

    private IEnumerator Rampa(float desde, float hasta, float duracion)
    {
        if (duracion <= 0f)
        {
            Alfa(hasta);
            yield break;
        }

        // sin escala, igual que la máquina de escribir: con el juego en
        // pausa los diálogos tienen que seguir andando
        for (float t = 0f; t < duracion; t += Time.unscaledDeltaTime)
        {
            Alfa(Mathf.Lerp(desde, hasta, t / duracion));
            yield return null;
        }
        Alfa(hasta);
    }

    /// <summary>
    /// Deja en escena solo al personaje de esta línea. Se apagan TODOS
    /// los que aparecen en el diálogo y no solo el anterior: si dos
    /// líneas seguidas se cortan a mitad del fundido, podría haber
    /// quedado más de uno prendido.
    /// </summary>
    private void PonerRetrato(GameObject quien)
    {
        _enEscena = quien;

        if (lineas != null)
        {
            foreach (Linea l in lineas)
            {
                if (l.personaje != null && l.personaje != quien)
                {
                    l.personaje.SetActive(false);
                }
            }
        }
        if (quien != null)
        {
            quien.SetActive(true);
        }

        if (retrato != null)
        {
            retrato.enabled = quien != null;
        }
        if (marco != null)
        {
            marco.SetActive(quien != null);
        }
    }

    private void Alfa(float a)
    {
        if (grupoRetrato != null)
        {
            grupoRetrato.alpha = a;
            return;
        }
        if (retrato != null)
        {
            Color c = retrato.color;
            c.a = a;
            retrato.color = c;
        }
    }

    private float Leer()
    {
        if (grupoRetrato != null)
        {
            return grupoRetrato.alpha;
        }
        return retrato != null ? retrato.color.a : 1f;
    }

    private void SonarLetra(AudioClip clip)
    {
        if (clip == null)
        {
            return;
        }
        if (fuente == null)
        {
            fuente = gameObject.AddComponent<AudioSource>();
            fuente.playOnAwake = false;
        }
        // UnityEngine.Random y no Random a secas: con "using System" arriba,
        // el nombre choca con System.Random y no compila
        fuente.pitch = 1f + UnityEngine.Random.Range(-variacionTono, variacionTono);
        // PlayOneShot y no Play: Play cortaría el sonido anterior a la
        // mitad, y con letras rápidas se escucharía entrecortado
        fuente.PlayOneShot(clip, volumen);
    }

    private void Completar()
    {
        if (_escritura != null)
        {
            StopCoroutine(_escritura);
            _escritura = null;
        }
        texto.maxVisibleCharacters = int.MaxValue;
        _escribiendo = false;
        if (indicadorSeguir != null)
        {
            indicadorSeguir.SetActive(true);
        }
    }

    private void Terminar()
    {
        _activo = false;
        if (_escritura != null)
        {
            StopCoroutine(_escritura);
            _escritura = null;
        }
        // se suelta antes de llamar: si alTerminar empezara otro
        // diálogo, no tiene que pisarse con este
        Action alTerminar = _alTerminar;
        _alTerminar = null;
        alTerminar?.Invoke();
    }
}
