using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Siembra objetos (asteroides, chatarra, lo que sea) por TODOS los
/// sectores del Mapa, clonando moldes armados en el editor — mismo
/// approach que campo_asteroides.gd en Godot: los moldes son
/// RigidBody2D reales de la escena, con su collider y su masa, y esto
/// solo los copia, escala y tira al azar. Lo que ves en el editor es
/// lo que va a haber en el juego.
///
/// A diferencia de Godot, acá SÍ se puede escalar el GameObject entero
/// (RigidBody2D incluido) sin que el collider se desalinee — Unity no
/// tiene esa limitación, así que no hace falta el truco de escalar
/// solo los hijos.
/// </summary>
public class CampoObjetos : MonoBehaviour
{
    [Tooltip("El Mapa que define los sectores. Si está vacío, busca " +
        "el primero que encuentre en la escena.")]
    [SerializeField] private Mapa mapa;

    [Tooltip("Los objetos que se clonan — metele varias formas " +
        "distintas y elige una al azar por cada uno que siembra. " +
        "Con un solo molde funciona igual que antes.")]
    [SerializeField] private Rigidbody2D[] moldes;

    [Tooltip("Los moldes no se juegan — se sacan de la escena después " +
        "de clonarlos, para que no queden gigantes flotando de más.")]
    [SerializeField] private bool borrarMoldes = true;

    [Tooltip("Cuántos en TOTAL, repartidos entre todos los sectores " +
        "(no por sector).")]
    [SerializeField] private int cantidadTotal = 300;

    [Tooltip("Rango de escala, como factor del molde elegido.")]
    [SerializeField] private float escalaMin = 0.3f;
    [SerializeField] private float escalaMax = 1.2f;

    [Header("Variedad de color")]
    [Tooltip("Si tu molde tiene SpriteRenderer, cada copia se tiñe un " +
        "poco distinto — así dos clones del MISMO molde tampoco se " +
        "ven idénticos. En 0 no tiñe nada (color original).")]
    [SerializeField] [Range(0f, 1f)] private float variacionColor = 0.15f;

    [Tooltip("Cuánto varía el brillo entre copias (1 = sin variación).")]
    [SerializeField] private Vector2 rangoBrillo = new Vector2(0.85f, 1.15f);

    [Header("Distribución")]
    [Tooltip("Cuánto del sector se usa, como fracción. Menos que 1 " +
        "deja margen en los bordes.")]
    [SerializeField] private float margen = 0.9f;

    [Tooltip("Radio libre alrededor de donde arranca la nave (el " +
        "centro del sector 0,0) — ahí no se siembra nada.")]
    [SerializeField] private float despejeNave = 3f;

    [Tooltip("Deriva inicial. Sin fricción en el espacio, con poco alcanza.")]
    [SerializeField] private float derivaMax = 0.4f;
    [SerializeField] private float giroMax = 20f;

