# AGENTS.md

Notes for anyone, human or AI agent, changing this code. `README.md` covers playing the game, its sections and
controls; this file covers how the code is put together and the rules that keep it working.

## What this is

A cinematic platform escape in the style of Éric Chahi's 1991 game Another World, in C# on .NET 10 and Avalonia 12,
drawn through Avalonia's `DrawingContext` as flat-shaded polygons. One self-contained executable per platform (Windows, macOS and Linux,
x64 and ARM). Everything in it is original: the graphics are drawn in code, and the sounds, alien voices and music
are synthesised by `tools/make_sounds.py`. There is no score and no high-score table: progress is kept by four-letter
checkpoint codes.

## Commands

```bash
dotnet build AVAAnotherWorld.slnx                 # the SDK version is in global.json (10.0.400 or later)
dotnet test AVAAnotherWorld.slnx                  # every test runs headless: no window opens
dotnet run --project src/AVAAnotherWorld          # play it; --no-audio for silence
AW_SHOTS=$PWD/Images dotnet test tests/AVAAnotherWorld.Tests --filter ScreenshotTests   # refresh the README's pictures
AW_PROBE=1 dotnet test tests/AVAAnotherWorld.Tests --filter PilotProbe   # report how the computer player does, section by section
python3 tools/make_sounds.py                      # rebuild src/AVAAnotherWorld/Sounds (standard-library Python only)
```

`Directory.Build.props` sets `TreatWarningsAsErrors`, nullable reference types and implicit usings for every
project: a warning fails the build, so keep it at zero.

## Layout

- `src/AVAAnotherWorld/Engine/`: the game, with no Avalonia drawing in it.
  - `GameEngine.cs` runs the states (title, code entry, demonstration, play, pause, the end) and the phases inside
    play (cut-scene, death, section done).
  - `Sim.cs` steps the hero one 60th of a second at a time; `Sim.Foes.cs` does the foes, the friend, bolts and
    shields, the cage and each section's hazards.
  - `Sections.cs` has the screen size (`Field`, including `Field.Step`) and the nine sections: floors, walls, zones and
    barriers.
  - `World.cs` holds the world state, the `Controls` record and the copyable `Dice` random source.
  - `Scenes.cs` lists the cut-scenes shot by shot (`SceneBook`).
  - `Pilot.cs` is the computer player, used by the attract mode and the tests.
  - `CodeEntry.cs` is the four-letter checkpoint code entry.
  - `InputHandler.cs` (keys) and `ControllerManager.cs` (SDL2 gamepads) produce `Controls`.
  - `SoundManager.cs` plays the effects, ambience loops and music.
- `src/AVAAnotherWorld/Views/`: `GameCanvas` (split over partial files for rooms, figures and cut-scenes) draws the
  1024x768 frame and runs the 60 Hz timer. Ctrl+S saves a screenshot to the Pictures folder.
- `src/AVAAnotherWorld/Audio/`: the OpenAL Soft audio engine, shared with the author's other AVA games.
- `src/AVAAnotherWorld/Sounds/`: the generated WAV files, copied next to the executable.
- `tests/AVAAnotherWorld.Tests/`: xUnit v3 with `Avalonia.Headless.XUnit`. `GameplayTests.cs` holds most tests;
  `PilotProbe.cs` and `ScreenshotTests.cs` are switched on by the variables above.
- `tools/`: `make_sounds.py`, and `speech.py`, a small formant speech synthesiser it uses for the aliens.

## Rules that keep it working

- **Fixed steps.** `GameEngine` runs the simulation in fixed 1/60 s steps (`Field.Step`, `Sim.Step`) from an
  accumulator, independent of the frame rate, so tests can play whole sections deterministically. Draw random
  numbers from the world's seeded `Dice` (`w.Dice`), never from `Random.Shared`. The pilot copies the world to look
  ahead, which only works if all randomness lives in it.
- **Engine first, drawing second.** Gameplay belongs in `Engine/`, where tests can reach it without a window.
  `Views/` only reads the world and draws it.
- **Input goes through `InputHandler`.** `Controls` carries `Dx` plus `Up` and `Down` flags, not a signed `dy`. On
  the gamepad a negative stick Y means up. Test new controls through `InputHandler`, not by setting `Controls`
  directly.
  - Keys are tracked by `PhysicalKey`, because on some Linux setups Avalonia reports the release of Shift as
    CapsLock. `ShiftReleaseTests` covers this.
- **SDL under one lock.** `ControllerManager` starts, polls and stops SDL only inside `lock (Sdl)`. The tests
  create many handlers in parallel, and unlocked SDL calls corrupt its heap.
- **Audio.** OpenAL Soft through Silk.NET. `Directory.Build.targets` swaps in the official Windows `soft_oal.dll`
  from `Audio/native/`, which needs no Visual C++ runtime. In a virtual machine `OpenAlConfig` writes a larger
  mixing period to `ALSOFT_CONF` so the sound doesn't break up.
- **Tests run silent.** Tests make `GameEngine { Silent = true }`. The game saves no files of its own, apart from
  Ctrl+S screenshots.

## Making a change

- Add or update tests with every gameplay change. `dotnet test` must pass, with no warnings.
- If a section changes, run the pilot probe and make sure the computer player still gets through.
- If the change shows on screen, refresh the screenshots with the command above, and look at them.
- Update `README.md` (sections, controls, features) to match.
- New sounds go into `tools/make_sounds.py`, not as hand-edited WAV files.

## CI and releases

`.github/workflows/build.yml` builds and tests every push and pull request. It then publishes self-contained
single-file builds for win-x64/x86/arm64, linux-x64/arm64/arm and osx-x64/arm64. Every run keeps the builds as
workflow artifacts. Only pushes to `main` replace the assets of the `latest` pre-release, and a `v*` tag makes a
proper release. After a release it asks the portfolio site to rebuild, if the `SITE_REFRESH_TOKEN` secret is set.
