using System;
using BattleMaps.CollisionSystem;
using BattleMaps.Signals;
using UnityEngine;

namespace BattleMaps.Gameplay
{
    /// <summary>
    /// Reconstructed from GameplayController (IL2CPP index 138, 190 methods).
    /// Drives the main match loop, input steering, trail-kill resolution, camera zoom adaptation,
    /// bot respawning, and win/loss/revive transitions.
    /// </summary>
    public class GameplayController : MonoBehaviour
    {
        [Header("Core References")]
        public ZoneManager ZoneManager;
        public PlaygroundView PlaygroundView;
        public Camera GameplayCamera;

        [Header("Match Settings")]
        public float ArenaRadius = 32f;
        public int TargetBotCount = 8;
        public float BotRespawnInterval = 2.5f;
        public float WinThresholdPercent01 = 0.995f;

        [Header("Camera Settings")]
        public Vector3 CameraOffset = new Vector3(0f, 22f, -11f);
        public float CameraFollowSmooth = 7f;
        public float CameraSizeMultiplier = 1f;
        public float MechanicCameraZoom = 1f;
        public float VisionPowerZoom = 1f;

        public CollisionWorld CollisionWorld { get; private set; }
        public GameMode CurrentMode { get; private set; } = GameMode.Default;
        public ArenaSubMode CurrentSubMode { get; private set; } = ArenaSubMode.Classic;
        public bool IsInGame { get; private set; }
        public bool IsPaused { get; private set; }
        public float MatchElapsedSeconds { get; private set; }
        public int MatchCoinsEarned { get; private set; }
        public bool HasRevivedThisMatch { get; private set; }

        private float _botSpawnTimer;
        private Vector2 _virtualJoystickOrigin;
        private bool _pointerDragging;

        // Hook points for Phase 2 subsystems (Bots, Powers, Mechanics, Modes)
        public event Action<float> OnGameplayTick;
        public event Action<PlayerBase> OnPlayerSpawnedEvent;
        public event Action<bool> OnMatchFinishedEvent;

        public void Initialize(ZoneManager zoneManager, PlaygroundView playgroundView, Camera cam)
        {
            ZoneManager = zoneManager;
            PlaygroundView = playgroundView;
            GameplayCamera = cam != null ? cam : Camera.main;

            CollisionWorld = new CollisionWorld();
            CollisionWorld.TrailEnter += HandleTrailEnter;
            CollisionWorld.HeadToHeadCollision += HandleHeadToHeadCollision;

            ZoneManager.Initialize(CollisionWorld, ArenaRadius);
        }

        public void GameStart(GameMode mode, ArenaSubMode subMode, int localPlayerPowerIndex = 0, float customArenaRadius = -1f, System.Collections.Generic.IReadOnlyList<Vector2> customArenaPolygon = null)
        {
            CurrentMode = mode;
            CurrentSubMode = subMode;
            if (customArenaRadius > 10f) ArenaRadius = customArenaRadius;

            IsPaused = false;
            IsInGame = true;
            MatchElapsedSeconds = 0f;
            MatchCoinsEarned = 0;
            HasRevivedThisMatch = false;
            MechanicCameraZoom = 1f;
            VisionPowerZoom = 1f;

            ZoneManager.ResetAll();
            ZoneManager.Initialize(CollisionWorld, ArenaRadius, customArenaPolygon);

            Color battleMapBg = subMode == ArenaSubMode.WorldMap
                ? new Color(0.95f, 0.97f, 1.0f)
                : new Color(0.97f, 0.97f, 0.99f);
            Color borderColor = mode == GameMode.TeamBattle
                ? new Color(0.22f, 0.25f, 0.34f)
                : new Color(0.90f, 0.24f, 0.30f);

            PlaygroundView.BuildArena(ArenaRadius, battleMapBg, borderColor, customArenaPolygon);

            // Spawn Local Player safely inside the arena (works for both Circular and Country/Organic Polygon arenas)
            Vector2 localSpawn = Vector2.zero;
            if (customArenaPolygon != null && customArenaPolygon.Count >= 3 && CollisionWorld.Border.IsOutside(localSpawn, ZoneManager.DefaultSpawnZoneRadius))
                localSpawn = ZoneManager.GetAvailablePosition(2f);

            int localTeam = mode == GameMode.TeamBattle ? 0 : -1;
            Color localColor = localTeam >= 0 ? PlayerSkinPalette.GetTeamColor(localTeam) : PlayerSkinPalette.GetPrimary(0);
            var localModel = new SpawnPlayerModel
            {
                PlayerId = 1,
                DisplayName = "YOU",
                IsLocalPlayer = true,
                TeamId = localTeam,
                PrimaryColor = localColor,
                ShadowColor = PlayerSkinPalette.GetShadow(localColor),
                TrailColor = PlayerSkinPalette.GetTrail(localColor),
                SpawnPosition = localSpawn,
                SpawnZoneRadius = ZoneManager.DefaultSpawnZoneRadius,
                CharacterPowerIndex = localPlayerPowerIndex
            };

            var localPlayer = ZoneManager.SpawnPlayer(localModel);
            SubscribeLocalPlayerListeners(localPlayer);
            OnPlayerSpawnedEvent?.Invoke(localPlayer);

            // Spawn initial bots
            for (int i = 0; i < TargetBotCount; i++)
            {
                int botTeam = mode == GameMode.TeamBattle ? (i % 2 == 0 ? 1 : 0) : -1;
                var bot = ZoneManager.AutoSpawnBot(botTeam);
                OnPlayerSpawnedEvent?.Invoke(bot);
            }
        }

