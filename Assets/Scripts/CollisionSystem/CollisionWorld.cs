using System;
using System.Collections.Generic;
using BattleMaps.Clipper;
using UnityEngine;

namespace BattleMaps.CollisionSystem
{
    /// <summary>
    /// Custom trigger volume registered inside CollisionWorld (pads, power fields, pump towers, revive pickups).
    /// </summary>
    public struct CustomTriggerVolume
    {
        public int TriggerId;
        public int OwnerId;
        public EntityKind Kind;
        public Vector2 Center;
        public float Radius;
        public bool Active;
    }

    /// <summary>
    /// Circular shield or obstacle shell blocking movement/capture.
    /// </summary>
    public struct ObstacleShell
    {
        public int ShellId;
        public int OwnerId;
        public Vector2 Center;
        public float Radius;
        public bool IsShield;
    }

    /// <summary>
    /// Centralized deterministic 2D collision engine reconstructed from
    /// BattleMaps.CollisionSystem.CollisionWorld (index 3078, 57 methods).
    /// </summary>
    public sealed class CollisionWorld
    {
        public readonly EdgeGridWorld TrailGrid = new EdgeGridWorld(4f);
        public readonly ArenaBorder Border = new ArenaBorder();

        private readonly Dictionary<int, HeadPose> _heads = new(32);
        private readonly Dictionary<int, (List<Vector2> Poly, Rect Bounds)> _zones = new(32);
        private readonly Dictionary<int, CustomTriggerVolume> _triggers = new(64);
        private readonly Dictionary<int, ObstacleShell> _shells = new(32);
        private readonly HashSet<long> _activeTriggerPairs = new();
        private readonly HashSet<long> _frameTriggerPairs = new();

        public float NavTrailEdgeRadius { get; set; } = 0.45f;
        public int SelfTrailGraceSegments { get; set; } = 6;

        public event Action<TrailEnterHit> TrailEnter;
        public event Action<int, Vector2, Vector2> HeadMoved;
        public event Action<TriggerContact> TriggerEnter;
        public event Action<TriggerContact> TriggerExit;
        public event Action<int, int, Vector2> HeadToHeadCollision;

        public void Reset()
        {
            TrailGrid.Clear();
            _heads.Clear();
            _zones.Clear();
            _triggers.Clear();
            _shells.Clear();
            _activeTriggerPairs.Clear();
            _frameTriggerPairs.Clear();
        }

        public void SetBorderPolyline(IReadOnlyList<Vector2> points) => Border.SetBorderPolyline(points);
        public void SetCircularBorder(float radius) => Border.SetCircular(radius);

        public void RegisterHead(int ownerId, Vector2 pos, float bodyRadius = 0.5f, float trailHitRadius = 0.45f)
        {
            _heads[ownerId] = new HeadPose
            {
                OwnerId = ownerId,
                Position = pos,
                Velocity = Vector2.zero,
                BodyRadius = bodyRadius,
                TrailHitRadius = trailHitRadius,
                SimulationEnabled = true,
                IgnoreOwnTrail = false,
                IgnoreAllTrails = false,
                IgnoreShieldShells = false,
                IsAirborneOrUnderground = false
            };
        }

        public void RemoveHead(int ownerId)
        {
            _heads.Remove(ownerId);
            ResetOwner(ownerId);
        }

        public void TeleportHead(int ownerId, Vector2 pos)
        {
            if (!_heads.TryGetValue(ownerId, out var h)) return;
            Vector2 prev = h.Position;
            h.Position = pos;
            _heads[ownerId] = h;
            HeadMoved?.Invoke(ownerId, prev, pos);
        }

        public void SetHeadVelocity(int ownerId, Vector2 velocity)
        {
            if (!_heads.TryGetValue(ownerId, out var h)) return;
            h.Velocity = velocity;
            _heads[ownerId] = h;
        }

        public void SetHeadRadii(int ownerId, float bodyRadius, float trailHitRadius)
        {
            if (!_heads.TryGetValue(ownerId, out var h)) return;
            h.BodyRadius = bodyRadius;
            h.TrailHitRadius = trailHitRadius;
            _heads[ownerId] = h;
        }

        public void SetHeadFlags(int ownerId, bool ignoreOwnTrail, bool ignoreAllTrails, bool airborneOrUnderground)
        {
            if (!_heads.TryGetValue(ownerId, out var h)) return;
            h.IgnoreOwnTrail = ignoreOwnTrail;
            h.IgnoreAllTrails = ignoreAllTrails;
            h.IsAirborneOrUnderground = airborneOrUnderground;
            _heads[ownerId] = h;
        }

        public void SetHeadIgnoreShieldShells(int ownerId, bool ignore)
        {
            if (!_heads.TryGetValue(ownerId, out var h)) return;
            h.IgnoreShieldShells = ignore;
            _heads[ownerId] = h;
        }

        public void SetHeadSimulationEnabled(int ownerId, bool enabled)
        {
            if (!_heads.TryGetValue(ownerId, out var h)) return;
            h.SimulationEnabled = enabled;
            _heads[ownerId] = h;
        }

        public void AppendSegment(int ownerId, Vector2 a, Vector2 b) => TrailGrid.AppendSegment(ownerId, a, b);
        public void ResetOwner(int ownerId) => TrailGrid.ResetOwner(ownerId);

        public void RebuildZone(int ownerId, IReadOnlyList<Vector2> polygon)
        {
            if (polygon == null || polygon.Count < 3)
            {
                _zones.Remove(ownerId);
                return;
            }
            var copy = new List<Vector2>(polygon);
            _zones[ownerId] = (copy, ZoneUtils.CalculatePolygonBounds(copy));
        }

