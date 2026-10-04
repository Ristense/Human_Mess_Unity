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

    [Header("Textos")]
    [SerializeField] private TMP_Text pesoTexto;
    [SerializeField] private TMP_Text sectorTexto;
    [SerializeField] private TMP_Text cargaTexto;
    [SerializeField] private TMP_Text creditosTexto;
    [SerializeField] private TMP_Text objetosTexto;

    // Contar objetos del mapa con FindObjectsByType es relativamente
    // caro — no hace falta hacerlo los 60 frames por segundo. Un tick
    // cada tanto alcanza y sobra para que el número no se vea atrasado.
    [SerializeField] private int framesPorTickObjetos = 15;
    private int _tick;

    private void Update()
    {
        if (pesoTexto != null && nave != null)
        {
            float pesoBase = nave.mass;
            float pesoCarga = bodega != null ? bodega.MasaCarga() : 0f;
            pesoTexto.text = pesoCarga > 0f
                ? $"Peso: {pesoBase:0.0} kg  (+{pesoCarga:0.0} kg de carga)"
                : $"Peso: {pesoBase:0.0} kg";
        }

        if (sectorTexto != null && mapa != null && nave != null)
        {
            Vector2Int s = mapa.WorldToSector(nave.position);
            sectorTexto.text = $"Sector {s.x}, {s.y}";
        }

        if (cargaTexto != null && bodega != null)
        {
            cargaTexto.text = $"Carga: {bodega.Escombros} / {bodega.Capacidad}";
        }

        if (creditosTexto != null && bodega != null)
        {
            creditosTexto.text = $"Créditos: {bodega.Creditos:0}";
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
