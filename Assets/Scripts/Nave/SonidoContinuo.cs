using UnityEngine;

/// <summary>
/// Un sonido en bucle que se prende y se apaga con algo que dura: el
/// motor del taladro, la llama del soldador, un motor de la nave.
///
/// No va por el componente Sonidos: ese es para golpes sueltos
/// (PlayOneShot), y un bucle necesita su propio AudioSource para poder
/// pararlo. Por eso es un componente aparte, uno por sonido.
///
/// Entra y sale con una rampa de volumen en vez de cortarse en seco.
/// Un bucle que arranca y para de golpe hace "clic" en los extremos de
/// la onda, y además suena a interruptor y no a máquina.
///
/// Va en el objeto que hace el ruido. Se maneja desde código con
/// <see cref="Activo"/>.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class SonidoContinuo : MonoBehaviour
{
    [Tooltip("El bucle. Tiene que ser un clip que empalme consigo " +
        "mismo, si no se oye el salto en cada vuelta.")]
    [SerializeField] private AudioClip clip;

    [Tooltip("Qué tan fuerte suena cuando está del todo prendido.")]
    [Range(0f, 1f)]
    [SerializeField] private float volumen = 1f;

    [Tooltip("El tono. Abajo de 1 suena más grave y más pesado.")]
    [Range(0.1f, 3f)]
    [SerializeField] private float tono = 1f;

    [Tooltip("Segundos que tarda en llegar a todo volumen.")]
    [Min(0f)]
    [SerializeField] private float subida = 0.06f;

    [Tooltip("Segundos que tarda en apagarse. Un poco más larga que la " +
        "subida se oye como algo que FRENA en vez de algo que se corta.")]
    [Min(0f)]
    [SerializeField] private float bajada = 0.15f;

    [Header("Contra el parpadeo")]
    [Tooltip("Cuánto sigue sonando después de que se apagó el motivo, " +
        "antes de empezar a bajar.\n\n" +
        "Es lo que tapa los huecos: el taladro muerde a tics, así que " +
        "\"está cortando\" se prende y se apaga muchas veces por " +
        "segundo. Con esta espera eso se oye como un sonido solo.")]
    [Min(0f)]
    [SerializeField] private float espera = 0.25f;

    [Tooltip("Lo mínimo que suena una vez que arrancó, aunque sueltes " +
        "enseguida. Para que machacar la tecla no haga metralleta.")]
    [Min(0f)]
    [SerializeField] private float minimo = 0.2f;

    private AudioSource _fuente;
    private float _nivel;

    // "sonando" no es lo mismo que Activo: es Activo más la espera y el
    // mínimo. Es lo que de verdad decide si se oye.
    private bool _sonando;
    private float _desdeCuando;
    private float _ultimoActivo = float.NegativeInfinity;

    /// <summary>
    /// ¿Tiene que estar sonando? Se puede poner todos los frames: la
    /// rampa se encarga de que no se note el ida y vuelta.
    /// </summary>
    public bool Activo { get; set; }

    private void Awake()
    {
        _fuente = GetComponent<AudioSource>();
        _fuente.clip = clip;
        _fuente.loop = true;
        _fuente.playOnAwake = false;
        _fuente.pitch = tono;
        // arranca callado: el volumen lo maneja la rampa, no el
        // Inspector del AudioSource
        _fuente.volume = 0f;
    }

    private void OnDisable()
    {
        // Si la herramienta se guarda o se destruye sonando, que el
        // ruido no quede dando vueltas sin dueño.
        Activo = false;
        _sonando = false;
        _nivel = 0f;
        if (_fuente != null)
        {
            _fuente.Stop();
        }
    }

    /// <summary>
    /// Traduce el ir y venir de <see cref="Activo"/> en algo que se
    /// pueda escuchar.
    ///
    /// Prende al toque —el sonido tiene que salir cuando apretás, sin
    /// retraso— pero para apagarse pide dos cosas: que haya pasado la
    /// espera desde la última vez que estuvo activo, y que haya sonado
    /// al menos el mínimo. Lo primero tapa los huecos de algo que
    /// parpadea; lo segundo, el machaque de la tecla.
    /// </summary>
    private void ActualizarSonando()
    {
        if (Activo)
        {
            _ultimoActivo = Time.time;
            if (!_sonando)
            {
                _sonando = true;
                _desdeCuando = Time.time;
            }
            return;
        }

        if (!_sonando)
        {
            return;
        }
        if (Time.time - _ultimoActivo >= espera
            && Time.time - _desdeCuando >= minimo)
        {
            _sonando = false;
        }
    }

    private void Update()
    {
        if (_fuente == null || _fuente.clip == null)
        {
            return;
        }

        ActualizarSonando();

        float objetivo = _sonando ? 1f : 0f;
        float rampa = _sonando ? subida : bajada;
        _nivel = rampa > 0f
            ? Mathf.MoveTowards(_nivel, objetivo, Time.deltaTime / rampa)
            : objetivo;

        _fuente.volume = _nivel * volumen;

        // Parar del todo en silencio y no dejarlo girando mudo: un
        // AudioSource sonando cuesta aunque no se oiga, y acá puede
        // haber uno por herramienta.
        if (_nivel <= 0f)
        {
            if (_fuente.isPlaying)
            {
                _fuente.Stop();
            }
        }
        else if (!_fuente.isPlaying)
        {
            _fuente.Play();
        }
    }
}
