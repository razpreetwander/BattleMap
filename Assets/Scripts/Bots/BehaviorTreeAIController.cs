using System.Collections.Generic;
using BattleMaps.Clipper;
using BattleMaps.Gameplay;
using UnityEngine;

namespace BattleMaps.Bots.BotProfiles
{
    /// <summary>
    /// Reconstructed from BattleMaps.Bots.BotProfiles.BehaviorTreeAIController (IL2CPP index 5414, 43 methods)
    /// & BattleMaps.Bots.Controller.DefaultBotsController (index 5363).
    /// Implements the observed BT leaf tasks:
    ///   FindNextExpandPoint, GoToOwnZone, GoToEnemyTrail, AvoidLocalPlayerTrail,
    ///   CalculateDefenseAgainstEnemyOptions, IsTrailExposed, HasEnemiesInRange.
    /// </summary>
    public class BehaviorTreeAIController : MonoBehaviour
    {
        private readonly Dictionary<int, BotAgentRuntime> _agents = new(24);
        private ZoneManager _zoneManager;
        private float _difficultyLevel01 = 0.45f;

        private sealed class BotAgentRuntime
        {
            public PlayerBase Player;
            public BotProfileModel Profile;
            public AIControllerState State = AIControllerState.MoveInsideOwnZone;
            public readonly List<Vector2> Waypoints = new(8);
            public int WaypointIndex;
            public float DecisionTimer;
            public float StateTimer;
            public int HuntTargetId = -1;
            public Vector2 TargetPoint;
        }

        public void Initialize(ZoneManager zoneManager)
        {
            _zoneManager = zoneManager;
            _agents.Clear();
        }

        public void SetDifficultyLevel(float difficulty01)
        {
            _difficultyLevel01 = Mathf.Clamp01(difficulty01);
        }

        public void RegisterBot(PlayerBase bot, BotCategory? forcedCategory = null)
        {
            if (bot == null || bot.IsLocalPlayer) return;
            BotCategory cat = forcedCategory ?? PickCategoryForDifficulty(_difficultyLevel01);
            var runtime = new BotAgentRuntime
            {
                Player = bot,
                Profile = BotProfileModel.CreateForCategory(cat, _difficultyLevel01),
                DecisionTimer = Random.Range(0.05f, 0.25f)
            };
            _agents[bot.PlayerId] = runtime;
        }

        public void UnregisterBot(int playerId) => _agents.Remove(playerId);
        public void ClearAll() => _agents.Clear();

        public void TickAllBots(float dt)
        {
            if (_zoneManager == null) return;

            var toRemove = new List<int>();
            foreach (var kv in _agents)
            {
                var agent = kv.Value;
                if (agent.Player == null || !agent.Player.IsAlive)
                {
                    toRemove.Add(kv.Key);
                    continue;
                }
                TickAgent(agent, dt);
            }

            for (int i = 0; i < toRemove.Count; i++)
                _agents.Remove(toRemove[i]);
        }

        private void TickAgent(BotAgentRuntime agent, float dt)
        {
            agent.DecisionTimer -= dt;
            agent.StateTimer += dt;

            if (agent.DecisionTimer <= 0f)
            {
                agent.DecisionTimer = Random.Range(0.14f, 0.28f);
                EvaluateBehaviorTree(agent);
            }

            SteerAgent(agent);
        }

