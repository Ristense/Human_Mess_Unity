using UnityEngine;

/// <summary>
/// Un pedacito de material suelto, de los que escupe el taladro. Lleva
/// cuántos kg vale: es exactamente lo que se le sacó a la roca, así que
/// lo que entra a la bodega es lo que salió del asteroide, ni más ni
/// menos.
///
/// Va en el prefab del escombro, junto a un Rigidbody2D.
/// </summary>
public class Escombro : MonoBehaviour
{
    [Tooltip("Cuántos kg suma a la bodega al tragarlo. Lo pone el " +
        "taladro al crearlo.")]
    public float kg = 1f;

    [Tooltip("De qué es. Lo pone el taladro, según la roca de donde salió.")]
    public TipoMaterial material = TipoMaterial.Roca;
}
