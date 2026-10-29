# Dónde quedamos

Lo último de cada sesión, para retomar sin releer el historial. Lo más
nuevo arriba.

---

## 29 de octubre de 2026

### El retrato del diálogo pasó a ser un personaje de verdad

Antes el retrato era **un sprite suelto** en un `Image` del Canvas.
Ahora es un personaje en piezas, con la idea de animarlo.

Por qué el rodeo: `Sprite Skin` (los huesos 2D) necesita
`SpriteRenderer` y **no funciona sobre un `Image` de UI**. Así que el
personaje vive en el mundo y entra al Canvas por RenderTexture, igual
que el planeta.

La cadena quedó así:

```
Sanao  (mundo, ~730, 292, -219)        ← Layer "Retrato"
├─ BrazoAtras   SpriteRenderer          Order in Layer más bajo
├─ Cuerpo       SpriteRenderer
├─ Cuello → Cabeza
└─ BrazoDelante SpriteRenderer          Order in Layer más alto

Main Camera (orth)  → Culling Mask solo "Retrato", Target Texture RT_Retrato
Main Camera         → Culling Mask sin "Retrato", Target Texture None

Canvas / Panel_Intro / Retrato / Foto   ← RawImage con RT_Retrato
```

`Dialogo`: cada línea apunta al **rig** (un GameObject) en vez de a un
`Sprite`. Los personajes viven puestos en la escena y el diálogo prende
el de la línea y apaga a los demás, con el fundido de siempre.

### Lo que falta ahí

- `Sprite-Unlit-Default` a **todas** las piezas. Con el material Lit se
  ven negras, porque la `Global Light 2D` está en layer Default y la
  cámara del retrato no la ve.
- Pivots en las articulaciones (hombro, codo, base del cuello).
- Order in Layer de cada pieza.
- Recién ahí, `Sprite Skin` si se quiere doblar el codo. Para solo
  rotar piezas rígidas no hace falta.
- Reasignar los campos del `Dialogo`: al cambiar los tipos, Unity
  perdió lo que había. Las líneas quedan con *Personaje* vacío
  (vacío = narrador, sin retrato) y el campo *Retrato* también.

### Escombros

- Al nacer dan un **pop**: entran al 30%, se pasan al 135% y se
  acomodan, en 0.18 s. Los campos están en el prefab `Escombros`.
- La aspiradora ahora se los traga cuando **tocan** la boca y no cuando
  su centro llega al centro de la boca. El tamaño lo lee del collider,
  así que funciona con cualquier `Escala Escombro`.

**Sin verificar:** quedó reportado que los escombros de roca no se
recogen. No hay nada en el código que trate distinto a la roca — el
taladro usa el mismo prefab y el mismo camino para los tres materiales.
La única asimetría real es que el hielo puede entrar a los tanques aun
con la bodega llena; o sea que **con la bodega llena, la roca deja de
entrar y el hielo sigue**. Falta confirmar si era eso, y si pasa con la
aspiradora o agarrándolos con la mano.
