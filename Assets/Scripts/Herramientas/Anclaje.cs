using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// El anclaje: calzado en la mano, con el clic DERECHO dispara el perno
/// (el sprite "Anclaje") hacia donde apunta, y si choca con algo se
/// queda clavado ahí, moviéndose junto con eso. Otro clic derecho lo
/// recoge de vuelta.
///
/// El perno no es un Rigidbody: a esta escala y a esta velocidad, un
/// cuerpo físico atravesaría las rocas entre un paso y otro. En vez de
/// eso, cada paso tira un rayo del largo de lo que avanza: si el rayo
/// toca algo, ahí se clava. No se puede pasar de largo.
///
/// Igual es física de verdad: al disparar, la nave recibe el retroceso,
/// y al clavarse le pega a la roca con el impulso del perno.
///
/// Va en el objeto de la herramienta, junto a su Modulo.
/// </summary>
public class Anclaje : Herramienta
{
    [Header("Perno")]
    [Tooltip("El sprite que sale disparado (el hijo \"Anclaje\").")]
    [SerializeField] private Transform perno;

    [Tooltip("Unidades por segundo. La nave mide ~0.5.")]
    [SerializeField] private float velocidad = 4f;

    [Tooltip("Hasta dónde llega. Si no toca nada, vuelve solo.")]
    [SerializeField] private float alcance = 1.5f;

    [Tooltip("Cuánto se entierra en lo que toca, para que se vea clavado.")]
    [SerializeField] private float profundidad = 0.01f;

    [Tooltip("Qué capas lo frenan.")]
    [SerializeField] private LayerMask capas = ~0;

    [Header("Física")]
    [Tooltip("La masa del perno, en kg. Con esto se calcula el retroceso " +
        "que recibe la nave y el golpe que recibe la roca.")]
    [SerializeField] private float masaPerno = 2f;

    private enum Estado { Guardado, Volando, Clavado }
    private Estado _estado = Estado.Guardado;

    // dónde va el perno guardado, respecto a la herramienta
    private Vector3 _posGuardado;
    private Quaternion _rotGuardado;
    // Al soltarse en el mundo, Unity le pasa la escala a valores de
    // mundo; al volver hay que devolverle la suya, si no queda chiquito
    private Vector3 _escalaGuardado = Vector3.one;

    // dónde está la punta con el perno guardado, respecto a la herramienta
    private Vector3 _puntaGuardada;

    /// <summary>
    /// Para el brazo, la punta es siempre la del perno GUARDADO: si la
    /// punta viaja con el perno, el brazo creería que la herramienta mide
    /// metros y se frenaría contra cualquier cosa (el parpadeo).
    /// </summary>
    public override Vector2 PosicionPunta => punta != null
        ? (Vector2)transform.TransformPoint(_puntaGuardada)
        : (Vector2)transform.position;

    private Vector2 _dir;
    private float _recorrido;

    // la Z con la que salió: la conserva todo el viaje y clavado, así
    // sigue dibujándose entre las mismas capas
    private float _z;

    [Tooltip("Escribe en la Console qué hace el perno y contra qué choca.")]
    [SerializeField] private bool diagnostico;

    // a qué quedó clavado, y en qué lugar de eso (en su espacio local)
    private Transform _clavadoEn;
    private Vector3 _posEnClavado;
    private Quaternion _rotEnClavado;

    private readonly RaycastHit2D[] _impactos = new RaycastHit2D[8];

    /// <summary>¿Está clavado en algo?</summary>
    public bool Clavado => _estado == Estado.Clavado;

    /// <summary>Lo que tiene clavado, o null.</summary>
    public Transform ClavadoEn => _clavadoEn;

    /// <summary>Dónde está el perno, en mundo.</summary>
    public Vector2 PosicionPerno => perno != null ? (Vector2)perno.position : (Vector2)transform.position;

    protected override void Awake()
    {
        base.Awake();
        if (perno != null)
        {
            _posGuardado = perno.localPosition;
            _rotGuardado = perno.localRotation;
            _escalaGuardado = perno.localScale;
        }
        if (punta != null)
        {
            _puntaGuardada = transform.InverseTransformPoint(punta.position);
        }
    }

