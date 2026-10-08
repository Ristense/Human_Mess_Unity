# Human Mess — notas de diseño

Documento vivo. Acá se anota la idea a medida que se habla, no es spec cerrada.

## Concepto actual (v2 — reemplaza el sandbox de soldar con clic)

Sandbox espacial de limpieza orbital. Sin fail state, sin presión de tiempo.
El planeta da igual cuál sea — lo importante es la órbita y la chatarra.

## La nave — modular

- Tiene un **centro/hub** al que se le conectan **partes**.
- Partes conocidas hasta ahora:
  - **Cargamento** — módulos de almacenamiento, para llevar piezas.
  - **Herramientas** — módulos de utilidad, para usar sobre los escombros.
- Implica que el jugador arma su propia nave combinando módulos antes de
  salir a limpiar (loadout).

## El brazo

- Sale de la punta de la nave.
- Se comporta como un **brazo con IK** (inverse kinematics) — no es un
  cañón que dispara, es una extremidad que se estira y dobla para
  alcanzar cosas.
- **Sigue al mouse**: el jugador apunta con el cursor y el brazo se
  reacomoda para llegar hasta ahí.
- Es la herramienta principal de interacción con los escombros (agarrar,
  quizás cortar/reparar dependiendo del módulo equipado).

## Movimiento — tanque

- La nave (el chasis) se mueve con **controles de tanque**: adelante/atrás
  + rotación, no vuelo libre en cualquier dirección.
- El brazo es independiente del chasis — el chasis apunta con el cuerpo,
  el brazo apunta con el mouse. Dos sistemas de apuntado separados.

## La nave nodriza / recolectora

- Existe una **nave más grande** (nodriza) de la que sale tu nave modular.
- Ahí volvés a **depositar** lo que juntaste en tus módulos de carga.
- Es el hub central — loadout, descarga, probablemente progresión.

## Desguace vs. reparación

- No todo es basura para triturar. Hay piezas que **conviene reparar**
  en vez de desguazar.
- Esas piezas reparables **se cargan/guardan aparte** — no se
  "picadillan" con el resto.
- Implica al menos dos destinos posibles para cada pieza que agarrás:
  triturar (picadillo/chatarra) o conservar entera (reparar después).
- "Picadillo" = la acción de destruir/triturar piezas para convertirlas
  en materia prima o chatarra vendible/reciclable.

## Estructura del juego — secciones de órbita

- La órbita se divide en **secciones** que el jugador va limpiando una
  por una — como niveles o zonas, no una sola órbita infinita.
- Pendiente: cómo se desbloquean, si tienen dificultad creciente, si hay
  algo especial por sección.

## Abierto / sin resolver todavía

- Qué más módulos existen además de cargamento y herramientas.
- Qué hace exactamente el brazo con una pieza reparable vs. una para
  desguazar (¿una acción distinta? ¿un módulo distinto?).
- Cómo se financia/progresa el loadout de la nave modular.
- Qué pasa en la nodriza además de depositar — ¿se ve, se gestiona algo
  ahí?
- Cuántas secciones de órbita, y qué las diferencia entre sí.

## Descartado de la versión anterior

- El sistema de tienda/plata/mejoras compradas (clicker) — no encajaba
  con la dirección interactiva que se busca ahora.
- El disparo instantáneo de dos clics para soldar — reemplazado por el
  brazo IK como forma principal de interactuar con los escombros.
