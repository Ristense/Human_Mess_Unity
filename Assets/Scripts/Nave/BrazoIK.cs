using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// El brazo de la nave: dos huesos (brazo superior + antebrazo) que se
/// doblan solos con IK para que la mano llegue al mouse — la versión
/// de código de lo que en Godot eran dos Line2D más la cuenta
/// trigonométrica en arm.gd.
///
/// La IK de 2 huesos es la de siempre (ley de cosenos): dados el
/// hombro, un objetivo, y los dos largos, hay un único triángulo
/// posible (o ninguno, si el objetivo está demasiado lejos o
/// demasiado cerca) — se arma ese triángulo y listo, no hace falta
/// iterar ni resolver nada numéricamente.
///
/// El objetivo normalmente es el mouse, convertido de pantalla a
/// mundo con un raycast contra el plano XY de la nave — funciona
/// igual sea tu cámara Ortográfica o en Perspectiva angulada (que es
/// justo lo que tenemos ahora para mirar al planeta), porque no
/// asume nada sobre cómo está parada la cámara.
/// </summary>
public class BrazoIK : MonoBehaviour
{
    [Header("Huesos")]
    [Tooltip("De dónde sale el brazo — normalmente un punto en la nave.")]
    [SerializeField] private Transform hombro;
    [SerializeField] private float largoSuperior = 0.4f;
    [SerializeField] private float largoInferior = 0.4f;

    [Tooltip("Para qué lado se dobla el codo. Probá -1 si te sale " +
        "doblado al revés de lo que esperás.")]
    [SerializeField] private int ladoCodo = 1;

    [Header("Visual: lineas de debug (opcional)")]
    [SerializeField] private LineRenderer lineaSuperior;
    [SerializeField] private LineRenderer lineaInferior;

    [Header("Visual: sprites (opcional)")]
    [Tooltip("El sprite del brazo superior. Su Pivot tiene que estar " +
        "en el borde (el que va pegado al hombro), NO al centro — " +
        "Sprite Editor > Pivot > Left (o Custom). Como la IK es " +
        "rigida, nunca se reescala: solo se mueve y rota.")]
    [SerializeField] private Transform spriteSuperior;
    [SerializeField] private Transform spriteInferior;

    [Tooltip("El objeto de la mano. Lo movemos desde acá y no desde el " +
        "script Mano porque los dos corren en LateUpdate, y entre dos " +
        "scripts no hay orden garantizado: la mano podría leer la " +
        "posición VIEJA del brazo y quedar un frame atrás. Todo lo que " +
        "cuelgue de ella (el sprite, las partículas) la sigue solo.")]
    [SerializeField] private Transform mano;

    [Tooltip("Grados de corrección si tu sprite NO está dibujado " +
        "mirando hacia la derecha (+X). Si lo dibujaste mirando hacia " +
        "arriba, probá -90. Si quedó al revés de lo que esperabas, " +
        "probá +90 en vez de -90.")]
    [SerializeField] private float offsetRotacionSprite = -90f;

    [Header("Delay")]
    [Tooltip("Cuánto tarda el brazo en alcanzar al mouse. 0 = pegado " +
        "al toque, como antes. Valores chicos (0.05-0.15) dan un " +
        "delay sutil; más alto se siente más \"elástico\" o pesado.")]
    [SerializeField] private float tiempoSuavizado = 0.08f;

    [Header("Cámara")]
    [Tooltip("Si está vacío, usa Camera.main.")]
    [SerializeField] private Camera camara;

    /// <summary>
    /// Si no es null, el brazo apunta ACÁ en vez de al mouse. Es lo
    /// mismo que aim_override en Godot: sirve para congelar el brazo
    /// señalando un botón de menú o un punto fijo, sin que el mouse
    // lo pueda mover mientras tanto.
    /// </summary>
    public Vector2? AimOverride { get; set; }

    /// <summary>Dónde quedó la mano este frame, en coordenadas de MUNDO.</summary>
    public Vector2 ManoGlobal { get; private set; }

