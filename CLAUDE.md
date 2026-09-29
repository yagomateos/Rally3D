# CLAUDE.md — Rally 3D

Juego de rally en Unity **6000.0.84f1** con **URP 17**, publicado como **build web (WebGL) en itch.io**.
Cuatro tramos (`Stage01` pinar/tierra, `Stage02` nieve, `Stage03` desierto, `Stage04` costera de asfalto), rivales con IA, menú, elección de coche, daños, copiloto
y controles táctiles para móvil. Todo el contenido (terreno, carretera, coches, texturas, sonido, UI) se genera por código.

## Reglas de trabajo

- **Idioma:** responde al usuario en castellano. **Todo texto visible en el juego va en castellano** (HUD, menús,
  botones, carteles), sin palabras en inglés. Los comentarios y los identificadores del código van en inglés.
- **Cambia solo lo que se pide.** Nada de refactors, renombrados o "mejoras" de paso. Si ves algo mejorable, propónlo aparte.
- Si el usuario pide un **commit antes de empezar**, hazlo antes de tocar nada. No hagas `push` ni commits sin que se pida.
  El repositorio de GitHub es **privado**.
- En tareas por bloques, da un **resumen breve tras cada bloque** y, al final, una **lista de pruebas manuales**.
- Cuando algo no se pueda verificar (dispositivo real, mando físico, iOS), márcalo como **hipótesis a verificar**; no lo des por probado.
- **Builds:** el build web tarda unos 10 minutos. Lánzalo solo si la tarea lo pide o el usuario lo ha autorizado.
- Tras un build para publicar, regenera `~/Desktop/Rally3D_Web.zip` con `index.html` **en la raíz** del zip (ver README).
- Mantén el **README.md** (en castellano) al día cuando cambien controles, opciones, dificultad o tests.

## Estructura

- `Assets/Scripts` → ensamblado `Rally.Runtime` (namespace raíz `Rally`):
  `AI`, `Audio`, `Camera` (`Rally.CameraSystem`), `Car`, `Systems` (+ `Procedural`), `Track` (+ `Generation`), `UI`, `VFX`.
- `Assets/Scripts/Editor` → `Rally.Editor` (`Rally.EditorTools`): generadores del tramo (`StageBuilder`, `TerrainBuilder`,
  `CarFactory`, `PropFactory`, `MaterialFactory`, `ProceduralTextures`, `TextureBaker`, `StageTheme`), `ProjectSetup` y `WebBuild`.
- `Assets/Tests/PlayMode` y `Assets/Tests/EditMode` → `Rally.Tests` (utilidades en `StageTestUtility`).
- `Assets/Plugins/WebGL/*.jslib`: `RallyMotion` (sensores de inclinación) y `RallySpeech` (voz del copiloto, es-ES).
- `Assets/WebGLTemplates/Rally3D`: plantilla web propia (ocupa todo el iframe, sin pie, DPR limitado a 1,5 en móvil).
- Terreno: resolución pensada para la descarga web (alturas 1025, pintura 512, hierba 512; `TerrainBuilder`).
  No la subas sin medir el tamaño del build: el terreno fue el 78 % de la descarga. `QA14` vigila que no asome por la carretera.
- Los *normal maps* no llevan sufijo de tema: todos los tramos comparten el mismo archivo (`TextureBaker`).
- `Assets/Resources/DifficultyData.asset`: niveles FÁCIL / NORMAL / DIFÍCIL (`Systems/DifficultyData.cs`).
- Escena `Stage01` (raíces): `Terrain`, `Track`, `Stage Dressing`, `Systems`, `Cars`, `RaceManager`, `Main Camera`,
  `Sun`, `SkyReflectionProbe`, `PostProcessVolume`, `SkidMarks`, `Weather`, `StageAudio`, `HUD`, `Menus`.

### Escenas generadas: cuidado

Las cuatro escenas las crea **`StageBuilder`** (menú **Rally ▸ Build Stage (full)** / **Build Snow Stage 02** / **Build Desert Stage 03** /
**Build Coast Stage 04**, o `BuildFromCommandLine` / `BuildSnowFromCommandLine` / `BuildDesertFromCommandLine` / `BuildCoastFromCommandLine`).
Regenerar **sobrescribe la escena**. Los tramos 02–04 usan los prefabs de coche del 01: genera el 01 primero.
- Los cambios de comportamiento van en código (componentes que añade `RaceManager.Start`, UI construida en código),
  no a mano en la escena.
