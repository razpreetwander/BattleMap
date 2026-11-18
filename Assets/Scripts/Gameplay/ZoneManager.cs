using System;
using System.Collections.Generic;
using BattleMaps.Clipper;
using BattleMaps.CollisionSystem;
using BattleMaps.Signals;
using UnityEngine;

namespace BattleMaps.Gameplay
{
    /// <summary>
    /// Reconstructed from ZoneManager (IL2CPP index 178, 95 methods).
    /// Owns all active players, bots, zones, territory boolean merges/cuts, paint stamps, and leaderboard ranks.
    /// </summary>
    public class ZoneManager : MonoBehaviour
    {
        [Header("Arena Configuration")]
        public float ArenaRadius = 32f;
        public float DefaultSpawnZoneRadius = 2.6f;
        public int MaxBotsCount = 9;
        public bool BorderKillsPlayers = true;

        public CollisionWorld CollisionWorld { get; private set; }
        public PlayerBase MainPlayer { get; private set; }
        public float ArenaTotalArea => CollisionWorld != null ? CollisionWorld.Border.TotalArenaArea() : Mathf.PI * ArenaRadius * ArenaRadius;

        private readonly List<PlayerBase> _players = new(24);
        private readonly Dictionary<int, PlayerBase> _playerById = new(24);
        private readonly List<PlayerBase> _rankedPlayers = new(24);
        private int _nextPlayerId = 1;
        private Transform _zonesRoot;
        private Transform _playersRoot;

        public IReadOnlyList<PlayerBase> Players => _players;
        public IReadOnlyList<PlayerBase> RankedPlayers => _rankedPlayers;

        public event Action<PlayerBase> PlayerSpawn;
        public event Action<PlayerBase> PlayerDespawn;
        public event Action ZonesChanged;

        public void Initialize(CollisionWorld collisionWorld, float arenaRadius, IReadOnlyList<Vector2> customArenaPolygon = null)
        {
            CollisionWorld = collisionWorld;
            ArenaRadius = arenaRadius;
            if (customArenaPolygon != null && customArenaPolygon.Count >= 3)
            {
                CollisionWorld.SetBorderPolyline(customArenaPolygon);
                ArenaRadius = CollisionWorld.Border.Radius;
            }
            else
            {
                CollisionWorld.SetCircularBorder(arenaRadius);
            }

            if (_zonesRoot == null)
            {
                _zonesRoot = new GameObject("Zones").transform;
                _zonesRoot.SetParent(transform, false);
            }
            if (_playersRoot == null)
            {
                _playersRoot = new GameObject("Players").transform;
                _playersRoot.SetParent(transform, false);
            }
        }

        private void ClampToCurrentArena(List<Vector2> pts)
        {
            if (CollisionWorld != null && !CollisionWorld.Border.IsCircular)
                ZoneUtils.ClampZonePointsToPolygonArena(pts, CollisionWorld.Border.Polyline);
            else
                ZoneUtils.ClampZonePointsToArena(pts, ArenaRadius);
        }

        public void ResetAll()
        {
            for (int i = _players.Count - 1; i >= 0; i--)
            {
                var p = _players[i];
                if (p == null) continue;
                p.OnDispose();
                if (p.OwnedZone != null) Destroy(p.OwnedZone.gameObject);
                Destroy(p.gameObject);
            }
            _players.Clear();
            _playerById.Clear();
            _rankedPlayers.Clear();
            MainPlayer = null;
            _nextPlayerId = 1;
            CollisionWorld?.Reset();
            CollisionWorld?.SetCircularBorder(ArenaRadius);
        }

        public PlayerBase GetPlayerByID(int id) => _playerById.TryGetValue(id, out var p) ? p : null;

        public bool IsSameTeam(int playerA, int playerB)
        {
            if (playerA == playerB) return true;
            var a = GetPlayerByID(playerA);
            var b = GetPlayerByID(playerB);
            return a != null && b != null && a.TeamId >= 0 && a.TeamId == b.TeamId;
        }

