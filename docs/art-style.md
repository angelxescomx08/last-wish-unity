# Guía de estilo visual de Last Wish (obligatoria)

> **Regla del usuario (27-10-2026):** el estilo de las ilustraciones animadas de las cartas básicas de la Pícara
> (Puñalada, Esquiva, Finta) es el estilo de **todo** el juego. Esta guía **sustituye** a las pautas de arte
> anteriores: el método «Espectro»/sprites dibujados por código de la versión Python
> (`python/last-wish/docs/code-drawn-sprites.md`, `docs/visual-design.md`) y el look de los generadores de enemigos
> `Tools/GenerateEnemyArt*.py`. Todo arte nuevo —cartas, enemigos, jefes, personajes, efectos, fondos, iconos— y todo
> redibujo que pida el usuario se hace con ella, para que enemigos, personajes y cartas se vean como una sola cosa.
> El arte que ya existe se queda hasta que el usuario pida rehacerlo (sus generadores siguen sirviendo para
> regenerarlo tal cual).

Referencias aprobadas (mirarlas antes de dibujar):

| Qué | Archivo |
|---|---|
| Las tres ilustraciones, fotogramas clave ×1,5 | `docs/screenshots/estilo-referencia-ilustraciones.png` |
| La mano real en combate (GIF, 125 ms por fotograma) | `docs/screenshots/estilo-referencia-mano.gif` |
| Partículas: proyectil arcano cian (anticipación → vuelo → impacto → disipación) | `docs/screenshots/estilo-referencia-particulas.png` |
| Código del arte aprobado | `Tools/VfxLab/rogue_art.py`, `Tools/VfxLab/pixelkit.py` |
| Hojas del juego | `Assets/Resources/Game/Cards/Anim/` |

---

## 1. El prompt original

El texto exacto no se conservó (la conversación se resumió). Esto es lo que pidió el usuario, con sus requisitos en
orden; la última frase sí es literal. Si el usuario pega el texto exacto, sustitúyelo aquí.

> Actúa como **Senior Unity 2D VFX Engineer**. Mejora lo visual de este juego, que es 100 % 2D, con iluminación y
> partículas mágicas inspiradas en **Core Keeper** y **Noita**.
>
> 1. Inspecciona la versión de Unity, el render pipeline, la cámara y los sistemas visuales (no asumas URP).
> 2. Partículas (*Particle System*) en el plano XY.
> 3. Materiales compatibles con 2D, aditivos y alfa.
> 4. HDR y *Bloom* cuando sea compatible, con un **núcleo de píxel nítido** y el **resplandor suave en una capa aparte**.
> 5. Capas **Core, Glow, Sparks, Trails e Impact**.
> 6. *Color/Size over Lifetime*, *Noise*, *Burst*, *Sub Emitters*.
> 7. *Light2D* opcional.
> 8. Prefabs reutilizables y ajustes en el Inspector.
> 9. Optimiza (overdraw, materiales compartidos, *pooling*).
> 10. Diseña primero **un solo** efecto mágico azul/cian y una escena de demostración; no hagas muchos efectos antes de
>     validarlo.
> 11. Verifica compilación y referencias, y **nunca digas que se ve bien sin haberlo inspeccionado visualmente**.
>
> Criterios de calidad: pixel art nítido; bloom visible sin saturar; partículas variadas; **anticipación → impacto →
> disipación**; legible con muchos efectos a la vez; atractivo **también sin bloom**. Implementa, configura y documenta
> lo que no se pueda hacer automáticamente.
>
> «quiero que hagas los sprites de las cartas basicas de picara y no sigas los lineamientos que estan en el proyecto
> porque como que he visto que no quedan bien esto es una prueba, deben estar animados tener particulas brillantes y
> bonitas»

Aclaración posterior del usuario: de las cartas solo se quería **la imagen del cuadro** (la ilustración), no un marco
nuevo; el marco, el texto y los números de la carta del juego no cambian.

---

## 2. El estilo

### 2.1 Principios

1. **Pixel art de verdad, pequeño y nítido.** Se dibuja a resolución nativa (una ilustración de carta mide 88×64) y se
   escala por enteros al exportar (×2). Nada de antialias, degradados suaves ni escalados fraccionarios en la pintura.
2. **Dos capas por fotograma: pintura y luz.** La *base* es pintura opaca que se lee sola; la *emisiva* es luz (RGB
   sobre negro, valores > 1 permitidos) y **solo emite lo mágico**: runas, filos encantados, estrellas, chispas,
   motas, ojos, ventanas encendidas. El metal, la ropa y el fondo no emiten.
