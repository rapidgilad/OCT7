# Sound overrides

The game synthesizes its placeholder sound effects in code (`game/scripts/Audio/SoundSynth.cs`).
Drop a recording here with one of these names (`.wav` or `.ogg`) and it replaces the synthesized sound. No code change is needed.

| File | Used for |
|---|---|
| `rifle.wav` | Rifles, carbines, assault rifles |
| `machine_gun.wav` | MAG / PKM teams, vehicle machine guns |
| `sniper.wav` | Sniper rifles |
| `cannon.wav` | Tank guns; the autocannon plays it pitched up |
| `rocket_launch.wav` | RPGs and ATGMs (Kornet, Spike) |
| `explosion.wav` | Rocket and shell impacts, destroyed vehicles and buildings (played at different pitches) |

Tips:
- **Format:** short mono files, 44.1 kHz. Trim the silence at the start.
- **Varied sounds:** each shot already gets a random pitch (±7%) and volume. A single good recording per sound is enough.
- **Git:** `.wav`, `.ogg` and `.mp3` are tracked with Git LFS (`git lfs install` once).
- **Preview:** export the current synthesized set with `godot --headless --path game -- --export-sounds ~/oct7-sounds`.
