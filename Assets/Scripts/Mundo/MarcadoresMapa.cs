using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pone un sprite en ciertas esquinas del mapa: las del borde de la
/// forma de esta partida, y las 4 de los sectores que elijas. Para
/// marcar dónde termina la zona.
///
/// Los puntos se calculan del Mapa cada vez, no están cargados a mano:
/// como la forma cambia en cada partida, los marcadores se van solos a
/// las esquinas que toquen.
///
/// En el editor dibuja los puntos aunque no tengas prefab todavía, así
/// ves dónde van a caer.
///
/// Va en cualquier objeto — el Mapa mismo es un buen lugar.
/// </summary>
public class MarcadoresMapa : MonoBehaviour
{
    [SerializeField] private Mapa mapa;

    [Tooltip("Lo que se pone en cada punto. Puede quedar vacío y solo " +
        "ves los gizmos.")]
    [SerializeField] private GameObject prefab;

    [Tooltip("Marcar las esquinas del borde del mapa. Con una forma " +
        "en L o en T salen más de 4: también las de adentro.")]
    [SerializeField] private bool esquinasDelMapa = true;

    [Tooltip("Sectores a los que también se les marcan las esquinas. " +
        "(0, 0) es el del centro.")]
    [SerializeField] private List<Vector2Int> sectores = new List<Vector2Int> { Vector2Int.zero };

    [Tooltip("Girar cada marcador para que apunte hacia afuera del " +
        "centro de lo que marca. Sirve si el sprite es una esquinera " +
        "o una flecha; si es simétrico, da igual.")]
    [SerializeField] private bool rotarHaciaAfuera = false;

    [Tooltip("Corrección de giro si el sprite no está dibujado mirando " +
        "a la derecha.")]
    [SerializeField] private float offsetRotacion = 0f;

    [Header("Gizmos")]
    [SerializeField] private Color colorGizmo = new Color(1f, 0.3f, 0.3f, 0.9f);
    [SerializeField] private float tamanoGizmo = 1.5f;

    private void Start()
    {
        if (mapa == null || prefab == null)
        {
            return;
        }

        foreach (Punto p in Puntos())
        {
            Quaternion giro = Quaternion.identity;
            if (rotarHaciaAfuera)
            {
                Vector2 d = p.posicion - p.centro;
                float grados = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg + offsetRotacion;
                giro = Quaternion.Euler(0f, 0f, grados);
            }
            Instantiate(prefab, p.posicion, giro, transform);
        }
    }

    private struct Punto
    {
        public Vector2 posicion;
        // el centro de lo que esta esquina marca, para saber cuál es
        // "afuera" al girarla
        public Vector2 centro;
    }

    private List<Punto> Puntos()
    {
        var lista = new List<Punto>();

        if (esquinasDelMapa)
        {
            foreach (Mapa.Esquina e in mapa.EsquinasContorno())
            {
                Agregar(lista, e.posicion, e.adentro);
            }
        }

        foreach (Vector2Int s in sectores)
        {
            // un sector apagado en esta partida no existe: no se marca
            if (!mapa.Activo(s))
            {
                continue;
            }
            Vector2 c = mapa.SectorCenterWorld(s);
            foreach (Vector2 e in mapa.EsquinasSector(s))
            {
                Agregar(lista, e, c);
            }
        }
        return lista;
    }

    /// <summary>
    /// Agrega sin repetir: dos sectores vecinos comparten esquina, y si
    /// no se filtra quedan dos marcadores encimados en el mismo lugar.
    /// </summary>
    private static void Agregar(List<Punto> lista, Vector2 pos, Vector2 centro)
    {
        foreach (Punto p in lista)
        {
            if ((p.posicion - pos).sqrMagnitude < 0.0001f)
            {
                return;
            }
        }
        lista.Add(new Punto { posicion = pos, centro = centro });
    }

    private void OnDrawGizmos()
    {
        if (mapa == null)
        {
            return;
        }
        Gizmos.color = colorGizmo;
        foreach (Punto p in Puntos())
        {
            Gizmos.DrawWireSphere(p.posicion, tamanoGizmo);
        }
    }
}
