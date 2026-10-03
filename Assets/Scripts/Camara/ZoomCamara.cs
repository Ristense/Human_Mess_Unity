using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Zoom con la rueda del mouse. Cambia el Orthographic Size de la
/// cámara — en una cámara ortográfica (la normal para 2D) ESO es el
/// zoom: más chico ves menos mundo pero más de cerca, más grande ves
/// más mundo pero más lejos. Al revés de como funcionaba el "zoom" en
/// Godot, que ahí multiplicaba.
///
/// Mismo patrón de suavizado que usamos en otros lados: no saltás al
/// valor pedido, lo perseguís de a poco cada frame — se siente menos
/// seco que si el zoom cambiara de un tirón por cada click de rueda.
/// </summary>
[RequireComponent(typeof(Camera))]
public class ZoomCamara : MonoBehaviour
{
    [Tooltip("Cuánto cambia el zoom por cada tick de rueda. Es " +
        "multiplicativo: un paso se siente igual de grande estando " +
        "cerca que estando lejos.")]
    [SerializeField] private float zoomStep = 1.1f;

    [Tooltip("Qué tan cerca te podés acercar (Orthographic Size más chico).")]
    [SerializeField] private float zoomMin = 2f;

    [Tooltip("Qué tan lejos te podés alejar (Orthographic Size más grande).")]
    [SerializeField] private float zoomMax = 15f;

    [Tooltip("Qué tan rápido el zoom real persigue al zoom pedido. " +
        "Más alto = más seco; más bajo = más suave.")]
    [SerializeField] private float zoomSpeed = 12f;

    private Camera _cam;
    private float _target;

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        _target = _cam.orthographicSize;
    }

    private void Update()
    {
        if (Mouse.current == null)
        {
            return;
        }

        float scroll = Mouse.current.scroll.ReadValue().y;
        if (scroll > 0f)
        {
            // rueda hacia arriba = acercarse = ver MENOS mundo = size más chico
            _target = Mathf.Max(_target / zoomStep, zoomMin);
        }
        else if (scroll < 0f)
        {
            _target = Mathf.Min(_target * zoomStep, zoomMax);
        }

        // suavizado exponencial: se acerca un porcentaje de lo que
        // falta cada frame, así que no depende de los FPS
        float t = 1f - Mathf.Exp(-zoomSpeed * Time.deltaTime);
        _cam.orthographicSize = Mathf.Lerp(_cam.orthographicSize, _target, t);
    }
}
