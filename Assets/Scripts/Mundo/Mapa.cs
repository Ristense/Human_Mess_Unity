using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// El mapa: una grilla de sectores, igual que en la versión Godot. El
/// mundo es UNO SOLO y continuo — los sectores son una grilla LÓGICA
/// encima del espacio para saber dónde estás, qué spawnear en cada
/// lado y qué llevás limpiado. No son escenas separadas ni hay
/// transición entre uno y otro.
///
/// Las coordenadas van con signo y el centro es (0,0): en un 3x3 van
/// de (-1,-1) arriba-izquierda a (1,1) abajo-derecha. Así "el sector
/// de al lado" es sumar 1, sin corregir offsets, y donde arrancás es
/// siempre el cero.
///
/// OJO con el tamaño: sectorSize está en unidades de MUNDO (metros,
/// si laburás a la escala default de Unity), no en píxeles de
/// pantalla — así el mapa mide lo mismo sea cual sea tu resolución.
/// </summary>
public class Mapa : MonoBehaviour
{
    [SerializeField] private int cols = 3;
    [SerializeField] private int rows = 3;

    [Tooltip("Cuánto mide un sector en unidades de mundo.")]
    [SerializeField] private Vector2 sectorSize = new Vector2(32f, 18f);

    [Tooltip("Cambiá esto para volver a tirar los dados de todo el mapa de una.")]
    [SerializeField] private int worldSeed = 0;

    [Header("Gizmos (solo editor)")]
    [SerializeField] private bool dibujarGizmos = true;
    [SerializeField] private Color colorLinea = new Color(0.4f, 0.8f, 1f, 0.6f);
    [SerializeField] private Color colorSectorInicial = new Color(0.3f, 1f, 0.5f, 0.15f);

    public int Cols => cols;
    public int Rows => rows;
    public Vector2 SectorSize => sectorSize;

    /// <summary>Cuántos sectores hay a cada lado del centro. En un 3x3 da (1, 1).</summary>
    private Vector2Int Half()
    {
        return new Vector2Int((cols - 1) / 2, (rows - 1) / 2);
    }

    /// <summary>
    /// El rectángulo del mundo entero, en coordenadas globales. Esto
    /// es lo que Nave.cs usa para no dejarte salir del mapa.
    /// </summary>
    public Rect WorldBounds()
    {
        Vector2 size = new Vector2(cols, rows) * sectorSize;
        Vector2 origen = (Vector2)transform.position - size * 0.5f;
        return new Rect(origen, size);
    }

    /// <summary>El centro en mundo del sector (sx, sy).</summary>
    public Vector2 SectorCenterWorld(Vector2Int s)
    {
        return (Vector2)transform.position + new Vector2(s.x, s.y) * sectorSize;
    }

    /// <summary>
    /// En qué sector cae un punto del mundo. Se recorta a la grilla:
    /// un punto afuera devuelve el sector del borde más cercano en vez
    /// de una coordenada que no existe.
    /// </summary>
    public Vector2Int WorldToSector(Vector2 worldPos)
    {
        Vector2 rel = (worldPos - (Vector2)transform.position);
        rel.x /= sectorSize.x;
        rel.y /= sectorSize.y;
        Vector2Int half = Half();
        int sx = Mathf.Clamp(Mathf.RoundToInt(rel.x), -half.x, half.x);
        int sy = Mathf.Clamp(Mathf.RoundToInt(rel.y), -half.y, half.y);
        return new Vector2Int(sx, sy);
    }

    /// <summary>Todos los sectores, para recorrerlos con un foreach.</summary>
    public List<Vector2Int> AllSectors()
    {
        var salida = new List<Vector2Int>();
        Vector2Int half = Half();
        for (int y = -half.y; y <= half.y; y++)
        {
            for (int x = -half.x; x <= half.x; x++)
            {
                salida.Add(new Vector2Int(x, y));
            }
        }
        return salida;
    }

    /// <summary>
    /// Semilla estable de un sector: la misma coordenada da siempre el
    /// mismo número, así el contenido de un sector se puede regenerar
    /// solo sin guardarlo en ningún archivo.
    /// </summary>
    public int SectorSeed(Vector2Int s)
    {
        // Un hash simple y determinístico — no necesita coincidir con
        // el de Godot, solo necesita dar siempre el MISMO número para
        // la misma coordenada.
        int h = s.x * 73856093 ^ s.y * 19349663;
        return h ^ worldSeed;
    }

    private void OnDrawGizmos()
    {
        if (!dibujarGizmos)
        {
            return;
        }
        foreach (Vector2Int s in AllSectors())
        {
            Vector3 centro = SectorCenterWorld(s);
            Vector3 size = new Vector3(sectorSize.x, sectorSize.y, 0f);

            if (s == Vector2Int.zero)
            {
                Gizmos.color = colorSectorInicial;
                Gizmos.DrawCube(centro, size);
            }

            Gizmos.color = colorLinea;
            Gizmos.DrawWireCube(centro, size);
        }
    }
}
