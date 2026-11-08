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
| Cartas básicas de la Guerrera (su espada y su escudo, sin personaje) | `Tools/VfxLab/warrior_art.py` |
| Cartas del botín de la Pícara (sus dagas, cartas, venenos, humo, capa, botas y dados, sin personaje) | `Tools/VfxLab/rogue_pool_art.py`, `_2.py` … `_6.py` |
| Cartas del botín de la Guerrera (su equipo, su vida como corazón de cristal, sin personaje) | `Tools/VfxLab/warrior_pool_art.py`, `_2.py` … `_4.py` |
| Qué cartas tienen ya ilustración animada (lista generada) | `docs/card-art.md` (`Tools/VfxLab/card_art_status.py`) |
| Hojas del juego | `Assets/Resources/Game/Cards/Anim/` |
| **Personajes y enemigos: método y diseño obligatorios (§3.1)** — ábrelo en el navegador | `Tools/VfxLab/mage_hero.html` → `Assets/Resources/Game/Heroes/mage*` |
| La Guerrera en combate: mismo método (yelmo con visera en T, escudo, sin capa) | `Tools/VfxLab/warrior_hero.html` → `Assets/Resources/Game/Heroes/warrior*` |
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

**Cartas de la Guerrera sin personaje** (`warrior_art.py`, 31-10-2026: Espadazo, Bloqueo, Guardia Férrea). El sujeto es
su equipo, el de su sprite (`warrior_hero.html`): la espada larga con runas cian (carmesí con Fuerza/Furia), guarda y
pomo dorados con gema cian y empuñadura carmesí, y el escudo heater carmesí con borde de acero, ribete dorado, espada
dorada y gema cian. Rampas propias en `warrior_art.KW` (carmesí `#25070f`→`#ff7464`, oro `#5e4626`→`#f4dc9c`, placas
de luz `#1e2b48`→`#a9c8f2`); el golpe de metal da chispas cálidas, la magia sigue en cian. Espadazo = espada en guardia
ante una muralla con un estandarte carmesí que ondea → se echa atrás (motas que convergen, destello en la punta) →
tajo de arriba abajo con media luna cian gruesa → golpe en el suelo (destello, grietas con luz cian, anillo de choque,
esquirlas de piedra, chispas que rebotan, polvo, temblor de 1 px) → sube de nuevo. Bloqueo = escudo clavado en el suelo
de una cripta con haz de luz; un hacha enemiga (filo con brillo violeta) asoma por la derecha, se echa atrás (destello)
y baja (imágenes residuales); el escudo se prepara (contorno azul acero), recibe el golpe (cara encendida, se encoge y
retrocede 3 px arañando el suelo), dos contornos de escudo se expanden (gana escudo) y las chispas rebotan en el suelo.
Guardia Férrea = espada clavada tras el escudo sobre un estrado con círculo de runas; 8 placas de luz orbitan, se
alejan cargándose, se cierran de golpe en un baluarte en arco (juntas que destellan, anillo, polvo, temblor) y, con la
guardia en pie (Aguante), un pulso dorado sube del escudo a la empuñadura: las runas se vuelven carmesí, estrella roja
y chevrones que suben (Fuerza); las placas se deshacen en motas y vuelven a orbitar.

