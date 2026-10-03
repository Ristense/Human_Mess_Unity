using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Zoom con la rueda del mouse. Detecta sola si la cámara es
/// Orthographic o Perspective y usa el control que corresponda a
/// cada una — así no se rompe si después le cambiás el modo de
/// proyección (como pasó: esto estaba hecho solo para Ortográfica y
/// dejó de reaccionar al pasar a Perspective).
///
///   Orthographic: cambia Orthographic Size (cuánto mundo entra en
///                 pantalla — esto no tiene "distancia" real).
///   Perspective:  cambia Field of View (el "lente" de la cámara —
///                 más angosto se siente más cerca, sin mover la
///                 cámara de lugar y sin pelearse con SeguirJugador,
///                 que es quien controla la posición).
///
/// Mismo patrón de suavizado que usamos en otros lados: no saltás al
/// valor pedido, lo perseguís de a poco cada frame.
/// </summary>
[RequireComponent(typeof(Camera))]
public class ZoomCamara : MonoBehaviour
{
    [Tooltip("Cuánto cambia el zoom por cada tick de rueda. Es " +
        "multiplicativo: un paso se siente igual de grande estando " +
        "cerca que estando lejos. Aplica en modo Orthographic.")]
    [SerializeField] private float zoomStep = 1.1f;

    [Header("Orthographic")]
    [SerializeField] private float orthoMin = 2f;
    [SerializeField] private float orthoMax = 15f;

    [Header("Perspective (Field of View, en grados)")]
    [Tooltip("FOV más chico = más acercado (como un zoom de lente).")]
    [SerializeField] private float fovMin = 15f;
    [SerializeField] private float fovMax = 70f;
    [Tooltip("Cuántos grados de FOV cambia por tick de rueda.")]
    [SerializeField] private float fovStep = 3f;

    [Tooltip("Qué tan rápido el zoom real persigue al zoom pedido. " +
        "Más alto = más seco; más bajo = más suave.")]
    [SerializeField] private float zoomSpeed = 12f;

    private Camera _cam;
    private float _target;

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        _target = _cam.orthographic ? _cam.orthographicSize : _cam.fieldOfView;
    }

    private void Update()
    {
        if (Mouse.current == null)
        {
            return;
        }

        float scroll = Mouse.current.scroll.ReadValue().y;

        if (_cam.orthographic)
        {
            if (scroll > 0f)
            {
                // rueda arriba = acercarse = ver MENOS mundo = size más chico
                _target = Mathf.Max(_target / zoomStep, orthoMin);
            }
            else if (scroll < 0f)
            {
                _target = Mathf.Min(_target * zoomStep, orthoMax);
            }
        }
        else
        {
            if (scroll > 0f)
            {
                // rueda arriba = acercarse = FOV más angosto
                _target = Mathf.Max(_target - fovStep, fovMin);
            }
            else if (scroll < 0f)
            {
                _target = Mathf.Min(_target + fovStep, fovMax);
            }
        }

        // suavizado exponencial: se acerca un porcentaje de lo que
        // falta cada frame, así que no depende de los FPS
        float t = 1f - Mathf.Exp(-zoomSpeed * Time.deltaTime);
        if (_cam.orthographic)
        {
            _cam.orthographicSize = Mathf.Lerp(_cam.orthographicSize, _target, t);
        }
        else
        {
            _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, _target, t);
        }
    }
}
