using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// El mousecito del HUD: muestra qué botón sirve ahora, prendiendo ese
/// en neón.
///
///   - mano vacía encima de algo agarrable → clic IZQUIERDO (agarrar)
///   - con algo agarrado → clic IZQUIERDO (soltando se suelta)
///   - con una herramienta calzada → clic DERECHO (usarla)
///
/// Si ningún botón sirve, se esconde entero.
///
/// Apaga los componentes que dibujan y no el objeto: si apagara el
/// objeto, este script dejaría de correr y no habría quién lo prendiera.
///
/// Va en el objeto del mouse (el padre de Base, Click_Iz y Click_Der).
/// </summary>
public class AvisoMouse : MonoBehaviour
{
    [SerializeField] private Mano mano;

    [Header("Partes")]
    [SerializeField] private Image clickIzquierdo;
    [SerializeField] private Image clickDerecho;

    [Header("Encendido")]
    [Tooltip("El material neón (el mismo del slider sirve).")]
    [SerializeField] private Material neon;

    [Tooltip("Color del botón que sirve.")]
    [SerializeField] private Color colorPrendido = Color.white;

    [Tooltip("Color del botón que no sirve. Sin neón.")]
    [SerializeField] private Color colorApagado = new Color(1f, 1f, 1f, 0.3f);

    [Tooltip("Que el clic derecho se prenda con una herramienta calzada.")]
    [SerializeField] private bool avisarHerramienta = true;

    private Graphic[] _partes;
    private bool _visible = true;
    private int _estado = -1;

    private void Awake()
    {
        _partes = GetComponentsInChildren<Graphic>(true);
    }

    private void LateUpdate()
    {
        if (mano == null)
        {
            return;
        }

        // Con la herramienta trabajando se esconde entero: ya estás
        // apretando el botón que hay que apretar, y el cartel solo tapa
        // justo lo que estás mirando. El izquierdo se apaga solo,
        // porque mientras tanto no hay candidato.
        bool izq = mano.Agarrado != null || mano.Candidato() != null;
        bool der = avisarHerramienta && mano.TieneEquipado && !mano.HerramientaEnUso;

        Mostrar(izq || der);

        // solo se repinta cuando cambia
        int estado = (izq ? 1 : 0) | (der ? 2 : 0);
        if (estado == _estado)
        {
            return;
        }
        _estado = estado;
        Pintar(clickIzquierdo, izq);
        Pintar(clickDerecho, der);
    }

    private void Pintar(Image boton, bool prendido)
    {
        if (boton == null)
        {
            return;
        }
        boton.material = prendido ? neon : null;
        boton.color = prendido ? colorPrendido : colorApagado;
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
