# Rally 3D — Stage 01 "Pinar de Valdeniebla"

Arcade-realistic rally prototype built with Unity 6 (6000.0.84f1), URP 17, Input System.
Everything (terrain, road, textures, trees, props, cars, sky, sounds) is generated procedurally —
no third-party assets.

## Play

- Editor: open `Assets/Scenes/Stage01.unity` and press Play (click the Game view so it has focus).
- Standalone macOS build: `Builds/Rally3D.app`.

| Action | Keyboard | Gamepad |
|---|---|---|
| Throttle | W / ↑ | RT |
| Brake / reverse | S / ↓ | LT |
| Steer | A D / ← → | Left stick |
| Handbrake | Space | B / RB |
| Reset to road | R | Y |
| Camera (chase / far / hood / bumper) | C | View/Select |
| Pause | Esc | Start |
| Restart stage | Backspace | X |
| Start / confirm | Enter | A |

## Regenerating the stage

`Rally ▸ Build Stage (full)` rebuilds textures, materials, prefabs, terrain and the scene from code.
Tweak the data assets in `Assets/Settings/Rally` first:

- `Stage01` (`StageDefinition`): route segments, widths, surfaces, jumps, terrain shape, checkpoints.
- `CarTuning`: engine, gearbox, brakes, steering, suspension, tyres, assists.
- `SurfaceDatabase`: grip, drag, dust, debris and sound per surface (dirt, gravel, mud, asphalt, grass).

Batch mode: `Unity -batchmode -projectPath . -executeMethod Rally.EditorTools.StageBuilder.BuildFromCommandLine`

## Code layout (`Assets/Scripts`)

- `Car/` — `CarController` (WheelCollider physics + assists), `CarDrivetrain`, `CarWheel`, `CarBodyMotion`,
  `PlayerCarInput`, `CarFeedback`, `CarTuning`.
- `Camera/` — `RallyCamera` (chase camera, FOV, lean, shake, terrain avoidance, hood/bumper views).
- `Track/` — `TrackPath`, `Checkpoint`, `StageDefinition`, `TerrainSurfaceMap`, `Generation/` (route, terrain sculpting, road mesh).
- `AI/` — `AIDriver` (pure pursuit, curvature speed profile, mistakes, recovery).
- `Systems/` — `RaceManager`, `RaceParticipant`, `RallyInput`, surfaces, `GameBootstrap`, procedural mesh/noise helpers.
- `UI/` — `RaceHUD`, `StageMenus`, `UIFactory`.
- `Audio/` — `CarAudio`, `StageAudio`, `ProceduralAudio` (placeholder synthesis; real clips can be assigned).
- `VFX/` — dust/debris, skid marks, weather, speed post-effects.
- `Editor/` — the stage builder and asset factories.