        private void EvaluateBehaviorTree(BotAgentRuntime agent)
        {
            var bot = agent.Player;
            var profile = agent.Profile;
            Vector2 pos = bot.Position2D;
            float arenaMaxR = _zoneManager.ArenaRadius - 2.2f;

            // 1. Border danger override
            if (pos.magnitude > arenaMaxR)
            {
                agent.State = AIControllerState.MoveInsideOwnZone;
                agent.TargetPoint = GetClosestOwnZonePosition(bot);
                return;
            }

            // 2. Trail defense check (IsTrailExposed / CalculateDefenseAgainstEnemyOptions)
            if (bot.CurrentZoneStatus != ZoneStatus.InOwnZone)
            {
                float maxSafeTrail = profile.Looping.LoopDistance * (profile.Looping.LoopTurns + 1.2f);
                bool longTrail = bot.ActiveTrailLength > maxSafeTrail;
                bool enemyThreat = HasEnemyNearOwnTrail(bot, profile.Perception.EnemyDetectionRange * 0.75f);

                if (longTrail || (enemyThreat && Random.value < profile.Perception.EnemyCloseDetectionChance))
                {
                    agent.State = AIControllerState.MoveInsideOwnZone;
                    agent.TargetPoint = GetClosestOwnZonePosition(bot);
                    return;
                }
            }

            // 3. Opportunity attack check (GetEnemyClosestTrailPosition / GoToEnemyTrail)
            if (FindAttackableEnemyTrail(bot, profile, out int targetId, out Vector2 enemyTrailPos))
            {
                if (Random.value < profile.Aggression.AttackEnemyDecisionChance)
                {
                    agent.State = AIControllerState.HuntingTarget;
                    agent.HuntTargetId = targetId;
                    agent.TargetPoint = enemyTrailPos;
                    return;
                }
            }

            // 4. Continue active loop waypoints or start a fresh expansion loop
            if (agent.State == AIControllerState.Looping && agent.WaypointIndex < agent.Waypoints.Count)
                return;

            BuildExpansionLoopWaypoints(agent);
            agent.State = AIControllerState.Looping;
        }

        private void BuildExpansionLoopWaypoints(BotAgentRuntime agent)
        {
            var bot = agent.Player;
            var loopCfg = agent.Profile.Looping;
            agent.Waypoints.Clear();
            agent.WaypointIndex = 0;

            Vector2 center = bot.OwnedZone != null ? bot.OwnedZone.Centroid : bot.Position2D;
            Vector2 outward = (bot.Position2D - center);
            if (outward.sqrMagnitude < 0.25f)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                outward = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            }
            else
            {
                outward.Normalize();
            }

            // Bias slightly inward if close to arena border
            float safeR = _zoneManager.ArenaRadius - 3.5f;
            if ((bot.Position2D + outward * loopCfg.LoopDistance).magnitude > safeR)
                outward = (-bot.Position2D).normalized;

            float turnSign = Random.value > 0.5f ? 1f : -1f;
            Vector2 tangent = new Vector2(-outward.y * turnSign, outward.x * turnSign);

            float dist = loopCfg.LoopDistance * Random.Range(0.75f, 1.15f);
            Vector2 p1 = ClampToSafeArena(bot.Position2D + outward * dist, safeR);
            Vector2 p2 = ClampToSafeArena(p1 + tangent * (dist * 0.9f), safeR);
            Vector2 p3 = ClampToSafeArena(center + tangent * (dist * 0.45f), safeR);

            agent.Waypoints.Add(p1);
            agent.Waypoints.Add(p2);
            agent.Waypoints.Add(p3);
            agent.Waypoints.Add(center);
            agent.TargetPoint = p1;
        }

        private void SteerAgent(BotAgentRuntime agent)
        {
            var bot = agent.Player;
            Vector2 pos = bot.Position2D;

            if (agent.State == AIControllerState.Looping && agent.WaypointIndex < agent.Waypoints.Count)
            {
                agent.TargetPoint = agent.Waypoints[agent.WaypointIndex];
                if (Vector2.Distance(pos, agent.TargetPoint) < 1.35f)
                {
                    agent.WaypointIndex++;
                    if (agent.WaypointIndex >= agent.Waypoints.Count)
                        agent.TargetPoint = GetClosestOwnZonePosition(bot);
                    else
                        agent.TargetPoint = agent.Waypoints[agent.WaypointIndex];
                }
            }

            Vector2 desired = agent.TargetPoint - pos;
            if (desired.sqrMagnitude < 0.04f) return;
            desired.Normalize();

            // Self-trail lookahead avoidance (AvoidTrail)
            Vector2 lookAhead = pos + desired * 1.45f;
            if (_zoneManager.CollisionWorld.TrailOverlapsCircle(lookAhead, 0.85f, bot.PlayerId, ignoreOwnTrail: false, out var hit)
                && hit.TrailOwnerId == bot.PlayerId)
            {
                // Steer sharply perpendicular to avoid crossing own trail
                Vector2 perpL = new Vector2(-desired.y, desired.x);
                Vector2 perpR = new Vector2(desired.y, -desired.x);
                Vector2 toHome = (GetClosestOwnZonePosition(bot) - pos).normalized;
                desired = Vector2.Dot(perpL, toHome) >= Vector2.Dot(perpR, toHome) ? perpL : perpR;
                agent.State = AIControllerState.AvoidingTrail;
            }

            bot.DesiredHeading = desired;
        }