**Diez cartas del botín de la Pícara sin personaje** (`rogue_pool_art.py`, 3-11-2026: las comunes que se leen sin
figura). El sujeto son sus dagas con runas (las de Puñalada y Finta, `rogue_art.dagger_labels`) y lo que tocan; cada
una tiene un escenario propio para que la mano no se vea repetida, y lo que la carta hace se ve en la escena (los 2
enemigos al azar, el Combo, el escudo que se pierde). Pinchazo = estilete fino que prende al vuelo una hoja de otoño
delante de la luna llena (la hoja se queda ensartada y luego cae). Tajo Veloz = sobre los tejados con viento, la daga se
echa atrás y cruza en media luna cian con imágenes residuales violetas; del corte sube una gota de maná azul (Combo: +1
de maná). Lanzar la Daga = callejón de ladrillos con farol: la daga gira en el aire, se clava en un cartel de «se
busca», tiembla, se deshace en humo violeta que vuelve a casa y allí se rehace (la Daga Oculta). Lluvia de Dagas =
grieta violeta en el cielo; dos andanadas caen sobre dos sigilos del empedrado (2 enemigos), se quedan clavadas
(anillo de choque, chispas, polvo) y se funden en motas. Cuchillada Errante = cripta con tres sigilos flotantes: una
ruleta elige uno, la daga va hasta él por un camino ondulado con estela punteada, lo rompe, la ruleta elige otro y
repite (Combo), y vuelve. Marcar Objetivo = mira carmesí (anillo, cuatro marcas que se cierran con un pulso, ojo en
rombo) sobre una ciudad en ruinas; tres dagas lanzadas le dan y cada golpe suelta «+» carmesí (el daño extra).
Abrir la Guardia = el escudo redondo de un enemigo con una barrera azul (su escudo); la estocada le da en el umbo, se
agrieta, estalla en cuatro trozos con astillas y la barrera se hace añicos azules; luego vuelve a aparecer. Corte y
Guardia = dos dagas cruzadas ante un ventanal con luz de luna; una corta en media luna por encima, vuelve y el choque
levanta un rombo de luz azul (gana escudo). Afilar = la punta raspa una piedra de afilar en tres pasadas con chispas
cálidas, las runas se cargan más en cada una y la hoja alzada destella (el +6 del siguiente golpe). Carterista = puesto
de mercado de noche: la daga corta el cordel de una bolsa, cae, revienta y las monedas giran y salen volando hacia
arriba; bajan una bolsa nueva. Colores nuevos en `rogue_pool_art.KR` (hojas, ladrillo, papel, maná `#4b93ff`, tejas);
el azul del escudo (barrera enemiga, rombo de Corte y Guardia) es la rampa `blue` de `card_frame.py` (`#0f2550`→`#c2e4ff`).

**Otras diez del botín de la Pícara** (`rogue_pool_art_2.py`, 3-11-2026, mismo planteamiento; importa las ayudas de
`rogue_pool_art.py`). Las cartas que se juegan o se tiran aparecen como cartas pequeñas: dorso violeta con borde dorado
y runa cian, o cara clara si es la carta que se descarta (`paint_card`, que se estrecha al girar); al cortarlas o
quemarlas se consumen desde el corte con borde de brasas (`burn_away`). Chatarra = una carta descartada cae sobre un
montón de chatarra (broquel roto, espada clavada con el puño arriba, engranaje, botella, cadena, trapo carmesí), el
montón tiembla, sale una carta que sube a la mano (roba 1) y sube una gota de maná (Despojo). Deshacerse = una carta
flota sobre un puente de noche; la daga la parte en media luna, las mitades arden en brasas violetas que giran hacia
el centro y forman un rombo azul con cuatro puntos (4 de escudo). Finta Doble = dos dagas fantasma violetas llegan desde
los lados y se cruzan delante de la de verdad (rombo azul, 3 de escudo); con el Combo, una se vuelve cian y va a uno de
dos sigilos elegido al azar. Quiebro = en el adarve, una flecha llega hacia la daga, un giro de la hoja la desvía entre
chispas y un rombo azul (6 de escudo) y, con el Combo, sube una carta (roba 1). Tirar y Cortar = una carta lanzada al
aire se corta y arde en naranja; la hoja toma la luz naranja de Despojo y raja un poste de entrenamiento con una X
(naranja y cian), astillas y la marca del corte. Hoja Envenenada = estantes de boticario; la punta se hunde en un
cuenco de veneno que burbujea, sale verde con tres cargas que se encienden en las runas y tres gotas que caen; la capa
se va con un destello. Veneno = pantano de noche; un frasco vuela, estalla sobre el sigilo del enemigo (charco y nube
verde que sube) y, con el Combo, un segundo frasco. Muro de Humo = una bomba con la mecha encendida estalla en un muro
de humo; dos flechas se quedan dentro con chispas azules (14 de escudo) y rueda otra bomba. Contraataque = en la arena
con antorchas, el hacha del atacante baja contra la daga, que la para del todo (rombo azul) y responde con una media
luna que sube por el mango hasta su mano (estrella carmesí: 3 de daño). Golpe de Gracia = campo de batalla bajo una luna
con bruma roja; la daga se clava en un yelmo caído con ojos rojos, se parte en dos, el alma sale como humo rojo y sube
la gota de maná (si lo matas, +1 de maná). Rampas nuevas en `rogue_pool_art_2.K2`: naranja de Despojo `#4a1a06`→`#ffd08a`,
veneno `#0c2410`→`#e2ffb0`, humo `#16121f`→`#8a809e`, vidrio. Quedan por hacer Paso Atrás y Rodar (necesitan su figura).

