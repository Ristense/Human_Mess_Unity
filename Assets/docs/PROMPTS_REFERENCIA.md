# Human Mess — prompts para imágenes de referencia

Prompts para generar referencia visual del juego. **No son para generar
assets finales** — el arte del juego son polígonos planos hechos en el
editor. Esto es para tener un norte: paleta, siluetas, ambiente.

Los prompts van en inglés porque los generadores responden mejor así.
Los comentarios en español son para vos.

---

## La paleta (va en casi todos los prompts)

Sale de [`palette.gd`](../scripts/palette.gd), no es inventada:

| rol | hex | dónde se usa |
|---|---|---|
| `VOID` | `#080b14` | el vacío, fondo |
| `DEEP` | `#0e1322` | fondo secundario |
| `PANEL` | `#141a2b` | paneles de HUD |
| `LINE` | `#232c45` | grillas, bordes |
| `BONE` | `#e6e2d6` | blanco hueso, texto |
| `DIM` | `#8892ab` | gris azulado |
| `AMBER` | `#e9a63c` | **color de trabajo** |
| `DANGER` | `#d9503f` | solo peligro |
| `SAFE` | `#79b48a` | verde, estado OK |
| `ION` | `#6fd3e0` | cian, energía |

**Bloque para pegar al final de cualquier prompt:**

```
strict limited palette: near-black navy #080b14 background, dark slate
#141a2b panels, cool grey-blue #8892ab, bone white #e6e2d6, amber
#e9a63c as the only warm accent, cyan #6fd3e0 for energy, red #d9503f
used sparingly for danger only. flat vector shapes, no gradients, no
lens flare, no photorealism.
```

**Bloque negativo — pegalo también, si el generador lo acepta aparte:**

```
no photorealism, no 3D render, no lens flare, no nebula, no starfield,
no glowing neon, no clean shiny spaceship, no heroic sci-fi, no weapons,
no explosions, no text, no UI overlay, no gradients, no soft shadows,
no depth of field
```

---

## 1. Key art — de qué se trata el juego

Si vas a generar una sola imagen, generá esta. Es la que tiene que
explicar el juego sin texto.

```
A small ugly salvage ship alone in orbit, gripping a torn satellite
with a long mechanical arm. The ship is cobbled together from
mismatched rectangular modules bolted to a central hull. Around it, a
slow cloud of scrap: bent panels, loose cable, a cracked fuel tank.
Below, the curve of a grey planet, featureless. Lonely, industrial,
patient. Nothing is exploding.
[+ bloque de paleta]
```

## 2. Ambiente general

La idea rectora del juego: **pantalla de seguimiento orbital**. No es
space opera, es un monitor técnico.

```
Orbital debris cleanup sandbox, 2D top-down view. A dark cluttered
orbit filled with slowly tumbling junk: broken panels, bent structural
beams, dead satellites, torn foil. Everything drifts weightlessly with
no up or down. The mood is patient industrial salvage work, not combat
— no explosions, no enemies. Reads like a technical tracking display
rather than a movie space scene. Flat vector illustration, thin
outlines, large readable silhouettes.
[+ bloque de paleta]
```

## 3. La nave modular

Chasis con hub central y módulos que se enchufan en slots. Controles de
tanque, o sea que la silueta tiene que leer hacia dónde apunta.

```
Small modular salvage spacecraft seen from directly above, flat 2D.
A compact central hull with a clear pointed front, and rectangular
module slots on both flanks where cargo and tool modules bolt on.
The modules are obviously detachable blocks, in a visible grid, not
smooth hull. Utilitarian and boxy, welded and patched, nothing
aerodynamic — it never enters atmosphere. Chunky readable silhouette.
[+ bloque de paleta]
```

**Variante — el loadout:**

```
Technical exploded diagram of a modular spacecraft, flat 2D top-down.
The central hull in the middle, and detached square modules floating
around it in a grid arrangement, each a different function: cargo
container, tool rack, battery. Thin connector lines showing where each
one attaches. Blueprint-like, labeled slots, orthographic.
[+ bloque de paleta]
```

