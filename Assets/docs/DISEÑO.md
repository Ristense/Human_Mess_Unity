# Human Mess — notas de diseño

Documento vivo. Acá se anota la idea a medida que se habla, no es spec cerrada.

## Concepto

Salvataje espacial con **física real**. Sos un condenado a muerte que se
compra semanas de vida juntando chatarra en órbita.

El planeta da igual cuál sea — lo importante es la órbita y la chatarra.

### Principio que manda sobre todo lo demás

**Todo es física de verdad.** Masa real, espacio newtoniano, sin roce. Lo
pesado cuesta porque F = m·a, no porque haya una regla que lo diga. Si
algo se siente raro, se arregla la física, no se le pone un parche.

## La nave — modular

- Tiene un **casco** con **zonas**: grillas de casilleros donde se
  enchufan módulos.
- Los módulos se **anidan**: una herramienta va dentro de un módulo de
  herramientas, que va dentro de la nave. Sacás el módulo y se va con
  las herramientas puestas.
- Cada zona acepta un **tipo** (`modulo`, `herramienta`, ...) y cada
  módulo ocupa **cols × rows** casilleros.
- Montado, un módulo **deja de ser un cuerpo aparte**: es parte del casco.
  Su masa se suma a la nave.
- Cada zona tiene una **luz** que avisa si está libre u ocupada.

### Módulos que existen

| Módulo | Qué hace | Estado |
|---|---|---|
| Módulo de herramientas | portaherramientas de 4 casilleros | hecho |
| Anclaje | herramienta | arte hecho |
| Taladro | herramienta (venía de Godot) | arte hecho |
| Expansión de peso | +2000 kg de capacidad de carga, con medidor | hecho |
| Cuerda | carrete con gancho | arte en proceso |

### Ideas de módulos (sin decidir)

Los buenos son los que **te meten en un problema físico**, no los que suman
un número:

- **Propulsor auxiliar** — empuje extra; montado fuera del eje te da torque.
- **Giroscopio** — girás sin combustible, pero te sube la inercia.
- **Lastre** — solo mueve el centro de masa.
- **Cuerda con winche** — dos masas que se arrastran mutuamente.
- **Electroimán** — atrae chatarra, y por la tercera ley te atrae a vos.
- **Pinza hidráulica**, **soplete**, **soldadora**, **procesadora**.
- **Panel solar**, **batería**, **escáner**, **foco**, **baliza**.

Idea temática: los módulos son **alquilados** — si volvés sin uno, lo
pagás. Perder una herramienta en el espacio duele de verdad.

## Movimiento — tanque

- Controles de tanque: **W/S** empuje, **A/D** giro.
- **Espacio** = estabilizador: contra-fuerza real que frena, con lo que
  pese la nave. Al llegar casi a cero corta en seco, si no quedaría
  derivando un resto para siempre.
- El brazo es independiente del chasis: el chasis apunta con el cuerpo,
  el brazo con el mouse.

## El brazo y la mano

- Brazo de **dos huesos con IK**, sigue al mouse con un poco de retraso.
- **Clic sostenido** = agarrar. La mano tira de la pieza con un resorte
  aplicado **en el punto que tocaste**, así que la pieza pivotea.
- **Tercera ley**: lo que la mano le hace a la pieza, la pieza se lo hace
  a la nave. Agarrás un asteroide pesado y te arrastra a vos.
- Lo liviano agarrado no gira (se congela la rotación mientras lo tenés).
- Lo que está bajo la mano se **resalta con un contorno**.
- **E sostenida** (con barra de carga):
  - con algo en la mano → se lo calza a la mano, o lo ancla en el
    casillero más cercano si en la mano no entra
  - con la mano vacía → se saca lo equipado
- El clic solo agarra y suelta. Montar es trabajo de la E.

## Peso y carga

Dos números distintos, que no hay que mezclar:

- **Lo que pesás** = casco + módulos montados + material. Va a la física:
  cargado acelerás y frenás peor.
- **Cuánto material te entra** = 1000 kg base + lo que sumen los módulos
  de expansión. La barra del medidor mide **solo el material**.

Los asteroides pesan más cuanto más grandes, pero solo un 15% de lo que
diría la física real — al 100% los grandes serían inamovibles.

### Materiales (ideas)

- **Placas de metal**
- **Escombro**

## El mapa

- Grilla de **3 × 3 sectores** de 150 × 50.
- La **base** está en el sector de abajo al medio. Arrancás ahí.
- ~300 objetos sembrados (asteroides con variedad de forma, tamaño y
  color, y satélites).

## La base

- Ahí volvés a **descargar** lo que juntaste.
- Es el hub: loadout, descarga, probablemente progresión.

## Desguace vs. reparación

- No todo es basura para triturar. Hay piezas que **conviene reparar**.
- Esas piezas se guardan aparte, no se "picadillan" con el resto.
- Dos destinos por pieza: triturar (materia prima) o conservar entera.

## HUD

- **Velocímetro** radial.
- **Barra de carga de la E**, siguiendo a la mano.
- **Medidor de material** en el módulo de expansión.
- Textos: peso, sector, carga, créditos.

## Visual

- Planeta 3D rotando de fondo, con parallax: se mueve con vos pero se va
  quedando atrás.
- Fondo de estrellas procedural e infinito.
- Neón con bloom para las luces; cinta de peligro animada en la base.

## Abierto / sin resolver todavía

- **¿Hay fail state?** La versión anterior decía "sin fail state, sin
  presión de tiempo", pero la historia del condenado comprando semanas
  de vida implica un reloj. Hay que decidir cuál de las dos manda.
- **¿Hay plata?** Se había descartado la tienda, pero ahora hay créditos
  en el HUD y la base compraría la carga. Qué se compra con eso.
- Cómo se genera material: ¿el taladro saca escombros de los asteroides?
- Qué hace el brazo con una pieza reparable vs. una para desguazar.
- Qué pasa en la base además de descargar.
- Si los sectores tienen dificultad creciente o algo especial.

## Descartado

- El clicker de tienda/mejoras de la primera versión.
- El disparo de dos clics para soldar — reemplazado por el brazo IK.
- Godot: el proyecto se pasó a Unity 6 por comodidad, no por una
  limitación del motor. El de Godot queda como referencia.
