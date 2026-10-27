# VFX Lab (prueba visual de la Pícara)

Escena aislada para validar efectos de partículas brillantes en pixel art antes de llevarlos al juego. Contiene un
efecto de **proyectil arcano cian** completo y las **tres cartas básicas de la Pícara** (Puñalada, Esquiva, Finta)
con ilustraciones animadas nuevas y partículas propias. La escena no toca el juego (`Bootstrap.unity`,
`UnityRenderBackend` y `Domain` siguen igual); las ilustraciones sí se llevaron a las cartas del juego (ver *En el juego*).

> Prueba de arte: las cartas **no** siguen el estilo de `Tools/GenerateCardArt.py` (44×32 con tiles DCSS). Son
> ilustraciones nuevas de 88×64 a 12 fps con capa emisiva, generadas por `Tools/VfxLab/`.

## Abrir y jugar

1. **Last Wish > VFX Lab > Construir escena y prefabs** (regenera materiales, prefabs y `Assets/VfxLab/Scenes/VfxLab.unity`).
2. **Last Wish > VFX Lab > Jugar VFX Lab** (abre la escena y entra en Play; el botón Play normal arranca `Bootstrap`
   porque `EditorStartup` fija `playModeStartScene`. El laboratorio lo quita para esa sesión y lo restaura al salir).
3. **Last Wish > VFX Lab > Construir y capturar fotogramas** genera capturas en `TestResults/vfxlab/` (ver *Verificación*).

| Tecla | Acción |
|---|---|
| 1 / 2 / 3 o clic en una carta | Jugar la carta (salto, destello de foil, ráfaga del marco y proyectil) |
| Espacio | Lanzar el proyectil |
| S | Prueba de estrés: 8 proyectiles casi simultáneos y las 3 cartas |
| B | Bloom sí/no (el pixel art debe verse bien sin él) |
| L | Luces 2D simuladas sí/no |
| K | Temblor de cámara sí/no |
| T | Cámara lenta (×0,25) |
| A | Autojuego (juega las cartas en bucle) |
| H | Ocultar la ayuda |

## Estado del proyecto que condiciona el diseño

- Unity **6000.3.6f1**, **Built-in Render Pipeline** (`m_CustomRenderPipeline: 0`), espacio de color **Gamma**.
  URP no está instalado, así que **no hay Renderer 2D, Light2D, Volume ni Shader Graph**. Unity Hub avisa de que el
  Built-in queda obsoleto desde Unity 6.5 (con soporte hasta 6.7 LTS).
- El juego dibuja todo en un lienzo virtual 1280×720 con un command buffer propio; migrar a URP afectaría a todo el
  juego, por eso el laboratorio implementa HDR, bloom y luz 2D sobre el Built-in, en una escena aparte.
- Se añadió el módulo `com.unity.modules.particlesystem` al `manifest.json` (no estaba). Es el único cambio fuera de
  `Assets/VfxLab` y `Tools/VfxLab`.

## Arquitectura

```text
Cámara de píxeles (PixelGlowCamera)
  └─ RenderTexture 640×360 ARGBHalf (HDR)  ← fondo, personajes, cartas, partículas (1 texel = 1 píxel de arte)
Cámara presentadora (PixelGlowPresenter, OnRenderImage)
  ├─ Bloom: umbral con rodilla suave → cadena de reducción (6 niveles) → suma con filtro tienda
  └─ Composición: capa de píxeles con sharp-bilinear (nítida a cualquier escala) + núcleos >1 hacia blanco
                  + bloom suave a resolución de pantalla + viñeta leve, con bandas negras si cambia el aspecto
```

- **Núcleo nítido y resplandor separados.** Las partículas se rasterizan en la rejilla de 640×360 (píxeles de verdad,
  como en Noita/Core Keeper) y el bloom se calcula aparte y se suma suavizado. Con el bloom apagado se sigue viendo un
  núcleo blanco porque la composición blanquea los valores HDR > 1 (`hotWhite`).
- **Materiales compartidos** (`Assets/VfxLab/Materials`): `VFX_Particles_Add` (aditivo HDR), `VFX_Particles_AddSoft`
  (aditivo con posterizado + tramado Bayer para halos de pixel art), `VFX_Trail_Add`, `VFX_Particles_Alpha` (alfa
  tramado), `VFX_Sprite_Add` (capas emisivas), `VFX_Light2D`, `VFX_Sprite` (destello de golpe) y `VFX_CardSheen`.
  Todas las partículas usan **un solo atlas** (`vfx_atlas.png`, 8×8 celdas de 16 px) vía *Texture Sheet Animation*,
  así comparten material.