3. **Se lee sin brillo.** La base debe funcionar sin halo ni bloom (silueta clara, contraste de valores). La luz
   añade magia, no forma.
4. **Escenas, no iconos.** Cada imagen es una escena pequeña con fondo propio (cielo nocturno con luna y tejados,
   bruma teal, nebulosa violeta) y una viñeta suave hacia `ink`. Una idea por imagen, el sujeto centrado y ocupando
   ~50–60 % del ancho.
5. **Noche fría + magia cian/violeta + chispas cálidas.** Los valores oscuros dominan; la magia es el punto más
   brillante; un acento cálido pequeño (ventanas, brasas) da contraste de temperatura.
6. **Movimiento con intención.** Todo bucle tiene **reposo (flotación) → anticipación → impacto → disipación**.

### 2.2 Paleta (`rogue_art.K`)

| Rampa | Tonos (oscuro → claro) | Uso |
|---|---|---|
| ink | `#07050c` | contorno, viñeta, negros |
| night | `#0b0817` `#120d24` `#1b1436` `#271d4a` | cielos nocturnos |
| teal | `#070d16` `#0c1726` `#11243a` `#1a3550` | bruma fría (defensa, sigilo) |
| vio | `#0e0718` `#1a0c2c` `#281242` `#3a1a5c` | fondos arcanos |
| moon | `#6f63b8` `#a69be6` `#e2dcff` | luna, estrellas lejanas |
| steel | `#3d435c` `#646d8c` `#98a2c0` `#cdd5ec` `#f4f7ff` | metal (5 pasos, luz arriba-izquierda) |
| grip / guard | `#2a1638` `#4a2a66` `#6e4493` / `#4b3f78` `#7a6cb0` `#b6abe0` | empuñaduras, guardas |
| cyan (magia) | `#0d3550` `#137ea3` `#2fc4e6` `#8ff1ff` `#e6feff` | runas, estelas, chispas |
| violet (magia) | `#2d1346` `#55248a` `#8a47cf` `#c79bff` `#f0e0ff` | fantasmas, sombras mágicas |
| body / scarf | `#0c0814` `#171025` `#241838` / `#3a1a5e` `#5d2a92` `#8f50d0` | figuras en sombra, telas |
| rim | `#58dcff` (cian) `#3a2163` (violeta) | luz de borde en siluetas oscuras |
| cálidos | `#f2b35e` `#a8603a`, oro `#8a6a3a` | ventanas, brasas, detalles |

Luces de la capa emisiva (`rogue_art.L`): cian `(0.25, 0.85, 1.0)`, blanco, violeta `(0.6, 0.3, 1.0)`, azul
`(0.2, 0.45, 1.0)`, cálido `(1.0, 0.6, 0.25)`. Los núcleos van a **blanco**; el color vive en los bordes.

Personajes y enemigos nuevos usan estas mismas rampas (un color propio por criatura como acento, p. ej. veneno verde,
fuego naranja, siempre en rampa de 4–5 pasos con el mismo contraste) para que todo parezca del mismo mundo.

### 2.3 Reglas de pintura

- **Luz arriba-izquierda**, sombra abajo-derecha; metal con brillo especular de 1–2 px.
- **Contorno** `ink` de 1 px por fuera de la silueta (`outline_mask = dilate(m) & ~m`); en figuras oscuras, **luz de
  borde** cian de 1 px en el lado iluminado.
- **Degradados y transparencias con tramado ordenado** Bayer 4×4 (`Canvas.put(..., alpha<1)` tramado), nunca mezcla
  suave en la base. Los halos de la base son discos concéntricos tramados (p. ej. el halo de la luna).
- La mezcla suave solo existe en la **luz** (capa emisiva, halo del juego, bloom del laboratorio).
- **Estrella de 4 puntas** (`star4`): núcleo blanco de 1 px, brazos cian que se apagan hacia fuera, diagonales cortas si
  el brazo mide ≥ 3. Es la firma del estilo: impactos, puntas de filo, destellos.
- **Motas** (`motes`): puntos de 1 px que suben ondulando y se encienden/apagan con `sin(πu)`; las más brillantes se
  vuelven cruces de 5 px.
- **Imágenes residuales** violetas/cian (fantasmas tramados) para movimiento rápido; **líneas de velocidad** de
  1 px detrás de lo que se mueve; **medialunas** de barrido con cabeza brillante y cola que se adelgaza.
- Polvo y aterrizajes con `dust0/dust1`, sin emisión.