    private void Start()
    {
        if (mapa == null)
        {
            mapa = FindFirstObjectByType<Mapa>();
        }
        if (mapa == null || moldes == null || moldes.Length == 0)
        {
            Debug.LogWarning("CampoObjetos: falta el Mapa o los moldes, no siembro nada.");
            return;
        }

        List<Vector2Int> sectores = mapa.AllSectors();
        if (sectores.Count == 0)
        {
            return;
        }

        int porSector = Mathf.Max(1, cantidadTotal / sectores.Count);
        float[] radioPorMolde = new float[moldes.Length];
        for (int i = 0; i < moldes.Length; i++)
        {
            radioPorMolde[i] = RadioDe(moldes[i]);
        }

        Vector2 despejeCentro = mapa.SectorCenterWorld(Vector2Int.zero);

        foreach (Vector2Int s in sectores)
        {
            // semilla propia por sector: mismo sector, mismo contenido
            // siempre, y cada uno tira sus propios dados — si
            // compartieran semilla, todos los sectores saldrían con
            // el mismo patrón calcado.
            var rng = new System.Random(mapa.SectorSeed(s));
            Vector2 centro = mapa.SectorCenterWorld(s);
            Vector2 mitad = mapa.SectorSize * 0.5f * margen;

            var puestos = new List<(Vector2 pos, float radio)>();

            for (int i = 0; i < porSector; i++)
            {
                int indiceMolde = rng.Next(moldes.Length);
                float escala = Mathf.Lerp(escalaMin, escalaMax, (float)rng.NextDouble());
                float radio = radioPorMolde[indiceMolde] * escala;

                Vector2 pos = Vector2.zero;
                bool libre = false;

                // 30 intentos y si no, se saltea: mejor uno de menos
                // que uno incrustado adentro de otro.
                for (int intento = 0; intento < 30; intento++)
                {
                    pos = centro + new Vector2(
                        Lerp(-mitad.x, mitad.x, rng),
                        Lerp(-mitad.y, mitad.y, rng));

                    if (Vector2.Distance(pos, despejeCentro) < despejeNave + radio)
                    {
                        continue;
                    }

                    libre = true;
                    foreach (var p in puestos)
                    {
                        if (Vector2.Distance(pos, p.pos) < radio + p.radio)
                        {
                            libre = false;
                            break;
                        }
                    }
                    if (libre)
                    {
                        break;
                    }
                }

                if (!libre)
                {
                    continue;
                }

                puestos.Add((pos, radio));
                Sembrar(moldes[indiceMolde], pos, escala, rng);
            }
        }

        if (borrarMoldes)
        {
            foreach (var m in moldes)
            {
                if (m != null)
                {
                    Destroy(m.gameObject);
                }
            }
        }
    }

    private static float Lerp(float a, float b, System.Random rng)
    {
        return Mathf.Lerp(a, b, (float)rng.NextDouble());
    }

    private void Sembrar(Rigidbody2D molde, Vector2 pos, float escala, System.Random rng)
    {
        Rigidbody2D clon = Instantiate(molde, pos, Quaternion.Euler(0f, 0f, (float)rng.NextDouble() * 360f));
        clon.transform.localScale = molde.transform.localScale * escala;

        // la masa sale del ÁREA, no del tamaño lineal: al doble de
        // escala, cuatro veces más pesado. Es lo que hace que un
        // objeto grande cueste de verdad mover con el brazo.
        clon.mass = molde.mass * escala * escala;
        clon.gravityScale = 0f;
        clon.linearDamping = 0f;
        clon.angularDamping = 0f;

        clon.linearVelocity = new Vector2(
            Lerp(-derivaMax, derivaMax, rng),
            Lerp(-derivaMax, derivaMax, rng));
        clon.angularVelocity = Lerp(-giroMax, giroMax, rng);

        if (clon.GetComponent<Asteroide>() == null)
        {
            clon.gameObject.AddComponent<Asteroide>();
        }

        TenirSprites(clon.transform, rng);
    }

    /// <summary>
    /// Tiñe TODOS los SpriteRenderer del clon (el de arriba y los
    /// hijos, por si el molde tiene varias capas) con el mismo tono,
    /// para que no quede cada pedazo de un color distinto al azar.
    /// </summary>
    private void TenirSprites(Transform raiz, System.Random rng)
    {
        if (variacionColor <= 0f)
        {
            return;
        }

        float h = Lerp(-variacionColor, variacionColor, rng) * 0.5f;
        float brillo = Lerp(rangoBrillo.x, rangoBrillo.y, rng);

        var renderers = raiz.GetComponentsInChildren<SpriteRenderer>();
        foreach (var sr in renderers)
        {
            Color.RGBToHSV(sr.color, out float hue, out float sat, out float val);
            hue = Mathf.Repeat(hue + h, 1f);
            val = Mathf.Clamp01(val * brillo);
            Color nuevo = Color.HSVToRGB(hue, sat, val);
            nuevo.a = sr.color.a;
            sr.color = nuevo;
        }
    }

    /// <summary>Radio aproximado del molde, medido sobre su collider.</summary>
    private static float RadioDe(Rigidbody2D cuerpo)
    {
        var col = cuerpo.GetComponent<Collider2D>();
        if (col == null)
        {
            return 0.3f;
        }
        return Mathf.Max(col.bounds.extents.x, col.bounds.extents.y);
    }
}