        public Vector2 GetAvailablePosition(float minDistanceFromPlayers = 6.5f)
        {
            float maxR = Mathf.Max(3f, ArenaRadius - DefaultSpawnZoneRadius - 2f);
            Vector2 best = Vector2.zero;
            if (CollisionWorld != null && !CollisionWorld.Border.IsCircular)
                ZoneUtils.TryFindInteriorPoint(CollisionWorld.Border.Polyline, out best);

            float bestMinDist = -1f;

            for (int attempt = 0; attempt < 48; attempt++)
            {
                Vector2 cand = UnityEngine.Random.insideUnitCircle * maxR;
                if (CollisionWorld != null && CollisionWorld.Border.IsOutside(cand, DefaultSpawnZoneRadius + 0.5f))
                    continue;
                float nearest = float.MaxValue;
                for (int i = 0; i < _players.Count; i++)
                {
                    if (!_players[i].IsAlive) continue;
                    float d = Vector2.Distance(cand, _players[i].Position2D);
                    if (d < nearest) nearest = d;
                }
                if (nearest >= minDistanceFromPlayers) return cand;
                if (nearest > bestMinDist) { bestMinDist = nearest; best = cand; }
            }
            return best;
        }

        public List<Vector2> BuildSpawnZonePoints(Vector2 center, float radius)
        {
            var pts = ZoneUtils.GetPointsOnCircle(center, radius, 20);
            ClampToCurrentArena(pts);
            return pts;
        }

        public PlayerBase SpawnPlayer(SpawnPlayerModel model)
        {
            if (model.PlayerId <= 0) model.PlayerId = _nextPlayerId++;
            if (model.SpawnZoneRadius <= 0.1f) model.SpawnZoneRadius = DefaultSpawnZoneRadius;

            var zoneGo = new GameObject($"Zone_{model.PlayerId}_{model.DisplayName}");
            zoneGo.transform.SetParent(_zonesRoot, false);
            var zone = zoneGo.AddComponent<ZoneBase>();
            zone.Init(model.PlayerId, model.TeamId, model.PrimaryColor, model.ShadowColor, CollisionWorld);

            var spawnPoly = BuildSpawnZonePoints(model.SpawnPosition, model.SpawnZoneRadius);
            CutZonesForSpawn(model.PlayerId, model.TeamId, spawnPoly);
            zone.SetPolygonPoints(spawnPoly, ArenaTotalArea, animateGain: false);
            zone.PlayIntroductionInflateAnimation();

            var playerGo = new GameObject($"Player_{model.PlayerId}_{model.DisplayName}");
            playerGo.transform.SetParent(_playersRoot, false);
            var player = playerGo.AddComponent<PlayerBase>();
            player.Init(model, zone, CollisionWorld, this);

            _players.Add(player);
            _playerById[player.PlayerId] = player;
            if (model.IsLocalPlayer) MainPlayer = player;

            ResortPlayers();
            PlayerSpawn?.Invoke(player);
            ZonesChanged?.Invoke();
            return player;
        }

        public PlayerBase AutoSpawnBot(int teamId = -1, string botCategory = "Default")
        {
            int idx = _nextPlayerId;
            Color primary = teamId >= 0 ? PlayerSkinPalette.GetTeamColor(teamId) : PlayerSkinPalette.GetPrimary(idx);
            var model = new SpawnPlayerModel
            {
                PlayerId = _nextPlayerId++,
                DisplayName = PlayerSkinPalette.BotNames[idx % PlayerSkinPalette.BotNames.Length],
                IsLocalPlayer = false,
                TeamId = teamId,
                PrimaryColor = primary,
                ShadowColor = PlayerSkinPalette.GetShadow(primary),
                TrailColor = PlayerSkinPalette.GetTrail(primary),
                SpawnPosition = GetAvailablePosition(),
                SpawnZoneRadius = DefaultSpawnZoneRadius,
                BotCategoryName = botCategory
            };
            return SpawnPlayer(model);
        }

        public void DeregisterPlayer(PlayerBase player)
        {
            if (player == null) return;
            _players.Remove(player);
            _playerById.Remove(player.PlayerId);
            if (MainPlayer == player) MainPlayer = null;
            PlayerDespawn?.Invoke(player);
            ResortPlayers();
            ZonesChanged?.Invoke();
        }

        /// <summary>
        /// Cuts overlapping opponent territories when a new player spawns (ZoneManager.CutZonesForSpawn).
        /// </summary>
        public void CutZonesForSpawn(int spawnerId, int spawnerTeamId, IReadOnlyList<Vector2> spawnPoly)
        {
            SubtractFromOtherZones(spawnerId, spawnerTeamId, spawnPoly, allowElimination: false);
        }