### 2.4 Animación

- **12 fps, bucle de 24 fotogramas (2 s)** para ilustraciones; acciones de personajes/enemigos con la misma cadencia.
- **Bucle sin costura**: todo lo periódico es función de `t = f / FRAMES` con un número **entero** de ciclos por bucle;
  las posiciones aleatorias salen de `np.random.default_rng(semilla)` fija (el arte es determinista).
- **Curvas**: `ease_out` cúbico para impactos y llegadas, `ease_in_out` (smoothstep) para recogidas; nada lineal en
  lo que tiene peso.
- **Estructura** (ejemplo Puñalada): flotación suave → la daga se recoge hacia atrás (anticipación) → estocada rápida
  con líneas de velocidad y estrella en la punta (impacto) → chispas que caen y reflejo que recorre el filo
  (disipación) → vuelta al reposo.
- Los efectos de luz parpadean (ventanas, runas) con fases distintas para que la escena respire.

---

## 3. Técnica: cómo se dibuja

Python 3.10+ con **numpy y Pillow**, sin editor de imágenes. El kit está en `Tools/VfxLab/pixelkit.py`:

| API | Para qué |
|---|---|
| `Canvas(w, h)` | lienzo float RGBA (`px`), rejillas `x`, `y` y matriz Bayer `B` |
| `poly`, `rect`, `disc`, `line`, `pixel` | máscaras booleanas a resolución nativa |
| `put(mask, color, alpha, mode='dither')` | **pintura**: alfa < 1 se trama (pixel art) |
| `add(mask, light, alpha)` | **luz** aditiva (capa emisiva) |
| `dilate`, `erode`, `shift` | contornos, bordes y desplazamientos de máscaras |
| `hexc`, `mix` | colores |
| `BitmapFont` | texto con las fuentes pixel del juego (`Resources/Game/Fonts`) |
| `save_sheet` | hojas de fotogramas |

Receta de un fotograma (`rogue_art.punalada_frame(f)` es el modelo):

```python
def mi_frame(f):
    t = f / FRAMES
    c, g = Canvas(AW, AH), Canvas(AW, AH)          # c = pintura, g = luz
    gradient_bg(c, K['night0'], K['night2'], K['night1'])   # 1. fondo
    ...                                             # 2. escena (luna, tejados, bruma)
    ...                                             # 3. sujeto: máscara → put por tonos → contorno → luz de borde
    star4(c, g, x, y, arm=3)                        # 4. efectos: cada uno pinta en c y emite en g
    motes(c, g, t, seed=5, count=10, area=(...), col_a=K['cyan3'], col_b=K['violet3'], light=L['cyan'])
    vignette(c)
    return c, g
```

`render(id)` devuelve las listas `(frames, glows)`. Para una pieza nueva: un módulo como `rogue_art.py` con su
`<id>_frame`, registrado en los exportadores (abajo).

---

## 4. Partículas

### 4.1 En el juego (lienzo propio, sin HDR)

El juego dibuja en su lienzo de 1280×720 (`Presentation`), sin bloom. La luz se simula así
(`Presentation/Cards/CardArtAnimation.cs`):

1. **Fotograma ya iluminado**: base + emisivo × 0,6, y lo que pasa de 1 se empuja hacia blanco (× 0,5).
2. **Halo**: el emisivo difuminado a dos radios (σ 1,4 y 4,0 px nativos) en una hoja aparte, dibujado con
   `Blend.Add` a ganancia 0,5. Es el sustituto del bloom.
3. **Partículas vivas**, encima del fotograma y debajo del marco:
   - 3 **destellos** (estrella de 4 puntas: núcleo `lerp(color, blanco, 0,65)`, brazos de 1–3 unidades), periodo
     1,25 + 0,4·i s, visibles el 55 % del ciclo con envolvente `sin(πu/0,55)^1,5`;
   - 4 **motas** de 1 unidad que suben la mitad de la ventana ondulando ±2 unidades, periodo 2,1 + 0,35·i s,
     envolvente `sin(πu)`.
   - **Unidad = un píxel del arte nativo** a la escala en que se ve la carta (nunca medios píxeles).
   - **Deterministas**: posición = hash(id, ranura, ciclo); nada de `Random` por fotograma.
   - `Blend.Add` ignora el alfa del lienzo: se multiplica a mano por `Canvas.Alpha` y se apaga a 0,35 en cartas
     injugables.
   - Pocas: ≤ 12 quads por carta.
