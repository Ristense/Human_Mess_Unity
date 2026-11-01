using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lo que se escribe en el archivo de la partida guardada. Son clases
/// pelada: solo datos, sin nada de lógica, porque JsonUtility de Unity
/// solo sabe guardar campos públicos de clases marcadas Serializable.
///
/// Ojo con JsonUtility: no sabe guardar arreglos de arreglos. Por eso
/// los contornos de una roca van aplanados — todos los puntos en una
/// lista, y aparte cuántos puntos tiene cada contorno.
/// </summary>
[Serializable]
public class Partida
{
    [Tooltip("Por si algún día cambia el formato: un archivo viejo se " +
        "reconoce por acá en vez de romperse en silencio.")]
    public int version = 1;

    public int semillaMapa;
    public int sectorPlaneta;

    public DatosNave nave = new DatosNave();
    public DatosBodega bodega = new DatosBodega();
    public DatosSoporte soporte = new DatosSoporte();
    public List<DatosRoca> rocas = new List<DatosRoca>();
}

[Serializable]
public class DatosNave
{
    public Vector3 posicion;
    public float giro;
    public Vector2 velocidad;
    public float velocidadAngular;
}

[Serializable]
public class DatosBodega
{
    public float carga;
    public float creditos;
    public int placas;
    public float kgPlacas;
    public float kgHielo;
    public float kgRestos;
}

[Serializable]
public class DatosSoporte
{
    public float kgHidrogeno;
    public float kgOxigeno;
}

[Serializable]
public class DatosRoca
{
    [Tooltip("De qué prefab salió, para poder volver a crearla.")]
    public string id;

    public Vector3 posicion;
    public float giro;
    public Vector3 escala;
    public Vector2 velocidad;
    public float velocidadAngular;

    public float densidad;
    public float areaOriginal;
    public int material;

    [Tooltip("Si se le llegó a sacar material. Una roca entera no " +
        "necesita que le devolvamos la forma: ya nace así.")]
    public bool tallada;

    [Tooltip("Cuántos puntos tiene cada contorno.")]
    public List<int> largos = new List<int>();

    [Tooltip("Los puntos de todos los contornos, uno detrás del otro.")]
    public List<Vector2> puntos = new List<Vector2>();

    /// <summary>Parte la lista aplanada en los contornos que eran.</summary>
    public List<Vector2[]> Forma()
    {
        var salida = new List<Vector2[]>();
        int i = 0;
        foreach (int largo in largos)
        {
            if (i + largo > puntos.Count)
            {
                break;
            }
            var contorno = new Vector2[largo];
            for (int j = 0; j < largo; j++)
            {
                contorno[j] = puntos[i + j];
            }
            salida.Add(contorno);
            i += largo;
        }
        return salida;
    }

    /// <summary>Aplana los contornos para poder escribirlos.</summary>
    public void PonerForma(List<Vector2[]> forma)
    {
        largos.Clear();
        puntos.Clear();
        foreach (Vector2[] contorno in forma)
        {
            largos.Add(contorno.Length);
            puntos.AddRange(contorno);
        }
    }
}
