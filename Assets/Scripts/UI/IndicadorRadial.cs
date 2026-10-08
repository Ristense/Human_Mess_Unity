using UnityEngine;

/// <summary>
/// Le pasa el progreso de la Mano al shader RadialProgreso.
///
/// Usa un MaterialPropertyBlock y no material.SetFloat: así no se
/// crea una copia del material por cada indicador en escena, y no se
/// rompe el batching. Importa si algún día hay varios aros a la vez
/// (un socket por zona, por ejemplo).
///
/// Va en el objeto que tiene el Renderer del aro.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class IndicadorRadial : MonoBehaviour
{
    [Tooltip("De dónde sale el progreso.")]
    [SerializeField] private Mano mano;

    [Tooltip("Cómo se llama la propiedad en el shader.")]
    [SerializeField] private string propiedad = "_Progreso";

    [Tooltip("Segundos que tarda en alcanzar el valor real. 0 = sin " +
        "suavizado. Un poquito evita el parpadeo si rozás la tecla.")]
    [SerializeField] private float suavizado = 0.06f;

    [Tooltip("Esconder el aro cuando no hay nada en curso.")]
    [SerializeField] private bool esconderEnReposo = true;

    private Renderer _render;
    private MaterialPropertyBlock _bloque;
    private int _id;
    private float _mostrado;
    private float _velocidad;

    private void Awake()
    {
        _render = GetComponent<Renderer>();
        _bloque = new MaterialPropertyBlock();
        // el shader se busca por ID numérico, no por string, en cada
        // frame: resolver el nombre es lo caro
        _id = Shader.PropertyToID(propiedad);
    }

    private void LateUpdate()
    {
        if (mano == null)
        {
            return;
        }

        float objetivo = mano.Progreso;
        _mostrado = suavizado > 0f
            ? Mathf.SmoothDamp(_mostrado, objetivo, ref _velocidad, suavizado)
            : objetivo;

        if (esconderEnReposo)
        {
            bool visible = _mostrado > 0.001f;
            if (_render.enabled != visible)
            {
                _render.enabled = visible;
            }
            if (!visible)
            {
                return;
            }
        }

        _render.GetPropertyBlock(_bloque);
        _bloque.SetFloat(_id, _mostrado);
        _render.SetPropertyBlock(_bloque);
    }
}
