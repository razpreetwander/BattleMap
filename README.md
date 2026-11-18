# BattleMaps (`com.xsupreme.battlemaps`) — Clean-Room Unity 6 Source Reconstruction

Built from REA static reverse-engineering of `D:\RE\base.apk` (Unity 6 IL2CPP metadata v39 + 46 Addressables bundles).

## How to Run in Unity Editor (Unity 6 / 2022.3 LTS+)
1. Open Unity Hub -> **Add project from disk** -> select `D:\RE\BattleMaps_Unity`.
2. Open the project.
3. Click **`BattleMaps -> Create & Open Main Scene`** in the top menu bar (or simply hit **Play** in any scene — `BattleMapsRuntimeBootstrapper` auto-initializes on Play!).
4. Controls:
   - **Move:** Click & drag mouse / touch virtual joystick, OR **WASD / Arrow Keys**.
   - **Activate Character Power:** Press **`SPACE`** or tap the **Power Button** in the bottom-right HUD.
   - **Pause:** Click **`||`** in top-left HUD.

## Complete Module Map (A to Z)
| Directory | Namespace | Reconstructed Systems |
|---|---|---|
| `Assets/Scripts/GameFlow/` | `Libraries.GameFlow.FSM` | `GameFSM`, `FSMStateBase`, `Push/Pop/ClearStack` async state stack |
| `Assets/Scripts/Signals/` | `BattleMaps.Signals` | `EventBus` + gameplay signals (`PlayerSpawned`, `PlayerKilled`, `ZoneCaptured`, `ZoneStolen`, `MatchEnded`, `FlyingText`, `PowerActivated`) |
| `Assets/Scripts/Clipper/` | `BattleMaps.Clipper` | `BattleMapsClipper`, `Clipper2CSharpLib` (Planar Arrangement Half-Edge boolean Union/Difference/Intersection), `ZoneUtils`, `ZoneMeshBuilder` (Earcut 3D raised slabs) |
| `Assets/Scripts/CollisionSystem/` | `BattleMaps.CollisionSystem` | `CollisionWorld`, `EdgeGridWorld` (spatial hash grid), `ArenaBorder`, `HeadPose`, triggers & shield shells |
| `Assets/Scripts/Gameplay/` | `BattleMaps.Gameplay` | `GameMode`, `ZoneStatus`, `ZoneBase`, `PlayerBase`, `ZoneManager`, `PlaygroundView`, `GameplayController`, `GameManager` |
| `Assets/Scripts/Bots/` | `BattleMaps.Bots.BotProfiles` | `BehaviorTreeAIController`, `AIControllerState` (6 states), `BotCategory` (37 archetypes incl. 3×3×3 BT matrix), `Aggression/Perception/LoopingProfileModel` |
| `Assets/Scripts/DifficultyTuningSystem/` | `BattleMaps.DifficultyTuningSystem.Controller` | `DifficultyTuningSystemController` (dynamic difficulty scaling) |
| `Assets/Scripts/CharacterPowers/` | `BattleMaps.CharacterPowers.*` | All **44 `PowerType`** powers (`PowersFactory`), `PowerBase`, `CharactersManager` (12 unlockable/upgradable heroes) |
| `Assets/Scripts/GameplayMechanics/` | `BattleMaps.GameplayMechanics` | `GameplayMechanicsController` (`SpeedBoost` combo pads, `JumpingRamp` arc + stomp kill, `Tunnel` dive + emerge kill), `EnemyTrain` + `PumpTower` |
| `Assets/Scripts/Modes/` | `BattleMaps.Modes` | `MatchModeRulesController` (`TeamBattle` 4v4, `PaintBrawl`), `MetaProgressionController` (World 5 continents / 25 countries, 6 Chapters, Power Chests, Upgrades) |
| `Assets/Scripts/SaveStates/` | `BattleMaps.SaveStates` | `SaveStateController` persistent JSON save (`Coins`, `Trophies`, `XP`, `Characters`, `World`, `Chapters`, `DailyMissions`, `Events`, `Settings`) |
| `Assets/Scripts/UI/` | `GameOps.UIFramework` & `BattleMaps.UI.Screens` | **All 15 Screens & Popups**: Home, Gameplay HUD (Leaderboard + Minimap + Power + Flying Text), Revive Popup, Level End, Pause, Heroes/Powers, Shop & Chests, World Map (5 Continents), Chapters, Party Lobby, Daily Missions, XP Road, Bonus Mini-Game (3×3 Picker), Live Events, Settings |
| `Assets/Scripts/Maps/` | `BattleMaps.World.Controller` & `BattleMaps.Gameplay` | `WorldCountryGeometryResolver` (25 country-shaped PolygonArenas + map pins), `OrganicLevelShapeModel` (6 organic Chapter arenas) |
| `Assets/Scripts/VFX/` & `Assets/Shaders/` | `BattleMaps.VFX` & `BattleMaps.CharacterOutfits` | `BattleMapsZoneShader.shader` (custom slab + capture sweep), `CharacterOutfitBuilder` (10 3D outfits), `GameplayVfxController` (capture/kill/power/edge-spark ParticleSystems) |
| `Assets/Scripts/Localization/` | `BattleMaps.Localization` | `LocalizationManager` (English, Hinglish, Spanish, French) |
| `Assets/Scripts/Networking/` & `Server~/` | `BattleMaps.Features.Multiplayer` | `BattleMapsMultiplayerClient` (WebSocket LAN/online client) + zero-dependency Node.js `battlemaps_relay_server.mjs` |
| `Assets/Scripts/Audio/` | `BattleMaps.Audio` | `CoreGameplaySfxController` procedural SFX + looping synth BGM synthesizer |
| `Assets/Scripts/Bootstrap/` & `Assets/Editor/` | `BattleMaps.Bootstrap` / `BattleMaps.EditorTools` | Turnkey runtime bootstrapper & one-click Unity Editor scene builder |

## Provenance & Exclusions
- **Derived from REA Evidence:** Namespaces, enums (`PowerType`, `GameMode`, `ZoneStatus`, `AIControllerState`, `BotCategory`, `GameplayMechanicsPadKind`), model fields (`JumpingRampDataModel`, `TunnelDataModel`, `GroundSpeedBoostDataModel`, `Aggression/Perception/LoopingProfileModel`, `CharacterConfig`), and core architecture (`BattleMapsClipper`, `ZoneUtils`, `CollisionWorld`, `GameFSM`).
- **Reconstructed by Us:** Full C# algorithm implementations (since `libil2cpp.so` native code was absent from the split `base.apk`), procedural 3D meshes, procedural UGUI screens, procedural audio, and tuned gameplay constants.
- **Intentionally Excluded:** All third-party X Supreme ads, mediation, analytics, IAP billing, and proprietary server/Nakama credentials.
