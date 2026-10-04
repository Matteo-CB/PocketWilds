# Pocket Wilds (provisional name)

Mobile portrait idle game. 3D chibi animals in a living diorama.
Source of truth: the project Google Drive ("Projet Idle Animals - nom provisoire").

## Versions

- Unity 6000.3.8f1 (6.3 LTS), URP 17.3
- Blender 5.2 LTS (sources in `SourceArt/Blender`)

## Layout

| Path | Content |
| --- | --- |
| `Assets/_Game/Core` | IDs, BigValue, clock, pooling, logging, profiler markers |
| `Assets/_Game/Content` | Runtime definitions and registry (`Scripts/`), content data, quality/URP assets |
| `Assets/_Game/Simulation` | Player state, economy (pure C#, no scene) |
| `Assets/_Game/Services` | Save, device settings |
| `Assets/_Game/Presentation` | Views, pools, scene entries, frame rate and quality |
| `Assets/_Game/UI` | Screens and UI helpers |
| `Assets/_Game/Bootstrap` | Composition root (`SC_Bootstrap`) and the single game loop |
| `Assets/_Game/Benchmarks` | Editor and development builds only |
| `Assets/_Game/Editor` | Setup, build, save fixtures, production status |
| `Assets/_Game/Tests` | EditMode, PlayMode, golden saves |
| `SourceArt/` | Blender, audio and UI sources (not imported by Unity) |

Assembly direction: Core <- Content <- Simulation <- Services <- Presentation <- UI <- Bootstrap. Simulation never references Presentation or UI.

## Commands

Run from the project folder with the Unity editor path set:

```bash
Unity.exe -batchmode -projectPath . -executeMethod PocketWilds.Editor.Setup.ProjectSetup.RunBatch
Unity.exe -batchmode -projectPath . -runTests -testPlatform EditMode -testResults TestResults/editmode.xml
Unity.exe -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults TestResults/playmode.xml
Unity.exe -batchmode -projectPath . -buildTarget Android -executeMethod PocketWilds.Editor.Build.BuildTools.BuildAndroidDevelopmentBatch
```

In the editor: `Pocket Wilds/Play From Bootstrap` starts like a real launch.

## Production tracking

`Assets/_Game/Editor/Production/AssetProductionStatus.json` holds the execution status of every manifest asset and of the MCP Production Queue (T00 to T30).