    /// <summary>Dónde quedó el codo este frame, en coordenadas de MUNDO.</summary>
    public Vector2 CodoGlobal { get; private set; }

    private void Reset()
    {
        camara = Camera.main;
    }

    private void LateUpdate()
    {
        if (hombro == null)
        {
            return;
        }
        if (camara == null)
        {
            camara = Camera.main;
        }

        Vector2 objetivo = AimOverride ?? MousePlanoMundo();

        // el delay va ACÁ, sobre el objetivo, no sobre la mano
        // resultante — si suavizara la mano directamente, la distancia
        // codo-mano dejaría de ser exactamente largoInferior mientras
        // persigue al objetivo, y el antebrazo se vería "estirarse".
        // Suavizando antes de resolver la IK, el brazo sigue siendo
        // rígido en todo momento, solo que apunta con un poquito de
        // retraso.
        if (!_tieneObjetivoSuave || AimOverride.HasValue)
        {
            // Con AimOverride la mano va CLAVADA ahí, sin retraso: está
            // sosteniendo algo y tiene que quedar pegada al punto de
            // agarre. Si lo suavizáramos, se despegaría mientras la
            // pieza se mueve.
            _objetivoSuave = objetivo;
            _velocidadSuave = Vector2.zero;
            _tieneObjetivoSuave = true;
        }
        _objetivoSuave = Vector2.SmoothDamp(_objetivoSuave, objetivo, ref _velocidadSuave, tiempoSuavizado);

        // El freno contra lo sólido va DESPUÉS del suavizado y no se
        // guarda en él: si se guardara, el objetivo suave quedaría
        // pegado al borde y al sacar el mouse de la roca tardaría en
        // despegarse.
        ResolverIK(hombro.position, Bloquear(hombro.position, _objetivoSuave));
        ActualizarVisual();
    }

    // ------------------------------------------------------------------
    //  Choque de la mano contra lo sólido
    // ------------------------------------------------------------------

    [Header("Choque")]
    [Tooltip("Que la mano no pueda meterse adentro de las cosas: se " +
        "frena en el borde. Sin esto atraviesa las rocas y las agarra " +
        "desde adentro.")]
    [SerializeField] private bool chocar = true;

    [Tooltip("Qué capas frenan a la mano.")]
    [SerializeField] private LayerMask capasSolidas = ~0;

    [Tooltip("Cuánto se queda la mano antes del borde. Tiene que ser " +
        "MENOR que el Radio Agarre de la Mano, si no queda frenada " +
        "fuera de alcance de lo que toca.")]
    [SerializeField] private float margenChoque = 0.01f;

    [Tooltip("La nave: sus propios colliders no frenan al brazo. Vacío " +
        "= la busca hacia arriba en la jerarquía.")]
    [SerializeField] private Rigidbody2D nave;

    /// <summary>
    /// Lo que la mano tiene agarrado: tampoco la frena, porque la mano
    /// está pegada a eso. Lo setea la Mano.
    /// </summary>
    public Rigidbody2D Ignorar { get; set; }

    /// <summary>
    /// Cuánto sobresale de la mano la herramienta calzada, hacia adelante.
    /// El freno contra lo sólido cuenta esto, así la que queda en el
    /// borde es la PUNTA de la herramienta y no la mano. Lo setea la Mano.
    /// </summary>
    public float AlcanceExtra { get; set; }

    /// <summary>
    /// Medio ancho de la herramienta calzada. Con esto el freno pasa de
    /// ser una línea a ser un círculo de este radio: la herramienta no se
    /// puede colar en un agujero más angosto que ella. Lo setea la Mano.
    /// </summary>
    public float RadioExtra { get; set; }

    /// <summary>Dónde está el hombro, en mundo.</summary>
    public Vector2 Hombro => hombro != null ? (Vector2)hombro.position : (Vector2)transform.position;

    /// <summary>Hacia dónde mira la mano ahora (sin normalizar).</summary>
    public Vector2 Mira => HaciaDondeMira(Hombro, CodoGlobal, ManoGlobal);

    /// <summary>Hasta dónde llega el brazo estirado.</summary>
    public float AlcanceMax => largoSuperior + largoInferior;

