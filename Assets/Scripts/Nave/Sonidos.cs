using UnityEngine;

/// <summary>
/// Los efectos de sonido de la nave, todos en un lugar.
///
/// Uno solo y central en vez de un AudioSource por cosa: así el volumen
/// se toca en un lado, y no quedan fuentes sueltas repartidas por la
/// jerarquía que después nadie encuentra.
///
/// Cada efecto acepta VARIOS clips y un rango de tono. No es adorno:
/// un sonido que se repite muchas veces por minuto, como el de agarrar,
/// suena a máquina si sale idéntico siempre. Variando un poco el tono
/// ya se oye como un objeto de verdad, aunque tengas un solo clip.
///
/// Va en la nave. Quien lo necesite lo encuentra solo.
/// </summary>
public class Sonidos : MonoBehaviour
{
    /// <summary>Un efecto: uno o varios clips, con su volumen y su tono.</summary>
    [System.Serializable]
    public struct Efecto
    {
        [Tooltip("Los clips. Si hay más de uno, sale uno al azar.")]
        public AudioClip[] clips;

        [Tooltip("Qué tan fuerte suena. 0 = mudo.")]
        [Range(0f, 1f)]
        public float volumen;

        [Tooltip("Cuánto varía el tono, hacia arriba y hacia abajo. " +
            "0.1 = ±10%, que ya alcanza para que no suene a repetición. " +
            "0 = siempre igual.")]
        [Range(0f, 0.5f)]
        public float variacion;

        [Tooltip("Lo mínimo entre dos veces, en segundos. Machacando la " +
            "tecla, los de más se tragan en vez de amontonarse.")]
        [Min(0f)]
        public float esperaMinima;

        // Cuándo sonó por última vez. NonSerialized: es estado de la
        // partida, no un ajuste — si se guardara, al abrir Unity
        // volvería con una hora vieja.
        [System.NonSerialized]
        public float ultimaVez;
    }

    [Tooltip("Por dónde sale. Vacío = se crea uno acá mismo.")]
    [SerializeField] private AudioSource fuente;

    [Header("La mano")]
    [Tooltip("Agarrar algo con el clic. El snap metálico.")]
    [SerializeField] private Efecto agarrar;

    [Tooltip("Encajar una pieza en su lugar (montarla o equiparla).")]
    [SerializeField] private Efecto encajar;

    // Al agregar el componente, los campos de un struct arrancan en
    // cero — o sea volumen 0, o sea mudo, sin que se note por qué. Esto
    // los deja usables de entrada: ponés el clip y suena.
    private void Reset()
    {
        agarrar = PorDefecto();
        encajar = PorDefecto();
    }

    private static Efecto PorDefecto() => new Efecto
    {
        volumen = 1f,
        variacion = 0.08f,
        esperaMinima = 0.08f,
    };

    private void Awake()
    {
        if (fuente == null)
        {
            fuente = GetComponent<AudioSource>();
        }
        if (fuente == null)
        {
            fuente = gameObject.AddComponent<AudioSource>();
        }
        // Que no arranque sonando lo que sea que tenga puesto, y que no
        // se apague solo al terminar un clip: esta fuente es solo para
        // PlayOneShot.
        fuente.playOnAwake = false;
        fuente.Stop();
    }

    /// <summary>El snap de agarrar algo con la mano.</summary>
    public void Agarrar() => Sonar(ref agarrar);

    /// <summary>Una pieza que entra en su lugar.</summary>
    public void Encajar() => Sonar(ref encajar);

    // por referencia y no por copia: el efecto se anota a sí mismo
    // cuándo sonó, y sobre una copia esa anotación se perdería
    private void Sonar(ref Efecto efecto)
    {
        if (fuente == null || efecto.clips == null || efecto.clips.Length == 0)
        {
            return;
        }

        if (efecto.esperaMinima > 0f
            && Time.time - efecto.ultimaVez < efecto.esperaMinima)
        {
            return;
        }
        efecto.ultimaVez = Time.time;

        AudioClip clip = efecto.clips.Length == 1
            ? efecto.clips[0]
            : efecto.clips[Random.Range(0, efecto.clips.Length)];
        if (clip == null)
        {
            return;
        }

        // El tono es del AudioSource y no del clip, así que hay que
        // ponerlo ANTES de disparar. Los que ya están sonando se quedan
        // con el que tenían: PlayOneShot los deja correr aparte.
        fuente.pitch = efecto.variacion > 0f
            ? 1f + Random.Range(-efecto.variacion, efecto.variacion)
            : 1f;

        // PlayOneShot y no Play: dos agarres seguidos se superponen en
        // vez de cortarse, que es lo que pasaría en la realidad.
        fuente.PlayOneShot(clip, efecto.volumen);
    }
}
