# Guía de estilo visual de Last Wish (obligatoria)

> **Regla del usuario (27-10-2026):** el estilo de las ilustraciones animadas de las cartas básicas de la Pícara
> (Puñalada, Esquiva, Finta) es el estilo de **todo** el juego. Esta guía **sustituye** a las pautas de arte
> anteriores: el método «Espectro»/sprites dibujados por código de la versión Python
> (`python/last-wish/docs/code-drawn-sprites.md`, `docs/visual-design.md`) y el look de los generadores de enemigos
> `Tools/GenerateEnemyArt*.py`. Todo arte nuevo —cartas, enemigos, jefes, personajes, efectos, fondos, iconos— y todo
> redibujo que pida el usuario se hace con ella, para que enemigos, personajes y cartas se vean como una sola cosa.
> El arte que ya existe se queda hasta que el usuario pida rehacerlo (sus generadores siguen sirviendo para
> regenerarlo tal cual).
>
> **Personajes, enemigos y animaciones: se dibujan con código en HTML, una figura completa por fotograma, como
> `Tools/VfxLab/mage_hero.html`; nunca por piezas (reglas del usuario, 30-10-2026; §3.1).**

Referencias aprobadas (mirarlas antes de dibujar):

| Qué | Archivo |
|---|---|
| Las tres ilustraciones, fotogramas clave ×1,5 | `docs/screenshots/estilo-referencia-ilustraciones.png` |
| La mano real en combate (GIF, 125 ms por fotograma) | `docs/screenshots/estilo-referencia-mano.gif` |
| Partículas: proyectil arcano cian (anticipación → vuelo → impacto → disipación) | `docs/screenshots/estilo-referencia-particulas.png` |
| Código del arte aprobado | `Tools/VfxLab/rogue_art.py`, `Tools/VfxLab/pixelkit.py` |
| Cartas básicas del Mago (hechizos sin personaje, rampa de fuego) | `Tools/VfxLab/mage_art.py` |
| Hojas del juego | `Assets/Resources/Game/Cards/Anim/` |
| **Personajes y enemigos: método y diseño obligatorios (§3.1)** — ábrelo en el navegador | `Tools/VfxLab/mage_hero.html` → `Assets/Resources/Game/Heroes/mage*` |
| La Pícara en combate: hecha por piezas, pendiente de rehacer con el método del Mago | `Tools/VfxLab/rogue_hero_art.py` |

---

## 1. El prompt original

Texto exacto, pegado por el usuario el 30-10-2026 (sustituye a la reconstrucción que había aquí):

