using System;
using System.Collections.Generic;
using UnityEngine;

namespace BattleMaps.CharacterPowers.Powers
{
    /// <summary>
    /// Exact 45-entry enum extracted directly from BattleMaps.CharacterPowers.Powers.PowerType (IL2CPP index 4697).
    /// </summary>
    public enum PowerType
    {
        None = 0,
        CoinDrop,
        HiddenTreasure,
        ObjectMagnet,
        ElectricTerrain,
        ElectricField,
        StaticElectricField,
        Sprinter,
        PaintMines,
        InstantDash,
        Explorer,
        ShockField,
        PaintCircle,
        ExpandingPaint,
        PaintCone,
        PaintHose,
        RockTail,
        FasterInPaintedTerritory,
        FasterInNeutralTerritory,
        TerritoryViscosity,
        BetterVision,
        ShockWave,
        ShieldedArea,
        TowerRadar,
        MiniMapRadar,
        FireTail,
        FlameThrower,
        PaintThrow,
        PaintBowling,
        TrailPulse,
        MuddyTerrain,
        Disruptor,
        TeslaTower,
        LavaSplatter,
        FreezingField,
        FreezingStaticField,
        FrozenWave,
        Firecracker,
        Bomb,
        FireBomb,
        PaintEraserBomb,
        FireMeteoriteRain,
        FireWave,
        Inferno
    }

    public enum CharacterClassType
    {
        Balanced = 0,
        Speedster = 1,
        Painter = 2,
        Tank = 3,
        Elemental = 4,
        Explosive = 5
    }

    public enum CharacterRarityType
    {
        Common = 0,
        Rare = 1,
        Epic = 2,
        Legendary = 3
    }

    /// <summary>
    /// Reconstructed from BattleMaps.CharacterPowers.Characters.CharacterConfig (IL2CPP index 5031).
    /// </summary>
    [Serializable]
    public class CharacterConfig
    {
        public string CharacterId;
        public string DisplayName;
        public string Description;
        public PowerType[] PowerTypes;
        public CharacterClassType Class;
        public CharacterRarityType Rarity;
        public Color PrimaryColor;
        public float ChargeRequirement = 100f;
        public float ActiveDuration = 5.0f;
        public int UnlockCostCoins = 0;
    }

    /// <summary>
    /// Persistent runtime state for an unlocked/upgradable character.
    /// </summary>
    [Serializable]
    public class CharacterProgressState
    {
        public string CharacterId;
        public bool Unlocked;
        public int Level = 1;
        public int Cards;
        public int EquippedOutfitIndex;
    }

    [Serializable]
    public class PowerSettingsBase
    {
        public PowerType Type;
        public float Duration = 5f;
        public float Radius = 4.2f;
        public float SpeedMultiplier = 1.45f;
        public float PowerStrength = 1f;
        public Color EffectColor = Color.cyan;
    }
}
