using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// La cola de la ventana, como la de un globo de cómic: un triángulo con
/// la PUNTA en la mano y la BASE pegada al borde de la ventana. Se
/// rearma cada frame, así que si la mano se mueve el triángulo se
/// estira, se angosta y se ladea solo.
///
/// Es un componente de UI más (como una Image): tiene Color y Material,
/// así que el neón se le pone igual que a cualquier otra cosa del HUD.
///
/// Las dos esquinas de la base son dos objetos vacíos que acomodás a
/// mano sobre el borde de la ventana. La punta sigue a la mano, más un
/// corrimiento en píxeles para dejarla justo donde la quieras.
///
/// Va en un objeto del Canvas (sin Image), por ejemplo hijo de la ventana.
/// </summary>
[ExecuteAlways]   // para verla y acomodarla en el editor sin darle Play
[RequireComponent(typeof(CanvasRenderer))]
public class ColaVentana : MaskableGraphic
{
    [Header("Punta")]
    [Tooltip("Lo que sigue la punta: la Mano, en el mundo.")]
    [SerializeField] private Transform objetivo;

    [Tooltip("La cámara del juego. Vacío = Camera.main.")]
    [SerializeField] private Camera camara;

    [Tooltip("Corrimiento de la punta en PÍXELES de pantalla, para " +
        "acomodarla justo donde va en la mano.")]
    [SerializeField] private Vector2 desplazamientoPunta;

    [Tooltip("Opcional: un objeto del Canvas que hace de punta cuando no " +
        "hay objetivo (por ejemplo para verla y acomodarla en el editor " +
        "sin darle Play).")]
    [SerializeField] private RectTransform puntaEditor;

    [Header("Base")]
    [Tooltip("Esquina de un lado de la base, sobre el borde de la ventana.")]
    [SerializeField] private RectTransform baseA;

    [Tooltip("Esquina del otro lado.")]
    [SerializeField] private RectTransform baseB;

    [Header("Dibujo")]
    [Tooltip("Opcional: una textura para el triángulo. La punta toma la " +
        "parte de arriba al medio y la base el borde de abajo.")]
    [SerializeField] private Sprite sprite;

    public override Texture mainTexture =>
        sprite != null ? sprite.texture : s_WhiteTexture;

    protected override void Awake()
    {
        base.Awake();
        // la cola no tiene que tapar clics: es solo dibujo
        raycastTarget = false;
    }

    private void LateUpdate()
    {
        // la mano se mueve todo el tiempo: hay que rearmar el triángulo
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (baseA == null || baseB == null)
        {
            return;
        }
        if (!Punta(out Vector2 punta))
        {
            return;
        }

        Vector2 a = rectTransform.InverseTransformPoint(baseA.position);
        Vector2 b = rectTransform.InverseTransformPoint(baseB.position);

        // UV de un sprite que puede venir de una hoja con varios: la punta
        // arriba al medio, la base abajo de punta a punta
        Vector4 uv = sprite != null
            ? UnityEngine.Sprites.DataUtility.GetOuterUV(sprite)
            : new Vector4(0f, 0f, 1f, 1f);

        Color32 c = color;
        vh.AddVert(punta, c, new Vector2((uv.x + uv.z) * 0.5f, uv.w));
        vh.AddVert(a, c, new Vector2(uv.x, uv.y));
        vh.AddVert(b, c, new Vector2(uv.z, uv.y));
        vh.AddTriangle(0, 1, 2);
    }

    /// <summary>La punta, en coordenadas locales de este objeto.</summary>
    private bool Punta(out Vector2 local)
    {
        local = Vector2.zero;

        Camera cam = camara != null ? camara : Camera.main;
        if (objetivo != null && cam != null)
        {
            Vector2 pantalla = (Vector2)cam.WorldToScreenPoint(objetivo.position)
                + desplazamientoPunta;
            Camera camCanvas = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform, pantalla, camCanvas, out local);
        }

        if (puntaEditor != null)
        {
            local = rectTransform.InverseTransformPoint(puntaEditor.position);
            return true;
        }
        return false;
    }
}