4. **Reloj**: `GameContext.Time` + una fase sacada del id (FNV-1a), para que las copias vayan juntas y lo que se
   reconstruye cada fotograma no salte.

Enemigos y personajes nuevos siguen el mismo esquema: hoja iluminada + hoja de halo (`Blend.Add`) + partículas del
sistema de efectos del juego (`Presentation/Fx`, con *pooling*), con la paleta y las formas de esta guía.

### 4.2 Con cámaras y partículas de Unity (VFX Lab)

`Assets/VfxLab` demuestra el estilo con HDR real (Built-in RP, Gamma, sin URP); detalle en `docs/vfx-lab.md`.

- **Capa de píxeles** 640×360 ARGBHalf: 1 texel = 1 píxel de arte; la presentación la sube con *sharp-bilinear*.
- **Bloom propio separado del núcleo**: umbral 1,15 con rodilla 0,35, intensidad 0,85, 6 niveles; los valores HDR > 1
  se blanquean (*hot white* 0,45) para que el núcleo se vea blanco aunque se apague el bloom.
- **Capas de un efecto**: *Core* (núcleo ×5 parpadeante), *Glow* (halo tramado), *Sparks* (chispas con gravedad,
  arrastre y estela de 1 px), *Trails* (estela de píxeles con ruido + `TrailRenderer` de 2 px), *Impact* (destello
  animado, anillo de 44 puntos, esquirlas que giran) y disipación (subemisor de ascuas al morir las chispas, 45 %,
  motas con ruido).
- **Un atlas** (`vfx_atlas.png`, 8×8 celdas de 16 px, PPU 16: tamaño 1 = 16 px exactos) y **materiales compartidos**
  (aditivo HDR, aditivo suave posterizado + Bayer, alfa tramado).
- **Tamaño sobre la vida con fotogramas del atlas** (estrella de 7 px → punto de 1 px), no escalando el quad.
- **Intensidad HDR por partícula** en *Custom Data* `Custom1.x` (curva sobre la vida), leída en el shader como
  `TEXCOORD0.z`.
- **Luz 2D simulada** (`DstColor One` + 8 % aditivo) con presupuesto global para que varias no quemen la imagen.
- **Legibilidad con muchos efectos**: si otro impacto ocurrió hace < 0,12 s se omiten destello y halo.
- **Tiempos del proyectil**: anticipación 0,38 s (coincide con el impulso de la Pícara) → vuelo 0,30 s en arco con
  aceleración → impacto → disipación ~1,5 s; el objetivo reacciona 0,05 s después del impacto.
- *Pooling* (`VfxPool`) y emisión manual a lo largo del recorrido (sin ráfagas al reutilizar objetos).

---

## 5. Criterios de calidad (lista de comprobación)

- [ ] Píxeles nítidos a la escala de juego; ningún medio píxel; contorno y luz de borde presentes.
- [ ] Se lee **sin** halo/bloom; con él, la magia brilla sin saturar ni tapar la forma.
- [ ] Partículas variadas (estrellas, motas, chispas, estelas, polvo) y pocas.
- [ ] El bucle tiene anticipación → impacto → disipación y no tiene costura.
- [ ] Paleta de §2.2 (o una rampa nueva con el mismo contraste) y núcleos blancos.
- [ ] Legible con muchas cartas/efectos a la vez (mano completa, prueba de estrés).
- [ ] **Inspeccionado a ojo**: GIF de vista previa y captura del juego real. Nunca se da por bueno sin mirarlo.

---

## 6. Flujo de trabajo y verificación

```powershell
python Tools/VfxLab/generate_vfxlab_art.py --preview TestResults/vfxlab-art    # arte + GIF por carta (laboratorio)
python Tools/VfxLab/export_game_card_art.py --preview TestResults/card-anim    # hojas del juego + GIF de la ventana
python Tools/VfxLab/export_game_card_art.py --check                            # el repositorio coincide con el generador
```

1. Dibujar/ajustar en el módulo de arte, mirar el GIF fotograma a fotograma.
2. Exportar al juego y mirar las capturas reales: prueba PlayMode `RogueCardArtTests` → `TestResults/rogue-hand-*.png`
   (mano, carta sostenida, mazo). Para una pieza nueva, una prueba igual con capturas.
3. Pruebas EditMode y PlayMode completas (AGENTS.md).

Con el editor abierto: `Temp/vfxlab-tests.request` con `EditMode` o `PlayMode` (opcionalmente seguido de una clase)
hace que Unity importe lo cambiado, recompile, ejecute y deje el resultado en `TestResults/remote-*.txt`.