**Tercera tanda del botín de la Pícara** (`rogue_pool_art_3.py`, 3-11-2026, todas poco comunes). Corte Rápido = en un
dojo con biombos, dos cortes en X atraviesan un fardo de paja colgado; cae la parte de abajo, suben una carta (roba 1)
y otra con el Combo, y bajan un fardo nuevo. Tajo = en un muelle de noche, una media luna raja en diagonal una caja; la
tapa resbala al agua (salpicadura), sube una carta y salen «+» carmesí (Combo: +5). Puñalada Trapera = un desgarro de
sombra se abre en un callejón por detrás del sigilo de un enemigo; la daga sale de él (solo se ve lo que asoma), apuñala
y vuelve a apuñalar con luz carmesí (Combo). Golpe Desesperado = templo en ruinas; una carta se quema como precio y sus
brasas entran en la hoja, que tiembla con un aura carmesí y runas rojas, y un golpe enorme agrieta una columna y le
arranca un trozo (temblor, escombros, polvo); la piedra se recompone al final. Instinto = bosque de noche con ojos
rojos; un abanico de cinco cartas (su mano) se enciende carta a carta y cada una manda su luz a la hoja, que gira y
golpea al sigilo. Juego de Manos = mesa de juego con tapete granate y vela; la carta de arriba del descarte se levanta,
desaparece en humo violeta, cruza como estela de estrellas y reaparece al otro lado como carta de su mano. Manto Raído =
en un campanario con viento, su capa cuelga sola de una clavija (cuello recogido, capucha caída a la espalda, broche
dorado, bajo en jirones, agujeros); para una flecha tras un rombo azul y, con Despojo, sus desgarros brillan en naranja
y sube un segundo rombo. Reflejos = cueva con estalactitas y cristales; el tiempo se ralentiza (todo más azul) y la daga
parte en el aire tres gotas que caen; queda un círculo de runas azul con chevrones que suben (el poder: +1 de
destreza). Escudo Reactivo = una trampa con cara tallada dispara un virote; justo antes, un broquel de luz con anillo de
runas y red de hexágonos se abre en la punta de la daga, ondea con el golpe y el virote se astilla; suben una carta y una
gota de maná (Combo). Guardia Evasiva = claro con niebla; dos dagas cruzadas levantan un rombo grande que para dos
flechas de los sigilos enemigos y, con el Combo, una bruma pálida los apaga y aparecen chevrones azules hacia abajo
(pegan menos). Ayudas nuevas: `card_rot` (carta girada, para el abanico y el descarte), `plus_marks`, `rising_card`,
`ward_rings`, `slash_line`, `straw_bundle`, `crate`, `rift`, `pillar`, `cloak_mask`, `drop_mask`, `rune_buckler`; rampas
`K3` (paja, agua, tapete, tela de la capa, piedra del templo).

