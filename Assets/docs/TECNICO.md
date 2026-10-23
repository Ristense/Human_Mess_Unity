# Human Mess — notas técnicas

Cosas de Unity que nos costaron encontrar. Si algo se rompe raro, mirá
acá primero.

## Arquitectura de cámaras

URP tiene dos renderers que **no se pueden mezclar** en un stack:
`Renderer2D` solo procesa `Light2D`, y `UniversalRenderer` (3D) solo luces
3D. Por eso hay dos cámaras que no se apilan:

| Cámara | Renderer | Ve | Sale a |
|---|---|---|---|
| `Base_Camara` | 2D, **ortográfica** | todo menos `Fondo3D` | pantalla |
| `3DWorld_Camara` | 3D, perspectiva | solo `Fondo3D` | `RT_Planeta` |

El `Quad` (hijo de `Base_Camara`) muestra `RT_Planeta`. Es el equivalente
al SubViewport de Godot.

## Las trampas que se repiten

**Una layer nueva no entra sola al Culling Mask.** Si la máscara de la
cámara está elegida a mano, una layer agregada después queda afuera y el
objeto desaparece sin error. Dejá el Culling Mask en `Everything` y
destildá solo `Fondo3D`.

**Un Canvas en `Screen Space - Overlay` no recibe Bloom.** Se dibuja
después del post-procesado. Para que brille: `Screen Space - Camera`, con
`Base_Camara` y `Plane Distance` 1.

**Adentro del Canvas va `UI → Image`, no `2D Object → Sprite`.** Un
SpriteRenderer dentro del Canvas hereda su escala (~0.004) y queda
microscópico.

**Asignar un `Vector2` a `transform.position` pone Z en 0.** Hay que
armar el `Vector3` a mano y conservar la Z.

**Un `Rigidbody2D` simula en mundo e ignora a su padre** — también si es
Kinematic. Por eso lo montado usa `simulated = false`: es la única forma
de que siga al padre.

**Yo leo la escena del disco.** Si no guardaste (`Ctrl+S`), veo la
versión vieja.

**El `Escombro Prefab` del taladro NO puede ser un asteroide.** Si tiene
`Tallable`, cada escombro nace delante del taladro, el taladro lo muerde,
suelta más escombros mordibles, y crece exponencial: en la pega se comió
la memoria y colgó Windows entero; acá colgaba el editor. Pasó por
arrastrar `Asteroide01` a ese campo — "funcionaba", solo que salían
gigantes. Ya está blindado (el taladro se salta lo que tenga `Escombro`,
y si el prefab trae `Tallable` se lo saca al nacer), pero el prefab bueno
es uno propio: sprite chico, `Rigidbody2D`, `CircleCollider2D` y
`Escombro`. Nada de `PolygonCollider2D`.

**En el `Canvas_HUD` no se posiciona UI asignando píxeles a
`position`.** Eso solo coincide en `Screen Space - Overlay`; en `Camera`
el canvas vive en el mundo y el objeto sale volando a kilómetros, sin
error. Para anclar UI a un punto de pantalla:
`RectTransformUtility.ScreenPointToLocalPointInRectangle` con la cámara
del canvas, y asignar `anchoredPosition`. Así lo hace `RuedaMano`.

## Escala

Los sprites están en **PPU 1000**. La nave mide ~media unidad. Todo lo que
sean radios, fuerzas y distancias está en esa escala: un número que
parece chico (0.05) puede ser grande.

La cámara ortográfica tiene `Size 2` → ve 4 unidades de alto.

## El agarre

Es un resorte calculado a mano, no un Joint de Unity. Las tres cosas que
lo hacen estable (las mismas que hubo que arreglar en Godot):

1. **Masa efectiva en el punto agarrado**: `1 / (1/m + r²/I)`. Amortiguar
   con la masa total da un freno más fuerte que la inercia que frena, y
   eso invierte la velocidad y la amplifica cada paso: la pieza sale
   eyectada.
2. **Rigidez acotada al paso de física**: `min(k, m_eff / dt² · 0.1)`.
3. **Recortar el resorte, nunca la suma**: recortar el total se come el
   amortiguador justo cuando más falta.

`angularVelocity` de Unity viene en **grados**/seg; para las cuentas hay
que pasarla a radianes.

## Shaders

Están en `Assets/Shaders`, a código (ShaderLab + HLSL).

| Shader | Para qué |
|---|---|
| `Estrellas` | campo de estrellas procedural |
| `Neon` | color HDR para que lo agarre el Bloom |
| `RadialProgreso` | aro de progreso |
| `CintaPeligro` | franjas que desfilan |
| `Contorno` | borde de lo que está bajo la mano |

En un shader de sprites, **no pongas `_MainTex_ST` ni `_TexelSize` dentro
del `CBUFFER`**: rompe el SRP Batcher 2D y tira un warning.

Para que algo brille: material con color **HDR** + Bloom en el Volume +
el sprite base en **blanco** (el color sale del material; un sprite rojo
tintado de verde da negro).
