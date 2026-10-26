using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Los chorritos de gas de maniobra (el "pssst" de los propulsores
/// reales). Cada tecla prende sus ParticleSystem mientras esté apretada
/// y los apaga al soltarla. W y S no hacen nada acá: el motor principal
/// ya tiene lo suyo.
///
/// Cada emisor es un ParticleSystem hijo de la nave, puesto en el
/// costado de donde tiene que SALIR el gas, mirando hacia afuera. El
/// gas sale al lado contrario del empuje: para girar a la derecha,
/// el chorro sale de la izquierda.
/// </summary>
public class ChorrosGas : MonoBehaviour
{
    [Header("Giro (A / D)")]
    [SerializeField] private ParticleSystem[] giroIzquierda;
    [SerializeField] private ParticleSystem[] giroDerecha;

    [Header("Costado (Q / E)")]
    [SerializeField] private ParticleSystem[] costadoQ;
    [SerializeField] private ParticleSystem[] costadoE;

    private readonly HashSet<ParticleSystem> _quieren = new HashSet<ParticleSystem>();
    private readonly List<ParticleSystem> _todos = new List<ParticleSystem>();

    private void Update()
    {
        Keyboard k = Keyboard.current;
        bool a = k != null && (k.aKey.isPressed || k.leftArrowKey.isPressed);
        bool d = k != null && (k.dKey.isPressed || k.rightArrowKey.isPressed);
        bool q = k != null && k.qKey.isPressed;
        bool e = k != null && k.eKey.isPressed;

        // Un mismo sistema puede estar en varias listas (el chorro de la
        // esquina sirve para girar y para el costado). Por eso primero se
        // junta todo lo que ALGUNA lista pide y recién después se
        // prende o apaga: si cada lista decidiera por su cuenta, una lo
        // apagaría y otra lo prendería en el mismo frame.
        _quieren.Clear();
        _todos.Clear();
        Juntar(giroIzquierda, a && !d);
        Juntar(giroDerecha, d && !a);
        Juntar(costadoQ, q && !e);
        Juntar(costadoE, e && !q);

        foreach (ParticleSystem ps in _todos)
        {
            Poner(ps, _quieren.Contains(ps));
        }
    }

    private void Juntar(ParticleSystem[] sistemas, bool activo)
    {
        if (sistemas == null)
        {
            return;
        }
        foreach (ParticleSystem ps in sistemas)
        {
            if (ps == null)
            {
                continue;
            }
            _todos.Add(ps);
            if (activo)
            {
                _quieren.Add(ps);
            }
        }
    }

    // Se prende y apaga la EMISIÓN, no el objeto: así las partículas ya
    // nacidas terminan su vida y el chorro se desvanece en vez de
    // cortarse de golpe.
    private static void Poner(ParticleSystem ps, bool activo)
    {
        if (activo && !ps.isEmitting)
        {
            ps.Play();
        }
        else if (!activo && ps.isEmitting)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}