**Cuarta tanda del botín de la Pícara** (`rogue_pool_art_4.py`, 3-11-2026: las tres poco comunes que quedaban y siete
raras). Agilidad = sus botas solas (cuero oscuro, vuelta violeta con una runa cosida, hebilla) en una cuerda floja entre
dos torres; llega una flecha, se agachan (la cuerda se comba), saltan por encima con imágenes residuales y al caer sube
un rombo azul (7 de escudo); con el Combo, otra flecha, otro salto más alto y un rombo mayor. Señuelo = un espantapájaros
de paja con su capucha y su bufanda violeta en un campo (destello violeta que atrae la mirada); rombo azul (5 de
escudo), el sigilo de un enemigo dispara un rayo carmesí, revienta la paja y el rayo, ya violeta, se curva hasta el
sigilo del otro enemigo, que se agrieta. Sombra Esquiva = un farol en un poste proyecta la sombra de la daga en el muro
(cada sillar toma un tono por su distancia a la luz: nada de luz tramada); llega una flecha, la daga se hunde en su
sombra (contorno violeta), la flecha se clava en el poste y la daga vuelve a salir con un rombo (7 de escudo); con el
Combo la sombra se despega del muro como carta y entra en la pila de robo (Daga Oculta). Capa de Sombras = su capa de
noche flota sola (capucha caída, broche dorado, forro de estrellas, ribete dorado); se abre en alas, dentro se forma un
rombo azul (8 de escudo) y se cierra; el reloj de arena gira (empieza tu turno), su onda fría rompe contra la capa (no
pasa más allá de su sombra) y, al abrirse otra vez, el rombo sigue ahí. Corte Afortunado = plaza con pozo de los deseos
(tejadillo, cubo); salta una moneda que gira y, de cara, enseña un trébol de cuatro hojas verde (la Suerte); su oro entra
en las runas, que se vuelven doradas, y la estocada es un crítico: estrella dorada enorme, aspa dorada y «+» dorados.
Hoja Única = sala de espejos; los dos espejos laterales reflejan la daga (la copia en los tonos fríos del vidrio), se
agrietan y estallan en esquirlas; lo que había en ellos entra en la única hoja, que se enciende en violeta (Singular,
el color del aviso del juego), cae de punta sobre el sigilo del suelo (anillos de choque, «+» violetas) y los espejos se
vuelven a platear de arriba abajo. Ráfaga de Cortes = patio con un gong de bronce; arriba, las tres cartas jugadas este
turno se encienden una a una y cada una manda su luz a un corte que raja el gong; la cuarta es esta carta, que se suma a
la fila, y su corte es carmesí; el gong se balancea con las marcas brillando. Tirar los Dados = garito con lámpara; el
cubilete tiembla y vuelca un dado que rueda y cae en 1; un trébol de cuatro hojas (la tirada extra de la Suerte) lo
hace rodar otra vez y cae en 3: los puntos se vuelven azules y suben tres gotas de maná; el dado vuelve al cubilete en
humo violeta. Es la primera carta cuyo fotograma fijo no es el 0 (`still` 16: el dado con el 3 y el maná). Torbellino =
páramo con menhires; la daga gira hasta levantar un embudo de viento (franjas en espiral que giran, sin tramado) y
cabalga a su alrededor, por detrás y por delante; el embudo avanza hasta el sigilo, lo corta y su viento se vuelve un
rombo (8 de daño, 5 de escudo); con el Combo crece, lo acompaña una segunda hoja carmesí, otro golpe con «+» y un rombo
mayor. Dagas Ocultas = desván con ventana redonda y luz de luna; dos copias de sombra se despegan de la daga, se pliegan
en cartas con una daga dibujada y entran en la pila de robo de la caja; con el Combo, una tercera en cian. Ayudas nuevas:
`comet` (rayo con estela que se afila), `draw_pile`, `boot_mask`/`paint_boot`/`boots`, `decoy_masks`/`paint_decoy`,
`shadow_of`, `warm_wall`, `lantern`, `starry_cloak`, `hourglass`, `lucky_coin`, `gold_runes`, `well`, `mirror_frame`,
`reflection`, `gong`, `die_in_air`/`die_landed`, `dice_cup`, `funnel`, `hidden_card`; rampas `K4` (cuero de las botas,
saco, hueso de los dados, vidrio, bronce, forro de la capa, brezo) y la luz verde `LUCK` del trébol.

