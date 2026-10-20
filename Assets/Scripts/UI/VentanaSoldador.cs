using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// La ventana del soldador: un cuadro tipo viñeta de cómic que se abre
/// con una tecla, con su cola (ColaVentana) apuntando a la mano.
///
/// Mientras está abierta, la mano se queda quieta (BrazoIK.Quieto): el
/// mouse queda libre para trabajar adentro de la ventana.
///
/// Prende y apaga los HIJOS, nunca este objeto: si se apagara a sí
/// mismo, no habría quién lo volviera a prender.
///
/// Va en el objeto padre de la ventana (el cuadro y la cola, hijos).
/// </summary>
public class VentanaSoldador : MonoBehaviour
{
    [SerializeField] private Key tecla = Key.F;

    [Tooltip("El brazo, para dejar la mano quieta mientras está abierta.")]
    [SerializeField] private BrazoIK brazo;

    /// <summary>¿Está abierta?</summary>
    public bool Abierta { get; private set; }

    private void Start()
    {
        Mostrar(false);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current[tecla].wasPressedThisFrame)
        {
            Mostrar(!Abierta);
        }
    }

    public void Mostrar(bool abierta)
    {
        Abierta = abierta;
        foreach (Transform h in transform)
        {
            h.gameObject.SetActive(abierta);
        }
        if (brazo != null)
        {
            brazo.Quieto = abierta;
        }
    }

    private void OnDisable()
    {
        // que la mano no quede pegada si la ventana desaparece abierta
        if (brazo != null)
        {
            brazo.Quieto = false;
        }
    }
}
