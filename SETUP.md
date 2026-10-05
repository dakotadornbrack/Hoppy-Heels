# Hoppy Heels — Scene Setup Guide

## Unity version
**Unity 6 (6000.3.10f1)** (Android build support module required)

TextMeshPro is bundled in `com.unity.ugui` 2.0 — no separate import needed in Unity 6.

---

## Project settings (do once)

| Setting | Value |
|---|---|
| **Player → Other Settings → Active Input Handling** | **Both** — scripts use legacy `Input.GetAxis` / `Input.acceleration` alongside the New Input System |
| **Player → Other Settings → Color Space** | Linear |
| **Player → Other Settings → Auto Graphics API** | On (defaults to OpenGLES3 on Android) |
| **Player → Other Settings → Minimum API Level** | Currently API 25 (Android 7.1) |
| **Player → Other Settings → Package Name** | Still the template default `com.DefaultCompany.2D-URP` — set a real one before any store release |
| **Render Pipeline** | URP with 2D Renderer (already configured) |

---

## Quick setup (recommended)

1. Open `Assets/Scenes/SampleScene.unity`, or create a **new empty scene**.
2. Run **Hoppy Heels > Build Skeleton Scene** from the menu bar.
3. *(Optional)* Select **AudioManager** and assign AudioClips.
4. Save the scene (Ctrl+S) and make sure it's in Build Settings.

The builder:
- Ensures tags `Player`, `BouncePlatform`, `HazardPlatform`, `Gem` and layer `Platform` (index 6) exist.
- Loads prefabs from `Assets/Prefabs/` (Player, StaticPlatform, MovingPlatform, BreakablePlatform, Gem, Background, NeonSigns). If a prefab is missing, it creates a placeholder-art version. **Existing prefabs are never overwritten**, so custom art and collider tweaks survive rebuilds.
- Creates every scene object below and wires all serialized references and button/UnityEvent listeners.

> The builder **does not clear the scene** first; it only reuses an existing Main Camera. Running it on an already-built scene creates duplicate managers, spawners, and UI. Delete the old objects (or use a fresh scene) before rebuilding.

---

## Scene hierarchy

```
Scene
├── GameManager          GameManager + ScoreManager + SettingsManager
├── AudioManager         AudioManager (DontDestroyOnLoad)
├── Main Camera          Camera (ortho, size 7) + CameraFollow → target: Player
├── Player               Player prefab instance (see below)
├── PlatformSpawner      PlatformSpawner
│   ├── StaticPool       ObjectPool → StaticPlatform prefab
│   ├── MovingPool       ObjectPool → MovingPlatform prefab
│   ├── BreakablePool    ObjectPool → BreakablePlatform prefab
│   └── GemPool          ObjectPool → Gem prefab
├── Background           Background prefab (ParallaxLayer, factor ~0.05, sorting −20)
├── NeonSigns            NeonSigns prefab (ParallaxLayer, factor ~0.4, sorting −10)
├── UI Canvas            Screen Space Overlay, scale with screen size 1080×1920, match 0.5
│   ├── HUD              SafeArea — ScoreText, BestText, HudGemsText, PauseButton
│   ├── GameOverPanel    Score / Best / Gems / Total Gems texts, "Try Again" (starts hidden)
│   ├── MainMenuPanel    Best, Total Gems, "Play", "Settings" (starts visible)
│   ├── PausePanel       "Resume", "Restart", "Settings", "Menu" (starts hidden)
│   └── SettingsPanel    Sound toggle, sensitivity slider + value, "Back" (starts hidden)
└── EventSystem
```

---

## Prefab requirements

If you replace prefab art or make new prefabs by hand, keep these intact:

### Player
- Root: Tag **Player**, **Rigidbody2D** (Gravity 2.5, Collision Detection Continuous, Freeze Rotation Z), **BoxCollider2D**, **PlayerController**, **PlayerAnimator**.
- Visuals go on a **child** object. `PlayerAnimator` squashes, stretches, and tilts the *first child* (the current prefab has `Outline` → `HeelCoior`). If the sprite sits on the root with no children, the animation does nothing.

### Platforms

