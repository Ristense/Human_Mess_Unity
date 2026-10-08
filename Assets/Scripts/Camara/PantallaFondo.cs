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

    [Tooltip("La Render Texture que muestra este quad. Si la asignás, " +
        "el quad respeta SU proporción y se agranda hasta tapar la " +
        "pantalla, recortando lo que sobre. Vacío = se estira al " +
        "aspecto de la cámara, que deforma la imagen si la textura no " +
        "tiene la misma proporción que la ventana.")]
    [SerializeField] private Texture contenido;

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

        // Sin textura asignada: estirar y listo (lo de siempre).
        // Con textura: "cover", como el background-size de CSS. La
        // imagen conserva su proporción y crece hasta que no quede
        // ningún borde a la vista; lo que se pase, se recorta.
        //
        // Estirar sería "fill", y es lo que achataba el planeta cuando
        // la Render Texture era cuadrada y la ventana 16:9.
        if (contenido != null && contenido.height > 0)
        {
            float aspectoTex = (float)contenido.width / contenido.height;
            if (aspectoTex > camara.aspect)
            {
                // la textura es más panorámica: calza en alto y sobra a los lados
                ancho = alto * aspectoTex;
            }
            else
            {
                // más alta: calza en ancho y sobra arriba y abajo
                alto = ancho / aspectoTex;
            }
        }

        transform.localScale = new Vector3(ancho * margen, alto * margen, 1f);
    }
}
