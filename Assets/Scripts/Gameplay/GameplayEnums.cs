using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleMaps.Gameplay
{
    /// <summary>
    /// Exact enum extracted from BattleMaps.Gameplay.GameMode (IL2CPP index 5075).
    /// </summary>
    public enum GameMode
    {
        Default = 0,
        TeamBattle = 1,
        PartyMode = 2,
        None = 3
    }

    /// <summary>
    /// Sub-mode selector covering the Addressables bundles (World, Chapters, PaintBrawl, Classic).
    /// </summary>
    public enum ArenaSubMode
    {
        Classic = 0,
        WorldMap = 1,
        Chapters = 2,
        TeamBattle = 3,
        PaintBrawl = 4
    }

    /// <summary>
    /// Exact enum extracted from BattleMaps.Gameplay.ZoneStatus (IL2CPP index 5095).
    /// </summary>
    public enum ZoneStatus
    {
        InOwnZone = 0,
        InEnemyZone = 1,
        OutOfZone = 2
    }

    /// <summary>
    /// Reconstructed from BattleMaps.Gameplay.ZoneInfo (index 5089).
    /// </summary>
    [Serializable]
    public struct ZoneInfo
    {
        public int OwnerId;
        public int TeamId;
        public float Area;
        public float Percent01;
        public Vector2 Centroid;
        public Rect Bounds;
    }

    /// <summary>
    /// Reconstructed from BattleMaps.Gameplay.IZone (index 5097).
    /// </summary>
    public interface IZone
    {
        int OwnerId { get; }
        int TeamId { get; }
        float Area { get; }
        IReadOnlyList<Vector2> Points { get; }
        bool IsPointInside(Vector2 point);
    }

    /// <summary>
    /// Reconstructed from BattleMaps.Gameplay.IGameModeController (index 5096).
    /// </summary>
    public interface IGameModeController
    {
        GameMode Mode { get; }
        void InitializeMode(GameplayController controller);
        void StartMatch();
        void TickMode(float deltaTime);
        void EndMatch(bool localPlayerWon);
    }

    /// <summary>
    /// Reconstructed from BattleMaps.Gameplay.SpawnPlayerModel (index 5111).
    /// </summary>
    [Serializable]
    public struct SpawnPlayerModel
    {
        public int PlayerId;
        public string DisplayName;
        public bool IsLocalPlayer;
        public int TeamId;
        public Color PrimaryColor;
        public Color ShadowColor;
        public Color TrailColor;
        public Vector2 SpawnPosition;
        public float SpawnZoneRadius;
        public int CharacterPowerIndex;
        public string BotCategoryName;
    }

    /// <summary>
    /// Vibrant BattleMaps color palette generator for players, bots, and teams.
    /// </summary>
    public static class PlayerSkinPalette
    {
        private static readonly Color[] Colors =
        {
            new Color(0.14f, 0.78f, 0.45f), // Emerald Green (Default Local Player)
            new Color(0.95f, 0.26f, 0.34f), // Coral Red
            new Color(0.20f, 0.56f, 0.96f), // Sky Blue
            new Color(0.98f, 0.68f, 0.14f), // Amber Gold
            new Color(0.64f, 0.31f, 0.92f), // Vivid Purple
            new Color(0.08f, 0.78f, 0.82f), // Cyan Teal
            new Color(0.96f, 0.38f, 0.72f), // Hot Pink
            new Color(0.95f, 0.48f, 0.16f), // Tangerine
            new Color(0.48f, 0.84f, 0.18f), // Lime
            new Color(0.32f, 0.38f, 0.94f), // Indigo
            new Color(0.88f, 0.22f, 0.52f), // Magenta
            new Color(0.16f, 0.64f, 0.58f), // Jade
        };

        public static readonly string[] BotNames =
        {
            "Nova", "Blitz", "Vortex", "Kairo", "Zeph", "Milo", "Sora", "Riven",
            "Nyx", "Pixel", "Echo", "Dash", "Vex", "Luna", "Ares", "Koda",
            "Orion", "Tango", "Juno", "Spike"
        };

        public static Color GetPrimary(int index) => Colors[Mathf.Abs(index) % Colors.Length];
        public static Color GetShadow(Color primary) => Color.Lerp(primary, Color.black, 0.32f);
        public static Color GetTrail(Color primary) => new Color(primary.r, primary.g, primary.b, 0.62f);

        public static Color GetTeamColor(int teamId)
        {
            return teamId switch
            {
                0 => new Color(0.18f, 0.58f, 0.98f), // Blue Team
                1 => new Color(0.95f, 0.25f, 0.32f), // Red Team
                2 => new Color(0.16f, 0.80f, 0.44f), // Green Team
                _ => new Color(0.98f, 0.72f, 0.14f), // Yellow Team
            };
        }
    }
}