| Prefab | Script | Tag |
|---|---|---|
| StaticPlatform | `PlatformBase` | — |
| MovingPlatform | `MovingPlatform` | — |
| BreakablePlatform | `BreakablePlatform` | — |
| *(not spawned yet)* Bounce | `BouncePlatform` | `BouncePlatform` |
| *(not spawned yet)* Hazard | `HazardPlatform` | `HazardPlatform` |

Each platform needs a **BoxCollider2D with Used By Effector** and a **PlatformEffector2D** (one-way, surface arc ~170°) so the player can jump up through it. The existing prefabs have this. Art lives on a `PlatformColor` child.

> Bounce and Hazard platforms are coded but have no pool or prefab. To use them, create the prefab, add an ObjectPool for it under PlatformSpawner, and add a branch in `PlatformSpawner.PickPool()`.

### Gem
Tag **Gem**, **CircleCollider2D** (Is Trigger), **Collectible** script. Art goes on a `GemColor` child.

---

## Wiring reference (what the builder hooks up)

| Source | Calls |
|---|---|
| Play button | `GameManager.StartGame` |
| Pause button (HUD) | `GameManager.PauseGame` |
| Resume | `GameManager.ResumeGame` |
| Restart (pause) / Try Again (game over) | `GameManager.RestartGame` |
| Menu (pause) | `GameManager.QuitToMenu` |
| Settings (menu & pause) / Back | `SettingsPanel.Show` / `Hide` |
| `onGameStarted` | MainMenuPanel.Hide, SettingsPanel.Hide, PauseButton.Show, ScoreManager.StartTracking |
| `onGamePaused` | PausePanel.Show, PauseButton.Hide |
| `onGameResumed` | PausePanel.Hide, SettingsPanel.Hide, PauseButton.Show |
| `onGameOver` | GameOverPanel.Show, PauseButton.Hide |

ScoreManager's TMP_Text fields and SettingsPanel's toggle, slider, and label are also assigned automatically.

> RestartGame and QuitToMenu both reload the scene, which always opens on the main menu.

---

## Tuning knobs

| Component | Field | Default |
|---|---|---|
| PlayerController | jumpForce / boostJumpForce / maxSpeed | 16 / 23 / 9 |
| SettingsManager | tilt sensitivity (player-adjustable) | 22 (range 10–40) |
| PlatformSpawner | minGapY / maxGapY / maxGapYHard | 1.0 / 1.8 / 2.6 |
| PlatformSpawner | difficultyRampHeight | 100 |
| PlatformSpawner | movingChance / breakableChance | 0.20 / 0.10 |
| PlatformSpawner | gemSpawnChance | 0.30 |
| CameraFollow | autoScrollStartHeight / BaseSpeed / Accel | 30 / 0.4 / 0.01 |
| CameraFollow | graceTime | 2 s |
| Collectible | pointValue | 50 |
| ScoreManager | pointsPerUnit | 10 |

These are the code defaults; values saved in the scene or prefabs override them.

---

## Editor test checklist

- [ ] Press Play — main menu shows Best and Total Gems; gameplay is frozen
- [ ] **Play** hides the menu, shows the HUD pause button, and the player starts bouncing on the first platform
- [ ] A/D (or Left/Right arrows) steers; player wraps from one screen edge to the other
- [ ] Player passes up through platforms and only bounces when landing on top
- [ ] Camera scrolls up as player climbs and never scrolls down
- [ ] Above height ~30 the camera keeps creeping up on its own and speeds up over time
- [ ] Moving platforms oscillate left/right
- [ ] Breakable platforms disappear shortly after landing
- [ ] Gems spawn, add 50 points and a gem on pickup, and show a sparkle
- [ ] Score and gem count update in the HUD
- [ ] Pause → Resume / Restart / Menu / Settings all work
- [ ] Settings: sound toggle mutes audio; sensitivity slider changes steering speed; both persist after restarting Play mode
- [ ] Falling below the camera bottom triggers game over (not within the first 2 s)
- [ ] Game-over panel shows Score, Best, Gems, Total Gems; **Try Again** returns to the main menu
- [ ] High score and total gems persist between sessions

## On-device checks (Android)
- [ ] Tilt steering feels right at default sensitivity
- [ ] HUD stays clear of the notch/camera cutout (SafeArea)
- [ ] Background fills the screen width without edge gaps