**Quinta y sexta tanda: el resto de la Pícara** (`rogue_pool_art_5.py` y `rogue_pool_art_6.py`, 3-11-2026; con ellas
están animadas sus 66 cartas). Paso Atrás y Rodar, que se habían dejado por necesitar su figura, se hacen con sus botas
(como Agilidad): no hace falta dibujarla. Las cartas que se descartan o se roban por tipo se pintan con el color de su
tipo (`type_card`, `kind_card_rot`: carmesí con espada = ataque, azul con escudo = habilidad, violeta con estrella =
poder); las caras claras de papel giradas parecían papeles o una mano. Los poderes dejan un círculo de runas en el suelo
(`power_ring`) y el Combo es el signo de dos cheurones en el verde del aviso del juego (`combo_sign`, `K5['combo*']`,
`LC`). Paso Atrás = bosque de bambú; sus botas saltan un paso atrás y el tajo carmesí de un enemigo raja el suelo donde
estaban (rombo de 3). Rodar = pasillo con antorchas; rueda (una bola de su capa con una franja dorada que gira) bajo una
flecha; al caer se le escapa una carta (descarte al azar), sube otra y un rombo pequeño. Rebuscar = sótano con un cofre;
la daga hurga y saca una carta de cada tipo. Vaciar Bolsillos = desván; su zurrón se vuelca, las cuatro cartas de su mano
caen al descarte con las monedas, cuatro dorsos suben de la pila de robo al mismo sitio y se dan la vuelta. Abanico de
Cuchillas = la daga se abre en un abanico de siete hojas que vuelan a los tres sigilos; con Singular, rombo violeta y
carta. Astucia = un zorro dibujado en estrellas sobre el bosque guiña un ojo y caen dos estrellas que son cartas.
Danza de Sombras = su daga y dos sombras bailan en un salón bajo el signo del Combo, que se enciende para siempre.
Estilo Propio = su gorro de bufón en un escenario para un rayo tras un rombo grande (12); con Singular suenan sus
cascabeles en violeta y salen dos cartas. Estocada Oportuna = placas de hierro giran alrededor de un enemigo; la daga
espera el hueco y entra por él; con Combo, una carta. Fortuna Audaz = moneda de la fortuna en un templo; en cada turno
cae del lado de la carta (sube una carta) o del escudo (rombo de 4). Maestra de Dagas = mandala de ocho dagas; una Daga
Oculta robada pasa por su núcleo y salen dos (una a la mano, otra a la pila). Preparación = mesa de mapas; la punta toca
la gema de maná de la siguiente carta y se le cae un maná. Rapiña = cada carta que cae al montón de descartes sale como
una hoja de sombra hacia uno de dos sigilos al azar. Sombra Gemela = la daga y su gemela de sombra cortan la misma media
luna una tras otra y el signo del Combo sale dos veces. Asesina = un corte baja la barra de vida del enemigo por debajo
de la marca de la calavera y un tajo carmesí de ejecución rompe su sigilo. Cadena Perfecta = cinco eslabones se encienden
con cinco cartas; el quinto es de oro y su carta sale dos veces. Lluvia de Golpes = tres ataques del descarte llaman, uno
por uno, una lluvia de dagas sobre los tres enemigos. Mazo Impecable = las cuatro cartas de su mano enseñan gemas baratas,
se encienden en oro y cae una hoja dorada enorme (temblor); con Combo, un tajo carmesí y «+». Mil Cortes = bajo una luna
de sangre las cartas del descarte destellan una tras otra y cada una es un corte en uno de dos enemigos. Nada que Perder
= una ruleta elige una carta de su mano, arde en un brasero (`mage_art.flame`) y salen dos cartas del humo. Ritmo Letal =
un metrónomo en una torre del reloj; en el primer golpe del turno a la carta se le cae un maná. Tormenta de Acero = una
nube de tormenta llueve dagas sobre los tres enemigos; con Singular la segunda lluvia es violeta y más fuerte. Tormenta
de Veneno = una nube verde sobre un pantano llueve veneno y los sigilos se vuelven verdes y burbujean. Cuatro de ellas
usan un fotograma fijo distinto del 0 (`still`): Astucia 8, Lluvia de Golpes 5, Tormenta de Acero 6, Tormenta de Veneno
12. Ayudas nuevas: `enemy_sigil`, `type_card`/`type_glyph`, `kind_card_rot`, `power_ring`, `falling_card`, `combo_sign`,
`tube` y `jester_cap`, `cloak_ball`, `chest`, `satchel`, `fortune_coin`, `big_card`, `ghost_blade`, `hp_bar`, `skull`,
`chain_link`, `ground_sigil`, `metronome`, `brazier`; rampas `K5` (Combo, bambú, cofre) y `K6` (cadena, sangre, tormenta).

