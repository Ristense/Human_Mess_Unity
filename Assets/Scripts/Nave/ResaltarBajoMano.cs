using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Le pone contorno a lo que está bajo la mano: lo que agarrarías si
/// hicieras clic ahora. Mientras lo tenés agarrado, el contorno sigue
/// encima de eso, con otro color.
///
/// No le cambia el material al sprite: le cuelga un hijo con una copia
/// del sprite y el shader Contorno, que dibuja solo el borde. Así el
/// original conserva su material y su iluminación 2D. El hijo se crea
/// la primera vez que hace falta y después solo se prende y apaga.
///
/// Va en el mismo objeto que la Mano.
/// </summary>
public class ResaltarBajoMano : MonoBehaviour
{
    [SerializeField] private Mano mano;

    [Tooltip("Un material con el shader HumanMess/Contorno.")]
    [SerializeField] private Material materialContorno;

    [Tooltip("Color mientras solo pasás la mano por encima.")]
    [SerializeField] private Color colorEncima = new Color(1f, 1f, 1f, 0.8f);

    [Tooltip("Color mientras lo tenés agarrado.")]
    [SerializeField] private Color colorAgarrado = new Color(1f, 0.8f, 0.2f, 1f);

    [Tooltip("Cuántos lugares por encima del sprite original se dibuja " +
        "el contorno, en Order in Layer.")]
    [SerializeField] private int ordenExtra = 1;

    [Tooltip("Cuánto más grande que el sprite es el rectángulo donde se " +
        "dibuja el contorno. El contorno va AFUERA del dibujo, y si el " +
        "dibujo llega al borde del PNG no hay lugar. 1.3 = 30% de aire " +
        "alrededor. Subilo si el contorno sale cortado en los bordes.")]
    [Range(1f, 2f)]
    [SerializeField] private float expansion = 1.3f;

    private MaterialPropertyBlock _bloque;
    private static readonly int IdRect = Shader.PropertyToID("_RectUV");
    private static readonly int IdExpansion = Shader.PropertyToID("_Expansion");

    // sprite original -> su renderer de contorno
    private readonly Dictionary<SpriteRenderer, SpriteRenderer> _contornos =
        new Dictionary<SpriteRenderer, SpriteRenderer>();

    private SpriteRenderer _encendido;

    private void Awake()
    {
        if (mano == null)
        {
            mano = GetComponent<Mano>();
        }
    }

    private void LateUpdate()
    {
        if (mano == null || materialContorno == null)
        {
            return;
        }

        bool agarrando = mano.Agarrado != null;
        Rigidbody2D objetivo = agarrando ? mano.Agarrado : mano.Candidato();

        SpriteRenderer original = objetivo != null
            ? objetivo.GetComponentInChildren<SpriteRenderer>()
            : null;
        SpriteRenderer contorno = original != null ? ContornoDe(original) : null;

        if (contorno != _encendido)
        {
            if (_encendido != null)
            {
                _encendido.enabled = false;
            }
            _encendido = contorno;
        }

        if (_encendido == null)
        {
            return;
        }

        // Se copia todo cada frame y no solo al crearlo: si el sprite
        // cambia (una animación, una herramienta que se prende), el
        // contorno tiene que acompañar.
        _encendido.sprite = original.sprite;
        _encendido.flipX = original.flipX;
        _encendido.flipY = original.flipY;
        _encendido.drawMode = original.drawMode;
        _encendido.size = original.size;
        _encendido.sortingLayerID = original.sortingLayerID;
        _encendido.sortingOrder = original.sortingOrder + ordenExtra;
        _encendido.color = agarrando ? colorAgarrado : colorEncima;
        Expandir(_encendido, original.sprite);
        _encendido.enabled = true;
    }

    /// <summary>
    /// Agranda el renderer del contorno y le dice al shader cuánto, para
    /// que achique la imagen adentro en la misma proporción. El dibujo
    /// queda igual, pero con lugar alrededor para el anillo.
    /// </summary>
    private void Expandir(SpriteRenderer c, Sprite s)
    {
        if (s == null)
        {
            return;
        }

        // Con empaquetado Tight el sprite no tiene un rectángulo limpio
        // en la textura y no se puede calcular el recorte. Ahí se
        // dibuja sin expandir, que es mejor que dibujar mal.
        float e = expansion;
        Vector4 rectUV = new Vector4(0f, 0f, 1f, 1f);
        if (s.packed && s.packingMode == SpritePackingMode.Tight)
        {
            e = 1f;
        }
        else
        {
            Rect r = s.textureRect;
            Texture t = s.texture;
            rectUV = new Vector4(r.x / t.width, r.y / t.height,
                                 r.width / t.width, r.height / t.height);
        }

        // Escalar agranda alrededor del PIVOT del sprite, pero el shader
        // achica alrededor del CENTRO. Si el pivot no está al centro, se
        // desalinean y el contorno queda corrido. Este corrimiento hace
        // que el agrandado también sea alrededor del centro.
        Vector3 centro = s.bounds.center;
        c.transform.localScale = new Vector3(e, e, 1f);
        c.transform.localPosition = new Vector3(centro.x * (1f - e), centro.y * (1f - e), 0f);

        if (_bloque == null)
        {
            _bloque = new MaterialPropertyBlock();
        }
        c.GetPropertyBlock(_bloque);
        _bloque.SetVector(IdRect, rectUV);
        _bloque.SetFloat(IdExpansion, e);
        c.SetPropertyBlock(_bloque);
    }

    private SpriteRenderer ContornoDe(SpriteRenderer original)
    {
        if (_contornos.TryGetValue(original, out SpriteRenderer c) && c != null)
        {
            return c;
        }

        // Hijo sin desplazamiento ni escala propia: así hereda la
        // posición, la rotación y la escala del original sin hacer
        // ninguna cuenta.
        GameObject go = new GameObject("Contorno");
        go.layer = original.gameObject.layer;
        go.transform.SetParent(original.transform, false);

        c = go.AddComponent<SpriteRenderer>();
        c.sharedMaterial = materialContorno;
        c.enabled = false;

        _contornos[original] = c;
        return c;
    }

    private void OnDisable()
    {
        if (_encendido != null)
        {
            _encendido.enabled = false;
            _encendido = null;
        }
    }
}