## 4. El brazo

Brazo de 2 huesos con IK que sigue al mouse. Hombro → codo → mano.

```
Articulated two-segment robotic arm mounted on the nose of a small
spacecraft, flat 2D side-on view. Upper arm and forearm of similar
length with a visible elbow joint, ending in a simple gripper claw.
Thin industrial limb, exposed pistons and cables, no armor plating.
Shown mid-reach, bent at the elbow, grabbing a chunk of floating
debris. The arm is thin and the debris is much bigger than the hand.
[+ bloque de paleta]
```

## 5. Las herramientas

Tres taladros y una sierra, montados en la mano del brazo.

```
Set of four industrial handheld space tools laid out in a row, flat 2D
technical illustration, orthographic side view:
1. an anchor drill — a heavy spike that bolts the operator to a rock
2. a digging drill — a wide-mouthed auger that chews material and
   sucks the chips back in
3. a cutting saw — an exposed circular blade on a short handle
4. a basic drill — a simple pointed bit
All share the same design language: stubby, bare mechanism, chunky grip
meant for a robot claw, no ergonomic curves. Amber accents mark the
working end of each one.
[+ bloque de paleta]
```

**El ancla en uso** (es la herramienta conceptualmente más importante —
resuelve que en el espacio no hay fricción):

```
Flat 2D illustration: a small spacecraft bolted to a large asteroid by
a rigid anchor spike, tethered and stable while its robotic arm works
on the rock surface. The ship is dwarfed by the asteroid. Show the
tension of being physically pinned to something enormous.
[+ bloque de paleta]
```

## 6. La cuerda

Cadena de eslabones con física, con una punta en cada extremo que se
engancha a sockets.

```
Flat 2D illustration of a segmented industrial tether floating in zero
gravity: a chain of short rigid links connected by visible pin joints,
curving loosely in a slack S-curve. A distinct connector tip at each
end, drawn as a small blunt plug. Mechanical and jointed, clearly not
soft rope or cable. Shown drifting, not taut.
[+ bloque de paleta]
```

## 7. Los escombros y el tallado

La sierra y el taladro **le restan polígono de verdad** al objeto. La
referencia tiene que mostrar el corte, no la explosión.

```
Flat 2D illustration of a large irregular asteroid with a deep notch
physically carved out of one side, as if a saw had eaten a wedge from
its outline. Sharp faceted cut faces contrasting with the rough natural
edge. Small angular chips and fragments drifting away from the cut,
in the same colour as the parent rock. The hole is a real change to the
silhouette, not a texture or a scorch mark.
[+ bloque de paleta]
```

**Campo de asteroides:**

```
Flat 2D top-down asteroid field, sparse not crowded. Chunky irregular
rocks of three clear size tiers — small pebbles, mid chunks, and a few
huge slow ones — tumbling at different rates across a near-black navy
void. Each rock is a simple faceted polygon with two or three flat
shades, no texture detail. Lots of empty space between them.
[+ bloque de paleta]
```

**Escombros sueltos y cableado** (lo que aspira el taladro, y el
cableado que se corta y queda como cuerda usable):

```
Scattered small debris floating in zero gravity: bolts, bent metal
shards, torn foil, broken circuit board fragments, a loose glove, and a
long loose cable coiling slowly through the middle of the frame. Flat
angular shapes, no perspective, all roughly the same small scale, drifting
apart at different speeds.
[+ bloque de paleta]
```

## 8. Satélite para desguazar

Que se le vean **las uniones**: la idea es que mirando la pieza
entiendas por dónde se desarma. Eso es la mecánica dibujada.

```
A dead communications satellite drifting, seen from above. A boxy
central body, two long solar panel wings with several cells cracked or
missing, a dish antenna bent out of alignment, and a bundle of exposed
wiring spilling from a torn access panel. Every joint and bolt clearly
visible, as if drawn to show how it comes apart.
[+ bloque de paleta]
```

## 9. La cápsula tripulada

