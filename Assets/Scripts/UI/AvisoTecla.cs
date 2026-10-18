using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Un aviso que se prende solo cuando toca: el del clic cuando la mano
/// está encima de algo que se puede agarrar, el de la E cuando ya lo
/// tenés agarrado. El resto del tiempo, apagado.
///
/// Usa la misma búsqueda que el agarre (Mano.Candidato), así que se
/// prende exactamente cuando un clic agarraría algo.
///
/// Apaga los componentes que dibujan (el texto, un fondo si tiene) y no
/// el objeto: si apagara el objeto, este script dejaría de correr y no
/// habría quién lo volviera a prender.
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
        "de la E en Agarrando.")]
    [SerializeField] private Cuando cuando = Cuando.EncimaDeAlgo;

    [Tooltip("Partes que se esconden mientras mantenés la tecla (por " +
        "ejemplo el fondo, para que se vea el slider).")]
    [SerializeField] private Graphic[] ocultarAlMantener;

    private Graphic[] _partes;
    private bool _visible = true;

    private void Awake()
    {
        // todo lo que dibuja, en este objeto y sus hijos
        _partes = GetComponentsInChildren<Graphic>(true);
        Mostrar(false);
    }

    private void LateUpdate()
    {
        if (mano == null)
        {
            return;
        }

        bool agarrando = mano.Agarrado != null;
        bool visible = agarrando
            ? cuando != Cuando.EncimaDeAlgo
            : cuando != Cuando.Agarrando && mano.Candidato() != null;
        Mostrar(visible);

        if (visible && ocultarAlMantener != null)
        {
            bool manteniendo = mano.Progreso > 0f;
            foreach (Graphic g in ocultarAlMantener)
            {
                if (g != null)
                {
                    g.enabled = !manteniendo;
                }
            }
        }
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
