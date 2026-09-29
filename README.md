# Rally 3D — Tramo 01 «Pinar de Valdeniebla»

Prototipo de rally arcade-realista hecho con **Unity 6 (6000.0.84f1)**, **URP 17** y el **Input System**.
Todo (terreno, carretera, texturas, árboles, decorado, coches, cielo y sonidos) se genera por código:
no usa assets de terceros.

Se juega en **PC** (teclado o mando) y en el **navegador**, también en **móvil** (inclinación del teléfono,
botones táctiles o mando en pantalla). La versión web está preparada para publicarse en **itch.io**.

![Menú principal](docs/capturas/pc-menu.jpg)

## Capturas

| Elegir coche | Copiloto: notas de curva |
|---|---|
| ![Elección de coche con estadísticas](docs/capturas/pc-elegir-coche.jpg) | ![Aviso del copiloto IZQUIERDA 5 LARGA](docs/capturas/pc-copiloto.jpg) |

| Daños y penalización (+5 s al reiniciar) | Pausa con SALIR AL MENÚ |
|---|---|
| ![Barra de daños, humo y penalización](docs/capturas/pc-danos.jpg) | ![Menú de pausa](docs/capturas/pc-pausa.jpg) |

| En carrera detrás de los rivales | Controles |
|---|---|
| ![En carrera](docs/capturas/pc-carrera.jpg) | ![Pantalla de controles](docs/capturas/pc-controles.jpg) |

| Móvil — inicio | Móvil — botones + inclinación |
|---|---|
| ![Inicio en móvil](docs/capturas/movil-inicio.jpg) | ![Modo botones](docs/capturas/movil-botones.jpg) |

| Móvil — modo MANDO | Móvil — pausa (sensibilidad y diagnóstico del sensor) |
|---|---|
| ![Joystick y botones A B X Y](docs/capturas/movil-mando.jpg) | ![Pausa en móvil](docs/capturas/movil-pausa.jpg) |

| Móvil — opciones | Móvil sin sensor de inclinación (aparecen IZQUIERDA / DERECHA) |
|---|---|
| ![Opciones en móvil](docs/capturas/movil-opciones.jpg) | ![Botones de giro de reserva](docs/capturas/movil-sin-sensor.jpg) |

> Las capturas de móvil están hechas en un Android simulado (Chrome, 915×412 en horizontal).

## Cómo se juega

1. **Menú principal:** JUGAR, CONTROLES, OPCIONES y SALIR (SALIR solo fuera del navegador).
2. **JUGAR → elige tu coche** con las flechas y pulsa EMPEZAR. Hay tres coches:
   - **VALDENIEBLA #7**, equilibrado.
   - **AZUR #3**, el más rápido pero con menos agarre.
   - **CARMESÍ #11**, con mucho agarre y menos punta.

   Cada coche tiene decoración y prestaciones propias. El juego recuerda tu elección, también al usar REPETIR TRAMO.
3. **Durante el tramo:**
   - El **copiloto** avisa de cada curva (de 1, la más cerrada, a 6, la más rápida), de las **horquillas** y de los **saltos**.
     Lo hace en pantalla y, en el navegador, con voz en castellano.
   - Si **reinicias** (R), se suman **+5 s**.
   - En cada control ves la **diferencia con tu mejor tiempo**: verde si vas más rápido, rojo si vas más lento.
   - Los **choques abollan el coche**, le quitan potencia y hacen que la dirección tire hacia el lado dañado.
     Reiniciar no repara el coche; repetir el tramo, sí.
4. **Pausa:** CONTINUAR, REPETIR TRAMO, **SALIR AL MENÚ** (y la sensibilidad de la inclinación en el móvil).

### Opciones

| Opción | Valores |
|---|---|
| Volumen | 100 / 75 / 50 / 25 / 0 % |
| Daños | COMPLETOS · SOLO VISUALES · DESACTIVADOS |
| Copiloto | VOZ Y TEXTO · SOLO TEXTO · DESACTIVADO |
| Rivales | SÍ · NO (contra el reloj, como en un rally real) |
| Sensibilidad inclinación (móvil) | BAJA · MEDIA · ALTA · MUY ALTA |
| Acelerar solo (móvil) | SÍ · NO: el coche acelera solo salvo cuando frenas |

## Controles

### PC