De acá sale el nombre del juego. **Lo que no se ve es lo que pega** — si
el generador te mete algo explícito, bajale el tono hasta que quede solo
la ausencia.

```
A small crew re-entry capsule, scorched and cracked open along one
seam, drifting. Through the gap, the inside is dark: torn seat webbing,
loose personal objects, a floating clipboard. Nothing graphic, nothing
visible. The restraint is the point.
[+ bloque de paleta]
```

## 10. La nave nodriza

Adonde volvés a depositar lo que juntaste.

```
Flat 2D illustration of a large industrial mothership in orbit, seen
from above, with a small salvage craft approaching its open docking
bay. The mothership is long and utilitarian — storage racks, cranes,
processing drums — built for sorting scrap, not for combat. Scale
contrast is the point: the player ship is tiny beside it.
[+ bloque de paleta]
```

## 11. El HUD

Ya hay `PesoLabel`, `SectorLabel` y la carga de la bodega. Es un monitor
técnico, no un HUD de juego de acción.

```
Flat 2D UI mockup of a minimal technical HUD overlay for a space
salvage game. Thin monospaced readouts in the top-left corner showing
mass in kilograms, a sector coordinate like "Sector 0, 0", and a thin
cargo fill bar. A small 3x3 sector minimap of thin grid lines with the
current cell marked. No health bar, no ammo counter, no crosshair.
Sparse, quiet, lots of empty screen. Amber for active values, cool grey
for idle.
[+ bloque de paleta]
```

## 12. El condenado — solo para menú y portada

El contador en el antebrazo es el juego entero en un objeto: **salvás
chatarra para comprar semanas de vida.**

Ojo que esto es el único prompt donde aparece una persona, y por eso va
al final: **es para pantallas de menú, nunca para el mundo del juego.**
Adentro de la órbita no hay nadie.

```
Character concept sheet: a convict in a patched salvage EVA suit,
standing against a plain background. Prisoner number stencilled on the
chest plate, a locked collar unit at the throat, a small counter
display on the forearm showing days remaining. Helmet visor dark and
reflective, face never visible. Tired posture. Flat 2D, orthographic,
no environment.
[+ bloque de paleta]
```

---

## Cómo usarlos

- **Pegá siempre el bloque de paleta al final.** Es lo que hace que las
  imágenes se parezcan entre sí en vez de ser 12 juegos distintos.
- **Tirá el mismo prompt 4 veces antes de tocarlo.** La variación entre
  seeds suele ser más útil que reescribirlo.
- **Una cosa por imagen.** "Nave y satélite y asteroides" sale siempre
  peor que tres imágenes separadas.
- **Si querés que dos cosas compartan estilo, generalas en la misma
  imagen** y después recortá. Sale más parejo que dos tiradas.
- Si el generador te tira cosas muy cinematográficas, sumá:
  `flat 2d game art, orthographic, no perspective, no depth of field`.
- Si sale con perspectiva en vez de top-down, insistí con
  `seen from directly above, no perspective`.
- Si algo sale muy limpio o muy heroico, sumale:
  `improvised, mismatched, repaired with visible patches`.
- Para siluetas puras (útil para decidir formas antes que colores):
  `pure black silhouette on white background, no interior detail`.

## Lo que NO tiene que aparecer

Sale de [`DISEÑO.md`](DISE%C3%91O.md) — el juego **no tiene fail state,
ni presión de tiempo, ni enemigos**:

- Naves de combate, armas, escudos, explosiones
- Alienígenas, criaturas, tripulación humana viva
- Nebulosas de colores, estrellas brillantes, lens flares
- Cualquier cosa que sugiera peligro o urgencia

El rojo `#d9503f` está reservado para peligro real. Si aparece en una
referencia "porque queda lindo", esa referencia está mintiendo sobre el
juego.

**La excepción son las dos piezas del gancho narrativo** (la cápsula y
el condenado). Ahí sí hay presencia humana, pero por ausencia: restos y
un traje con la cara tapada. Nadie vivo, nadie en el mundo de juego.