- **Intensidad HDR por partícula**: cada sistema escribe su intensidad en *Custom Data* (`Custom1.x`, con curva sobre la
  vida) y el shader la lee como `TEXCOORD0.z`. Así un mismo material da núcleos de ×5 y estelas de ×0,6.
- **Tamaño en píxeles exactos**: una partícula de tamaño 1 mide 16 px = 1 celda del atlas. El "tamaño sobre la vida"
  de chispas y motas se hace con fotogramas del atlas que encogen la forma (estrella de 7 px → punto de 1 px), no
  escalando el quad, para que nunca aparezcan píxeles a medias.
- **Light2D simulada** (`VfxLight` + `LastWish/VFX/Light2D`): sprite con mezcla `DstColor One` (destino × (1 + luz)):
  ilumina en cian lo que hay detrás y deja el negro en negro, más un 8 % aditivo. Solo se dibuja mientras su nivel es
  > 0, y tiene un **presupuesto global** (`VfxLight.MaxTotalLevel`): como las luces se multiplican, varias superpuestas
  se escalan para no quemar la imagen.
- **Pooling** (`VfxPool` + `PooledVfx`): los proyectiles se precalientan (6) y vuelven solos al pool cuando no quedan
  partículas vivas. Las estelas se emiten a mano a lo largo del recorrido (`ParticleSystem.Emit`), con espaciado exacto
  y sin la ráfaga que daría `rateOverDistance` al reutilizar un objeto que "salta" de posición.

## El proyectil arcano cian (`Prefabs/VFX_ArcaneBolt_Cyan.prefab`)

| Fase | Duración | Capas (Core / Glow / Sparks / Trails / Impact) |
|---|---|---|
| Anticipación | 0,38 s (coincide con el impulso de la animación de ataque de la Pícara) | chispas que convergen girando (Sparks), 5 runas en órbita, núcleo que crece (Core), halo tramado (Glow), luz que sube |
| Lanzamiento | instantáneo | destello (Impact) y abanico de chispas hacia el objetivo |
| Vuelo | 0,30 s, arco de 1,2 u con aceleración | núcleo parpadeante ×5 (Core), halo (Glow), estela de píxeles con ruido (Trails), cinta `TrailRenderer` de 2 px, chispas que se desprenden con estela de 1 px |
| Impacto | instantáneo | destello animado (Impact), halo, anillo de 44 puntos que se expande, 44 chispas con gravedad, arrastre y estela, 8 esquirlas que giran, pulso de luz, temblor de 2 px |
| Disipación | ~1,5 s | ascuas que suben (subemisor de las chispas al morir, prob. 45 %), motas que titilan con ruido |

Parámetros editables en el Inspector: tiempos, arco, aceleración, picos de luz, espaciado de estelas y la ventana
`impactFlashCooldown` (si otro impacto ocurrió hace < 0,12 s se omiten destello y halo: es lo que mantiene legible la
prueba de estrés). Cada capa es un `ParticleSystem` normal dentro del prefab.

El objetivo (Gólem de Musgo) usa su propia animación `hurt`, cuyo fotograma 0 ya es una silueta blanca: el
laboratorio la arranca 0,05 s después del impacto y desde el fotograma 2, para que se lea el destello cian del impacto
en vez de un bloque blanco.

## Cartas de la Pícara (`Prefabs/Card_*.prefab`)

- Marco nuevo de 104×144 en pixel art (cuero violeta, acero, gemas cian) con el texto real de las cartas en las fuentes
  del juego (`lw8`, `lw8b`, `tiny5`): Puñalada «Inflige 6 de daño.», Esquiva «Gana 6 de escudo.», Finta «Inflige 4 de
  daño. Combo: gana 4 de escudo.».
- Ilustración de 88×64, 24 fotogramas a 12 fps en bucle, con **capa emisiva** separada (solo brilla lo mágico):
  - **Puñalada**: daga con runas cian que laten, se recoge (anticipación), estoca con líneas de velocidad y estrella en
    la punta (impacto), chispas y reflejo que recorre el filo (disipación); luna, tejados y ventanas cálidas.
  - **Esquiva**: pícara encapuchada con ojos cian y bufanda violeta que esquiva a un lado y al otro dejando dos
    imágenes residuales, rachas de viento, arco de barrido y polvo al aterrizar.
  - **Finta**: una daga fantasma violeta amaga por arriba con estela discontinua y se deshace en motas; la daga real
    golpea por abajo con media luna cian, estrella y chispas, y vuelve girando.