**El botín de la Guerrera** (`warrior_pool_art.py` … `warrior_pool_art_4.py`, 8-11-2026: con ellos están animadas sus 50
cartas). Sin figura: su espada y su escudo (los de `warrior_art.py`) y el resto de su equipo dibujado como una sola
silueta cada uno: su hacha de guerra (`greataxe`), su cuerno (`war_horn`), su guantelete solo (`gauntlet`, sin brazo), su
yelmo cerrado con visera en T y penacho, vacío sobre un poste (`helm`), su coraza en un soporte (`cuirass`, con escamas
que crecen), su martillo (`warhammer`) y una tablilla de hierro encadenada (`iron_tablet`). Su vida es un corazón de
cristal en un engaste dorado (`life_heart`: la parte roja es la vida que tiene, el resto cristal oscuro; grieta = herida).
Un dibujo por mecánica, igual en todas sus cartas: **perder vida** = una gota de su sangre que cae desde arriba (su mano,
fuera del cuadro) y salpica (`blood_drop`, `drip`, `splash`); **Furia** = esa sangre en el arma: runas carmesí y un aura
carmesí que parpadea (`blood_aura`); **Aguante** = el escudo que ya tiene, un contorno azul tenue que respira
(`faint_ward`), y un pulso dorado que sale de la gema del escudo (`gold_pulse`); **Fuerza** = cheurones carmesí que suben
(`rising_chevrons`); **curarse** = cruces verdes que suben (`heal_crosses`, el verde de las curas del juego) y el corazón que
se llena; **escudo** = el rombo azul (`ward_rings`); **robar** = una carta que sube girando; **maná** = el cristal azul;
**inicio/fin de turno** = el reloj de arena que gira; **poder** = el círculo de runas en el suelo. Los sigilos enemigos
giran un número entero de vueltas por bucle (`foe_sigil`). Comunes: Corte Sangriento (bajo una luna roja su sangre cae
en la guarda, las runas se vuelven carmesí y una media luna carmesí raja un sigilo), Embate Firme (tras su escudo el pulso
dorado carga la espada, que embiste un portón de roble; «+» dorados), Escudo de Sangre (tres gotas sobre el escudo en una
cripta, venas carmesí y un rombo grande con borde carmesí), Golpe Doble (dos tajos, abajo y arriba, abollan un peto
enemigo en su soporte), Herida Abierta (dos gotas bajan por el filo hasta una pila de piedra y cada salpicadura es una
carta), Muro de Acero (paveses de hierro brotan del suelo y paran las flechas; el pulso dorado da Fuerza), Postura Firme
(en un acantilado con tormenta el escudo se clava en la roca; con su sangre, un segundo rombo), Tajo Brutal (con su sangre
en la hoja, un tajo carmesí corta el estandarte enemigo, que vuelve a desenrollarse) y Vendaje (una venda se enrolla en su
corazón agrietado y lo sella; el pulso del escudo cura más). Poco comunes: Fortaleza Interior (su corazón en la ventana de
una torre sobre el círculo; la cura cae dentro y las almenas levantan un rombo), Grito de Batalla (su cuerno suena en la
muralla y dos cartas salen del sonido), Grito de Guerra (el grito de su estandarte llega a tres sigilos y cada uno le
devuelve un cristal de maná), Hachazo Temerario (el hacha alzada gotea su sangre y parte un tótem enemigo; sale otro),
Puño de Hierro (el guantelete golpea un bloque marcado; cheurones desde los nudillos), Respiro (campamento: las brasas se
vuelven verdes y llenan el corazón; el escudo apoyado en la espada da un rombo), Rugido (su yelmo en un poste en la arena;
con su sangre la visera se enciende carmesí y ruge: tres cheurones), Sangre Hirviente (un cáliz de su sangre sobre el
círculo; gira el reloj, hierve, se derrama una gota y sube un cheurón del vapor), Sangría (su sangre corre por el canal de
un altar hasta la espada clavada, que se llena de carmesí desde la punta; tres cheurones) y Sed de Batalla (en un
cementerio la espada atraviesa un sigilo y su vida vuelve por la hoja a su corazón). Raras: Conjuro de Combate (en un
círculo de menhires la punta dibuja un anillo de runas y dispara un rayo; rombo en el escudo), Coraza Viva (lo que le falta
al corazón fluye a su coraza y le crecen escamas), Golpe Pesado (en una cantera un golpe lento parte un bloque; la magia
enemiga lo vuelve a juntar), Golpe Total (en un cruce de caminos la espada da una vuelta entera por tres sigilos), Golpe de
Escudo (en un puente con niebla el rombo se pliega en el escudo, que embiste y estalla en azul), Martillo Sísmico (el
martillo golpea el cañón, las grietas corren y la roca brota bajo tres sigilos; el pulso dorado da dos cheurones), Pacto de
Hierro (su sangre en la tablilla, las runas arden, caen las cadenas y sube un cristal de maná), Regeneración (enredaderas
florecen alrededor del corazón en unas ruinas; gira el reloj y cura), Represalia (un rayo enemigo le saca sangre, que se
duplica en la hoja y vuelve como tajo carmesí), Sacrificio (bajo un ventanal rojo el corazón vierte su sangre en la espada,
que vuela contra un sigilo; con Furia sube una carta), Segunda Piel (su corazón en una malla de acero; cuando pierde una
gota la malla da un rombo), Torbellino de Acero (en dunas de noche la espada gira como un disco por tres sigilos; con Furia,
una cruz verde por golpe), Torre (bloques de piedra se apilan en una torre tras el escudo) y Transfusión (el rombo se vacía
como luz azul por un serpentín de vidrio hasta el corazón y se vuelve verde). Épicas y legendarias, a lo grande: Baluarte
(seis escudos de luz se traban alrededor del suyo en un paso nevado), Furia Desatada (entre ruinas en llamas cuatro cortes
y, con Furia, dos más en carmesí), Gran Golpe (la espada alzada llama a una hoja colosal de luz que cae sobre un sigilo),
Gran Muralla (una muralla con torres se alza en ola por la llanura), Ira Carmesí (bajo un sol negro su sangre arde en la
espada; la mitad vuelve en verde), Piel de Acero (se acaba el reloj y el acero líquido cubre su coraza), Bastión (un
castillo entero se alza tras el escudo, ondean las banderas), Berserker (cada gota sobre su yelmo pintado de guerra
enciende la visera y sube un cheurón), Doblar Escudo (sobre un lago helado el reflejo del escudo sale del hielo y el rombo
se duplica), Ejecución (un tajo vacía la barra de vida enemiga, se enciende la calavera y su vida vuelve al corazón),
Escudo Impenetrable (una flecha, una bola de fuego y un hacha se rompen contra la cúpula), Golpe Vampírico (el tajo manda un
enjambre de murciélagos de sangre al corazón, que se llena), Muralla Infranqueable (en una ventisca el rombo se sella en oro
y no se va al girar el reloj) y Voluntad de Hierro (su corazón enjaulado en hierro: las gotas grandes se rompen en los
barrotes y solo pasa una gota pequeña).

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

