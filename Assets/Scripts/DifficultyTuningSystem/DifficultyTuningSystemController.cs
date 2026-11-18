using BattleMaps.Bots.BotProfiles;
using BattleMaps.Gameplay;
using UnityEngine;

namespace BattleMaps.DifficultyTuningSystem.Controller
{
    /// <summary>
    /// Reconstructed from BattleMaps.DifficultyTuningSystem.Controller.DifficultyTuningSystemController (index 2885, 89 methods)
    /// & DtsProgressionContext (index 2889, 91 methods).
    /// Dynamically scales bot intelligence, aggression, and arena pressure based on player win/loss streak and captured territory.
    /// </summary>
    public class DifficultyTuningSystemController : MonoBehaviour
    {
        public float BaseDifficulty01 = 0.38f;
        public float CurrentDifficulty01 { get; private set; } = 0.38f;
        public int ConsecutiveWins { get; private set; }
        public int ConsecutiveLosses { get; private set; }

        private BehaviorTreeAIController _aiController;
        private ZoneManager _zoneManager;

        public void Initialize(BehaviorTreeAIController aiController, ZoneManager zoneManager)
        {
            _aiController = aiController;
            _zoneManager = zoneManager;
            RecalculateDifficulty();
        }

        public void RecordMatchOutcome(bool won, float finalPercent01)
        {
            if (won)
            {
                ConsecutiveWins++;
                ConsecutiveLosses = 0;
            }
            else
            {
                ConsecutiveLosses++;
                ConsecutiveWins = 0;
            }

            float delta = won ? 0.08f : (finalPercent01 < 0.15f ? -0.10f : -0.04f);
            BaseDifficulty01 = Mathf.Clamp(BaseDifficulty01 + delta, 0.15f, 0.92f);
            RecalculateDifficulty();
        }

        public void TickDynamicTuning()
        {
            if (_zoneManager == null || _zoneManager.MainPlayer == null) return;
            float playerPct = _zoneManager.MainPlayer.GetCapturedPercent01();
            // Gradually ramp challenge as player captures more of the map
            CurrentDifficulty01 = Mathf.Clamp01(BaseDifficulty01 + playerPct * 0.25f);
            _aiController?.SetDifficultyLevel(CurrentDifficulty01);
        }

        private void RecalculateDifficulty()
        {
            CurrentDifficulty01 = BaseDifficulty01;
            _aiController?.SetDifficultyLevel(CurrentDifficulty01);
        }
    }
}
