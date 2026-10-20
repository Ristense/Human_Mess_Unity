/// <summary>
/// De qué está hecho algo que se puede sacar con el taladro. La bodega
/// lleva la cuenta de cada uno por separado.
/// </summary>
public enum TipoMaterial
{
    Roca,
    Hielo,

    // metal hecho pedazos: lo que sale de taladrar la chatarra en vez de
    // cortarla en placas con el soldador. Vale menos que una placa.
    Restos,
}
