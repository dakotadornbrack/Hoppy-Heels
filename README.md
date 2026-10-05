# Hoppy Heels

A vertical endless platformer for Android, in the spirit of Doodle Jump. Bounce from platform to platform, tilt your phone to steer, collect gems, and see how high you can climb before the screen catches up with you.

Built in **Unity 6** with **C#**. All art is my own.

## Gameplay

https://github.com/user-attachments/assets/1211ab07-34b0-4e6a-aa61-2caba8c9b7d3

## Features

- **Procedural level generation** — platforms spawn ahead of the player and are recycled behind, with difficulty that ramps up with height
- **Multiple platform types** — static, moving, and breakable platforms
- **Tilt controls** with adjustable sensitivity, and keyboard controls in the editor
- **Auto-scrolling camera** that kicks in partway up to keep the pressure on
- **Collectible gems** with a lifetime gem total and a persistent high score
- **Full game loop UI** — main menu, HUD, pause, settings, and game over, with notch-safe layout
- **Juice** — procedural squash and stretch on the player, parallax backgrounds, particle effects on pickups

## Technical highlights

- **Object pooling** for platforms and gems, so nothing is created or destroyed during play
- **Event-driven game state**: `GameManager` runs a `MainMenu → Playing ⇄ Paused → GameOver` state machine and broadcasts UnityEvents; UI panels and score tracking react instead of polling
- **Weighted, height-aware spawning** that keeps every gap reachable by tightening horizontal spread as vertical gaps grow
- **One-way platforms** via `PlatformEffector2D`, with tag-based collision dispatch for special platform behaviours
- **Editor tooling**: a `SceneBuilder` editor script rebuilds the entire scene (hierarchy, prefabs, UI, and all reference and event wiring) from a single menu item
- **Procedural animation**: player squash, stretch, and tilt driven by Rigidbody velocity, with no animation clips

## Project structure

```
Assets/
├── Scripts/
│   ├── Core/          GameManager (state machine), SettingsManager
│   ├── Player/        PlayerController, PlayerAnimator
│   ├── Platforms/     PlatformBase + Moving / Breakable / Bounce / Hazard
│   ├── Generation/    ObjectPool, PlatformSpawner
│   ├── Camera/        CameraFollow, ParallaxLayer
│   ├── Collectibles/  Collectible (gems)
│   ├── Scoring/       ScoreManager
│   ├── Audio/         AudioManager
│   ├── Effects/       GemSparkle
│   └── UI/            Menu / pause / settings / game-over panels, SafeArea
├── Editor/            SceneBuilder
├── Art/               Sprites
└── Prefabs/
```

## Running it

1. Install **Unity 6000.3.10f1** with the Android Build Support module.
2. Open this folder in Unity Hub.
3. Open `Assets/Scenes/SampleScene.unity` and press Play. Use **A/D** or the **arrow keys** to steer.

See [SETUP.md](SETUP.md) for scene setup details and a test checklist.

## Status

In active development, with a Google Play release planned.

## License

© 2026 Dakota Dornbrack. All rights reserved. This code is public for portfolio viewing only; see [LICENSE](LICENSE).