**Estado (31-10-2026):** El Mago y la Guerrera, hechos con este método y en el juego (`mage_hero.html`,
`warrior_hero.html`). **El Mercader** (vendedor de la tienda, 04-11-2026): `Tools/VfxLab/merchant.html` +
`export_merchant_art.py` → `Resources/Game/Shop/merchant*`; animado el mismo día («anima al vendedor, está muy
estático»): reposo de 24 fotogramas (respira, el farol se balancea, bufanda y pluma con la corriente, parpadeo), hablar
(12, en bucle), saludar quitándose el sombrero, dar las gracias con una reverencia, negar con el dedo y moneda (lanza y
atrapa una moneda cuando la tienda está tranquila). Cada fotograma es una pose completa; todas las reacciones empiezan y
terminan en el fotograma 1 del reposo (la pose aprobada) y el exportador lo comprueba píxel a píxel. El farol cuelga
recto y se mueve en píxeles enteros aunque el cuerpo se incline. **El Brujo** (sala de mejoras, 04-11-2026):
`Tools/VfxLab/warlock.html` + `export_warlock_art.py` → `Resources/Game/Warlock/warlock*`; capucha ciruela con la punta
caída y un cascabel de oro, cara en sombra con ojos verdes y nariz ganchuda, barba con anillo, manto de musgo, túnica con
runas, frasco verde y bastón nudoso (lo sostiene la mano lejana, que es parte de la misma silueta) con una jaula de raíces
y una llama fantasma verde. Reposo 24, hablar 12, saludar (llamita en la palma), conjurar 18 (alza el bastón; el destello
del fotograma 8 es el evento `cast` que la sala usa para lanzar el rayo), negar y pensar (se acaricia la barba). El
exportador mide los pies con las babuchas y el bajo, no con el bastón. Se enseñó la pose de prueba al usuario antes de la
hoja. Pendientes de rehacer con él: la Pícara (su hoja actual es por piezas) y los enemigos, cuando el
usuario lo pida.

**Cara cubierta (regla del usuario, 31-10-2026):** la cara en sombra con ojos que brillan solo tiene sentido si algo la
cubre (el sombrero del Mago, la capucha de la Pícara, el yelmo con visera de la Guerrera). Una cara descubierta y oscura
«se ve rara»: a todo personaje con cara en sombra dale algo que la tape. Y no todos llevan capa: la Guerrera no.