    private void Update()
    {
        if (perno == null)
        {
            return;
        }

        if (EnLaMano && Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
        {
            if (_estado == Estado.Guardado)
            {
                Disparar();
            }
            else
            {
                Guardar();
            }
        }

        // Si la sueltan de la mano con el perno afuera, vuelve: si no,
        // quedaría un perno huérfano clavado en una roca.
        if (!EnLaMano && _estado != Estado.Guardado)
        {
            Guardar();
        }
    }

    private void FixedUpdate()
    {
        if (_estado == Estado.Volando)
        {
            Volar(Time.fixedDeltaTime);
        }
    }

    private void LateUpdate()
    {
        if (_estado != Estado.Clavado)
        {
            return;
        }
        // lo que tenía clavado desapareció (se lo comió el taladro)
        if (_clavadoEn == null)
        {
            Guardar();
            return;
        }
        // sigue a la roca: se mueve y gira con ella
        Vector3 p = _clavadoEn.TransformPoint(_posEnClavado);
        p.z = _z;
        perno.SetPositionAndRotation(p,
            _clavadoEn.rotation * _rotEnClavado);
    }

    private void Disparar()
    {
        _dir = Direccion();
        _recorrido = 0f;
        _z = perno.position.z;
        Log($"disparo hacia {_dir}");
        _estado = Estado.Volando;

        // suelto en el mundo: desde ahora no sigue a la mano
        perno.SetParent(null, worldPositionStays: true);

        // retroceso: el perno sale con m·v para adelante, la nave recibe
        // lo mismo para atrás
        Rigidbody2D nave = Usuario != null ? Usuario.Nave : null;
        if (nave != null)
        {
            nave.AddForceAtPosition(-_dir * masaPerno * velocidad, transform.position,
                ForceMode2D.Impulse);
        }
    }

    private void Volar(float dt)
    {
        float paso = velocidad * dt;
        Vector2 desde = perno.position;

        var filtro = new ContactFilter2D();
        filtro.SetLayerMask(capas);
        filtro.useTriggers = false;
        int n = Physics2D.Raycast(desde, _dir, filtro, _impactos, paso);

        float mejor = float.MaxValue;
        RaycastHit2D golpe = default;
        for (int i = 0; i < n; i++)
        {
            RaycastHit2D h = _impactos[i];
            // fraction 0 = el rayo arranca adentro de eso (el propio
            // perno, o algo encimado al lanzador): no cuenta
            if (h.fraction <= 0f || !Frena(h.collider) || h.distance >= mejor)
            {
                continue;
            }
            mejor = h.distance;
            golpe = h;
        }

        if (golpe.collider != null)
        {
            Clavar(golpe);
            return;
        }

        perno.position += (Vector3)(_dir * paso);
        _recorrido += paso;
        if (_recorrido >= alcance)
        {
            Guardar();
        }
    }

    private bool Frena(Collider2D c)
    {
        Rigidbody2D rb = c.attachedRigidbody;
        Rigidbody2D nave = Usuario != null ? Usuario.Nave : null;
        if (rb != null && rb == nave)
        {
            return false;
        }
        // ni módulos ni herramientas (incluida esta)
        return c.GetComponentInParent<Modulo>() == null;
    }

    private void Clavar(RaycastHit2D golpe)
    {
        Vector2 donde = golpe.point + _dir * profundidad;
        Vector3 p = new Vector3(donde.x, donde.y, _z);
        Log($"clavado en '{golpe.collider.name}'");
        perno.position = p;

        Rigidbody2D rb = golpe.rigidbody;
        _clavadoEn = rb != null ? rb.transform : golpe.collider.transform;
        _posEnClavado = _clavadoEn.InverseTransformPoint(p);
        _rotEnClavado = Quaternion.Inverse(_clavadoEn.rotation) * perno.rotation;
        _estado = Estado.Clavado;

        // el golpe: le entrega a la roca el impulso que traía el perno
        if (rb != null)
        {
            rb.AddForceAtPosition(_dir * masaPerno * velocidad, golpe.point, ForceMode2D.Impulse);
        }
    }

    private void Log(string que)
    {
        if (diagnostico)
        {
            Debug.Log($"[Anclaje] {que} | perno en {perno.position}", perno);
        }
    }

    private void Guardar()
    {
        Log("vuelve");
        _estado = Estado.Guardado;
        _clavadoEn = null;
        perno.SetParent(transform, worldPositionStays: false);
        perno.localPosition = _posGuardado;
        perno.localRotation = _rotGuardado;
        perno.localScale = _escalaGuardado;
    }

    private void OnDestroy()
    {
        // si la herramienta se destruye con el perno afuera, que no quede tirado
        if (perno != null && perno.parent != transform)
        {
            Destroy(perno.gameObject);
        }
    }
}