> Actúa como un **Senior Technical Artist, Pixel Art VFX Artist y Unity Graphics Engineer**, especializado en videojuegos 2D con efectos visuales de alta calidad, similares a los de **Core Keeper, Noita y otros videojuegos pixel art modernos**.
>
> Estoy desarrollando un videojuego **100% 2D en Unity utilizando C#**. Quiero implementar efectos visuales espectaculares, con partículas luminosas, animaciones fluidas, colores vibrantes y una estética pixel art coherente.
>
> ### Objetivo principal
>
> Diseñar e implementar un sistema profesional de VFX 2D que consiga efectos visualmente atractivos, con iluminación, partículas, brillos y animaciones que se integren perfectamente en un mundo pixel art.
>
> No quiero partículas genéricas de Unity ni efectos visuales simples. Busco resultados pulidos, con personalidad, profundidad visual y calidad de producción.
>
> ### 1. Técnicas de partículas
>
> Utiliza las técnicas más adecuadas para conseguir efectos de calidad:
>
> - Unity Particle System configurado exclusivamente para el plano XY.
> - Partículas basadas en sprites pixel art.
> - Sprite sheets con animaciones de partículas.
> - Emisión, velocidad, tamaño y rotación controlados mediante curvas.
> - Color over Lifetime y Size over Lifetime.
> - Sub Emitters para explosiones, impactos y efectos secundarios.
> - Particle Trails y Trail Renderer cuando sean apropiados.
> - Ruido y movimiento orgánico para simular magia, fuego, energía y electricidad.
> - Partículas secundarias para chispas, humo, residuos y destellos.
>
> ### 2. Iluminación y shaders
>
> Aprovecha las capacidades gráficas de Unity 2D:
>
> - Universal Render Pipeline (URP) con 2D Renderer.
> - Shader Graph para crear materiales personalizados.
> - Bloom mediante HDR y postprocesamiento.
> - Light2D para iluminación dinámica.
> - Shaders aditivos para partículas luminosas.
> - Máscaras, gradientes, distorsión y animaciones procedurales cuando aporten valor.
> - Control del brillo y de los colores sin perder la estética pixel art.
>
> El brillo debe verse espectacular, pero sin convertir el efecto en una mancha borrosa.
>
> ### 3. Estética pixel art
>
> Respeta las siguientes reglas:
>
> - Mantener una resolución visual consistente.
> - Evitar filtrado bilineal en los sprites pixel art.
> - Utilizar Point Filtering cuando corresponda.
> - Conservar siluetas de píxeles definidas.
> - Combinar núcleos pixelados con halos luminosos suaves.
> - Evitar que los efectos parezcan gráficos 3D o partículas genéricas.
> - Mantener coherencia entre sprites, iluminación y animaciones.
>
> ### 4. Principios de diseño profesional de VFX
>
> Aplica principios de animación y efectos visuales:
>
> - Anticipation.
> - Impact.
> - Follow-through.
> - Dissipation.
> - Timing y spacing.
> - Squash and stretch cuando corresponda.
> - Contraste de tamaño, velocidad y luminosidad.
> - Jerarquía visual entre partículas principales y secundarias.
> - Variación controlada para evitar movimientos artificiales.
>
> Cada efecto debe tener una secuencia visual clara: **anticipación → acción principal → impacto → disipación**.
>
> ### 5. Arquitectura e implementación en Unity
>
> Cuando te solicite crear un efecto:
>
> 1. Analiza primero el resultado visual deseado.
> 2. Propón la técnica más apropiada para Unity 2D.
> 3. Define la estructura de GameObjects y componentes.
> 4. Proporciona el código C# necesario.
> 5. Especifica los valores recomendados del Particle System.
> 6. Describe los shaders y materiales necesarios.
> 7. Explica cómo configurar la iluminación y el Bloom.
> 8. Implementa optimizaciones de rendimiento.
> 9. Indica cómo convertir el resultado en un prefab reutilizable.
>
> Prioriza soluciones mantenibles, modulares y eficientes.
>
> Utiliza object pooling cuando sea conveniente y evita asignaciones innecesarias durante la ejecución.
>
> ### 6. Calidad visual
>
> Quiero que los efectos tengan las siguientes características:
>
> - Partículas brillantes con núcleos definidos.
> - Chispas con movimiento dinámico.
> - Estelas luminosas atractivas.
> - Explosiones con varias capas de partículas.
> - Animaciones orgánicas y no repetitivas.
> - Colores intensos y armoniosos.
> - Buena legibilidad incluso cuando aparecen múltiples efectos simultáneamente.
> - Integración natural con escenarios pixel art.
>
> ### 7. Criterios técnicos
>
> No propongas sistemas 3D cuando una solución 2D sea suficiente.
>
> No utilices herramientas externas innecesarias.
>
> No dependas exclusivamente de Bloom para conseguir un buen resultado: el efecto debe ser visualmente atractivo incluso sin postprocesamiento.
>
> Distingue claramente entre técnicas que Unity ofrece de forma nativa y soluciones que requieren shaders o implementaciones personalizadas.
>
> ### 8. Forma de trabajar
>
> Cuando te pida implementar un efecto, no te limites a explicar conceptos.
>
> Quiero soluciones concretas, configuraciones precisas, código funcional y recomendaciones visuales.
>
> Si existen varias técnicas, compara sus ventajas en términos de calidad visual, rendimiento y complejidad.
>
> **Prioridad final: calidad visual excepcional, coherencia pixel art, rendimiento y facilidad de reutilización.**
>
> Mi objetivo es conseguir efectos que parezcan diseñados por un artista profesional de VFX para videojuegos 2D, no simplemente partículas configuradas con los valores predeterminados de Unity.

Notas para este proyecto: Last Wish usa el **Built-in Render Pipeline, sin URP** (no hay 2D Renderer, Shader Graph,
Light2D ni Volume). Por eso el juego hornea la luz en las hojas y añade un halo aditivo y partículas vivas (§4.1),
y el VFX Lab hace su propio HDR, bloom y luz 2D simulada (§4.2). No se cambia de pipeline sin que el usuario lo pida.

La primera petición de arte con este prompt (cartas de la Pícara) fue, literal:

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

Rampa de fuego (`mage_art.KM`, Chispa Ígnea): `#3d0f14` `#8f2419` `#d9531e` `#f5973a` `#ffd47a` `#fff6dc` (borde oscuro →
núcleo), humo `#2a2233` `#3d3348`; luces `fire` `(1.0, 0.42, 0.1)` y `ember` `(1.0, 0.68, 0.3)`. El fuego no lleva
contorno `ink`: su borde es el rojo oscuro tramado.

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

