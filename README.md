# Last Wish - Unity

Base jugable de **Último Deseo**, migrada desde el proyecto Python/Pygame a
**Unity 6.3 LTS, 6000.3.6f1**. Es una primera etapa de la migración.

## Abrir y jugar

1. Clona este repositorio y agrega su carpeta raíz en Unity Hub.
2. Ábrelo con **6000.3.6f1** y espera la importación inicial de paquetes y recursos.
3. Selecciona **Last Wish > Jugar** en el menú superior. Abre la escena inicial y activa Play en la pestaña Game.
4. Pulsa **Jugar**, elige personaje y comienza el combate.

La interfaz se crea al ejecutar el juego: la pestaña **Scene** puede verse vacía antes de Play. Para arrancar manualmente, abre `Assets/Scenes/Bootstrap.unity`, pulsa ▶ **Play** y cambia a **Game**. El editor usa vista 2D y el botón Play inicia siempre Bootstrap, aunque estés en una escena vacía.

Unity Hub debe tener una licencia activa. La primera importación del editor
puede tardar; la pantalla de carga del juego aparece al pulsar Play o ejecutar
una build, no durante la importación de Unity Hub.

## Incluido

- Pantalla de carga con progreso de recursos mediante `Resources.LoadAsync`.
- Menú, ajustes de FPS, selección de guerrera/mago/pícara.
- Mazmorra y sprites actuales del proyecto original, con animaciones de idle,
  ataque, defensa, daño y derrota. Se conservan los tiempos de los atlas.
- Combate básico contra un espectro: mazo de diez cartas, mano de cinco, maná,
  daño, escudo, robo, descarte y reciclado del mazo.
- Victoria, derrota, volver al menú y repetir batalla.
- Música original, efectos y UI en español.
- Lienzo de referencia 1280x720 adaptable a la ventana, texturas pixel-art sin
  mipmaps ni compresión y filtrado Point en los personajes.
- Cálculo de daño con `BigInteger`: `(base + bonos planos) * multiplicadores`.
- Escena inicial, ajustes de proyecto y `.meta` incluidos en Git.

## Controles

| Acción | Control |
|---|---|
| Jugar carta contra el espectro | Clic o teclas 1-5 |
| Terminar turno | Botón o Espacio |
| Volver al menú durante combate | Botón Menú o Esc |
| Mostrar FPS y milisegundos | Menú > Ajustes |

Mientras se resuelve una animación, las acciones se bloquean para evitar jugar
cartas dos veces. Se usa la semilla 42 en esta demo para reproducir las partidas.

## Estructura

```text
Assets/Scripts/Domain/        Reglas C# puras, sin UnityEngine
Assets/Scripts/Application/   Comandos de la sesión de combate
Assets/Scripts/Runtime/       Canvas, carga, animaciones y presentación
Assets/Editor/                Importadores y configuración/build
Assets/Resources/Game/        Arte, metadatos, fuentes y audio seleccionados
Assets/Scenes/Bootstrap.unity Escena de entrada
Assets/Tests/EditMode/        Pruebas de reglas y números grandes
Assets/Tests/PlayMode/        Prueba de carga > menú > selección > combate
Tools/                       Validación adicional sin arrancar el editor
```

La UI se construye con componentes uGUI al iniciar. El proyecto usa el renderizador
Built-in y la entrada clásica de Unity para mantener esta base pequeña.
Las actualizaciones del HUD ocurren al cambiar el combate; las animaciones cambian
el UV del atlas sin crear imágenes ni recortar texturas cada frame.

## Pruebas y build

En Unity: **Window > General > Test Runner**, pestañas EditMode y PlayMode.
**Last Wish > Configure project** aplica la configuración base; **Last Wish >
Build Windows** genera `Builds/Windows/LastWish.exe` (ignorado por Git).

PowerShell, reemplazando las rutas del editor y proyecto si es necesario:

```powershell
$editor = 'C:/Program Files/Unity/Hub/Editor/6000.3.6f1/Editor/Unity.exe'
$project = (Get-Location).Path
& $editor -batchmode -nographics -projectPath $project -executeMethod LastWish.Editor.ProjectSetup.Configure -quit -logFile Logs/import.log
& $editor -batchmode -nographics -projectPath $project -runTests -testPlatform EditMode -testResults TestResults/editmode.xml -logFile Logs/editmode.log
& $editor -batchmode -projectPath $project -runTests -testPlatform PlayMode -testResults TestResults/playmode.xml -logFile Logs/playmode.log
```

Los comandos de tests no llevan `-quit`: el Test Runner cierra el editor.
En Windows espera a que termine el proceso antes de abrir otro editor del mismo
proyecto. Los resultados de pruebas, capturas, builds y cachés están ignorados.

Para compilar C# contra las bibliotecas locales sin abrir Unity y ejecutar los
casos NUnit del dominio con .NET 9, usa `Tools/Validate.ps1`. Esta validación no
sustituye al Test Runner ni comprueba el renderizado de Unity.

## Alcance de esta etapa

El port **no incluye todavía** mapa de la run, reliquias, gachapón, tienda,
recompensas, todos los enemigos/jefes, estados, reacciones elementales, todas las
cartas ni guardado de partida. Los valores de la demo son una base inicial;
no se promete paridad completa con el balance de Python. La selección de objetivo
es automática porque hay un único enemigo. La segunda etapa puede ampliar el
dominio sin mezclarlo con el Canvas.

El código Python, sus generadores y los diseños archivados no forman parte del
runtime Unity. El arte seleccionado procede de las versiones activas del juego;
consulta `THIRD_PARTY_NOTICES.md` y `docs/port-status.md`.

## Capturas de Unity

![Menú](docs/screenshots/menu.png)

![Combate](docs/screenshots/combat.png)
