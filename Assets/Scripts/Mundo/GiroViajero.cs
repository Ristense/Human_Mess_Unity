using UnityEngine;

/// <summary>
/// Un giro EXTRA del planeta, independiente del autogiro constante de
/// GirarPlaneta — este reacciona a que VOS te muevas. La idea es
/// reforzar la misma sensación que da Paralaje (que estás rodeando
/// algo lejano) pero con rotación en vez de con posición: cuando te
/// movés para un lado, el planeta gira un poco para ese lado, como si
/// lo estuvieras viendo desde otro ángulo.
///
/// Se puede combinar sin problema con GirarPlaneta en el mismo
/// objeto: los dos llaman a transform.Rotate, y una rotación relativa
/// más otra simplemente se suman, no se pisan.
/// </summary>
public class GiroViajero : MonoBehaviour
{
    [Tooltip("La nave — de ahí sale cuánto te moviste.")]
    [SerializeField] private Transform objetivo;

    [Tooltip("Grados de giro extra por cada unidad que te movés en X " +
        "(izquierda/derecha). Chico a propósito: esto es un EXTRA " +
        "sutil, no el giro principal del planeta.")]
    [SerializeField] private float gradosPorUnidadX = 0.02f;

    [Tooltip("Lo mismo pero para el movimiento en Y (arriba/abajo), " +
        "inclinando el planeta en vez de girarlo sobre su eje.")]
    [SerializeField] private float gradosPorUnidadY = 0.01f;

    private Vector3 _posAnterior;
    private bool _tieneAnterior;

    private void LateUpdate()
    {
        if (objetivo == null)
        {
            return;
        }

        if (!_tieneAnterior)
        {
            // primer frame: no hay "anterior" todavía, solo guardamos
            // y arrancamos a medir desde acá
            _posAnterior = objetivo.position;
            _tieneAnterior = true;
            return;
        }

        Vector3 delta = objetivo.position - _posAnterior;
        _posAnterior = objetivo.position;

        // Y del mundo = girar sobre el eje de giro normal (como mirar
        // el globo desde otro lado). X del mundo = inclinar en el eje
        // perpendicular. Son rotaciones LOCALES relativas (Space.Self)
        // para que respeten la inclinación que ya le puso GirarPlaneta.
        transform.Rotate(Vector3.up, delta.x * gradosPorUnidadX, Space.Self);
        transform.Rotate(Vector3.right, -delta.y * gradosPorUnidadY, Space.Self);
    }
}
