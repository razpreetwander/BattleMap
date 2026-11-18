using System;
using UnityEngine;

namespace BattleMaps.Bots.BotProfiles
{
    /// <summary>
    /// Exact enum extracted from BattleMaps.Bots.BotProfiles.AIControllerState (IL2CPP index 5419).
    /// </summary>
    public enum AIControllerState
    {
        Looping = 0,
        AvoidingTrail = 1,
        HuntingTarget = 2,
        Probing = 3,
        MoveInsideOwnZone = 4,
        FollowingWaypoints = 5
    }

    /// <summary>
    /// Exact enum extracted from BattleMaps.Bots.BotProfiles.AIControllerHuntingType (IL2CPP index 5418).
    /// </summary>
    public enum AIControllerHuntingType
    {
        None = 0,
        NearestEnemyTrail = 1,
        LocalPlayer = 2,
        KingTarget = 3
    }

    /// <summary>
    /// Exact enum extracted from BattleMaps.Bots.BotProfiles.BotCategory (IL2CPP index 5427).
    /// </summary>
    public enum BotCategory
    {
        Default,
        Passive,
        AggressiveTowardsPlayer,
        AggressiveTowardsBots,
        AggressiveTowardsAll,
        Suicidal,
        Defensive,
        Strategic,
        Adventurer,
        Passive_Big_Loops,
        BT_AggL_PerL_LoopL, BT_AggL_PerL_LoopM, BT_AggL_PerL_LoopH,
        BT_AggL_PerM_LoopL, BT_AggL_PerM_LoopM, BT_AggL_PerM_LoopH,
        BT_AggL_PerH_LoopL, BT_AggL_PerH_LoopM, BT_AggL_PerH_LoopH,
        BT_AggM_PerL_LoopL, BT_AggM_PerL_LoopM, BT_AggM_PerL_LoopH,
        BT_AggM_PerM_LoopL, BT_AggM_PerM_LoopM, BT_AggM_PerM_LoopH,
        BT_AggM_PerH_LoopL, BT_AggM_PerH_LoopM, BT_AggM_PerH_LoopH,
        BT_AggH_PerL_LoopL, BT_AggH_PerL_LoopM, BT_AggH_PerL_LoopH,
        BT_AggH_PerM_LoopL, BT_AggH_PerM_LoopM, BT_AggH_PerM_LoopH,
        BT_AggH_PerH_LoopL, BT_AggH_PerH_LoopM, BT_AggH_PerH_LoopH
    }

    /// <summary>
    /// Exact fields extracted from BattleMaps.Bots.BotProfiles.AggressionProfileModel (IL2CPP index 5366).
    /// </summary>
    [Serializable]
    public struct AggressionProfileModel
    {
        public bool AlwaysAvoidPlayer;
        public float CanWinAttackRaceDecisionChance;
        public float AttackEnemyDecisionChance;
        public bool EscapeToFirstValidOption;
        public float PumpTowerEscapeSafeMargin;
        public float OwnZoneEscapeSafeMargin;
        public float AttackEscapeSafeMargin;
    }

    /// <summary>
    /// Exact fields extracted from BattleMaps.Bots.BotProfiles.PerceptionProfileModel (IL2CPP index 5373).
    /// </summary>
    [Serializable]
    public struct PerceptionProfileModel
    {
        public float ObstaclePenaltyMultiplier;
        public float EnemyCloseDetectionChance;
        public float EnemyDetectionRange;
        public float LongTrailDetectionChance;
        public float LongTrailThresholdPrc;
        public float PumpTowerDetectionChance;
        public float PumpTowerDetectionRange;
    }

    /// <summary>
    /// Exact fields extracted from BattleMaps.Bots.BotProfiles.LoopingProfileModel (IL2CPP index 5428).
    /// </summary>
    [Serializable]
    public struct LoopingProfileModel
    {
        public float LoopDistance;
        public int LoopTurns;
        public float MaxTurnAngle;
        public float RelaxExpansionAtPercentage;
    }

    /// <summary>
    /// Combined bot behavior profile matching BattleMaps.Bots.BotProfiles.BotProfileModel (index 5426).
    /// </summary>
    [Serializable]
    public class BotProfileModel
    {
        public BotCategory Category;
        public AggressionProfileModel Aggression;
        public PerceptionProfileModel Perception;
        public LoopingProfileModel Looping;

        public static BotProfileModel CreateForCategory(BotCategory category, float difficulty01 = 0.5f)
        {
            float d = Mathf.Clamp01(difficulty01);
            var profile = new BotProfileModel
            {
                Category = category,
                Aggression = new AggressionProfileModel
                {
                    AlwaysAvoidPlayer = category == BotCategory.Passive || category == BotCategory.Passive_Big_Loops,
                    CanWinAttackRaceDecisionChance = Mathf.Lerp(0.25f, 0.85f, d),
                    AttackEnemyDecisionChance = Mathf.Lerp(0.15f, 0.72f, d),
                    EscapeToFirstValidOption = d < 0.4f,
                    PumpTowerEscapeSafeMargin = 2.5f,
                    OwnZoneEscapeSafeMargin = Mathf.Lerp(1.8f, 3.8f, d),
                    AttackEscapeSafeMargin = 2.2f
                },
                Perception = new PerceptionProfileModel
                {
                    ObstaclePenaltyMultiplier = 1.5f,
                    EnemyCloseDetectionChance = Mathf.Lerp(0.45f, 0.95f, d),
                    EnemyDetectionRange = Mathf.Lerp(6f, 14f, d),
                    LongTrailDetectionChance = Mathf.Lerp(0.35f, 0.90f, d),
                    LongTrailThresholdPrc = Mathf.Lerp(0.35f, 0.18f, d),
                    PumpTowerDetectionChance = 0.65f,
                    PumpTowerDetectionRange = 14f
                },
                Looping = new LoopingProfileModel
                {
                    LoopDistance = Mathf.Lerp(3.2f, 6.8f, d),
                    LoopTurns = 3,
                    MaxTurnAngle = 115f,
                    RelaxExpansionAtPercentage = 0.25f
                }
            };

            string name = category.ToString();
            if (name.Contains("AggH") || category == BotCategory.AggressiveTowardsAll || category == BotCategory.AggressiveTowardsPlayer)
            {
                profile.Aggression.AttackEnemyDecisionChance = Mathf.Max(profile.Aggression.AttackEnemyDecisionChance, 0.80f);
                profile.Aggression.AlwaysAvoidPlayer = false;
            }
            else if (name.Contains("AggL") || category == BotCategory.Defensive)
            {
                profile.Aggression.AttackEnemyDecisionChance = 0.18f;
                profile.Aggression.OwnZoneEscapeSafeMargin = 4.2f;
            }

            if (name.Contains("LoopH") || category == BotCategory.Passive_Big_Loops || category == BotCategory.Adventurer)
            {
                profile.Looping.LoopDistance = 7.8f;
                profile.Looping.LoopTurns = 4;
            }
            else if (name.Contains("LoopL") || category == BotCategory.Defensive)
            {
                profile.Looping.LoopDistance = 2.8f;
                profile.Looping.LoopTurns = 2;
            }

            if (name.Contains("PerH") || category == BotCategory.Strategic)
            {
                profile.Perception.EnemyDetectionRange = 16f;
                profile.Perception.EnemyCloseDetectionChance = 0.95f;
            }
            return profile;
        }
    }
}
