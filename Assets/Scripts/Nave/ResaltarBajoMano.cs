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

    [Header("Marca redonda")]
    [Tooltip("Un PNG que se dibuja encima de lo que está bajo la mano: " +
        "un aro, un círculo, una mira. Aparece y desaparece con el " +
        "mismo gatillo que el contorno.\n\n" +
        "Es para las piezas de forma rara, donde el contorno que sigue " +
        "el dibujo queda feo. Vacío = no se dibuja nada y todo sigue " +
        "igual que antes.")]
    [SerializeField] private Sprite marca;

    [Tooltip("Qué tan más grande que la pieza se dibuja la marca. 1 = " +
        "justo del tamaño de su caja; 1.2 le deja un poco de aire.")]
    [SerializeField] private float margenMarca = 1.2f;

    [Tooltip("Con qué material se dibuja la marca. Vacío = el default " +
        "de sprites, que recibe la luz 2D de la escena. Si querés que " +
        "se vea siempre con el mismo brillo, ponele uno Unlit.")]
    [SerializeField] private Material materialMarca;

    [Tooltip("Grados por segundo que gira la marca. Positivo = contra " +
        "reloj. En 0 queda quieta.")]
    [SerializeField] private float giroMarca = 25f;

    [Tooltip("Cuántos segundos tarda un ciclo completo del parpadeo. " +
        "Más alto = más lento. En 0 no parpadea.")]
    [SerializeField] private float cicloParpadeo = 3f;

    [Tooltip("Hasta dónde baja el alpha en lo más apagado del " +
        "parpadeo, como fracción del normal. 0.5 = nunca baja de la " +
        "mitad, así que no llega a apagarse del todo.")]
    [Range(0f, 1f)]
    [SerializeField] private float alphaMinimo = 0.5f;

    [Header("Rocas talladas")]
    [Tooltip("Una roca con cortes ya no tiene la forma de su sprite: su " +
        "contorno se dibuja con una línea que sigue la forma de verdad. " +
        "Este es el material de esa línea (el Neon_Blanco sirve). Vacío = " +
        "usa el contorno del sprite, como antes.")]
    [SerializeField] private Material materialLinea;

    [Tooltip("Para pasar el grosor y el margen de píxeles a unidades. " +
        "Vacío = Camera.main.")]
    [SerializeField] private Camera camara;

    private LineRenderer _linea;
    private SpriteRenderer _marca;
    private static readonly int IdGrosor = Shader.PropertyToID("_Grosor");
    private static readonly int IdMargen = Shader.PropertyToID("_Margen");

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
        if (mano == null)
        {
            return;
        }

        bool agarrando = mano.Agarrado != null;
        Rigidbody2D objetivo = agarrando ? mano.Agarrado : mano.Candidato();
        Color color = agarrando ? colorAgarrado : colorEncima;

        // La marca va primero y aparte: es independiente del contorno,
        // así podés usar una sin la otra. Antes esto estaba después del
        // guard de abajo, y vaciar materialContorno —que es justamente
        // lo que hacés si solo querés la marca— mataba las dos.
        DibujarMarca(objetivo, color);

        // Sin material de contorno no hay ni borde de sprite ni línea.
        // Se apaga lo que haya quedado prendido y se sale.
        if (materialContorno == null)
        {
            DibujarLinea(null, color);
            if (_encendido != null)
            {
                _encendido.enabled = false;
                _encendido = null;
            }
            return;
        }

        // tallada: línea por la forma real, y el contorno del sprite apagado
        Tallable tallada = objetivo != null && materialLinea != null
            ? objetivo.GetComponent<Tallable>()
            : null;
        if (tallada != null && !tallada.EstaTallado)
        {
            tallada = null;
        }
        DibujarLinea(tallada, color);

        SpriteRenderer original = objetivo != null && tallada == null
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

    /// <summary>
    /// Pone la marca redonda encima de lo que esté bajo la mano,
    /// centrada en su caja y escalada para cubrirla.
    ///
    /// Es UN solo renderer reusado, suelto en el mundo y no colgado de
    /// la pieza: como la marca es redonda no necesita heredar la
    /// rotación de nadie, y así no hay que crear un hijo por cada cosa
    /// que toques.
    /// </summary>
    private void DibujarMarca(Rigidbody2D objetivo, Color color)
    {
        Renderer r = objetivo != null && marca != null
            ? objetivo.GetComponentInChildren<Renderer>()
            : null;

        if (r == null)
        {
            if (_marca != null)
            {
                _marca.enabled = false;
            }
            return;
        }

        if (_marca == null)
        {
            var go = new GameObject("Marca bajo la mano");
            _marca = go.AddComponent<SpriteRenderer>();
        }

        // se asigna cada frame por si cambiás el sprite o el material
        // en el inspector con el juego andando
        _marca.sprite = marca;
        if (materialMarca != null)
        {
            _marca.sharedMaterial = materialMarca;
        }

        Bounds b = r.bounds;
        _marca.transform.position = new Vector3(b.center.x, b.center.y,
            r.transform.position.z);

        // El lado más largo de la pieza manda: así una pieza alargada
        // queda entera adentro de la marca en vez de asomar por las
        // puntas.
        float lado = Mathf.Max(b.size.x, b.size.y) * margenMarca;
        float propio = marca.bounds.size.x;
        float f = propio > 0.0001f ? lado / propio : 1f;
        _marca.transform.localScale = new Vector3(f, f, 1f);

        // Gira contra reloj: en 2D el Z positivo va en ese sentido. Sale
        // de Time.time y no de un acumulador propio, así no se desfasa
        // por los frames en los que la marca estuvo apagada.
        _marca.transform.rotation = giroMarca != 0f
            ? Quaternion.Euler(0f, 0f, Time.time * giroMarca)
            : Quaternion.identity;

        // Parpadeo: una onda suave entre alphaMinimo y el alpha normal.
        // Nunca llega a cero a propósito — si se apagara del todo
        // perderías de vista qué tenés debajo de la mano justo cuando
        // vas a hacer clic.
        if (cicloParpadeo > 0f)
        {
            float t = (Mathf.Sin(Time.time * Mathf.PI * 2f / cicloParpadeo) + 1f) * 0.5f;
            color.a *= Mathf.Lerp(alphaMinimo, 1f, t);
        }

        _marca.color = color;
        _marca.gameObject.layer = r.gameObject.layer;
        _marca.sortingLayerID = r.sortingLayerID;
        _marca.sortingOrder = r.sortingOrder + ordenExtra;
        _marca.enabled = true;
    }

    /// <summary>
    /// El contorno de una roca tallada: una línea cerrada que sigue su
    /// forma real, con el mismo margen y grosor (en píxeles de pantalla)
    /// que el contorno del shader.
    /// </summary>
    private void DibujarLinea(Tallable t, Color color)
    {
        if (t == null)
        {
            if (_linea != null)
            {
                _linea.enabled = false;
            }
            return;
        }

        if (_linea == null)
        {
            // suelta y no hija de la roca: va en coordenadas de mundo
            var go = new GameObject("Contorno (linea)");
            _linea = go.AddComponent<LineRenderer>();
            _linea.useWorldSpace = true;
            _linea.loop = true;
            _linea.numCornerVertices = 2;
            _linea.sharedMaterial = materialLinea;
        }

        Camera cam = camara != null ? camara : Camera.main;
        float porPixel = cam != null && cam.orthographic
            ? 2f * cam.orthographicSize / Screen.height
            : 0.002f;
        float grosor = materialContorno.GetFloat(IdGrosor) * porPixel;
        float margen = materialContorno.GetFloat(IdMargen) * porPixel;

        // el centro de la línea va a "margen + medio grosor" del borde
        List<Vector3> puntos = t.ContornoMundo(margen + grosor * 0.5f);
        _linea.positionCount = puntos.Count;
        _linea.SetPositions(puntos.ToArray());
        _linea.startWidth = _linea.endWidth = grosor;
        _linea.startColor = _linea.endColor = color;

        Renderer r = t.GetComponentInChildren<MeshRenderer>();
        if (r != null)
        {
            _linea.gameObject.layer = r.gameObject.layer;
            _linea.sortingLayerID = r.sortingLayerID;
            _linea.sortingOrder = r.sortingOrder + ordenExtra;
        }
        _linea.enabled = puntos.Count > 2;
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
        if (_linea != null)
        {
            _linea.enabled = false;
        }
        if (_marca != null)
        {
            _marca.enabled = false;
        }
    }
}