        /// <summary>
        /// Core BattleMaps territory capture algorithm:
        /// 1. Build closed loop polygon from player's trail + existing zone perimeter.
        /// 2. Union loop with player's existing zone (BattleMapsClipper.UnionNonAlloc).
        /// 3. Clamp to arena boundary & rebuild 3D raised mesh.
        /// 4. Subtract captured territory from all overlapping enemy zones (CutOtherZonesSystem).
        /// 5. Eliminate any enemy whose territory is wiped out or completely enclosed (CheckPlayersInsideCreatedZone).
        /// </summary>
        public float ProcessTrailCapture(PlayerBase capturer, IReadOnlyList<Vector2> trail)
        {
            if (capturer == null || capturer.OwnedZone == null || trail == null || trail.Count < 2)
                return 0f;

            var closurePoly = ZoneUtils.BuildBestTrailClosurePolygon(capturer.OwnedZone.Points, trail);
            if (closurePoly == null || closurePoly.Count < 3)
                return 0f;

            return ApplyZoneUnionAndCut(capturer, closurePoly);
        }

        /// <summary>
        /// Instant shape paint stamp used by Character Powers (PaintCircle, PaintBomb, PaintThrow),
        /// JumpingRamp landing draw, Tunnel emerge draw, and PumpTower capture.
        /// </summary>
        public float ApplyPaintStamp(PlayerBase owner, Vector2 center, float radius, int segments = 16)
        {
            if (owner == null || !owner.IsAlive || owner.OwnedZone == null) return 0f;
            var circle = ZoneUtils.GetPointsOnCircle(center, radius, segments);
            ClampToCurrentArena(circle);
            return ApplyZoneUnionAndCut(owner, circle);
        }

        /// <summary>
        /// Erases painted territory in a circular area (used by PaintEraserBomb power).
        /// </summary>
        public void EraseTerritoryCircle(int attackerId, Vector2 center, float radius)
        {
            var eraser = ZoneUtils.GetPointsOnCircle(center, radius, 16);
            SubtractFromOtherZones(attackerId, -1, eraser, allowElimination: true);
            ResortPlayers();
            ZonesChanged?.Invoke();
        }

        private float ApplyZoneUnionAndCut(PlayerBase owner, List<Vector2> addedPoly)
        {
            float prevArea = owner.OwnedZone.Area;
            var unionBuf = BattleMapsClipper.GetNestedBuffer();
            try
            {
                BattleMapsClipper.UnionNonAlloc(owner.OwnedZone.Points, addedPoly, unionBuf);
                var merged = ZoneUtils.SelectSurvivingZonePiece(unionBuf.Paths, owner.Position2D);
                if (merged == null || merged.Count < 3)
                    return 0f;

                ClampToCurrentArena(merged);
                owner.OwnedZone.SetPolygonPoints(merged, ArenaTotalArea, animateGain: true);
            }
            finally
            {
                BattleMapsClipper.Return(unionBuf);
            }

            float addedArea = Mathf.Max(0f, owner.OwnedZone.Area - prevArea);

            // Cut overlapping enemy territories
            SubtractFromOtherZones(owner.PlayerId, owner.TeamId, owner.OwnedZone.Points, allowElimination: true);

            // Check if any enemy was completely enclosed inside the newly captured territory
            CheckPlayersInsideCreatedZone(owner, addedPoly);

            ResortPlayers();
            ZonesChanged?.Invoke();

            if (addedArea > 0.1f)
            {
                float pctGain = (addedArea / Mathf.Max(1f, ArenaTotalArea)) * 100f;
                EventBus.Shared.Publish(new ZoneCapturedSignal(
                    owner.PlayerId, addedArea, owner.OwnedZone.CapturedPercent01, owner.OwnedZone.Centroid));

                if (owner.IsLocalPlayer && pctGain >= 0.25f)
                {
                    EventBus.Shared.Publish(new FlyingTextSignal(
                        $"+{pctGain:0.0}%",
                        owner.transform.position + Vector3.up * 1.4f,
                        owner.ZoneColor,
                        Mathf.Clamp(0.9f + pctGain * 0.08f, 0.9f, 1.8f)));
                }
            }

            return addedArea;
        }