- Partículas en vivo por carta: motas que titilan, destellos en la ilustración, chispas que suben por encima del marco,
  barrido de foil en el marco, contorno al pasar el ratón y ráfaga desde el borde al jugarla.

## Verificación

- **Compilación**: los tres ensamblados (`LastWish.VfxLab`, `.Editor`, `.Tests`) compilan sin errores ni avisos en
  6000.3.6f1.
- **Capturas** (`Assets/VfxLab/Tests/VfxLabCaptureTests.cs`, PlayMode): reloj fijo a 30 fps, guarda en
  `TestResults/vfxlab/` la composición final: bucle de cartas, *hover*, el proyectil fotograma a fotograma, los mismos
  momentos sin bloom y la prueba de estrés, más `stats.txt`. Falla si se registra cualquier error o excepción.
  Resultado de la última pasada: capa 640×360 ARGBHalf en Direct3D12, ~330 partículas vivas por proyectil y ~1 100 en
  la prueba de estrés, sin errores.
- **Por lotes** (con Unity cerrado): `Tools/VfxLab/run_vfxlab_capture.bat` (construye y captura; `build` solo construye).
- **Ciclo desatendido**: si existe `Temp/vfxlab-capture.request` (o `vfxlab-build.request`), el editor abierto
  refresca, recompila y lanza la captura (`VfxLabRemote.cs`). `Temp/` se borra al cerrar Unity y Git lo ignora. No
  actúa si hay una escena con cambios sin guardar o errores de compilación.

## Regenerar el arte

```powershell
python Tools/VfxLab/generate_vfxlab_art.py                                   # escribe Assets/VfxLab/Art
python Tools/VfxLab/generate_vfxlab_art.py --preview TestResults/vfxlab-art  # además GIF y hojas de contacto
```

Necesita Python 3.10+, numpy y Pillow. Las fuentes se leen de `Assets/Resources/Game/Fonts`.

## En el juego (ilustraciones animadas)

El juego no tiene HDR ni bloom, así que `Tools/VfxLab/export_game_card_art.py` hornea cada ilustración en
`Assets/Resources/Game/Cards/Anim/`: fotogramas ya iluminados (base + emisivo, núcleos a blanco, ×2), un halo por
fotograma (emisivo difuminado, que el juego suma con mezcla aditiva) y `anim.json`. `CardWidget` dibuja la cara de la
carta con la ventana de arte transparente y, detrás, el fotograma actual, el halo y unos destellos y motas en vivo
(`Presentation/Cards/CardArtAnimation.cs`). Detalle técnico en `docs/port-status.md` → *Animated card illustrations*.
Capturas de la mano real: prueba PlayMode `RogueCardArtTests` → `TestResults/rogue-hand-*.png`.

Para iterar con el editor abierto, `Temp/vfxlab-tests.request` con el texto `EditMode` o `PlayMode` (y opcionalmente
una clase, p. ej. `PlayMode LastWish.Tests.RogueCardArtTests`) hace que el editor importe lo que cambió, recompile y
ejecute esas pruebas; el resultado queda en `TestResults/remote-<modo>[-<clase>].txt`.

## Pendiente (no hecho automáticamente)

1. **Validar a ojo en tu pantalla** (Jugar VFX Lab) y decidir la dirección antes de hacer más efectos (requisito 10).
   Siguientes candidatos: efectos propios por carta (tajo para Puñalada, ráfaga de evasión para Esquiva, doble tajo
   para Finta) reutilizando el atlas, los materiales y el sistema de capas.
2. **Integración en el juego — hecha para las ilustraciones** (camino a): Puñalada, Esquiva y Finta muestran la
   ilustración animada en la ventana de arte de la carta normal del juego (sin el marco del laboratorio), con halo
   aditivo y destellos/motas en vivo; ver *En el juego* arriba. Falta, si se quiere, llevar el proyectil cian y las
   ráfagas al jugar la carta al sistema de efectos del juego (`Presentation/Fx`).
3. **URP 2D**: si se migra, `Light2D` sustituye a `VfxLight`, el *Bloom* del Volume a `PixelGlowCamera` y la
   *Pixel Perfect Camera* a la capa de 640×360; atlas, materiales (pasados a URP) y prefabs se reutilizan.
4. Pasar el Test Runner completo del proyecto (EditMode y PlayMode); solo se ejecutó la prueba del laboratorio.
