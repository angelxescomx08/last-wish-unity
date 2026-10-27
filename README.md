# Last Wish - Unity

**Último Deseo**, migrado desde el proyecto Python/Pygame a **Unity 6.3 LTS,
6000.3.6f1**. Todas las pantallas, reglas y efectos del original están portados a
C#; el estado y las diferencias conocidas están en `docs/port-status.md`.

## Abrir y jugar

1. Agrega esta carpeta (`C:/Users/USER/Desktop/web/unity/last-wish-unity`) en Unity Hub.
2. Ábrela con **6000.3.6f1** y espera la importación inicial.
3. Elige **Last Wish > Jugar** en el menú superior (abre `Assets/Scenes/Bootstrap.unity`
   y pulsa Play en la pestaña Game).

El juego se dibuja al ejecutarse: la pestaña **Scene** se ve vacía antes de Play.
La pantalla de carga aparece al pulsar Play o al abrir una build. El juego no
necesita Python.

## La partida

Igual que el original: menú (Jugar, Ajustes, Pruebas, Salir) → elegir personaje y
semilla → mapa por pisos con combate, élite, tesoro, tienda y sobres, suceso, Brujo,
gachapón, altar de purga y jefe → recompensas → siguiente piso. La vida, el oro, el
mazo y las reliquias pasan de sala en sala. Como en el original, no hay guardado:
**Pausa · Esc > Abandonar partida** vuelve al menú.

## Controles (los del original)

| Acción | Control |
|---|---|
| Jugar una carta | Arrastrarla fuera de la mano (o clic para sujetarla y clic de nuevo); sobre un enemigo si apunta a uno |
| Teclado | 1–9 toma una carta, ←/→/Tab cambia de objetivo, Enter/Espacio la juega |
| Cancelar | Clic derecho o Esc |
| Terminar turno | Botón o E |
| Ver todo de un enemigo / del héroe | Clic derecho, mantener pulsado o I |
| Elementos y reacciones (Mago) | Botón R o tecla R |
| Historial del combate | Tira de la izquierda: rueda, arrastrar o tocar |
| Hoja del héroe | Botón del casco o C |
| Pausa | Botón o Esc |
| Mostrar FPS | Ajustes |

## Estructura

```text
Assets/Scripts/Domain/        Reglas C# puras (sin UnityEngine): combate, cartas, reliquias, enemigos (originales y de los pisos 1 y 2), run
Assets/Scripts/Presentation/  Escenas, UI y efectos del original sobre un lienzo virtual 1280×720 (C# puro)
Assets/Scripts/Runtime/       Unity: GameBootstrap (bucle de main.py), backend de dibujo, entrada, audio, archivos
Assets/Editor/                Importador de pixel art, configuración y build
Assets/Resources/Game/        PNG, metadatos, fuentes y audio originales
Assets/Scenes/Bootstrap.unity Escena de entrada
Assets/Tests/EditMode/        Reglas y paridad con el motor Python
Assets/Tests/PlayMode/        El juego completo con entrada simulada y capturas
Tools/                        Exportadores desde el repositorio Python y validación sin editor
supabase/                     Telemetría de poder: Supabase local (Docker), esquema SQL y pruebas
```

El backend de Unity mete todo el frame en una sola malla dinámica (submallas solo
al cambiar de textura o de mezcla) ejecutada por un command buffer; las caras de
carta se componen una vez en texturas fuera de pantalla y se reutilizan, y el
lienzo se presenta escalado a la ventana con bandas negras como el `Viewport`
original. En estado estable no se reserva memoria por frame.

## Pruebas y build

En Unity: **Window > General > Test Runner**, pestañas EditMode y PlayMode. Las
pruebas PlayMode guardan capturas del lienzo en `TestResults/`.
**Last Wish > Build Windows** genera `Builds/Windows/LastWish.exe` (ignorado por Git).

```powershell
$editor = 'C:/Program Files/Unity/Hub/Editor/6000.3.6f1/Editor/Unity.exe'
$project = (Get-Location).Path
& $editor -batchmode -nographics -projectPath $project -executeMethod LastWish.Editor.ProjectSetup.Configure -quit -logFile Logs/import.log
& $editor -batchmode -nographics -projectPath $project -runTests -testPlatform EditMode -testResults TestResults/editmode.xml -logFile Logs/editmode.log
& $editor -batchmode -projectPath $project -runTests -testPlatform PlayMode -testResults TestResults/playmode.xml -logFile Logs/playmode.log
```

Los comandos de tests no llevan `-quit`: el Test Runner cierra el editor. Revisa
los XML y los logs, no solo el código de salida. `Tools/Validate.ps1` compila todo
contra las DLL de Unity y ejecuta los casos del dominio con .NET; no sustituye al
Test Runner.

Herramientas opcionales de migración (se ejecutan con el Python del repositorio original):

