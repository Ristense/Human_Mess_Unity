using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Velocímetro de aguja... sin aguja: un arco que se llena según qué
/// tan rápido vas.
///
/// Usa una Image del Canvas en modo Filled / Radial 360. "Arco" deja el
/// medidor abierto abajo como un tablero de auto: con 0.75, a velocidad
/// máxima se llenan tres cuartos de vuelta y no el círculo entero.
///
/// Va en cualquier objeto del Canvas.
/// </summary>
public class Velocimetro : MonoBehaviour
{
    [SerializeField] private Rigidbody2D nave;

    [Tooltip("Image con Image Type = Filled y Fill Method = Radial 360.")]
    [SerializeField] private Image relleno;

    [Tooltip("Opcional: el número.")]
    [SerializeField] private TMP_Text texto;

    [Tooltip("A qué velocidad (unidades/seg) el medidor llega al tope.")]
    [SerializeField] private float velocidadMaxima = 5f;

    [Tooltip("Qué fracción del círculo usa el medidor. 1 = vuelta " +
        "entera, 0.75 = tablero de auto, abierto abajo. Para que " +
        "arranque abajo a la izquierda, girá la Image en Z.")]
    [Range(0.1f, 1f)]
    [SerializeField] private float arco = 0.75f;

    [Tooltip("Segundos que tarda la aguja en alcanzar el valor. Sin " +
        "esto tiembla con cada empujoncito del motor.")]
    [SerializeField] private float suavizado = 0.15f;

    [Tooltip("Multiplicador para el número del texto. Si querés " +
        "mostrar algo más \"de nave\" que 0.4, poné 100 y lo ves como " +
        "40.")]
    [SerializeField] private float escalaTexto = 100f;

    [SerializeField] private string unidad = "m/s";

    private float _mostrado;
    private float _vel;

    private void LateUpdate()
    {
        if (nave == null)
        {
            return;
        }

        float rapidez = nave.linearVelocity.magnitude;
        _mostrado = suavizado > 0f
            ? Mathf.SmoothDamp(_mostrado, rapidez, ref _vel, suavizado)
            : rapidez;

        if (relleno != null && velocidadMaxima > 0f)
        {
            relleno.fillAmount = Mathf.Clamp01(_mostrado / velocidadMaxima) * arco;
        }

        if (texto != null)
        {
            texto.text = $"{_mostrado * escalaTexto:0} {unidad}";
        }
    }
}
