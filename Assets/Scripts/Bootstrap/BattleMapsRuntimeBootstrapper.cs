using GameOps.UIFramework;
using BattleMaps.Audio;
using BattleMaps.Bots.BotProfiles;
using BattleMaps.CharacterPowers.Characters;
using BattleMaps.DifficultyTuningSystem.Controller;
using BattleMaps.Features.Multiplayer;
using BattleMaps.Gameplay;
using BattleMaps.GameplayMechanics;
using BattleMaps.Modes;
using BattleMaps.SaveStates;
using BattleMaps.Signals;
using BattleMaps.UI.Screens;
using BattleMaps.VFX;
using BattleMaps.World.Controller;
using UnityEngine;

namespace BattleMaps.Bootstrap
{
    /// <summary>
    /// Turnkey runtime bootstrapper that wires EVERY reconstructed BattleMaps system
    /// (Core Engine, Clipper2, CollisionWorld, Custom Country & Organic Polygon Maps,
    /// AI Bots, 44 Character Powers, 10 3D Outfits, Particle VFX, Map Mechanics,
    /// EnemyTrain, PumpTowers, TeamBattle Revive Orbs, WebSocket Multiplayer, SaveStates,
    /// Procedural SFX+BGM, Localization, and all 15 UI screens).
    /// </summary>
    public class BattleMapsRuntimeBootstrapper : MonoBehaviour
    {
        private static bool _bootstrapped;

        private GameManager _gameManager;
        private SaveStateController _saveController;
        private GameplayController _gameplayController;
        private ZoneManager _zoneManager;
        private PlaygroundView _playgroundView;
        private BehaviorTreeAIController _aiController;
        private DifficultyTuningSystemController _dtsController;
        private CharactersManager _charactersManager;
        private GameplayMechanicsController _mechanicsController;
        private EnemyTrainAndPumpTowerController _hazardsController;
        private MatchModeRulesController _modeRulesController;
        private TeamReviveCollectibleSystem _teamReviveSystem;
        private MetaProgressionController _metaController;
        private GameplayVfxController _vfxController;
        private BattleMapsMultiplayerClient _multiplayerClient;
        private UIScreenRouter _uiRouter;

        private GameMode _lastMode = GameMode.Default;
        private ArenaSubMode _lastSubMode = ArenaSubMode.Classic;
        private float _lastArenaRadius = 32f;
        private System.Collections.Generic.List<Vector2> _lastArenaPolygon;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrapOnPlay()
        {
            if (_bootstrapped || FindFirstObjectByType<BattleMapsRuntimeBootstrapper>() != null)
                return;
            var go = new GameObject("BattleMaps_RuntimeRoot");
            go.AddComponent<BattleMapsRuntimeBootstrapper>();
        }

        private void Awake()
        {
            if (_bootstrapped)
            {
                Destroy(gameObject);
                return;
            }
            _bootstrapped = true;
            DontDestroyOnLoad(gameObject);

            SetupSceneCameraAndLight();
            CreateAllSystems();
            RegisterAllScreens();
            WireCallbacks();

            _uiRouter.ShowScreen(UIScreenId.Home);
        }

        private void SetupSceneCameraAndLight()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.11f, 0.14f, 0.22f);
            cam.fieldOfView = 45f;
            cam.transform.position = new Vector3(0f, 24f, -12f);
            cam.transform.LookAt(Vector3.zero);

