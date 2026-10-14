using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// La lucecita de la mano: verde mientras tiene algo (agarrado con el
/// clic o calzado con la tecla), roja con la mano vacía.
///
/// Pinta todo lo que cuelgue de "Luz": los SpriteRenderer (el neón) y las
/// Light2D, igual que las luces de las zonas. El color va por renderer y
/// no por material, así que con el material neón compartido sigue
/// brillando.
///
/// Va en cualquier objeto — el de la Mano es un buen lugar.
/// </summary>
public class LuzMano : MonoBehaviour
{
    [SerializeField] private Mano mano;

    [Tooltip("La luz (o el padre de varias cosas a pintar juntas).")]
    [SerializeField] private Transform luz;

    [SerializeField] private Color conAlgo = new Color(0.3f, 1f, 0.4f, 1f);
    [SerializeField] private Color vacia = new Color(1f, 0.2f, 0.2f, 1f);

    private bool _pintada;
    private bool _teniaAlgo;

    private void Awake()
    {
        if (mano == null)
        {
            mano = GetComponentInParent<Mano>();
        }
    }

    private void LateUpdate()
    {
        if (mano == null || luz == null)
        {
            return;
        }

        bool tieneAlgo = mano.Agarrado != null || mano.TieneEquipado;

        // solo se repinta cuando cambia: recorrer los hijos cada frame
        // por nada no tiene sentido
        if (_pintada && tieneAlgo == _teniaAlgo)
        {
            return;
        }
        _pintada = true;
        _teniaAlgo = tieneAlgo;
        Pintar(luz, tieneAlgo ? conAlgo : vacia);
    }

    private static void Pintar(Transform t, Color c)
    {
        if (t.TryGetComponent(out SpriteRenderer sr))
        {
            sr.color = c;
        }
        if (t.TryGetComponent(out Light2D l))
        {
            l.color = c;
        }
        for (int i = 0; i < t.childCount; i++)
        {
            Pintar(t.GetChild(i), c);
        }
    }
}