**Sombras de contacto (usuario, 31-10-2026: «parece que flotan»):** todo personaje y enemigo pisa una sombra en
pixel art (`Presentation/Fx/GroundShadow.cs`): elipse plana en tinta casi negra fría `(10, 6, 18)`, tres tonos (alfa
110/172/228) con tramado Bayer 4×4 hacia el borde, en píxeles del arte (×2 en combate), centrada en la línea donde
apoyan las suelas. Los suelos de las mazmorras son casi negros donde están las figuras, así que a los héroes se les pone
antes una luz de suelo tenue y fría `(32, 40, 62)` (suelo iluminado con una mancha oscura); los enemigos ya tienen su
resplandor de color, y la sombra va encima de él. Para los héroes, el exportador mide los pies en cada fotograma
(`ground` en el JSON) y la sombra los sigue (estocada, retroceso, rodillas). Nunca una elipse fija «al centro de la
celda»: no coincide con el sprite.

**El mapa del piso (usuario, 31-10-2026: «desencaja del arte actual»):** el mapa ya no son cajas planas de colores, es
la **planta de la cripta vista desde arriba** (`Presentation/Map/MapArt.cs`), pintada por código en píxeles del arte
(×2) con la paleta nocturna: roca con ruido y contorno de tinta; cada sala, una cámara tallada en su celda con muro norte
de ladrillo (canto iluminado arriba, pie en sombra) y suelo de losas con juntas y oclusión al oeste y al norte (la luz
viene de arriba a la izquierda); pasillos solo entre salas conectadas; escaleras que suben desde el borde inferior hasta
la entrada; el jefe espera en un salón con dos pilares y alfombra carmesí. Encima: las salas a las que aún no puedes
llegar se hunden en sombra (la planta oscurecida un 58 % hacia la tinta, se sigue leyendo), las que puedes elegir
tienen un **círculo de runas cian** en el suelo y una luz que late, las que ya pasaste tienen sus dos antorchas
encendidas y el salón del jefe una luz carmesí. Cada sala lleva su **icono** (`Tools/VfxLab/map_icons.py` →
`Resources/Game/Map/map_icons*.png`, 16×16 nativos, el del jefe 20×20): espadas cruzadas, cráneo con cuernos, cráneo
con corona, cofre, bolsa con moneda, lápida con «?» violeta, caldero, máquina de cápsulas, altar con llama y una gema
que marca dónde estás; mismo contorno, rampas y tramado, y sus partes emisivas brillan en las salas elegibles (hoja de
halo, `Blend.Add`). A la derecha, la leyenda «Salas» con los tipos del piso; al pasar el ratón, qué hay en la sala.
Las celdas miden múltiplos de 8 px para que la trama Bayer no se corte, y la cámara nunca es más estrecha que su icono.

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
4. **Reloj**: `GameContext.Time` + una fase sacada del id (FNV-1a, en 1000003 pasos: cada carta del juego tiene la suya), para que las copias vayan juntas y lo que se
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
python Tools/VfxLab/card_art_status.py                                          # rehace docs/card-art.md (qué cartas están animadas)
python Tools/VfxLab/export_hero_art.py --preview TestResults/hero-preview       # hojas de la Pícara y el Mago + GIFs
python Tools/VfxLab/export_hero_art.py --hero mage                               # solo uno (el Mago necesita Node)
# Tools/VfxLab/mage_hero.html: ábrelo en el navegador para ver el Mago animado; es la fuente de su hoja
python Tools/VfxLab/export_hero_art.py --check                                 # hoja = generador; una sola masa
python Tools/VfxLab/map_icons.py --check                                       # iconos del mapa = generador
```

1. Dibujar/ajustar en el módulo de arte, mirar el GIF fotograma a fotograma.
2. Exportar al juego y mirar las capturas reales: pruebas PlayMode `RogueCardArtTests` → `TestResults/rogue-hand-*.png`,
   `MageCardArtTests` → `TestResults/mage-hand-*.png`, `WarriorCardArtTests` → `TestResults/warrior-hand-*.png` y
   `RogueLootCardArtTests` → `TestResults/rogue-loot-*.png`, `WarriorLootCardArtTests` → `TestResults/warrior-loot-*.png`
   (mano, carta sostenida, mazo). Para una pieza nueva, una prueba
   igual con capturas.
3. Pruebas EditMode y PlayMode completas (AGENTS.md).

Con el editor abierto: `Temp/vfxlab-tests.request` con `EditMode` o `PlayMode` (opcionalmente seguido de una clase)
hace que Unity importe lo cambiado, recompile, ejecute y deje el resultado en `TestResults/remote-*.txt`.