```powershell
python Tools/ExportPythonCatalog.py --source-dir RUTA_PYTHON --output docs/migration/python-catalog.json
python Tools/ExportNumericCards.py --source-dir RUTA_PYTHON --output-dir .
python Tools/ExportEnemyPatterns.py --source-dir RUTA_PYTHON --output-dir .
python Tools/ImportPythonAssets.py --source-dir RUTA_PYTHON
python Tools/ExportPresentationData.py --source-dir RUTA_PYTHON
python Tools/ExportCardFx.py --source-dir RUTA_PYTHON
python Tools/ExportPixelFonts.py --source-dir RUTA_PYTHON
```

Sonidos propios del gachapón (no existen en el original; solo la biblioteca estándar de Python):

```powershell
python Tools/GenerateGachaAudio.py                      # escribe Assets/Resources/Game/Audio/gacha_*.wav y sus .meta
python Tools/GenerateGachaAudio.py --check              # comprueba que los WAV coinciden con las recetas
python Tools/GenerateGachaAudio.py --preview TestResults/audio   # mezcla tiradas completas para escucharlas
```

El arte original se conserva sin redibujar. Consulta `THIRD_PARTY_NOTICES.md`.

## Telemetría de poder

Cada partida registra qué hacen las cartas, las reliquias y los encuentros (jugadas, daño y
bloqueo por maná, vida perdida frente a la media del piso, elecciones, jefes que matan) y la
guarda en Supabase para consultarla con SQL. Durante el combate solo se suman enteros; el JSON
se escribe en disco en segundo plano y se sube sin bloquear el juego, incluso sin conexión.

```powershell
powershell -ExecutionPolicy Bypass -File Tools/Telemetry/LocalSupabase.ps1 start   # Docker Desktop + Node.js
```

Studio local: <http://127.0.0.1:54323>. Consultas, métricas y paso a producción: `docs/telemetry.md`.

## Estilo visual

Todo el arte nuevo sigue **`docs/art-style.md`** (decisión del 27-10-2026, sustituye a las pautas anteriores): pixel art
pequeño y nítido con la luz en una capa aparte, noche fría con magia cian/violeta y acentos cálidos, estrellas de 4
puntas, motas y bucles de anticipación → impacto → disipación. Incluye el prompt original, la paleta, la técnica de
dibujo y cómo se hacen las partículas.

![Estilo de referencia](docs/screenshots/estilo-referencia-ilustraciones.png)

## Ilustraciones de las cartas

Cada carta tiene su propia ilustración en la ventana del marco (`Cards/Art/<id>.png`, 44×32 píxeles a ×4),
en lugar del icono genérico de su tipo. Se componen con tiles CC0 de Dungeon Crawl Stone Soup (los mismos de
los iconos genéricos) y efectos de píxel; Espora y Moho conservan su arte original:

```powershell
python Tools/GenerateCardArt.py            # escribe las 176 ilustraciones y la lista "art" de Cards/layout.json
python Tools/GenerateCardArt.py --check    # comprueba que coinciden con el repositorio
python Tools/GenerateCardArt.py --only a_tajo --preview TestResults/card-art   # hoja de contacto para revisar
```

Lee los tiles del repositorio Python (`--source-dir`, por defecto `../../python/last-wish`).

### Ilustraciones animadas (cartas básicas de la Pícara)

Puñalada, Esquiva y Finta tienen ilustración animada en el juego: la del VFX Lab (88×64, 24 fotogramas a
12 fps) en la ventana de arte de la carta normal, con un halo de luz aditivo y destellos y motas en vivo. Se
ven en la mano (también inclinadas y al sostenerlas), el mazo, recompensas y tienda; donde la carta es una
imagen fija (vuelo al robar, apertura de sobres) se ve su fotograma fijo. El marco, el texto y los números no
cambian.

```powershell
python Tools/VfxLab/export_game_card_art.py           # escribe Cards/Anim (hojas, halos y anim.json)
python Tools/VfxLab/export_game_card_art.py --check   # comprueba que coinciden con el generador
```

Código: `Presentation/Cards/CardArtAnimation.cs`. Pruebas: `CardArtAnimationTests` (EditMode) y
`RogueCardArtTests` (PlayMode, capturas `TestResults/rogue-*.png`). Una carta nueva con animación solo
necesita su entrada en el exportador.

## VFX Lab (prueba de efectos y cartas de la Pícara)

Escena aislada en `Assets/VfxLab` para validar partículas brillantes en pixel art (inspiración Core Keeper/Noita)
sin tocar el juego: un proyectil arcano cian por capas (núcleo, halo, chispas, estelas, impacto) y las tres cartas
básicas de la Pícara con ilustraciones animadas nuevas y partículas. Funciona sobre el Built-in Render Pipeline del
proyecto: capa de píxeles 640×360 HDR, bloom propio separado del núcleo nítido, luz 2D simulada y *pooling*.