| Acción | Teclado | Mando (Xbox / PlayStation) |
|---|---|---|
| Acelerar | W / ↑ | RT / R2 |
| Frenar / marcha atrás | S / ↓ | LT / L2 |
| Girar | A D / ← → | Stick izquierdo |
| Freno de mano | Espacio | B / Círculo o RB / R1 |
| Reiniciar en la pista | R | Y / Triángulo |
| Cámara (persecución / lejana / capó / paragolpes) | C | View / Share |
| Pausa | Esc (en el navegador también **P**) | Start / Options |
| Repetir tramo | Retroceso | X / Cuadrado |
| Empezar / confirmar | Enter (en el navegador también **clic**) | A / Cruz |

En el navegador, **P** también pausa porque a pantalla completa el navegador usa Esc para salir de ella.

### Móvil (navegador)

Toca la pantalla para empezar. Hay **dos modos**, y se cambia entre ellos con el botón que hay bajo **PAUSA**.
El juego recuerda el modo elegido.

| Modo | Girar | Acelerar | Frenar | Otros |
|---|---|---|---|---|
| **BOTONES** | Inclinar el móvil | ACELERAR | FRENAR | REINICIAR, PAUSA |
| **MANDO** | Joystick en pantalla | **A** (verde) | **B** (rojo) | **X** freno de mano (azul), **Y** reiniciar (amarillo) |

- **Inclinación:** al salir, y al volver de la pausa, la postura del móvil cuenta como «recto».
  La sensibilidad se cambia en **PAUSA → SENSIBILIDAD INCLINACIÓN**: BAJA, MEDIA, ALTA o MUY ALTA.
- **Sin sensor:** si el navegador no da datos de inclinación, a los 2 s aparecen **IZQUIERDA / DERECHA**.
  **Brave bloquea los sensores de movimiento**; usa Chrome, o el modo MANDO.
- **Diagnóstico:** en **PAUSA** aparece una línea `SENSOR: …` con el estado del sensor, para saber por qué no funciona la inclinación.

## Jugar y compilar

- **Editor:** abre `Assets/Scenes/Stage01.unity` y pulsa Play. Haz clic en la vista Game para que tenga el foco.
- **Build web desde la línea de comandos:**
  ```bash
  unity build . --target WebGL --execute-method Rally.EditorTools.WebBuild.Build \
    -o ~/Desktop/Rally3D_Web --editor-version 6000.0.84f1
  ```
  También se puede hacer desde **File ▸ Build Profiles ▸ Web ▸ Build**.

### Publicar en itch.io

1. Comprime **el contenido** de la carpeta del build, de modo que `index.html`, `Build/` y `TemplateData/`
   queden en la raíz del zip y no dentro de otra carpeta:
   ```bash
   cd ~/Desktop/Rally3D_Web && zip -r ../Rally3D_Web.zip index.html Build TemplateData -x "*.DS_Store"
   ```
2. En itch.io: **Kind of project = HTML**, sube el zip y marca **This file will be played in the browser**.
3. En **Embed options**: tamaño **1280 × 720**, activa **Fullscreen button** y **Mobile friendly** con
   orientación **Landscape**.

Ajustes del proyecto que ya vienen preparados para esto:
- **Compresión Gzip con *Decompression Fallback*:** funciona aunque el servidor no envíe `Content-Encoding`.
  Con Brotli sin fallback suele fallar en itch.
- **Canvas por defecto de 1280 × 720.**
- **Plantilla propia `Assets/WebGLTemplates/Rally3D`:** el juego ocupa todo el iframe, sin la barra inferior
  de Unity que cortaba el HUD. En el móvil limita la resolución a 1,5 píxeles por punto, para mantener los fps.

## Qué se ha hecho

### Versión web (WebGL)
- **Botón SALIR:** en web no aparece, porque `Application.Quit()` deja el canvas congelado.
- **Empezar:** con clic o toque, además de Enter; ese primer gesto también da el foco al iframe y desbloquea el audio.
- **Pausa:** con **P** además de Esc, y automática al perder el foco (por ejemplo, al hacer clic fuera del juego en itch).
- **Frecuencia de fotogramas:** `targetFrameRate = -1`, para que el navegador marque el ritmo con `requestAnimationFrame`; un valor fijo provocaba tirones.
- **Fuente:** en web se usa la integrada, porque el navegador no da acceso a las fuentes del sistema.
- **Audio de los coches:** no sonaba en web. Fijar `AudioSource.time` antes de `Play()` hacía que Unity pusiera en cola
  dos órdenes por canal mientras el audio estaba bloqueado. Al desbloquearse, la orden antigua detenía el sonido.
  Ahora, en web, ese desfase no se fija.
