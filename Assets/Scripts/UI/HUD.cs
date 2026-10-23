using TMPro;
using UnityEngine;

/// <summary>
/// El HUD de la nave: peso, sector, carga, créditos y cuántos objetos
/// hay sembrados en el mapa — el mismo set de labels que tenías en el
/// HUD de Godot.
///
/// Los campos de texto son TMP_Text (no un tipo concreto) para que
/// funcione igual si los armás con TextMeshPro - Text (UI) en un
/// Canvas o con el componente 3D — arrastrás el que corresponda.
/// </summary>
public class HUD : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Rigidbody2D nave;
    [SerializeField] private Bodega bodega;
    [SerializeField] private Mapa mapa;

    [Tooltip("Los tanques. Vacío = lo busca en la escena.")]
    [SerializeField] private SoporteVital soporte;

    [Header("Textos")]
    [SerializeField] private TMP_Text pesoTexto;
    [SerializeField] private TMP_Text sectorTexto;
    [SerializeField] private TMP_Text cargaTexto;
    [SerializeField] private TMP_Text creditosTexto;
    [SerializeField] private TMP_Text placasTexto;
    [SerializeField] private TMP_Text hieloTexto;
    [SerializeField] private TMP_Text restosTexto;
    [SerializeField] private TMP_Text objetosTexto;

    [Tooltip("Oxígeno: porcentaje y minutos de aire que quedan.")]
    [SerializeField] private TMP_Text oxigenoTexto;

    [Tooltip("Hidrógeno: porcentaje y minutos de EMPUJE CONTINUO. Son " +
        "el peor caso, no el tiempo que va a durar de verdad: solo " +
        "baja mientras acelerás.")]
    [SerializeField] private TMP_Text hidrogenoTexto;

    // Contar objetos del mapa con FindObjectsByType es relativamente
    // caro — no hace falta hacerlo los 60 frames por segundo. Un tick
    // cada tanto alcanza y sobra para que el número no se vea atrasado.
    [SerializeField] private int framesPorTickObjetos = 15;
    private int _tick;

    private void Awake()
    {
        if (soporte == null)
        {
            soporte = FindAnyObjectByType<SoporteVital>();
        }
    }

    private void Update()
    {
        if (oxigenoTexto != null && soporte != null)
        {
            oxigenoTexto.text =
                $"O₂: {soporte.Oxigeno * 100f:0}%  ({soporte.MinutosAire:0} min)";
        }

        if (hidrogenoTexto != null && soporte != null)
        {
            hidrogenoTexto.text =
                $"H₂: {soporte.Hidrogeno * 100f:0}%  ({soporte.MinutosEmpuje:0.0} min de empuje)";
        }

        if (pesoTexto != null && nave != null)
        {
            // nave.mass ya trae todo sumado si PesoNave lo aplica a la
            // física, así que acá NO se le vuelve a sumar la carga: se
            // contaría dos veces
            float pesoCarga = bodega != null ? bodega.Carga : 0f;
            pesoTexto.text = pesoCarga > 0f
                ? $"Peso: {nave.mass:0} kg  ({pesoCarga:0} kg de carga)"
                : $"Peso: {nave.mass:0} kg";
        }

        if (sectorTexto != null && mapa != null && nave != null)
        {
            Vector2Int s = mapa.WorldToSector(nave.position);
            sectorTexto.text = $"Sector {s.x}, {s.y}";
        }

        if (cargaTexto != null && bodega != null)
        {
            cargaTexto.text = $"Carga: {bodega.Carga:0} / {bodega.Capacidad:0} kg";
        }

        if (creditosTexto != null && bodega != null)
        {
            creditosTexto.text = $"Créditos: {bodega.Creditos:0}";
        }

        if (placasTexto != null && bodega != null)
        {
            placasTexto.text = $"Placas: {bodega.Placas}  ({bodega.KgPlacas:0} kg)";
        }

        if (hieloTexto != null && bodega != null)
        {
            hieloTexto.text = $"Hielo: {bodega.KgHielo:0} kg";
        }

        if (restosTexto != null && bodega != null)
        {
            restosTexto.text = $"Restos: {bodega.KgRestos:0} kg";
        }

        if (objetosTexto != null)
        {
            _tick++;
            if (_tick >= framesPorTickObjetos)
            {
                _tick = 0;
                int n = FindObjectsByType<Asteroide>(FindObjectsSortMode.None).Length;
                objetosTexto.text = $"Objetos en el mapa: {n}";
            }
        }
    }
}
