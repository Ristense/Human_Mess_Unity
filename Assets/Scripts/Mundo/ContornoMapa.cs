using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dibuja el borde del mapa con LineRenderer: una línea cerrada por
/// cada vuelta del contorno.
///
/// Solo el borde de afuera. Las paredes entre dos sectores prendidos no
/// se dibujan, porque ahí no termina nada — el mundo sigue. Eso lo
/// resuelve Mapa.ContornoBorde(); acá solo se dibuja lo que devuelve.
///
/// Son varias líneas y no una sola porque la forma puede tener más de
/// una vuelta (un hueco adentro es su propio contorno), y un
/// LineRenderer dibuja una polilínea sola: metiendo todo junto
/// quedarían tramos uniendo vueltas que no se tocan.
///
/// Va en el mismo objeto que el Mapa, o en cualquiera si le arrastrás
/// el Mapa.
/// </summary>
public class ContornoMapa : MonoBehaviour
{
    [Tooltip("El mapa que se delimita. Vacío = lo busca en este objeto " +
        "y después en la escena.")]
    [SerializeField] private Mapa mapa;

    [Tooltip("El material de la línea. El Neon_Blanco queda bien: con " +
        "HDR lo agarra el Bloom y el borde brilla.")]
    [SerializeField] private Material material;

    [Tooltip("Grosor de la línea, en unidades de mundo. Ojo que los " +
        "sectores son grandes: con 32x18 por sector, 0.1 ya se ve fino.")]
    [Min(0.001f)]
    [SerializeField] private float grosor = 0.15f;

    [SerializeField] private Color color = new Color(0.4f, 0.8f, 1f, 0.7f);

    [Tooltip("Z en la que se dibuja. Negativo la trae hacia la cámara.")]
    [SerializeField] private float z = 0f;

    [Tooltip("En qué capa de orden se dibuja, como cualquier sprite.")]
    [SerializeField] private string capaOrden = "Default";

    [SerializeField] private int ordenEnCapa = 0;

    private readonly List<LineRenderer> _lineas = new List<LineRenderer>();
    private int _semilla;
    private bool _dibujado;

    private void Start()
    {
        // En Start y no en Awake: el Mapa arma la forma en su Awake, y
        // acá ya tiene que estar hecha para poder recorrerla.
        if (mapa == null)
        {
            mapa = GetComponent<Mapa>();
        }
        if (mapa == null)
        {
            mapa = FindAnyObjectByType<Mapa>();
        }
        Dibujar();
    }

    private void LateUpdate()
    {
        // Si el mapa se regeneró (otra partida sin recargar la escena),
        // el borde viejo es de una forma que ya no existe.
        if (mapa != null && (!_dibujado || mapa.Semilla != _semilla))
        {
            Dibujar();
        }
    }

    /// <summary>Rehace el borde. Llamalo si cambiaste el mapa a mano.</summary>
    public void Dibujar()
    {
        if (mapa == null)
        {
            return;
        }
        _semilla = mapa.Semilla;
        _dibujado = true;

        List<List<Vector2>> vueltas = mapa.ContornoBorde();

        // Los LineRenderer se reusan: solo se crean los que falten y se
        // apagan los que sobren. Una forma nueva cada partida no tiene
        // por qué dejar basura para el GC.
        for (int i = 0; i < vueltas.Count; i++)
        {
            LineRenderer l = Linea(i);
            List<Vector2> puntos = vueltas[i];
            l.positionCount = puntos.Count;
            for (int p = 0; p < puntos.Count; p++)
            {
                l.SetPosition(p, new Vector3(puntos[p].x, puntos[p].y, z));
            }
            l.enabled = puntos.Count > 2;
        }

        for (int i = vueltas.Count; i < _lineas.Count; i++)
        {
            _lineas[i].enabled = false;
        }
    }

    private LineRenderer Linea(int i)
    {
        while (_lineas.Count <= i)
        {
            var go = new GameObject("Borde " + _lineas.Count);
            go.transform.SetParent(transform, false);
            _lineas.Add(go.AddComponent<LineRenderer>());
        }

        LineRenderer l = _lineas[i];
        // en mundo: los puntos vienen en mundo, así que el objeto no
        // tiene que meter su transform en el medio
        l.useWorldSpace = true;
        l.loop = true;
        l.sharedMaterial = material;
        l.startWidth = l.endWidth = grosor;
        l.startColor = l.endColor = color;
        l.numCornerVertices = 2;
        // una línea de esquinas rectas no necesita suavizado, y así no
        // se redondean los vértices del mapa
        l.numCapVertices = 0;
        l.sortingLayerName = capaOrden;
        l.sortingOrder = ordenEnCapa;
        l.enabled = true;
        return l;
    }

    private void OnValidate()
    {
        // tocás el grosor o el color en el Inspector con el juego
        // andando y se ve al toque
        if (Application.isPlaying && _dibujado)
        {
            Dibujar();
        }
    }
}
