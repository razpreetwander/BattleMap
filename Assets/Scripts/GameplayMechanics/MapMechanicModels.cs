using System;
using UnityEngine;

namespace BattleMaps.GameplayMechanics
{
    /// <summary>
    /// Exact enum extracted from BattleMaps.GameplayMechanics.GameplayMechanicsPadKind (IL2CPP index 2744).
    /// </summary>
    public enum GameplayMechanicsPadKind
    {
        SpeedBoost = 0,
        JumpingRamp = 1,
        Tunnel = 2
    }

    /// <summary>
    /// Reconstructed with exact observed fields from JumpingRampDataModel (IL2CPP index 2707).
    /// </summary>
    [Serializable]
    public class JumpingRampDataModel
    {
        public float JumpHeight = 4.2f;
        public float SpeedBoostMultiplier = 1.45f;
        public float HangDurationSeconds = 1.15f;
        public bool LaunchZoneDraw = true;
        public float LaunchZoneRadius = 1.8f;
        public bool LandZoneDraw = true;
        public float LandZoneRadius = 3.0f;
        public float LandKillRadius = 2.6f;
        public bool JumpZoomEnabled = true;
        public float JumpZoomMultiplier = 1.24f;
        public bool ShowTrail = true;
    }

    /// <summary>
    /// Reconstructed with exact observed fields from GroundSpeedBoostDataModel (IL2CPP index 2765).
    /// </summary>
    [Serializable]
    public class GroundSpeedBoostDataModel
    {
        public float StartSmoothingSeconds = 0.15f;
        public float EndSmoothingSeconds = 0.45f;
        public float ComboWindowSeconds = 4.0f;
        public float DurationSeconds = 2.2f;
        public float[] SpeedByTier = { 1.45f, 1.72f, 2.0f };
    }

    /// <summary>
    /// Reconstructed with exact observed fields from TunnelDataModel (IL2CPP index 2777).
    /// </summary>
    [Serializable]
    public class TunnelDataModel
    {
        public float Depth = -1.6f;
        public float DurationSeconds = 1.85f;
        public float SpeedMultiplier = 1.35f;
        public bool EnterZoneDraw = true;
        public float EnterZoneRadius = 1.6f;
        public bool ExitZoneDraw = true;
        public float ExitZoneRadius = 3.2f;
        public float EmergeKillRadius = 2.8f;
        public bool CameraZoomEnabled = true;
        public float CameraZoomMultiplier = 1.18f;
        public bool ShowTrail = true;
    }
}