        private Vector2 GetClosestOwnZonePosition(PlayerBase bot)
        {
            if (bot.OwnedZone == null || bot.OwnedZone.Points.Count < 3)
                return Vector2.zero;
            ZoneUtils.ClosestPointOnPolygonWithNormal(bot.OwnedZone.Points, bot.Position2D, out Vector2 closest, out _, out _);
            return Vector2.Lerp(closest, bot.OwnedZone.Centroid, 0.25f);
        }

        private bool HasEnemyNearOwnTrail(PlayerBase bot, float range)
        {
            if (!bot.HasActiveTrail) return false;
            float rangeSq = range * range;
            var players = _zoneManager.Players;
            var trail = bot.TrailPoints;

            for (int i = 0; i < players.Count; i++)
            {
                var enemy = players[i];
                if (enemy == null || !enemy.IsAlive || enemy.PlayerId == bot.PlayerId) continue;
                if (_zoneManager.IsSameTeam(bot.PlayerId, enemy.PlayerId)) continue;

                // Sample every 3rd trail point for speed
                for (int t = 0; t < trail.Count; t += 3)
                {
                    if ((enemy.Position2D - trail[t]).sqrMagnitude <= rangeSq)
                        return true;
                }
            }
            return false;
        }

        private bool FindAttackableEnemyTrail(PlayerBase bot, BotProfileModel profile, out int targetId, out Vector2 bestPoint)
        {
            targetId = -1;
            bestPoint = Vector2.zero;
            float bestDistSq = profile.Perception.EnemyDetectionRange * profile.Perception.EnemyDetectionRange;
            var players = _zoneManager.Players;

            for (int i = 0; i < players.Count; i++)
            {
                var enemy = players[i];
                if (enemy == null || !enemy.IsAlive || enemy.PlayerId == bot.PlayerId) continue;
                if (!enemy.HasActiveTrail) continue;
                if (_zoneManager.IsSameTeam(bot.PlayerId, enemy.PlayerId)) continue;
                if (enemy.IsLocalPlayer && profile.Aggression.AlwaysAvoidPlayer) continue;

                var trail = enemy.TrailPoints;
                for (int t = 0; t < trail.Count; t += 2)
                {
                    float d2 = (bot.Position2D - trail[t]).sqrMagnitude;
                    if (d2 < bestDistSq)
                    {
                        bestDistSq = d2;
                        targetId = enemy.PlayerId;
                        bestPoint = trail[t];
                    }
                }
            }
            return targetId >= 0;
        }

        private static Vector2 ClampToSafeArena(Vector2 pt, float safeRadius)
        {
            if (pt.magnitude > safeRadius) return pt.normalized * safeRadius;
            return pt;
        }

        private static BotCategory PickCategoryForDifficulty(float diff01)
        {
            if (diff01 < 0.3f)
                return Random.value < 0.5f ? BotCategory.Passive : BotCategory.BT_AggL_PerL_LoopL;
            if (diff01 < 0.65f)
                return Random.value < 0.5f ? BotCategory.BT_AggM_PerM_LoopM : BotCategory.Strategic;
            return Random.value < 0.5f ? BotCategory.BT_AggH_PerH_LoopH : BotCategory.AggressiveTowardsAll;
        }
    }
}
