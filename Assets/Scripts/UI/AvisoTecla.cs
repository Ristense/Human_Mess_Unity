using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Un aviso (la "E") que se prende solo cuando la mano está encima de
/// algo que se puede agarrar, y se apaga el resto del tiempo.
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
    [SerializeField] private Mano mano;

    [Tooltip("Que también se vea mientras tenés algo agarrado: ahí la " +
        "tecla sirve para calzártelo o anclarlo.")]
    [SerializeField] private bool tambienAgarrando = false;

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
        bool visible = agarrando ? tambienAgarrando : mano.Candidato() != null;
        Mostrar(visible);
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