        private void SubscribeLocalPlayerListeners(PlayerBase localPlayer)
        {
            if (localPlayer == null) return;
            localPlayer.Died += OnLocalPlayerDied;
            localPlayer.LoopClosed += OnLocalPlayerLoopClosed;
        }

        private void Update()
        {
            if (!IsInGame || IsPaused) return;

            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            MatchElapsedSeconds += dt;

            // 1. Read local player input (Mouse/Touch drag + WASD/Arrows)
            PollLocalPlayerInput();

            // 2. Notify Phase 2 subsystems (Bot AI, Character Powers, Map Mechanics, Mode rules)
            OnGameplayTick?.Invoke(dt);

            // 3. Advance all alive players
            var players = ZoneManager.Players;
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] != null && players[i].IsAlive)
                    players[i].TickMovement(dt);
            }

            // 4. Step CollisionWorld (trails, head-to-head, triggers)
            CollisionWorld.Tick(dt);

            // 5. Maintain bot population in Classic / PaintBrawl
            if (CurrentMode != GameMode.TeamBattle)
            {
                _botSpawnTimer += dt;
                if (_botSpawnTimer >= BotRespawnInterval && ZoneManager.AliveBotCount() < TargetBotCount)
                {
                    _botSpawnTimer = 0f;
                    var bot = ZoneManager.AutoSpawnBot(-1);
                    OnPlayerSpawnedEvent?.Invoke(bot);
                }
            }

            // 6. Camera adaptation
            AdaptCamera(dt);
        }

        private void PollLocalPlayerInput()
        {
            var local = ZoneManager.MainPlayer;
            if (local == null || !local.IsAlive) return;

            // Keyboard WASD / Arrow steering
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            if (h * h + v * v > 0.04f)
            {
                local.DesiredHeading = new Vector2(h, v).normalized;
                return;
            }

            // Touch / Mouse virtual joystick steering (exact BattleMaps mobile feel)
            if (Input.GetMouseButtonDown(0))
            {
                _pointerDragging = true;
                _virtualJoystickOrigin = Input.mousePosition;
            }
            else if (Input.GetMouseButtonUp(0))
            {
                _pointerDragging = false;
            }

            if (_pointerDragging)
            {
                Vector2 delta = (Vector2)Input.mousePosition - _virtualJoystickOrigin;
                if (delta.sqrMagnitude > 144f) // 12px deadzone
                {
                    local.DesiredHeading = delta.normalized;
                    // Slowly recenter origin so quick reversal feels crisp
                    if (delta.magnitude > 90f)
                        _virtualJoystickOrigin = (Vector2)Input.mousePosition - delta.normalized * 90f;
                }
            }
        }

        private void HandleTrailEnter(TrailEnterHit hit)
        {
            var hitter = ZoneManager.GetPlayerByID(hit.HitterHeadId);
            var victim = ZoneManager.GetPlayerByID(hit.TrailOwnerId);
            if (hitter == null || victim == null || !hitter.IsAlive || !victim.IsAlive) return;

            // Teammates don't cut each other's trails in Team Battle
            if (hit.HitterHeadId != hit.TrailOwnerId && ZoneManager.IsSameTeam(hit.HitterHeadId, hit.TrailOwnerId))
                return;

            // RockTail power protects owner's trail from being cut
            if (victim.HasRockTail && hit.HitterHeadId != hit.TrailOwnerId)
                return;

            if (hit.HitterHeadId == hit.TrailOwnerId)
            {
                victim.Die(victim.PlayerId, "SelfTrail");
            }
            else
            {
                hitter.RegisterKill();
                if (hitter.IsLocalPlayer)
                {
                    MatchCoinsEarned += 25;
                    OnLocalPlayerKills(victim);
                }
                victim.Die(hitter.PlayerId, "TrailCut");
            }
        }

        private void HandleHeadToHeadCollision(int idA, int idB, Vector2 point)
        {
            var a = ZoneManager.GetPlayerByID(idA);
            var b = ZoneManager.GetPlayerByID(idB);
            if (a == null || b == null || !a.IsAlive || !b.IsAlive) return;
            if (ZoneManager.IsSameTeam(idA, idB)) return;

            bool aSafe = a.CurrentZoneStatus == ZoneStatus.InOwnZone || a.IsInvincible || a.IsShielded;
            bool bSafe = b.CurrentZoneStatus == ZoneStatus.InOwnZone || b.IsInvincible || b.IsShielded;

            if (aSafe && !bSafe) { a.RegisterKill(); b.Die(a.PlayerId, "HeadCollision"); }
            else if (bSafe && !aSafe) { b.RegisterKill(); a.Die(b.PlayerId, "HeadCollision"); }
            else if (!aSafe && !bSafe)
            {
                // Player with larger territory survives head-on clash (tie = both die)
                if (a.OwnedZone.Area > b.OwnedZone.Area * 1.05f) { a.RegisterKill(); b.Die(a.PlayerId, "HeadCollision"); }
                else if (b.OwnedZone.Area > a.OwnedZone.Area * 1.05f) { b.RegisterKill(); a.Die(b.PlayerId, "HeadCollision"); }
                else { a.Die(b.PlayerId, "HeadCollision"); b.Die(a.PlayerId, "HeadCollision"); }
            }
        }

        private void OnLocalPlayerLoopClosed(PlayerBase player, float capturedArea)
        {
            MatchCoinsEarned += Mathf.RoundToInt(capturedArea * 0.5f);
            if (player.GetCapturedPercent01() >= WinThresholdPercent01)
            {
                OnLocalPlayerWon();
            }
        }

        private void OnLocalPlayerKills(PlayerBase victim)
        {
            EventBus.Shared.Publish(new FlyingTextSignal(
                $"KO {victim.DisplayName}! +25",
                victim.transform.position + Vector3.up * 1.5f,
                new Color(1f, 0.85f, 0.15f),
                1.3f));
        }

        private void OnLocalPlayerDied(PlayerBase player)
        {
            if (!IsInGame) return;
            FinishGame(localPlayerWon: false);
        }

        public void OnLocalPlayerWon()
        {
            if (!IsInGame) return;
            MatchCoinsEarned += 250;
            FinishGame(localPlayerWon: true);
        }

        public void FinishGame(bool localPlayerWon, int winningTeamId = -1)
        {
            IsInGame = false;
            var local = ZoneManager.MainPlayer;
            float pct = local != null ? local.GetCapturedPercent01() : 0f;
            int kills = local != null ? local.Kills : 0;

            EventBus.Shared.Publish(new MatchEndedSignal(localPlayerWon, pct, kills, winningTeamId));
            OnMatchFinishedEvent?.Invoke(localPlayerWon);
        }

        public void RevivePlayer()
        {
            if (HasRevivedThisMatch) return;
            HasRevivedThisMatch = true;
            IsInGame = true;

            int localTeam = CurrentMode == GameMode.TeamBattle ? 0 : -1;
            Color localColor = localTeam >= 0 ? PlayerSkinPalette.GetTeamColor(localTeam) : PlayerSkinPalette.GetPrimary(0);
            var model = new SpawnPlayerModel
            {
                PlayerId = 1,
                DisplayName = "YOU",
                IsLocalPlayer = true,
                TeamId = localTeam,
                PrimaryColor = localColor,
                ShadowColor = PlayerSkinPalette.GetShadow(localColor),
                TrailColor = PlayerSkinPalette.GetTrail(localColor),
                SpawnPosition = ZoneManager.GetAvailablePosition(),
                SpawnZoneRadius = ZoneManager.DefaultSpawnZoneRadius * 1.2f
            };
            var revived = ZoneManager.SpawnPlayer(model);
            revived.SetupInvincibilityMode(3.0f);
            SubscribeLocalPlayerListeners(revived);
            OnPlayerSpawnedEvent?.Invoke(revived);
        }

        public void PauseGame() => IsPaused = true;
        public void ResumeGame() => IsPaused = false;
        public void SetMechanicCameraZoom(float zoom) => MechanicCameraZoom = Mathf.Clamp(zoom, 0.7f, 1.8f);
        public void EnhanceVision(float zoom) => VisionPowerZoom = Mathf.Clamp(zoom, 0.8f, 1.75f);

        public void AdaptCamera(float dt)
        {
            if (GameplayCamera == null || ZoneManager.MainPlayer == null) return;
            var local = ZoneManager.MainPlayer;
            float sizeGrowth = 1f + Mathf.Clamp(local.GetCapturedPercent01() * 1.4f, 0f, 0.45f);
            float totalZoom = CameraSizeMultiplier * MechanicCameraZoom * VisionPowerZoom * sizeGrowth;

            Vector3 targetPos = local.transform.position + CameraOffset * totalZoom;
            GameplayCamera.transform.position = Vector3.Lerp(GameplayCamera.transform.position, targetPos, dt * CameraFollowSmooth);
            GameplayCamera.transform.LookAt(local.transform.position + Vector3.forward * 1.2f);
        }
    }
}