    /// <summary>
    /// A dónde QUERÉS llevar la mano: el mouse, aunque la mano esté
    /// clavada en otro lado sosteniendo algo. Es lo que tironea de lo
    /// agarrado — si se midiera contra la mano, que ya está sobre la
    /// pieza, el error daría ~0 y no se movería nada.
    /// </summary>
    public Vector2 Intencion => MousePlanoMundo();

    [Tooltip("Escribe en la Console contra qué frena la mano, y lo " +
        "dibuja en magenta en la Scene.")]
    [SerializeField] private bool diagnostico;

    [Tooltip("La mano mira hacia el mouse (la línea del hombro) en vez " +
        "de seguir el ángulo del antebrazo.")]
    [SerializeField] private bool manoApunta = true;

    private Collider2D _ultimoChoque;
    private readonly RaycastHit2D[] _impactos = new RaycastHit2D[16];

    private void Awake()
    {
        if (nave == null)
        {
            nave = GetComponentInParent<Rigidbody2D>();
        }
    }

    /// <summary>
    /// Tira una línea del hombro al objetivo y, si se cruza con algo
    /// sólido, devuelve el punto donde entra — un pelito antes. Es lo
    /// que hace que la mano tope contra el borde en vez de atravesar.
    /// </summary>
    private Vector2 Bloquear(Vector2 desde, Vector2 hasta)
    {
        if (!chocar)
        {
            return hasta;
        }

        Vector2 d = hasta - desde;
        float largo = d.magnitude;
        if (largo < 0.00001f)
        {
            return hasta;
        }
        Vector2 dir = d / largo;

        // Con herramienta, un círculo de su ancho en vez de una línea: una
        // línea pasa por cualquier grieta, y la herramienta se metía
        // adentro de los pozos angostos que ella misma abría.
        float radio = Mathf.Max(0f, RadioExtra);

        // 1) la mano sola, del hombro hasta el objetivo
        float mejor = PrimerChoque(desde, dir, radio, largo);
        float hasta1 = mejor == float.MaxValue
            ? largo
            : Mathf.Clamp(mejor - margenChoque, 0f, largo);

        // 2) la herramienta, que sobresale hacia donde mira el ANTEBRAZO
        // y no hacia donde va la línea del hombro. Antes se estiraba la
        // línea del hombro, y si el brazo estaba doblado la punta
        // "chocaba" contra paredes a las que no apuntaba: la mano quedaba
        // frenada lejos, contra nada.
        float extra = Mathf.Max(0f, AlcanceExtra);
        if (extra <= 0f || PuntaLibre(desde, dir, hasta1, radio, extra))
        {
            return desde + dir * hasta1;
        }

        // Si la punta choca, se busca lo más lejos que se puede llegar
        // con la punta libre, partiendo el rango a la mitad.
        float bajo = 0f;
        float alto = hasta1;
        for (int i = 0; i < 8; i++)
        {
            float medio = (bajo + alto) * 0.5f;
            if (PuntaLibre(desde, dir, medio, radio, extra))
            {
                bajo = medio;
            }
            else
            {
                alto = medio;
            }
        }
        return desde + dir * bajo;
    }

    /// <summary>¿Con la mano a "t" del hombro, la herramienta entra sin chocar?</summary>
    private bool PuntaLibre(Vector2 desde, Vector2 dir, float t, float radio, float extra)
    {
        Vector2 objetivo = desde + dir * t;
        Pose(desde, objetivo, out Vector2 codo, out Vector2 mano);
        Vector2 mira = HaciaDondeMira(desde, codo, mano);
        if (mira.sqrMagnitude < 0.0000001f)
        {
            return true;
        }
        return PrimerChoque(mano, mira.normalized, radio, extra + margenChoque) == float.MaxValue;
    }

    /// <summary>
    /// Para dónde mira la mano (y lo que tenga calzado). Con "Mano Apunta"
    /// mira en la línea del hombro, que es la línea hacia el mouse: la
    /// herramienta va derecho a donde vas y entra recta en los pozos. Sin
    /// eso sigue al antebrazo, y con el codo doblado queda en diagonal.
    /// </summary>
    private Vector2 HaciaDondeMira(Vector2 h, Vector2 codo, Vector2 mano)
    {
        return manoApunta ? mano - h : mano - codo;
    }

