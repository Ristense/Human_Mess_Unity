using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Un aviso que se prende solo cuando toca: el del clic cuando la mano
/// está encima de algo que se puede agarrar, el de la tecla cuando ya lo
/// tenés agarrado. El resto del tiempo, apagado.
///
/// Usa la misma búsqueda que el agarre (Mano.Candidato), así que se
/// prende exactamente cuando un clic agarraría algo.
///
/// Apaga los componentes que dibujan (el texto, un fondo si tiene) y no
/// el objeto: si apagara el objeto, este script dejaría de correr y no
/// habría quién lo volviera a prender.
///
/// Mientras manejás el cable del anclaje (perno clavado + Ctrl) el
/// cartel se presta: en vez de la tecla muestra cuántos metros de
/// cuerda hay afuera, y al soltar Ctrl vuelve a lo de antes. Va acá y
/// no en un script aparte porque los dos pelean por el mismo texto:
/// separados habría que acordarse de poner el otro componente, y el
/// orden entre los dos pasaría a importar.
///
/// Va en el objeto del aviso.
/// </summary>
public class AvisoTecla : MonoBehaviour
{
    public enum Cuando
    {
        [Tooltip("Mano vacía encima de algo que se puede agarrar (el clic).")]
        EncimaDeAlgo,
        [Tooltip("Con algo agarrado (la E, para calzarlo o anclarlo).")]
        Agarrando,
        [Tooltip("En los dos casos.")]
        LasDos,
    }

    [SerializeField] private Mano mano;

    [Tooltip("Cuándo se prende. El aviso del clic va en EncimaDeAlgo, y el " +
        "de la tecla en Agarrando.")]
    [SerializeField] private Cuando cuando = Cuando.EncimaDeAlgo;

    [Tooltip("Partes que se esconden mientras mantenés la tecla (por " +
        "ejemplo el fondo, para que se vea el slider).")]
    [SerializeField] private Graphic[] ocultarAlMantener;

    [Header("Cable del anclaje")]
    [Tooltip("Con el perno clavado y Ctrl apretado, este cartel muestra " +
        "los metros de cuerda en vez de la tecla.")]
    [SerializeField] private bool mostrarCable = true;

    [Tooltip("El texto que se reemplaza. Vacío = el de este objeto.")]
    [SerializeField] private TMP_Text texto;

    [Tooltip("Cuántos metros vale una unidad de mundo. 100, igual que " +
        "el velocímetro.")]
    [SerializeField] private float metrosPorUnidad = 100f;

    [Tooltip("Cómo se escribe el número. {0} son los metros.")]
    [SerializeField] private string formato = "{0:0} m";

    private Graphic[] _partes;
    private bool _visible = true;

    // lo que decía el cartel antes de prestarse (la tecla): es a lo que
    // hay que volver, y sale del propio texto para no tener una segunda
    // copia que se desincronice si lo cambiás en el Inspector
    private string _original;
    private bool _conCable;

    private void Awake()
    {
        // todo lo que dibuja, en este objeto y sus hijos
        _partes = GetComponentsInChildren<Graphic>(true);
        if (texto == null)
        {
            texto = GetComponentInChildren<TMP_Text>(true);
        }
        if (texto != null)
        {
            _original = texto.text;
        }
        Mostrar(false);
    }

    private void LateUpdate()
    {
        // El cable manda: si lo estás soltando o recogiendo, eso es lo
        // único que importa ahí. Y se muestra aunque el aviso de la
        // tecla no correspondiera — con la herramienta equipada no hay
        // nada "agarrado", así que normalmente este cartel está apagado.
        if (ActualizarCable())
        {
            Mostrar(true);
            // si el fondo había quedado escondido por mantener la
            // tecla, acá vuelve: el número necesita su marco
            Ocultables(true);
            return;
        }

        if (mano == null)
        {
            return;
        }

        bool agarrando = mano.Agarrado != null;
        bool visible = agarrando
            ? cuando != Cuando.EncimaDeAlgo
            : cuando != Cuando.Agarrando && mano.Candidato() != null;
        Mostrar(visible);

        if (visible)
        {
            Ocultables(mano.Progreso <= 0f);
        }
    }

    /// <summary>Prende o apaga las partes que estorban al mantener la tecla.</summary>
    private void Ocultables(bool visible)
    {
        if (ocultarAlMantener == null)
        {
            return;
        }
        foreach (Graphic g in ocultarAlMantener)
        {
            if (g != null)
            {
                g.enabled = visible;
            }
        }
    }

    /// <summary>
    /// Pone los metros de cuerda en el cartel, o lo devuelve a como
    /// estaba. Dice si el cable se lo quedó este frame.
    /// </summary>
    private bool ActualizarCable()
    {
        if (!mostrarCable || texto == null)
        {
            return false;
        }

        Anclaje anclaje = Anclaje.ConElCable;
        if (anclaje == null)
        {
            if (_conCable)
            {
                _conCable = false;
                texto.text = _original;
            }
            return false;
        }

        _conCable = true;
        texto.text = string.Format(formato, anclaje.LargoCable * metrosPorUnidad);
        return true;
    }

    private void OnDisable()
    {
        // si el cartel se apaga con los metros puestos, al volver tiene
        // que decir la tecla otra vez
        if (_conCable && texto != null)
        {
            texto.text = _original;
        }
        _conCable = false;
    }

    private void Mostrar(bool visible)
    {
        if (_visible == visible)
        {
            return;
        }
        _visible = visible;
        foreach (Graphic g in _partes)
        {
            if (g != null)
            {
                g.enabled = visible;
            }
        }
    }
}
