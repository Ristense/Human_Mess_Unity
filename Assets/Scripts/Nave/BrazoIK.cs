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
        if (!_tieneObjetivoSuave)
        {
            _objetivoSuave = objetivo;
            _tieneObjetivoSuave = true;
        }
        _objetivoSuave = Vector2.SmoothDamp(_objetivoSuave, objetivo, ref _velocidadSuave, tiempoSuavizado);

        ResolverIK(hombro.position, _objetivoSuave);
        ActualizarVisual();
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
        Vector2 haciaObjetivo = objetivo - origen;
        float distancia = haciaObjetivo.magnitude;

        float alcanceMax = largoSuperior + largoInferior;
        float alcanceMin = Mathf.Abs(largoSuperior - largoInferior);
        float d = Mathf.Clamp(distancia, alcanceMin + 0.0001f, alcanceMax - 0.0001f);

        Vector2 direccion = distancia > 0.0001f ? haciaObjetivo / distancia : Vector2.right;
        Vector2 mano = origen + direccion * d;

        float cosHombro = (largoSuperior * largoSuperior + d * d - largoInferior * largoInferior)
            / (2f * largoSuperior * d);
        cosHombro = Mathf.Clamp(cosHombro, -1f, 1f);
        float anguloHombro = Mathf.Acos(cosHombro);

        float anguloDireccion = Mathf.Atan2(direccion.y, direccion.x);
        float anguloBrazo = anguloDireccion + ladoCodo * anguloHombro;

        Vector2 codo = origen + new Vector2(Mathf.Cos(anguloBrazo), Mathf.Sin(anguloBrazo)) * largoSuperior;

        CodoGlobal = codo;
        ManoGlobal = mano;
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

            Vector2 dir = ManoGlobal - CodoGlobal;
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
