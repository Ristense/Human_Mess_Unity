using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// El medidor de calor del soldador: una barra que sube con el clic
/// sostenido y que hay que mantener adentro de la franja.
///
/// Es una Image con Image Type = Filled y NO un Slider. Un Slider está
/// hecho para que el usuario arrastre un handle: trae interacción,
/// navegación por teclado y un pivote que acá sobra y se come clics del
/// minijuego. Esto se llena solo y no se toca.
///
/// La franja se acomoda sola desde los valores del soldador, con
/// anclajes en vez de píxeles: así no hay dos lugares donde decir
/// dónde está el rango, y si se cambia el mínimo o el máximo en el
/// Inspector, el dibujo sigue. Un rectángulo puesto a mano queda
/// mintiendo a la primera que alguien toca un número.
///
/// Va en el objeto del medidor, adentro de la ventana del soldador.
/// </summary>
public class BarraCalor : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("De dónde sale el calor. Vacío = lo busca en la ventana.")]
    [SerializeField] private SoldadorCorte soldador;

    [Tooltip("La barra que se llena. Tiene que ser una Image con " +
        "Image Type = Filled. El Fill Method decide si el medidor es " +
        "vertical u horizontal; la franja se acomoda a eso.")]
    [SerializeField] private Image relleno;

    [Tooltip("El rectángulo que marca la franja donde corta. Se " +
        "acomoda solo según el rango del soldador — ponelo como hijo " +
        "del fondo de la barra y no te preocupes por su tamaño.\n\n" +
        "Opcional: sin esto el medidor funciona igual, solo que no se " +
        "ve a qué hay que apuntar.")]
    [SerializeField] private RectTransform zona;

    [Header("Colores")]
    [Tooltip("Frío: todavía no corta.")]
    [SerializeField] private Color colorFrio = new Color(0.45f, 0.6f, 0.85f);

    [Tooltip("En la franja: está cortando.")]
    [SerializeField] private Color colorEnRango = new Color(1f, 0.75f, 0.25f);

    [Tooltip("Recalentado: se pasó y dejó de cortar.")]
    [SerializeField] private Color colorRecalentado = new Color(0.9f, 0.3f, 0.2f);

    [Tooltip("Segundos que tarda el color en cambiar. 0 = al toque. Un " +
        "poquito evita el parpadeo al rozar el borde de la franja.")]
    [SerializeField] private float suavizadoColor = 0.08f;

    [Header("Aviso de recalentado")]
    [Tooltip("Cuánto parpadea la barra al recalentarse, en veces por " +
        "segundo. 0 = no parpadea.")]
    [SerializeField] private float parpadeo = 4f;

    private float _ultimoMin = -1f;
    private float _ultimoMax = -1f;
    private bool _ultimoHorizontal;
    private Color _color;

    private void OnEnable()
    {
        if (soldador == null)
        {
            // Arriba si la barra cuelga del soldador, abajo si cuelga de
            // la ventana: el soldador vive en la punta, que está varios
            // niveles MÁS ABAJO que la ventana. Buscar solo para un lado
            // obliga a adivinar dónde se puso la barra.
            soldador = GetComponentInParent<SoldadorCorte>()
                ?? GetComponentInChildren<SoldadorCorte>(true);
        }
        if (soldador == null)
        {
            // Sin esto la barra se queda en cero para siempre y no hay
            // forma de adivinar por qué.
            Debug.LogWarning("[BarraCalor] No encuentro el SoldadorCorte. " +
                "Asignalo a mano o poné la barra adentro de la ventana " +
                "del soldador.", this);
            return;
        }

        // que no herede el color del frame anterior al reabrir
        _color = colorFrio;
        Acomodar(true);
    }

    private void LateUpdate()
    {
        if (soldador == null || relleno == null)
        {
            return;
        }

        relleno.fillAmount = soldador.Calor;

        Color objetivo = soldador.Recalentado
            ? colorRecalentado
            : soldador.EnRango ? colorEnRango : colorFrio;

        _color = suavizadoColor > 0f
            ? Color.Lerp(_color, objetivo, Time.deltaTime / suavizadoColor)
            : objetivo;

        // El parpadeo va sobre el alfa y no sobre el color: así se nota
        // sin pelearse con el rojo, que es lo que comunica el problema.
        float alfa = 1f;
        if (soldador.Recalentado && parpadeo > 0f)
        {
            alfa = Mathf.Lerp(0.45f, 1f,
                Mathf.PingPong(Time.unscaledTime * parpadeo * 2f, 1f));
        }

        relleno.color = new Color(_color.r, _color.g, _color.b, _color.a * alfa);

        Acomodar(false);
    }

    /// <summary>
    /// Pone la franja donde dice el soldador. Se usan anclajes y no
    /// tamaños en píxeles para que valga igual con la barra de
    /// cualquier alto, y para que acompañe si el Canvas se escala.
    /// </summary>
    private void Acomodar(bool forzar)
    {
        if (zona == null)
        {
            return;
        }

        float min = soldador.RangoMin;
        float max = soldador.RangoMax;
        bool horizontal = relleno != null
            && relleno.fillMethod == Image.FillMethod.Horizontal;

        if (!forzar && Mathf.Approximately(min, _ultimoMin)
            && Mathf.Approximately(max, _ultimoMax)
            && horizontal == _ultimoHorizontal)
        {
            return;
        }

        _ultimoMin = min;
        _ultimoMax = max;
        _ultimoHorizontal = horizontal;

        zona.anchorMin = horizontal ? new Vector2(min, 0f) : new Vector2(0f, min);
        zona.anchorMax = horizontal ? new Vector2(max, 1f) : new Vector2(1f, max);
        zona.offsetMin = Vector2.zero;
        zona.offsetMax = Vector2.zero;
    }
}
