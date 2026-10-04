using UnityEngine;

/// <summary>
/// Marca vacía para contar/filtrar objetos sembrados en el mapa — el
/// equivalente a add_to_group("asteroides") de Godot. Unity no tiene
/// grupos por string, así que esto hace el mismo trabajo: un
/// FindObjectsByType&lt;Asteroide&gt;() cuenta todos los que haya vivos.
/// </summary>
public class Asteroide : MonoBehaviour
{
}