- Los tramos 02–04 reutilizan el generador con `StageTheme.Kind.Snow` / `Desert` / `Coast`: sus assets llevan el sufijo `_Snow` / `_Desert` / `_Coast`
  y no pisan los del tramo 01. En código de runtime el tema se lee de `StageDefinition.theme` (`ForestTheme` … `CoastTheme`).
- Reglas por tramo (p. ej. `topSpeedScale` de la costera) van en `StageDefinition` y se aplican a la **copia** de ajustes del jugador
  (`CarCatalog.Apply`) y al plan de velocidad de la IA (`AIDriver.ApplyDifficulty`), nunca al asset compartido.
- Nada de luces reales para ambientar (farolas, etc.): material emisivo + *bloom*. Las luces dinámicas hunden el rendimiento web.
- Un recorrido nuevo no debe cruzarse consigo mismo: compruébalo antes de generarlo (distancia mínima entre partes no contiguas).
- No edites a mano el YAML de `.unity` / `.prefab` si hay un Editor conectado (`unity status`); usa el Editor.

## Convenciones de código

- Un `MonoBehaviour` por fichero, namespace según la carpeta, comentarios `/// <summary>` que explican el **porqué**.
- Campos de ajuste: `[SerializeField] private` con `[Tooltip]` y `[Header]`. Son **`public`** solo cuando el usuario
  pide que sean editables desde fuera (por ejemplo, las masas, `pushForce` o `pushRecoveryTime`).
- API de Unity 6: `Rigidbody.linearVelocity` (no `velocity`), `PhysicsMaterial`, `FindFirstObjectByType` / `FindAnyObjectByType`.
- **Entrada:** solo **Input System** (`activeInputHandler: 1`). Todas las acciones están en `Systems/RallyInput.cs`,
  con teclado y mando. No uses `UnityEngine.Input`. Antes de asignar una tecla, comprueba que no esté ya usada
  (↓ = frenar/marcha atrás, P = pausa en web, Q = mirar atrás, stick derecho = mirar alrededor).
- **UI:** uGUI construida en código con `UIFactory` (colores `Accent`, `PanelDark`, `TextMain`...).
  - `CanvasScaler`: 1920 × 1080, Match 0,5.
  - Orden de los canvas: HUD 10, táctil 15, menús 20, menú principal 30, pantalla de salir 50.
  - La fuente web no tiene flechas (↑ ← →): escribe los nombres de las teclas en texto.
- **Configuración del jugador:** `PlayerPrefs` con prefijo `Rally.` (`Rally.Difficulty`, `Rally.Volume`, `Rally.SelectedCar`...)
  y `PlayerPrefs.Save()` después de cada cambio (en web, si no, se pierde).
- **Física:** paso fijo de 0,01 s (`GameBootstrap`); los coches usan `WheelCollider` en la capa `Vehicle` (índice 8).
  El ajuste del jugador es una **copia** de `CarTuning` (`CarController.ApplyTuning`): no modifiques el asset compartido en runtime.
- **Tests:** `QA<nn>_<Escenario>_<ResultadoEsperado>`. Las mediciones largas se marcan como `[Explicit]`.
  Cada funcionalidad nueva de juego lleva su test de PlayMode.

## Compatibilidad web (WebGL / itch.io)

- **Ajustes:**
  - Gzip + *Decompression Fallback*.
  - Plantilla `PROJECT:Rally3D`, sin hilos, memoria de 32 MB que crece hasta 2 GB.
  - Espacio de color lineal; *power preference* alto rendimiento.
  - `ProjectSetup.Apply` reescribe `productName` ("Rally 3D") y otros ajustes: mantenlos coherentes.
- Todo lo exclusivo del navegador va entre `#if UNITY_WEBGL && !UNITY_EDITOR`.
  Los tests del Editor **no compilan esos bloques**: compílalos aparte (Roslyn de Unity con los *defines* de WebGL)
  o comprueba el build.