- **Todo el código específico de web** va bajo `#if UNITY_WEBGL && !UNITY_EDITOR`; en las demás plataformas no cambia nada.

### Móvil
- **Plugin de sensores `Assets/Plugins/WebGL/RallyMotion.jslib`:** el acelerómetro propio de Unity no arranca en
  Android Chrome si la API de permisos no responde exactamente «granted», algo habitual dentro del iframe de itch.
  El plugin lee `devicemotion`, la *Generic Sensor API* y `deviceorientation`, y usa la primera fuente que dé datos.
  Compensa la orientación de la pantalla y, donde el navegador lo exige (iOS y Chrome reciente), pide permiso en cada toque.
- **Inclinación:**
  - Ángulo en grados, con el giro completo a 22° en sensibilidad MEDIA.
  - Zona muerta de 2° y curva casi lineal (exponente 1,15).
  - Filtro **One Euro** (Casiez et al., CHI 2012), que quita el temblor sin añadir retraso.
- **Ayuda de dirección a alta velocidad**, solo para los controles táctiles: el coche reduce el ángulo de las ruedas
  con la velocidad (34° → 11° a 150 km/h), y la ayuda lo amplía hasta 17,6° a 150 km/h sin pasar nunca del máximo del coche.
  El teclado y los rivales no cambian.
- **Controles táctiles creados por código** (`TouchControls`, `TouchPedal`, `TouchJoystick`): modos BOTONES y MANDO, multitáctil.

### Menú, coches y reglas
- **Menú principal** (`MainMenu`): se construye por código sobre la escena real, con una cámara que gira alrededor del coche (`MenuCamera`).
  La carrera tiene un estado `Menu` previo al título. **SALIR AL MENÚ** recarga el tramo con el menú; **REPETIR TRAMO** lo recarga sin él.
- **Elección de coche** (`CarCatalog`): el jugador se queda la decoración elegida (pintura, colores, número y color en el mapa)
  y el rival que la llevaba recibe la suya. El jugador recibe una **copia propia** de la configuración (`CarTuning`)
  con los ajustes del coche, sin tocar la de los rivales ni el archivo del proyecto (`CarController.ApplyTuning`).
- **Daños** (`CarDamage`, en todos los coches):
  - Se calculan por zonas (delante, detrás, izquierda y derecha) a partir de la velocidad del impacto.
  - **Abolladuras reales** de la malla de la carrocería hacia dentro.
  - **Humo** cuando el frontal está muy dañado.
  - En modo COMPLETOS, **menos potencia** y **tirón de la dirección**. Esto va aparte del multiplicador de potencia que usa la IA.
- **Penalización y parciales:** +5 s por cada reinicio pedido por el jugador; el reinicio automático tras volcar es gratis.
  Los tiempos parciales del mejor recorrido se guardan y se comparan en cada control.
- **Copiloto** (`Pacenotes`, `CoDriver`, `Plugins/WebGL/RallySpeech.jslib`):
  - Las notas se generan a partir de la curvatura del recorrido: grado 1–6 según el radio más cerrado,
    HORQUILLA, LARGA, SE CIERRA, SE ABRE.
  - Los saltos se toman de la definición del tramo.
  - El aviso llega unos 2,6 s antes según la velocidad, y dos notas seguidas se dicen juntas («IZQUIERDA 3 Y DERECHA 4»).
- **Rendimiento en móvil** (`MobilePerformance`): sin desenfoque de movimiento, grano, aberración ni distorsión;
  bloom y antialiasing más ligeros; distancia de dibujado de 900 m (la niebla lo tapa); la mitad de partículas de clima;
  y como mucho 50 ms de física por fotograma.

### Interfaz y juego
- **Idioma:** todos los textos en castellano (HUD, menús, avisos, botones y carteles SALIDA / META del escenario).
- **HUD:** *Canvas Scaler* en Scale With Screen Size, 1920 × 1080 y Match 0,5. El nombre del tramo se ajusta solo
  en pantallas 4:3 y 3:2, y los textos de SALIDA / META ya no pisan la barra de progreso.