**Cartas de hechizo sin personaje** (`mage_art.py`, 30-10-2026: Proyectil Arcano, Égida, Chispa Ígnea). El usuario pidió
la técnica de la Pícara **sin figuras armadas por piezas** (la figura de Esquiva lo es): cada imagen es el hechizo,
como la daga de Puñalada. Proyectil Arcano = orbe cian en un círculo rúnico vertical que gira → se recoge (aplasta) →
sale disparado como cometa (estela cian→violeta, anillo de pulso, líneas de velocidad) → estalla en el cielo (estrella,
anillo de choque, chispas, esquirlas violetas) → se vuelve a formar con motas que convergen. Égida = escudo de
cometa con un sigilo arcano (cristal, anillo y rayos con runas), dentro de una cúpula; una flecha maldita violeta
llega (la cúpula se ilumina de ese lado), choca (destello de toda la red hexagonal y onda de celdas, retroceso,
chispas que rebotan) y sale dando vueltas. Chispa Ígnea = llama flotante sobre un círculo de brasas → se encoge →
bola de fuego en parábola con brasas y humo → aterriza (abanico de lenguas, chispas) y deja **dos llamas** en el suelo
(las 2 de Fuego) que arden durante el bucle y se apagan; la llama de casa se reaviva. La llama se pinta por capas
escaladas (rojo, naranja, amarillo, núcleo blanco) con lenguas laterales y trozos que se desprenden de la punta.

---

### 3.1 Personajes, enemigos y sus animaciones: método y diseño obligatorios

> **Regla del usuario (30-10-2026):** «que a fuerzas sea así […] quiero que sea este para todas las animaciones y el
> diseño de personajes y enemigos». El método con el que se hizo El Mago (`Tools/VfxLab/mage_hero.html`) es el
> **único** permitido para cualquier personaje, enemigo, jefe o figura animada, y su diseño es el del juego.
> Antes ya había dicho: «no uses eso porque se ve muy mal […] ya lo había aclarado muchas veces que funciona mal esa
> técnica» (la técnica por piezas, prohibida abajo).

**Referencia:** `Tools/VfxLab/mage_hero.html`. Ábrelo en el navegador: el Mago animado (reposo, ataque, guardia, daño,
habilidad, muerte), con su halo y cada fotograma a tamaño de combate. Ese mismo archivo es la fuente de las hojas del
juego (`Assets/Resources/Game/Heroes/mage*`).

#### Prohibido (técnica por piezas, sin excepciones)

- Unir partes sueltas: cápsulas o tubos por extremidad, IK de brazos o piernas, polígonos por pieza, barridos de
  miembros apilados, *rigs* con articulaciones o cadenas rígidas.
- **«Masa + brazo y arma encima»**: un brazo como tubo del hombro a la mano y un arma rígida dibujados encima del
  cuerpo, con su propio contorno. Así estaba el primer Mago de este método y la hoja actual de la Pícara
  (`rogue_hero_art.py`); el usuario lo rechazó el 30-10-2026.
- Animar moviendo o girando partes por separado, o interpolando ángulos de brazos y armas entre poses.
- Copiar la figura de la carta Esquiva (`rogue_art.figure_parts`, un polígono por extremidad).
- Volver a proponer la técnica por piezas en cualquier forma, ni «solo para el brazo».

Historial de rechazos: La Guerrera (dos veces), primer sprite de la Pícara (cápsulas + IK, 27-10-2026), Pícara y Mago
con brazo y arma encima (30-10-2026).

#### El método: dibujar con código en una página HTML, una figura completa por fotograma

Todo es un programa en JavaScript dentro de una página HTML (`<script id="mage-art">` en `mage_hero.html`); no hay
dibujos a mano ni piezas montadas.

1. **Mapa de zonas de la figura entera** (`figure(p)`). En cada fotograma se recorren los 96×96 píxeles y se decide a
   qué parte pertenece cada uno, fila a fila según su altura sobre el suelo: sombrero (se estrecha hacia la punta, que
   se dobla y se mece), ala, cabeza redonda, cara, pelo, esclavina, torso, faja, túnica (se ensancha y su bajo sigue a
   los pies), botas, capa detrás. Las extremidades y lo que sostiene son **zonas del mismo mapa**: la manga se calcula
   como un paño que cuelga de la esclavina (borde de arriba recto, el de abajo cayendo en campana hacia el puño), la
   mano cierra sobre el bastón y el bastón con su cuna y su cristal también es zona del mapa. Todo queda conectado en
   una sola silueta (lo que quedara suelto se descarta).
2. **Pintado** (`paint`). Cada zona con su rampa de color (oscuro → claro), luz desde arriba a la izquierda, tramado
   Bayer entre tonos, **un único contorno** oscuro de 1 px alrededor de toda la figura, luz de borde cian en los bordes
   de delante y de arriba y violeta detrás, sombra de la manga sobre el cuerpo, ribetes dorados, runas.
3. **Dos capas**: pintura (se lee sola) + luz emisiva solo en lo mágico (cristal, ojos, gema, runas). El halo del juego
   es esa luz difuminada y sumada (no hay bloom).
