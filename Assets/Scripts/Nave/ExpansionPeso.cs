using UnityEngine;

/// <summary>
/// Módulo que le sube el tope de peso a la nave. Mientras esté montado
/// suma su bonus; lo desmontás y el tope vuelve a bajar.
///
/// No hace nada por sí solo: PesoNave lo busca entre sus hijos, y un
/// módulo suelto flotando en el espacio no es hijo de nadie. O sea que
/// enchufarlo y desenchufarlo ya lo prende y lo apaga, sin que haya
/// que avisarle a nadie.
///
/// Va en el mismo objeto que el Modulo.
/// </summary>
public class ExpansionPeso : MonoBehaviour
{
    [Tooltip("Cuántos kg de capacidad le suma a la nave mientras esté " +
        "montado.")]
    public float bonus = 1000f;
}