        public void RemoveZone(int ownerId) => _zones.Remove(ownerId);

        public bool IsPointInZone(int ownerId, Vector2 point)
        {
            if (!_zones.TryGetValue(ownerId, out var z)) return false;
            return ZoneUtils.IsPointInsideFast(z.Poly, z.Bounds, point);
        }

        public int OverlapPointOwner(Vector2 point)
        {
            foreach (var kv in _zones)
            {
                if (ZoneUtils.IsPointInsideFast(kv.Value.Poly, kv.Value.Bounds, point))
                    return kv.Key;
            }
            return -1;
        }

        public void SetTrigger(CustomTriggerVolume volume) => _triggers[volume.TriggerId] = volume;
        public void RemoveTrigger(int triggerId) => _triggers.Remove(triggerId);

        public void AddObstacleShell(ObstacleShell shell) => _shells[shell.ShellId] = shell;
        public void RemoveObstacleShell(int shellId) => _shells.Remove(shellId);

        public bool IsBlockedByShell(int ownerId, Vector2 pos, float radius, out Vector2 pushOutNormal)
        {
            pushOutNormal = Vector2.zero;
            if (_heads.TryGetValue(ownerId, out var head) && head.IgnoreShieldShells) return false;
            foreach (var kv in _shells)
            {
                var s = kv.Value;
                if (s.IsShield && s.OwnerId == ownerId) continue;
                Vector2 delta = pos - s.Center;
                float minDist = s.Radius + radius;
                if (delta.sqrMagnitude < minDist * minDist)
                {
                    pushOutNormal = delta.sqrMagnitude > 1e-6f ? delta.normalized : Vector2.right;
                    return true;
                }
            }
            return false;
        }

        public bool TrailOverlapsCircle(Vector2 center, float radius, int queryingOwnerId, bool ignoreOwnTrail, out TrailEnterHit hit)
            => TrailGrid.QueryTrailOverlapCircleFirst(center, radius, queryingOwnerId, ignoreOwnTrail, SelfTrailGraceSegments, out hit);

        /// <summary>
        /// Runs per-tick sensing for trails, head-to-head collisions, and trigger enter/exit events.
        /// </summary>
        public void Tick(float deltaTime)
        {
            SenseTrails();
            SenseHeadCollisions();
            SenseTriggers();
        }

        private void SenseTrails()
        {
            var headList = new List<HeadPose>(_heads.Values);
            for (int i = 0; i < headList.Count; i++)
            {
                var h = headList[i];
                if (!h.SimulationEnabled || h.IgnoreAllTrails || h.IsAirborneOrUnderground) continue;

                if (TrailGrid.QueryTrailOverlapCircleFirst(
                        h.Position, h.TrailHitRadius, h.OwnerId, h.IgnoreOwnTrail, SelfTrailGraceSegments, out var hit))
                {
                    TrailEnter?.Invoke(hit);
                }
            }
        }

        private void SenseHeadCollisions()
        {
            var headList = new List<HeadPose>(_heads.Values);
            for (int i = 0; i < headList.Count; i++)
            {
                var a = headList[i];
                if (!a.SimulationEnabled || a.IsAirborneOrUnderground) continue;

                for (int j = i + 1; j < headList.Count; j++)
                {
                    var b = headList[j];
                    if (!b.SimulationEnabled || b.IsAirborneOrUnderground) continue;

                    float r = a.BodyRadius + b.BodyRadius;
                    if ((a.Position - b.Position).sqrMagnitude <= r * r)
                    {
                        HeadToHeadCollision?.Invoke(a.OwnerId, b.OwnerId, (a.Position + b.Position) * 0.5f);
                    }
                }
            }
        }

        private void SenseTriggers()
        {
            _frameTriggerPairs.Clear();
            var heads = new List<HeadPose>(_heads.Values);
            var triggers = new List<CustomTriggerVolume>(_triggers.Values);

            for (int i = 0; i < heads.Count; i++)
            {
                var h = heads[i];
                if (!h.SimulationEnabled) continue;

                for (int j = 0; j < triggers.Count; j++)
                {
                    var t = triggers[j];
                    if (!t.Active) continue;

                    float r = h.BodyRadius + t.Radius;
                    if ((h.Position - t.Center).sqrMagnitude <= r * r)
                    {
                        long pairKey = ((long)h.OwnerId << 32) | (uint)t.TriggerId;
                        _frameTriggerPairs.Add(pairKey);
                        if (_activeTriggerPairs.Add(pairKey))
                        {
                            TriggerEnter?.Invoke(new TriggerContact(h.OwnerId, t.TriggerId, h.Position));
                        }
                    }
                }
            }

            // Fire TriggerExit for pairs that stopped overlapping
            if (_activeTriggerPairs.Count > 0)
            {
                var toRemove = new List<long>();
                foreach (var pairKey in _activeTriggerPairs)
                {
                    if (!_frameTriggerPairs.Contains(pairKey))
                        toRemove.Add(pairKey);
                }
                for (int i = 0; i < toRemove.Count; i++)
                {
                    long k = toRemove[i];
                    _activeTriggerPairs.Remove(k);
                    int headId = (int)(k >> 32);
                    int trigId = (int)(k & 0xFFFFFFFFL);
                    Vector2 pos = _heads.TryGetValue(headId, out var h) ? h.Position : Vector2.zero;
                    TriggerExit?.Invoke(new TriggerContact(headId, trigId, pos));
                }
            }
        }
    }
}