    /// <summary>
    /// A qué distancia choca un círculo de "radio" (0 = una línea) lanzado
    /// desde "desde". float.MaxValue si no choca con nada que frene.
    /// </summary>
    private float PrimerChoque(Vector2 desde, Vector2 dir, float radio, float largo)
    {
        var filtro = new ContactFilter2D();
        filtro.SetLayerMask(capasSolidas);
        filtro.useTriggers = false;

        int n = radio > 0f
            ? Physics2D.CircleCast(desde, radio, dir, filtro, _impactos, largo)
            : Physics2D.Raycast(desde, dir, filtro, _impactos, largo);

        // se busca el más cercano a mano en vez de confiar en el orden
        // en que vienen los resultados
        float mejor = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            RaycastHit2D h = _impactos[i];
            // fraction 0 = arranca adentro de eso. Pasa si la nave ya
            // quedó encimada con algo: frenar ahí dejaría la mano clavada.
            if (h.fraction <= 0f || !Frena(h.collider))
            {
                continue;
            }
            if (h.distance < mejor)
            {
                mejor = h.distance;
                _ultimoChoque = h.collider;
            }
        }
        if (diagnostico && mejor != float.MaxValue)
        {
            Debug.DrawLine(desde, desde + dir * mejor, Color.magenta);
            Debug.Log($"[Brazo] frena contra '{_ultimoChoque.name}' " +
                $"(padre: {(_ultimoChoque.transform.parent != null ? _ultimoChoque.transform.parent.name : "-")})",
                _ultimoChoque);
        }
        return mejor;
    }

    private bool Frena(Collider2D c)
    {
        Rigidbody2D rb = c.attachedRigidbody;
        if (rb != null && (rb == nave || rb == Ignorar))
        {
            return false;
        }
        // Los escombros tampoco: la aspiradora los junta justo en la
        // punta, y si frenaran, empujarían la mano para afuera del pozo
        // — cada frame un poquito, hasta sacarla al borde.
        if (c.GetComponentInParent<Escombro>() != null)
        {
            return false;
        }
        // los módulos y herramientas no frenan: se agarran desde
        // cualquier punto, y no tiene sentido chocar contra ellos
        return c.GetComponentInParent<Modulo>() == null;
    }

    private Vector2 _objetivoSuave;
    private Vector2 _velocidadSuave;
    private bool _tieneObjetivoSuave;

    /// <summary>
    /// Convierte la posición del mouse en pantalla a un punto en el
    /// MUNDO, sobre el plano XY en la altura Z del hombro — así la
    /// mano siempre cae justo en el plano donde vive la nave, sin
    /// importar el ángulo de la cámara.
    /// </summary>
    private Vector2 MousePlanoMundo()
    {
        if (camara == null || Mouse.current == null)
        {
            return hombro.position;
        }

        Vector2 mousePix = Mouse.current.position.ReadValue();
        Ray ray = camara.ScreenPointToRay(mousePix);
        Plane plano = new Plane(Vector3.forward, new Vector3(0f, 0f, hombro.position.z));

        if (plano.Raycast(ray, out float distancia))
        {
            return ray.GetPoint(distancia);
        }
        return hombro.position;
    }

    /// <summary>
    /// La ley de cosenos de siempre. Con el hombro, el objetivo y los
    /// dos largos, hay un solo triángulo posible — esto arma ese
    /// triángulo. Si el objetivo está fuera de alcance (muy lejos o
    /// muy cerca, adentro de la "zona muerta" entre |L1-L2| y 0), se
    /// recorta a la distancia alcanzable más próxima en esa dirección,
    /// así el brazo queda estirado o doblado al máximo en vez de roto.
    /// </summary>
    private void ResolverIK(Vector2 origen, Vector2 objetivo)
    {
        Pose(origen, objetivo, out Vector2 codo, out Vector2 mano);
        CodoGlobal = codo;
        ManoGlobal = mano;
    }

    private void Pose(Vector2 origen, Vector2 objetivo, out Vector2 codo, out Vector2 mano)
    {
        Vector2 haciaObjetivo = objetivo - origen;
        float distancia = haciaObjetivo.magnitude;

        float alcanceMax = largoSuperior + largoInferior;
        float alcanceMin = Mathf.Abs(largoSuperior - largoInferior);
        float d = Mathf.Clamp(distancia, alcanceMin + 0.0001f, alcanceMax - 0.0001f);

        Vector2 direccion = distancia > 0.0001f ? haciaObjetivo / distancia : Vector2.right;
        mano = origen + direccion * d;

        float cosHombro = (largoSuperior * largoSuperior + d * d - largoInferior * largoInferior)
            / (2f * largoSuperior * d);
        cosHombro = Mathf.Clamp(cosHombro, -1f, 1f);
        float anguloHombro = Mathf.Acos(cosHombro);

        float anguloDireccion = Mathf.Atan2(direccion.y, direccion.x);
        float anguloBrazo = anguloDireccion + ladoCodo * anguloHombro;

        codo = origen + new Vector2(Mathf.Cos(anguloBrazo), Mathf.Sin(anguloBrazo)) * largoSuperior;
    }

    private void ActualizarVisual()
    {
        if (lineaSuperior != null)
        {
            lineaSuperior.positionCount = 2;
            lineaSuperior.SetPosition(0, hombro.position);
            lineaSuperior.SetPosition(1, CodoGlobal);
        }
        if (lineaInferior != null)
        {
            lineaInferior.positionCount = 2;
            lineaInferior.SetPosition(0, CodoGlobal);
            lineaInferior.SetPosition(1, ManoGlobal);
        }

        if (spriteSuperior != null)
        {
            PosicionarSprite(spriteSuperior, hombro.position, CodoGlobal);
        }
        if (spriteInferior != null)
        {
            PosicionarSprite(spriteInferior, CodoGlobal, ManoGlobal);
        }

        if (mano != null)
        {
            // La mano va en la punta, mirando como mira el antebrazo.
            // La Z se respeta: define en qué capa se dibuja y no tiene
            // nada que ver con la pose del brazo.
            Vector3 p = ManoGlobal;
            p.z = mano.position.z;
            mano.position = p;

            Vector2 dir = HaciaDondeMira(hombro.position, CodoGlobal, ManoGlobal);
            if (dir.sqrMagnitude > 0.000001f)
            {
                float angulo = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                mano.rotation = Quaternion.Euler(0f, 0f, angulo + offsetRotacionSprite);
            }
        }
    }

    /// <summary>
    /// Pone el sprite con su borde (el Pivot) en "desde", mirando hacia
    /// "hasta". Nunca toca la Scale: como el largo entre desde y hasta
    /// es siempre el mismo (lo fija la IK), el dibujo ya calza solo
    /// mientras lo hayas armado del tamaño correcto en el editor.
    /// </summary>
    private void PosicionarSprite(Transform sprite, Vector2 desde, Vector2 hasta)
    {
        // Ojo con asignar un Vector2 a .position: C# lo convierte a
        // Vector3 poniendo Z = 0, así que cada frame le borraría la
        // profundidad que acomodaste en el editor. La Z no es parte de
        // la pose del brazo — define qué sprite tapa a cuál — así que
        // se respeta la que tenga.
        sprite.position = new Vector3(desde.x, desde.y, sprite.position.z);

        Vector2 delta = hasta - desde;
        float angulo = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        sprite.rotation = Quaternion.Euler(0f, 0f, angulo + offsetRotacionSprite);
    }

    private void OnDrawGizmosSelected()
    {
        if (hombro == null)
        {
            return;
        }
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(hombro.position, 0.03f);
        Gizmos.DrawLine(hombro.position, CodoGlobal);
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(CodoGlobal, ManoGlobal);
        Gizmos.DrawWireSphere(ManoGlobal, 0.03f);
    }
}
