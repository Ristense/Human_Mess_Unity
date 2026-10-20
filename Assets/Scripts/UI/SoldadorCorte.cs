using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

/// <summary>
/// El soldador del minijuego: con el clic sostenido va quemando la
/// línea de corte que tiene bajo la punta. La punta no se mueve — lo
/// que se pasea es la placa (PlacaMovil).
///
/// Cuando una pieza queda con todo su contorno cortado se suelta, y se
/// guarda como placa de metal en la bodega. Lo que pesa sale de cuántas
/// celdas tiene: una pieza grande es una placa pesada.
///
/// Va en el objeto del soldador, adentro de la ventana — así deja de
/// correr solo cuando la ventana se cierra.
/// </summary>
public class SoldadorCorte : MonoBehaviour
{
    [SerializeField] private PlacaCorte placa;

    [Tooltip("De dónde sale el punto que corta. Vacío = lo busca en la " +
        "placa.")]
    [SerializeField] private PlacaMovil movil;

    [Header("Llama")]
    [Tooltip("Qué ancho quema, en píxeles del Canvas. Más ancho = más " +
        "fácil no salirse de la línea.")]
    [SerializeField] private float radio = 14f;

    [Tooltip("Cuánto quema por segundo. 1 = un trocito por segundo.")]
    [SerializeField] private float velocidad = 2.5f;

    [Tooltip("Con qué botón se mantiene la llama.")]
    [SerializeField] private bool botonIzquierdo = true;

    [Header("Lo que sale")]
    [Tooltip("Cuántos kg pesa cada celda de la pieza.")]
    [SerializeField] private float kgPorCelda = 5f;

    [Tooltip("Dónde se guardan las placas. Vacío = la busca en la escena.")]
    [SerializeField] private Bodega bodega;

    /// <summary>¿Está quemando ahora mismo?</summary>
    public bool Cortando { get; private set; }

    /// <summary>¿La punta está sobre una línea? Sirve para el aviso.</summary>
    public bool EnLinea { get; private set; }

    private void OnEnable()
    {
        if (movil == null && placa != null)
        {
            movil = placa.GetComponent<PlacaMovil>();
        }
        if (placa != null)
        {
            placa.PiezaLiberada += Soltar;
        }
    }

    private void OnDisable()
    {
        if (placa != null)
        {
            placa.PiezaLiberada -= Soltar;
        }
    }

    private void Update()
    {
        if (placa == null || movil == null || Mouse.current == null)
        {
            return;
        }

        ButtonControl boton = botonIzquierdo
            ? Mouse.current.leftButton
            : Mouse.current.rightButton;
        Cortando = boton.isPressed;

        if (!Cortando)
        {
            EnLinea = false;
            return;
        }

        int tocados = placa.Quemar(movil.PuntoSoldador, radio,
            velocidad * Time.deltaTime);
        EnLinea = tocados > 0;
    }

    private void Soltar(int pieza)
    {
        float kg = placa.CeldasDe(pieza) * kgPorCelda;

        Bodega donde = bodega != null ? bodega : FindAnyObjectByType<Bodega>();
        if (donde == null)
        {
            return;
        }
        // una placa no se parte: si no entra entera, no entra
        if (!donde.SumarPlaca(kg))
        {
            Debug.Log("[Soldador] la placa no entra en la bodega", this);
        }
    }
}
