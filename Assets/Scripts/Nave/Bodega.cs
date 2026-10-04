using UnityEngine;

/// <summary>
/// La bodega de carga de la nave: cuánta chatarra entra, cuánto pesa,
/// y los créditos — el mismo trío que tenías en main.gd de Godot
/// (escombros / capacidad / creditos), movido a su propio componente
/// en vez de vivir en el script de la nave.
/// </summary>
public class Bodega : MonoBehaviour
{
    [Tooltip("Cuántas unidades de chatarra entran en la bodega.")]
    [SerializeField] private int capacidad = 120;

    [Tooltip("Peso (kg) de CADA unidad de chatarra — con esto sale el " +
        "peso total de la carga sin tener que guardar un número aparte.")]
    [SerializeField] private float masaPorUnidad = 0.5f;

    [Tooltip("Cuánta chatarra llevás ahora. No lo toques a mano en " +
        "runtime, usá SumarCarga().")]
    [SerializeField] private int escombros = 0;

    [SerializeField] private float creditos = 0f;

    public int Capacidad => capacidad;
    public int Escombros => escombros;
    public float Creditos => creditos;
    public bool Llena => escombros >= capacidad;

    /// <summary>Peso de la carga en kg, aparte del peso de la nave misma.</summary>
    public float MasaCarga()
    {
        return escombros * masaPorUnidad;
    }

    /// <summary>
    /// Intenta sumar `n` unidades. Si no entran todas, suma las que
    /// sí entran y devuelve false — así quien aspira sabe si se quedó
    /// pegado por bodega llena.
    /// </summary>
    public bool SumarCarga(int n = 1)
    {
        int entran = Mathf.Min(n, capacidad - escombros);
        if (entran <= 0)
        {
            return false;
        }
        escombros += entran;
        return entran == n;
    }

    /// <summary>
    /// Vacía la bodega y la convierte en créditos. Devuelve cuánto se
    /// vendió (en unidades), por si quien llama quiere avisar algo.
    /// </summary>
    public int Vender(float precioPorUnidad)
    {
        int vendidos = escombros;
        creditos += vendidos * precioPorUnidad;
        escombros = 0;
        return vendidos;
    }
}
