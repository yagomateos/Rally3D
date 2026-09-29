# Audio

All sounds are currently synthesised at runtime by `ProceduralAudio` (engine, turbo, gravel, squeal,
rolling, wind, impacts, landings, ambience, countdown beeps).

To use real recordings, drop the clips in this folder and assign them on:

- `CarAudio` (car prefabs in `Assets/Prefabs/Cars`): engine on/off load loops (recorded at ~3000 rpm,
  or set `Clip Base Rpm`), turbo, gravel slide, tarmac squeal, rolling, wind, impact, landing.
- `StageAudio` (scene object `StageAudio`): ambience loop.

Any field left empty keeps its procedural fallback.