4. **Cada fotograma tiene su pose completa escrita en una tabla** (`POSES`: posición, agacharse, inclinarse, cabeceo,
   dónde está la mano, ángulo del bastón, brillo del cristal, viento de la tela…). La figura se dibuja entera desde
   cero en cada fotograma; al reproducir no se interpola nada ni se gira ninguna pieza. El reposo respira y mueve la
   tela como deformación de toda la figura.
5. **Efectos por fotograma** con el vocabulario de las cartas (estrellas de 4 puntas, motas, chispas, líneas de
   velocidad, imágenes residuales, orbe y rayo, cúpula de hexágonos, círculo rúnico), anticipación → impacto →
   disipación.
6. **Exportación con el mismo código**: `Tools/VfxLab/mage_hero_html.py` ejecuta el script de la página con Node y
   `export_hero_art.py` hace las hojas (×1 y ×2), el halo, el JSON con eventos y la validación (cada fotograma, con su
   arma, es **una sola región conectada**; las acciones terminan en el fotograma 0 del reposo). La página y el juego no
   pueden quedar distintos.

#### El diseño de personajes y enemigos

- **Celda nativa de 96×96** (jefes y élites pueden usar una mayor), suelas en la fila 93; los héroes miran a la
  derecha y los enemigos hacia el héroe. Hoja ×2 para combate.
- **Proporciones chibi**: cabeza grande (unos 15 px en una figura de ~80), cuerpo compacto, silueta clara que se lee a
  tamaño de combate.
- **Silueta oscura** en rampas frías (night, vio, teal o el azul del Mago) con luz arriba a la izquierda, un único
  contorno `#07050c`, luz de borde cian `#58dcff` delante y arriba y violeta `#3a2163` detrás.
- **Cara en sombra con ojos que brillan** (cian en los héroes; cada enemigo con su color de acento).
- **Un acento propio por personaje** en rampa de 4–5 pasos (Mago: túnica azul, faja violeta y ribetes dorados; Pícara:
  violeta; enemigos: veneno verde, fuego naranja, hielo…) y pocos detalles dorados.
- **Solo lo mágico emite luz** (ojos, cristales, runas, gemas, hechizos).
- **Animación a 12 fps**: reposo de 24 fotogramas (2 s) en bucle sin costura; acciones cortas (ataque ~10, guardia ~9,
  daño ~7, habilidad ~12) que vuelven al fotograma 0 del reposo; muerte (~17) que se queda en su último fotograma;
  eventos de golpe y lanzamiento en el JSON; halo aditivo.
- **Cada acción usa el vocabulario de sus cartas o de sus movimientos** (Mago: Proyectil Arcano, Égida, círculo
  rúnico; Pícara: Puñalada, Esquiva, Finta; enemigos: sus golpes y hechizos con el mismo lenguaje de efectos).

#### Cómo hacer un personaje o enemigo nuevo

1. Copiar `mage_hero.html` como plantilla y cambiar el mapa de zonas (`figure`), el pintado y las tablas de poses.
2. Abrirlo en el navegador y mirarlo fotograma a fotograma.
3. **Enseñar al usuario una pose de prueba** (la página publicada vale) y esperar su visto bueno antes de la hoja entera.
4. Exportar con un adaptador como `mage_hero_html.py` (Node) desde `export_hero_art.py`, y pruebas como
   `MageHeroArtTests`.

**Estado (30-10-2026):** El Mago, hecho con este método y en el juego. Pendientes de rehacer con él: la Pícara (su hoja
actual es por piezas), la Guerrera y los enemigos, cuando el usuario lo pida.

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
python Tools/VfxLab/export_hero_art.py --preview TestResults/hero-preview       # hojas de la Pícara y el Mago + GIFs
python Tools/VfxLab/export_hero_art.py --hero mage                               # solo uno (el Mago necesita Node)
# Tools/VfxLab/mage_hero.html: ábrelo en el navegador para ver el Mago animado; es la fuente de su hoja
python Tools/VfxLab/export_hero_art.py --check                                 # hoja = generador; una sola masa
```

1. Dibujar/ajustar en el módulo de arte, mirar el GIF fotograma a fotograma.
2. Exportar al juego y mirar las capturas reales: pruebas PlayMode `RogueCardArtTests` → `TestResults/rogue-hand-*.png`
   y `MageCardArtTests` → `TestResults/mage-hand-*.png` (mano, carta sostenida, mazo). Para una pieza nueva, una prueba
   igual con capturas.
3. Pruebas EditMode y PlayMode completas (AGENTS.md).

Con el editor abierto: `Temp/vfxlab-tests.request` con `EditMode` o `PlayMode` (opcionalmente seguido de una clase)
hace que Unity importe lo cambiado, recompile, ejecute y deje el resultado en `TestResults/remote-*.txt`.