- **Polvo:** más ligero. Las nubes son más transparentes (55 %) y duran menos (55 %); los rivales levantan el 40 %
  del polvo. Se ajusta en el Inspector, apartado *Visibility* de `CarDustEffects`.

## Pruebas realizadas

### Tests automáticos de Unity
Se ejecutan con `unity test . --mode PlayMode` y `--mode EditMode`, en batch y sin abrir el Editor.

| Test | Qué comprueba | Resultado |
|---|---|---|
| `QA01_ResetAfterCuttingOffRoad…` | Al reiniciar tras salirse, el coche vuelve donde dejó la carretera | ✅ |
| `QA02_ResetOntoAnotherCar…` | Si el punto de reinicio está ocupado por otro coche, busca uno libre | ✅ |
| `QA03_CarOnItsRoof…` | El coche volcado se recupera solo | ✅ |
| `QA04_ConfirmOnResults…` | Enter en la pantalla de resultados recarga el tramo una sola vez | ✅ |
| `QA05_Stage01_IsReadyForAPlayerBuild` | La escena y sus dependencias están listas para un build | ✅ |
| `QA06_PlayerReset_AddsFiveSecondPenalty` | Reiniciar suma 5 s al reloj y al tiempo final | ✅ |
| `QA07_HardFrontalHit_DamagesCarAndReducesPower` | Un choque frontal a 72 km/h daña el coche, quita potencia y abolla la carrocería **hacia dentro** | ✅ |
| `QA08_Pacenotes_CallCornersBothWaysAndTheThreeJumps` | Notas de curva a ambos lados, grados válidos, en orden y los 3 saltos del tramo | ✅ |
| `QA09_CoDriver_CallsTheNextCornerOnScreen` | Al acercarse a una curva, el copiloto muestra la nota correcta | ✅ |

Se pasaron después de cada cambio (9 en total: 8 de PlayMode y 1 de EditMode).
`QA04` falla de vez en cuando justo después de una recompilación y pasa al repetirlo, así que parece intermitente.
`QA08` detectó que la primera versión del detector de saltos (por el perfil de altura) solo encontraba 1 de los 3;
ahora los saltos se toman de la definición del tramo.

### Compilación del código web
Unity no compila los bloques `#if UNITY_WEBGL && !UNITY_EDITOR` al ejecutar los tests en el Editor.
Por eso se compilaron aparte con el compilador de Unity (Roslyn), con los *defines* de web y los de macOS. Resultado: **0 errores**.

### Pruebas en el navegador (Chrome automatizado con CDP)
- **PC (1280 × 720):**
  - Carga del build.
  - Empezar con Enter o con clic.
  - Pausa con Esc y con P; menú de pausa sin botón SALIR; botón CONTINUAR con el ratón.
  - Pausa al perder el foco.
  - Conducción y capturas del HUD.
- **Audio:** se instrumentó el Web Audio del navegador (fuentes creadas, ganancia y nivel de señal por nodo).
  Así se localizó el fallo de los coches: los canales tenían ganancia 0,38 pero salida 0.
  Tras la corrección, la señal de los coches pasó de 0 a 0,10–0,39.
- **Android simulado** (agente de usuario Android, pantalla táctil, 915 × 412 en horizontal, acelerómetro simulado):

| Escenario | Resultado |
|---|---|
| Sensor normal | Giro en los dos sentidos (±0,38 con ±22°) |
| El navegador responde «prompt» a la API de permisos | La inclinación funciona igualmente |
| `requestPermission` rechaza el permiso (como en Chrome reciente) | La inclinación funciona igualmente |
| Solo `deviceorientation`, sin acelerómetro | Gira en el mismo sentido que con acelerómetro (±0,37) |
| Sin sensores | Aparecen IZQUIERDA / DERECHA a los 2 s y giran |
| Modo MANDO | A acelera hasta ~78 km/h; el joystick gira a derecha e izquierda |
| Pedal táctil ACELERAR | ~100 km/h en recta |

- **Menú y reglas, en PC (1280 × 720):**
  - Recorrido completo: menú → controles → opciones → elegir coche → cambiar a AZUR → empezar → carrera
    → pausa → SALIR AL MENÚ → de vuelta al menú con el coche elegido.
  - Choque real (acelerando sin girar): barra de daños, humo y **+5 s PENALIZACIÓN** al pulsar R.
  - Copiloto: «IZQUIERDA 5 · LARGA» antes de la primera curva.
