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
    [Tooltip("Si la asignás, la barra muestra la carga de la tecla de " +
        "la mano. Dejala VACÍA para usarla para otra cosa: ahí el " +
        "valor se lo pasás vos desde cualquier script con " +
        "barra.Valor = 0.4f (de 0 a 1).")]
    [SerializeField] private Mano mano;

    /// <summary>
    /// Cuánto se muestra, de 0 a 1. Se puede escribir desde afuera
    /// cuando no hay una Mano asignada — así la misma barra sirve
    /// para combustible, carga de la bodega, integridad, lo que sea.
    /// </summary>
    public float Valor { get; set; }

    [Tooltip("El sprite que se estira.")]
    [SerializeField] private Transform relleno;

    /// <summary>El sprite que se estira, para quien quiera pintarlo.</summary>
    public Transform Relleno => relleno;

    [Tooltip("En qué eje crece. El PIVOT del sprite tiene que estar " +
        "del lado por donde arranca: Left para horizontal, Bottom " +
        "para vertical. Si está al centro crece para los dos lados y " +
        "queda como una barra de ecualizador.")]
    [SerializeField] private Eje eje = Eje.Horizontal;

    public enum Eje
    {
        Horizontal,
        Vertical,
    }

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

            // Si quedó guardada vacía (escala 0 en el eje que crece), toda
            // cuenta daría 0 para siempre y la barra no subiría nunca. Ahí
            // se toma 1 como tamaño lleno.
            if (eje == Eje.Vertical && Mathf.Approximately(_escalaBase.y, 0f))
            {
                _escalaBase.y = 1f;
            }
            else if (eje == Eje.Horizontal && Mathf.Approximately(_escalaBase.x, 0f))
            {
                _escalaBase.x = 1f;
            }
        }
    }

    private void LateUpdate()
    {
        if (relleno == null)
        {
            return;
        }

        float objetivo = Mathf.Clamp01(mano != null ? mano.Progreso : Valor);
        _mostrado = suavizado > 0f
            ? Mathf.SmoothDamp(_mostrado, objetivo, ref _velocidad, suavizado)
            : objetivo;

        // Se escala RELATIVO a la escala que le pusiste en el editor,
        // así el grosor de la barra queda como vos lo dejaste y solo
        // cambia el largo.
        Vector3 e = _escalaBase;
        if (eje == Eje.Vertical)
        {
            e.y = _escalaBase.y * _mostrado;
        }
        else
        {
            e.x = _escalaBase.x * _mostrado;
        }
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
