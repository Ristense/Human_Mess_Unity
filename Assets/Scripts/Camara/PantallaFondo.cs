using UnityEngine;

/// <summary>
/// Mantiene este Quad tapando EXACTAMENTE lo que ve la cámara, a una
/// distancia fija — se reescala solo cuando cambia el zoom, el FOV o
/// el aspecto de la ventana. Sin esto, al alejar el zoom se le ven
/// los bordes al quad.
///
/// Va en el Quad, que tiene que ser HIJO de la cámara.
/// </summary>
[ExecuteAlways]
public class PantallaFondo : MonoBehaviour
{
    [Tooltip("La cámara que lo mira. Si está vacío, usa la del padre.")]
    [SerializeField] private Camera camara;

    [Tooltip("A qué distancia de la cámara se planta. Tiene que estar " +
        "entre el Near y el Far, y más lejos que todo lo que quieras " +
        "ver por delante.")]
    [SerializeField] private float distancia = 50f;

    [Tooltip("Un poquito más grande que lo justo, por si acaso.")]
    [SerializeField] private float margen = 1.02f;

    private void LateUpdate()
    {
        if (camara == null)
        {
            camara = GetComponentInParent<Camera>();
            if (camara == null)
            {
                return;
            }
        }

        transform.localPosition = new Vector3(0f, 0f, distancia);
        transform.localRotation = Quaternion.identity;

        float alto;
        if (camara.orthographic)
        {
            // en ortográfica el encuadre no depende de la distancia
            alto = camara.orthographicSize * 2f;
        }
        else
        {
            // en perspectiva, lo que entra a cierta distancia sale de
            // la mitad del FOV vertical: alto = 2 · d · tan(fov/2)
            alto = 2f * distancia * Mathf.Tan(camara.fieldOfView * 0.5f * Mathf.Deg2Rad);
        }

        float ancho = alto * camara.aspect;
        transform.localScale = new Vector3(ancho * margen, alto * margen, 1f);
    }
}