            if (FindFirstObjectByType<Light>() == null)
            {
                var lightGo = new GameObject("Directional Light");
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.15f;
                light.color = new Color(1f, 0.98f, 0.94f);
                lightGo.transform.rotation = Quaternion.Euler(52f, -30f, 0f);
            }
        }

        private void CreateAllSystems()
        {
            _saveController = gameObject.AddComponent<SaveStateController>();
            _gameManager = gameObject.AddComponent<GameManager>();
            _zoneManager = gameObject.AddComponent<ZoneManager>();
            _playgroundView = gameObject.AddComponent<PlaygroundView>();
            _gameplayController = gameObject.AddComponent<GameplayController>();
            _aiController = gameObject.AddComponent<BehaviorTreeAIController>();
            _dtsController = gameObject.AddComponent<DifficultyTuningSystemController>();
            _charactersManager = gameObject.AddComponent<CharactersManager>();
            _mechanicsController = gameObject.AddComponent<GameplayMechanicsController>();
            _hazardsController = gameObject.AddComponent<EnemyTrainAndPumpTowerController>();
            _modeRulesController = gameObject.AddComponent<MatchModeRulesController>();
            _teamReviveSystem = gameObject.AddComponent<TeamReviveCollectibleSystem>();
            _metaController = gameObject.AddComponent<MetaProgressionController>();
            _vfxController = gameObject.AddComponent<GameplayVfxController>();
            _multiplayerClient = gameObject.AddComponent<BattleMapsMultiplayerClient>();
            gameObject.AddComponent<CoreGameplaySfxController>();
            _uiRouter = gameObject.AddComponent<UIScreenRouter>();

            _gameplayController.Initialize(_zoneManager, _playgroundView, Camera.main);
            _gameManager.BindGameplayController(_gameplayController);
            _aiController.Initialize(_zoneManager);
            _dtsController.Initialize(_aiController, _zoneManager);
            _charactersManager.Initialize(_gameplayController);
            _charactersManager.SelectCharacter(_saveController.Data.SelectedCharacterIndex);
            _mechanicsController.Initialize(_gameplayController);
            _hazardsController.Initialize(_gameplayController);
            _modeRulesController.Initialize(_gameplayController);
            _teamReviveSystem.Initialize(_gameplayController);
            _vfxController.Initialize(_gameplayController);
            _multiplayerClient.Initialize(_gameplayController);
        }

        private void RegisterAllScreens()
        {
            var home = _uiRouter.RegisterScreen<HomeScreen>();
            home.Bind(_charactersManager, (mode, sub) => LaunchMatch(mode, sub, 32f, null));

            var hud = _uiRouter.RegisterScreen<GameplayHudScreen>();
            hud.BindSystems(_gameplayController, _charactersManager, _modeRulesController);

            var revive = _uiRouter.RegisterScreen<RevivePopupScreen>();
            revive.Bind(_gameplayController);

            _uiRouter.RegisterScreen<LevelEndScreen>();

            var pause = _uiRouter.RegisterScreen<PausePopupScreen>();
            pause.Bind(_gameplayController);

            var chars = _uiRouter.RegisterScreen<CharacterSelectionScreen>();
            chars.Bind(_charactersManager, _metaController);

            var shop = _uiRouter.RegisterScreen<ShopAndChestsScreen>();
            shop.Bind(_metaController);

            var world = _uiRouter.RegisterScreen<WorldMapScreen>();
            world.Bind(_metaController, countryId =>
            {
                var countryPoly = WorldCountryGeometryResolver.BuildCountryArenaPolygon(countryId, 34f);
                LaunchMatch(GameMode.Default, ArenaSubMode.WorldMap, 34f, countryPoly);
            });

            var chapters = _uiRouter.RegisterScreen<ChaptersScreen>();
            chapters.Bind(_metaController, chapterIdx =>
            {
                float r = 30f + chapterIdx * 2.0f;
                var chapterPoly = OrganicLevelShapeModel.BuildChapterArenaPolygon(chapterIdx, r);
                LaunchMatch(GameMode.Default, ArenaSubMode.Chapters, r, chapterPoly);
            });

            var party = _uiRouter.RegisterScreen<PartyLobbyScreen>();
            party.Bind((mode, sub) => LaunchMatch(mode, sub, 34f, null), _multiplayerClient);

            _uiRouter.RegisterScreen<MissionsScreen>();
            _uiRouter.RegisterScreen<XPRoadScreen>();
            _uiRouter.RegisterScreen<BonusGameScreen>();
            _uiRouter.RegisterScreen<EventsScreen>();
            _uiRouter.RegisterScreen<SettingsPopupScreen>();
        }

        private void WireCallbacks()
        {
            _gameplayController.OnPlayerSpawnedEvent += player =>
            {
                if (player.IsLocalPlayer)
                    _charactersManager.BindPlayerPowers(player, _charactersManager.SelectedCharacterIndex);
                else
                    _aiController.RegisterBot(player);
            };

            _gameplayController.OnGameplayTick += dt =>
            {
                _dtsController.TickDynamicTuning();
                _aiController.TickAllBots(dt);
                _charactersManager.TickPowers(dt);
                _mechanicsController.TickMechanics(dt);
                _hazardsController.Tick(dt);
                _modeRulesController.TickModeRules(dt);
                _teamReviveSystem.Tick(dt);
                _vfxController.TickEdgeSparks();
                _multiplayerClient.TickNetwork(dt);
            };

            EventBus.Shared.Subscribe<MatchEndedSignal>(OnMatchEnded);
        }

        public void LaunchMatch(GameMode mode, ArenaSubMode subMode, float arenaRadius = 32f, System.Collections.Generic.List<Vector2> customPolygon = null)
        {
            _lastMode = mode;
            _lastSubMode = subMode;
            _lastArenaRadius = arenaRadius;
            _lastArenaPolygon = customPolygon;

            _aiController.ClearAll();
            _charactersManager.ResetMatchPowers();
            _mechanicsController.ClearAll();
            _hazardsController.ClearAll();
            _teamReviveSystem.ClearAll();

            _gameManager.StartMatch(mode, subMode, _charactersManager.SelectedCharacterIndex, arenaRadius, customPolygon);
            _modeRulesController.OnMatchStarted(mode, subMode);

            bool spawnTrain = subMode == ArenaSubMode.Chapters || subMode == ArenaSubMode.Classic;
            _mechanicsController.SpawnArenaMechanics(arenaRadius, boostCount: 4, rampCount: 3, tunnelCount: 2);
            _hazardsController.SpawnHazardsAndTowers(arenaRadius, enableTrain: spawnTrain, pumpTowerCount: 2);

            _uiRouter.ShowScreen(UIScreenId.GameplayHud);
        }

        private void OnMatchEnded(MatchEndedSignal sig)
        {
            var local = _gameplayController.ZoneManager.MainPlayer;
            int loops = local != null ? local.LoopsClosed : 0;
            int coins = _gameplayController.MatchCoinsEarned + (local != null ? local.CoinsCollected : 0);

            _dtsController.RecordMatchOutcome(sig.LocalPlayerWon, sig.FinalPercent01);
            _metaController.RecordMatchCompleted(_lastSubMode, sig.LocalPlayerWon, sig.FinalPercent01, sig.Kills, loops, coins);

            var endScreen = _uiRouter.GetScreen<LevelEndScreen>(UIScreenId.LevelEnd);
            endScreen?.Configure(sig.LocalPlayerWon, sig.FinalPercent01, sig.Kills, coins,
                () => LaunchMatch(_lastMode, _lastSubMode, _lastArenaRadius, _lastArenaPolygon));

            if (!sig.LocalPlayerWon && !_gameplayController.HasRevivedThisMatch && _uiRouter.CurrentScreenId == UIScreenId.GameplayHud)
            {
                _uiRouter.OpenPopup(UIScreenId.RevivePopup);
            }
            else
            {
                _uiRouter.ShowScreen(UIScreenId.LevelEnd);
            }
        }

        private void OnDestroy()
        {
            EventBus.Shared.Unsubscribe<MatchEndedSignal>(OnMatchEnded);
        }
    }
}
