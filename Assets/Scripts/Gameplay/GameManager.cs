using System;
using Libraries.GameFlow.FSM;
using UnityEngine;

namespace BattleMaps.Gameplay
{
    /// <summary>
    /// Top-level coordinator reconstructed from BattleMaps.Gameplay.GameManager (IL2CPP index 5081, 48 methods).
    /// Owns GameFSM, mode transitions, frame-rate policy, and session counters.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GameFSM GameFlowController { get; private set; }
        public GameplayController GameplayController { get; private set; }

        public GameMode CurrentGameMode { get; private set; } = GameMode.Default;
        public ArenaSubMode CurrentSubMode { get; private set; } = ArenaSubMode.Classic;
        public int SessionGameCount { get; private set; }
        public int TotalGameCount { get; private set; }
        public bool IsInGame => GameplayController != null && GameplayController.IsInGame;
        public bool IsPaused => GameplayController != null && GameplayController.IsPaused;

        public event Action<GameMode, ArenaSubMode> GameModeChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            GameFlowController = new GameFSM();
            UpdateTargetFrameRate(60);
        }

        public void BindGameplayController(GameplayController controller)
        {
            GameplayController = controller;
        }

        public void UpdateTargetFrameRate(int fps)
        {
            Application.targetFrameRate = Mathf.Clamp(fps, 30, 120);
        }

        public void SetGameMode(GameMode mode, ArenaSubMode subMode)
        {
            CurrentGameMode = mode;
            CurrentSubMode = subMode;
            GameModeChanged?.Invoke(mode, subMode);
        }

        public void StartMatch(GameMode mode, ArenaSubMode subMode, int selectedPowerIndex = 0, float arenaRadius = 32f, System.Collections.Generic.IReadOnlyList<Vector2> customArenaPolygon = null)
        {
            SetGameMode(mode, subMode);
            SessionGameCount++;
            TotalGameCount++;
            GameplayController?.GameStart(mode, subMode, selectedPowerIndex, arenaRadius, customArenaPolygon);
        }

        private void Update()
        {
            GameFlowController?.Tick(Time.deltaTime);
        }
    }
}
