using UnityEngine;

/// <summary>
/// Barrita de progreso hecha con dos sprites: un fondo y un relleno
/// que se estira. Nada de Canvas — así se ordena con Order in Layer
/// como el resto, le pegan las Light2D y no depende de la escala del
/// proyecto.
///
/// El relleno tiene que tener el PIVOT A LA IZQUIERDA (Sprite Editor >
/// Pivot > Left). Si está al centro, crece para los dos lados y queda
/// como una barra de ecualizador.
///
/// Va en el objeto padre de la barra.
/// </summary>
public class BarraProgreso : MonoBehaviour
{
    [Tooltip("De dónde sale el progreso.")]
    [SerializeField] private Mano mano;

    [Tooltip("El sprite que se estira. Pivot a la izquierda.")]
    [SerializeField] private Transform relleno;

    [Tooltip("Esconder la barra cuando no hay nada en curso. " +
        "Apagalo si querés verla siempre para acomodarla.")]
    [SerializeField] private bool esconderEnReposo = true;

    [Tooltip("Qué tan rápido aparece y desaparece, en segundos. " +
        "0 = seco. Un valor chico evita el parpadeo cuando tocás la " +
        "tecla sin querer.")]
    [SerializeField] private float suavizado = 0.08f;

    private float _mostrado;
    private float _velocidad;
    private Vector3 _escalaBase;
    private bool _visible = true;

    private void Awake()
    {
        if (relleno != null)
        {
            // guardamos la escala que le pusiste en el editor para
            // escalar RELATIVO a eso, y no pisarle el grosor
            _escalaBase = relleno.localScale;
        }
    }

    private void LateUpdate()
    {
        if (mano == null || relleno == null)
        {
            return;
        }

        float objetivo = mano.Progreso;
        _mostrado = suavizado > 0f
            ? Mathf.SmoothDamp(_mostrado, objetivo, ref _velocidad, suavizado)
            : objetivo;

        Vector3 e = _escalaBase;
        e.x = _escalaBase.x * _mostrado;
        relleno.localScale = e;

        if (esconderEnReposo)
        {
            // el umbral es para que no quede un pelito de barra
            // dibujado cuando el suavizado todavía está bajando
            bool visible = _mostrado > 0.001f;
            if (_visible != visible)
            {
                _visible = visible;
                // se apagan los HIJOS y no este objeto: apagarse a sí
                // mismo frenaría el script y nunca volvería a prender
                foreach (Transform h in transform)
                {
                    h.gameObject.SetActive(visible);
                }
            }
        }
    }
}
