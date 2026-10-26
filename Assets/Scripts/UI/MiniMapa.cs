using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// La grilla chica del HUD: el Mapa calcado, con un punto donde estás.
///
/// No hay nada dibujado a mano: las celdas se arman leyendo el Mapa al
/// arrancar. Como la forma cambia en cada partida, la grilla la copia
/// sola — un sector apagado sale apagado (o no sale), y el inicial se
/// marca aparte porque es donde está la base.
///
/// El punto va al centro del sector donde estás, y ese sector además
/// se pinta distinto, para leerlo de reojo. Solo se mueve al cambiar de
/// sector.
///
/// Las celdas respetan la proporción de los sectores (32x18 no es un
/// cuadrado) y se acomodan al tamaño del rectángulo donde va esto: lo
/// agrandás en el Canvas y la grilla lo llena sola.
///
/// Va en un objeto del Canvas con RectTransform. Ese rectángulo es el
/// marco del minimapa.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class MiniMapa : MonoBehaviour
{
    [Tooltip("El mapa que se calca. Vacío = lo busca en la escena.")]
    [SerializeField] private Mapa mapa;

    [Tooltip("Lo que marca el punto. Vacío = la nave.")]
    [SerializeField] private Transform objetivo;

    [Header("Celdas")]
    [Tooltip("Sprite de cada sector. Vacío = un cuadrado liso.")]
    [SerializeField] private Sprite spriteCelda;

    [Tooltip("Espacio entre celdas, en píxeles del Canvas.")]
    [Min(0f)]
    [SerializeField] private float separacion = 2f;

    [SerializeField] private Color colorActivo = new Color(0.4f, 0.8f, 1f, 0.35f);

    [Tooltip("Los sectores que no existen en esta partida. Con alfa 0 " +
        "no se dibujan y la grilla toma la forma del mapa; con un poco " +
        "de alfa se ve el molde entero con los huecos.")]
    [SerializeField] private Color colorApagado = new Color(0.4f, 0.8f, 1f, 0.05f);

    [Tooltip("El sector de la base.")]
    [SerializeField] private Color colorInicial = new Color(0.3f, 1f, 0.5f, 0.45f);

    [Tooltip("El sector donde estás ahora. Tapa a los otros colores.")]
    [SerializeField] private Color colorActual = new Color(0.4f, 0.8f, 1f, 0.7f);

    [Header("Punto")]
    [Tooltip("Tu propio punto (una Image en el Canvas). Vacío = se crea " +
        "uno con el sprite y el color de abajo.")]
    [SerializeField] private RectTransform punto;

    [SerializeField] private Sprite spritePunto;

    [SerializeField] private Color colorPunto = Color.white;

    [Tooltip("Tamaño del punto creado, en píxeles del Canvas.")]
    [Min(1f)]
    [SerializeField] private float tamanoPunto = 6f;

    private RectTransform _rt;
    private readonly Dictionary<Vector2Int, Image> _celdas = new Dictionary<Vector2Int, Image>();
    private Vector2Int _actual;
    private bool _hayActual;
    private int _semilla;
    private float _escala;
    private Rect _molde;

    private void Awake()
    {
        _rt = (RectTransform)transform;
    }

    private void Start()
    {
        // En Start y no en Awake: el Mapa arma la forma de la partida en
        // su Awake, y acá ya tiene que estar hecha para poder calcarla.
        if (mapa == null)
        {
            mapa = FindAnyObjectByType<Mapa>();
        }
        if (objetivo == null)
        {
            Nave nave = FindAnyObjectByType<Nave>();
            objetivo = nave != null ? nave.transform : null;
        }
        Construir();
    }

    // El rectángulo cambió de tamaño (resolución, o lo estiraste en el
    // Canvas): las celdas se reacomodan para seguir llenándolo.
    private void OnRectTransformDimensionsChange()
    {
        if (_rt != null && mapa != null && _celdas.Count > 0)
        {
            Acomodar();
        }
    }

    private void LateUpdate()
    {
        if (mapa == null)
        {
            return;
        }

        // Si el mapa se regeneró (otra partida sin recargar la escena),
        // la grilla vieja ya no es la forma de ahora.
        if (mapa.Semilla != _semilla)
        {
            Construir();
        }

        if (objetivo == null)
        {
            return;
        }

        Vector2Int s = mapa.WorldToSector(objetivo.position);
        if (!_hayActual || s != _actual)
        {
            // el punto va al centro de la celda: dice en qué sector
            // estás, no dónde adentro de él
            if (punto != null)
            {
                punto.anchoredPosition = AUi(mapa.SectorCenterWorld(s));
            }

            // Se repinta solo al cambiar de sector, no cada frame: es lo
            // único que cambia, y Image.color marca el Canvas para
            // reconstruir aunque le pongas el mismo color.
            Vector2Int antes = _actual;
            bool habia = _hayActual;
            _actual = s;
            _hayActual = true;
            if (habia)
            {
                Pintar(antes);
            }
            Pintar(_actual);
        }
    }

    /// <summary>Arma las celdas y el punto desde cero, con la forma de esta partida.</summary>
    private void Construir()
    {
        if (mapa == null)
        {
            return;
        }
        _semilla = mapa.Semilla;
        _hayActual = false;

        foreach (Image img in _celdas.Values)
        {
            if (img != null)
            {
                Destroy(img.gameObject);
            }
        }
        _celdas.Clear();

        foreach (Vector2Int s in mapa.TodosLosSectores())
        {
            // Un sector apagado e invisible no se crea: no hay por qué
            // tener objetos que no dibujan nada.
            if (!mapa.Activo(s) && colorApagado.a <= 0f)
            {
                continue;
            }

            var go = new GameObject("Sector " + s.x + "," + s.y, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_rt, false);
            Image img = go.GetComponent<Image>();
            img.sprite = spriteCelda;
            // el minimapa es para mirar: que no se coma los clics del HUD
            img.raycastTarget = false;
            _celdas[s] = img;
            Pintar(s);
        }

        PrepararPunto();
        Acomodar();
    }

    private void PrepararPunto()
    {
        if (punto == null)
        {
            var go = new GameObject("Punto", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_rt, false);
            Image img = go.GetComponent<Image>();
            img.sprite = spritePunto;
            img.color = colorPunto;
            img.raycastTarget = false;
            punto = (RectTransform)go.transform;
            punto.sizeDelta = new Vector2(tamanoPunto, tamanoPunto);
        }
        Centrar(punto);
        // siempre por encima de las celdas, que se acaban de crear
        punto.SetAsLastSibling();
    }

    /// <summary>
    /// Calcula la escala mundo→Canvas y pone cada celda en su lugar.
    ///
    /// Una sola escala para los dos ejes: así el sector conserva su
    /// proporción y la grilla entra entera en el rectángulo, dejando
    /// margen en el lado que sobra en vez de deformarse.
    /// </summary>
    private void Acomodar()
    {
        _molde = mapa.WorldBounds();
        Rect marco = _rt.rect;
        _escala = _molde.width > 0f && _molde.height > 0f
            ? Mathf.Min(marco.width / _molde.width, marco.height / _molde.height)
            : 0f;

        Vector2 lado = mapa.SectorSize * _escala - Vector2.one * separacion;
        lado = Vector2.Max(lado, Vector2.zero);

        foreach (KeyValuePair<Vector2Int, Image> par in _celdas)
        {
            RectTransform c = par.Value.rectTransform;
            Centrar(c);
            c.sizeDelta = lado;
            c.anchoredPosition = AUi(mapa.SectorCenterWorld(par.Key));
        }

        // con la escala nueva, el centro de la celda quedó en otro lado
        if (_hayActual && punto != null)
        {
            punto.anchoredPosition = AUi(mapa.SectorCenterWorld(_actual));
        }
    }

    /// <summary>
    /// De mundo a Canvas: relativo al centro del molde, por la escala.
    ///
    /// Sale directo como anchoredPosition porque todo lo de adentro se
    /// ancla al CENTRO del marco (ver Centrar): ahí el (0, 0) es el medio
    /// del rectángulo, tenga el marco el pivot donde lo tenga.
    /// </summary>
    private Vector2 AUi(Vector2 mundo)
    {
        return (mundo - _molde.center) * _escala;
    }

    private static void Centrar(RectTransform r)
    {
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
    }

    private void Pintar(Vector2Int s)
    {
        if (!_celdas.TryGetValue(s, out Image img) || img == null)
        {
            return;
        }
        img.color = _hayActual && s == _actual ? colorActual
            : !mapa.Activo(s) ? colorApagado
            : s == mapa.SectorInicial ? colorInicial
            : colorActivo;
    }
}