        private void SubtractFromOtherZones(int attackerId, int attackerTeamId, IReadOnlyList<Vector2> cutterPoly, bool allowElimination)
        {
            var diffBuf = BattleMapsClipper.GetNestedBuffer();
            try
            {
                for (int i = 0; i < _players.Count; i++)
                {
                    var other = _players[i];
                    if (other == null || !other.IsAlive || other.PlayerId == attackerId) continue;
                    if (attackerTeamId >= 0 && other.TeamId == attackerTeamId) continue;
                    if (other.OwnedZone == null || other.OwnedZone.Points.Count < 3) continue;
                    if (!BattleMapsClipper.IsOverlapping(other.OwnedZone.Points, cutterPoly)) continue;

                    float beforeArea = other.OwnedZone.Area;
                    BattleMapsClipper.DifferenceNonAlloc(other.OwnedZone.Points, cutterPoly, diffBuf);

                    Vector2? anchor = other.HasActiveTrail ? other.TrailPoints[0] : (Vector2?)null;
                    var surviving = ZoneUtils.SelectSurvivingZonePiece(diffBuf.Paths, other.Position2D, anchor);

                    if (surviving == null || ZoneUtils.CalculatePolygonArea(surviving) < 0.35f)
                    {
                        if (allowElimination)
                        {
                            var attacker = GetPlayerByID(attackerId);
                            attacker?.RegisterKill();
                            other.Die(attackerId, "ZoneWiped");
                        }
                    }
                    else
                    {
                        other.OwnedZone.SetPolygonPoints(surviving, ArenaTotalArea, animateGain: false);
                        float stolen = Mathf.Max(0f, beforeArea - other.OwnedZone.Area);
                        if (stolen > 0.1f)
                            EventBus.Shared.Publish(new ZoneStolenSignal(attackerId, other.PlayerId, stolen));
                    }
                }
            }
            finally
            {
                BattleMapsClipper.Return(diffBuf);
            }
        }

        private void CheckPlayersInsideCreatedZone(PlayerBase capturer, IReadOnlyList<Vector2> createdPoly)
        {
            for (int i = 0; i < _players.Count; i++)
            {
                var other = _players[i];
                if (other == null || !other.IsAlive || other.PlayerId == capturer.PlayerId) continue;
                if (IsSameTeam(capturer.PlayerId, other.PlayerId)) continue;
                if (other.IsAirborne || other.IsUnderground || other.IsInvincible) continue;

                // If opponent's head is swallowed inside the newly enclosed loop while outside their own zone
                if (other.CurrentZoneStatus != ZoneStatus.InOwnZone && ZoneUtils.ContainsPoint(createdPoly, other.Position2D))
                {
                    capturer.RegisterKill();
                    other.Die(capturer.PlayerId, "Enclosed");
                }
            }
        }

        public void ResortPlayers()
        {
            _rankedPlayers.Clear();
            for (int i = 0; i < _players.Count; i++)
            {
                if (_players[i] != null && _players[i].IsAlive)
                    _rankedPlayers.Add(_players[i]);
            }
            _rankedPlayers.Sort((a, b) => b.GetCapturedPercent01().CompareTo(a.GetCapturedPercent01()));

            for (int i = 0; i < _rankedPlayers.Count; i++)
                _rankedPlayers[i].SetKing(i == 0 && _rankedPlayers[i].GetCapturedPercent01() > 0.005f);
        }

        public int GetPlayerRankById(int playerId)
        {
            for (int i = 0; i < _rankedPlayers.Count; i++)
            {
                if (_rankedPlayers[i].PlayerId == playerId) return i + 1;
            }
            return -1;
        }

        public int AliveBotCount()
        {
            int count = 0;
            for (int i = 0; i < _players.Count; i++)
            {
                if (_players[i] != null && _players[i].IsAlive && !_players[i].IsLocalPlayer)
                    count++;
            }
            return count;
        }

        public float GetTeamCapturedPercent01(int teamId)
        {
            float sum = 0f;
            for (int i = 0; i < _players.Count; i++)
            {
                var p = _players[i];
                if (p != null && p.IsAlive && p.TeamId == teamId)
                    sum += p.GetCapturedPercent01();
            }
            return Mathf.Clamp01(sum);
        }
    }
}