- **Trampas ya encontradas:**
  - `Application.targetFrameRate` debe ser **-1** en web; cualquier otro valor tartamudea.
  - `AudioSource.time` antes de `Play()` deja los bucles **mudos** en web (Unity encola un Play doble mientras el audio está bloqueado).
  - El audio solo arranca tras un gesto del usuario.
  - A pantalla completa, **Esc** lo usa el navegador para salir de ella: la pausa también va con **P**.
  - `Application.Quit()` no cierra la pestaña: en web, "Salir del juego" muestra una pantalla final.
  - **Sensores en Android:** escucha `devicemotion` en cuanto cargue y pide permiso en el primer toque.
    El Input System ya compensa la orientación de la pantalla. **Brave bloquea los sensores.**
  - La pausa salta al perder el foco (`OnApplicationFocus`).
- **Móvil:**
  - `MobilePerformance` quita los efectos caros, acorta la distancia de dibujado y limita la física a 50 ms por fotograma.
  - Dificultad FÁCIL por defecto.
  - Controles táctiles: modo BOTONES (inclinación) y modo MANDO (joystick + A/B/X/Y), más PAUSA, REINICIAR y ATRÁS.
- Verifica en un navegador de verdad (Chrome sin cabeza por CDP, con Android emulado y el acelerómetro simulado).
  Sirve el build con `python3 -m http.server` desde la carpeta del build.

## Compilar y probar

```bash
# Tests (batch, sin abrir el Editor)
unity test . --mode PlayMode
unity test . --mode EditMode

# Build web
unity build . --target WebGL --execute-method Rally.EditorTools.WebBuild.Build \
  -o ~/Desktop/Rally3D_Web --editor-version 6000.0.84f1

# Zip para itch.io (index.html en la raíz)
cd ~/Desktop/Rally3D_Web && zip -r ../Rally3D_Web.zip index.html Build TemplateData -x "*.DS_Store"
```

`QA04` (Enter en resultados) falla a veces justo después de recompilar y pasa al repetirlo: es intermitente, no una regresión.

## Estado del proyecto (septiembre de 2026)

**Hecho:**
- Cuatro tramos (pinar, nieve, desierto, costera de asfalto) con copiloto (voz + texto), rivales con IA y ayuda de alcance, y dificultad en `DifficultyData`
  (tiempos del rival en el tramo 01: 2:04 / 1:53 / 1:42).
- Daños por zonas, +5 s por reinicio, tiempos parciales y cámaras (persecución, lejana, capó, paragolpes, mirar atrás).
- Golpes y empuje a baja velocidad entre coches.
- Menú principal, elección de coche, opciones, pausa con salir al menú / salir del juego.
- Motor sintetizado (`ProceduralAudio`): el usuario **prefirió volver a él** en lugar de las grabaciones CC0.
- Ovejas que cruzan en la costera (`SheepCrossing`/`Sheep`); el daño de choque escala con la masa del objeto golpeado.
- Paredes invisibles en el borde del mapa (`MapBounds`, creadas en runtime), props ligeros derribables (`Knockable`, no estáticos),
  navegación de menús con stick y con vuelta (`MenuNavigation.WrapColumn`) y celebración al ganar (`Celebration`).
- Un prop que el coche pueda derribar debe llevar `Knockable` (collider trigger hasta el golpe); los sólidos, collider normal.
- Cuenta atrás fuera de la carretera con vuelta automática, ayuda de conducción (`DrivingAssist`), ovejas en la carretera en los tramos 01 y 04.
- Controles de móvil y 28 tests (27 PlayMode + 1 EditMode) más dos pruebas *Explicit* (medición de dificultad y capturas de la oveja).
- Recortes para la web del terreno, los *normal maps* y la pantalla de inicio. Build final con 4 tramos: **41,6 MB**
  (antes 51,4 MB con 2). Carga con caché vacía: 19 s a 20 Mbps y 36 s a 10 Mbps. Cada tramo nuevo añade unos 6–8 MB.

**Pendiente de verificar en dispositivos reales:**
- Permiso de movimiento en iPhone / iPad (Safari).
- Mando físico en el navegador, incluido R3 para mirar atrás.
- Rendimiento en móviles de gama baja.
- Que NORMAL se gane en 2–3 intentos con jugadores reales.