- **Menú en el móvil simulado:** las opciones caben en una pantalla horizontal. Con **ACELERAR SOLO**, el coche va a 116 km/h sin tocar ningún pedal.
- **Fallos encontrados gracias a estas pruebas y ya corregidos:**
  - La elección de coche se perdía al usar REPETIR TRAMO o al volver al menú.
  - Las flechas ↑ ← → no existen en la fuente web.
  - El panel de información tapaba el coche en la pantalla de elección.
  - El botón VOLVER de Opciones se salía de la pantalla en el móvil.
  - ACELERAR SOLO no se aplicaba si se activaba desde el menú.
  - Eje de inclinación mal interpretado en horizontal: el volante se quedaba a tope y el coche no avanzaba.
  - El plugin esperaba un permiso que Chrome para Android nunca concede antes de escuchar el sensor (código 32 en el diagnóstico).
  - El botón MANDO no se podía pulsar en la pantalla de título.
  - Solapes del HUD al traducir al castellano.

### Pendiente de verificar en dispositivos reales
- **iPhone / iPad:** no se ha probado el permiso de movimiento de Safari.
- **Mando físico en el navegador:** el mapeo existe, pero no se ha probado con un mando conectado.
- **Rendimiento en móviles de gama baja.**
- **Sensibilidad de la inclinación:** cuál de los cuatro niveles se nota mejor con el móvil en la mano.
- **Voz del copiloto:** depende de las voces que tenga instaladas el navegador o el sistema; si no hay voz en castellano, lee con la voz por defecto.
- **Mejor tiempo en PC nativo:** se pierde el guardado anterior al cambiar el nombre del producto a «Tramo 01».

## Regenerar el tramo

`Rally ▸ Build Stage (full)` reconstruye desde código las texturas, materiales, prefabs, el terreno y la escena.
Antes, ajusta los assets de datos de `Assets/Settings/Rally`:

- **`Stage01` (`StageDefinition`):** tramos del recorrido, anchos, superficies, saltos, forma del terreno y controles.
- **`CarTuning`:** motor, caja de cambios, frenos, dirección, suspensión, neumáticos y ayudas.
- **`SurfaceDatabase`:** agarre, resistencia, polvo, piedras y sonido de cada superficie (tierra, grava, barro, asfalto, hierba).

En modo batch: `Unity -batchmode -projectPath . -executeMethod Rally.EditorTools.StageBuilder.BuildFromCommandLine`

## Estructura del código (`Assets/Scripts`)

- **`Car/`** (incluye `CarDamage`: daños y abolladuras):
  - `CarController`: física con WheelCollider y ayudas.
  - `CarDrivetrain`, `CarWheel`, `CarBodyMotion`, `CarFeedback`, `CarTuning`.
  - `PlayerCarInput`: teclado, mando, inclinación, controles táctiles y ayuda a alta velocidad.
- **`Camera/`:** `MenuCamera` (cámara giratoria del menú) y `RallyCamera`, con cámara de persecución, FOV, inclinación, vibración, evitación del terreno y vistas de capó y paragolpes.
- **`Track/`:** `Pacenotes` (notas del copiloto), `TrackPath`, `Checkpoint`, `StageDefinition`, `TerrainSurfaceMap` y `Generation/` (recorrido, esculpido del terreno, malla de la carretera).
- **`AI/`:** `AIDriver`, con *pure pursuit*, perfil de velocidad según la curvatura, errores y recuperación.
- **`Systems/`:** `RaceManager`, `RaceParticipant`, `RallyInput`, `CarCatalog` (coches elegibles), `MobilePerformance`, superficies,
  `GameBootstrap` y utilidades de mallas y ruido procedurales.
- **`UI/`:** `MainMenu`, `RaceHUD`, `StageMenus`, `CoDriver`, `UIFactory`, `TouchControls`, `TouchPedal`, `TouchJoystick`.
- **`Audio/`:** `CarAudio`, `StageAudio` y `ProceduralAudio` (síntesis provisional; se pueden asignar clips reales).
- **`VFX/`:** polvo y piedras, marcas de derrape, clima y postprocesado según la velocidad.
- **`Editor/`:** el generador del tramo, las fábricas de assets y `WebBuild` (build web por línea de comandos).
- **`Plugins/WebGL/`** (en `Assets/`): `RallyMotion.jslib` (sensores de movimiento) y `RallySpeech.jslib` (voz del copiloto).
