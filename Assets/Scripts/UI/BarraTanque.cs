using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Una barra de tanque: muestra cuánto queda de oxígeno o de
/// hidrógeno. Una por tanque, cada una con su sprite.
///
/// Usa una Image con Image Type = Filled, igual que la barra de calor
/// del soldador y por la misma razón: un Slider trae un handle
/// arrastrable que acá no pinta nada.
///
/// Va en el objeto de la barra, en el Canvas.
/// </summary>
public class BarraTanque : MonoBehaviour
{
    public enum Cual
    {
        Oxigeno,
        Hidrogeno
    }

    [Tooltip("De dónde sale el nivel. Vacío = lo busca en la escena.")]
    [SerializeField] private SoporteVital soporte;

    [Tooltip("Qué tanque muestra esta barra.")]
    [SerializeField] private Cual tanque = Cual.Oxigeno;

    [Tooltip("Para un tanque DIEGÉTICO (un sprite pegado a la nave): " +
        "el SpriteRenderer, con material del shader " +
        "HumanMess/RellenoVertical.\n\n" +
        "Se llena por shader y no escalando el objeto porque escalarlo " +
        "estira el dibujo y lo despega del casco.")]
    [SerializeField] private SpriteRenderer sprite;

    [Tooltip("Para un tanque en el HUD: la Image que se llena, con " +
        "Image Type = Filled, Fill Method = Vertical, Fill Origin = " +
        "Bottom.\n\n" +
        "Se usa uno u otro, no los dos.")]
    [SerializeField] private Image relleno;

    [Tooltip("Cómo se llama el nivel en el shader.")]
    [SerializeField] private string propiedad = "_Relleno";

    [Header("Aviso")]
    [Tooltip("Desde qué nivel se considera que queda poco, de 0 a 1.")]
    [Range(0f, 1f)]
    [SerializeField] private float avisoBajo = 0.2f;

    [SerializeField] private Color colorNormal = Color.white;
    [SerializeField] private Color colorBajo = new Color(0.9f, 0.3f, 0.2f);

    [Tooltip("Parpadeo cuando queda poco, en veces por segundo. 0 = no " +
        "parpadea.")]
    [SerializeField] private float parpadeo = 2f;

    private MaterialPropertyBlock _bloque;
    private int _id;

    private void Reset()
    {
        sprite = GetComponent<SpriteRenderer>();
        relleno = GetComponent<Image>();
    }

    private void OnEnable()
    {
        if (soporte == null)
        {
            soporte = FindAnyObjectByType<SoporteVital>();
        }
        if (sprite == null && relleno == null)
        {
            sprite = GetComponent<SpriteRenderer>();
            relleno = GetComponent<Image>();
        }

        _bloque = new MaterialPropertyBlock();
        // por ID y no por nombre: resolver el string es lo caro, y esto
        // se escribe todos los frames
        _id = Shader.PropertyToID(propiedad);
    }

    private void LateUpdate()
    {
        if (soporte == null)
        {
            return;
        }

        float nivel = tanque == Cual.Oxigeno ? soporte.Oxigeno : soporte.Hidrogeno;

        Color c = nivel <= avisoBajo ? colorBajo : colorNormal;

        // El parpadeo va en el alfa y no en el color, para que el rojo
        // del aviso no se pierda a mitad de ciclo.
        if (nivel <= avisoBajo && parpadeo > 0f)
        {
            c.a *= Mathf.Lerp(0.4f, 1f,
                Mathf.PingPong(Time.unscaledTime * parpadeo * 2f, 1f));
        }

        if (sprite != null)
        {
            // MaterialPropertyBlock y no material.SetFloat: lo segundo
            // clona el material por cada tanque y rompe el batching.
            sprite.GetPropertyBlock(_bloque);
            _bloque.SetFloat(_id, nivel);
            sprite.SetPropertyBlock(_bloque);
            sprite.color = c;
        }

        if (relleno != null)
        {
            relleno.fillAmount = nivel;
            relleno.color = c;
        }
    }
}
