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

    [Header("Calor")]
    [Tooltip("Cuánto sube el calor por segundo con el botón apretado.")]
    [Min(0f)]
    [SerializeField] private float subidaCalor = 0.8f;

    [Tooltip("Cuánto baja por segundo al soltar. Más lento que la " +
        "subida deja margen para corregir; más rápido obliga a estar " +
        "encima todo el rato.")]
    [Min(0f)]
    [SerializeField] private float bajadaCalor = 0.55f;

    [Tooltip("Desde qué calor empieza a cortar.")]
    [Range(0f, 1f)]
    [SerializeField] private float rangoMin = 0.45f;

    [Tooltip("Hasta qué calor es un corte limpio. Pasado esto se " +
        "recalienta: sigue cortando, pero arruina la placa.")]
    [Range(0f, 1f)]
    [SerializeField] private float rangoMax = 0.7f;

    [Tooltip("Cuánto queda de la placa si TODO se cortó recalentado. " +
        "0.65 = pesa el 65%, y como se vende por kilo, también vale el " +
        "65%. 1 = recalentarse no cuesta nada.")]
    [Range(0f, 1f)]
    [SerializeField] private float penalizacionRecalentado = 0.65f;

    [Header("Lo que sale")]
    [Tooltip("Cuántos kg pesa cada celda de la pieza.")]
    [SerializeField] private float kgPorCelda = 5f;

    [Tooltip("Dónde se guardan las placas. Vacío = la busca en la escena.")]
    [SerializeField] private Bodega bodega;

    /// <summary>
    /// ¿Está quemando ahora mismo? Con el botón apretado NO alcanza:
    /// además el calor tiene que estar en la franja.
    /// </summary>
    public bool Cortando { get; private set; }

    /// <summary>¿La punta está sobre una línea? Sirve para el aviso.</summary>
    public bool EnLinea { get; private set; }

    /// <summary>El calor de la llama, de 0 a 1. Para el medidor.</summary>
    public float Calor { get; private set; }

    /// <summary>¿El botón está apretado? El calor sube mientras lo esté.</summary>
    public bool Apretado { get; private set; }

    /// <summary>¿El calor está en la franja que corta?</summary>
    public bool EnRango => Calor >= rangoMin && Calor <= rangoMax;

    /// <summary>Se pasó de la franja: no corta hasta que baje.</summary>
    public bool Recalentado => Calor > rangoMax;

    /// <summary>Dónde empieza la franja, de 0 a 1. Para dibujarla.</summary>
    public float RangoMin => rangoMin;

    /// <summary>Dónde termina la franja, de 0 a 1. Para dibujarla.</summary>
    public float RangoMax => rangoMax;

    /// <summary>
    /// Qué tan limpia va la pieza que estás cortando, de 0 a 1. Sale de
    /// cuánto se quemó recalentado contra cuánto se quemó bien, y es lo
    /// que multiplica los kg al soltarla.
    /// </summary>
    public float Calidad
    {
        get
        {
            float total = _quemadoBien + _quemadoMal;
            if (total <= 0f)
            {
                return 1f;
            }
            return Mathf.Lerp(penalizacionRecalentado, 1f, _quemadoBien / total);
        }
    }

    private float _quemadoBien;
    private float _quemadoMal;

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

        // La ventana se abre con la llama apagada. Si el calor quedara
        // del rato anterior, al abrir estarías cortando sin haber
        // apretado nada.
        Calor = 0f;
        Apretado = false;
        Cortando = false;
        EnLinea = false;
        _quemadoBien = 0f;
        _quemadoMal = 0f;
    }

    private void OnValidate()
    {
        // Con el mínimo por encima del máximo la franja no existe y
        // nunca se podría cortar, sin ningún aviso de por qué.
        rangoMax = Mathf.Max(rangoMax, rangoMin);
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
        Apretado = boton.isPressed;

        // El calor sube mientras se aprieta y baja al soltar. Cortar no
        // es apretar y listo: hay que SOSTENERLO en la franja, soltando
        // y volviendo a apretar mientras se pasea la placa. Esa es toda
        // la mecánica — la mano hace dos cosas a la vez, mover el metal
        // y dosificar la llama.
        Calor = Mathf.Clamp01(Calor
            + (Apretado ? subidaCalor : -bajadaCalor) * Time.deltaTime);

        // Recalentado SÍ corta: el castigo no es que te frene, es que
        // arruina el metal. Así apretar a fondo sigue siendo una
        // opción —más rápida y más torpe— y no una pared. El que tiene
        // apuro saca placas feas; el que dosifica las saca enteras.
        Cortando = EnRango || Recalentado;

        if (!Cortando)
        {
            EnLinea = false;
            return;
        }

        float cantidad = velocidad * Time.deltaTime;
        int tocados = placa.Quemar(movil.PuntoSoldador, radio, cantidad);
        EnLinea = tocados > 0;

        // Se mide lo que de verdad se quemó, no el tiempo: pasear la
        // llama por el aire no debería contar ni a favor ni en contra.
        if (tocados > 0)
        {
            float hecho = tocados * cantidad;
            if (Recalentado)
            {
                _quemadoMal += hecho;
            }
            else
            {
                _quemadoBien += hecho;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        PlacaMovil m = movil != null
            ? movil
            : (placa != null ? placa.GetComponent<PlacaMovil>() : null);
        if (placa == null || m == null)
        {
            return;
        }

        // En el espacio de la PLACA, que es donde Quemar compara: el
        // radio está en sus coordenadas, no en píxeles de pantalla.
        // Dibujarlo en mundo mentiría apenas la placa se ladea o el
        // Canvas se escala.
        Gizmos.matrix = placa.transform.localToWorldMatrix;
        Gizmos.color = Cortando
            ? new Color(1f, 0.75f, 0.25f)
            : new Color(0.45f, 0.6f, 0.85f, 0.7f);
        Gizmos.DrawWireSphere(m.PuntoSoldador, radio);
    }

    private void Soltar(int pieza)
    {
        float calidad = Calidad;
        float kg = placa.CeldasDe(pieza) * kgPorCelda * calidad;

        // La cuenta arranca de cero para la pieza siguiente: lo que
        // arruinaste en esta ya lo pagaste, y arrastrarlo haría que un
        // mal rato al principio te hundiera la plancha entera.
        _quemadoBien = 0f;
        _quemadoMal = 0f;

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