- **Last Wish > VFX Lab > Jugar VFX Lab** — 1-3 o clic juegan cartas, Espacio lanza, S prueba de estrés, B bloom, T cámara lenta.
- **Last Wish > VFX Lab > Construir y capturar fotogramas** — capturas en `TestResults/vfxlab/`.
- Arte: `python Tools/VfxLab/generate_vfxlab_art.py`. Detalle, parámetros y pendientes: `docs/vfx-lab.md`.

## Enemigos nuevos del piso 1

La partida del menú añade al piso 1 diecisiete enemigos que no existen en el original:
- 7 normales, 3 esbirros invocables y 3 élites.
- 4 jefes repartidos en 3 combates, uno de ellos la pareja Los Hermanos Lápida.
- 6 grupos nuevos.

Mecánicas nuevas:
- Invocación con un máximo de 3 enemigos en pantalla. Los esbirros no dan oro.
- Pinchos, Desvío, Blindaje, Carroña, Buche, Carga/Aturdido y Vigilancia.
- Combos de jefe contra estrategias: robar Fuerza, arrancar escudo, mudar el veneno y los elementos, castigar jugar muchas cartas.

Las pruebas de paridad siguen usando el contenido original (`EnemyRoster.Original`). El bestiario completo está en `docs/enemies.md`.
Las hojas de sprites nuevas se generan así:

```powershell
python Tools/GenerateEnemyArt.py           # escribe las 17 hojas (Python 3.11 + Pillow 11)
python Tools/GenerateEnemyArt.py --check   # comprueba que coinciden con el repositorio
```

## Enemigos nuevos del piso 2

La partida del menú también tiene un piso 2 propio, con quince enemigos que no existen en el original:
- 6 normales, 3 esbirros y 3 élites.
- 3 jefes nuevos (El Nigromante, La Hidra de la Cisterna y La Reina de los Espejos), que sustituyen al Señor de la Cripta.
- 6 grupos nuevos, mezclados con los enemigos originales.

Mecánicas nuevas:
- Amparo, Rito, Rencor, Cicatrización, Cabezas, Reflejo y Ansia de Sangre en los enemigos.
- Sangrado y Necrosis, dos perjuicios nuevos para el héroe.
- Ilusiones que desaparecen con su invocadora y sacrificios de esbirros (Ofrenda Oscura).
- Combos de jefe contra estrategias: anular la curación, castigar los golpes enormes, devolver el veneno y los perjuicios, y regenerarse salvo con Fuego o Veneno.

En Pruebas, los índices nuevos (combates 31-42, élites 9-11 y jefes 7-9) fuerzan ese contenido también en los pisos 1 y 3.
Detalle y números en `docs/enemies.md`.

```powershell
python Tools/GenerateEnemyArtFloor2.py           # escribe las 15 hojas del piso 2 (Python 3.10+ y Pillow 11+)
python Tools/GenerateEnemyArtFloor2.py --check   # comprueba que coinciden con el repositorio
```

## Enemigos nuevos del piso 3

El piso 3 de la partida del menú es una ciudadela helada con quince enemigos nuevos:
- 6 normales (uno, el Abanderado, solo sale acompañado), 3 esbirros y 3 élites.
- 3 jefes nuevos (El Rey Liche con su Filacteria, El Relojero y La Dragona de Escarcha), que sustituyen al Señor de la Cripta.
- 6 grupos nuevos, mezclados con los enemigos originales. Una de cada cuatro peleas de pareja trae un tercer enemigo, como en el original.

Mecánicas nuevas:
- Pendón (aura que da Inspiración a los aliados), Cólera, Inmortal y Cuerda en los enemigos.
- Ataques Perforantes que ignoran el escudo y Rebobinar, que recupera la vida perdida en tu turno.
- Agotamiento y Congelación, dos perjuicios nuevos para el héroe: menos maná, y vida perdida por cada carta que te quede en la mano.

Los pisos 4 en adelante siguen como en el original. En Pruebas, los índices del piso 3 (combates 43-54, élites 12-14 y jefes
10-12) fuerzan ese contenido en cualquiera de los pisos 1-3. Detalle y números en `docs/enemies.md`.

```powershell
python Tools/GenerateEnemyArtFloor3.py           # escribe las 15 hojas del piso 3 (Python 3.10+ y Pillow 11+)
python Tools/GenerateEnemyArtFloor3.py --check   # comprueba que coinciden con el repositorio
```

## Capturas

Renderizadas con el verificador fuera de línea a partir de los mismos comandos de
dibujo que ejecuta Unity:

![Menú](docs/screenshots/menu.png)

![Combate: apuntar una carta](docs/screenshots/combat.png)

![Turno enemigo](docs/screenshots/enemy-turn.png)

![Mapa](docs/screenshots/map.png)

![Tienda](docs/screenshots/shop.png)

![Gachapón](docs/screenshots/gacha.png)
