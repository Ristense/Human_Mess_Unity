using UnityEngine;

/// <summary>
/// Hace que la cámara del fondo 3D acompañe el zoom de la cámara del
/// juego. Sin esto, al alejarte el quad crece en pantalla pero la
/// imagen del planeta adentro sigue del mismo tamaño — y se le ven
/// los bordes del cuadro.
///
/// Va en la cámara 3D (la que renderiza a la Render Texture).
/// </summary>
[ExecuteAlways]
public class SincronizarZoom3D : MonoBehaviour
{
    [Tooltip("La cámara del juego, la que maneja el zoom.")]
    [SerializeField] private Camera camaraJuego;

    [Tooltip("Cuánto acompaña. 1 = se aleja igual que vos. Menos que " +
        "1 = se aleja menos (se siente más lejano, que es lo normal " +
        "para un planeta). 0 = no acompaña nada.")]
    [Range(0f, 1f)]
    [SerializeField] private float intensidad = 0.6f;

    private Camera _propia;
    private float _fovBase3D;
    private float _fovBaseJuego;
    private bool _listo;

    private void OnEnable()
    {
        _propia = GetComponent<Camera>();
        _listo = false;
    }

    private void LateUpdate()
    {
        if (_propia == null || camaraJuego == null)
        {
            return;
        }

        // La primera vez guardamos los dos valores de arranque: a
        // partir de ahí todo se calcula como proporción respecto de
        // ese estado inicial, así no importa con qué números empieces.
        if (!_listo)
        {
            _fovBase3D = _propia.fieldOfView;
            _fovBaseJuego = ZoomDe(camaraJuego);
            _listo = true;
            return;
        }

        if (_fovBaseJuego <= 0.0001f)
        {
            return;
        }

        float proporcion = ZoomDe(camaraJuego) / _fovBaseJuego;
        // con intensidad < 1, el cambio se amortigua en vez de copiarse tal cual
        float aplicada = Mathf.Lerp(1f, proporcion, intensidad);
        _propia.fieldOfView = Mathf.Clamp(_fovBase3D * aplicada, 1f, 179f);
    }

    /// <summary>
    /// "Cuánto mundo ve" la cámara del juego, sirva ortográfica o en
    /// perspectiva — en una es el Orthographic Size, en la otra el FOV.
    /// </summary>
    private static float ZoomDe(Camera c)
    {
        return c.orthographic ? c.orthographicSize : c.fieldOfView;
    }
}
